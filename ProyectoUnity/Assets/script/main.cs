using UnityEngine;
using TMPro;
using UnityEngine.UI;
using Unity.Services.Core;
using Unity.Services.Authentication;
using Unity.Netcode;

public class inicio : MonoBehaviour
{
    [Header("Paneles de interfaz")]
    [SerializeField] private GameObject PanelInicio;
    [SerializeField] private GameObject PanelSeleccion;
    [SerializeField] private GameObject PanelHost;
    [SerializeField] private GameObject PanelCliente;
    [SerializeField] private GameObject PantallaDeCarga;
    [SerializeField] public GameObject PanelFin;
 
    // variables 
    public static string nombre = "";
    public TextMeshProUGUI txt_tiempo;


    // declaracionde componentes
    public GameObject Mapa3D;
    public GameObject Cochera;
    public TextMeshProUGUI txt_inicio;
    public TextMeshProUGUI txt_codigo;
    public TextMeshProUGUI txt_info;
    public TextMeshProUGUI txt_nombres;
    public TextMeshProUGUI txt_outputPlayers;
    public TextMeshProUGUI txt_resultados; // texto dentro de PanelFin para el marcador
    public Button btn_iniciar;
    public Button btn_crearSala; 
    public Button btn_UnirmeSale;
    public Button btn_salir;
    public Button btn_playGame;
    public Button btn_unirmeSala;
    public TMP_InputField inputCode;
    public TMP_InputField inputName;

    // otras clases 
    public NetWork net;
    public Red red;

    public void mostrarPanelFin()
    {

        // terminar partid
        if (Mapa3D != null) Mapa3D.SetActive(false);
        if (Cochera != null) Cochera.SetActive(false);
        if (txt_tiempo != null) txt_tiempo.gameObject.SetActive(false);

        foreach (Red jugador in FindObjectsByType<Red>((FindObjectsSortMode.None)))
        {
            jugador.MostrarJugador(false);
        }

        if (txt_resultados != null) txt_resultados.text = armarMarcador();

        // panel fin del juego
        if (PanelFin != null) PanelFin.SetActive(true);
    }

    // lista de jugadores ordenada por bajas, el primero es el ganador
    private string armarMarcador()
    {
        Personaje[] jugadores = FindObjectsByType<Personaje>(FindObjectsSortMode.None);
        System.Array.Sort(jugadores, (a, b) => b.bajas.Value.CompareTo(a.bajas.Value));

        string marcador = "";
        foreach (Personaje p in jugadores)
        {
            Red r = p.GetComponent<Red>();
            string nombreJugador = r != null ? r.nombreJugador.Value.ToString() : p.name;
            marcador += nombreJugador + "  -  " + p.bajas.Value + " bajas\n";
        }

        if (jugadores.Length > 0)
        {
            bool empate = jugadores.Length > 1 && jugadores[0].bajas.Value == jugadores[1].bajas.Value;
            Red ganador = jugadores[0].GetComponent<Red>();
            string titulo = empate ? "Empate" : "Gana: " + (ganador != null ? ganador.nombreJugador.Value.ToString() : jugadores[0].name);
            marcador = titulo + "\n\n" + marcador;
        }
        return marcador;
    }


    public void iniciarPartidaBoton()
    {
        Debug.Log("Boton iniciar partida presionado");
        GameManager gm = FindFirstObjectByType<GameManager>();
        if (gm != null)
        {
            gm.iniciarPartida();
        }
        else
        {
            Debug.LogError("No se encontro el GameManager"); // <- y esta también, por si acaso
        }
    }


    // esta funcion se encargara de desactivar los elementos del canvas y iniciar el mapa 
    public void iniciarPartida()
    {
        // PanelInicio.SetActive(false);
        // PanelCliente.SetActive(false);
        // PanelHost.SetActive(false);
        // PanelSeleccion.SetActive(false);
        // PantallaDeCarga.SetActive(false);
        if (PanelInicio != null) PanelInicio.SetActive(false);
        if (PanelCliente != null) PanelCliente.SetActive(false);
        if (PanelHost != null) PanelHost.SetActive(false);
        if (PanelSeleccion != null) PanelSeleccion.SetActive(false);
        if (PantallaDeCarga != null) PantallaDeCarga.SetActive(false);

        //if (PanelFIn != null) PanelFin.SetActive(false);
        if (PanelFin != null) PanelFin.SetActive(false);

        if (Mapa3D != null) Mapa3D.SetActive(true);
        if (Cochera != null) Cochera.SetActive(true);
        if (txt_tiempo != null) txt_tiempo.gameObject.SetActive(true);

        foreach (Red jugador in FindObjectsByType<Red>(FindObjectsSortMode.None))
        {
            jugador.MostrarJugador(true);
        }
    }


    // este codigo se ejecuta siemrpe que algien se conecta al servidor
    public void AlConectarseUnCliente(ulong clienteIDd)
    {
        Debug.Log("Se ha unido algien");
    }

    // se encarge de mostrar los paneles nesesarios y oclutarlos cuando es nesesario
    public void ocultarPaneles()
    {
        Mapa3D.SetActive(false);
        Cochera.SetActive(false);
        if (txt_tiempo != null)
        {
            txt_tiempo.gameObject.SetActive(false);
        }

        // Ocultamos todos los paneles secundarios primero
        PanelSeleccion.SetActive(false);
        PanelHost.SetActive(false);
        PanelCliente.SetActive(false);
        PantallaDeCarga.SetActive(false);
        PanelFin.SetActive(false);

        // Mantenemos únicamente el panel principal encendido
        PanelInicio.SetActive(true);
        
        txt_inicio.text = "Bienvenido";
        
    }

    // crear host
    // se crean los botones de crear sala y unirse a una sala, se espera que el host cree la sala y este a la 
    // espera de invitados
    private void iniciar()
    {
        // desplega las obciones de crear una sala y unirme a una 
        // se espera una presala para de esta manera ser capas de elegir el host, y 
        // separarlos de los invitados 

        PanelSeleccion.SetActive(true);
        PanelInicio.SetActive(false);
        if (inputName != null)
        {
            nombre = inputName.text;
        }
    }


    // salir
    public void salir()
    {
        Debug.Log("Salir");
        // cierra la conexion antes de salir para que los demas vean que se fue
        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
        {
            NetworkManager.Singleton.Shutdown();
        }
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    // crear host
    // se espera que el host cree la sala y este a la espera de invitados
    // se iniciarn los servicios de unity y se creara un codigo para que los invitados puedan unirse a la sala
    public async void crearSala()
    {
        // creacion de la sala y el host

        PanelSeleccion.SetActive(false);
        PanelHost.SetActive(true);

        // txt_nombres.text="Esperando jugadores ...";
        txt_codigo.text = "Generando codigo ...";
        
        //NetWork.iniciarHost();
        string code=await net.iniciarRelay();
        txt_codigo.text = code != null ? "Code: " + code : "No se pudo crear la sala, revisa tu conexion";

        
    }

    //  codigo y botones para unirse a la sala del host
    // se espera que el host ya haya creado la sala y este a la espera de invitados
    public async void unirmeSala()
    {
        PanelSeleccion.SetActive(false);
        PanelCliente.SetActive(true);

        txt_info.text="Igrese el codigo de la sala";

    }

    // despues de ingresar el codigo como invitado, se unira a la sala del host
    public async void juntarPlayer()
    {
  
        string codigo = inputCode != null ? inputCode.text : "";
        //Debug.Log("Intentando unirse a la sala: " + codigo + " con el nombre: " + nombre);
        // coneccion a la red
        bool exito = await net.unirseAlHost(codigo);

        if (exito == true)
        {
            PanelCliente.SetActive(false);
            PantallaDeCarga.SetActive(true);
            //PanelPantallaDeCarga.SetActive(true);
        }
        else
        {
            txt_info.text = "Codigo invalido o la sala no existe";
        }
    }



    // inciar servicios de unity
    // async es para permitir mensajes a internet si que se detenga la ejecucion del codigo.
    public async void iniciarServicios()
    {
        await UnityServices.InitializeAsync();
        await AuthenticationService.Instance.SignInAnonymouslyAsync();

        NetworkManager.Singleton.OnClientConnectedCallback += AlConectarseUnCliente;
    }
    //unirseAlHost

    private void Start()
    {
        iniciarServicios();
        ocultarPaneles();
        

        // asignacion de los eventos para cada boton 
        if (btn_iniciar != null) btn_iniciar.onClick.AddListener(iniciar);
        if (btn_salir != null) btn_salir.onClick.AddListener(salir);
        if (btn_crearSala != null) btn_crearSala.onClick.AddListener(crearSala);
        if (btn_UnirmeSale != null) btn_UnirmeSale.onClick.AddListener(unirmeSala); 
        if (btn_unirmeSala != null) btn_unirmeSala.onClick.AddListener(juntarPlayer);
        // if (btn_playGame != null) btn_playGame.onClick.AddListener(iniciarPartida);
        if (btn_playGame != null) btn_playGame.onClick.AddListener(iniciarPartidaBoton);
    }
}