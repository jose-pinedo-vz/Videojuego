using UnityEngine;
using System.Collections.Generic;
using Unity.Netcode;
using Unity.Collections;
using TMPro;
using UnityEngine.UI;
using Unity.Services.Core;
using Unity.Services.Authentication;



public class GameManager : NetworkBehaviour 
{
    // cuando se trabaja con mensajes en intener no se puede mandar varialbes de 
    // memoria elastica, por eso se usa <FixedString32Bytes> en lugar de <string>
    // string tiene una tamanio de memoria varailbe, e inpredeciable, no apto para viajar por intenet
    public NetworkList<FixedString32Bytes> nombresJugadores;
    private inicio texto;


    // tiempo de duracion de la partida 
    public NetworkVariable<float> tiempoRestante = new NetworkVariable<float>(60f);
    public TextMeshProUGUI txt_tiempo;
    private bool partidaActiva = false;

    //public Transform[] puntosDeSpawn;
    //public Transform[] puntosDeSpawn;

    // movimiento de cada persnaje

    
    // Cada mapa guarda sus spawns como hijos y se encuentran por nombre: "spawn1", "spawn2"...
    // para jugadores y "spawnArma1", "spawnArma2"... para armas
    public const string PREFIJO_JUGADOR = "spawn";
    public const string PREFIJO_ARMA = "spawnArma";

    private int indiceMapa = 0; // mapa de la partida, posicion en inicio.Mapas
    private List<Vector3> spawnsDisponibles = new List<Vector3>(); // los que nadie ha ocupado
    private List<int> ordenMapas = new List<int>(); // mapas que faltan por jugarse, ya mezclados
    private int ultimoMapa = -1;
    private int rondasRestantes = 0; // mapas que faltan por jugar segun la configuracion del host

    // Registro en servidor para asignar uno de los 4 personajes (Cheto, Kaiser, Dorito, CR7) al azar sin repetir
    private readonly Dictionary<ulong, int> personajePorCliente = new Dictionary<ulong, int>();
    private readonly Dictionary<ulong, FixedString32Bytes> etiquetaPorCliente = new Dictionary<ulong, FixedString32Bytes>();

    public int AsignarPersonajeUnico(ulong clientId)
    {
        LimpiarClientesDesconectados();

        if (personajePorCliente.TryGetValue(clientId, out int yaAsignado))
        {
            return yaAsignado;
        }

        HashSet<int> ocupados = new HashSet<int>(personajePorCliente.Values);
        List<int> disponibles = new List<int>();
        for (int i = 0; i < CatalogoPersonajes.TOTAL_PERSONAJES; i++)
        {
            if (!ocupados.Contains(i))
            {
                disponibles.Add(i);
            }
        }

        // Respaldo por si hubiera más de 4 clientes conectados simultáneamente
        if (disponibles.Count == 0)
        {
            for (int i = 0; i < CatalogoPersonajes.TOTAL_PERSONAJES; i++)
                disponibles.Add(i);
        }

        int elegido = disponibles[Random.Range(0, disponibles.Count)];
        personajePorCliente[clientId] = elegido;
        Debug.Log($"GameManager: Cliente {clientId} recibió personaje aleatorio '{CatalogoPersonajes.ObtenerSubnombre(elegido)}' (índice {elegido}).");
        return elegido;
    }

    // Cambio de personaje en el lobby: solo se permite si nadie más lo tiene
    public bool CambiarPersonaje(ulong clientId, int nuevo)
    {
        if (partidaActiva) return false; // solo se elige en el lobby, antes de iniciar
        if (nuevo < 0 || nuevo >= CatalogoPersonajes.TOTAL_PERSONAJES) return false;
        LimpiarClientesDesconectados();
        foreach (var kv in personajePorCliente)
        {
            if (kv.Key != clientId && kv.Value == nuevo) return false;
        }
        personajePorCliente[clientId] = nuevo;
        return true;
    }

    private void LimpiarClientesDesconectados()
    {
        if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsListening) return;

        HashSet<ulong> activos = new HashSet<ulong>(NetworkManager.Singleton.ConnectedClientsIds);
        List<ulong> obsoletos = new List<ulong>();
        foreach (ulong id in personajePorCliente.Keys)
        {
            if (!activos.Contains(id)) obsoletos.Add(id);
        }
        foreach (ulong id in obsoletos)
        {
            personajePorCliente.Remove(id);
            etiquetaPorCliente.Remove(id);
        }
    }

    public void RegistrarJugadorEnSala(ulong clientId, string nombreConSubnombre)
    {
        if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsServer) return;

        FixedString32Bytes valor = nombreConSubnombre;
        etiquetaPorCliente[clientId] = valor;
        SincronizarListaNombresServidor();
    }

    private void SincronizarListaNombresServidor()
    {
        if (!IsServer || nombresJugadores == null) return;

        nombresJugadores.Clear();
        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
        {
            foreach (ulong id in NetworkManager.Singleton.ConnectedClientsIds)
            {
                if (etiquetaPorCliente.TryGetValue(id, out FixedString32Bytes nombre))
                {
                    nombresJugadores.Add(nombre);
                }
            }
        }
        else
        {
            foreach (var kv in etiquetaPorCliente)
            {
                nombresJugadores.Add(kv.Value);
            }
        }
    }

    private void AlDesconectarseCliente(ulong clientId)
    {
        if (!IsServer) return;
        bool habia = personajePorCliente.Remove(clientId);
        habia |= etiquetaPorCliente.Remove(clientId);
        if (habia)
        {
            SincronizarListaNombresServidor();
        }
    }

    // Orden aleatorio: no se repite un mapa hasta haber jugado todos
    private int SiguienteMapa(int cantidadMapas)
    {
        if (ordenMapas.Count == 0)
        {
            for (int i = 0; i < cantidadMapas; i++) ordenMapas.Add(i);

            // Mezcla Fisher-Yates
            for (int i = ordenMapas.Count - 1; i > 0; i--)
            {
                int j = Random.Range(0, i + 1);
                (ordenMapas[i], ordenMapas[j]) = (ordenMapas[j], ordenMapas[i]);
            }

            // al volver a empezar la lista, que no toque dos veces seguidas el mismo
            if (cantidadMapas > 1 && ordenMapas[0] == ultimoMapa)
            {
                (ordenMapas[0], ordenMapas[cantidadMapas - 1]) = (ordenMapas[cantidadMapas - 1], ordenMapas[0]);
            }
        }

        ultimoMapa = ordenMapas[0];
        ordenMapas.RemoveAt(0);
        return ultimoMapa;
    }

    public static List<Transform> BuscarPuntos(GameObject mapa, string prefijo)
    {
        List<Transform> puntos = new List<Transform>();
        if (mapa == null) return puntos;

        foreach (Transform t in mapa.GetComponentsInChildren<Transform>(true)) // true: el mapa esta apagado hasta iniciar
        {
            if (!t.name.StartsWith(prefijo, System.StringComparison.OrdinalIgnoreCase)) continue;
            // "spawnArma1" tambien empieza con "spawn", no es de jugador
            if (prefijo == PREFIJO_JUGADOR && t.name.StartsWith(PREFIJO_ARMA, System.StringComparison.OrdinalIgnoreCase)) continue;
            puntos.Add(t);
        }
        return puntos;
    }

    private GameObject MapaActual()
    {
        if (texto == null) texto = FindFirstObjectByType<inicio>();
        if (texto == null) return null;

        List<GameObject> mapas = texto.Mapas;
        return indiceMapa < mapas.Count ? mapas[indiceMapa] : null;
    }

    // Todos los spawns del mapa en el mismo plano Z (el del que mas se repite), asi uno mal colocado
    // en la escena (ej. spawn2 del patio en z=19) no deja al jugador fuera del alcance de cajas y disparos
    private List<Vector3> TodosLosSpawns()
    {
        List<Vector3> posiciones = new List<Vector3>();
        foreach (Transform punto in BuscarPuntos(MapaActual(), PREFIJO_JUGADOR))
        {
            posiciones.Add(punto.position);
        }
        if (posiciones.Count == 0) return posiciones;

        List<float> zs = new List<float>();
        foreach (Vector3 v in posiciones) zs.Add(v.z);
        zs.Sort();
        float planoZ = zs[zs.Count / 2]; // mediana

        for (int i = 0; i < posiciones.Count; i++)
        {
            posiciones[i] = new Vector3(posiciones[i].x, posiciones[i].y, planoZ);
        }
        return posiciones;
    }

    // Spawn al azar para reaparecer tras morir, evita el mas cercano al lugar de la muerte
    public Vector3 SpawnAleatorio(Vector3 evitar)
    {
        List<Vector3> todos = TodosLosSpawns();
        if (todos.Count == 0) return Vector3.zero;

        if (todos.Count > 1)
        {
            int cercano = 0;
            for (int i = 1; i < todos.Count; i++)
            {
                if ((todos[i] - evitar).sqrMagnitude < (todos[cercano] - evitar).sqrMagnitude) cercano = i;
            }
            todos.RemoveAt(cercano);
        }
        return todos[Random.Range(0, todos.Count)];
    }

    public Vector3 AsignarSpawn()
    {
        // Si ya se ocuparon todos (mas jugadores que spawns) se vuelven a repartir
        if (spawnsDisponibles.Count == 0)
        {
            spawnsDisponibles.AddRange(TodosLosSpawns());
        }

        if (spawnsDisponibles.Count == 0)
        {
            Debug.LogWarning("¡El mapa no tiene spawns de jugador (hijos llamados spawn1, spawn2...)!");
            return Vector3.zero;
        }

        // Aparición aleatoria, cada jugador cae en un spawn distinto
        int indice = Random.Range(0, spawnsDisponibles.Count);
        Vector3 posicion = spawnsDisponibles[indice];
        spawnsDisponibles.RemoveAt(indice);
        return posicion;
    }


    public void iniciarPartida()
    {
        if (NetworkManager.Singleton.IsServer)
        {
            partidaActiva = true;
            ordenMapas.Clear(); // la partida recorre los mapas desde cero
            if (texto == null) texto = FindFirstObjectByType<inicio>();
            int disponibles = texto != null ? texto.Mapas.Count : 1;
            rondasRestantes = ConfiguracionPartida.MapasAJugar(disponibles); // reglas del host

            // marcador en cero al empezar, las bajas se acumulan entre mapas; limpiar cargadores extra al iniciar nueva partida
            foreach (Personaje p in FindObjectsByType<Personaje>(FindObjectsSortMode.None))
            {
                p.bajas.Value = 0;
                p.ReiniciarCargadoresNuevaPartidaRpc();
            }

            iniciarRonda();
        }
    }

    // Una ronda es un mapa con su tiempo, se llama al empezar la partida y al acabarse el tiempo de cada mapa
    private void iniciarRonda()
    {
        tiempoRestante.Value = ConfiguracionPartida.duracionSegundos;
        rondasRestantes--;

        // El servidor elige el mapa y se lo avisa a todos en iniciarClienteRpc
        int cantidadMapas = texto != null ? texto.Mapas.Count : 0;
        indiceMapa = SiguienteMapa(Mathf.Max(cantidadMapas, 1));
        spawnsDisponibles.Clear();
        Debug.Log("Mapa elegido: " + (MapaActual() != null ? MapaActual().name : "ninguno") + ", faltan " + ordenMapas.Count + " de " + cantidadMapas + " mapas");

        // cada jugador a un spawn del mapa elegido
        foreach (Personaje p in FindObjectsByType<Personaje>(FindObjectsSortMode.None))
        {
            Red r = p.GetComponent<Red>();
            if (r != null) r.TeleportDuenioRpc(AsignarSpawn());
        }

        // Activar armas aleatorias en el mapa
        GestorArmas ga = GestorArmas.Instancia;
        if (ga == null) ga = gameObject.AddComponent<GestorArmas>();
        if (ga != null) ga.IniciarRondaArmas(ga.ObtenerOCrearSpawnsEnMapa(MapaActual()));
        else Debug.LogWarning("GameManager: no se encontró GestorArmas");

        iniciarClienteRpc(indiceMapa);
    }

    [Rpc(SendTo.ClientsAndHost)]
    public void SincronizarArmaSpawneadaClienteRpc(string nombreArma, Vector3 posicion, bool activar, bool enCaja, int municion, float bateria, bool esNuevaCajaEnSpawn)
    {
        GestorArmas ga = GestorArmas.Instancia;
        if (ga == null) ga = gameObject.AddComponent<GestorArmas>();
        if (ga != null) ga.AplicarEstadoArmaRemoto(nombreArma, posicion, activar, enCaja, municion, bateria, esNuevaCajaEnSpawn);
    }

    [Rpc(SendTo.Server)]
    public void NotificarCajaArmaTomadaServerRpc(string nombreArma)
    {
        GestorArmas ga = GestorArmas.Instancia;
        if (ga == null) ga = gameObject.AddComponent<GestorArmas>();
        if (ga != null) ga.ProcesarCajaArmaTomadaServidor(nombreArma);
    }

    [Rpc(SendTo.Server)]
    public void SolicitarConsumoCajaMunicionServerRpc(int indiceCaja)
    {
        GestorArmas ga = GestorArmas.Instancia;
        if (ga == null) ga = gameObject.AddComponent<GestorArmas>();
        if (ga != null) ga.ProcesarConsumoCajaMunicionServidor(indiceCaja);
    }

    [Rpc(SendTo.ClientsAndHost)]
    public void SincronizarCajaMunicionClienteRpc(int indiceCaja, Vector3 posicion, bool disponible, int indiceSpawn)
    {
        GestorArmas ga = GestorArmas.Instancia;
        if (ga == null) ga = gameObject.AddComponent<GestorArmas>();
        if (ga != null) ga.AplicarEstadoCajaMunicionRemoto(indiceCaja, posicion, disponible, indiceSpawn);
    }

    [Rpc(SendTo.ClientsAndHost)]
    public void iniciarClienteRpc(int indiceMapaElegido)
    {
        if (texto != null)
        {
            texto.iniciarPartida(indiceMapaElegido);
        }
        if (txt_tiempo != null) txt_tiempo.gameObject.SetActive(true);
    }


    // Declaro una funcion la cual me permite crear una bariable de 32bits, para que no me genre errores e futuro en 
    // conexiones
    private void Awake()
    {
        nombresJugadores = new NetworkList<FixedString32Bytes>();
        if (GetComponent<GestorArmas>() == null && FindFirstObjectByType<GestorArmas>(FindObjectsInactive.Include) == null)
        {
            gameObject.AddComponent<GestorArmas>();
        }
    }


    // cada ves que entrea un jugador, o intancia la clase inicio, se guarde el nombre de este en la bariable
    public override void OnNetworkSpawn()
    {
        texto = FindFirstObjectByType<inicio>();
        nombresJugadores.OnListChanged += OnListaCambio;

        if (IsServer && NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.OnClientDisconnectCallback += AlDesconectarseCliente;
        }

        if (txt_tiempo != null)
        {
            txt_tiempo.gameObject.SetActive(false);
        }


        actualizarTexto();
    }
    
    // cada ves que algien se sale se elimina el nombre de este 
    public override void OnNetworkDespawn()
    {
        nombresJugadores.OnListChanged -= OnListaCambio;
        if (NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.OnClientDisconnectCallback -= AlDesconectarseCliente;
        }
        personajePorCliente.Clear();
        etiquetaPorCliente.Clear();
        actualizarTexto();
    }
    

    // 
    private void OnListaCambio(NetworkListEvent<FixedString32Bytes> cambio)
    {
        actualizarTexto();
    }

    // se encarga de prosesar los datos, agarra la lista, y la inserta al componente respectivo en la interfaz
    // tambine recorre poco a poca la lista de nombre para ordenarlos
    private void actualizarTexto()
    {
        if (texto == null) return;

        string listaCompleta = "";
        foreach (var nombre in nombresJugadores)
        {
            listaCompleta += nombre.ToString() + "\n";
        }

        if (texto.txt_nombres != null) texto.txt_nombres.text = listaCompleta;
        if (texto.txt_outputPlayers != null) texto.txt_outputPlayers.text = listaCompleta;
    }


    // verifica que quine la llame sea el servidor, y ya agrega el nombre a la lista
    public void agregarNombre(string nombre)
    {
        // Debug.Log($"AgregarNombre llamado con '{nombre}', IsServer={IsServer}");
        if (NetworkManager.Singleton.IsServer) // me aseguro de que quien llame e este metodo sea el server y no un user
        {
            nombresJugadores.Add(nombre);
        }
    }

    private void Update()
    {
        // Solo el servidor descuenta el tiempo
        if (IsServer && partidaActiva)
        {
            tiempoRestante.Value -= Time.deltaTime;

            if (tiempoRestante.Value <= 0f)
            {
                tiempoRestante.Value = 0f;
                Debug.Log("Se acabó el tiempo");

                // La partida solo termina cuando ya se jugaron todos los mapas elegidos por el host
                if (rondasRestantes > 0)
                {
                    iniciarRonda();
                }
                else
                {
                    partidaActiva = false;
                    //if (texto != null) texto.mostrarPanelFin();
                    finPartidaClienteRpc();
                }
            }
        }

        // Todos muestran el valor que el servidor sincroniza
        if (txt_tiempo != null)
        {
            txt_tiempo.text = Mathf.CeilToInt(tiempoRestante.Value).ToString();
        }
    }

    [Rpc(SendTo.ClientsAndHost)]
    public void finPartidaClienteRpc()
    {
        if (texto != null) texto.mostrarPanelFin();
    }

}