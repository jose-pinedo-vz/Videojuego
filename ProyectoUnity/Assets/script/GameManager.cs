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

    public Vector3 AsignarSpawn()
    {
        // Si ya se ocuparon todos (mas jugadores que spawns) se vuelven a repartir
        if (spawnsDisponibles.Count == 0)
        {
            foreach (Transform punto in BuscarPuntos(MapaActual(), PREFIJO_JUGADOR))
            {
                spawnsDisponibles.Add(punto.position);
            }
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
            ordenMapas.Clear(); // la partida recorre todos los mapas desde cero

            // marcador en cero al empezar, las bajas se acumulan entre mapas
            foreach (Personaje p in FindObjectsByType<Personaje>(FindObjectsSortMode.None))
            {
                p.bajas.Value = 0;
            }

            iniciarRonda();
        }
    }

    // Una ronda es un mapa con su tiempo, se llama al empezar la partida y al acabarse el tiempo de cada mapa
    private void iniciarRonda()
    {
        tiempoRestante.Value = 60f;

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
        if (ga != null) ga.IniciarRondaArmas(BuscarPuntos(MapaActual(), PREFIJO_ARMA));
        else Debug.LogWarning("GameManager: no se encontró GestorArmas");

        iniciarClienteRpc(indiceMapa);
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
    }


    // cada ves que entrea un jugador, o intancia la clase inicio, se guarde el nombre de este en la bariable
    public override void OnNetworkSpawn()
    {
        texto = FindFirstObjectByType<inicio>();
        nombresJugadores.OnListChanged += OnListaCambio;

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

                // La partida solo termina cuando ya se jugaron todos los mapas
                if (ordenMapas.Count > 0)
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