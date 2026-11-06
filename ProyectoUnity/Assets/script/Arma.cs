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
    private bool jugadorCerca = false; //Esto dejas aber si el jugador esta tocando al arma
    private Transform mano; //El objeto al que se pegara cuando el jugador lo agarre (debe ser invisible)
    private float tiempoUltimoDisparo=0f;
    private Personaje jugadorQueLaEquipo;
    private LineRenderer trazo;
    private float tiempoRecogida = 0f; // Para evitar que se suelte inmediatamente después de recoger
    private Quaternion rotacionInicial;
    private Quaternion rotacionBaseIzquierda;
    private MeshRenderer renderizadorArma;
    private MeshRenderer[] renderizadoresArma;
    private bool enCaja = false;
    private GameObject cajaVisual;
    private static int ultimoFrameAccionC = -1;

    [Header("Modo de Disparo")]
    [SerializeField] protected ModoDisparo modoDisparo = ModoDisparo.Semiautomatico;

    [Header("Raygun (Rayo Continuo)")]
    [SerializeField] protected float tiempoCargaRequerido = 3.0f;
    [SerializeField] protected float duracionBateriaMaxima = 10.0f;
    [SerializeField] protected float anchoRayo = 1.0f;
    [SerializeField] protected float radioRayo = 0.5f;
    [SerializeField] protected float alcanceRayo = 80.0f;

    private float tiempoCargaActual = 0f;
    private float duracionBateriaActual = 10.0f;
    private bool disparandoRayo = false;
    private bool cargandoRayo = false;
    private bool disparandoRayoRemoto = false;
    private AudioSource audioLoopRayo;
    private HashSet<Personaje> victimasAlcanzadas = new HashSet<Personaje>();

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
    private int municionActual;
    private bool recargando = false;

    [Header("Area de recogida (trigger en el suelo)")]
    [Tooltip("Desplazamiento del centro del collider de recogida. Vector3.zero = no tocar el centro que ya tiene el collider en el editor")]
    [SerializeField] protected Vector3 offsetAreaRecogida = Vector3.zero;
    [Tooltip("Tamanio del area de recogida (solo aplica si el collider es un BoxCollider). Vector3.zero = no tocar el tamanio que ya tiene en el editor")]
    [SerializeField] protected Vector3 tamanoAreaRecogida = Vector3.zero;

    public int MunicionActual => modoDisparo == ModoDisparo.RayoContinuo ? Mathf.CeilToInt(duracionBateriaActual) : municionActual;
    public int MunicionMaxima => modoDisparo == ModoDisparo.RayoContinuo ? Mathf.CeilToInt(duracionBateriaMaxima) : municionMaxima;
    public bool Recargando => recargando;
    public bool EnCaja => enCaja;
    public string NombreMostrado => enCaja ? "Caja de madera" : (string.IsNullOrEmpty(nombreMostrado) ? gameObject.name : nombreMostrado);
    public float TiempoRecogida => tiempoRecogida;
    public ModoDisparo Modo => modoDisparo;
    public float AnchoRayo => anchoRayo;
    public float TiempoCargaActual => tiempoCargaActual;
    public float TiempoCargaRequerido => tiempoCargaRequerido;
    public float DuracionBateriaActual => duracionBateriaActual;
    public float DuracionBateriaMaxima => duracionBateriaMaxima;
    public bool DisparandoRayo => disparandoRayo;
    public bool CargandoRayo => cargandoRayo;

    protected virtual void Awake()
    {
        municionActual = municionMaxima;
        duracionBateriaActual = duracionBateriaMaxima;
        rotacionInicial = transform.rotation;
        rotacionBaseIzquierda = CalcularRotacionBaseIzquierda();

        if (jugadorQueLaEquipo == null)
            transform.rotation = rotacionBaseIzquierda;

        InicializarRenderizadoresArma();
        if (renderizadoresArma != null)
        {
            foreach (MeshRenderer mr in renderizadoresArma)
                if (mr != null) mr.enabled = true;
        }

        NormalizarEscalaArmaSiEsNecesario();
        CrearCajaVisualSiNoExiste();
        AjustarAreaDeRecogida();
        ColocarEnCaja(iniciarEnCaja);
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
        ConfigurarDatosArma(cadencia, alcance, municionMax, recarga, modoDisparo, Dispersion);
    }

    public void ConfigurarDatosArma(float cadencia, float alcance, int municionMax, float recarga, ModoDisparo modo, float dispersion)
    {
        Cadencia = cadencia;
        Alcance = alcance;
        municionMaxima = municionMax;
        municionActual = municionMax;
        tiempoRecarga = recarga;
        modoDisparo = modo;
        Dispersion = dispersion;

        if (modoDisparo == ModoDisparo.RayoContinuo)
        {
            duracionBateriaMaxima = municionMax > 0 ? (float)municionMax : 10.0f;
            duracionBateriaActual = duracionBateriaMaxima;
            tiempoCargaActual = 0f;
            alcanceRayo = alcance > 0f ? alcance : 80f;
        }
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

    private Bounds ObtenerBoundsArma()
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
                if (!tieneBounds)
                {
                    b = mr.bounds;
                    tieneBounds = true;
                }
                else
                {
                    b.Encapsulate(mr.bounds);
                }
            }
        }
        return b;
    }

    private Quaternion CalcularRotacionBaseIzquierda()
    {
        string n = gameObject.name.ToLowerInvariant();
        if (n.Contains("barret"))
            return Quaternion.Euler(0f, 90f, -90f);
        if (n.Contains("mac10") || n.Contains("mac"))
            return Quaternion.Euler(0f, 0f, 180f);
        if (n.Contains("bereta") || n.Contains("raygun"))
            return Quaternion.Euler(0f, 180f, 0f);
        if (n.Contains("ak"))
            return Quaternion.identity;

        return GetComponent<MeshFilter>() != null ? rotacionInicial : Quaternion.identity;
    }

    private void NormalizarEscalaArmaSiEsNecesario()
    {
        string n = gameObject.name.ToLowerInvariant();
        if (n.Contains("ak"))
        {
            if (transform.localScale.x > 0.8f) transform.localScale = new Vector3(0.2f, 0.2f, 0.2f);
            return;
        }
        if (n.Contains("barret"))
        {
            if (transform.localScale.x > 0.8f) transform.localScale = new Vector3(0.208f, 0.208f, 0.208f);
            return;
        }
        if (n.Contains("mac"))
        {
            if (transform.localScale.x > 0.8f) transform.localScale = new Vector3(0.25f, 0.25f, 0.25f);
            return;
        }
        if (n.Contains("bereta"))
        {
            if (transform.localScale.x < 2f) transform.localScale = new Vector3(25f, 25f, 25f);
            return;
        }
        if (n.Contains("raygun"))
        {
            if (transform.localScale.x > 0.8f) transform.localScale = new Vector3(0.15f, 0.15f, 0.15f);
            return;
        }

        Bounds b = ObtenerBoundsArma();
        float maxDim = Mathf.Max(b.size.x, Mathf.Max(b.size.y, b.size.z));
        if (maxDim > 0.0001f && (maxDim < 0.15f || maxDim > 1.8f))
        {
            float factor = 0.55f / maxDim;
            transform.localScale *= factor;
        }
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

    //TERMINADO (El arma detecta al jugador cerca)
    protected virtual void OnTriggerEnter(Collider other)//Funcion para recoger el Arma si el jugador esta cercas (NO LE CAMBIEN EL NOMBRE PORFAS)
    {
        if (other.CompareTag("Player"))
        {
            // solo cuenta el jugador de esta PC (o en escena de prueba sin red)
            Personaje p = other.GetComponentInParent<Personaje>();
            if (p == null || (p.IsSpawned && !p.IsOwner)) return;

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
            if (p == null || (p.IsSpawned && !p.IsOwner)) return;

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
        }
    }

    protected virtual void Update()
    {
        if (jugadorCerca && Keyboard.current != null && Keyboard.current.cKey.wasPressedThisFrame && jugadorQueLaEquipo == null)
        {
            if (Time.frameCount != ultimoFrameAccionC)
            {
                Personaje jugador = mano != null ? mano.GetComponentInParent<Personaje>() : null;
                if (jugador != null && jugador.armaEquipada == null)
                {
                    RecogerArma();
                }
            }
        }

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
    }

    protected virtual void OnDisable()
    {
        DetenerRayoContinuo();
    }

    private void ActualizarRotacionEnMano()
    {
        if (jugadorQueLaEquipo == null) return;

        if (renderizadorArma == null)
            renderizadorArma = GetComponentInChildren<MeshRenderer>();

        bool mirandoIzquierda = jugadorQueLaEquipo.DireccionHorizontal > 0f;
        Vector3 posJugador = jugadorQueLaEquipo.transform.position;

        // 1. Calculamos primero la pose cuando el personaje mira a la derecha (-X, rotación Y = -90°),
        //    donde la posición de Mano y el pivote del FBX ya colocan el arma frente al personaje (+Z).
        Quaternion rotJugDerecha = Quaternion.Euler(0f, -90f, 0f);
        Vector3 offsetManoLocal = mano != null ? (mano.localPosition + posicionEnMano) : posicionEnMano;
        Vector3 posManoDerecha = posJugador
            + (rotJugDerecha * Vector3.Scale(jugadorQueLaEquipo.transform.localScale, offsetManoLocal));
        Quaternion rotArmaDerecha = Quaternion.Euler(0f, 180f, 0f) * rotacionBaseIzquierda * Quaternion.Euler(rotacionEnMano);

        transform.rotation = rotArmaDerecha;
        transform.position = posManoDerecha;

        // 2. Si el personaje mira a la izquierda (+X), rotamos el arma 180° en Y (apuntando a +X)
        //    y espejeamos el centro visual del mesh en X respecto al personaje, manteniendo
        //    exactamente la misma altura (Y) y profundidad hacia la cámara (Z) para que no se oculte detrás.
        if (mirandoIzquierda)
        {
            Vector3 centroDerecha = ObtenerCentroVisual();
            Vector3 deltaDerecha = centroDerecha - posJugador;

            Quaternion rotArmaIzquierda = rotacionBaseIzquierda * Quaternion.Euler(rotacionEnMano);
            transform.rotation = rotArmaIzquierda;

            Vector3 centroActual = ObtenerCentroVisual();
            Vector3 centroDeseadoIzquierda = posJugador + new Vector3(-deltaDerecha.x, deltaDerecha.y, deltaDerecha.z);
            transform.position += (centroDeseadoIzquierda - centroActual);
        }

        if (enCaja)
        {
            ActualizarTransformCaja();
        }
    }
    
    //TERMINADO
    protected virtual void RecogerArma()
    {
        if (mano == null) return;
        Personaje jugador = mano.GetComponentInParent<Personaje>(); //Busca el script Personaje subiendo desde la mano
        if (jugador == null || jugador.armaEquipada != null) return;

        ultimoFrameAccionC = Time.frameCount;
        Debug.Log("Arma recogida");

        transform.parent=mano;//Esto hace que el arma pase a ser hija de "la mano" pa que se ponga ahi pues
        transform.localPosition=posicionEnMano; //En donde se va a quedar agarrada
        // Rotación se maneja dinámicamente en Update()

        Debug.Log($"Después de parent - Arma Pos: {transform.position}, Arma Rot: {transform.rotation.eulerAngles}");
        Debug.Log($"Mano Pos: {mano.position}, Mano Rot: {mano.rotation.eulerAngles}");

        GetComponent<Collider>().enabled=false;//Desabilita OnTriggerExit y Enter para que no se la puedan robar (¿Seria interesante si se pudiera?)
        jugadorCerca = false; // con el collider apagado OnTriggerExit ya no se llama, si no se podra recoger otra vez
        recargando = false;
        municionActual = municionMaxima; // el arma se recoge con el cargador lleno
        tiempoRecogida = 0.5f; // Cooldown de 0.5s para evitar que se suelte inmediatamente
        jugador.armaEquipada = this; //Le dice al personaje "esta es tu arma ahora"
        jugadorQueLaEquipo=jugador; //Para solucionar un problema que tomo 3 horas resolver...
        ActualizarRotacionEnMano();
        Red red = jugador.GetComponent<Red>();
        if (red != null && red.IsSpawned)
            red.AvisarArmaRpc(gameObject.name); // por nombre, para que los demas peguen esta misma arma
    }

    // para que los demas jugadores vean el trazo de un disparo que no es suyo
    public void MostrarDisparo(Vector3 inicio, Vector3 fin)
    {
        StartCoroutine(MostrarTrazo(inicio, fin));
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
        float duracionTrazo = modoDisparo == ModoDisparo.Automatico ? Mathf.Clamp(0.7f / Cadencia, 0.025f, 0.08f) : 0.1f;
        yield return new WaitForSeconds(duracionTrazo);
        if (!disparandoRayo && !disparandoRayoRemoto)
            trazo.enabled = false;
    }

    // Procesa la entrada continua o por pulsos segun el modo del arma
    public void ProcesarEntradaDisparo(bool presionando, bool presionoEsteFrame, bool soltoEsteFrame)
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
                if (presionoEsteFrame) Disparar();
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
            if (tiempoCargaActual < tiempoCargaRequerido)
            {
                if (!cargandoRayo)
                {
                    cargandoRayo = true;
                    IniciarAudioCarga();
                }
                tiempoCargaActual += Time.deltaTime;

                if (tiempoCargaActual >= tiempoCargaRequerido)
                {
                    tiempoCargaActual = tiempoCargaRequerido;
                    cargandoRayo = false;
                    disparandoRayo = true;
                    IniciarAudioRayo();
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

    private void ActualizarFisicaYVisualRayo()
    {
        if (jugadorQueLaEquipo == null) return;

        float dirX = jugadorQueLaEquipo.DireccionHorizontal;
        Vector3 direccion = new Vector3(dirX, 0f, 0f).normalized;

        Vector3 inicioTrazo = transform.position;
        Bounds b = ObtenerBoundsArma();
        if (b.size.sqrMagnitude > 0.00001f)
        {
            inicioTrazo = new Vector3(dirX > 0f ? b.max.x : b.min.x, b.center.y, b.center.z);
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
                    victima.RecibirDaño(jugadorQueLaEquipo);
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
        tiempoCargaActual = 0f;
        cargandoRayo = false;
        disparandoRayo = false;
        victimasAlcanzadas.Clear();
        DetenerAudiosRayo();

        if (trazo != null) trazo.enabled = false;

        if (jugadorQueLaEquipo != null && jugadorQueLaEquipo.IsSpawned)
        {
            jugadorQueLaEquipo.ActualizarEstadoRayoRpc(false, Vector3.zero, Vector3.zero, 0f);
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
                if (!disparandoRayoRemoto)
                {
                    disparandoRayoRemoto = true;
                    IniciarAudioRayo();
                }
            }
            else
            {
                trazo.enabled = false;
                disparandoRayoRemoto = false;
                DetenerAudiosRayo();
            }
        }
    }

    private void IniciarAudioCarga()
    {
        Sonidos.Reproducir("tick", transform.position, 1.2f, 1.6f);
    }

    private void IniciarAudioRayo()
    {
        if (audioLoopRayo == null)
        {
            audioLoopRayo = gameObject.AddComponent<AudioSource>();
            audioLoopRayo.playOnAwake = false;
            audioLoopRayo.loop = true;
            audioLoopRayo.spatialBlend = 0f;
        }
        AudioClip clip = Resources.Load<AudioClip>("Audio/raygun_laser");
        if (clip == null) clip = Resources.Load<AudioClip>("Audio/disparo_raygun");
        if (clip != null && audioLoopRayo != null)
        {
            audioLoopRayo.clip = clip;
            audioLoopRayo.volume = 0.55f;
            audioLoopRayo.Play();
        }
    }

    private void DetenerAudiosRayo()
    {
        if (audioLoopRayo != null && audioLoopRayo.isPlaying)
        {
            audioLoopRayo.Stop();
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

        Debug.Log("jugadorQueLaEquipo: " + (jugadorQueLaEquipo != null ? jugadorQueLaEquipo.name : "null"));
        Debug.Log($"Arma Rot al disparar: {transform.rotation.eulerAngles}");

        // Dirección horizontal hacia donde mira el personaje (+1 = izquierda/+X, -1 = derecha/-X)
        float dirX = jugadorQueLaEquipo != null
            ? jugadorQueLaEquipo.DireccionHorizontal
            : (transform.right.x >= 0f ? 1f : -1f);

        Vector3 direccionDisparo = new Vector3(
            dirX,
            Random.Range(-Dispersion, Dispersion) * 0.1f,
            0f
        ).normalized;
        Debug.Log("direccionDisparo: " + direccionDisparo);

        // Punto visual de salida en la boca del arma
        Vector3 inicioTrazo = transform.position;
        Bounds b = ObtenerBoundsArma();
        if (b.size.sqrMagnitude > 0.00001f)
        {
            inicioTrazo = new Vector3(dirX > 0f ? b.max.x : b.min.x, b.center.y, b.center.z);
        }

        // El rayo sale a la altura del arma pero en el mismo plano Z que el jugador
        Vector3 origen = inicioTrazo;
        if (jugadorQueLaEquipo != null)
        {
            origen.x = jugadorQueLaEquipo.transform.position.x;
            origen.z = jugadorQueLaEquipo.transform.position.z;
        }

        Debug.DrawRay(origen, direccionDisparo * Alcance, Color.red, 2f);

        // Ignoramos triggers, colliders que envuelven el origen (distancia ~0, como Cochera convexa) y al propio tirador
        RaycastHit[] impactos = Physics.RaycastAll(origen, direccionDisparo, Alcance, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
        RaycastHit? impactoValido = null;
        float menorDistancia = float.MaxValue;

        foreach (RaycastHit hit in impactos)
        {
            if (hit.distance <= 0.05f) continue;

            if (jugadorQueLaEquipo != null &&
                (hit.collider.transform.IsChildOf(jugadorQueLaEquipo.transform) ||
                 hit.collider.GetComponentInParent<Personaje>() == jugadorQueLaEquipo))
            {
                continue;
            }

            if (hit.distance < menorDistancia)
            {
                menorDistancia = hit.distance;
                impactoValido = hit;
            }
        }

        Vector3 puntoFinal = origen + direccionDisparo * Alcance;
        if (impactoValido.HasValue)
        {
            RaycastHit impacto = impactoValido.Value;
            puntoFinal = impacto.point;
            Debug.Log("Le diste a: " + impacto.collider.name + " en la posicion: " + impacto.point);

            Personaje personajeGolpeado = impacto.collider.GetComponentInParent<Personaje>(); // por si el collider esta en un hijo
            if (personajeGolpeado != null && personajeGolpeado != jugadorQueLaEquipo)
            {
                personajeGolpeado.RecibirDaño(jugadorQueLaEquipo);
            }
        }
        else
        {
            Debug.Log("Fallaste");
        }

        // El trazo visual mantiene el plano Z de la boca del arma para salir totalmente horizontal (+X o -X)
        Vector3 finTrazo = new Vector3(puntoFinal.x, puntoFinal.y, inicioTrazo.z);
        StartCoroutine(MostrarTrazo(inicioTrazo, finTrazo));
        if (jugadorQueLaEquipo != null && jugadorQueLaEquipo.IsSpawned)
            jugadorQueLaEquipo.MostrarDisparoRpc(inicioTrazo, finTrazo);
    }
    public void AdjuntarA(Transform nuevaMano, Personaje jugador)
    {
        mano = nuevaMano;
        transform.parent = mano;
        transform.localPosition = posicionEnMano;
        GetComponent<Collider>().enabled = false;
        jugador.armaEquipada = this;
        jugadorQueLaEquipo = jugador;
        ActualizarRotacionEnMano();
        recargando = false;
        municionActual = municionMaxima; // el arma se recoge con el cargador lleno
        duracionBateriaActual = duracionBateriaMaxima;
        tiempoCargaActual = 0f;
        DetenerRayoContinuo();
    }

    // Se llama al presionar R. Si ya esta recargando o el cargador esta lleno, no hace nada
    public void IniciarRecarga()
    {
        if (recargando) return;
        if (modoDisparo == ModoDisparo.RayoContinuo)
        {
            if (duracionBateriaActual >= duracionBateriaMaxima) return;
        }
        else
        {
            if (municionActual >= municionMaxima) return;
        }
        StartCoroutine(RecargarAhoraSi());
    }

    private IEnumerator RecargarAhoraSi()
    {
        recargando = true;
        if (disparandoRayo || cargandoRayo) DetenerRayoContinuo();
        Debug.Log("Recargando...");
        yield return new WaitForSeconds(tiempoRecarga);
        municionActual = municionMaxima;
        duracionBateriaActual = duracionBateriaMaxima;
        tiempoCargaActual = 0f;
        recargando = false;
    }

    private void RomperCaja()
    {
        Personaje portador = jugadorQueLaEquipo;
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
                red.SoltarArmaEnPosicionServerRpc(gameObject.name, posDrop, false);
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
    // Lo llama GestorArmas en todos los clientes cuando el jugador muere o suelta el arma
    public void Soltar(Vector3 posicion)
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
        municionActual = municionMaxima; // queda lista para el siguiente que la recoja
        duracionBateriaActual = duracionBateriaMaxima;
        tiempoCargaActual = 0f;
        DetenerRayoContinuo();

        transform.parent = null;
        transform.rotation = rotacionBaseIzquierda;
        transform.position = posicion;

        // Compensamos el desfase del pivote del FBX para que el centro visual de la caja/arma quede exactamente en posicion
        Vector3 centroActual = ObtenerCentroVisual();
        transform.position += (posicion - centroActual);

        if (enCaja) ActualizarTransformCaja();

        Collider col = GetComponent<Collider>();
        if (col != null) col.enabled = true;
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
        Personaje portador = jugadorQueLaEquipo;
        float dirX = portador.DireccionHorizontal;
        Vector3 posicionDrop = portador.transform.position + new Vector3(dirX * 0.45f, 0.25f, 0f);
        bool seguiaEnCaja = enCaja;

        if (portador.armaEquipada == this)
            portador.armaEquipada = null;
        jugadorQueLaEquipo = null;

        Red red = portador.GetComponent<Red>();
        if (red != null && red.IsSpawned && NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
        {
            red.SoltarArmaEnPosicionServerRpc(gameObject.name, posicionDrop, seguiaEnCaja);
        }
        else
        {
            Soltar(posicionDrop);
        }
    }
}