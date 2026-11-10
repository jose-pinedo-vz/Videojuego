using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Ragdoll "de plastilina" que se arma en tiempo de ejecucion sobre los huesos del modelo (SkinnedMeshRenderer.bones),
/// asi funciona con los 4 personajes sin tocar los prefabs.
/// - Apaga el Animator y el CharacterController mientras dura.
/// - A cada hueso importante le pone Rigidbody + SphereCollider + CharacterJoint al hueso padre (articulaciones flojas).
/// - Se mantiene en el plano 2.5D (Z congelada) y al terminar devuelve cada hueso a su pose y el personaje se levanta donde cayo.
/// Es solo visual/fisico local: lo que se sincroniza es la orden de iniciar (Personaje.IniciarRagdollVisualRpc).
/// </summary>
public class RagdollPlastilina : MonoBehaviour
{
    private class Hueso
    {
        public Transform t;
        public Rigidbody rb;
        public SphereCollider col;
        public CharacterJoint unionPadre;
        public Vector3 posLocal;
        public Quaternion rotLocal;
        public Vector3 escLocal;
    }

    private readonly List<Hueso> huesos = new List<Hueso>();
    private readonly List<SkinnedMeshRenderer> mallas = new List<SkinnedMeshRenderer>();
    private readonly List<bool> mallasFueraPantalla = new List<bool>();

    private Personaje personaje;
    private Animator animator;
    private CharacterController controlador;
    private Hueso raiz; // la cadera: el hueso del que cuelgan todos los demas
    private bool esDuenio;
    private float altura = 1f;
    private Coroutine rutina;

    public bool Activo { get; private set; }

    // direccion: hacia donde sale despedido (la fuerza se ajusta al tamano del personaje), duracion en segundos
    public void Iniciar(Personaje quien, Animator anim, CharacterController cc, Vector3 direccion, float duracion, bool duenio)
    {
        if (Activo) return;

        personaje = quien;
        animator = anim;
        controlador = cc;
        esDuenio = duenio;

        if (!ConstruirHuesos()) return; // sin esqueleto no hay nada que soltar

        Activo = true;
        if (animator != null) animator.enabled = false;
        if (controlador != null) controlador.enabled = false;

        // las mallas con huesos sueltos no deben desaparecer por salirse de su caja de culling
        foreach (SkinnedMeshRenderer m in mallas)
        {
            mallasFueraPantalla.Add(m.updateWhenOffscreen);
            m.updateWhenOffscreen = true;
        }

        // despedido como por una explosion pequena: no es mortal, solo lo tira
        float fuerza = altura * 7f;
        foreach (Hueso h in huesos)
        {
            float factor = h == raiz ? 1f : 0.7f;
            h.rb.linearVelocity = direccion.normalized * fuerza * factor;
            h.rb.angularVelocity = Random.insideUnitSphere * 6f; // gira como muñeco flojo
        }

        rutina = StartCoroutine(Esperar(duracion));
    }

    private IEnumerator Esperar(float duracion)
    {
        yield return new WaitForSeconds(duracion);
        Terminar(true);
    }

    // levantarse: true = el duenio se levanta donde cayo; false = solo se cancela (ej. murio en pleno ragdoll)
    public void Terminar(bool levantarse)
    {
        if (!Activo) return;
        if (rutina != null) StopCoroutine(rutina);
        rutina = null;

        Vector3 destino = personaje.transform.position;
        if (levantarse && raiz != null) destino = CalcularLugarDeLevantarse();

        // los colliders se apagan ya mismo (Destroy tarda al final del cuadro) para no estorbar al raycast ni a nadie
        foreach (Hueso h in huesos)
        {
            if (h.col != null) h.col.enabled = false;
            if (h.unionPadre != null) Destroy(h.unionPadre);
            if (h.col != null) Destroy(h.col);
            if (h.rb != null) Destroy(h.rb);
        }

        // cada hueso vuelve a su pose de antes y el Animator retoma el control
        foreach (Hueso h in huesos)
        {
            if (h.t == null) continue;
            h.t.localPosition = h.posLocal;
            h.t.localRotation = h.rotLocal;
            h.t.localScale = h.escLocal;
        }
        for (int i = 0; i < mallas.Count; i++)
        {
            if (mallas[i] != null) mallas[i].updateWhenOffscreen = mallasFueraPantalla[i];
        }
        if (animator != null) animator.enabled = true;

        Activo = false;
        huesos.Clear();
        mallas.Clear();
        mallasFueraPantalla.Clear();
        raiz = null;

        personaje.AlTerminarRagdoll(destino, esDuenio && levantarse);
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

    private bool ConstruirHuesos()
    {
        huesos.Clear();
        mallas.Clear();
        mallasFueraPantalla.Clear();

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

        // elegir los huesos con largo suficiente: dedos y puntas sueltas se quedan pegados al hueso padre
        List<Transform> elegidos = new List<Transform>();
        foreach (Transform b in conjunto)
        {
            if (LargoDelHueso(b, conjunto) >= altura * 0.06f) elegidos.Add(b);
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
            Vector3 haciaHijo = DireccionAHijos(b, conjunto);

            h.rb = b.gameObject.AddComponent<Rigidbody>();
            h.rb.mass = Mathf.Max(0.2f, largo * 4f);
            h.rb.linearDamping = 0.6f;
            h.rb.angularDamping = 3f; // plastilina: se frena rapido y se queda blandita
            h.rb.constraints = RigidbodyConstraints.FreezePositionZ; // juego 2.5D, no se sale del plano
            h.rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

            h.col = b.gameObject.AddComponent<SphereCollider>();
            h.col.radius = Mathf.Max(altura * 0.035f, largo * 0.4f) / Mathf.Max(0.0001f, Mathf.Abs(b.lossyScale.x));
            h.col.center = b.InverseTransformPoint(b.position + haciaHijo * largo * 0.5f);

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
            // limites amplios y flojos: se dobla como plastilina
            j.lowTwistLimit = new SoftJointLimit { limit = -40f };
            j.highTwistLimit = new SoftJointLimit { limit = 40f };
            j.swing1Limit = new SoftJointLimit { limit = 55f };
            j.swing2Limit = new SoftJointLimit { limit = 55f };
            h.unionPadre = j;
        }
        if (raiz == null) raiz = huesos[0];

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
