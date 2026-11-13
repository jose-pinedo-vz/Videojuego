using UnityEngine;
using UnityEngine.InputSystem;
using Unity.Netcode;
using System.Collections;
using System.Collections.Generic;

// Gestiona el spawn aleatorio de armas y cajas de munición en el mapa.
// Máximo maxArmasActivas armas visibles a la vez, elegidas aleatoriamente al iniciar partida.
public class GestorArmas : NetworkBehaviour
{
    private const int MAX_CAJAS_MUNICION_MAPA = 3;
    private const int COPIAS_POR_ARMA = 2;

    [Header("Configuración")]
    [Tooltip("Cuántas cajas de armas pueden aparecer inicialmente al mismo tiempo")]
    [SerializeField] private int maxArmasActivas = 6;
    [Tooltip("Segundos que tarda un arma tirada en el suelo en despawnear si nadie la recoge")]
    [SerializeField] private float tiempoDespawnArmaSuelo = 15f;
    [Tooltip("Segundos que tarda en volver a aparecer una caja de armas en un spawn ya abierto")]
    [SerializeField] private float tiempoReaparicionCajaArma = 18f;
    [Tooltip("Cuántas cajas de munición aparecen simultáneamente en el mapa (máximo estricto: 3)")]
    [SerializeField] private int limiteCajasMunicionMapa = 3;
    [Tooltip("Segundos que tarda una caja de munición en reaparecer en otra ubicación")]
    [SerializeField] private float segundosRespawnMunicion = 18f;

    [Header("Armas disponibles — arrastrar todas las que haya en la escena")]
    [SerializeField] private Arma[] todasLasArmas;

    // spawn que le tocó a cada arma en esta partida y seguimiento de spawns de cajas abiertas
    private Dictionary<string, Vector3> spawnAsignado = new Dictionary<string, Vector3>();
    private readonly List<Vector3> spawnsArmasRonda = new List<Vector3>();
    private readonly Dictionary<string, int> spawnOrigenDeArma = new Dictionary<string, int>();
    private readonly Dictionary<int, string> armaEnSpawn = new Dictionary<int, string>();
    private GameObject mapaActualRonda;
    private bool armasConfiguradas = false;

    // Cajas de municion activas y sus puntos posibles en el mapa actual
    private readonly List<CajaMunicion> cajasMunicion = new List<CajaMunicion>();
    private readonly List<Vector3> puntosMunicionMapa = new List<Vector3>();

    public static GestorArmas Instancia { get; private set; }
    public float TiempoDespawnArmaSuelo => tiempoDespawnArmaSuelo;

    private struct ConfigModeloArma
    {
        public string nombre;
        public string rutaFbx;
        public Vector3 escala;
        public Vector3 rotacionEuler;
        public float cadencia;
        public float alcance;
        public float dispersion;
        public int municionMax;
        public float tiempoRecarga;
        public ModoDisparo modo;
        public float ralentizacion;
        public int perdigones;
        public float retrocesoH;
        public float retrocesoV;
        public bool destruyeSuperficies;
        public System.Type tipoScript;
    }

    private static readonly ConfigModeloArma[] CONFIG_MODELOS = new ConfigModeloArma[]
    {
        // Armas originales (AK-47 balanceada: menos dispersion, menor cadencia y retroceso fisico)
        new ConfigModeloArma { nombre = "ak47",        rutaFbx = "Assets/Modelos/ak/ak47.fbx",                 escala = new Vector3(0.20f, 0.20f, 0.20f),    rotacionEuler = Vector3.zero,                cadencia = 6.8f,  alcance = 7.5f,  dispersion = 1.2f, municionMax = 18, tiempoRecarga = 1.8f, modo = ModoDisparo.Automatico,     ralentizacion = 0f,    perdigones = 1, retrocesoH = 3.2f,  retrocesoV = 0.0f, tipoScript = typeof(Arma) },
        new ConfigModeloArma { nombre = "barret",      rutaFbx = "Assets/Modelos/barret/barret.fbx",           escala = new Vector3(0.19f, 0.19f, 0.19f),    rotacionEuler = new Vector3(0f, -90f, 90f),  cadencia = 1.5f,  alcance = 15f,   dispersion = 0.1f, municionMax = 5,  tiempoRecarga = 2.5f, modo = ModoDisparo.Semiautomatico, ralentizacion = 0f,    perdigones = 1, retrocesoH = 9.5f,  retrocesoV = 0.8f, tipoScript = typeof(Arma) },
        new ConfigModeloArma { nombre = "mac10",       rutaFbx = "Assets/Modelos/mac10/mac10.fbx",             escala = new Vector3(0.115f, 0.115f, 0.115f), rotacionEuler = new Vector3(0f, -90f, 90f),  cadencia = 16f,   alcance = 4.0f,  dispersion = 0.5f, municionMax = 30, tiempoRecarga = 1.3f, modo = ModoDisparo.Automatico,     ralentizacion = 0f,    perdigones = 1, retrocesoH = 0.0f,  retrocesoV = 0.0f, tipoScript = typeof(Arma) },
        new ConfigModeloArma { nombre = "bereta",      rutaFbx = "Assets/Modelos/bereta/bereta.fbx",           escala = new Vector3(0.125f, 0.125f, 0.125f), rotacionEuler = new Vector3(0f, 180f, 0f),   cadencia = 5f,    alcance = 5.5f,  dispersion = 0.8f, municionMax = 12, tiempoRecarga = 1.2f, modo = ModoDisparo.Semiautomatico, ralentizacion = 0f,    perdigones = 1, retrocesoH = 0.0f,  retrocesoV = 0.0f, tipoScript = typeof(Arma) },
        new ConfigModeloArma { nombre = "raygun",      rutaFbx = "Assets/Modelos/raygun/raygun.fbx",           escala = new Vector3(0.046f, 0.046f, 0.046f), rotacionEuler = new Vector3(90f, 180f, 0f),  cadencia = 20f,   alcance = 80f,   dispersion = 0.0f, municionMax = 5,  tiempoRecarga = 2.0f, modo = ModoDisparo.RayoContinuo,   ralentizacion = 0f,    perdigones = 1, retrocesoH = 0.0f,  retrocesoV = 0.0f, tipoScript = typeof(Arma) },

        // 6 Armas nuevas (Antitanques con empuje fisico exagerado y perforacion de coberturas)
        new ConfigModeloArma { nombre = "machete",     rutaFbx = "Assets/Modelos/machete/machete.fbx",         escala = Vector3.one,                         rotacionEuler = Vector3.zero,                cadencia = 3.6f,  alcance = 2.45f, dispersion = 0.0f, municionMax = 0,  tiempoRecarga = 0.0f, modo = ModoDisparo.Semiautomatico, ralentizacion = 0f,    perdigones = 1, retrocesoH = 0.0f,  retrocesoV = 0.0f, tipoScript = typeof(ArmaMachete) },
        new ConfigModeloArma { nombre = "antitanques", rutaFbx = "Assets/Modelos/Antitanques/antitanques.fbx", escala = new Vector3(0.19f, 0.19f, 0.19f),    rotacionEuler = Vector3.zero,                  cadencia = 0.7f,  alcance = 20f,   dispersion = 0.0f, municionMax = 3,  tiempoRecarga = 3.0f, modo = ModoDisparo.Semiautomatico, ralentizacion = 0.38f, perdigones = 1, retrocesoH = 13.0f, retrocesoV = 2.4f, destruyeSuperficies = true, tipoScript = typeof(Arma) },
        new ConfigModeloArma { nombre = "barreno",     rutaFbx = "Assets/Modelos/barreno/barreno.fbx",         escala = new Vector3(0.08f, 0.08f, 0.08f),    rotacionEuler = Vector3.zero,                cadencia = 1.4f,  alcance = 9.0f,  dispersion = 0.0f, municionMax = 3,  tiempoRecarga = 1.6f, modo = ModoDisparo.Semiautomatico, ralentizacion = 0f,    perdigones = 1, retrocesoH = 0.0f,  retrocesoV = 0.0f, tipoScript = typeof(ArmaBarreno) },
        new ConfigModeloArma { nombre = "escudo",      rutaFbx = "Assets/Modelos/escudo/escudo.fbx",           escala = new Vector3(0.15f, 0.15f, 0.15f),    rotacionEuler = Vector3.zero,                cadencia = 2.0f,  alcance = 2.25f, dispersion = 0.0f, municionMax = 0,  tiempoRecarga = 0.0f, modo = ModoDisparo.Semiautomatico, ralentizacion = 0f,    perdigones = 1, retrocesoH = 0.0f,  retrocesoV = 0.0f, tipoScript = typeof(ArmaEscudo) },
        new ConfigModeloArma { nombre = "platanos",    rutaFbx = "Assets/Modelos/platanos/penca.fbx",          escala = Vector3.one,                         rotacionEuler = Vector3.zero,                cadencia = 2.2f,  alcance = 6.5f,  dispersion = 0.0f, municionMax = 5,  tiempoRecarga = 1.2f, modo = ModoDisparo.Semiautomatico, ralentizacion = 0f,    perdigones = 1, retrocesoH = 0.0f,  retrocesoV = 0.0f, tipoScript = typeof(ArmaPlatanos) },
        new ConfigModeloArma { nombre = "escopeta",    rutaFbx = "Assets/Modelos/escopeta/escopeta.fbx",       escala = new Vector3(0.17f, 0.17f, 0.17f),    rotacionEuler = Vector3.zero,                cadencia = 1.15f, alcance = 6.2f,  dispersion = 9.0f, municionMax = 5,  tiempoRecarga = 2.2f, modo = ModoDisparo.Semiautomatico, ralentizacion = 0f,    perdigones = 6, retrocesoH = 0.0f,  retrocesoV = 0.0f, tipoScript = typeof(Arma) },
    };

    private static readonly Vector3[] SPAWNS_COCHERA_POSTER = new Vector3[]
    {
        new Vector3(0.071f,   0.30077758f, -0.013f),
        new Vector3(-0.295f,  0.322f,      -0.0924f),
        new Vector3(-0.3712f, -0.158f,     -0.0846f),
        new Vector3(0.364f,   -0.152f,     -0.0528f),
        new Vector3(-0.2785f, -0.34f,      -0.048f),
        new Vector3(0.38f,    0.1063f,     -0.057f),
    };

    private static readonly Vector3[] SPAWNS_COSINA_LOCAL = new Vector3[]
    {
        new Vector3(-0.39f,      5.97f,  2.92f),
        new Vector3(0.45000076f, 3.04f,  3.26f),
        new Vector3(10.35f,      8.51f,  2.9f),
        new Vector3(3.5f,        1.291f, 2.969f),
        new Vector3(10.57f,      4.38f,  3.058f),
    };

    private void Awake()
    {
        Instancia = this;
        AsegurarListaArmas();
    }

    private void Start()
    {
        AsegurarListaArmas();

        // En escenas de prueba sin red activa (ej. Prueba animacion), repartir cajas en los spawnArma automáticamente
        if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsListening)
        {
            List<Transform> spawnsEnEscena = ObtenerOCrearSpawnsEnEscena();
            if (spawnsEnEscena.Count > 0)
            {
                IniciarRondaArmasLocal(spawnsEnEscena);
            }
        }
    }

    private void Update()
    {
        // Atajo de prueba: presionar T vuelve a barajar y spawnear las cajas de armas en puntos aleatorios
        if (Keyboard.current != null && Keyboard.current.tKey.wasPressedThisFrame)
        {
            if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsListening)
            {
                List<Transform> spawns = ObtenerOCrearSpawnsEnEscena();
                IniciarRondaArmasLocal(spawns);
                Debug.Log("GestorArmas: Cajas de armas barajadas y re-spawneadas (Tecla T)");
            }
            else if (EsServidor())
            {
                List<Transform> spawns = ObtenerOCrearSpawnsEnEscena();
                IniciarRondaArmas(spawns);
                Debug.Log("GestorArmas: Cajas de armas barajadas y re-spawneadas en servidor (Tecla T)");
            }
        }
    }

    private bool EsServidor()
    {
        return NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening && NetworkManager.Singleton.IsServer;
    }

    private void AsegurarListaArmas()
    {
        if (!armasConfiguradas)
        {
            armasConfiguradas = true;
            ConfigurarArmasEnEscena();
        }

        List<Arma> encontradas = new List<Arma>();
        foreach (Arma a in FindObjectsByType<Arma>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (a != null && !encontradas.Contains(a))
                encontradas.Add(a);
        }

        if (todasLasArmas == null || todasLasArmas.Length < encontradas.Count)
        {
            todasLasArmas = encontradas.ToArray();
        }
    }

    private void ConfigurarArmasEnEscena()
    {
        for (int i = 0; i < CONFIG_MODELOS.Length; i++)
        {
            ConfigModeloArma cfg = CONFIG_MODELOS[i];
            GameObject objBase = BuscarObjetoArmaEnEscena(cfg.nombre);

            for (int copia = 0; copia < COPIAS_POR_ARMA; copia++)
            {
                string nombreInstancia = copia == 0 ? cfg.nombre : $"{cfg.nombre}_{copia + 1}";
                GameObject objArma = copia == 0 ? objBase : BuscarObjetoArmaEnEscena(nombreInstancia);

#if UNITY_EDITOR
                if (objArma == null)
                {
                    GameObject prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(cfg.rutaFbx);
                    if (prefab != null)
                    {
                        objArma = Instantiate(prefab);
                        objArma.name = nombreInstancia;
                        objArma.transform.localScale = cfg.escala;
                        objArma.transform.rotation = Quaternion.Euler(cfg.rotacionEuler);
                    }
                }
#endif
                if (objArma == null && copia > 0 && objBase != null)
                {
                    objArma = Instantiate(objBase);
                    objArma.name = nombreInstancia;
                    Transform cajaDuplicada = objArma.transform.Find("CajaMaderaVisual");
                    if (cajaDuplicada != null) DestroyImmediate(cajaDuplicada.gameObject);
                }

                if (objArma == null)
                {
                    objArma = new GameObject(nombreInstancia);
                    objArma.transform.localScale = cfg.escala;
                    objArma.transform.rotation = Quaternion.Euler(cfg.rotacionEuler);
                }

                if (objArma != null)
                {
                    objArma.name = nombreInstancia;
                    if (copia == 0 && objBase == null) objBase = objArma;

                    if (objArma.transform.parent == null)
                    {
                        objArma.transform.localScale = cfg.escala;
                        objArma.transform.rotation = Quaternion.Euler(cfg.rotacionEuler);
                    }

                    BoxCollider box = objArma.GetComponent<BoxCollider>();
                    if (box == null) box = objArma.AddComponent<BoxCollider>();
                    box.isTrigger = true;

                    LineRenderer lr = objArma.GetComponent<LineRenderer>();
                    if (lr == null) lr = objArma.AddComponent<LineRenderer>();
                    lr.enabled = false;
                    lr.startWidth = 0.05f;
                    lr.endWidth = 0.05f;
                    lr.useWorldSpace = true;
#if UNITY_EDITOR
                    Material matTrazo = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>("Assets/trazo.mat");
                    if (matTrazo != null) lr.sharedMaterial = matTrazo;
#endif

                    System.Type tipoRequerido = cfg.tipoScript ?? typeof(Arma);
                    Arma scriptArma = objArma.GetComponent<Arma>();
                    if (scriptArma != null && scriptArma.GetType() != tipoRequerido)
                    {
                        DestroyImmediate(scriptArma);
                        scriptArma = null;
                    }
                    if (scriptArma == null)
                    {
                        scriptArma = (Arma)objArma.AddComponent(tipoRequerido);
                    }

                    scriptArma.ConfigurarDatosArma(
                        cfg.cadencia,
                        cfg.alcance,
                        cfg.municionMax,
                        cfg.tiempoRecarga,
                        cfg.modo,
                        cfg.dispersion,
                        cfg.ralentizacion,
                        cfg.perdigones,
                        cfg.retrocesoH,
                        cfg.retrocesoV,
                        cfg.destruyeSuperficies
                    );
                    scriptArma.RecalibrarModeloVisual();
                }
            }
        }
    }

    private GameObject BuscarObjetoArmaEnEscena(string nombreModelo)
    {
        foreach (Arma a in FindObjectsByType<Arma>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (a != null && a.gameObject.name.Equals(nombreModelo, System.StringComparison.OrdinalIgnoreCase))
                return a.gameObject;
        }

        foreach (Transform t in FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (t != null && t.parent == null && t.name.Equals(nombreModelo, System.StringComparison.OrdinalIgnoreCase))
                return t.gameObject;
        }

        return null;
    }

    public List<Transform> ObtenerOCrearSpawnsEnMapa(GameObject mapa)
    {
        if (mapa == null) return ObtenerOCrearSpawnsEnEscena();
        mapaActualRonda = mapa;

        List<Transform> puntos = GameManager.BuscarPuntos(mapa, GameManager.PREFIJO_ARMA);
        if (puntos.Count > 0) return puntos;

        // Si es ecenaCochera o ecenaCosina, recrear tus puntos spawnArma en sus coordenadas exactas
        if (mapa.name.Equals("ecenaCochera", System.StringComparison.OrdinalIgnoreCase))
        {
            Transform posterFondo = null;
            foreach (Transform t in mapa.GetComponentsInChildren<Transform>(true))
            {
                if (t.name == "Poster_fondo")
                {
                    posterFondo = t;
                    break;
                }
            }

            Transform padre = posterFondo != null ? posterFondo : mapa.transform;
            for (int i = 0; i < SPAWNS_COCHERA_POSTER.Length; i++)
            {
                GameObject punto = new GameObject(GameManager.PREFIJO_ARMA + (i + 1));
                punto.transform.SetParent(padre, false);
                if (posterFondo != null)
                {
                    punto.transform.localPosition = SPAWNS_COCHERA_POSTER[i];
                }
                else
                {
                    Vector3 p = SPAWNS_COCHERA_POSTER[i];
                    punto.transform.localPosition = new Vector3(-p.x * 19.527935f, 4.829999f + p.y * 9.974148f, -3.87f - p.z * 11.585816f);
                }
                puntos.Add(punto.transform);
            }
            return puntos;
        }

        if (mapa.name.Equals("ecenaCosina", System.StringComparison.OrdinalIgnoreCase))
        {
            for (int i = 0; i < SPAWNS_COSINA_LOCAL.Length; i++)
            {
                GameObject punto = new GameObject(GameManager.PREFIJO_ARMA + (i + 1));
                punto.transform.SetParent(mapa.transform, false);
                punto.transform.localPosition = SPAWNS_COSINA_LOCAL[i];
                puntos.Add(punto.transform);
            }
            return puntos;
        }

        List<Transform> spawnsJugador = GameManager.BuscarPuntos(mapa, GameManager.PREFIJO_JUGADOR);
        if (spawnsJugador.Count > 0)
        {
            int cantidad = Mathf.Max(maxArmasActivas, 5);
            for (int i = 0; i < cantidad; i++)
            {
                Transform refSpawn = spawnsJugador[i % spawnsJugador.Count];
                GameObject punto = new GameObject(GameManager.PREFIJO_ARMA + (i + 1));
                punto.transform.SetParent(mapa.transform);
                punto.transform.position = refSpawn.position + new Vector3(0.6f, 0f, 0f);
                puntos.Add(punto.transform);
            }
            return puntos;
        }
        Personaje jugadorRef = FindFirstObjectByType<Personaje>(FindObjectsInactive.Include);

        float zPlano = jugadorRef != null ? jugadorRef.transform.position.z
            : (spawnsJugador.Count > 0 ? spawnsJugador[0].position.z : 6.16f);
        float yPiso = spawnsJugador.Count > 0 ? spawnsJugador[0].position.y
            : (jugadorRef != null ? jugadorRef.transform.position.y - 1.5f : 3.25f);

        float minX = mapa.transform.position.x + 1.5f;
        float maxX = mapa.transform.position.x + 11.5f;
        if (spawnsJugador.Count >= 2)
        {
            minX = float.MaxValue;
            maxX = float.MinValue;
            foreach (Transform sp in spawnsJugador)
            {
                if (sp.position.x < minX) minX = sp.position.x;
                if (sp.position.x > maxX) maxX = sp.position.x;
            }
            if (Mathf.Abs(maxX - minX) < 6f)
            {
                float centroX = (minX + maxX) * 0.5f;
                minX = centroX - 5f;
                maxX = centroX + 5f;
            }
        }
        else if (jugadorRef != null)
        {
            minX = jugadorRef.transform.position.x - 5f;
            maxX = jugadorRef.transform.position.x + 5f;
        }

        int total = Mathf.Max(maxArmasActivas, 5);
        for (int i = 0; i < total; i++)
        {
            float t = total > 1 ? (float)i / (total - 1) : 0.5f;
            float posX = Mathf.Lerp(minX, maxX, t);
            GameObject punto = new GameObject(GameManager.PREFIJO_ARMA + (i + 1));
            punto.transform.SetParent(mapa.transform);
            punto.transform.position = new Vector3(posX, yPiso, zPlano);
            puntos.Add(punto.transform);
        }

        return puntos;
    }

    private List<Transform> ObtenerOCrearSpawnsEnEscena()
    {
        List<Transform> puntos = new List<Transform>();
        foreach (Transform t in FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (t.name.StartsWith(GameManager.PREFIJO_ARMA, System.StringComparison.OrdinalIgnoreCase) && t.gameObject.activeInHierarchy)
            {
                puntos.Add(t);
            }
        }

        if (puntos.Count > 0) return puntos;

        GameObject mapa = GameObject.Find("Mapa3D");
        if (mapa == null)
        {
            foreach (GameObject go in FindObjectsByType<GameObject>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (go.activeInHierarchy && go.transform.parent == null && go.name.StartsWith("ecena", System.StringComparison.OrdinalIgnoreCase))
                {
                    mapa = go;
                    break;
                }
            }
        }

        if (mapa != null)
            return ObtenerOCrearSpawnsEnMapa(mapa);

        Personaje jugadorRef = FindFirstObjectByType<Personaje>(FindObjectsInactive.Include);
        float zPlano = jugadorRef != null ? jugadorRef.transform.position.z : 6.16f;
        float yPiso = jugadorRef != null ? jugadorRef.transform.position.y - 1.5f : 3.25f;
        float centroX = jugadorRef != null ? jugadorRef.transform.position.x : 6.5f;

        GameObject contenedor = new GameObject("SpawnsArmas_Auto");
        int total = Mathf.Max(maxArmasActivas, 5);
        for (int i = 0; i < total; i++)
        {
            float posX = (centroX - 5f) + i * 2.5f;
            GameObject punto = new GameObject(GameManager.PREFIJO_ARMA + (i + 1));
            punto.transform.SetParent(contenedor.transform);
            punto.transform.position = new Vector3(posX, yPiso, zPlano);
            puntos.Add(punto.transform);
        }

        return puntos;
    }

    private static string NombreBaseArma(string nombre)
    {
        if (string.IsNullOrEmpty(nombre)) return "";
        int idx = nombre.LastIndexOf('_');
        if (idx > 0 && idx < nombre.Length - 1 && int.TryParse(nombre.Substring(idx + 1), out _))
        {
            return nombre.Substring(0, idx).ToLowerInvariant();
        }
        return nombre.ToLowerInvariant();
    }

    private List<Arma> SeleccionarArmasDistintasParaInicio(int cantidad)
    {
        List<Arma> todasMezcladas = new List<Arma>();
        foreach (Arma a in todasLasArmas)
        {
            if (a != null) todasMezcladas.Add(a);
        }
        Mezclar(todasMezcladas);

        List<Arma> seleccion = new List<Arma>();
        HashSet<string> tiposUsados = new HashSet<string>();

        foreach (Arma a in todasMezcladas)
        {
            if (seleccion.Count >= cantidad) break;
            string tipoBase = NombreBaseArma(a.gameObject.name);
            if (tiposUsados.Add(tipoBase))
            {
                seleccion.Add(a);
            }
        }

        // Respaldo si hubiera mas spawns que tipos distintos
        foreach (Arma a in todasMezcladas)
        {
            if (seleccion.Count >= cantidad) break;
            if (!seleccion.Contains(a))
                seleccion.Add(a);
        }
        return seleccion;
    }

    // Llamado por GameManager.iniciarPartida() en el servidor, con los spawnArma del mapa elegido
    public void IniciarRondaArmas(List<Transform> spawns)
    {
        if (!EsServidor()) return;

        StopAllCoroutines();
        AsegurarListaArmas();

        if (spawns == null || spawns.Count == 0)
        {
            spawns = ObtenerOCrearSpawnsEnEscena();
        }

        // Ocultar todas al iniciar nueva ronda/mapa, en todos los clientes
        foreach (Arma a in todasLasArmas)
            if (a != null) EnviarActualizacionArma(a.gameObject.name, a.transform.position, false, true, a.MunicionMaxima, a.DuracionBateriaMaxima, false);

        spawnAsignado.Clear();
        spawnsArmasRonda.Clear();
        spawnOrigenDeArma.Clear();
        armaEnSpawn.Clear();

        List<Transform> spawnsMezclados = new List<Transform>(spawns);
        Mezclar(spawnsMezclados);
        foreach (Transform sp in spawnsMezclados)
        {
            if (sp != null) spawnsArmasRonda.Add(sp.position);
        }

        int cantidad = Mathf.Min(maxArmasActivas, spawnsArmasRonda.Count, todasLasArmas.Length);
        List<Arma> elegidas = SeleccionarArmasDistintasParaInicio(cantidad);

        for (int i = 0; i < elegidas.Count && i < spawnsArmasRonda.Count; i++)
        {
            Arma arma = elegidas[i];
            if (arma == null) continue;
            string nombre = arma.gameObject.name;
            Vector3 pos = spawnsArmasRonda[i];
            spawnAsignado[nombre] = pos;
            spawnOrigenDeArma[nombre] = i;
            armaEnSpawn[i] = nombre;
            EnviarActualizacionArma(nombre, pos, true, true, arma.MunicionMaxima, arma.DuracionBateriaMaxima, true);
        }

        IniciarCajasMunicion(spawns, true);
    }

    // Versión local para escenas de prueba sin servidor de red
    public void IniciarRondaArmasLocal(List<Transform> spawns)
    {
        StopAllCoroutines();
        AsegurarListaArmas();
        if (spawns == null || spawns.Count == 0) return;

        foreach (Arma a in todasLasArmas)
            if (a != null) AplicarEstadoArmaLocal(a, a.transform.position, false, true, a.MunicionMaxima, a.DuracionBateriaMaxima, false);

        spawnAsignado.Clear();
        spawnsArmasRonda.Clear();
        spawnOrigenDeArma.Clear();
        armaEnSpawn.Clear();

        List<Transform> spawnsMezclados = new List<Transform>(spawns);
        Mezclar(spawnsMezclados);
        foreach (Transform sp in spawnsMezclados)
        {
            if (sp != null) spawnsArmasRonda.Add(sp.position);
        }

        int cantidad = Mathf.Min(maxArmasActivas, spawnsArmasRonda.Count, todasLasArmas.Length);
        List<Arma> elegidas = SeleccionarArmasDistintasParaInicio(cantidad);

        for (int i = 0; i < elegidas.Count && i < spawnsArmasRonda.Count; i++)
        {
            Arma arma = elegidas[i];
            if (arma == null) continue;
            string nombre = arma.gameObject.name;
            Vector3 pos = spawnsArmasRonda[i];
            spawnAsignado[nombre] = pos;
            spawnOrigenDeArma[nombre] = i;
            armaEnSpawn[i] = nombre;
            AplicarEstadoArmaLocal(arma, pos, true, true, arma.MunicionMaxima, arma.DuracionBateriaMaxima, true);
        }

        IniciarCajasMunicion(spawns, false);
    }

    // Llamado cuando el jugador recoge o abre una caja de armas en un spawn
    public void NotificarCajaArmaTomada(Arma arma)
    {
        if (arma == null) return;

        if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsListening || EsServidor())
        {
            ProcesarCajaArmaTomadaServidor(arma.gameObject.name);
            return;
        }

        GameManager gm = FindFirstObjectByType<GameManager>();
        if (gm != null && gm.IsSpawned)
        {
            gm.NotificarCajaArmaTomadaServerRpc(arma.gameObject.name);
        }
    }

    public void ProcesarCajaArmaTomadaServidor(string nombreArma)
    {
        if (string.IsNullOrEmpty(nombreArma)) return;
        if (!spawnOrigenDeArma.TryGetValue(nombreArma, out int idxSpawn)) return;

        spawnOrigenDeArma.Remove(nombreArma);
        if (armaEnSpawn.TryGetValue(idxSpawn, out string nombreActual) && nombreActual == nombreArma)
        {
            armaEnSpawn.Remove(idxSpawn);
        }

        StartCoroutine(ReaparecerCajaArmaCoroutine(idxSpawn));
    }

    private IEnumerator ReaparecerCajaArmaCoroutine(int idxSpawn)
    {
        yield return new WaitForSeconds(tiempoReaparicionCajaArma);

        if (idxSpawn < 0 || idxSpawn >= spawnsArmasRonda.Count) yield break;
        if (armaEnSpawn.ContainsKey(idxSpawn)) yield break;

        AsegurarListaArmas();

        // Tipos ya visibles en cajas cerradas para priorizar variedad aleatoria
        HashSet<string> tiposEnCajas = new HashSet<string>();
        foreach (string nombreCaja in armaEnSpawn.Values)
        {
            tiposEnCajas.Add(NombreBaseArma(nombreCaja));
        }

        List<Arma> candidatasPreferidas = new List<Arma>();
        List<Arma> candidatasLibres = new List<Arma>();
        foreach (Arma a in todasLasArmas)
        {
            if (a == null) continue;
            if (a.gameObject.activeInHierarchy || a.transform.parent != null) continue;
            candidatasLibres.Add(a);
            if (!tiposEnCajas.Contains(NombreBaseArma(a.gameObject.name)))
                candidatasPreferidas.Add(a);
        }

        List<Arma> pool = candidatasPreferidas.Count > 0 ? candidatasPreferidas : candidatasLibres;
        if (pool.Count == 0) yield break;

        Arma elegida = pool[Random.Range(0, pool.Count)];
        string nombreElegida = elegida.gameObject.name;
        Vector3 posSpawn = spawnsArmasRonda[idxSpawn];

        spawnAsignado[nombreElegida] = posSpawn;
        spawnOrigenDeArma[nombreElegida] = idxSpawn;
        armaEnSpawn[idxSpawn] = nombreElegida;

        EnviarActualizacionArma(nombreElegida, posSpawn, true, true, elegida.MunicionMaxima, elegida.DuracionBateriaMaxima, true);
    }

    // Despawnea armas sueltas en el suelo tras tiempoDespawnArmaSuelo sin que nadie las recoja
    public void DespawnearArmaPorTiempo(Arma arma)
    {
        if (arma == null || !arma.gameObject.activeInHierarchy || arma.transform.parent != null) return;
        if (spawnOrigenDeArma.ContainsKey(arma.gameObject.name)) return; // caja cerrada esperando en spawn

        bool enRed = NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening;
        if (enRed && !EsServidor()) return;

        EnviarActualizacionArma(arma.gameObject.name, arma.transform.position, false, false, 0, 0f, false);
    }

    // =========================================================================
    // SISTEMA DE CAJAS DE MUNICIÓN (MÁXIMO 3 SIMULTÁNEAS Y REAPARICIÓN ESPACIADA)
    // =========================================================================

    private void AsegurarCajasMunicionInstanciadas()
    {
        int limite = Mathf.Clamp(limiteCajasMunicionMapa, 1, MAX_CAJAS_MUNICION_MAPA);
        cajasMunicion.RemoveAll(c => c == null);

        // Reutilizar las existentes en escena (incluso desactivadas) y destruir excedentes si hubiera mas de 3
        CajaMunicion[] existentes = FindObjectsByType<CajaMunicion>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        System.Array.Sort(existentes, (a, b) => string.CompareOrdinal(a.name, b.name));
        foreach (CajaMunicion cm in existentes)
        {
            if (cm == null) continue;
            if (!cajasMunicion.Contains(cm))
            {
                if (cajasMunicion.Count < limite)
                {
                    cajasMunicion.Add(cm);
                }
                else
                {
                    Destroy(cm.gameObject);
                }
            }
        }

        while (cajasMunicion.Count < limite)
        {
            int idx = cajasMunicion.Count;
            string nombreCaja = $"CajaMunicion_{idx}";
            GameObject existente = null;
#if UNITY_EDITOR
            GameObject prefabFbx = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Modelos/caja municion/caja municion.fbx");
            if (prefabFbx != null)
            {
                existente = Instantiate(prefabFbx);
                existente.name = nombreCaja;
            }
#endif
            if (existente == null)
            {
                existente = new GameObject(nombreCaja);
            }

            CajaMunicion comp = existente.GetComponent<CajaMunicion>();
            if (comp == null) comp = existente.AddComponent<CajaMunicion>();
            comp.Inicializar(idx, -1, Vector3.zero);
            comp.EstablecerDisponible(false);
            cajasMunicion.Add(comp);
        }
    }

    private void ConstruirPuntosMunicion(List<Transform> spawnsArmas)
    {
        puntosMunicionMapa.Clear();
        List<Vector3> crudos = new List<Vector3>();

        if (spawnsArmas != null)
        {
            foreach (Transform t in spawnsArmas)
            {
                if (t != null) crudos.Add(t.position + new Vector3(0.95f, 0.25f, 0f));
            }
        }

        if (mapaActualRonda != null)
        {
            foreach (Transform sp in GameManager.BuscarPuntos(mapaActualRonda, GameManager.PREFIJO_JUGADOR))
            {
                if (sp != null) crudos.Add(sp.position + new Vector3(0f, 0.25f, 0f));
            }
        }

        // Añadir puntos intermedios y laterales para cubrir todo lo ancho del mapa
        if (spawnsArmas != null && spawnsArmas.Count >= 2)
        {
            float planoZ = spawnsArmas[0] != null ? spawnsArmas[0].position.z : 0f;
            for (int i = 0; i < spawnsArmas.Count; i++)
            {
                if (spawnsArmas[i] == null) continue;
                Transform sig = spawnsArmas[(i + 1) % spawnsArmas.Count];
                if (sig != null)
                {
                    Vector3 medio = Vector3.Lerp(spawnsArmas[i].position, sig.position, 0.5f);
                    medio.z = planoZ;
                    medio.y = Mathf.Max(spawnsArmas[i].position.y, sig.position.y) + 0.35f;
                    crudos.Add(medio);
                }
            }
        }

        if (crudos.Count == 0) return;

        // Mantener separacion entre ubicaciones posibles para obligar a recorrer el mapa
        float[] distanciasFiltro = { 3.0f, 2.0f, 1.0f };
        foreach (float distMin in distanciasFiltro)
        {
            puntosMunicionMapa.Clear();
            float distMinSq = distMin * distMin;
            foreach (Vector3 cand in crudos)
            {
                bool muyCerca = false;
                foreach (Vector3 aceptado in puntosMunicionMapa)
                {
                    if ((cand - aceptado).sqrMagnitude < distMinSq)
                    {
                        muyCerca = true;
                        break;
                    }
                }
                if (!muyCerca)
                {
                    puntosMunicionMapa.Add(cand);
                }
            }

            if (puntosMunicionMapa.Count >= Mathf.Min(crudos.Count, MAX_CAJAS_MUNICION_MAPA + 2))
                break;
        }
    }

    private int ElegirIndiceSpawnMunicionDistante(int evitarIndice)
    {
        List<int> libres = new List<int>();
        List<Vector3> posicionesActivas = new List<Vector3>();

        foreach (CajaMunicion otra in cajasMunicion)
        {
            if (otra != null && otra.EstaDisponible)
                posicionesActivas.Add(otra.transform.position);
        }

        for (int i = 0; i < puntosMunicionMapa.Count; i++)
        {
            if (i == evitarIndice && puntosMunicionMapa.Count > 1) continue;

            bool ocupado = false;
            foreach (CajaMunicion otra in cajasMunicion)
            {
                if (otra != null && otra.EstaDisponible && otra.IndicePuntoSpawnActual == i)
                {
                    ocupado = true;
                    break;
                }
            }
            if (!ocupado) libres.Add(i);
        }

        if (libres.Count == 0)
        {
            for (int i = 0; i < puntosMunicionMapa.Count; i++)
            {
                if (i != evitarIndice || puntosMunicionMapa.Count == 1)
                    libres.Add(i);
            }
        }

        if (posicionesActivas.Count > 0 && libres.Count > 1)
        {
            // Filtrar puntos alejados (>= 3.5m) de las otras cajas activas para no juntarlas en una misma zona
            List<int> bienSeparados = new List<int>();
            foreach (int idx in libres)
            {
                float minDist = float.MaxValue;
                foreach (Vector3 posAct in posicionesActivas)
                {
                    float d = Vector3.Distance(puntosMunicionMapa[idx], posAct);
                    if (d < minDist) minDist = d;
                }
                if (minDist >= 3.5f) bienSeparados.Add(idx);
            }

            if (bienSeparados.Count > 0)
                return bienSeparados[Random.Range(0, bienSeparados.Count)];

            // Si el mapa es pequeno, escoger uno de los 2 puntos mas lejanos
            libres.Sort((a, b) =>
            {
                float dA = DistanciaMinimaA(puntosMunicionMapa[a], posicionesActivas);
                float dB = DistanciaMinimaA(puntosMunicionMapa[b], posicionesActivas);
                return dB.CompareTo(dA);
            });
            int top = Mathf.Min(2, libres.Count);
            return libres[Random.Range(0, top)];
        }

        return libres[Random.Range(0, libres.Count)];
    }

    private static float DistanciaMinimaA(Vector3 punto, List<Vector3> otros)
    {
        float mejor = float.MaxValue;
        foreach (Vector3 o in otros)
        {
            float d = Vector3.Distance(punto, o);
            if (d < mejor) mejor = d;
        }
        return mejor;
    }

    private void IniciarCajasMunicion(List<Transform> spawnsArmas, bool enRed)
    {
        AsegurarCajasMunicionInstanciadas();
        ConstruirPuntosMunicion(spawnsArmas);

        if (puntosMunicionMapa.Count == 0) return;

        for (int i = 0; i < cajasMunicion.Count; i++)
        {
            cajasMunicion[i].EstablecerDisponible(false);
        }

        int totalCajas = Mathf.Min(cajasMunicion.Count, MAX_CAJAS_MUNICION_MAPA);
        for (int i = 0; i < totalCajas; i++)
        {
            int idxSpawn = ElegirIndiceSpawnMunicionDistante(-1);
            Vector3 pos = puntosMunicionMapa[idxSpawn];
            if (enRed)
            {
                EnviarActualizacionCajaMunicion(i, pos, true, idxSpawn);
            }
            else
            {
                AplicarEstadoCajaMunicionRemoto(i, pos, true, idxSpawn);
            }
        }
    }

    public void NotificarCajaMunicionConsumida(CajaMunicion caja, Personaje jugador)
    {
        if (caja == null) return;

        if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsListening || EsServidor())
        {
            ProcesarConsumoCajaMunicionServidor(caja.IndiceCaja);
            return;
        }

        GameManager gm = FindFirstObjectByType<GameManager>();
        if (gm != null && gm.IsSpawned)
        {
            gm.SolicitarConsumoCajaMunicionServerRpc(caja.IndiceCaja);
        }
    }

    public void ProcesarConsumoCajaMunicionServidor(int indiceCaja)
    {
        AsegurarCajasMunicionInstanciadas();
        if (indiceCaja < 0 || indiceCaja >= cajasMunicion.Count) return;

        CajaMunicion caja = cajasMunicion[indiceCaja];
        int spawnAnterior = caja.IndicePuntoSpawnActual;

        EnviarActualizacionCajaMunicion(indiceCaja, caja.transform.position, false, spawnAnterior);
        StartCoroutine(ReaparecerCajaMunicionCoroutine(indiceCaja, spawnAnterior));
    }

    private IEnumerator ReaparecerCajaMunicionCoroutine(int indiceCaja, int spawnAnterior)
    {
        yield return new WaitForSeconds(segundosRespawnMunicion);

        AsegurarCajasMunicionInstanciadas();
        if (indiceCaja < 0 || indiceCaja >= cajasMunicion.Count) yield break;

        if (puntosMunicionMapa.Count == 0)
        {
            ConstruirPuntosMunicion(ObtenerOCrearSpawnsEnEscena());
        }
        if (puntosMunicionMapa.Count == 0) yield break;

        int nuevoIndice = ElegirIndiceSpawnMunicionDistante(spawnAnterior);
        Vector3 nuevaPos = puntosMunicionMapa[nuevoIndice];
        EnviarActualizacionCajaMunicion(indiceCaja, nuevaPos, true, nuevoIndice);
    }

    private void EnviarActualizacionCajaMunicion(int indiceCaja, Vector3 posicion, bool disponible, int indiceSpawn)
    {
        if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsListening)
        {
            AplicarEstadoCajaMunicionRemoto(indiceCaja, posicion, disponible, indiceSpawn);
            return;
        }

        if (IsSpawned)
        {
            ActualizarCajaMunicionRpc(indiceCaja, posicion, disponible, indiceSpawn);
            return;
        }

        GameManager gm = FindFirstObjectByType<GameManager>();
        if (gm != null && gm.IsSpawned)
        {
            gm.SincronizarCajaMunicionClienteRpc(indiceCaja, posicion, disponible, indiceSpawn);
        }
        else
        {
            AplicarEstadoCajaMunicionRemoto(indiceCaja, posicion, disponible, indiceSpawn);
        }
    }

    [Rpc(SendTo.ClientsAndHost)]
    private void ActualizarCajaMunicionRpc(int indiceCaja, Vector3 posicion, bool disponible, int indiceSpawn)
    {
        AplicarEstadoCajaMunicionRemoto(indiceCaja, posicion, disponible, indiceSpawn);
    }

    public void AplicarEstadoCajaMunicionRemoto(int indiceCaja, Vector3 posicion, bool disponible, int indiceSpawn)
    {
        AsegurarCajasMunicionInstanciadas();
        if (indiceCaja < 0 || indiceCaja >= cajasMunicion.Count) return;

        CajaMunicion caja = cajasMunicion[indiceCaja];
        caja.IndicePuntoSpawnActual = indiceSpawn;
        if (disponible)
        {
            caja.gameObject.SetActive(true);
            caja.ColocarEnPosicion(posicion);
        }
        else
        {
            caja.EstablecerDisponible(false);
        }
    }

    // Llamado desde Personaje (via RPC al servidor) cuando se suelta el arma conservando exactamente su municion restante
    public void SoltarArmaEnPosicion(string nombreArma, Vector3 posicion, bool enCaja, int municionRestante, float bateriaRestante)
    {
        if (!EsServidor()) return;
        EnviarActualizacionArma(nombreArma, posicion, true, enCaja, municionRestante, bateriaRestante, false);
    }

    private void EnviarActualizacionArma(string nombreArma, Vector3 posicion, bool activar, bool enCaja, int municion, float bateria, bool esNuevaCajaEnSpawn)
    {
        if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsListening)
        {
            AplicarEstadoArmaRemoto(nombreArma, posicion, activar, enCaja, municion, bateria, esNuevaCajaEnSpawn);
            return;
        }

        if (IsSpawned)
        {
            ActualizarArmaRpc(nombreArma, posicion, activar, enCaja, municion, bateria, esNuevaCajaEnSpawn);
            return;
        }

        GameManager gm = FindFirstObjectByType<GameManager>();
        if (gm != null && gm.IsSpawned)
        {
            gm.SincronizarArmaSpawneadaClienteRpc(nombreArma, posicion, activar, enCaja, municion, bateria, esNuevaCajaEnSpawn);
        }
        else
        {
            AplicarEstadoArmaRemoto(nombreArma, posicion, activar, enCaja, municion, bateria, esNuevaCajaEnSpawn);
        }
    }

    // Activa o desactiva un arma en todos los clientes y la mueve a la posición indicada
    [Rpc(SendTo.ClientsAndHost)]
    private void ActualizarArmaRpc(string nombreArma, Vector3 posicion, bool activar, bool enCaja, int municion, float bateria, bool esNuevaCajaEnSpawn)
    {
        AplicarEstadoArmaRemoto(nombreArma, posicion, activar, enCaja, municion, bateria, esNuevaCajaEnSpawn);
    }

    public void AplicarEstadoArmaRemoto(string nombreArma, Vector3 posicion, bool activar, bool enCaja, int municion, float bateria, bool esNuevaCajaEnSpawn)
    {
        Arma arma = BuscarArmaPorNombre(nombreArma);
        if (arma == null)
        {
            Debug.LogWarning($"GestorArmas: no se encontró el arma '{nombreArma}'");
            return;
        }

        AplicarEstadoArmaLocal(arma, posicion, activar, enCaja, municion, bateria, esNuevaCajaEnSpawn);
    }

    private void AplicarEstadoArmaLocal(Arma arma, Vector3 posicion, bool activar, bool enCaja, int municion, float bateria, bool esNuevaCajaEnSpawn)
    {
        if (activar)
        {
            arma.gameObject.SetActive(true);
            arma.ColocarEnCaja(enCaja);
            if (esNuevaCajaEnSpawn)
            {
                arma.PrepararParaNuevaCaja();
                arma.EstablecerEsperandoEnSpawn(true);
            }
            else if (!enCaja)
            {
                arma.EstablecerEstadoMunicion(municion, bateria);
                arma.EstablecerEsperandoEnSpawn(false);
            }
            arma.Soltar(posicion); // desancla del jugador si estaba pegada y la centra en posicion sin reiniciar balas
        }
        else
        {
            arma.EstablecerEsperandoEnSpawn(false);
            arma.ColocarEnCaja(enCaja);
            arma.Soltar(posicion); // si un jugador la traia del mapa anterior, se la quita de la mano
            arma.gameObject.SetActive(false);
        }
    }

    public Arma BuscarArma(string nombre) => BuscarArmaPorNombre(nombre);

    private Arma BuscarArmaPorNombre(string nombre)
    {
        AsegurarListaArmas();
        foreach (Arma a in todasLasArmas)
            if (a != null && a.gameObject.name.Equals(nombre, System.StringComparison.OrdinalIgnoreCase)) return a;
        return null;
    }

    // Mezcla Fisher-Yates
    private static void Mezclar<T>(List<T> lista)
    {
        for (int i = lista.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            (lista[i], lista[j]) = (lista[j], lista[i]);
        }
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = new Color(1f, 0.75f, 0.1f, 0.85f);
        foreach (Transform t in FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (t != null && t.name.StartsWith(GameManager.PREFIJO_ARMA, System.StringComparison.OrdinalIgnoreCase))
            {
                Gizmos.DrawWireCube(t.position, new Vector3(0.45f, 0.45f, 0.45f));
                Gizmos.DrawSphere(t.position, 0.12f);
            }
        }
    }
}
