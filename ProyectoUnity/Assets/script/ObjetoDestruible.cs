using System.Collections.Generic;
using UnityEngine;

public enum TipoObstaculoDestruible
{
    ParedCobertura = 0,
    PlataformaCaja = 1
}

/// <summary>
/// Obstaculo destruible del entorno (paredes de cobertura o cajas/plataformas para brincar).
/// Se puede colocar directamente dentro de cada mapa en la escena (editable con el mouse en Scene View)
/// o generarse dinamicamente. Soporta disparos del Francotirador Antitanques y explosiones del Barreno,
/// bloqueando balas convencionales mientras permanece intacto.
/// </summary>
[SelectionBase]
[ExecuteAlways]
public class ObjetoDestruible : MonoBehaviour
{
    [Header("Configuracion en Escena")]
    [SerializeField] private TipoObstaculoDestruible tipo = TipoObstaculoDestruible.ParedCobertura;
    [Tooltip("Opcional: usar dimensiones personalizadas en vez del tamanio estandar del tipo elegido")]
    [SerializeField] private bool usarDimensionesPersonalizadas = false;
    [SerializeField] private Vector3 dimensionesPersonalizadas = new Vector3(0.42f, 1.65f, 1.35f);

    private int idObstaculo = -1;
    private Vector3 dimensiones = new Vector3(0.42f, 1.65f, 1.35f);
    private bool generadoDinamicamente = false;
    private bool destruido = false;
    private bool necesitaRefrescarVisual = false;

    private BoxCollider colliderPrincipal;
    private GameObject visualRaiz;
    private Color colorBaseEscombros = new Color(0.68f, 0.36f, 0.20f, 1f);
    private Color colorBordeEscombros = new Color(0.32f, 0.20f, 0.12f, 1f);

    public int IdObstaculo => idObstaculo;
    public TipoObstaculoDestruible Tipo => tipo;
    public bool Destruido => destruido;
    public bool GeneradoDinamicamente => generadoDinamicamente;

    public Vector3 CentroVisual
    {
        get
        {
            if (colliderPrincipal != null) return colliderPrincipal.bounds.center;
            Collider col = GetComponent<Collider>();
            if (col != null) return col.bounds.center;
            return transform.position + Vector3.up * (dimensiones.y * 0.5f);
        }
    }

    private void OnEnable()
    {
        if (!destruido)
        {
            AplicarConfiguracionVisualYCollider();
        }
    }

    private void Awake()
    {
        if (!destruido)
        {
            AplicarConfiguracionVisualYCollider();
        }
    }

    private void OnValidate()
    {
        necesitaRefrescarVisual = true;
    }

    private void Update()
    {
        if (!Application.isPlaying && necesitaRefrescarVisual)
        {
            necesitaRefrescarVisual = false;
            AplicarConfiguracionVisualYCollider();
        }
    }

    /// <summary>
    /// Configura el tipo desde codigo/Editor y reconstruye al instante su malla y collider.
    /// </summary>
    public void EstablecerTipo(TipoObstaculoDestruible nuevoTipo)
    {
        tipo = nuevoTipo;
        usarDimensionesPersonalizadas = false;
        AplicarConfiguracionVisualYCollider();
    }

    /// <summary>
    /// Inicializa un obstaculo colocado manualmente dentro de un mapa de la escena y lo restaura para la ronda.
    /// </summary>
    public void ConfigurarComoObstaculoEnEscena(int id)
    {
        idObstaculo = id;
        generadoDinamicamente = false;
        RestaurarEstado();
    }

    /// <summary>
    /// Inicializa un obstaculo generado dinamicamente por GestorEntornoDestruible.
    /// </summary>
    public void ConfigurarComoObstaculoGenerado(int id, TipoObstaculoDestruible tipoObstaculo)
    {
        idObstaculo = id;
        tipo = tipoObstaculo;
        usarDimensionesPersonalizadas = false;
        generadoDinamicamente = true;
        destruido = false;
        AplicarConfiguracionVisualYCollider();
    }

    public void RestaurarEstado()
    {
        destruido = false;
        AplicarConfiguracionVisualYCollider();
        if (visualRaiz != null) visualRaiz.SetActive(true);
        foreach (Collider c in GetComponentsInChildren<Collider>(true))
        {
            if (c != null) c.enabled = true;
        }
        foreach (Renderer r in GetComponentsInChildren<Renderer>(true))
        {
            if (r != null) r.enabled = true;
        }
    }

    public void AplicarConfiguracionVisualYCollider()
    {
        necesitaRefrescarVisual = false;

        // Si el usuario adjunto este script a un mueble ya existente que ya tiene su propio MeshRenderer, respetar su geometria
        bool esMuebleExistente = !generadoDinamicamente && TieneMallaPropiaExterna();

        dimensiones = usarDimensionesPersonalizadas
            ? new Vector3(
                Mathf.Max(0.2f, dimensionesPersonalizadas.x),
                Mathf.Max(0.2f, dimensionesPersonalizadas.y),
                Mathf.Max(0.2f, dimensionesPersonalizadas.z))
            : (tipo == TipoObstaculoDestruible.ParedCobertura
                ? new Vector3(0.42f, 1.65f, 1.35f)
                : new Vector3(1.65f, 1.02f, 1.35f));

        if (!esMuebleExistente)
        {
            colliderPrincipal = GetComponent<BoxCollider>();
            if (colliderPrincipal == null) colliderPrincipal = gameObject.AddComponent<BoxCollider>();
            colliderPrincipal.isTrigger = false;
            colliderPrincipal.center = new Vector3(0f, dimensiones.y * 0.5f, 0f);
            colliderPrincipal.size = dimensiones;
            colliderPrincipal.enabled = !destruido;

            ConstruirVisualProcedural();
        }
        else if (colliderPrincipal == null)
        {
            colliderPrincipal = GetComponent<BoxCollider>();
        }
    }

    private bool TieneMallaPropiaExterna()
    {
        MeshFilter mfPropio = GetComponent<MeshFilter>();
        if (mfPropio != null && mfPropio.sharedMesh != null) return true;

        foreach (MeshFilter mf in GetComponentsInChildren<MeshFilter>(true))
        {
            if (mf == null || mf.sharedMesh == null) continue;
            Transform cur = mf.transform;
            bool dentroDePreview = false;
            while (cur != null && cur != transform)
            {
                if (cur.name == "VisualObstaculo")
                {
                    dentroDePreview = true;
                    break;
                }
                cur = cur.parent;
            }
            if (!dentroDePreview) return true;
        }
        return false;
    }

    private void ConstruirVisualProcedural()
    {
        LimpiarVisualAnterior();

        visualRaiz = new GameObject("VisualObstaculo");
        visualRaiz.hideFlags = HideFlags.DontSaveInEditor | HideFlags.DontSaveInBuild;
        visualRaiz.transform.SetParent(transform, false);

        if (tipo == TipoObstaculoDestruible.ParedCobertura)
        {
            colorBaseEscombros = new Color(0.72f, 0.34f, 0.22f, 1f);
            colorBordeEscombros = new Color(0.28f, 0.24f, 0.22f, 1f);

            Material matLadrillo = CrearMaterialPreview(colorBaseEscombros, 0.05f, 0.30f);
            Material matConcreto = CrearMaterialPreview(new Color(0.46f, 0.44f, 0.42f, 1f), 0.05f, 0.25f);
            Material matContorno = CrearMaterialPreview(colorBordeEscombros, 0.15f, 0.45f);
            Material matFranjaAviso = CrearMaterialPreview(new Color(0.92f, 0.72f, 0.18f, 1f), 0.1f, 0.5f);

            CrearPieza(visualRaiz.transform, "BaseConcreto",
                new Vector3(0f, 0.12f, 0f), new Vector3(dimensiones.x + 0.08f, 0.24f, dimensiones.z + 0.06f), matConcreto);
            CrearPieza(visualRaiz.transform, "CuerpoPared",
                new Vector3(0f, dimensiones.y * 0.5f + 0.04f, 0f), new Vector3(dimensiones.x, dimensiones.y - 0.14f, dimensiones.z), matLadrillo);
            CrearPieza(visualRaiz.transform, "RemateSuperior",
                new Vector3(0f, dimensiones.y - 0.06f, 0f), new Vector3(dimensiones.x + 0.07f, 0.12f, dimensiones.z + 0.05f), matConcreto);

            float posZMarco = dimensiones.z * 0.46f;
            CrearPieza(visualRaiz.transform, "PosteFrontal",
                new Vector3(0f, dimensiones.y * 0.5f, posZMarco), new Vector3(dimensiones.x + 0.03f, dimensiones.y, 0.09f), matContorno);
            CrearPieza(visualRaiz.transform, "PosteTrasero",
                new Vector3(0f, dimensiones.y * 0.5f, -posZMarco), new Vector3(dimensiones.x + 0.03f, dimensiones.y, 0.09f), matContorno);

            CrearPieza(visualRaiz.transform, "FranjaTactica",
                new Vector3(0f, dimensiones.y * 0.58f, 0f), new Vector3(dimensiones.x + 0.025f, 0.11f, dimensiones.z * 0.92f), matFranjaAviso);
        }
        else
        {
            colorBaseEscombros = new Color(0.68f, 0.45f, 0.20f, 1f);
            colorBordeEscombros = new Color(0.36f, 0.22f, 0.10f, 1f);

            Material matMadera = CrearMaterialPreview(colorBaseEscombros, 0.05f, 0.35f);
            Material matMarco = CrearMaterialPreview(colorBordeEscombros, 0.08f, 0.30f);
            Material matTapa = CrearMaterialPreview(new Color(0.78f, 0.56f, 0.26f, 1f), 0.05f, 0.42f);
            Material matAcero = CrearMaterialPreview(new Color(0.28f, 0.30f, 0.34f, 1f), 0.65f, 0.65f);

            CrearPieza(visualRaiz.transform, "CuerpoCaja",
                new Vector3(0f, (dimensiones.y - 0.10f) * 0.5f, 0f), new Vector3(dimensiones.x - 0.04f, dimensiones.y - 0.10f, dimensiones.z - 0.04f), matMadera);
            CrearPieza(visualRaiz.transform, "TapaPlataforma",
                new Vector3(0f, dimensiones.y - 0.06f, 0f), new Vector3(dimensiones.x, 0.12f, dimensiones.z), matTapa);

            float px = dimensiones.x * 0.5f - 0.06f;
            float pz = dimensiones.z * 0.5f - 0.06f;
            float h = dimensiones.y - 0.04f;
            CrearPieza(visualRaiz.transform, "EsquinaFL", new Vector3(-px, h * 0.5f, pz), new Vector3(0.11f, h, 0.11f), matMarco);
            CrearPieza(visualRaiz.transform, "EsquinaFR", new Vector3(px, h * 0.5f, pz), new Vector3(0.11f, h, 0.11f), matMarco);
            CrearPieza(visualRaiz.transform, "EsquinaBL", new Vector3(-px, h * 0.5f, -pz), new Vector3(0.11f, h, 0.11f), matMarco);
            CrearPieza(visualRaiz.transform, "EsquinaBR", new Vector3(px, h * 0.5f, -pz), new Vector3(0.11f, h, 0.11f), matMarco);

            CrearPieza(visualRaiz.transform, "RefuerzoFrente",
                new Vector3(0f, dimensiones.y * 0.48f, 0f), new Vector3(dimensiones.x + 0.02f, 0.11f, dimensiones.z + 0.02f), matAcero);
        }

        visualRaiz.SetActive(!destruido);
    }

    private void LimpiarVisualAnterior()
    {
        if (visualRaiz != null)
        {
            DestruirSegunModo(visualRaiz);
            visualRaiz = null;
        }

        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            Transform hijo = transform.GetChild(i);
            if (hijo != null && hijo.name == "VisualObstaculo")
            {
                DestruirSegunModo(hijo.gameObject);
            }
        }
    }

    private static void DestruirSegunModo(Object obj)
    {
        if (obj == null) return;
        if (Application.isPlaying) Destroy(obj);
        else DestroyImmediate(obj);
    }

    private static Material CrearMaterialPreview(Color color, float metallic, float smoothness)
    {
        Material mat = ModelosProceduralesArmas.CrearMaterialColor(color, metallic, smoothness);
        if (mat != null)
        {
            mat.hideFlags = HideFlags.DontSaveInEditor | HideFlags.DontSaveInBuild;
        }
        return mat;
    }

    private static GameObject CrearPieza(Transform padre, string nombre, Vector3 posLocal, Vector3 escalaLocal, Material mat)
    {
        GameObject pieza = GameObject.CreatePrimitive(PrimitiveType.Cube);
        pieza.name = nombre;
        pieza.hideFlags = HideFlags.DontSaveInEditor | HideFlags.DontSaveInBuild;
        Collider c = pieza.GetComponent<Collider>();
        if (c != null) DestruirSegunModo(c);
        pieza.transform.SetParent(padre, false);
        pieza.transform.localPosition = posLocal;
        pieza.transform.localScale = escalaLocal;
        MeshRenderer mr = pieza.GetComponent<MeshRenderer>();
        if (mr != null && mat != null) mr.sharedMaterial = mat;
        return pieza;
    }

    public void DestruirLocal(Vector3 origenImpacto, Vector3 direccionImpacto, bool porExplosion)
    {
        if (destruido) return;
        destruido = true;

        Bounds area = ObtenerBoundsActuales();
        Vector3 centro = area.center;

        foreach (Collider c in GetComponentsInChildren<Collider>(true))
        {
            if (c != null) c.enabled = false;
        }

        if (visualRaiz != null)
        {
            visualRaiz.SetActive(false);
        }
        else
        {
            foreach (Renderer r in GetComponentsInChildren<Renderer>(true))
            {
                if (r != null) r.enabled = false;
            }
        }

        GenerarEscombrosYParticulas(area, direccionImpacto, porExplosion);
        Sonidos.Reproducir("romper_caja", centro, 1.0f, Random.Range(0.68f, 0.85f));
        Sonidos.Reproducir("impacto", centro, 0.90f, Random.Range(0.55f, 0.72f));
    }

    private Bounds ObtenerBoundsActuales()
    {
        if (colliderPrincipal != null && colliderPrincipal.bounds.size.sqrMagnitude > 0.0001f)
            return colliderPrincipal.bounds;

        Collider c = GetComponent<Collider>();
        if (c != null && c.bounds.size.sqrMagnitude > 0.0001f)
            return c.bounds;

        return new Bounds(transform.position + Vector3.up * (dimensiones.y * 0.5f), dimensiones);
    }

    private void GenerarEscombrosYParticulas(Bounds area, Vector3 direccionImpacto, bool porExplosion)
    {
        GameObject contenedorEscombros = new GameObject("EscombrosObstaculo");
        contenedorEscombros.transform.position = area.center;

        Vector3 dirBase = direccionImpacto.sqrMagnitude > 0.001f ? direccionImpacto.normalized : Vector3.right;
        Material matPrincipal = ModelosProceduralesArmas.CrearMaterialColor(colorBaseEscombros, 0.05f, 0.35f);
        Material matSecundario = ModelosProceduralesArmas.CrearMaterialColor(colorBordeEscombros, 0.1f, 0.4f);
        Personaje[] jugadores = FindObjectsByType<Personaje>(FindObjectsSortMode.None);

        int cantidad = porExplosion ? 14 : 11;
        List<Transform> cubos = new List<Transform>(cantidad);

        for (int i = 0; i < cantidad; i++)
        {
            GameObject cubo = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cubo.name = $"Escombro_{i}";
            cubo.transform.SetParent(contenedorEscombros.transform, true);

            float sx = Random.Range(0.14f, 0.30f);
            float sy = Random.Range(0.12f, 0.24f);
            float sz = Random.Range(0.14f, 0.28f);
            cubo.transform.localScale = new Vector3(sx, sy, sz);
            cubo.transform.position = new Vector3(
                Random.Range(area.min.x, area.max.x),
                Random.Range(area.min.y + 0.12f, area.max.y),
                area.center.z + Random.Range(-0.18f, 0.18f));
            cubo.transform.rotation = Random.rotation;

            MeshRenderer mr = cubo.GetComponent<MeshRenderer>();
            if (mr != null)
            {
                mr.sharedMaterial = (i % 3 == 0) ? matSecundario : matPrincipal;
                mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }

            Collider col = cubo.GetComponent<Collider>();
            if (col != null)
            {
                foreach (Personaje p in jugadores)
                {
                    if (p == null) continue;
                    Collider cc = p.GetComponent<Collider>();
                    if (cc != null) Physics.IgnoreCollision(col, cc);
                }
            }

            Rigidbody rb = cubo.AddComponent<Rigidbody>();
            rb.mass = 0.25f;
            Vector3 salida = (cubo.transform.position - area.center);
            salida.z *= 0.25f;

            Vector3 impulso = porExplosion
                ? (salida.normalized * Random.Range(4.0f, 8.5f) + Vector3.up * Random.Range(3.0f, 6.5f))
                : (dirBase * Random.Range(5.5f, 10.5f) + salida.normalized * Random.Range(1.5f, 3.5f) + Vector3.up * Random.Range(2.0f, 4.8f));
            impulso.z *= 0.25f;
            rb.linearVelocity = impulso;
            rb.angularVelocity = Random.insideUnitSphere * 10f;
            cubos.Add(cubo.transform);
        }

        ParticleSystem ps = contenedorEscombros.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = ps.main;
        main.duration = 0.35f;
        main.loop = false;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.28f, 0.65f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(1.4f, 4.6f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.22f, 0.55f);
        main.startColor = new Color(0.84f, 0.78f, 0.68f, 0.68f);
        main.gravityModifier = 0.25f;
        main.simulationSpace = ParticleSystemSimulationSpace.World;

        var emission = ps.emission;
        emission.rateOverTime = 0f;
        emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 18) });

        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.scale = new Vector3(Mathf.Max(0.3f, area.size.x), Mathf.Max(0.5f, area.size.y), 0.6f);

        var psr = contenedorEscombros.GetComponent<ParticleSystemRenderer>();
        Shader shPart = Shader.Find("Sprites/Default");
        if (shPart != null && psr != null) psr.material = new Material(shPart);
        ps.Play();

        LimpiadorEscombros limpiador = contenedorEscombros.AddComponent<LimpiadorEscombros>();
        limpiador.Inicializar(cubos, 1.45f);
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = tipo == TipoObstaculoDestruible.ParedCobertura
            ? new Color(0.95f, 0.45f, 0.18f, 0.85f)
            : new Color(0.95f, 0.82f, 0.20f, 0.85f);

        Matrix4x4 prev = Gizmos.matrix;
        Gizmos.matrix = Matrix4x4.TRS(transform.position, transform.rotation, transform.lossyScale);
        Gizmos.DrawWireCube(new Vector3(0f, dimensiones.y * 0.5f, 0f), dimensiones);
        Gizmos.matrix = prev;
    }

    private class LimpiadorEscombros : MonoBehaviour
    {
        private List<Transform> piezas;
        private float vida;
        private float transcurrido;

        public void Inicializar(List<Transform> cubos, float duracion)
        {
            piezas = cubos;
            vida = duracion;
        }

        private void Update()
        {
            transcurrido += Time.deltaTime;
            if (transcurrido >= vida * 0.55f && piezas != null)
            {
                float k = Mathf.InverseLerp(vida * 0.55f, vida, transcurrido);
                float factor = Mathf.Lerp(1f, 0.05f, k);
                foreach (Transform t in piezas)
                {
                    if (t != null) t.localScale *= Mathf.Lerp(1f, 0.88f, k);
                    if (factor < 0.1f && t != null) t.gameObject.SetActive(false);
                }
            }
            if (transcurrido >= vida)
            {
                Destroy(gameObject);
            }
        }
    }
}
