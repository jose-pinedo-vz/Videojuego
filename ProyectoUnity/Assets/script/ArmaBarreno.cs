using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

/// <summary>
/// Barreno:
/// - Paquete de 3 barrenos.
/// - Al lanzarse con V tiene un efecto de area medio capaz de matar a los enemigos INCLUYENDO al portador.
/// - Si alguien le dispara al portador mientras porta barrenos activos, estos explotan inmediatamente.
/// </summary>
public class ArmaBarreno : Arma
{
    [Header("Configuracion de Barreno")]
    [SerializeField] private float radioExplosion = 4.4f;
    [SerializeField] private float tiempoMecha = 1.75f;
    [SerializeField] private float velocidadLanzamientoX = 9.5f;
    [SerializeField] private float velocidadLanzamientoY = 5.2f;

    private bool detonandoEnPortador;

    protected override void Start()
    {
        if (municionMaxima <= 0)
        {
            municionMaxima = 3;
            municionActual = 3;
        }
        if (Cadencia <= 0f) Cadencia = 1.4f;
        if (tiempoRecarga <= 0f) tiempoRecarga = 1.6f;
        base.Start();
        ActualizarVisualPaquete();
    }

    protected override void ActualizarVisualMunicion()
    {
        base.ActualizarVisualMunicion();
        ActualizarVisualPaquete();
    }

    protected override void AlCompletarRecarga()
    {
        base.AlCompletarRecarga();
        ActualizarVisualPaquete();
    }

    private void ActualizarVisualPaquete()
    {
        if (enCaja) return;
        Transform t1 = transform.Find("BarrenoExtra_1");
        Transform t2 = transform.Find("BarrenoExtra_2");
        if (t1 != null) t1.gameObject.SetActive(municionActual >= 2);
        if (t2 != null) t2.gameObject.SetActive(municionActual >= 3);

        // Si ya lanzo los 3 barrenos y no tiene ninguno en la mano, atenuar el modelo principal hasta que recargue con caja de municion
        MeshRenderer[] mrs = GetComponentsInChildren<MeshRenderer>(true);
        foreach (MeshRenderer mr in mrs)
        {
            if (mr == null || mr.gameObject.name == "CajaMaderaVisual") continue;
            if (mr.transform.IsChildOf(t1 != null ? t1 : transform) && t1 != null && mr.transform != transform && mr.transform.parent == t1) continue;
            if (mr.transform.IsChildOf(t2 != null ? t2 : transform) && t2 != null && mr.transform != transform && mr.transform.parent == t2) continue;
            mr.enabled = !enCaja && municionActual > 0;
        }
    }

    public override void Disparar()
    {
        if (enCaja)
        {
            base.Disparar();
            ActualizarVisualPaquete();
            return;
        }

        if (recargando || jugadorQueLaEquipo == null) return;
        if (municionActual <= 0)
        {
            Sonidos.Reproducir("sin_municion", transform.position, 0.85f);
            return;
        }

        if (Time.time < tiempoUltimoDisparo + (1f / Mathf.Max(0.2f, Cadencia)))
            return;

        tiempoUltimoDisparo = Time.time;
        municionActual--;
        ContadorDisparos++;
        ActualizarVisualPaquete();

        Personaje lanzador = jugadorQueLaEquipo;
        float dirX = lanzador.DireccionHorizontal;
        // sale de la mano (donde esta el modelo del arma); el modelo de la mano se queda
        Vector3 origen = CentroBarrenoMano();
        if (origen == Vector3.zero || (origen - lanzador.transform.position).sqrMagnitude > 9f)
            origen = lanzador.transform.position + new Vector3(dirX * 0.55f, 1.1f, 0f);
        Vector3 velocidadInicial = new Vector3(dirX * velocidadLanzamientoX, velocidadLanzamientoY, 0f);

        UltimoInicioTrazo = origen;
        UltimoFinTrazo = origen + velocidadInicial.normalized * 1.2f;

        if (lanzador.IsSpawned && NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
        {
            lanzador.NotificarCambioMunicionRed(this);
            lanzador.LanzarBarrenoRpc(origen, velocidadInicial);
        }
        else
        {
            InstanciarBarrenoLanzado(lanzador, origen, velocidadInicial, true, radioExplosion, tiempoMecha);
        }
    }

    // piezas del paquete que NO son el barreno principal
    private static bool EsPiezaExtra(string nombre)
    {
        return nombre.StartsWith("BarrenoExtra") || nombre.StartsWith("Cinta") || nombre.StartsWith("Mecha") || nombre == "CajaMaderaVisual";
    }

    private static bool EsPiezaExtra(Transform t, Transform raiz)
    {
        for (; t != null && t != raiz; t = t.parent)
            if (EsPiezaExtra(t.name)) return true;
        return false;
    }

    // centro visual del barreno principal que sostiene el jugador
    public Vector3 CentroBarrenoMano()
    {
        bool hay = false;
        Bounds b = new Bounds(transform.position, Vector3.zero);
        foreach (MeshRenderer mr in GetComponentsInChildren<MeshRenderer>(true))
        {
            if (mr == null || EsPiezaExtra(mr.transform, transform)) continue;
            if (!hay) { b = mr.bounds; hay = true; } else b.Encapsulate(mr.bounds);
        }
        return hay ? b.center : transform.position;
    }

    // el modelo que se ve en la mano son los cilindros rojos del paquete (la malla del .fbx es diminuta):
    // se copia uno con su mismo tamaño y material, mas cinta y mecha
    private GameObject DuplicarCilindroDelPaquete(Vector3 posicion)
    {
        Transform origen = transform.Find("BarrenoExtra_1");
        if (origen == null) origen = transform.Find("BarrenoExtra_2");
        if (origen == null) return null;
        MeshFilter mf = origen.GetComponent<MeshFilter>();
        MeshRenderer mr = origen.GetComponent<MeshRenderer>();
        if (mf == null || mr == null || mf.sharedMesh == null) return null;

        GameObject raiz = new GameObject("BarrenoProyectil");
        raiz.transform.position = posicion;

        GameObject cuerpo = new GameObject("Cuerpo");
        cuerpo.transform.SetParent(raiz.transform, false);
        cuerpo.transform.rotation = origen.rotation;
        cuerpo.transform.localScale = origen.lossyScale;
        cuerpo.AddComponent<MeshFilter>().sharedMesh = mf.sharedMesh;
        cuerpo.AddComponent<MeshRenderer>().sharedMaterials = mr.sharedMaterials;

        Material matCinta = new Material(mr.sharedMaterial);
        matCinta.color = new Color(0.12f, 0.12f, 0.13f);
        Material matMecha = new Material(mr.sharedMaterial);
        matMecha.color = new Color(0.92f, 0.85f, 0.65f);
        // cilindro primitivo: eje en Y local, de -1 a 1
        AnadirBandaCilindro(cuerpo.transform, mf.sharedMesh, matCinta, 0.35f, 1.12f, 0.14f);
        AnadirBandaCilindro(cuerpo.transform, mf.sharedMesh, matCinta, -0.35f, 1.12f, 0.14f);
        AnadirBandaCilindro(cuerpo.transform, mf.sharedMesh, matMecha, 1.1f, 0.22f, 0.2f);

        foreach (Transform t in raiz.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = gameObject.layer;

        // estela para ver la parabola que sigue
        TrailRenderer estela = raiz.AddComponent<TrailRenderer>();
        estela.time = 0.55f;
        estela.startWidth = 0.14f;
        estela.endWidth = 0f;
        Shader sh = Shader.Find("Sprites/Default");
        if (sh != null)
        {
            estela.material = new Material(sh);
            estela.startColor = new Color(1f, 0.7f, 0.2f, 0.85f);
            estela.endColor = new Color(1f, 0.3f, 0.05f, 0f);
        }
        Debug.Log("[Barreno] copia del cilindro del paquete, tamaño " + cuerpo.transform.lossyScale);
        return raiz;
    }

    private static void AnadirBandaCilindro(Transform cuerpo, Mesh malla, Material mat, float y, float ancho, float alto)
    {
        GameObject banda = new GameObject("Banda");
        banda.transform.SetParent(cuerpo, false);
        banda.transform.localPosition = new Vector3(0f, y, 0f);
        banda.transform.localScale = new Vector3(ancho, alto, ancho);
        banda.AddComponent<MeshFilter>().sharedMesh = malla;
        banda.AddComponent<MeshRenderer>().sharedMaterial = mat;
    }

    // copia el barreno principal de la mano (misma malla, material, escala y rotacion) para lanzarlo
    public GameObject DuplicarBarrenoDeMano(Vector3 posicion)
    {
        GameObject desdeCilindro = DuplicarCilindroDelPaquete(posicion);
        if (desdeCilindro != null) return desdeCilindro;

        GameObject raiz = new GameObject("BarrenoProyectil");
        Vector3 centro = CentroBarrenoMano();
        raiz.transform.position = posicion;
        int piezas = 0;

        foreach (MeshFilter mf in GetComponentsInChildren<MeshFilter>(true))
        {
            MeshRenderer mr = mf.GetComponent<MeshRenderer>();
            if (mr == null || mf.sharedMesh == null || EsPiezaExtra(mf.transform, transform)) continue;

            GameObject copia = new GameObject(mf.name);
            copia.transform.SetParent(raiz.transform, false);
            copia.transform.position = posicion + (mf.transform.position - centro);
            copia.transform.rotation = mf.transform.rotation;
            copia.transform.localScale = mf.transform.lossyScale;
            copia.AddComponent<MeshFilter>().sharedMesh = mf.sharedMesh;
            MeshRenderer nuevo = copia.AddComponent<MeshRenderer>();
            nuevo.sharedMaterials = mr.sharedMaterials;
            piezas++;
        }

        // mallas con esqueleto: se congela la pose actual en una malla normal
        foreach (SkinnedMeshRenderer sk in GetComponentsInChildren<SkinnedMeshRenderer>(true))
        {
            if (sk == null || sk.sharedMesh == null || EsPiezaExtra(sk.transform, transform)) continue;
            Mesh congelada = new Mesh();
            sk.BakeMesh(congelada);
            GameObject copia = new GameObject(sk.name);
            copia.transform.SetParent(raiz.transform, false);
            copia.transform.position = posicion + (sk.transform.position - centro);
            copia.transform.rotation = sk.transform.rotation;
            copia.AddComponent<MeshFilter>().sharedMesh = congelada;
            copia.AddComponent<MeshRenderer>().sharedMaterials = sk.sharedMaterials;
            piezas++;
        }

        if (piezas == 0) { Destroy(raiz); return null; }

        foreach (Transform t in raiz.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = gameObject.layer;

        // el barreno lanzado debe verse: tamaño visible entre 0.35 y 0.8 unidades en su lado mas largo
        Bounds total = new Bounds(posicion, Vector3.zero);
        foreach (Renderer r in raiz.GetComponentsInChildren<Renderer>(true)) total.Encapsulate(r.bounds);
        float lado = Mathf.Max(total.size.x, total.size.y, total.size.z);
        Debug.Log("[Barreno] copia con " + piezas + " pieza(s), lado mas largo " + lado.ToString("F3"));
        if (lado > 0.0001f && (lado < 0.35f || lado > 0.8f))
            raiz.transform.localScale *= Mathf.Clamp(lado, 0.35f, 0.8f) / lado;
        return raiz;
    }

    /// <summary>
    /// Si alguien le dispara al portador mientras tiene barrenos disponibles, explota inmediatamente.
    /// </summary>
    public bool PuedeExplotarAlRecibirDisparo()
    {
        return !enCaja && municionActual > 0 && !detonandoEnPortador;
    }

    public void DetonarEnPortador(Personaje atacante)
    {
        if (detonandoEnPortador || jugadorQueLaEquipo == null) return;
        detonandoEnPortador = true;

        Personaje portador = jugadorQueLaEquipo;
        Vector3 centroExplosion = portador.transform.position + new Vector3(0f, 0.9f, 0f);
        municionActual = 0;
        ActualizarVisualPaquete();

        Personaje causante = atacante != null ? atacante : portador;

        if (portador.IsSpawned && NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
        {
            portador.SincronizarMunicionArmaRpc(gameObject.name, 0, DuracionBateriaActual);
            portador.DetonarBarrenoEnPosicionRpc(centroExplosion, radioExplosion);
        }

        EjecutarExplosionArea(centroExplosion, radioExplosion, causante, true);
        detonandoEnPortador = false;
    }

    public static void InstanciarBarrenoLanzado(Personaje lanzador, Vector3 origen, Vector3 velocidadInicial, bool aplicarDanoAutoritativo, float radio = 4.4f, float mecha = 1.75f)
    {
        // se duplica el barreno que tiene en la mano; si no hay arma a la mano se usa el modelo de respaldo
        GameObject proyectil = null;
        if (lanzador != null && lanzador.armaEquipada is ArmaBarreno enMano0 && enMano0 != null)
            proyectil = enMano0.DuplicarBarrenoDeMano(origen);
        if (proyectil == null)
            proyectil = ModelosProceduralesArmas.InstanciarModeloBarreno(origen);
        Sonidos.Reproducir("tick", origen, 0.9f, 1.5f);
        Debug.Log("[Barreno] lanzado en " + origen + " con modelo '" + proyectil.name + "' escala " + proyectil.transform.lossyScale);
        ProyectilBarreno comp = proyectil.AddComponent<ProyectilBarreno>();
        comp.Inicializar(lanzador, velocidadInicial, radio, mecha, aplicarDanoAutoritativo);
    }

    public static void EjecutarExplosionArea(Vector3 centro, float radio, Personaje responsable, bool aplicarDanoAutoritativo)
    {
        CrearEfectoVisualExplosion(centro, radio);
        Sonidos.Reproducir("explosion", centro, 1.0f, Random.Range(0.88f, 1.04f));
        Sonidos.Reproducir("disparo_barret", centro, 0.85f, 0.65f);
        EfectosDisparo.Sacudir(0.28f);

        if (!aplicarDanoAutoritativo) return;

        // Daña y mata a todos los personajes dentro del radio medio, INCLUYENDO al propio portador/lanzador
        HashSet<Personaje> afectados = new HashSet<Personaje>();
        Collider[] hits = Physics.OverlapSphere(centro, radio, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
        foreach (Collider col in hits)
        {
            if (col == null) continue;
            Personaje victima = col.GetComponentInParent<Personaje>();
            if (victima == null || afectados.Contains(victima)) continue;

            afectados.Add(victima);
            Personaje atacanteEfectivo = responsable != null ? responsable : victima;
            victima.RecibirDañoExplosion(atacanteEfectivo);
        }
    }

    public static void CrearEfectoVisualExplosion(Vector3 centro, float radio)
    {
        GameObject raiz = new GameObject("ExplosionBarrenoFX");
        raiz.transform.position = centro;

        // Esfera expansiva de onda de choque
        GameObject esfera = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        esfera.transform.SetParent(raiz.transform, false);
        Collider col = esfera.GetComponent<Collider>();
        if (col != null) Destroy(col);

        MeshRenderer mr = esfera.GetComponent<MeshRenderer>();
        Material matOnda = null;
        if (mr != null)
        {
            Shader sh = Shader.Find("Sprites/Default");
            if (sh == null) sh = Shader.Find("Universal Render Pipeline/Unlit");
            matOnda = new Material(sh);
            matOnda.color = new Color(1f, 0.55f, 0.1f, 0.78f);
            mr.material = matOnda;
        }

        // Particulas de fuego y humo
        ParticleSystem ps = raiz.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = ps.main;
        main.duration = 0.45f;
        main.loop = false;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.25f, 0.65f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(3.5f, 11f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.25f, 0.65f);
        main.startColor = new ParticleSystem.MinMaxGradient(
            new Color(1f, 0.85f, 0.2f, 1f),
            new Color(0.95f, 0.25f, 0.05f, 0.9f));
        main.gravityModifier = 0.6f;
        main.simulationSpace = ParticleSystemSimulationSpace.World;

        var emission = ps.emission;
        emission.rateOverTime = 0f;
        emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 38) });

        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = 0.35f;

        var psr = raiz.GetComponent<ParticleSystemRenderer>();
        Shader shPart = Shader.Find("Sprites/Default");
        if (shPart != null) psr.material = new Material(shPart);

        ps.Play();

        AnimadorOndaExplosion anim = raiz.AddComponent<AnimadorOndaExplosion>();
        anim.Inicializar(esfera.transform, matOnda, radio);
        Destroy(raiz, 1.2f);
    }
}

public class ProyectilBarreno : MonoBehaviour
{
    private Personaje lanzador;
    private Vector3 velocidad;
    private float radioExplosion = 4.4f;
    private float tiempoRestante = 1.75f;
    private bool aplicarDano;
    private float temporizadorChispa;
    private bool exploto;
    private bool enSuelo;

    public void Inicializar(Personaje quienLanza, Vector3 velInicial, float radio, float mecha, bool autoritativo)
    {
        lanzador = quienLanza;
        velocidad = velInicial;
        radioExplosion = radio;
        tiempoRestante = mecha;
        aplicarDano = autoritativo;
    }

    private void Update()
    {
        if (exploto) return;

        float dt = Time.deltaTime;
        tiempoRestante -= dt;
        temporizadorChispa += dt;

        if (temporizadorChispa >= 0.22f)
        {
            temporizadorChispa = 0f;
            Sonidos.Reproducir("tick", transform.position, 0.45f, 1.9f);
        }

        // En el suelo: se queda quieto y visible hasta que se acaba la mecha
        if (enSuelo)
        {
            if (tiempoRestante <= 0f) Detonar();
            return;
        }

        // Trayectoria balistica con rebote suave en piso/paredes
        velocidad.y -= 18f * dt;
        Vector3 paso = velocidad * dt;
        float dist = paso.magnitude;

        if (dist > 0.0001f)
        {
            // se toma el primer choque que no sea el propio lanzador
            RaycastHit[] hits = Physics.SphereCastAll(transform.position, 0.14f, paso.normalized, dist, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            RaycastHit? primero = null;
            foreach (RaycastHit h in hits)
            {
                if (h.collider == null || h.distance <= 0f) continue;
                if (lanzador != null && h.collider.transform.IsChildOf(lanzador.transform)) continue;
                if (primero == null || h.distance < primero.Value.distance) primero = h;
            }

            if (primero.HasValue)
            {
                RaycastHit hit = primero.Value;
                transform.position = hit.point + hit.normal * 0.14f;
                velocidad = Vector3.Reflect(velocidad, hit.normal) * 0.35f;
                velocidad.x *= 0.65f;

                // cayo sobre un piso y ya casi no rebota: se asienta ahi
                if (hit.normal.y > 0.6f && Mathf.Abs(velocidad.y) < 1.4f)
                {
                    velocidad = Vector3.zero;
                    enSuelo = true;
                    Debug.Log("[Barreno] aterrizo en " + transform.position + ", explota en " + tiempoRestante.ToString("F2") + " s");
                    Sonidos.Reproducir("tick", transform.position, 0.7f, 1.2f);
                }
            }
            else
            {
                transform.position += paso;
            }
        }

        // gira en el aire, al asentarse deja de girar
        if (!enSuelo) transform.Rotate(240f * dt, 120f * dt, 380f * dt, Space.Self);

        if (tiempoRestante <= 0f) Detonar();
    }

    private void Detonar()
    {
        exploto = true;
        ArmaBarreno.EjecutarExplosionArea(transform.position, radioExplosion, lanzador, aplicarDano);
        Destroy(gameObject);
    }
}

public class AnimadorOndaExplosion : MonoBehaviour
{
    private Transform esfera;
    private Material material;
    private float radioMax;
    private float tiempo;
    private const float Duracion = 0.26f;

    public void Inicializar(Transform tEsfera, Material mat, float radio)
    {
        esfera = tEsfera;
        material = mat;
        radioMax = radio;
    }

    private void Update()
    {
        tiempo += Time.deltaTime;
        float t = Mathf.Clamp01(tiempo / Duracion);
        if (esfera != null)
        {
            float diametro = Mathf.Lerp(0.4f, radioMax * 2f, 1f - Mathf.Pow(1f - t, 3f));
            esfera.localScale = Vector3.one * diametro;
        }
        if (material != null)
        {
            Color c = material.color;
            c.a = Mathf.Lerp(0.78f, 0f, t);
            material.color = c;
        }
        if (t >= 1f && esfera != null)
        {
            esfera.gameObject.SetActive(false);
        }
    }
}
