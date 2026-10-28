using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;

//public class Personaje : MonoBehaviour
public class Personaje : NetworkBehaviour
{
    [SerializeField] protected float salto = 10.0f;
    [SerializeField] protected float gravedad= 25f;
    [SerializeField] protected int muertes=1;
    [SerializeField] protected float velocidad=5f;
    [SerializeField] protected float velocidadDeslizar = 8f; 
    [SerializeField] protected float duracionDeslizar = 0.5f;
    [SerializeField] protected float velicidadAgachado = 2f;


    protected float direccionPersonaje=1f;
    protected bool estaDeslizando=false;
    protected bool estaAgachado=false;
    protected CharacterController controlador;

    public bool EstaDeslizando => estaDeslizando;
    public bool EnSuelo        => controlador != null && controlador.isGrounded;
    public bool EstaAgachado   => estaAgachado;

    protected Vector3 velocidadCaida; //variable para ver si esta callendo o para mover en el eje y
    public Arma armaEquipada; //Para saber que arma esta usando
    protected Vector3 posicionInicial;

    // bajas (kills) del jugador, solo el servidor las escribe y todos las leen para el marcador
    public NetworkVariable<int> bajas = new NetworkVariable<int>(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    // estado de animacion: el duenio lo escribe y todos lo aplican a su Animator, asi los demas ven las animaciones
    private NetworkVariable<float> animMovimiento  = new NetworkVariable<float>(0f,    NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
    private NetworkVariable<bool>  animEnSuelo      = new NetworkVariable<bool>(true,  NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
    private NetworkVariable<bool>  animSaltar       = new NetworkVariable<bool>(false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
    private NetworkVariable<bool>  animDeslizando   = new NetworkVariable<bool>(false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
    private NetworkVariable<bool>  animAgachado     = new NetworkVariable<bool>(false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
    private NetworkVariable<bool>  animCorriendo    = new NetworkVariable<bool>(false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);

    protected Animator animator;
    // nombre en minusculas -> hash del parametro real, para que "saltar" encuentre "Saltar" y "estaEnSuelo" encuentre "EstaEnSuelo"
    private Dictionary<string, int> parametrosAnimator = new Dictionary<string, int>();
    // para detectar el flanco ascendente de animSaltar (Trigger, no Bool en el controller nuevo)
    private bool prevAnimSaltar = false;

//==================================================================Cositas Obligatorias por C#====================================================================
    protected virtual void Awake() //Siempre es la primera funcion que se llama
    {
        controlador=GetComponent<CharacterController>(); //Es para los controladores de personaje, como controlar cuando esta chocando con tro objeto
        posicionInicial = transform.position; //Hace que se tome la posicion en la que empezo el jugador

        animator = GetComponentInChildren<Animator>(); // sirve si el Animator esta en el cuerpo o en el modelo hijo
        if (animator != null && animator.runtimeAnimatorController != null)
        {
            foreach (AnimatorControllerParameter parametro in animator.parameters)
            {
                parametrosAnimator[parametro.name.ToLower()] = parametro.nameHash;
            }
        }
    }
    protected virtual void Update() //Se actualiza siempre lo que esta dentro de esta funcion
    {

        if (!IsOwner) return; // un objeto por equipo
        if (!GetComponentInChildren<Renderer>().enabled) return; // espera a que empeise la partida



        //Agarra el movimiento y la gravedad
        Mover();
        GravedadJugador();
    
        //teclas especiales para funciones
        if (!estaDeslizando)
        {
        if (controlador.isGrounded && Keyboard.current.spaceKey.wasPressedThisFrame)
        {
            Saltar();
        }
        }

        if (armaEquipada != null && Keyboard.current.vKey.wasPressedThisFrame) //Solo dispara si tiene arma equipada
        {
            armaEquipada.Disparar();
        }

        if (armaEquipada != null && Keyboard.current.rKey.wasPressedThisFrame) //Recargar
        {
            armaEquipada.IniciarRecarga();
        }

        // el salto termina cuando vuelve a tocar el suelo
        animEnSuelo.Value = controlador.isGrounded;
        if (controlador.isGrounded && velocidadCaida.y <= 0f)
        {
            animSaltar.Value = false;
        }

        //LateUpdate();
    }
//================================================================================================================================================================


//=================================================================FUNCIONES DE MOVIMIENTO========================================================================
    //TERMINADO MOVER
    protected virtual void Mover()
    {
        if (estaDeslizando) return;
        
        float movimiento = 0f;
        if (Keyboard.current.aKey.isPressed) 
        { 
            movimiento = 1f;
        } 
        else if (Keyboard.current.dKey.isPressed) 
        {
            movimiento = -1f; 
        } //Agarra las teclas a,d o <-,->, para el movimiento

        if (movimiento>0)//Esto es para voltear al personaje, en caso de que le des a la izquierda que gire a la izquierda
        {
            direccionPersonaje=1f; // misma direccion que el movimiento, si no el slide sale para el lado contrario
        }
        else if (movimiento<0)
        {
            direccionPersonaje=-1f;
        }
        // gira al personaje en Y para que transform.right (hacia donde dispara el arma) apunte a donde camina
        transform.rotation = Quaternion.Euler(0f, direccionPersonaje > 0 ? 0f : 180f, 0f);
        animMovimiento.Value = Mathf.Abs(movimiento);
        // estaCorriendo: se mueve horizontalmente sin deslizarse
        animCorriendo.Value = (movimiento != 0f) && !estaDeslizando;

        bool shiftPresionado = Keyboard.current.leftShiftKey.isPressed; //Si se mantiene presionao
        bool shiftRecienPresionado = Keyboard.current.leftShiftKey.wasPressedThisFrame; //Si solo lo presiono una vez
        
        if (shiftRecienPresionado && !estaAgachado) //Si presiono shift y no esta agachado
        {
            if (movimiento != 0f) //Y no estaba quieto
            {
                Deslizar(movimiento); //Desliza
                return;
            }
            else //Sisi estaba quieto
            {
                Agachar(true); //Si agacha
            } 
        } 

        if (!shiftPresionado && estaAgachado) //Si dejo d epresionar shift que se lev ante
        {
            Agachar(false);
        }

        Vector3 direccion=new Vector3(movimiento,0f,0f); //Transforma el valor del movimiento (1 o -1) en un valor vectorial para que se mueva en esa direccion
        float velocidadActual = estaAgachado ? velicidadAgachado : velocidad; // agachado camina mas lento
        controlador.Move(direccion*velocidadActual*Time.deltaTime); //Esta es la instruccion para que se mueva, hacia la direccion dada, con la velocidad dada y en cierto numero de frames

    }
    //TERMINADO GRAVEDAD
    protected virtual void GravedadJugador() //La gravedad que jala al personaje hacia abajp
    {
        if (controlador.isGrounded && velocidadCaida.y<0) //isGrounded revisa si esta chocando con algo abajo, es decir si esta en el suelo 
        {
            velocidadCaida.y=-2f; 
            //Si el personaje está en el suelo y su velocidad vertical es negativa (está cayendo)
            //se establece una pequeñavelocidad hacia abajo para mantenerlo pegado al suelo.
        }

        velocidadCaida.y -= gravedad * Time.deltaTime; 
        controlador.Move(velocidadCaida * Time.deltaTime);
        //En caso de que lo anterior no suceda entonces simplemente se va reduciendo la posicion del personaje en y hasta que toque el suelo
    }
    //SOLO FALTA QUE APUNTE HACIA ARRIBA... NO SE COMO VA A "APUNTAR" SIQUIERA
    protected virtual void Saltar()
    {
        //falta hacer que cuando salte apunte hacia arriba
        //Debug.Log("Arriba");
        velocidadCaida.y=salto;
        animSaltar.Value = true;
        //Como ya tenemos la gravedad y la variable velocidadCaida.y que es practicamente la posicion del personaje en y 
        //solo hacemos que el personaje cambie esa posoicion aprovechando esta variable
    }
    //TERMINADO DESLIZARSE
    protected virtual void Deslizar(float movimiento)
    {
        {
            if (estaDeslizando) return; // Para quye no castee mucho la tecla de deslizarse
            StartCoroutine(DeslizarseAhoraSi(movimiento));
        }
    }
    //TERMINADO
    protected virtual IEnumerator DeslizarseAhoraSi(float movimiento)
    {
        //Debug.Log("Wiii");
        estaDeslizando=true;
        animDeslizando.Value = true;
        animMovimiento.Value = 1f;
        Quaternion rotacionOriginal=transform.rotation; //Guardar como estaba antes de deslizarse
        float tiempoTranscurrido = 0f;
        while (tiempoTranscurrido < duracionDeslizar)
        {
            Vector3 direccionDeslizar = new Vector3(movimiento, 0f, 0f); //De nuevo, para saber cual es el frente y hacia donde se deslizara
            controlador.Move(direccionDeslizar * velocidadDeslizar * Time.deltaTime); //avanza hacia enfrente

            tiempoTranscurrido += Time.deltaTime; //Contador del tiempo
            yield return null; //Espera al siguiente frame y sigue el ciclo para que se vea la animacion en un futuro
        }
        estaDeslizando = false;
        animDeslizando.Value = false;

        if (Keyboard.current.leftShiftKey.isPressed) //Si no dejo de presionar shift se queda agachado
        {
            Agachar(true);
        }
    }
    protected virtual void Agachar(bool agachar)
    {
        estaAgachado=agachar;
        animAgachado.Value = agachar;
        Debug.Log("Agachado");
    }
    void LateUpdate()
    {
        // Rotación garantizada cada frame (Blaidmir: asegura orientación correcta fuera de Mover)
        if (direccionPersonaje < 0f)       transform.rotation = Quaternion.Euler(0f, 180f, 0f);
        else if (direccionPersonaje > 0f)  transform.rotation = Quaternion.Euler(0f, 0f,   0f);

        // AnimadorPuente maneja el Animator del dueño directamente.
        // Aquí solo actualizamos para los clientes que observan a este jugador.
        ActualizarAnimator();
    }

    // Aplica el estado sincronizado al Animator local.
    // En el dueño, AnimadorPuente ya lo hace; aquí manejamos los parámetros que solo
    // Personaje conoce (por NetworkVariable) para que todos los observadores vean
    // las animaciones correctas.
    private void ActualizarAnimator()
    {
        if (animator == null || !IsSpawned) return;
        if (IsOwner) return; // el dueño usa AnimadorPuente que lo maneja con input directo

        AsignarFloat("movimiento",    animMovimiento.Value);
        AsignarBool("EstaEnSuelo",    animEnSuelo.Value);
        AsignarBool("estaDeslizando", animDeslizando.Value);
        AsignarBool("estaAgachado",   animAgachado.Value);
        AsignarBool("estaCorriendo",  animCorriendo.Value);

        // Saltar es Trigger en el controller nuevo: disparar solo en el flanco ascendente
        if (animSaltar.Value && !prevAnimSaltar) AsignarTrigger("Saltar");
        prevAnimSaltar = animSaltar.Value;
    }

    private void AsignarFloat(string nombre, float valor)
    {
        if (parametrosAnimator.TryGetValue(nombre.ToLower(), out int hash)) animator.SetFloat(hash, valor);
    }

    private void AsignarBool(string nombre, bool valor)
    {
        if (parametrosAnimator.TryGetValue(nombre.ToLower(), out int hash)) animator.SetBool(hash, valor);
    }

    private void AsignarTrigger(string nombre)
    {
        if (parametrosAnimator.TryGetValue(nombre.ToLower(), out int hash)) animator.SetTrigger(hash);
    }

    
//==================================================================================================================================================================


//=============================================================================FUNCIONES DE MUERTE Y REGENERACIÓN==================================================
    public virtual void RecibirDaño(Personaje atacante)
    {
        Debug.Log("Auch, me dio: "+atacante);
        // se le avisa al servidor, que es quien cuenta la baja y manda a morir al duenio de este personaje
        if (atacante != null) atacante.ReportarImpactoRpc(NetworkObject);
        else MorirDuenioRpc();
    }

    // lo llama el que disparo, se ejecuta en el servidor
    [Rpc(SendTo.Server)]
    private void ReportarImpactoRpc(NetworkObjectReference referenciaVictima)
    {
        if (!referenciaVictima.TryGet(out NetworkObject objetoVictima)) return;
        Personaje victima = objetoVictima.GetComponent<Personaje>();
        if (victima == null || victima == this) return;

        bajas.Value++; //Se le da un punto al que lo mato
        victima.MorirDuenioRpc();
    }

    // el trazo del disparo para que lo vean los demas jugadores
    [Rpc(SendTo.NotMe)]
    public void MostrarDisparoRpc(Vector3 inicio, Vector3 fin)
    {
        if (armaEquipada != null) armaEquipada.MostrarDisparo(inicio, fin);
    }

    [Rpc(SendTo.Owner)]
    private void MorirDuenioRpc()
    {
        morir(null);
    }

    public virtual void morir(Personaje atacante)
    {
        //atacante.muertes++;//Se le da un punto al que lo mato
        //atacante.muertes++;
        if (atacante != null) atacante.muertes++;

        // Soltar arma antes de teletransportarse para que no se vaya con el jugador
        if (armaEquipada != null)
        {
            SoltarArmaServRpc(armaEquipada.gameObject.name);
            armaEquipada = null;
        }

        controlador.enabled=false;//Se desactiva al personaje controller para que no haya broncas

        transform.position=posicionInicial; //Se envia al personaje a su posicion de spawn
        var nt = GetComponent<Unity.Netcode.Components.NetworkTransform>();
        if (nt != null) nt.Teleport(posicionInicial, transform.rotation, transform.localScale);

        velocidadCaida=Vector3.zero; //Para que no haya problemas raros con la velocidad de caida
        controlador.enabled=true;
    }

    // El dueño le avisa al servidor qué arma soltar, el servidor llama a GestorArmas
    [Rpc(SendTo.Server)]
    private void SoltarArmaServRpc(string nombreArma)
    {
        GestorArmas ga = GestorArmas.Instancia;
        if (ga != null) ga.SoltarArma(nombreArma);
    }
    public void FijarSpawn(Vector3 pos)
    {
        posicionInicial = pos;
    }
}
