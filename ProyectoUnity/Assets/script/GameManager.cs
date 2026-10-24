using UnityEngine;
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

    
    [SerializeField] private Transform[] puntosDeSpawn; // O List<Transform>
    private int indiceSpawn = 0;

    public Vector3 AsignarSpawn()
    {
        if (puntosDeSpawn == null || puntosDeSpawn.Length == 0)
        {
            Debug.LogWarning("¡No hay puntos de spawn asignados en el GameManager!");
            return Vector3.zero;
        }

        // Selecciona el punto actual
        Vector3 posicion = puntosDeSpawn[indiceSpawn].position;

        // Avanza al siguiente punto de spawn (si hay 2 spawns, alterna entre 0 y 1)
        indiceSpawn = (indiceSpawn + 1) % puntosDeSpawn.Length;

        return posicion;
    }


    public void iniciarPartida()
    {
        if (NetworkManager.Singleton.IsServer)
        {
            
            //txt_tiempo.SetActive(true);
            // txt_tiempo.gameObject.SetActive(true);


            tiempoRestante.Value = 60f;
            partidaActiva = true;
            iniciarClienteRpc();
        }
    }

    [Rpc(SendTo.ClientsAndHost)]
    public void iniciarClienteRpc()
    {
        if (texto != null)
        {
            texto.iniciarPartida();
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
                partidaActiva = false;
                Debug.Log("Se acabó el tiempo"); 
            }
        }

        // Todos muestran el valor que el servidor sincroniza
        if (txt_tiempo != null)
        {
            txt_tiempo.text = Mathf.CeilToInt(tiempoRestante.Value).ToString();
        }
    }

}