using UnityEngine;
using UnityEngine.InputSystem;
using Unity.Netcode;
using System.Collections.Generic;

// Gestiona el spawn aleatorio de armas en el mapa.
// Para agregar armas nuevas: colocar el prefab en la escena y arrastrarlo al arreglo
// todasLasArmas en el Inspector. No hay que tocar este script.
// Máximo maxArmasActivas armas visibles a la vez, elegidas aleatoriamente al iniciar partida.
public class GestorArmas : NetworkBehaviour
{
    [Header("Configuración")]
    [Tooltip("Cuántas armas pueden estar en el suelo al mismo tiempo")]
    [SerializeField] private int maxArmasActivas = 5;

    [Header("Armas disponibles — arrastrar todas las que haya en la escena")]
    [SerializeField] private Arma[] todasLasArmas;

    // spawn que le tocó a cada arma en esta partida
    private Dictionary<string, Vector3> spawnAsignado = new Dictionary<string, Vector3>();

    public static GestorArmas Instancia { get; private set; }

    private struct ConfigModeloArma
    {
        public string nombre;
        public string rutaFbx;
        public Vector3 escala;
        public Vector3 rotacionEuler;
        public float cadencia;
        public float alcance;
        public int municionMax;
        public float tiempoRecarga;
    }

    private static readonly ConfigModeloArma[] CONFIG_MODELOS = new ConfigModeloArma[]
    {
        new ConfigModeloArma { nombre = "ak47",   rutaFbx = "Assets/Modelos/ak/ak47.fbx",       escala = new Vector3(0.2f, 0.2f, 0.2f),       rotacionEuler = Vector3.zero,               cadencia = 10f,  alcance = 6f,   municionMax = 30, tiempoRecarga = 1.8f },
        new ConfigModeloArma { nombre = "barret", rutaFbx = "Assets/Modelos/barret/barret.fbx", escala = new Vector3(0.208f, 0.208f, 0.208f), rotacionEuler = new Vector3(0f, 90f, -90f), cadencia = 1.5f, alcance = 15f,  municionMax = 5,  tiempoRecarga = 2.5f },
        new ConfigModeloArma { nombre = "mac10",  rutaFbx = "Assets/Modelos/mac10/mac10.fbx",   escala = new Vector3(0.25f, 0.25f, 0.25f),    rotacionEuler = new Vector3(0f, 0f, 180f),  cadencia = 14f,  alcance = 4.5f, municionMax = 32, tiempoRecarga = 1.4f },
        new ConfigModeloArma { nombre = "bereta", rutaFbx = "Assets/Modelos/bereta/bereta.fbx", escala = new Vector3(25f, 25f, 25f),          rotacionEuler = new Vector3(0f, 180f, 0f),  cadencia = 5f,   alcance = 5.5f, municionMax = 12, tiempoRecarga = 1.2f },
        new ConfigModeloArma { nombre = "raygun", rutaFbx = "Assets/Modelos/raygun/raygun.fbx", escala = new Vector3(0.15f, 0.15f, 0.15f),    rotacionEuler = new Vector3(0f, 180f, 0f),  cadencia = 6f,   alcance = 8f,   municionMax = 15, tiempoRecarga = 2.0f },
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

            if (objArma != null && objArma.GetComponent<Arma>() == null)
            {
                objArma.name = cfg.nombre;
                objArma.transform.localScale = cfg.escala;
                objArma.transform.rotation = Quaternion.Euler(cfg.rotacionEuler);

                if (objArma.GetComponent<Collider>() == null)
                {
                    BoxCollider box = objArma.AddComponent<BoxCollider>();
                    box.isTrigger = true;
                }

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

                Arma nuevoScriptArma = objArma.AddComponent<Arma>();
                nuevoScriptArma.ConfigurarDatosArma(cfg.cadencia, cfg.alcance, cfg.municionMax, cfg.tiempoRecarga);
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

        List<Transform> spawnsJugador = GameManager.BuscarPuntos(mapa, GameManager.PREFIJO_JUGADOR);
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
