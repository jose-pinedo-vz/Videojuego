using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

/// <summary>
/// Ragdoll que se arma en tiempo de ejecucion sobre los huesos del modelo (SkinnedMeshRenderer.bones),
/// asi funciona con los 4 personajes sin tocar los prefabs.
///
/// Fases:
/// 1) Vuelo: sale despedido hacia donde miraba, girando hacia atras (como al resbalar con una cascara). Solo los huesos grandes
///    llevan Rigidbody + collider + CharacterJoint; los chicos (dedos, orejas...) siguen a su padre, asi la malla no se estira.
///    Usa la gravedad del personaje (no la de Physics), friccion en el piso y un amortiguamiento que crece con el tiempo
///    para que vaya perdiendo fuerza hasta quedar tirado de espaldas.
/// 2) Levantarse: cuando queda quieto, el cuerpo se mezcla con la animacion "Stand Up" del lobby (CatalogoPersonajes.clipLevantarse),
///    alineada con donde quedo la cadera, y al terminar el Animator retoma el control.
///
/// Es solo visual/fisico local: lo que se sincroniza es la orden de iniciar (Personaje.IniciarRagdollVisualRpc).
/// Un limite invisible (cajas del mapa) impide que el cuerpo salga del escenario.
/// </summary>
public class RagdollPlastilina : MonoBehaviour
{
    private class Hueso
    {
        public Transform t;
        public Rigidbody rb;
        public SphereCollider col;
        public CharacterJoint unionPadre;
        public Vector3 haciaHijo;
        public Vector3 posLocal;
        public Quaternion rotLocal;
        public Vector3 escLocal;
    }

    // tramo util del clip "Stand Up" de Mixamo (8.3 s en total): del segundo 1.9 (tirado) al 5.9 (ya de pie)
    private const float T_LEVANTAR_INICIO = 1.9f;
    private const float T_LEVANTAR_FIN = 5.9f;
    private const float VELOCIDAD_LEVANTAR = 2.4f; // ~1.7 s en vez de 4 s
    private const float FUNDIDO_LEVANTAR = 0.3f;

    private readonly List<Hueso> huesos = new List<Hueso>();
    private readonly List<Transform> todosLosHuesos = new List<Transform>();
    private readonly List<Quaternion> rotCapturada = new List<Quaternion>();
    private readonly List<SkinnedMeshRenderer> mallas = new List<SkinnedMeshRenderer>();
    private readonly List<bool> mallasFueraPantalla = new List<bool>();

    private Personaje personaje;
    private Animator animator;
    private CharacterController controlador;
    private Hueso raiz;            // la cadera: el hueso del que cuelgan todos los demas
    private Transform huesoCabeza;
    private bool esDuenio;
    private float altura = 1f;
    private float lado = 1f;       // hacia donde sale despedido (+1 / -1)
    private float mira = 1f;       // hacia donde mira el personaje (+1 / -1): cae de espaldas, con la cabeza hacia el lado contrario
    private float gravedadExtra;   // gravedad del personaje menos la de Physics
    private PhysicsMaterial materialPiel;
    private Coroutine rutina;

    private float tInicio;
    private float tiempoQuieto;
    private bool levantandose;
    private bool fundiendo;
    private float inicioFundido;
    private Vector3 hipsMundoCapturado;
    private Transform visual;          // hijo del personaje que contiene el esqueleto: se desplaza solo en pantalla mientras se levanta
    private Vector3 visualLocalOriginal;
    private float desfaseVisualX;
    private float sueloY;              // altura del piso donde se levanta
    private float distPiesRepososY;    // distancia del hueso mas bajo al piso estando de pie (medida al empezar)
    private float progresoLevantar;
    private Vector3 destinoFinal;
    private PlayableGraph grafo;
    private AnimationClipPlayable pClip;
    private RuntimeAnimatorController controladorOriginal;
    private bool rootMotionOriginal;

    private Bounds limites;        // zona del mapa donde puede estar el cuerpo
    private bool hayLimites;
    private float pisoMinimoY;     // si la cadera baja de aqui, se rescata hacia arriba
    private Vector3 puntoSeguro;   // ultima posicion de la cadera dentro del mapa

    public bool Activo { get; private set; }

    // direccion: x = hacia donde sale despedido (signo), z = hacia donde mira el personaje (signo; si es 0 se usa x), duracion = maximo de tiempo tirado
    public void Iniciar(Personaje quien, Animator anim, CharacterController cc, Vector3 direccion, float duracion, bool duenio)
    {
        if (Activo) return;

        personaje = quien;
        animator = anim;
        controlador = cc;
        esDuenio = duenio;
        lado = direccion.x < 0f ? -1f : 1f;
        mira = Mathf.Abs(direccion.z) > 0.01f ? Mathf.Sign(direccion.z) : lado;
        gravedadExtra = Mathf.Max(0f, personaje.Gravedad - Mathf.Abs(Physics.gravity.y));

        if (!ConstruirHuesos()) return; // sin esqueleto no hay nada que soltar

        Activo = true;
        levantandose = false;
        fundiendo = false;
        tInicio = Time.time;
        tiempoQuieto = 0f;
        if (animator != null) animator.enabled = false;
        if (controlador != null) controlador.enabled = false;

        // las mallas con huesos sueltos no deben desaparecer por salirse de su caja de culling
        foreach (SkinnedMeshRenderer m in mallas)
        {
            mallasFueraPantalla.Add(m.updateWhenOffscreen);
            m.updateWhenOffscreen = true;
        }

        float minY0 = float.MaxValue;
        foreach (Transform b in todosLosHuesos) if (b.position.y < minY0) minY0 = b.position.y;
        distPiesRepososY = Mathf.Max(0f, minY0 - personaje.transform.position.y); // de pie: lo mas bajo del esqueleto sobre el piso

        CalcularLimites();
        puntoSeguro = raiz.t.position;

        // resbalon: sale volando hacia adelante y arriba, y gira hacia atras para caer de espaldas.
        // Todos los huesos salen con la MISMA velocidad: si no, unos se adelantan a otros y se estira la malla
        float vx = lado * altura * 10f;
        float vy = altura * 13f;
        foreach (Hueso h in huesos)
        {
            h.rb.linearVelocity = new Vector3(vx, vy, 0f);
            float extra = h == raiz ? 0f : Random.Range(-4f, 4f); // brazos y piernas se agitan
            h.rb.angularVelocity = new Vector3(0f, 0f, mira * 2.5f + extra);
        }

        rutina = StartCoroutine(Esperar(duracion));
    }

    private IEnumerator Esperar(float duracion)
    {
        yield return new WaitForSeconds(duracion);
        Terminar(true);
    }

    private void FixedUpdate()
    {
        if (!Activo || levantandose || raiz == null || raiz.rb == null) return;

        float t = Time.time - tInicio;

        // pierde fuerza: al principio casi sin freno, despues se va frenando hasta quedar tirado
        float k = Mathf.Clamp01((t - 0.5f) / 1.5f);
        float amortiguaLineal = Mathf.Lerp(0.15f, 2.5f, k);
        float amortiguaAngular = Mathf.Lerp(0.3f, 6f, k);

        Vector3 sumaVel = Vector3.zero;
        foreach (Hueso h in huesos)
        {
            // la gravedad del juego es mayor que la de Physics (Personaje.gravedad): se completa aqui
            if (gravedadExtra > 0f) h.rb.AddForce(Vector3.down * gravedadExtra, ForceMode.Acceleration);
            h.rb.linearDamping = amortiguaLineal;
            h.rb.angularDamping = amortiguaAngular;
            sumaVel += h.rb.linearVelocity;
        }
        float velPromedio = (sumaVel / huesos.Count).magnitude;

        // ayuda para caer de espaldas: empuja el giro de la cadera hacia 95 grados hacia atras mientras no llega
        if (huesoCabeza != null && t < 1.2f)
        {
            Vector3 eje = huesoCabeza.position - raiz.t.position;
            float a = mira * Vector2.SignedAngle(Vector2.up, new Vector2(eje.x, eje.y)); // + = inclinado hacia atras
            if (a < 95f) raiz.rb.AddTorque(Vector3.forward * mira * 9f * Mathf.Clamp((95f - a) / 95f, -1f, 1f), ForceMode.Acceleration);
        }

        // se contiene dentro del mapa
        Contener();

        // cuando ya casi no se mueve, se levanta (no antes de 0.8 s para que alcance a caer)
        if (t > 0.8f && velPromedio < 0.6f && raiz.rb.angularVelocity.magnitude < 3f) tiempoQuieto += Time.fixedDeltaTime;
        else tiempoQuieto = 0f;
        if (tiempoQuieto > 0.35f) Terminar(true);
    }

    // Mantiene el cuerpo dentro del mapa: mueve TODOS los huesos juntos para no deformar la malla
    private void Contener()
    {
        Vector3 p = raiz.rb.position;
        Vector3 corregida = p;
        Vector3 velocidad = raiz.rb.linearVelocity;

        if (hayLimites)
        {
            float margen = 0.3f;
            if (p.x < limites.min.x + margen) { corregida.x = limites.min.x + margen; if (velocidad.x < 0f) velocidad.x = 0f; }
            if (p.x > limites.max.x - margen) { corregida.x = limites.max.x - margen; if (velocidad.x > 0f) velocidad.x = 0f; }
            if (p.y > limites.max.y + altura * 2f) { corregida.y = limites.max.y + altura * 2f; if (velocidad.y > 0f) velocidad.y = 0f; }
            if (p.y < pisoMinimoY)
            {
                // se hundio bajo el piso: vuelve al ultimo punto bueno
                corregida = puntoSeguro + Vector3.up * 0.3f;
                velocidad = Vector3.zero;
            }
        }
        // plano 2.5D: solo se corrige la Z si se aleja de verdad (si no, se corregiria en cada paso de fisica)
        float planoZ = personaje.transform.position.z;
        if (Mathf.Abs(p.z - planoZ) > 0.15f) corregida.z = planoZ;

        if ((corregida - p).sqrMagnitude > 0.000001f)
        {
            Vector3 delta = corregida - p;
            foreach (Hueso h in huesos)
            {
                h.rb.position += delta;
                h.rb.linearVelocity = velocidad;
            }
        }
        else
        {
            puntoSeguro = p;
        }
    }

    // zona valida = caja que une los colliders solidos del mapa (sin personajes, armas ni triggers)
    private void CalcularLimites()
    {
        hayLimites = false;
        Bounds b = new Bounds();
        foreach (Collider c in FindObjectsByType<Collider>(FindObjectsSortMode.None))
        {
            if (c == null || !c.enabled || c.isTrigger) continue;
            if (c is CharacterController) continue;
            if (c.GetComponentInParent<Personaje>() != null) continue;
            if (c.GetComponentInParent<Arma>() != null) continue;
            if (c.attachedRigidbody != null && !c.attachedRigidbody.isKinematic) continue;
            if (!hayLimites) { b = c.bounds; hayLimites = true; } else b.Encapsulate(c.bounds);
        }
        limites = b;
        // piso minimo: un poco bajo la superficie donde estaba parado, nunca mas abajo que el mapa
        pisoMinimoY = hayLimites ? Mathf.Max(b.min.y, personaje.transform.position.y - 3f) : personaje.transform.position.y - 3f;
    }

    // levantarse: true = el duenio se levanta donde cayo; false = solo se cancela (ej. murio en pleno ragdoll)
    public void Terminar(bool levantarse)
    {
        if (!Activo) return;

        if (levantandose)
        {
            if (!levantarse) CancelarLevantarse(); // murio mientras se levantaba
            return;
        }

        if (rutina != null) StopCoroutine(rutina);
        rutina = null;

        if (levantarse && PuedeLevantarse())
        {
            IniciarLevantarse();
            return;
        }

        // sin animacion de levantarse: cada hueso vuelve a su pose y el personaje reaparece donde cayo
        Vector3 destino = personaje.transform.position;
        if (levantarse && raiz != null) destino = CalcularLugarDeLevantarse();

        DestruirFisica();
        foreach (Hueso h in huesos)
        {
            if (h.t == null) continue;
            h.t.localPosition = h.posLocal;
            h.t.localRotation = h.rotLocal;
            h.t.localScale = h.escLocal;
        }
        if (animator != null) animator.enabled = true;
        Limpiar();

        personaje.AlTerminarRagdoll(destino, esDuenio && levantarse);
    }

    // los colliders se apagan ya mismo (Destroy tarda al final del cuadro) para no estorbar al raycast ni a nadie
    private void DestruirFisica()
    {
        foreach (Hueso h in huesos)
        {
            if (h.col != null) h.col.enabled = false;
            if (h.unionPadre != null) Destroy(h.unionPadre);
            if (h.col != null) Destroy(h.col);
            if (h.rb != null) Destroy(h.rb);
        }
    }

    private void Limpiar()
    {
        for (int i = 0; i < mallas.Count; i++)
        {
            if (mallas[i] != null) mallas[i].updateWhenOffscreen = mallasFueraPantalla[i];
        }
        Activo = false;
        levantandose = false;
        fundiendo = false;
        huesos.Clear();
        todosLosHuesos.Clear();
        rotCapturada.Clear();
        mallas.Clear();
        mallasFueraPantalla.Clear();
        raiz = null;
        huesoCabeza = null;
        visual = null;
    }

    // ---------------------------------------------------------------- levantarse con la animacion "Stand Up"

    private AnimationClip ClipLevantarse()
    {
        CatalogoPersonajes cat = CatalogoPersonajes.Obtener();
        return cat != null ? cat.clipLevantarse : null;
    }

    private bool PuedeLevantarse()
    {
        return raiz != null && animator != null && animator.avatar != null && animator.avatar.isValid && animator.avatar.isHuman
            && ClipLevantarse() != null;
    }

    private void IniciarLevantarse()
    {
        levantandose = true;
        AnimationClip clip = ClipLevantarse();
        Transform modelo = animator.transform;

        // 1) pose actual del ragdoll, para mezclarla con la animacion y que no pegue un salto
        Transform cadera = raiz.t;
        hipsMundoCapturado = cadera.position;
        rotCapturada.Clear();
        foreach (Transform b in todosLosHuesos) rotCapturada.Add(b.localRotation);
        Vector3 destino = CalcularLugarDeLevantarse(); // piso bajo la cadera (antes de mover nada)
        DestruirFisica();

        // 2) pose inicial de la animacion, para saber donde poner al personaje para que coincida con la cadera
        controladorOriginal = animator.runtimeAnimatorController;
        rootMotionOriginal = animator.applyRootMotion;
        animator.enabled = false;
        animator.runtimeAnimatorController = null;
        animator.applyRootMotion = false;
        clip.SampleAnimation(modelo.gameObject, T_LEVANTAR_INICIO);

        // el esqueleto cuelga de un hijo del personaje: ese hijo se desplaza (solo visual, en este cliente) para que la animacion
        // arranque justo donde quedo la cadera; el personaje en si se mueve al final, cuando ya esta de pie
        visual = cadera;
        while (visual.parent != null && visual.parent != personaje.transform) visual = visual.parent;
        visualLocalOriginal = visual.localPosition;
        sueloY = destino.y - 0.05f;
        desfaseVisualX = hipsMundoCapturado.x - cadera.position.x;
        visual.position += new Vector3(desfaseVisualX, 0f, 0f);
        CorregirAlturaVisual(0f);
        destinoFinal = new Vector3(personaje.transform.position.x + desfaseVisualX, destino.y, destino.z);

        // 3) reproducir el clip (solo el tramo en que se levanta)
        grafo = PlayableGraph.Create("RagdollLevantarse");
        grafo.SetTimeUpdateMode(DirectorUpdateMode.GameTime);
        AnimationPlayableOutput salida = AnimationPlayableOutput.Create(grafo, "Salida", animator);
        pClip = AnimationClipPlayable.Create(grafo, clip);
        pClip.SetTime(T_LEVANTAR_INICIO);
        pClip.SetSpeed(VELOCIDAD_LEVANTAR);
        salida.SetSourcePlayable(pClip);
        animator.enabled = true;
        grafo.Play();

        fundiendo = true;
        inicioFundido = Time.time;
        rutina = StartCoroutine(EsperarLevantarse());
    }

    private IEnumerator EsperarLevantarse()
    {
        float limite = Time.time + (T_LEVANTAR_FIN - T_LEVANTAR_INICIO) / VELOCIDAD_LEVANTAR + 1f; // por si el grafo se detiene
        while (grafo.IsValid() && pClip.GetTime() < T_LEVANTAR_FIN && Time.time < limite) yield return null;
        rutina = null;

        DetenerGrafo();
        if (visual != null) visual.localPosition = visualLocalOriginal;
        Vector3 destino = destinoFinal;
        Limpiar();
        personaje.AlTerminarRagdoll(destino, esDuenio);
    }

    private void CancelarLevantarse()
    {
        if (rutina != null) StopCoroutine(rutina);
        rutina = null;
        DetenerGrafo();
        if (visual != null) visual.localPosition = visualLocalOriginal;
        Limpiar();
        personaje.AlTerminarRagdoll(personaje.transform.position, false);
    }

    private void DetenerGrafo()
    {
        if (grafo.IsValid()) grafo.Destroy();
        if (animator != null)
        {
            animator.runtimeAnimatorController = controladorOriginal;
            animator.applyRootMotion = rootMotionOriginal;
            animator.enabled = true;
        }
    }

    // despues de que el Animator aplica la pose: se mezcla con la pose del ragdoll durante el primer momento
    private void LateUpdate()
    {
        if (!Activo || !levantandose) return;
        if (fundiendo) FundirConPoseRagdoll();
        double tt = grafo.IsValid() ? pClip.GetTime() : T_LEVANTAR_FIN;
        progresoLevantar = Mathf.Clamp01((float)((tt - T_LEVANTAR_INICIO) / (T_LEVANTAR_FIN - T_LEVANTAR_INICIO)));
        CorregirAlturaVisual(progresoLevantar);
    }

    private void FundirConPoseRagdoll()
    {

        float s = Mathf.Clamp01((Time.time - inicioFundido) / FUNDIDO_LEVANTAR);
        s = s * s * (3f - 2f * s);
        for (int i = 0; i < todosLosHuesos.Count && i < rotCapturada.Count; i++)
        {
            Transform b = todosLosHuesos[i];
            if (b != null) b.localRotation = Quaternion.Slerp(rotCapturada[i], b.localRotation, s);
        }
        if (raiz != null && raiz.t != null) raiz.t.position = Vector3.Lerp(hipsMundoCapturado, raiz.t.position, s);
        if (s >= 1f) fundiendo = false;
    }

    // el clip no trae el movimiento vertical del cuerpo (sin root motion): se mantiene el punto mas bajo sobre el piso,
    // igual que hace AnimacionLobby; la distancia al piso pasa de "tirado" a "de pie" segun avanza la animacion
    private void CorregirAlturaVisual(float progreso)
    {
        if (visual == null) return;
        float minY = float.MaxValue;
        foreach (Transform b in todosLosHuesos) if (b != null && b.position.y < minY) minY = b.position.y;
        float distancia = Mathf.Lerp(altura * 0.04f, distPiesRepososY, Mathf.SmoothStep(0f, 1f, progreso));
        visual.position += new Vector3(0f, sueloY + distancia - minY, 0f);
    }

    // donde se pone el personaje al levantarse: bajo la cadera, sobre el piso, en el plano de juego
    private Vector3 CalcularLugarDeLevantarse()
    {
        Vector3 cadera = raiz.t.position;
        float planoZ = personaje.transform.position.z;

        float y = personaje.transform.position.y;
        RaycastHit[] golpes = Physics.RaycastAll(cadera + Vector3.up * altura * 0.5f, Vector3.down, 30f, ~0, QueryTriggerInteraction.Ignore);
        float mejor = float.MaxValue;
        foreach (RaycastHit g in golpes)
        {
            if (g.collider == null) continue;
            if (g.collider.GetComponentInParent<Personaje>() != null) continue;
            if (g.collider.GetComponentInParent<Arma>() != null) continue;
            if (g.distance < mejor) { mejor = g.distance; y = g.point.y + 0.05f; }
        }
        return new Vector3(cadera.x, y, planoZ);
    }

    // ---------------------------------------------------------------- armado del cuerpo

    private bool ConstruirHuesos()
    {
        huesos.Clear();
        todosLosHuesos.Clear();
        mallas.Clear();
        mallasFueraPantalla.Clear();

        if (materialPiel == null)
        {
            materialPiel = new PhysicsMaterial("RagdollPiel")
            {
                dynamicFriction = 0.7f,
                staticFriction = 0.8f,
                bounciness = 0.1f,
                frictionCombine = PhysicsMaterialCombine.Maximum,
                bounceCombine = PhysicsMaterialCombine.Average
            };
        }

        // todos los huesos que usan las mallas del cuerpo (sin armas)
        HashSet<Transform> conjunto = new HashSet<Transform>();
        Bounds caja = new Bounds();
        bool primera = true;
        foreach (SkinnedMeshRenderer smr in personaje.GetComponentsInChildren<SkinnedMeshRenderer>(false))
        {
            if (smr.GetComponentInParent<Arma>() != null) continue;
            mallas.Add(smr);
            foreach (Transform b in smr.bones) if (b != null) conjunto.Add(b);
            if (primera) { caja = smr.bounds; primera = false; } else caja.Encapsulate(smr.bounds);
        }
        if (conjunto.Count < 3) return false;
        altura = Mathf.Max(0.2f, caja.size.y);

        // con avatar humanoide los huesos principales (cadera, columna, cabeza, brazos, piernas) siempre llevan fisica,
        // aunque midan poco: si la cadera no tuviera cuerpo, las piernas quedarian sueltas
        HashSet<Transform> principales = new HashSet<Transform>();
        if (animator != null && animator.avatar != null && animator.avatar.isValid && animator.avatar.isHuman)
        {
            HumanBodyBones[] nucleo = {
                HumanBodyBones.Hips, HumanBodyBones.Spine, HumanBodyBones.Chest, HumanBodyBones.Head,
                HumanBodyBones.LeftUpperArm, HumanBodyBones.LeftLowerArm, HumanBodyBones.RightUpperArm, HumanBodyBones.RightLowerArm,
                HumanBodyBones.LeftUpperLeg, HumanBodyBones.LeftLowerLeg, HumanBodyBones.RightUpperLeg, HumanBodyBones.RightLowerLeg };
            foreach (HumanBodyBones hb in nucleo)
            {
                Transform t = animator.GetBoneTransform(hb);
                if (t == null) continue;
                conjunto.Add(t);
                principales.Add(t);
            }
        }
        todosLosHuesos.AddRange(conjunto);

        // elegir los huesos con largo suficiente: dedos y puntas sueltas se quedan pegados al hueso padre
        List<Transform> elegidos = new List<Transform>();
        foreach (Transform b in conjunto)
        {
            if (principales.Contains(b) || LargoDelHueso(b, conjunto) >= altura * 0.12f) elegidos.Add(b); // los grandes; el resto sigue a su padre
        }
        if (elegidos.Count < 3)
        {
            elegidos.Clear();
            foreach (Transform b in conjunto) if (LargoDelHueso(b, conjunto) >= altura * 0.06f) elegidos.Add(b);
        }
        if (elegidos.Count < 3) return false;

        HashSet<Transform> setElegidos = new HashSet<Transform>(elegidos);
        Dictionary<Transform, Hueso> porTransform = new Dictionary<Transform, Hueso>();

        foreach (Transform b in elegidos)
        {
            Hueso h = new Hueso
            {
                t = b,
                posLocal = b.localPosition,
                rotLocal = b.localRotation,
                escLocal = b.localScale
            };

            float largo = LargoDelHueso(b, conjunto);
            h.haciaHijo = DireccionAHijos(b, conjunto);

            h.rb = b.gameObject.AddComponent<Rigidbody>();
            h.rb.mass = Mathf.Max(1f, largo * 4f);
            h.rb.solverIterations = 30;
            h.rb.solverVelocityIterations = 15;
            h.rb.maxAngularVelocity = 12f;
            h.rb.linearDamping = 0.15f;
            h.rb.angularDamping = 0.3f;
            h.rb.constraints = RigidbodyConstraints.FreezePositionZ; // juego 2.5D, no se sale del plano
            h.rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

            h.col = b.gameObject.AddComponent<SphereCollider>();
            h.col.radius = Mathf.Max(altura * 0.035f, largo * 0.4f) / Mathf.Max(0.0001f, Mathf.Abs(b.lossyScale.x));
            h.col.center = b.InverseTransformPoint(b.position + h.haciaHijo * largo * 0.5f);
            h.col.sharedMaterial = materialPiel;

            huesos.Add(h);
            porTransform[b] = h;
        }

        // uniones: cada hueso se cuelga del ancestro elegido mas cercano
        foreach (Hueso h in huesos)
        {
            Transform padre = h.t.parent;
            while (padre != null && !setElegidos.Contains(padre)) padre = padre.parent;

            if (padre == null)
            {
                if (raiz == null) raiz = h; // la primera sin ancestro es la cadera
                continue;
            }

            CharacterJoint j = h.t.gameObject.AddComponent<CharacterJoint>();
            j.connectedBody = porTransform[padre].rb;
            j.enablePreprocessing = false;
            j.enableProjection = true;
            j.projectionDistance = 0.005f; // si el hueso se separa de su articulacion, se vuelve a pegar
            j.projectionAngle = 5f;
            j.enableCollision = false;
            // el giro (twist) es sobre el propio hueso, como en una persona
            j.axis = h.t.InverseTransformDirection(h.haciaHijo).normalized;
            Vector3 perp = Vector3.Cross(h.haciaHijo, Vector3.forward);
            if (perp.sqrMagnitude < 0.01f) perp = Vector3.Cross(h.haciaHijo, Vector3.up);
            j.swingAxis = h.t.InverseTransformDirection(perp.normalized).normalized;
            j.lowTwistLimit = new SoftJointLimit { limit = -35f };
            j.highTwistLimit = new SoftJointLimit { limit = 35f };
            j.swing1Limit = new SoftJointLimit { limit = 60f };
            j.swing2Limit = new SoftJointLimit { limit = 45f };
            h.unionPadre = j;
        }
        if (raiz == null) raiz = huesos[0];

        // con avatar humanoide la cadera y la cabeza son conocidas
        if (animator != null && animator.avatar != null && animator.avatar.isValid && animator.avatar.isHuman)
        {
            Transform caderaHumana = animator.GetBoneTransform(HumanBodyBones.Hips);
            if (caderaHumana != null && porTransform.ContainsKey(caderaHumana)) raiz = porTransform[caderaHumana];
            huesoCabeza = animator.GetBoneTransform(HumanBodyBones.Head);
        }
        // la cadera no gira sobre X ni Y: se queda de lado a la camara (juego 2.5D) y solo cae girando sobre Z
        raiz.rb.constraints = RigidbodyConstraints.FreezePositionZ | RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationY;
        raiz.rb.mass *= 2f;

        // el ragdoll no choca con el propio CharacterController ni con otros jugadores
        List<Collider> ajenos = new List<Collider>();
        foreach (CharacterController otro in FindObjectsByType<CharacterController>(FindObjectsSortMode.None)) ajenos.Add(otro);
        foreach (Hueso h in huesos)
        {
            foreach (Collider c in ajenos) if (c != null) Physics.IgnoreCollision(h.col, c, true);
        }
        // y los huesos entre si no chocan (las articulaciones ya los mantienen juntos)
        for (int i = 0; i < huesos.Count; i++)
            for (int k = i + 1; k < huesos.Count; k++)
                Physics.IgnoreCollision(huesos[i].col, huesos[k].col, true);

        return true;
    }

    // distancia promedio del hueso a sus hijos que tambien son huesos; las puntas usan el largo hacia su padre
    private static float LargoDelHueso(Transform b, HashSet<Transform> conjunto)
    {
        float suma = 0f;
        int n = 0;
        foreach (Transform hijo in b)
        {
            if (!conjunto.Contains(hijo)) continue;
            suma += Vector3.Distance(b.position, hijo.position);
            n++;
        }
        if (n > 0) return suma / n;
        return b.parent != null ? Vector3.Distance(b.position, b.parent.position) * 0.8f : 0f;
    }

    private static Vector3 DireccionAHijos(Transform b, HashSet<Transform> conjunto)
    {
        Vector3 suma = Vector3.zero;
        int n = 0;
        foreach (Transform hijo in b)
        {
            if (!conjunto.Contains(hijo)) continue;
            suma += (hijo.position - b.position).normalized;
            n++;
        }
        if (n > 0 && suma.sqrMagnitude > 0.0001f) return suma.normalized;
        if (b.parent != null) return (b.position - b.parent.position).normalized; // punta: sigue la direccion del padre
        return Vector3.up;
    }
}
