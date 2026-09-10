using UnityEngine;
using TMPro;
using UnityEngine.UI;

using Unity.Services.Core;
using Unity.Services.Authentication;

public class inicio : MonoBehaviour
{
    // declaracionde componentes
    public GameObject Mapa3D;
    public GameObject piso;

    public TextMeshProUGUI txt_inicio;

    public Button btn_iniciar;
    public Button btn_crearSala; 
    public Button btn_UnirmeSale;
    public Button btn_salir;
    public Button btn_playGame;
    public Button btn_unirmeSala;

    public TMP_InputField inputCode;

    public NetWork net;

    public void UnirNombres()
    {
        
    }

    // crear host
    // se crean los botones de crear sala y unirse a una sala, se espera que el host cree la sala y este a la 
    // espera de invitados
    private void iniciar()
    {
        // desplega las obciones de crear una sala y unirme a una 
        // se espera una presala para de esta manera ser capas de elegir el host, y 
        // separarlos de los invitados 

        btn_crearSala.gameObject.SetActive(true);
        btn_UnirmeSale.gameObject.SetActive(true);

        btn_iniciar.gameObject.SetActive(false);
        btn_salir.gameObject.SetActive(false);
        txt_inicio.gameObject.SetActive(false);

    }


    // salir
    public void salir()
    {
        Debug.Log("Salir");
    }

    // crear host
    // se espera que el host cree la sala y este a la espera de invitados
    // se iniciarn los servicios de unity y se creara un codigo para que los invitados puedan unirse a la sala
    public async void crearSala()
    {
        // creacion de la sala y el host
        btn_UnirmeSale.gameObject.SetActive(false);
        txt_inicio.gameObject.SetActive(true);
        btn_playGame.gameObject.SetActive(true);
        txt_inicio.text = "Generando codigo ...";
        
        NetWork.iniciarHost();
        string code=await net.iniciarRelay();
        //Debug.Log($"se crea el codigo {code}");
        txt_inicio.text = "Code: " + code;

    }

    //  codigo y botones para unirse a la sala del host
    // se espera que el host ya haya creado la sala y este a la espera de invitados
    public async void unirmeSala()
    {
        btn_crearSala.gameObject.SetActive(false);
        btn_UnirmeSale.gameObject.SetActive(false);
        txt_inicio.gameObject.SetActive(true);
        btn_unirmeSala.gameObject.SetActive(true);
        txt_inicio.text="Igrese el codigo de la sala";

        inputCode.gameObject.SetActive(true);

    }

    // despues de ingresar el codigo como invitado, se unira a la sala del host
    public async void juntarPlayer()
    {
        string codigo=inputCode.text;
        try
        {
            if(codigo.Length>0)
            {
                Debug.Log($"Se unira a la sala {codigo}");
                bool exito=await net.unirseAlHost(codigo);
                if (exito)  txt_inicio.text="Conectado con exito";
                else txt_inicio.text="Hubo un error";
            }
            else
            {
                txt_inicio.text="Codigo incorrecto o hubo un error";
            }
        }
        catch (System.Exception ex)
        {
            // Muestra la causa real en la interfaz del juego
            txt_inicio.text = $"Error: {ex.Message}";
        }
    }

    // inciar servicios de unity
    // async es para permitir mensajes a internet si que se detenga la ejecucion del codigo.
    public async void iniciarServicios()
    {
        await UnityServices.InitializeAsync();
        await AuthenticationService.Instance.SignInAnonymouslyAsync();
    }

    // cargar componentes a la interfaz de usuario cuando se abre el juego 
    public void cargarComponentes()
    {
        Mapa3D.SetActive(false);
        piso.SetActive(false);

        // botones sueltos y para usarse despues
        btn_crearSala.gameObject.SetActive(false);
        btn_UnirmeSale.gameObject.SetActive(false);
        btn_playGame.gameObject.SetActive(false);
        inputCode.gameObject.SetActive(false);
        btn_unirmeSala.gameObject.SetActive(false);

    }

    private void Start()
    {
        // esta funcion es el inicio de todo 
        // su objetico, o por lo menos en este momento sera el crear las vistas e interfaces principales
        
        iniciarServicios();
        txt_inicio.text = "Bienvenido";
        cargarComponentes();

        if (btn_iniciar != null)
        {
            btn_iniciar.onClick.AddListener(iniciar);
        }
        if (btn_salir != null)
        {
            btn_salir.onClick.AddListener(salir);
        } 
        if (btn_crearSala != null)
        {
            btn_crearSala.onClick.AddListener(crearSala);
        }
        if (btn_crearSala != null)
        {
            btn_UnirmeSale.onClick.AddListener(unirmeSala);
        }
        if (btn_unirmeSala!=null)
        {
            btn_unirmeSala.onClick.AddListener(juntarPlayer);
        }
    }
}