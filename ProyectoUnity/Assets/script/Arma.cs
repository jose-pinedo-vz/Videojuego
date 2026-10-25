using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections;

public class Arma : MonoBehaviour
{
    [SerializeField] protected int Balas=1;
    [SerializeField] protected float Relantizacion =0f;
    [SerializeField] protected float Cadencia=10.0f;
    [SerializeField] protected float Alcance=5.0f;
    [SerializeField] protected float Retroceso=0-2f;
    [SerializeField] protected float Dispersion=1.0f;
    private bool jugadorCerca = false; //Esto dejas aber si el jugador esta tocando al arma
    private Transform mano; //El objeto al que se pegara cuando el jugador lo agarre (debe ser invisible)
    private float tiempoUltimoDisparo=0f;
    private Personaje jugadorQueLaEquipo;
    private LineRenderer trazo;

    [SerializeField] protected Vector3 rotacionEnMano = Vector3.zero; //Estos 2 son para controlar donde la va a agarrar 
    [SerializeField] protected Vector3 posicionEnMano = Vector3.zero; //Ocupare ayuda en esto porque es a mano, ojo de buen cubero y presicion

    //TERMINADO (El arma detecta al jugador cerca)
    protected virtual void OnTriggerEnter(Collider other)//Funcion para recoger el Arma si el jugador esta cercas (NO LE CAMBIEN EL NOMBRE PORFAS)
    {
        if (other.CompareTag("Player"))
        {
            jugadorCerca = true;
            mano=other.transform.Find("Mano");
            Debug.Log("Cerca del arma, presiona C para recoger");

            Personaje p = other.GetComponentInParent<Personaje>();
            if (p == null || !p.IsOwner) return;
        }
    }
    protected virtual void OnTriggerExit(Collider other) //Pa saber que el jugador se alejo y ya no la esta tocando
    {
        if (other.CompareTag("Player"))
        {
            jugadorCerca = false;
            Personaje p = other.GetComponentInParent<Personaje>();
            if (p == null || !p.IsOwner) return;
        }
    }

    protected virtual void Update()
    {
        if (jugadorCerca && Keyboard.current.cKey.wasPressedThisFrame)
        {
            RecogerArma();
        }
    }
    
    //TERMINADO
    protected virtual void RecogerArma()
    {
        Debug.Log("Arma recogida");

        transform.parent=mano;//Esto hace que el arma pase a ser hija de "la mano" pa que se ponga ahi pues
        transform.localPosition=posicionEnMano; //En donde se va a quedar agarrada
        transform.localRotation=Quaternion.Euler(rotacionEnMano);//Controlar a donde apunta

        GetComponent<Collider>().enabled=false;//Desabilita OnTriggerExit y Enter para que no se la puedan robar (¿Seria interesante si se pudiera?) 
        Personaje jugador = mano.GetComponentInParent<Personaje>(); //Busca el script Personaje subiendo desde la mano
        if (jugador != null)
        {
            jugador.armaEquipada = this; //Le dice al personaje "esta es tu arma ahora"
            jugadorQueLaEquipo=jugador; //Para solucionar un problema que tomo 3 horas resolver...
            jugador.GetComponent<Red>().AvisarArmaRpc();
        }
    }

    private IEnumerator MostrarTrazo(Vector3 inicio, Vector3 fin)
    {
        if (trazo == null) trazo = GetComponent<LineRenderer>();
        if (trazo == null) yield break;
        trazo.positionCount = 2;
        trazo.SetPosition(0, inicio);
        trazo.SetPosition(1, fin);
        trazo.enabled = true;
        yield return new WaitForSeconds(0.1f);
        trazo.enabled = false;
    }

    public virtual void Disparar()
    {
        //¿Esta disparando muy rapido?
        if (Time.time < tiempoUltimoDisparo + (1f / Cadencia))
        {
            return;
        }

        tiempoUltimoDisparo = Time.time;

        //Usamos la direccion del JUGADOR (limpia, solo rota en Y) en vez de la del arma (rotada por la pose de la mano)
        Vector3 direccionDisparo = jugadorQueLaEquipo != null ? jugadorQueLaEquipo.transform.right : transform.right;

        direccionDisparo += new Vector3(
            0f,
            Random.Range(-Dispersion, Dispersion) * 0.1f,
            0f
        );

        //Debug.Log("Disparo desde: " + transform.position + " hacia direccion: " + direccionDisparo);

        Debug.DrawRay(transform.position, direccionDisparo * Alcance, Color.red, 2f);


        Vector3 puntoFinal = transform.position + direccionDisparo.normalized * Alcance;
        RaycastHit impacto;

        //Lanza el rayo para impactar Parametros(Origen ,   Direccon    ,   Resultado(Esta guardando que golpeo)   ,   Distancia)
        if (Physics.Raycast(transform.position, direccionDisparo, out impacto, Alcance))
        {
            puntoFinal = impacto.point;
            Debug.Log("Le diste a: " + impacto.collider.name + " en la posicion: " + impacto.point);

            Personaje personajeGolpeado = impacto.collider.GetComponent<Personaje>();
            if (personajeGolpeado != null && personajeGolpeado != jugadorQueLaEquipo)
            {
                personajeGolpeado.RecibirDaño(jugadorQueLaEquipo);
            }
        }
        else
        {
            StartCoroutine(MostrarTrazo(transform.position, puntoFinal));
            Debug.Log("Fallaste");
        }
    }
    public void AdjuntarA(Transform nuevaMano, Personaje jugador)
    {
        mano = nuevaMano;
        transform.parent = mano;
        transform.localPosition = posicionEnMano;
        transform.localRotation = Quaternion.Euler(rotacionEnMano);
        GetComponent<Collider>().enabled = false;
        jugador.armaEquipada = this;
        jugadorQueLaEquipo = jugador;
    }
}