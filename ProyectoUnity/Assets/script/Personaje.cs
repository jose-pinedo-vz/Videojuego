using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections;

public class Personaje : MonoBehaviour
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
    protected Vector3 velocidadCaida; //variable para ver si esta callendo o para mover en el eje y
    public Arma armaEquipada; //Para saber que arma esta usando
    protected Vector3 posicionInicial;

//==================================================================Cositas Obligatorias por C#====================================================================
    protected virtual void Awake() //Siempre es la primera funcion que se llama
    {
        controlador=GetComponent<CharacterController>(); //Es para los controladores de personaje, como controlar cuando esta chocando con tro objeto 
        posicionInicial = transform.position; //Hace que se tome la posicion en la que empezo el jugador
    }
    protected virtual void Update() //Se actualiza siempre lo que esta dentro de esta funcion
    {
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

        LateUpdate();
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
            movimiento = -1f;
        } 
        else if (Keyboard.current.dKey.isPressed) 
        {
            movimiento = 1f; 
        } //Agarra las teclas a,d o <-,->, para el movimiento

        if (movimiento>0)//Esto es para voltear al personaje, en caso de que le des a la izquierda que gire a la izquierda
        {
            direccionPersonaje=1f;
        }
        else if (movimiento<0)
        {
            direccionPersonaje=-1f;
        }

        bool shiftPresionado = Keyboard.current.leftShiftKey.isPressed; //Si se mantiene presionao
        bool shiftRecienPresionado = Keyboard.current.leftShiftKey.wasPressedThisFrame; //Si solo lo presiono una vez
        
        if (shiftRecienPresionado && !estaAgachado) //Si presiono shift y no esta agachado
        {
            if (movimiento != 0f) //Y no estaba quieto
            {
                Deslizar(direccionPersonaje); //Desliza
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
        controlador.Move(direccion*velocidad*Time.deltaTime); //Esta es la instruccion para que se mueva, hacia la direccion dada, con la velocidad dada y en cierto numero de frames

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

        if (Keyboard.current.leftShiftKey.isPressed) //Si no dejo de presionar shift se queda agachado
        {
            Agachar(true);
        }
    }
    protected virtual void Agachar(bool agachar)
    {
        estaAgachado=agachar;
        Debug.Log("Agachado");
    }
    void LateUpdate()//Fuerza al personaje a estar siempre en el plano z
    {
        Vector3 pos = transform.position;
        pos.z = 0f;
        transform.position = pos;
    }
//==================================================================================================================================================================


//=============================================================================FUNCIONES DE MUERTE Y REGENERACIÓN==================================================
    public virtual void RecibirDaño(Personaje atacante)
    {
        Debug.Log("Auch, me dio: "+atacante);
        morir(atacante);
    }

    public virtual void morir(Personaje atacante)
    {
        atacante.muertes++;//Se le da un punto al que lo mato 
        
        controlador.enabled=false;//Se desactiva al personaje controller para que no haya broncas

        transform.position=posicionInicial; //Se envia al personaje a su posicion de spawn
        velocidadCaida=Vector3.zero; //Para que no haya problemas raros con la velocidad de caida
        controlador.enabled=true;
    }
}
