using UnityEngine;
using UnityEngine.InputSystem;
using Unity.Netcode;
using System.Collections;
using System.Collections.Generic;

// Gestiona el spawn aleatorio de armas y cajas de munición en el mapa.
// Máximo maxArmasActivas armas visibles a la vez, elegidas aleatoriamente al iniciar partida.
public class GestorArmas : NetworkBehaviour
{
    [Header("Configuración")]
    [Tooltip("Cuántas armas pueden estar en el suelo al mismo tiempo")]
    [SerializeField] private int maxArmasActivas = 6;
    [Tooltip("Cuántas cajas de munición aparecen simultáneamente en el mapa")]
    [SerializeField] private int maxCajasMunicionActivas = 3;
    [Tooltip("Segundos que tarda una caja de munición en reaparecer en otra ubicación")]
    [SerializeField] private float tiempoReaparicionMunicion = 12f;

    [Header("Armas disponibles — arrastrar todas las que haya en la escena")]
    [SerializeField] private Arma[] todasLasArmas;

    // spawn que le tocó a cada arma en esta partida
    private Dictionary<string, Vector3> spawnAsignado = new Dictionary<string, Vector3>();

    // Cajas de municion activas y sus puntos posibles en el mapa actual
    private readonly List<CajaMunicion> cajasMunicion = new List<CajaMunicion>();
    private readonly List<Vector3> puntosMunicionMapa = new List<Vector3>();

    public static GestorArmas Instancia { get; private set; }

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
        public System.Type tipoScript;
    }

    private static readonly ConfigModeloArma[] CONFIG_MODELOS = new ConfigModeloArma[]
    {
        // Armas originales
        new ConfigModeloArma { nombre = "ak47",        rutaFbx = "Assets/Modelos/ak/ak47.fbx",                 escala = new Vector3(0.20f, 0.20f, 0.20f),    rotacionEuler = Vector3.zero,                cadencia = 9.5f,  alcance = 7.5f,  dispersion = 2.5f, municionMax = 20, tiempoRecarga = 1.8f, modo = ModoDisparo.Automatico,     ralentizacion = 0f,    perdigones = 1, tipoScript = typeof(Arma) },
        new ConfigModeloArma { nombre = "barret",      rutaFbx = "Assets/Modelos/barret/barret.fbx",           escala = new Vector3(0.19f, 0.19f, 0.19f),    rotacionEuler = new Vector3(0f, -90f, 90f),  cadencia = 1.5f,  alcance = 15f,   dispersion = 0.1f, municionMax = 5,  tiempoRecarga = 2.5f, modo = ModoDisparo.Semiautomatico, ralentizacion = 0f,    perdigones = 1, tipoScript = typeof(Arma) },
        new ConfigModeloArma { nombre = "mac10",       rutaFbx = "Assets/Modelos/mac10/mac10.fbx",             escala = new Vector3(0.115f, 0.115f, 0.115f), rotacionEuler = new Vector3(0f, -90f, 90f),  cadencia = 16f,   alcance = 4.0f,  dispersion = 0.5f, municionMax = 15, tiempoRecarga = 1.3f, modo = ModoDisparo.Automatico,     ralentizacion = 0f,    perdigones = 1, tipoScript = typeof(Arma) },
        new ConfigModeloArma { nombre = "bereta",      rutaFbx = "Assets/Modelos/bereta/bereta.fbx",           escala = new Vector3(0.125f, 0.125f, 0.125f), rotacionEuler = new Vector3(0f, 180f, 0f),   cadencia = 5f,    alcance = 5.5f,  dispersion = 0.8f, municionMax = 12, tiempoRecarga = 1.2f, modo = ModoDisparo.Semiautomatico, ralentizacion = 0f,    perdigones = 1, tipoScript = typeof(Arma) },
        new ConfigModeloArma { nombre = "raygun",      rutaFbx = "Assets/Modelos/raygun/raygun.fbx",           escala = new Vector3(0.046f, 0.046f, 0.046f), rotacionEuler = new Vector3(90f, 180f, 0f),  cadencia = 20f,   alcance = 80f,   dispersion = 0.0f, municionMax = 10, tiempoRecarga = 2.0f, modo = ModoDisparo.RayoContinuo,   ralentizacion = 0f,    perdigones = 1, tipoScript = typeof(Arma) },

        // 6 Armas nuevas
        new ConfigModeloArma { nombre = "machete",     rutaFbx = "Assets/Modelos/machete/machete.fbx",         escala = Vector3.one,                         rotacionEuler = Vector3.zero,                cadencia = 3.6f,  alcance = 2.45f, dispersion = 0.0f, municionMax = 0,  tiempoRecarga = 0.0f, modo = ModoDisparo.Semiautomatico, ralentizacion = 0f,    perdigones = 1, tipoScript = typeof(ArmaMachete) },
        new ConfigModeloArma { nombre = "antitanques", rutaFbx = "Assets/Modelos/Antitanques/antitanques.fbx", escala = new Vector3(0.19f, 0.19f, 0.19f),    rotacionEuler = new Vector3(0f, -90f, 90f),  cadencia = 0.7f,  alcance = 20f,   dispersion = 0.0f, municionMax = 3,  tiempoRecarga = 3.0f, modo = ModoDisparo.Semiautomatico, ralentizacion = 0.38f, perdigones = 1, tipoScript = typeof(Arma) },
        new ConfigModeloArma { nombre = "barreno",     rutaFbx = "Assets/Modelos/barreno/barreno.fbx",         escala = new Vector3(0.08f, 0.08f, 0.08f),    rotacionEuler = Vector3.zero,                cadencia = 1.4f,  alcance = 9.0f,  dispersion = 0.0f, municionMax = 3,  tiempoRecarga = 1.6f, modo = ModoDisparo.Semiautomatico, ralentizacion = 0f,    perdigones = 1, tipoScript = typeof(ArmaBarreno) },
        new ConfigModeloArma { nombre = "escudo",      rutaFbx = "Assets/Modelos/escudo/escudo.fbx",           escala = new Vector3(0.15f, 0.15f, 0.15f),    rotacionEuler = Vector3.zero,                cadencia = 2.0f,  alcance = 2.25f, dispersion = 0.0f, municionMax = 0,  tiempoRecarga = 0.0f, modo = ModoDisparo.Semiautomatico, ralentizacion = 0f,    perdigones = 1, tipoScript = typeof(ArmaEscudo) },
        new ConfigModeloArma { nombre = "platanos",    rutaFbx = "Assets/Modelos/platanos/penca.fbx",          escala = Vector3.one,                         rotacionEuler = Vector3.zero,                cadencia = 2.2f,  alcance = 6.5f,  dispersion = 0.0f, municionMax = 5,  tiempoRecarga = 1.2f, modo = ModoDisparo.Semiautomatico, ralentizacion = 0f,    perdigones = 1, tipoScript = typeof(ArmaPlatanos) },
        new ConfigModeloArma { nombre = "escopeta",    rutaFbx = "Assets/Modelos/escopeta/escopeta.fbx",       escala = new Vector3(0.17f, 0.17f, 0.17f),    rotacionEuler = Vector3.zero,                cadencia = 1.15f, alcance = 6.2f,  dispersion = 9.0f, municionMax = 5,  tiempoRecarga = 2.2f, modo = ModoDisparo.Semiautomatico, ralentizacion = 0f,    perdigones = 6, tipoScript = typeof(Arma) },
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
        ConfigurarArmasEnEscena();

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
            GameObject objArma = BuscarObjetoArmaEnEscena(cfg.nombre);

#if UNITY_EDITOR
            if (objArma == null)
            {
                GameObject prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(cfg.rutaFbx);
                if (prefab != null)
                {
                    objArma = Instantiate(prefab);
                    objArma.name = cfg.nombre;
                    objArma.transform.localScale = cfg.escala;
                    objArma.transform.rotation = Quaternion.Euler(cfg.rotacionEuler);
                }
            }
#endif
            if (objArma == null)
            {
                objArma = new GameObject(cfg.nombre);
                objArma.transform.localScale = cfg.escala;
                objArma.transform.rotation = Quaternion.Euler(cfg.rotacionEuler);
            }

            if (objArma != null)
            {
                objArma.name = cfg.nombre;
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
                    cfg.perdigones
                );
                scriptArma.RecalibrarModeloVisual();
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

    // Llamado por GameManager.iniciarPartida() en el servidor, con los spawnArma del mapa elegido
    public void IniciarRondaArmas(List<Transform> spawns)
    {
        if (!EsServidor()) return;

        AsegurarListaArmas();

        if (spawns == null || spawns.Count == 0)
        {
            spawns = ObtenerOCrearSpawnsEnEscena();
        }

        // Ocultar todas, en todos los clientes
        foreach (Arma a in todasLasArmas)
            if (a != null) EnviarActualizacionArma(a.gameObject.name, a.transform.position, false, true);

        spawnAsignado.Clear();

        // Mezclar armas y spawns para aleatoriedad por partida
        List<Arma> disponibles = new List<Arma>(todasLasArmas);
        List<Transform> spawnsMezclados = new List<Transform>(spawns);
        Mezclar(disponibles);
        Mezclar(spawnsMezclados);

        int cantidad = Mathf.Min(maxArmasActivas, disponibles.Count, spawnsMezclados.Count);
        for (int i = 0; i < cantidad; i++)
        {
            if (disponibles[i] == null || spawnsMezclados[i] == null) continue;
            string nombre = disponibles[i].gameObject.name;
            Vector3 pos   = spawnsMezclados[i].position;
            spawnAsignado[nombre] = pos;
            // Decirle a todos los clientes que activen esta arma en esa posición dentro de su caja de madera
            EnviarActualizacionArma(nombre, pos, true, true);
        }

        IniciarCajasMunicion(spawns, true);
    }

    // Versión local para escenas de prueba sin servidor de red
    public void IniciarRondaArmasLocal(List<Transform> spawns)
    {
        AsegurarListaArmas();
        if (spawns == null || spawns.Count == 0) return;

        foreach (Arma a in todasLasArmas)
            if (a != null) AplicarEstadoArmaLocal(a, a.transform.position, false, true);

        spawnAsignado.Clear();

        List<Arma> disponibles = new List<Arma>(todasLasArmas);
        List<Transform> spawnsMezclados = new List<Transform>(spawns);
        Mezclar(disponibles);
        Mezclar(spawnsMezclados);

        int cantidad = Mathf.Min(maxArmasActivas, disponibles.Count, spawnsMezclados.Count);
        for (int i = 0; i < cantidad; i++)
        {
            if (disponibles[i] == null || spawnsMezclados[i] == null) continue;
            string nombre = disponibles[i].gameObject.name;
            Vector3 pos   = spawnsMezclados[i].position;
            spawnAsignado[nombre] = pos;
            AplicarEstadoArmaLocal(disponibles[i], pos, true, true);
        }

        IniciarCajasMunicion(spawns, false);
    }

    // =========================================================================
    // SISTEMA DE CAJAS DE MUNICIÓN (SPAWN Y REAPARICIÓN EN UBICACIÓN DISTINTA)
    // =========================================================================

    private void AsegurarCajasMunicionInstanciadas()
    {
        cajasMunicion.RemoveAll(c => c == null);
        while (cajasMunicion.Count < maxCajasMunicionActivas)
        {
            int idx = cajasMunicion.Count;
            string nombreCaja = $"CajaMunicion_{idx}";
            GameObject existente = GameObject.Find(nombreCaja);
            if (existente == null)
            {
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
        if (spawnsArmas == null || spawnsArmas.Count == 0) return;

        // Generar ubicaciones distribuidas a lo largo del mapa (desfasadas respecto a las cajas de armas y entre puntos)
        for (int i = 0; i < spawnsArmas.Count; i++)
        {
            if (spawnsArmas[i] == null) continue;
            Vector3 p = spawnsArmas[i].position;
            float offsetSide = (i % 2 == 0) ? 1.15f : -1.15f;
            puntosMunicionMapa.Add(p + new Vector3(offsetSide, 0.25f, 0f));

            // Punto intermedio entre este spawn y el siguiente en el mismo plano Z
            Transform sig = spawnsArmas[(i + 1) % spawnsArmas.Count];
            if (sig != null)
            {
                Vector3 medio = Vector3.Lerp(p, sig.position, 0.5f);
                medio.z = p.z;
                medio.y = Mathf.Max(p.y, sig.position.y) + 0.25f;
                puntosMunicionMapa.Add(medio);
            }
        }
    }

    private void IniciarCajasMunicion(List<Transform> spawnsArmas, bool enRed)
    {
        StopAllCoroutines();
        AsegurarCajasMunicionInstanciadas();
        ConstruirPuntosMunicion(spawnsArmas);

        if (puntosMunicionMapa.Count == 0) return;

        List<int> indicesLibres = new List<int>();
        for (int i = 0; i < puntosMunicionMapa.Count; i++) indicesLibres.Add(i);
        Mezclar(indicesLibres);

        for (int i = 0; i < cajasMunicion.Count; i++)
        {
            int idxSpawn = indicesLibres[i % indicesLibres.Count];
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

        if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsListening)
        {
            ProcesarConsumoCajaMunicionServidor(caja.IndiceCaja);
            return;
        }

        if (EsServidor())
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
        yield return new WaitForSeconds(tiempoReaparicionMunicion);

        AsegurarCajasMunicionInstanciadas();
        if (indiceCaja < 0 || indiceCaja >= cajasMunicion.Count) yield break;

        if (puntosMunicionMapa.Count == 0)
        {
            ConstruirPuntosMunicion(ObtenerOCrearSpawnsEnEscena());
        }
        if (puntosMunicionMapa.Count == 0) yield break;

        // Elegir una nueva ubicacion distinta a la anterior y que no este ocupada por otra caja de municion
        List<int> candidatos = new List<int>();
        for (int i = 0; i < puntosMunicionMapa.Count; i++)
        {
            if (i == spawnAnterior && puntosMunicionMapa.Count > 1) continue;

            bool ocupado = false;
            foreach (CajaMunicion otra in cajasMunicion)
            {
                if (otra != null && otra.EstaDisponible && otra.IndicePuntoSpawnActual == i)
                {
                    ocupado = true;
                    break;
                }
            }
            if (!ocupado) candidatos.Add(i);
        }

        if (candidatos.Count == 0)
        {
            for (int i = 0; i < puntosMunicionMapa.Count; i++)
            {
                if (i != spawnAnterior || puntosMunicionMapa.Count == 1)
                    candidatos.Add(i);
            }
        }

        int nuevoIndice = candidatos[Random.Range(0, candidatos.Count)];
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

    // Llamado desde Personaje (via RPC al servidor) cuando el jugador muere
    public void SoltarArma(string nombreArma)
    {
        if (!EsServidor()) return;

        Vector3 pos;
        if (!spawnAsignado.TryGetValue(nombreArma, out pos))
        {
            Debug.LogWarning($"GestorArmas: no hay spawn registrado para '{nombreArma}'");
            return;
        }
        // Al morir el jugador, el arma regresa a su spawn dentro de una caja cerrada
        EnviarActualizacionArma(nombreArma, pos, true, true);
    }

    // Llamado cuando el jugador suelta el arma voluntariamente (tecla C) o al romperse la caja (tecla V)
    public void SoltarArmaEnPosicion(string nombreArma, Vector3 posicion, bool enCaja = false)
    {
        if (!EsServidor()) return;

        // No actualizamos spawnAsignado porque el drop es temporal
        // El arma sigue asociada a su spawn original para cuando muera el jugador
        EnviarActualizacionArma(nombreArma, posicion, true, enCaja);
    }

    private void EnviarActualizacionArma(string nombreArma, Vector3 posicion, bool activar, bool enCaja)
    {
        if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsListening)
        {
            AplicarEstadoArmaRemoto(nombreArma, posicion, activar, enCaja);
            return;
        }

        if (IsSpawned)
        {
            ActualizarArmaRpc(nombreArma, posicion, activar, enCaja);
            return;
        }

        GameManager gm = FindFirstObjectByType<GameManager>();
        if (gm != null && gm.IsSpawned)
        {
            gm.SincronizarArmaSpawneadaClienteRpc(nombreArma, posicion, activar, enCaja);
        }
        else
        {
            AplicarEstadoArmaRemoto(nombreArma, posicion, activar, enCaja);
        }
    }

    // Activa o desactiva un arma en todos los clientes y la mueve a la posición indicada
    [Rpc(SendTo.ClientsAndHost)]
    private void ActualizarArmaRpc(string nombreArma, Vector3 posicion, bool activar, bool enCaja)
    {
        AplicarEstadoArmaRemoto(nombreArma, posicion, activar, enCaja);
    }

    public void AplicarEstadoArmaRemoto(string nombreArma, Vector3 posicion, bool activar, bool enCaja)
    {
        Arma arma = BuscarArmaPorNombre(nombreArma);
        if (arma == null)
        {
            Debug.LogWarning($"GestorArmas: no se encontró el arma '{nombreArma}'");
            return;
        }

        AplicarEstadoArmaLocal(arma, posicion, activar, enCaja);
    }

    private void AplicarEstadoArmaLocal(Arma arma, Vector3 posicion, bool activar, bool enCaja)
    {
        if (activar)
        {
            arma.gameObject.SetActive(true);
            arma.ColocarEnCaja(enCaja);
            arma.Soltar(posicion);          // desancla del jugador si estaba pegada y la centra en posicion
        }
        else
        {
            arma.ColocarEnCaja(enCaja);
            arma.Soltar(posicion);          // si un jugador la traia del mapa anterior, se la quita de la mano
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
