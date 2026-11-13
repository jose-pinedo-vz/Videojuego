using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;

public enum ModoDisparo
{
    Semiautomatico,
    Automatico,
    RayoContinuo
}

public class Arma : MonoBehaviour
{
    [SerializeField] protected int Balas=1;
    [SerializeField] protected float Relantizacion =0f;
    [SerializeField] protected float Cadencia=10.0f;
    [SerializeField] protected float Alcance=5.0f;
    [SerializeField] protected float Retroceso=0-2f;
    [SerializeField] protected float Dispersion=1.0f;
    [SerializeField] protected int perdigonesPorDisparo = 1;
    [SerializeField] protected float retrocesoDisparoHorizontal = 0f;
    [SerializeField] protected float retrocesoDisparoVertical = 0f;
    [SerializeField] protected float tiempoDespawnEnSuelo = 15f;
    [SerializeField] protected bool destruyeYAtraviesaSuperficies = false;
    protected bool jugadorCerca = false; //Esto dejas aber si el jugador esta tocando al arma
    protected Transform mano; //El objeto al que se pegara cuando el jugador lo agarre (debe ser invisible)
    protected float tiempoUltimoDisparo=0f;
    protected Personaje jugadorQueLaEquipo;
    protected LineRenderer trazo;
    private Coroutine corrutinaTrazo;
    private readonly List<LineRenderer> trazosExtraEscopeta = new List<LineRenderer>();
    private float tiempoRecogida = 0f; // Para evitar que se suelte inmediatamente después de recoger
    private Quaternion rotacionInicial;
    protected Quaternion rotacionBaseIzquierda;
    protected MeshRenderer renderizadorArma;
    protected MeshRenderer[] renderizadoresArma;
    protected bool enCaja = false;
    private GameObject cajaVisual;
    private static int ultimoFrameAccionC = -1;
    private bool datosConfigurados = false;
    private float tiempoEnSueloSinDuenio = 0f;
    private bool esperandoRecogidaEnSpawn = false;
    private bool despawnSolicitado = false;
    protected virtual bool EstaEnVuelo => false;

    // Caida al soltarse: gravedad simple + raycast al piso, igual en todos los clientes (no depende de red)
    private bool cayendo = false;
    private float velocidadCaida = 0f;
    private const float GRAVEDAD_ARMA = 18f;
    private const float DISTANCIA_MAX_CAIDA = 60f;

    [Header("Modo de Disparo")]
    [SerializeField] protected ModoDisparo modoDisparo = ModoDisparo.Semiautomatico;

    [Header("Raygun (Rayo Continuo)")]
    [SerializeField] protected float tiempoCargaRequerido = 3.0f;
    [SerializeField] protected float duracionBateriaMaxima = 5.0f;
    [SerializeField] protected float anchoRayo = 1.0f;
    [SerializeField] protected float radioRayo = 0.5f;
    [SerializeField] protected float alcanceRayo = 80.0f;

    private float tiempoCargaActual = 0f;
    private float duracionBateriaActual = 5.0f;
    private bool disparandoRayo = false;
    private bool cargandoRayo = false;
    private bool disparandoRayoRemoto = false;
    private bool cargandoRayoRemoto = false;
    private float tiempoCargaRemota = 0f;
    private AudioSource audioLoopRayo;
    private AudioClip clipRaygunCache;
    private HashSet<Personaje> victimasAlcanzadas = new HashSet<Personaje>();

    private enum EstadoAudioRaygun { Inactivo, Cargando, Disparando, Desvaneciendo }
    private EstadoAudioRaygun estadoAudioRaygun = EstadoAudioRaygun.Inactivo;
    private float velocidadFadeRaygun = 4f;

    // Segmentos medidos sobre "raygun audio disparo.mp3" (20.35s):
    // 8.75s -> 11.75s (3.00s exactos de carga ascendente), 11.75s pico de disparo, 12.25s..14.45s sostén de rayo, 14.85s+ apagado
    private const float RAYGUN_CLIP_INICIO_CARGA = 8.75f;
    private const float RAYGUN_CLIP_PICO_DISPARO = 11.75f;
    private const float RAYGUN_CLIP_LOOP_INICIO = 12.25f;
    private const float RAYGUN_CLIP_LOOP_FIN = 14.45f;
    private const float RAYGUN_CLIP_COLA_APAGADO = 14.85f;
    private const float RAYGUN_VOLUMEN_DISPARO = 0.72f;

    // Offset visual de referencia tomado directamente del AK-47 cuando el jugador apunta hacia la derecha (-X)
    private static bool tieneOffsetReferenciaAK = false;
    private static Vector3 offsetReferenciaAKDerecha = new Vector3(-0.082f, 0.144f, -0.261f);

    [SerializeField] protected Vector3 rotacionEnMano = Vector3.zero; //Estos 2 son para controlar donde la va a agarrar
    [SerializeField] protected Vector3 posicionEnMano = Vector3.zero; //Ocupare ayuda en esto porque es a mano, ojo de buen cubero y presicion

    [Header("Caja de Loot (Madera)")]
    [Tooltip("Si está activo, el arma aparece dentro de una caja de madera (cubo café) hasta romperla con V")]
    [SerializeField] protected bool iniciarEnCaja = true;
    [Tooltip("Si está activo, al romper la caja con V el arma cae al suelo frente al jugador para recogerla/robarla con C")]
    [SerializeField] protected bool soltarAlRomperCaja = true;
    [SerializeField] protected Vector3 tamanoCaja = new Vector3(0.45f, 0.45f, 0.45f);

    [Header("Nombre para el HUD")]
    [SerializeField] protected string nombreMostrado = ""; // si se deja vacio se usa el nombre del GameObject

    [Header("Municion")]
    [SerializeField] protected int municionMaxima = 10;
    [SerializeField] protected float tiempoRecarga = 1.5f;
    protected int municionActual;
    protected bool recargando = false;

    [Header("Area de recogida (trigger en el suelo)")]
    [Tooltip("Desplazamiento del centro del collider de recogida. Vector3.zero = no tocar el centro que ya tiene el collider en el editor")]
    [SerializeField] protected Vector3 offsetAreaRecogida = Vector3.zero;
    [Tooltip("Tamanio del area de recogida (solo aplica si el collider es un BoxCollider). Vector3.zero = no tocar el tamanio que ya tiene en el editor")]
    [SerializeField] protected Vector3 tamanoAreaRecogida = Vector3.zero;

    public int MunicionActual => modoDisparo == ModoDisparo.RayoContinuo ? Mathf.CeilToInt(duracionBateriaActual) : municionActual;
    public int MunicionCruda => municionActual;
    public int MunicionMaxima => modoDisparo == ModoDisparo.RayoContinuo ? Mathf.CeilToInt(duracionBateriaMaxima) : municionMaxima;
    public bool Recargando => recargando;
    public bool EnCaja => enCaja;
    public string NombreMostrado => enCaja ? "Caja de madera" : (!string.IsNullOrEmpty(nombreMostrado) ? nombreMostrado : FormatearNombreArma(gameObject.name));

    private static string FormatearNombreArma(string n)
    {
        if (string.IsNullOrEmpty(n)) return "Arma";
        string s = n.ToLowerInvariant();
        if (s.Contains("ak47") || s == "ak") return "AK-47";
        if (s.Contains("antitanque")) return "Francotirador Antitanques";
        if (s.Contains("barret")) return "Barret .50";
        if (s.Contains("mac10") || s.Contains("mac")) return "MAC-10";
        if (s.Contains("beret")) return "Beretta";
        if (s.Contains("raygun")) return "Raygun";
        if (s.Contains("machete")) return "Machete";
        if (s.Contains("barreno")) return "Paquete de Barrenos";
        if (s.Contains("escudo")) return "Escudo Troyano";
        if (s.Contains("platano") || s.Contains("penca")) return "Penca de Plátano";
        if (s.Contains("escopeta")) return "Escopeta";
        return n;
    }
    public float TiempoRecogida => tiempoRecogida;
    public ModoDisparo Modo => modoDisparo;
    public float FactorRalentizacion => (!enCaja && jugadorQueLaEquipo != null) ? Mathf.Clamp(Relantizacion, 0f, 0.85f) : 0f;
    public float AnchoRayo => anchoRayo;
    public float TiempoCargaActual => tiempoCargaActual;
    public float TiempoCargaRequerido => tiempoCargaRequerido;
    public float DuracionBateriaActual => duracionBateriaActual;
    public float DuracionBateriaMaxima => duracionBateriaMaxima;
    public bool DisparandoRayo => disparandoRayo;
    public bool CargandoRayo => cargandoRayo;
    public bool TieneAudioPersonalizadoRayo => ObtenerClipRaygun() != null;
    public int ContadorDisparos { get; protected set; }
    public Vector3 UltimoInicioTrazo { get; protected set; } = Vector3.zero;
    public Vector3 UltimoFinTrazo { get; protected set; } = Vector3.right;

    protected virtual void Awake()
    {
        municionActual = municionMaxima;
        duracionBateriaActual = duracionBateriaMaxima;
        rotacionInicial = transform.rotation;
        if (gameObject.name.ToLowerInvariant().Contains("antitanq"))
            destruyeYAtraviesaSuperficies = true;

        AsegurarMallaProceduralSiCorresponde();
        InicializarRenderizadoresArma();
        if (renderizadoresArma != null)
        {
            foreach (MeshRenderer mr in renderizadoresArma)
                if (mr != null) mr.enabled = true;
        }

        RecalibrarModeloVisual();
        CrearCajaVisualSiNoExiste();
        AjustarAreaDeRecogida();
        ColocarEnCaja(iniciarEnCaja);
    }

    protected virtual void AsegurarMallaProceduralSiCorresponde()
    {
        string n = gameObject.name.ToLowerInvariant();
        if (n.Contains("machete")) ModelosProceduralesArmas.AsegurarMallaMachete(gameObject);
        else if (n.Contains("platano") || n.Contains("penca")) ModelosProceduralesArmas.AsegurarMallaPencaPlatanos(gameObject);
        else if (n.Contains("barreno")) ModelosProceduralesArmas.AsegurarPaqueteTresBarrenos(gameObject);
    }

    public void RecalibrarModeloVisual()
    {
        AsegurarMallaProceduralSiCorresponde();
        InicializarRenderizadoresArma();
        rotacionBaseIzquierda = CalcularRotacionBaseIzquierda();
        NormalizarEscalaArmaSiEsNecesario();

        if (jugadorQueLaEquipo == null)
            transform.rotation = rotacionBaseIzquierda;
        else
            ActualizarRotacionEnMano();

        RegistrarReferenciaAKSiCorresponde();
        if (cajaVisual != null)
        {
            Bounds boundsArma = ObtenerBoundsArma();
            if (boundsArma.size.sqrMagnitude > 0.00001f)
                cajaVisual.transform.position = boundsArma.center;
            ActualizarTransformCaja();
        }
        AjustarAreaDeRecogida();
    }

    protected virtual void Start()
    {
        // Si estamos en una escena de prueba sin GestorArmas, creamos el gestor automáticamente
        if (GestorArmas.Instancia == null && (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsListening))
        {
            GameObject go = new GameObject("GestorArmas_Auto");
            go.AddComponent<GestorArmas>();
        }
    }

    public void ConfigurarDatosArma(float cadencia, float alcance, int municionMax, float recarga)
    {
        ConfigurarDatosArma(cadencia, alcance, municionMax, recarga, modoDisparo, Dispersion, Relantizacion, perdigonesPorDisparo, retrocesoDisparoHorizontal, retrocesoDisparoVertical, destruyeYAtraviesaSuperficies);
    }

    public void ConfigurarDatosArma(
        float cadencia,
        float alcance,
        int municionMax,
        float recarga,
        ModoDisparo modo,
        float dispersion,
        float ralentizacion = 0f,
        int perdigones = 1,
        float retrocesoH = 0f,
        float retrocesoV = 0f,
        bool destruyeSuperficies = false)
    {
        Cadencia = cadencia;
        Alcance = alcance;
        municionMaxima = municionMax;
        tiempoRecarga = recarga;
        modoDisparo = modo;
        Dispersion = dispersion;
        Relantizacion = ralentizacion;
        perdigonesPorDisparo = Mathf.Max(1, perdigones);
        retrocesoDisparoHorizontal = retrocesoH;
        retrocesoDisparoVertical = retrocesoV;
        destruyeYAtraviesaSuperficies = destruyeSuperficies || gameObject.name.ToLowerInvariant().Contains("antitanq");

        if (modoDisparo == ModoDisparo.RayoContinuo)
        {
            duracionBateriaMaxima = municionMax > 0 ? (float)municionMax : 5.0f;
            alcanceRayo = alcance > 0f ? alcance : 80f;
        }

        if (!datosConfigurados)
        {
            datosConfigurados = true;
            municionActual = municionMax;
            if (modoDisparo == ModoDisparo.RayoContinuo)
            {
                duracionBateriaActual = duracionBateriaMaxima;
                tiempoCargaActual = 0f;
            }
        }
        else if (municionMaxima > 0)
        {
            municionActual = Mathf.Clamp(municionActual, 0, municionMaxima);
        }
    }

    public virtual void PrepararParaNuevaCaja()
    {
        datosConfigurados = true;
        municionActual = municionMaxima;
        duracionBateriaActual = duracionBateriaMaxima;
        tiempoCargaActual = 0f;
        recargando = false;
        tiempoEnSueloSinDuenio = 0f;
        despawnSolicitado = false;
        ActualizarVisualMunicion();
    }

    public void EstablecerEsperandoEnSpawn(bool estado)
    {
        esperandoRecogidaEnSpawn = estado;
        tiempoEnSueloSinDuenio = 0f;
        despawnSolicitado = false;
    }

    public virtual void EstablecerEstadoMunicion(int municion, float bateria)
    {
        datosConfigurados = true;
        municionActual = municionMaxima > 0 ? Mathf.Clamp(municion, 0, municionMaxima) : municion;
        duracionBateriaActual = Mathf.Clamp(bateria, 0f, duracionBateriaMaxima);
        ActualizarVisualMunicion();
    }

    protected virtual void ActualizarVisualMunicion()
    {
    }

    private void InicializarRenderizadoresArma()
    {
        var lista = new System.Collections.Generic.List<MeshRenderer>();
        foreach (MeshRenderer mr in GetComponentsInChildren<MeshRenderer>(true))
        {
            if (mr == null) continue;
            if (mr.gameObject.name == "CajaMaderaVisual" || (cajaVisual != null && mr.gameObject == cajaVisual))
                continue;
            lista.Add(mr);
        }
        renderizadoresArma = lista.ToArray();
        if (renderizadoresArma.Length > 0 && renderizadorArma == null)
            renderizadorArma = renderizadoresArma[0];
    }

    protected Bounds ObtenerBoundsArma()
    {
        if (renderizadoresArma == null || renderizadoresArma.Length == 0)
            InicializarRenderizadoresArma();

        bool tieneBounds = false;
        Bounds b = new Bounds(transform.position, Vector3.zero);
        if (renderizadoresArma != null)
        {
            foreach (MeshRenderer mr in renderizadoresArma)
            {
                if (mr == null) continue;

                MeshFilter mf = mr.GetComponent<MeshFilter>();
                Bounds mrBounds = (mf != null && mf.sharedMesh != null)
                    ? TransformarBoundsLocalAMundo(mf.sharedMesh.bounds, mr.transform.localToWorldMatrix)
                    : mr.bounds;

                if (mrBounds.size.sqrMagnitude < 0.000001f) continue;

                if (!tieneBounds)
                {
                    b = mrBounds;
                    tieneBounds = true;
                }
                else
                {
                    b.Encapsulate(mrBounds);
                }
            }
        }
        return b;
    }

    private static Bounds TransformarBoundsLocalAMundo(Bounds localBounds, Matrix4x4 matriz)
    {
        Vector3 c = localBounds.center;
        Vector3 e = localBounds.extents;
        Bounds resultado = new Bounds(matriz.MultiplyPoint3x4(new Vector3(c.x - e.x, c.y - e.y, c.z - e.z)), Vector3.zero);
        for (int ix = -1; ix <= 1; ix += 2)
        {
            for (int iy = -1; iy <= 1; iy += 2)
            {
                for (int iz = -1; iz <= 1; iz += 2)
                {
                    resultado.Encapsulate(matriz.MultiplyPoint3x4(new Vector3(c.x + ix * e.x, c.y + iy * e.y, c.z + iz * e.z)));
                }
            }
        }
        return resultado;
    }

    private void RegistrarReferenciaAKSiCorresponde()
    {
        string n = gameObject.name.ToLowerInvariant();
        if (!n.Contains("ak")) return;

        Quaternion rotGuardada = transform.rotation;
        Quaternion rotDerechaAK = Quaternion.Euler(0f, 180f, 0f) * rotacionBaseIzquierda * Quaternion.Euler(rotacionEnMano);
        transform.rotation = rotDerechaAK;

        Bounds b = ObtenerBoundsArma();
        if (b.size.sqrMagnitude > 0.00001f)
        {
            offsetReferenciaAKDerecha = b.center - transform.position;
            tieneOffsetReferenciaAK = true;
        }

        transform.rotation = rotGuardada;
    }

    private static void AsegurarReferenciaAKEnEscena()
    {
        if (tieneOffsetReferenciaAK) return;
        foreach (Arma a in FindObjectsByType<Arma>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (a != null && a.gameObject.name.ToLowerInvariant().Contains("ak"))
            {
                a.rotacionBaseIzquierda = a.CalcularRotacionBaseIzquierda();
                a.NormalizarEscalaArmaSiEsNecesario();
                a.RegistrarReferenciaAKSiCorresponde();
                if (tieneOffsetReferenciaAK) return;
            }
        }
    }

    private Quaternion CalcularRotacionBaseIzquierda()
    {
        string n = gameObject.name.ToLowerInvariant();
        if (n.Contains("ak") || n.Contains("machete") || n.Contains("platano") || n.Contains("penca"))
            return Quaternion.identity;

        if (n.Contains("escudo"))
            return Quaternion.Euler(0f, 90f, 90f);

        if (n.Contains("barreno"))
            return Quaternion.Euler(0f, 90f, 0f);

        if (n.Contains("escopeta"))
            return Quaternion.Euler(90f, 0f, 0f);

        if (n.Contains("antitanq"))
            return Quaternion.Euler(90f, 180f, 0f);

        MeshFilter mfRaiz = GetComponent<MeshFilter>();
        if (mfRaiz != null && mfRaiz.sharedMesh != null)
        {
            Vector3 sz = mfRaiz.sharedMesh.bounds.size;

            if (n.Contains("barret") || n.Contains("mac"))
            {
                // En barret.fbx y mac10.fbx la malla está en la raíz y tenía rotación (-90, 0, -90) en Blender:
                // el largo del cañón viene en el eje Z local y el alto en el eje X local.
                if (sz.z > sz.x)
                    return Quaternion.Euler(0f, -90f, 90f);
                return Quaternion.Euler(0f, 180f, 0f);
            }

            if (n.Contains("raygun"))
            {
                // En raygun.fbx la malla está en la raíz y tenía rotación (90, -180, 0) en Blender:
                // el largo del cañón viene en el eje X local pero el alto viene en el eje Z local.
                if (sz.z > sz.y)
                    return Quaternion.Euler(90f, 180f, 0f);
                return Quaternion.Euler(0f, 180f, 0f);
            }

            if (n.Contains("bereta"))
            {
                return Quaternion.Euler(0f, 180f, 0f);
            }
        }
        else
        {
            // Si la malla está en un hijo (como en ak47.fbx o con Preserve Hierarchy activo), el hijo ya guarda su giro de Blender
            if (n.Contains("barret") || n.Contains("mac") || n.Contains("bereta") || n.Contains("raygun"))
                return Quaternion.identity;
        }

        return mfRaiz != null ? rotacionInicial : Quaternion.identity;
    }

    private float ObtenerLargoObjetivoMetros()
    {
        string n = gameObject.name.ToLowerInvariant();
        if (n.Contains("antitanq")) return 1.08f; // francotirador pesado antitanques
        if (n.Contains("ak"))       return 0.743f; // rifle de asalto (escala 0.20 del AK-47)
        if (n.Contains("barret"))   return 0.93f;  // rifle francotirador
        if (n.Contains("escopeta")) return 0.80f;  // escopeta
        if (n.Contains("escudo"))   return 0.78f;  // escudo troyano
        if (n.Contains("machete"))  return 0.56f;  // machete
        if (n.Contains("mac"))      return 0.39f;  // subfusil compacto
        if (n.Contains("raygun"))   return 0.42f;  // arma de rayos
        if (n.Contains("barreno"))  return 0.34f;  // paquete de barrenos
        if (n.Contains("platano") || n.Contains("penca")) return 0.34f; // penca de plátanos
        if (n.Contains("bereta"))   return 0.28f;  // pistola
        return 0.55f;
    }

    private void NormalizarEscalaArmaSiEsNecesario()
    {
        string n = gameObject.name.ToLowerInvariant();
        if (n.Contains("ak"))
        {
            transform.localScale = new Vector3(0.20f, 0.20f, 0.20f);
            return;
        }

        if (transform.localScale.sqrMagnitude < 0.000001f)
            transform.localScale = Vector3.one;

        Quaternion rotGuardada = transform.rotation;
        transform.rotation = rotacionBaseIzquierda;

        Bounds b = ObtenerBoundsArma();
        float largoActual = Mathf.Max(b.size.x, Mathf.Max(b.size.y, b.size.z));
        float largoObjetivo = ObtenerLargoObjetivoMetros();

        if (largoActual > 0.0001f)
        {
            float factor = largoObjetivo / largoActual;
            if (Mathf.Abs(factor - 1f) > 0.01f)
            {
                transform.localScale *= factor;
            }
        }

        transform.rotation = rotGuardada;
    }

    private void CrearCajaVisualSiNoExiste()
    {
        Bounds boundsArma = ObtenerBoundsArma();
        Vector3 centroMesh = boundsArma.size.sqrMagnitude > 0.00001f ? boundsArma.center : transform.position;

        if (cajaVisual == null)
        {
            Transform existente = transform.Find("CajaMaderaVisual");
            if (existente != null)
            {
                cajaVisual = existente.gameObject;
            }
        }

        if (cajaVisual == null)
        {
            cajaVisual = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cajaVisual.name = "CajaMaderaVisual";

            Collider colCaja = cajaVisual.GetComponent<Collider>();
            if (colCaja != null) Destroy(colCaja);

            MeshRenderer mrCaja = cajaVisual.GetComponent<MeshRenderer>();
            if (mrCaja != null)
            {
                Color colorMadera = new Color(0.55f, 0.35f, 0.16f, 1f);
                Material mat = mrCaja.sharedMaterial != null
                    ? new Material(mrCaja.sharedMaterial)
                    : new Material(Shader.Find("Universal Render Pipeline/Lit"));
                mat.color = colorMadera;
                if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", colorMadera);
                mrCaja.material = mat;
            }

            cajaVisual.transform.SetParent(transform, true);
        }

        if (boundsArma.size.sqrMagnitude > 0.00001f)
            cajaVisual.transform.position = centroMesh;
        ActualizarTransformCaja();
    }

    private void ActualizarTransformCaja()
    {
        if (cajaVisual == null) return;
        cajaVisual.transform.rotation = Quaternion.identity;
        Vector3 escalaPadre = transform.lossyScale;
        cajaVisual.transform.localScale = new Vector3(
            Mathf.Abs(escalaPadre.x) > 0.0001f ? tamanoCaja.x / Mathf.Abs(escalaPadre.x) : tamanoCaja.x,
            Mathf.Abs(escalaPadre.y) > 0.0001f ? tamanoCaja.y / Mathf.Abs(escalaPadre.y) : tamanoCaja.y,
            Mathf.Abs(escalaPadre.z) > 0.0001f ? tamanoCaja.z / Mathf.Abs(escalaPadre.z) : tamanoCaja.z
        );
    }

    public void ColocarEnCaja(bool estado)
    {
        CrearCajaVisualSiNoExiste();
        enCaja = estado;

        if (renderizadoresArma == null || renderizadoresArma.Length == 0)
        {
            InicializarRenderizadoresArma();
        }

        if (renderizadoresArma != null)
        {
            foreach (MeshRenderer mr in renderizadoresArma)
            {
                if (mr != null) mr.enabled = !enCaja;
            }
        }

        if (cajaVisual != null)
        {
            cajaVisual.SetActive(enCaja);
            if (enCaja) ActualizarTransformCaja();
        }

        if (!enCaja)
        {
            ActualizarVisualMunicion();
        }
    }

    public void AbrirCajaLocal()
    {
        ColocarEnCaja(false);
        if (jugadorQueLaEquipo != null)
            ActualizarRotacionEnMano();
    }

    private Vector3 ObtenerCentroVisual()
    {
        if (cajaVisual != null) return cajaVisual.transform.position;
        Bounds b = ObtenerBoundsArma();
        if (b.size.sqrMagnitude > 0.00001f) return b.center;
        return transform.position;
    }

    // Ajusta el collider de recogida centrado en la caja/arma con un tamaño uniforme en espacio mundial (~1.5m)
    private void AjustarAreaDeRecogida()
    {
        Collider col = GetComponent<Collider>();
        if (col == null)
        {
            col = gameObject.AddComponent<BoxCollider>();
        }

        col.isTrigger = true;

        if (col is BoxCollider box)
        {
            if (cajaVisual != null)
                box.center = cajaVisual.transform.localPosition;
            else if (offsetAreaRecogida != Vector3.zero)
                box.center = offsetAreaRecogida;

            Vector3 escala = transform.lossyScale;
            float tamanoMundo = 1.5f;
            box.size = new Vector3(
                Mathf.Abs(escala.x) > 0.0001f ? tamanoMundo / Mathf.Abs(escala.x) : tamanoMundo,
                Mathf.Abs(escala.y) > 0.0001f ? tamanoMundo / Mathf.Abs(escala.y) : tamanoMundo,
                Mathf.Abs(escala.z) > 0.0001f ? tamanoMundo / Mathf.Abs(escala.z) : tamanoMundo
            );
        }
        else if (col is SphereCollider sphere)
        {
            if (offsetAreaRecogida != Vector3.zero) sphere.center = offsetAreaRecogida;
        }
        else if (col is CapsuleCollider capsule)
        {
            if (offsetAreaRecogida != Vector3.zero) capsule.center = offsetAreaRecogida;
        }
    }

    private bool EstaJugadorRealmenteCerca(Personaje jugador)
    {
        if (jugador == null || jugador.EstaMuriendo || jugador.EnRagdoll || !jugador.gameObject.activeInHierarchy)
            return false;

        Vector3 centroArma = ObtenerCentroVisual();
        Vector3 centroJugador = jugador.transform.position + Vector3.up * 0.85f;
        float distXY = Vector2.Distance(
            new Vector2(centroArma.x, centroArma.y),
            new Vector2(centroJugador.x, centroJugador.y));

        if (distXY <= 1.85f && Mathf.Abs(centroArma.z - centroJugador.z) <= 2.2f)
            return true;

        Collider colArma = GetComponent<Collider>();
        Collider colJugador = jugador.GetComponent<Collider>();
        if (colArma != null && colJugador != null && colArma.enabled && colJugador.enabled)
            return colArma.bounds.Intersects(colJugador.bounds);

        return false;
    }

    //TERMINADO (El arma detecta al jugador cerca)
    protected virtual void OnTriggerEnter(Collider other)//Funcion para recoger el Arma si el jugador esta cercas (NO LE CAMBIEN EL NOMBRE PORFAS)
    {
        if (jugadorQueLaEquipo != null) return;
        if (other.CompareTag("Player"))
        {
            // solo cuenta el jugador de esta PC (o en escena de prueba sin red)
            Personaje p = other.GetComponentInParent<Personaje>();
            if (p == null || p.EstaMuriendo || p.EnRagdoll || (p.IsSpawned && !p.IsOwner)) return;

            jugadorCerca = true;
            mano=p.transform.Find("Mano");
            Debug.Log("Cerca del arma, presiona C para recoger");
        }
    }

    protected virtual void OnTriggerStay(Collider other)
    {
        if (!jugadorCerca && jugadorQueLaEquipo == null && other.CompareTag("Player"))
        {
            Personaje p = other.GetComponentInParent<Personaje>();
            if (p == null || p.EstaMuriendo || p.EnRagdoll || (p.IsSpawned && !p.IsOwner)) return;

            jugadorCerca = true;
            mano = p.transform.Find("Mano");
        }
    }

    protected virtual void OnTriggerExit(Collider other) //Pa saber que el jugador se alejo y ya no la esta tocando
    {
        if (other.CompareTag("Player"))
        {
            Personaje p = other.GetComponentInParent<Personaje>();
            if (p == null || (p.IsSpawned && !p.IsOwner)) return;

            jugadorCerca = false;
            if (mano == p.transform.Find("Mano")) mano = null;
        }
    }

    protected virtual void Update()
    {
        // Limpiar estado obsoleto tras muerte, ragdoll o teletransporte de spawn/ronda
        if (jugadorCerca)
        {
            Personaje candidato = mano != null ? mano.GetComponentInParent<Personaje>() : null;
            if (jugadorQueLaEquipo != null || !EstaJugadorRealmenteCerca(candidato))
            {
                jugadorCerca = false;
                mano = null;
            }
        }

        if (jugadorCerca && Keyboard.current != null && AjustesJuego.RecienPresionada(Accion.Recoger) && jugadorQueLaEquipo == null)
        {
            if (Time.frameCount != ultimoFrameAccionC)
            {
                Personaje jugador = mano != null ? mano.GetComponentInParent<Personaje>() : null;
                if (jugador != null && jugador.armaEquipada == null && EstaJugadorRealmenteCerca(jugador))
                {
                    RecogerArma();
                }
            }
        }

        if (cayendo) ActualizarCaida();

        // Actualizar cooldown para evitar que se suelte inmediatamente
        if (tiempoRecogida > 0f)
        {
            tiempoRecogida -= Time.deltaTime;
        }

        // Si el arma está equipada, actualizar su rotación para que siga al personaje
        if (jugadorQueLaEquipo != null)
        {
            ActualizarRotacionEnMano();
        }
        else if (gameObject.activeInHierarchy && !EstaEnVuelo && (!enCaja || !esperandoRecogidaEnSpawn))
        {
            // Arma sacada de caja (o soltada en el suelo): despawnea tras un tiempo si nadie la recoge
            float limiteDespawn = GestorArmas.Instancia != null
                ? GestorArmas.Instancia.TiempoDespawnArmaSuelo
                : tiempoDespawnEnSuelo;

            tiempoEnSueloSinDuenio += Time.deltaTime;
            float restante = limiteDespawn - tiempoEnSueloSinDuenio;
            if (restante > 0f && restante <= 3f)
            {
                bool parpadeoVisible = Mathf.FloorToInt(tiempoEnSueloSinDuenio * 7f) % 2 == 0;
                AplicarParpadeoDespawn(parpadeoVisible);
            }

            if (tiempoEnSueloSinDuenio >= limiteDespawn && !despawnSolicitado)
            {
                bool esAutoridad = NetworkManager.Singleton == null
                    || !NetworkManager.Singleton.IsListening
                    || NetworkManager.Singleton.IsServer;

                if (esAutoridad && GestorArmas.Instancia != null)
                {
                    despawnSolicitado = true;
                    GestorArmas.Instancia.DespawnearArmaPorTiempo(this);
                }
            }
        }

        if (modoDisparo == ModoDisparo.RayoContinuo || estadoAudioRaygun != EstadoAudioRaygun.Inactivo)
        {
            ActualizarAudioRaygun();
        }
    }

    private void AplicarParpadeoDespawn(bool visible)
    {
        if (enCaja)
        {
            if (cajaVisual != null) cajaVisual.SetActive(visible);
            return;
        }
        if (visible)
        {
            if (renderizadoresArma != null)
            {
                foreach (MeshRenderer mr in renderizadoresArma)
                    if (mr != null) mr.enabled = true;
            }
            ActualizarVisualMunicion();
        }
        else
        {
            if (renderizadoresArma != null)
            {
                foreach (MeshRenderer mr in renderizadoresArma)
                    if (mr != null) mr.enabled = false;
            }
        }
    }

    protected virtual void LateUpdate()
    {
        // Se vuelve a aplicar en LateUpdate para quedar sincronizada después del giro del Personaje
        if (jugadorQueLaEquipo != null)
        {
            ActualizarRotacionEnMano();

            if (disparandoRayo && (!jugadorQueLaEquipo.IsSpawned || jugadorQueLaEquipo.IsOwner))
            {
                ActualizarFisicaYVisualRayo();
            }
        }

        ActualizarEfectoKamehameha();
    }

    // Efecto visual tipo Kamehameha (carga y haz) solo para la raygun
    private EfectoKamehameha efectoKame;
    private void ActualizarEfectoKamehameha()
    {
        if (modoDisparo != ModoDisparo.RayoContinuo) return;
        bool cargando = cargandoRayo || cargandoRayoRemoto;
        bool disparando = disparandoRayo || disparandoRayoRemoto;
        if (efectoKame == null)
        {
            if (!cargando && !disparando) return;
            efectoKame = GetComponent<EfectoKamehameha>();
            if (efectoKame == null) efectoKame = gameObject.AddComponent<EfectoKamehameha>();
        }

        Vector3 boca = transform.position;
        Bounds b = ObtenerBoundsArma();
        if (b.size.sqrMagnitude > 0.00001f && jugadorQueLaEquipo != null)
        {
            float dirX = jugadorQueLaEquipo.DireccionHorizontal;
            boca = PegadoAPared()
                ? new Vector3(b.center.x, b.max.y, jugadorQueLaEquipo.transform.position.z)
                : new Vector3(dirX > 0f ? b.max.x : b.min.x, b.center.y, jugadorQueLaEquipo.transform.position.z);
        }
        efectoKame.Actualizar(cargando, disparando, boca, trazo, anchoRayo);
    }

    protected virtual void OnDisable()
    {
        DetenerRayoContinuo();
    }

    protected virtual void ActualizarRotacionEnMano()
    {
        if (jugadorQueLaEquipo == null) return;

        if (renderizadorArma == null)
            renderizadorArma = GetComponentInChildren<MeshRenderer>();

        AsegurarReferenciaAKEnEscena();

        bool mirandoIzquierda = jugadorQueLaEquipo.DireccionHorizontal > 0f;
        Vector3 posJugador = jugadorQueLaEquipo.transform.position;

        // 1. Calculamos primero la pose cuando el personaje mira a la derecha (-X, rotación Y = -90°),
        //    alineando el centro real del mesh de cualquier arma exactamente al mismo offset respecto a Mano que el AK-47.
        Quaternion rotJugDerecha = Quaternion.Euler(0f, -90f, 0f);
        Vector3 offsetManoLocal = mano != null ? (mano.localPosition + posicionEnMano) : posicionEnMano;
        Vector3 posManoDerecha = posJugador
            + (rotJugDerecha * Vector3.Scale(jugadorQueLaEquipo.transform.localScale, offsetManoLocal));
        Quaternion rotArmaDerecha = Quaternion.Euler(0f, 180f, 0f) * rotacionBaseIzquierda * Quaternion.Euler(rotacionEnMano);

        transform.rotation = rotArmaDerecha;
        transform.position = posManoDerecha;

        Bounds boundsDerecha = ObtenerBoundsArma();
        if (boundsDerecha.size.sqrMagnitude > 0.00001f)
        {
            Vector3 centroDeseadoDerecha = posManoDerecha + offsetReferenciaAKDerecha;
            transform.position += (centroDeseadoDerecha - boundsDerecha.center);
        }

        // 2. Si el personaje mira a la izquierda (+X), rotamos el arma 180° en Y (apuntando a +X)
        //    y espejeamos el centro visual del mesh en X respecto al personaje, manteniendo
        //    exactamente la misma altura (Y) y profundidad hacia la cámara (Z) para que no se oculte detrás.
        if (mirandoIzquierda)
        {
            Bounds bDer = ObtenerBoundsArma();
            Vector3 centroDerecha = bDer.size.sqrMagnitude > 0.00001f ? bDer.center : ObtenerCentroVisual();
            Vector3 deltaDerecha = centroDerecha - posJugador;

            Quaternion rotArmaIzquierda = rotacionBaseIzquierda * Quaternion.Euler(rotacionEnMano);
            transform.rotation = rotArmaIzquierda;

            Bounds bIzq = ObtenerBoundsArma();
            Vector3 centroActual = bIzq.size.sqrMagnitude > 0.00001f ? bIzq.center : ObtenerCentroVisual();
            Vector3 centroDeseadoIzquierda = posJugador + new Vector3(-deltaDerecha.x, deltaDerecha.y, deltaDerecha.z);
            transform.position += (centroDeseadoIzquierda - centroActual);
        }

        if (enCaja)
        {
            ActualizarTransformCaja();
        }
        else
        {
            AplicarApuntarArriba();
        }
    }

    // Animacion al estar pegado a una pared: el arma se levanta suavemente hasta apuntar hacia arriba
    private float pesoApuntarArriba;
    private int ultimoFrameApuntarArriba = -1;
    private const float AnguloApuntarArriba = 80f;
    public float PesoApuntarArriba => pesoApuntarArriba;

    private void AplicarApuntarArriba()
    {
        // el peso se actualiza una sola vez por cuadro (esta funcion se llama en Update y LateUpdate)
        if (ultimoFrameApuntarArriba != Time.frameCount)
        {
            ultimoFrameApuntarArriba = Time.frameCount;
            bool objetivo = jugadorQueLaEquipo != null && !recargando && PegadoAPared();
            pesoApuntarArriba = Mathf.MoveTowards(pesoApuntarArriba, objetivo ? 1f : 0f, Time.deltaTime * 7f);
        }
        if (pesoApuntarArriba < 0.001f) return;

        Bounds b = ObtenerBoundsArma();
        Vector3 pivote = b.size.sqrMagnitude > 0.00001f ? b.center : transform.position;
        // mirando a +X se gira +90 en Z; mirando a -X, -90, en ambos casos el canon termina hacia +Y
        float signo = jugadorQueLaEquipo.DireccionHorizontal > 0f ? 1f : -1f;
        float suave = Mathf.SmoothStep(0f, 1f, pesoApuntarArriba);
        transform.RotateAround(pivote, Vector3.forward, signo * AnguloApuntarArriba * suave);
    }
    
    //TERMINADO
    protected virtual void RecogerArma()
    {
        if (mano == null) return;
        Personaje jugador = mano.GetComponentInParent<Personaje>(); //Busca el script Personaje subiendo desde la mano
        if (jugador == null || jugador.armaEquipada != null || !EstaJugadorRealmenteCerca(jugador))
        {
            jugadorCerca = false;
            mano = null;
            return;
        }

        ultimoFrameAccionC = Time.frameCount;
        cayendo = false;
        tiempoEnSueloSinDuenio = 0f;
        despawnSolicitado = false;
        if (esperandoRecogidaEnSpawn)
        {
            esperandoRecogidaEnSpawn = false;
            if (GestorArmas.Instancia != null)
                GestorArmas.Instancia.NotificarCajaArmaTomada(this);
        }
        Debug.Log("Arma recogida");

        transform.parent=mano;//Esto hace que el arma pase a ser hija de "la mano" pa que se ponga ahi pues
        transform.localPosition=posicionEnMano; //En donde se va a quedar agarrada
        // Rotación se maneja dinámicamente en Update()

        Debug.Log($"Después de parent - Arma Pos: {transform.position}, Arma Rot: {transform.rotation.eulerAngles}");
        Debug.Log($"Mano Pos: {mano.position}, Mano Rot: {mano.rotation.eulerAngles}");

        GetComponent<Collider>().enabled=false;//Desabilita OnTriggerExit y Enter para que no se la puedan robar (¿Seria interesante si se pudiera?)
        jugadorCerca = false; // con el collider apagado OnTriggerExit ya no se llama, si no se podra recoger otra vez
        recargando = false;
        ColocarEnCaja(enCaja); // restaura visibilidad si venia parpadeando en el suelo, sin reiniciar balas
        tiempoRecogida = 0.5f; // Cooldown de 0.5s para evitar que se suelte inmediatamente
        jugador.armaEquipada = this; //Le dice al personaje "esta es tu arma ahora"
        jugadorQueLaEquipo=jugador; //Para solucionar un problema que tomo 3 horas resolver...
        ActualizarRotacionEnMano();
        Red red = jugador.GetComponent<Red>();
        if (red != null && red.IsSpawned)
            red.AvisarArmaRpc(gameObject.name, municionActual, duracionBateriaActual); // sincroniza arma y balas restantes
    }

    // para que los demas jugadores vean el trazo de un disparo que no es suyo
    public virtual void MostrarDisparo(Vector3 inicio, Vector3 fin)
    {
        ContadorDisparos++;
        UltimoInicioTrazo = inicio;
        UltimoFinTrazo = fin;
        if (corrutinaTrazo != null) StopCoroutine(corrutinaTrazo);
        corrutinaTrazo = StartCoroutine(MostrarTrazo(inicio, fin));

        // Si es escopeta y llega un disparo remoto, dibujar tambien los perdigones visuales
        if (perdigonesPorDisparo > 1)
        {
            float dist = Vector3.Distance(inicio, fin);
            Vector3 dirBase = (fin - inicio).normalized;
            int extras = perdigonesPorDisparo - 1;
            for (int i = 0; i < extras; i++)
            {
                float offsetY = ((i - (extras - 1) * 0.5f) / Mathf.Max(1f, extras)) * Dispersion * 0.22f;
                Vector3 dirPellet = new Vector3(dirBase.x, dirBase.y + offsetY, 0f).normalized;
                Vector3 finPellet = inicio + dirPellet * dist;
                finPellet.z = inicio.z;
                StartCoroutine(MostrarTrazoExtraEscopeta(i, inicio, finPellet));
            }
        }
    }

    private IEnumerator MostrarTrazo(Vector3 inicio, Vector3 fin)
    {
        if (trazo == null) trazo = GetComponent<LineRenderer>();
        if (trazo == null) yield break;
        trazo.positionCount = 2;
        trazo.SetPosition(0, inicio);
        trazo.SetPosition(1, fin);
        trazo.startWidth = 0.05f;
        trazo.endWidth = 0.05f;
        trazo.enabled = true;
        float duracionTrazo = modoDisparo == ModoDisparo.Automatico ? Mathf.Clamp(0.6f / Mathf.Max(1f, Cadencia), 0.025f, 0.075f) : 0.1f;
        yield return new WaitForSeconds(duracionTrazo);
        if (!disparandoRayo && !disparandoRayoRemoto)
            trazo.enabled = false;
    }

    private LineRenderer ObtenerTrazoExtraEscopeta(int indice)
    {
        while (trazosExtraEscopeta.Count <= indice)
        {
            GameObject goTrazo = new GameObject($"TrazoPerdigon_{trazosExtraEscopeta.Count}");
            goTrazo.transform.SetParent(transform, false);
            LineRenderer lr = goTrazo.AddComponent<LineRenderer>();
            if (trazo == null) trazo = GetComponent<LineRenderer>();
            if (trazo != null && trazo.sharedMaterial != null)
            {
                lr.sharedMaterial = trazo.sharedMaterial;
                lr.startColor = trazo.startColor;
                lr.endColor = trazo.endColor;
            }
            else
            {
                Shader sh = Shader.Find("Sprites/Default");
                if (sh != null) lr.material = new Material(sh);
                lr.startColor = new Color(1f, 0.85f, 0.3f, 0.9f);
                lr.endColor = new Color(1f, 0.55f, 0.1f, 0.3f);
            }
            lr.startWidth = 0.035f;
            lr.endWidth = 0.025f;
            lr.positionCount = 2;
            lr.enabled = false;
            trazosExtraEscopeta.Add(lr);
        }
        return trazosExtraEscopeta[indice];
    }

    private IEnumerator MostrarTrazoExtraEscopeta(int indice, Vector3 inicio, Vector3 fin)
    {
        LineRenderer lr = ObtenerTrazoExtraEscopeta(indice);
        if (lr == null) yield break;
        lr.positionCount = 2;
        lr.SetPosition(0, inicio);
        lr.SetPosition(1, fin);
        lr.enabled = true;
        yield return new WaitForSeconds(0.09f);
        if (lr != null) lr.enabled = false;
    }

    // Entrada secundaria (tecla F o clic derecho, ej. lanzar machete)
    public virtual void ProcesarEntradaSecundaria(bool presionoEsteFrame, bool presionando)
    {
    }

    // Procesa la entrada continua o por pulsos segun el modo del arma
    public virtual void ProcesarEntradaDisparo(bool presionando, bool presionoEsteFrame, bool soltoEsteFrame)
    {
        if (jugadorQueLaEquipo == null) return;

        // Si esta dentro de una caja de madera, cualquier pulsacion de V la rompe
        if (enCaja)
        {
            if (presionoEsteFrame) RomperCaja();
            return;
        }

        if (recargando)
        {
            if (disparandoRayo || cargandoRayo) DetenerRayoContinuo();
            return;
        }

        switch (modoDisparo)
        {
            case ModoDisparo.Semiautomatico:
                if (presionoEsteFrame)
                {
                    if (municionActual <= 0 && municionMaxima > 0)
                    {
                        Sonidos.Reproducir("sin_municion", transform.position, 0.8f);
                        return;
                    }
                    Disparar();
                }
                break;

            case ModoDisparo.Automatico:
                if (presionando)
                {
                    if (municionActual <= 0)
                    {
                        if (presionoEsteFrame) Sonidos.Reproducir("sin_municion", transform.position, 0.8f);
                        return;
                    }
                    if (Time.time >= tiempoUltimoDisparo + (1f / Cadencia))
                    {
                        Disparar();
                    }
                }
                break;

            case ModoDisparo.RayoContinuo:
                ProcesarRayoContinuo(presionando, presionoEsteFrame, soltoEsteFrame);
                break;
        }
    }

    private void ProcesarRayoContinuo(bool presionando, bool presionoEsteFrame, bool soltoEsteFrame)
    {
        if (duracionBateriaActual <= 0f)
        {
            if (disparandoRayo || cargandoRayo) DetenerRayoContinuo();
            if (presionoEsteFrame) Sonidos.Reproducir("sin_municion", transform.position, 0.8f);
            return;
        }

        if (presionando)
        {
            if (jugadorQueLaEquipo != null) jugadorQueLaEquipo.CancelarInmunidadPorAtaque();
            if (tiempoCargaActual < tiempoCargaRequerido)
            {
                if (!cargandoRayo)
                {
                    cargandoRayo = true;
                    IniciarAudioCarga();
                    if (jugadorQueLaEquipo != null && jugadorQueLaEquipo.IsSpawned && jugadorQueLaEquipo.IsOwner)
                    {
                        jugadorQueLaEquipo.ActualizarCargaRayoRpc(true);
                    }
                }
                tiempoCargaActual += Time.deltaTime;

                if (tiempoCargaActual >= tiempoCargaRequerido)
                {
                    tiempoCargaActual = tiempoCargaRequerido;
                    cargandoRayo = false;
                    disparandoRayo = true;
                    IniciarAudioRayo();
                    ActualizarFisicaYVisualRayo();
                }
            }
            else
            {
                disparandoRayo = true;
                duracionBateriaActual -= Time.deltaTime;
                if (duracionBateriaActual <= 0f)
                {
                    duracionBateriaActual = 0f;
                    DetenerRayoContinuo();
                    return;
                }
                ActualizarFisicaYVisualRayo();
            }
        }
        else
        {
            if (cargandoRayo || disparandoRayo || tiempoCargaActual > 0f)
            {
                DetenerRayoContinuo();
            }
        }
    }

    // true cuando el tirador tiene pegado enfrente CUALQUIER objeto solido del mapa (pared, caja, mueble, columna...):
    // en ese caso se dispara hacia arriba. Se lanzan varios rayos a distintas alturas del cuerpo
    // (pies, rodillas, pecho y cabeza) para detectar tambien objetos bajos o que solo tapan una parte del cuerpo.
    private static readonly float[] AlturasDeteccion = { 0.15f, 0.4f, 0.65f, 0.9f };

    private bool PegadoAPared()
    {
        if (jugadorQueLaEquipo == null) return false;

        Transform pj = jugadorQueLaEquipo.transform;
        CharacterController cc = pj.GetComponent<CharacterController>();
        float escala = Mathf.Max(pj.lossyScale.x, pj.lossyScale.z);
        float radio = cc != null ? cc.radius * escala : 0.3f;
        float alto = cc != null ? cc.height * pj.lossyScale.y : 1.8f;
        Vector3 piso = cc != null ? pj.TransformPoint(cc.center) - Vector3.up * (alto / 2f) : pj.position;
        Vector3 haciaFrente = new Vector3(jugadorQueLaEquipo.DireccionHorizontal, 0f, 0f);
        float distancia = radio + 0.3f;

        foreach (float fraccion in AlturasDeteccion)
        {
            Vector3 origen = piso + Vector3.up * (alto * fraccion);
            RaycastHit[] hits = Physics.RaycastAll(origen, haciaFrente, distancia, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            foreach (RaycastHit hit in hits)
            {
                if (hit.distance <= 0.05f) continue; // colliders que envuelven el origen (como la cochera)
                if (hit.collider.transform.IsChildOf(pj)) continue;
                if (hit.collider.GetComponentInParent<Personaje>() != null) continue; // otros jugadores no cuentan
                if (hit.collider.GetComponentInParent<Arma>() != null) continue;      // armas tiradas en el piso tampoco
                if (destruyeYAtraviesaSuperficies && hit.collider.GetComponentInParent<ObjetoDestruible>() != null) continue;
                if (hit.normal.y > 0.6f) continue; // rampa o piso inclinado, no es un obstaculo de frente
                return true;
            }
        }
        return false;
    }

    private void ActualizarFisicaYVisualRayo()
    {
        if (jugadorQueLaEquipo == null) return;

        float dirX = jugadorQueLaEquipo.DireccionHorizontal;
        bool haciaArriba = PegadoAPared();
        Vector3 direccion = haciaArriba ? Vector3.up : new Vector3(dirX, 0f, 0f).normalized;

        Vector3 inicioTrazo = transform.position;
        Bounds b = ObtenerBoundsArma();
        if (b.size.sqrMagnitude > 0.00001f)
        {
            inicioTrazo = haciaArriba
                ? new Vector3(b.center.x, b.max.y, b.center.z)
                : new Vector3(dirX > 0f ? b.max.x : b.min.x, b.center.y, b.center.z);
        }

        Vector3 origen = inicioTrazo;
        origen.x = jugadorQueLaEquipo.transform.position.x;
        origen.z = jugadorQueLaEquipo.transform.position.z;

        float distanciaMax = alcanceRayo > 0f ? alcanceRayo : (Alcance > 20f ? Alcance : 80f);
        float distanciaImpacto = distanciaMax;
        Vector3 puntoFinal = origen + direccion * distanciaMax;

        // Choque con pared u obstaculo solido
        RaycastHit[] impactosPared = Physics.RaycastAll(origen, direccion, distanciaMax, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
        float menorDistanciaSolida = float.MaxValue;
        foreach (RaycastHit hit in impactosPared)
        {
            if (hit.distance <= 0.05f) continue;
            if (hit.collider.transform.IsChildOf(jugadorQueLaEquipo.transform) ||
                hit.collider.GetComponentInParent<Personaje>() == jugadorQueLaEquipo)
                continue;

            Personaje p = hit.collider.GetComponentInParent<Personaje>();
            if (p == null)
            {
                if (hit.distance < menorDistanciaSolida)
                {
                    menorDistanciaSolida = hit.distance;
                    distanciaImpacto = hit.distance;
                    puntoFinal = hit.point;
                }
            }
        }

        // Colision gruesa del ancho del personaje (SphereCastAll)
        RaycastHit[] impactosEnemigos = Physics.SphereCastAll(origen, radioRayo, direccion, distanciaImpacto, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
        foreach (RaycastHit hit in impactosEnemigos)
        {
            if (hit.collider == null) continue;
            Personaje victima = hit.collider.GetComponentInParent<Personaje>();
            if (victima != null && victima != jugadorQueLaEquipo)
            {
                if (!victimasAlcanzadas.Contains(victima))
                {
                    victimasAlcanzadas.Add(victima);
                    victima.RecibirImpactoDeDisparo(jugadorQueLaEquipo, hit.point, direccion);
                }
            }
        }

        // Visual del rayo
        if (trazo == null) trazo = GetComponent<LineRenderer>();
        Vector3 finTrazo = new Vector3(puntoFinal.x, puntoFinal.y, inicioTrazo.z);
        if (trazo != null)
        {
            trazo.positionCount = 2;
            trazo.SetPosition(0, inicioTrazo);
            trazo.SetPosition(1, finTrazo);
            trazo.startWidth = anchoRayo;
            trazo.endWidth = anchoRayo;
            trazo.widthCurve = AnimationCurve.Constant(0f, 1f, 1f);
            trazo.widthMultiplier = anchoRayo;
            trazo.enabled = true;
        }

        if (jugadorQueLaEquipo != null && jugadorQueLaEquipo.IsSpawned)
        {
            jugadorQueLaEquipo.ActualizarEstadoRayoRpc(true, inicioTrazo, finTrazo, anchoRayo);
        }
    }

    public void DetenerRayoContinuo()
    {
        bool estabaDisparando = disparandoRayo || disparandoRayoRemoto;
        bool estabaCargando = cargandoRayo;

        tiempoCargaActual = 0f;
        tiempoCargaRemota = 0f;
        cargandoRayo = false;
        cargandoRayoRemoto = false;
        disparandoRayo = false;
        victimasAlcanzadas.Clear();
        DetenerAudiosRayo(estabaDisparando);

        if (trazo != null) trazo.enabled = false;

        if (jugadorQueLaEquipo != null && jugadorQueLaEquipo.IsSpawned && jugadorQueLaEquipo.IsOwner)
        {
            if (estabaCargando)
                jugadorQueLaEquipo.ActualizarCargaRayoRpc(false);
            jugadorQueLaEquipo.ActualizarEstadoRayoRpc(false, Vector3.zero, Vector3.zero, 0f);
            jugadorQueLaEquipo.NotificarCambioMunicionRed(this);
        }
    }

    public void ActualizarCargaRayoRemoto(bool cargando)
    {
        if (cargando)
        {
            if (!cargandoRayoRemoto && !disparandoRayoRemoto)
            {
                cargandoRayoRemoto = true;
                tiempoCargaRemota = 0f;
                IniciarAudioCarga();
            }
        }
        else
        {
            if (cargandoRayoRemoto && !disparandoRayoRemoto)
            {
                cargandoRayoRemoto = false;
                tiempoCargaRemota = 0f;
                DetenerAudiosRayo(false);
            }
        }
    }

    public void ActualizarRayoRemoto(bool activo, Vector3 inicio, Vector3 fin, float ancho)
    {
        if (trazo == null) trazo = GetComponent<LineRenderer>();
        if (trazo != null)
        {
            if (activo)
            {
                trazo.positionCount = 2;
                trazo.SetPosition(0, inicio);
                trazo.SetPosition(1, fin);
                trazo.startWidth = ancho;
                trazo.endWidth = ancho;
                trazo.widthCurve = AnimationCurve.Constant(0f, 1f, 1f);
                trazo.widthMultiplier = ancho;
                trazo.enabled = true;
                cargandoRayoRemoto = false;
                if (!disparandoRayoRemoto)
                {
                    disparandoRayoRemoto = true;
                    IniciarAudioRayo();
                }
            }
            else
            {
                bool veniaDisparando = disparandoRayoRemoto;
                trazo.enabled = false;
                disparandoRayoRemoto = false;
                cargandoRayoRemoto = false;
                DetenerAudiosRayo(veniaDisparando);
            }
        }
    }

    private AudioClip ObtenerClipRaygun()
    {
        if (clipRaygunCache != null) return clipRaygunCache;

        clipRaygunCache = Resources.Load<AudioClip>("Audio/raygun_disparo");
        if (clipRaygunCache == null)
            clipRaygunCache = Resources.Load<AudioClip>("Audio/raygun audio disparo");
#if UNITY_EDITOR
        if (clipRaygunCache == null)
            clipRaygunCache = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/raygun audio disparo.mp3");
#endif
        return clipRaygunCache;
    }

    private void AsegurarFuenteAudioRaygun()
    {
        if (audioLoopRayo != null) return;
        audioLoopRayo = GetComponent<AudioSource>();
        if (audioLoopRayo == null)
            audioLoopRayo = gameObject.AddComponent<AudioSource>();

        audioLoopRayo.playOnAwake = false;
        audioLoopRayo.loop = false;
        audioLoopRayo.spatialBlend = 0f;
    }

    private float CalcularAtenuacionDistancia()
    {
        Camera cam = Camera.main;
        if (cam == null) return AjustesJuego.Armas;
        float d = Vector2.Distance(
            new Vector2(cam.transform.position.x, cam.transform.position.y),
            new Vector2(transform.position.x, transform.position.y));
        return Mathf.Clamp01(1f - d / GestorSonido.DistanciaMaxima) * AjustesJuego.Armas; // sonido del rayo: usa el volumen de armas
    }

    private void IniciarAudioCarga()
    {
        AsegurarFuenteAudioRaygun();
        AudioClip clip = ObtenerClipRaygun();

        if (clip != null && clip.length > RAYGUN_CLIP_PICO_DISPARO + 0.5f && audioLoopRayo != null)
        {
            float duracionSegmentoCarga = RAYGUN_CLIP_PICO_DISPARO - RAYGUN_CLIP_INICIO_CARGA; // 3.00s
            audioLoopRayo.Stop();
            audioLoopRayo.clip = clip;
            audioLoopRayo.loop = false;
            audioLoopRayo.pitch = duracionSegmentoCarga / Mathf.Max(0.25f, tiempoCargaRequerido);
            audioLoopRayo.time = Mathf.Clamp(RAYGUN_CLIP_INICIO_CARGA, 0f, clip.length - 0.2f);
            audioLoopRayo.volume = 0.24f * CalcularAtenuacionDistancia();
            audioLoopRayo.Play();
            estadoAudioRaygun = EstadoAudioRaygun.Cargando;
            return;
        }

        // Respaldo si el clip aún no existe
        Sonidos.Reproducir("tick", transform.position, 1.2f, 1.6f);
    }

    private void IniciarAudioRayo()
    {
        AsegurarFuenteAudioRaygun();
        AudioClip clip = ObtenerClipRaygun();

        if (clip != null && clip.length > RAYGUN_CLIP_PICO_DISPARO + 0.5f && audioLoopRayo != null)
        {
            audioLoopRayo.pitch = 1.0f;
            audioLoopRayo.loop = false;

            // Si venía reproduciendo la carga de 3s y ya llegó justo al pico (11.75s), continuar sin corte
            bool veniaEnPunto = audioLoopRayo.clip == clip
                && audioLoopRayo.isPlaying
                && Mathf.Abs(audioLoopRayo.time - RAYGUN_CLIP_PICO_DISPARO) < 0.45f;

            if (!veniaEnPunto)
            {
                audioLoopRayo.clip = clip;
                audioLoopRayo.time = Mathf.Clamp(RAYGUN_CLIP_PICO_DISPARO, 0f, clip.length - 0.2f);
                audioLoopRayo.Play();
            }

            audioLoopRayo.volume = RAYGUN_VOLUMEN_DISPARO * CalcularAtenuacionDistancia();
            estadoAudioRaygun = EstadoAudioRaygun.Disparando;
            return;
        }

        AudioClip fallback = Resources.Load<AudioClip>("Audio/raygun_laser");
        if (fallback == null) fallback = Resources.Load<AudioClip>("Audio/disparo_raygun");
        if (fallback != null && audioLoopRayo != null)
        {
            audioLoopRayo.clip = fallback;
            audioLoopRayo.loop = true;
            audioLoopRayo.pitch = 1.0f;
            audioLoopRayo.volume = 0.55f * CalcularAtenuacionDistancia();
            audioLoopRayo.Play();
            estadoAudioRaygun = EstadoAudioRaygun.Disparando;
        }
    }

    private void DetenerAudiosRayo(bool veniaDisparandoRayo)
    {
        if (audioLoopRayo == null || !audioLoopRayo.isPlaying)
        {
            estadoAudioRaygun = EstadoAudioRaygun.Inactivo;
            return;
        }

        AudioClip clip = audioLoopRayo.clip;
        if (veniaDisparandoRayo && clip != null && clip == ObtenerClipRaygun() && clip.length > RAYGUN_CLIP_COLA_APAGADO + 0.3f)
        {
            // Pasar a la cola de enfriamiento del audio y desvanecer suavemente
            audioLoopRayo.pitch = 1.0f;
            audioLoopRayo.loop = false;
            if (audioLoopRayo.time < RAYGUN_CLIP_COLA_APAGADO)
                audioLoopRayo.time = RAYGUN_CLIP_COLA_APAGADO;
            velocidadFadeRaygun = RAYGUN_VOLUMEN_DISPARO / 0.45f;
            estadoAudioRaygun = EstadoAudioRaygun.Desvaneciendo;
            return;
        }

        // Si canceló la carga antes de los 3s, desvanecer rápido sin reproducir el disparo
        velocidadFadeRaygun = Mathf.Max(audioLoopRayo.volume, 0.3f) / 0.12f;
        estadoAudioRaygun = EstadoAudioRaygun.Desvaneciendo;
    }

    private void ActualizarAudioRaygun()
    {
        if (audioLoopRayo == null)
        {
            estadoAudioRaygun = EstadoAudioRaygun.Inactivo;
            return;
        }

        switch (estadoAudioRaygun)
        {
            case EstadoAudioRaygun.Cargando:
                if (!cargandoRayo && !cargandoRayoRemoto)
                {
                    DetenerAudiosRayo(false);
                    break;
                }
                if (cargandoRayoRemoto) tiempoCargaRemota += Time.deltaTime;
                float tActual = cargandoRayo ? tiempoCargaActual : tiempoCargaRemota;
                float progreso = Mathf.Clamp01(tActual / Mathf.Max(0.1f, tiempoCargaRequerido));
                audioLoopRayo.volume = Mathf.Lerp(0.24f, RAYGUN_VOLUMEN_DISPARO, progreso) * CalcularAtenuacionDistancia();
                break;

            case EstadoAudioRaygun.Disparando:
                if (!disparandoRayo && !disparandoRayoRemoto)
                {
                    DetenerAudiosRayo(true);
                    break;
                }
                audioLoopRayo.volume = RAYGUN_VOLUMEN_DISPARO * CalcularAtenuacionDistancia();

                // Si usa el clip personalizado, hacer bucle continuo dentro de la ventana energética del rayo (12.25s..14.45s)
                AudioClip clip = ObtenerClipRaygun();
                if (clip != null && audioLoopRayo.clip == clip && !audioLoopRayo.loop)
                {
                    float limiteFin = Mathf.Min(RAYGUN_CLIP_LOOP_FIN, clip.length - 0.15f);
                    if (!audioLoopRayo.isPlaying || audioLoopRayo.time >= limiteFin)
                    {
                        audioLoopRayo.time = Mathf.Min(RAYGUN_CLIP_LOOP_INICIO, limiteFin - 0.2f);
                        if (!audioLoopRayo.isPlaying) audioLoopRayo.Play();
                    }
                }
                break;

            case EstadoAudioRaygun.Desvaneciendo:
                if (!audioLoopRayo.isPlaying)
                {
                    estadoAudioRaygun = EstadoAudioRaygun.Inactivo;
                    break;
                }
                audioLoopRayo.volume = Mathf.MoveTowards(audioLoopRayo.volume, 0f, velocidadFadeRaygun * Time.deltaTime);
                if (audioLoopRayo.volume <= 0.002f)
                {
                    audioLoopRayo.Stop();
                    estadoAudioRaygun = EstadoAudioRaygun.Inactivo;
                }
                break;
        }
    }

    public virtual void Disparar()
    {
        // Si el modo es rayo continuo, se canaliza por la logica de rayo
        if (modoDisparo == ModoDisparo.RayoContinuo)
        {
            ProcesarRayoContinuo(true, true, false);
            return;
        }

        // Si el jugador tiene una caja de madera en las manos, presionar V la rompe para descubrir el arma
        if (enCaja)
        {
            RomperCaja();
            return;
        }

        Debug.Log("Disparar() llamado");
        if (recargando) return; // no se puede disparar mientras recarga

        //¿Se quedo sin balas?
        if (municionActual <= 0)
        {
            Debug.Log("Sin municion, presiona R para recargar");
            return;
        }

        //¿Esta disparando muy rapido?
        if (Time.time < tiempoUltimoDisparo + (1f / Cadencia))
        {
            return;
        }

        tiempoUltimoDisparo = Time.time;
        municionActual--;
        ContadorDisparos++;
        if (jugadorQueLaEquipo != null) jugadorQueLaEquipo.CancelarInmunidadPorAtaque();

        Debug.Log("jugadorQueLaEquipo: " + (jugadorQueLaEquipo != null ? jugadorQueLaEquipo.name : "null"));
        Debug.Log($"Arma Rot al disparar: {transform.rotation.eulerAngles}");

        // Dirección horizontal hacia donde mira el personaje (+1 = izquierda/+X, -1 = derecha/-X)
        float dirX = jugadorQueLaEquipo != null
            ? jugadorQueLaEquipo.DireccionHorizontal
            : (transform.right.x >= 0f ? 1f : -1f);

        // Pegado de frente a una pared: el disparo sale hacia arriba en vez de hacia adelante
        bool haciaArriba = PegadoAPared();

        // Punto visual de salida en la boca del arma
        Vector3 inicioTrazo = transform.position;
        Bounds b = ObtenerBoundsArma();
        if (b.size.sqrMagnitude > 0.00001f)
        {
            inicioTrazo = haciaArriba
                ? new Vector3(b.center.x, b.max.y, b.center.z)
                : new Vector3(dirX > 0f ? b.max.x : b.min.x, b.center.y, b.center.z);
        }

        // El rayo sale a la altura del arma pero en el mismo plano Z que el jugador
        Vector3 origen = inicioTrazo;
        if (jugadorQueLaEquipo != null)
        {
            origen.x = jugadorQueLaEquipo.transform.position.x;
            origen.z = jugadorQueLaEquipo.transform.position.z;
        }

        int numRayos = Mathf.Max(1, perdigonesPorDisparo);
        HashSet<Personaje> golpeadosEnEsteDisparo = new HashSet<Personaje>();
        Vector3 finTrazoPrincipal = haciaArriba
            ? (inicioTrazo + new Vector3(0f, Alcance, 0f))
            : (inicioTrazo + new Vector3(dirX * Alcance, 0f, 0f));

        for (int i = 0; i < numRayos; i++)
        {
            float spread;
            if (numRayos > 1)
            {
                // Abanico amplio para la escopeta + ligera variacion aleatoria
                float t = (i / (float)(numRayos - 1)) - 0.5f; // -0.5 .. +0.5
                spread = (t * Dispersion * 0.24f) + Random.Range(-Dispersion, Dispersion) * 0.03f;
            }
            else
            {
                spread = Random.Range(-Dispersion, Dispersion) * 0.1f;
            }

            Vector3 direccionDisparo = haciaArriba
                ? new Vector3(spread, 1f, 0f).normalized
                : new Vector3(dirX, spread, 0f).normalized;
            Debug.DrawRay(origen, direccionDisparo * Alcance, Color.red, 2f);

            // Ignoramos triggers, colliders que envuelven el origen (distancia ~0, como Cochera convexa) y al propio tirador
            RaycastHit[] impactos = Physics.RaycastAll(origen, direccionDisparo, Alcance, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            System.Array.Sort(impactos, (a, b) => a.distance.CompareTo(b.distance));

            Vector3 puntoFinal = origen + direccionDisparo * Alcance;
            HashSet<ObjetoDestruible> destruiblesEnRayo = null;

            foreach (RaycastHit hit in impactos)
            {
                if (hit.distance <= 0.05f || hit.collider == null) continue;

                if (jugadorQueLaEquipo != null &&
                    (hit.collider.transform.IsChildOf(jugadorQueLaEquipo.transform) ||
                     hit.collider.GetComponentInParent<Personaje>() == jugadorQueLaEquipo))
                {
                    continue;
                }

                ObjetoDestruible destruible = hit.collider.GetComponentInParent<ObjetoDestruible>();
                if (destruible != null && !destruible.Destruido)
                {
                    if (destruyeYAtraviesaSuperficies)
                    {
                        if (destruiblesEnRayo == null) destruiblesEnRayo = new HashSet<ObjetoDestruible>();
                        if (destruiblesEnRayo.Add(destruible))
                        {
                            GestorEntornoDestruible.SolicitarDestruirObstaculo(destruible, hit.point, direccionDisparo, false);
                        }
                        // El Antitanques atraviesa la superficie destruible en el mismo disparo
                        continue;
                    }

                    // Para el resto de armas, la cobertura destruible detiene el disparo
                    puntoFinal = hit.point;
                    break;
                }

                Debug.Log("Le diste a: " + hit.collider.name + " en la posicion: " + hit.point);
                Personaje personajeGolpeado = hit.collider.GetComponentInParent<Personaje>();
                if (personajeGolpeado != null)
                {
                    if (personajeGolpeado != jugadorQueLaEquipo && !golpeadosEnEsteDisparo.Contains(personajeGolpeado))
                    {
                        golpeadosEnEsteDisparo.Add(personajeGolpeado);
                        personajeGolpeado.RecibirImpactoDeDisparo(jugadorQueLaEquipo, hit.point, direccionDisparo);
                    }
                }

                puntoFinal = hit.point;
                break;
            }

            Vector3 finTrazo = new Vector3(puntoFinal.x, puntoFinal.y, inicioTrazo.z);
            if (i == 0)
            {
                finTrazoPrincipal = finTrazo;
            }
            else
            {
                StartCoroutine(MostrarTrazoExtraEscopeta(i - 1, inicioTrazo, finTrazo));
            }
        }

        // Retroceso fisico al disparar (AK-47 suave, francotiradores Barret/Antitanques mas exagerado)
        if (!haciaArriba && jugadorQueLaEquipo != null && (retrocesoDisparoHorizontal > 0f || retrocesoDisparoVertical > 0f))
        {
            jugadorQueLaEquipo.RecibirRetrocesoDisparo(retrocesoDisparoHorizontal, retrocesoDisparoVertical);
        }

        // El trazo visual principal mantiene el plano Z de la boca del arma
        UltimoInicioTrazo = inicioTrazo;
        UltimoFinTrazo = finTrazoPrincipal;
        if (corrutinaTrazo != null) StopCoroutine(corrutinaTrazo);
        corrutinaTrazo = StartCoroutine(MostrarTrazo(inicioTrazo, finTrazoPrincipal));
        if (jugadorQueLaEquipo != null && jugadorQueLaEquipo.IsSpawned)
        {
            jugadorQueLaEquipo.MostrarDisparoRpc(inicioTrazo, finTrazoPrincipal);
            jugadorQueLaEquipo.NotificarCambioMunicionRed(this);
        }
    }

    public void AdjuntarA(Transform nuevaMano, Personaje jugador, int municionSync = -1, float bateriaSync = -1f)
    {
        mano = nuevaMano;
        transform.parent = mano;
        transform.localPosition = posicionEnMano;
        GetComponent<Collider>().enabled = false;
        jugador.armaEquipada = this;
        jugadorQueLaEquipo = jugador;
        esperandoRecogidaEnSpawn = false;
        tiempoEnSueloSinDuenio = 0f;
        despawnSolicitado = false;
        ActualizarRotacionEnMano();
        recargando = false;
        if (municionSync >= 0)
        {
            EstablecerEstadoMunicion(municionSync, bateriaSync >= 0f ? bateriaSync : duracionBateriaActual);
        }
        ColocarEnCaja(enCaja);
        tiempoCargaActual = 0f;
        DetenerRayoContinuo();
    }

    // Se llama al presionar R. Solo recarga manualmente si el jugador tiene el cargador extra guardado en su Personaje.
    public void IniciarRecarga()
    {
        if (recargando || jugadorQueLaEquipo == null || enCaja) return;
        if (municionMaxima <= 0 && modoDisparo != ModoDisparo.RayoContinuo) return; // Armas cuerpo a cuerpo / escudo no usan cargador

        if (modoDisparo == ModoDisparo.RayoContinuo)
        {
            if (duracionBateriaActual >= duracionBateriaMaxima) return;
        }
        else
        {
            if (municionActual >= municionMaxima) return;
        }

        if (jugadorQueLaEquipo.CargadoresExtra <= 0)
        {
            Sonidos.Reproducir("sin_municion", transform.position, 0.85f);
            Debug.Log("No tienes cargador extra guardado. Busca una caja de municion.");
            return;
        }

        StartCoroutine(RecargarAhoraSi());
    }

    private IEnumerator RecargarAhoraSi()
    {
        recargando = true;
        if (disparandoRayo || cargandoRayo) DetenerRayoContinuo();
        Debug.Log("Recargando...");
        yield return new WaitForSeconds(tiempoRecarga);

        if (jugadorQueLaEquipo != null && jugadorQueLaEquipo.ConsumirCargadorExtra())
        {
            municionActual = municionMaxima;
            duracionBateriaActual = duracionBateriaMaxima;
            tiempoCargaActual = 0f;
            ActualizarVisualMunicion();
            AlCompletarRecarga();
            jugadorQueLaEquipo.NotificarCambioMunicionRed(this);
        }
        recargando = false;
    }

    protected virtual void AlCompletarRecarga()
    {
    }

    private void RomperCaja()
    {
        Personaje portador = jugadorQueLaEquipo;
        esperandoRecogidaEnSpawn = false;
        if (GestorArmas.Instancia != null)
            GestorArmas.Instancia.NotificarCajaArmaTomada(this);
        ColocarEnCaja(false);

        if (portador != null)
        {
            Red red = portador.GetComponent<Red>();
            if (red != null && red.IsSpawned)
            {
                red.AvisarRomperCajaRpc(gameObject.name);
            }
        }

        if (soltarAlRomperCaja)
        {
            float dirX = portador != null ? portador.DireccionHorizontal : 1f;
            Vector3 posBase = portador != null ? portador.transform.position : transform.position;
            // Cae al suelo frente al jugador para que él u otro jugador la pueda tomar/robar con C
            Vector3 posDrop = posBase + new Vector3(dirX * 0.45f, 0.25f, 0f);

            if (portador != null && portador.armaEquipada == this)
                portador.armaEquipada = null;
            jugadorQueLaEquipo = null;

            Red red = portador != null ? portador.GetComponent<Red>() : null;
            if (red != null && red.IsSpawned && NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
            {
                red.SoltarArmaEnPosicionServerRpc(gameObject.name, posDrop, false, municionActual, duracionBateriaActual);
            }
            else
            {
                Soltar(posDrop);
            }
        }
        else
        {
            ActualizarRotacionEnMano();
        }
    }

    // Suelta el arma: la desancla del jugador y la coloca en posicion (el suelo)
    // Conserva exactamente las balas restantes que tenia el arma (no rellena cargador gratis)
    public virtual void Soltar(Vector3 posicion)
    {
        // Limpiar referencia del jugador que la tenía
        if (jugadorQueLaEquipo != null)
        {
            if (jugadorQueLaEquipo.armaEquipada == this)
                jugadorQueLaEquipo.armaEquipada = null;
            jugadorQueLaEquipo = null;
        }
        mano = null;
        jugadorCerca = false;
        StopAllCoroutines(); // cancela una recarga en curso, si la habia
        recargando = false;
        if (trazo != null) trazo.enabled = false;
        tiempoCargaActual = 0f;
        tiempoEnSueloSinDuenio = 0f;
        despawnSolicitado = false;
        DetenerRayoContinuo();
        ColocarEnCaja(enCaja);

        transform.parent = null;
        transform.rotation = rotacionBaseIzquierda;
        transform.position = posicion;

        // Compensamos el desfase del pivote del FBX para que el centro visual de la caja/arma quede exactamente en posicion
        Vector3 centroActual = ObtenerCentroVisual();
        transform.position += (posicion - centroActual);

        if (enCaja) ActualizarTransformCaja();

        Collider col = GetComponent<Collider>();
        if (col != null) col.enabled = true;

        IniciarCaida(); // cae hasta el piso en vez de quedar flotando
    }

    // Primer collider solido bajo el punto (ignora triggers, jugadores y otras armas)
    private bool BuscarPiso(Vector3 origen, float distancia, out float yPiso)
    {
        yPiso = 0f;
        RaycastHit[] hits = Physics.RaycastAll(origen, Vector3.down, distancia, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
        float mejor = float.MaxValue; bool hay = false;
        foreach (RaycastHit h in hits)
        {
            if (h.collider.GetComponentInParent<Personaje>() != null) continue;
            if (h.collider.GetComponentInParent<Arma>() != null) continue;
            if (h.distance < mejor) { mejor = h.distance; yPiso = h.point.y; hay = true; }
        }
        return hay;
    }

    private float MitadAltoVisual()
    {
        if (enCaja) return tamanoCaja.y * 0.5f;
        Bounds b = ObtenerBoundsArma();
        return b.size.sqrMagnitude > 0.00001f ? b.extents.y : 0.1f;
    }

    private void IniciarCaida()
    {
        velocidadCaida = 0f;
        Vector3 centro = ObtenerCentroVisual();
        // solo cae si hay piso debajo; si no, se queda donde esta (no se pierde en el vacio)
        cayendo = BuscarPiso(centro + Vector3.up * 0.3f, DISTANCIA_MAX_CAIDA, out _);
    }

    private void ActualizarCaida()
    {
        if (jugadorQueLaEquipo != null || transform.parent != null) { cayendo = false; return; }
        velocidadCaida += GRAVEDAD_ARMA * Time.deltaTime;
        float paso = velocidadCaida * Time.deltaTime;
        Vector3 centro = ObtenerCentroVisual();
        float mitad = MitadAltoVisual();

        if (BuscarPiso(centro + Vector3.up * 0.3f, DISTANCIA_MAX_CAIDA, out float yPiso))
        {
            float yFinal = yPiso + mitad;            // la base queda al ras del piso
            float yNueva = centro.y - paso;
            if (yNueva <= yFinal) { transform.position += Vector3.up * (yFinal - centro.y); cayendo = false; velocidadCaida = 0f; return; }
            transform.position += Vector3.down * paso;
        }
        else cayendo = false;
    }

    // Suelta el arma voluntariamente con tecla C (sincronizado en red)
    public void SoltarVoluntariamente()
    {
        Debug.Log("SoltarVoluntariamente() llamado");
        DetenerRayoContinuo();
        if (jugadorQueLaEquipo == null)
        {
            Debug.Log("jugadorQueLaEquipo es null, no se puede soltar");
            return;
        }

        ultimoFrameAccionC = Time.frameCount;
        esperandoRecogidaEnSpawn = false;
        Personaje portador = jugadorQueLaEquipo;
        float dirX = portador.DireccionHorizontal;
        Vector3 posicionDrop = portador.transform.position + new Vector3(dirX * 0.45f, 0.25f, 0f);
        bool seguiaEnCaja = enCaja;
        int municionRestante = municionActual;
        float bateriaRestante = duracionBateriaActual;

        if (portador.armaEquipada == this)
            portador.armaEquipada = null;
        jugadorQueLaEquipo = null;

        Red red = portador.GetComponent<Red>();
        if (red != null && red.IsSpawned && NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
        {
            red.SoltarArmaEnPosicionServerRpc(gameObject.name, posicionDrop, seguiaEnCaja, municionRestante, bateriaRestante);
        }
        else
        {
            Soltar(posicionDrop);
        }
    }
}