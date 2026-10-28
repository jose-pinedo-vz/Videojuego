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

    [Header("Nombre para el HUD")]
    [SerializeField] protected string nombreMostrado = ""; // si se deja vacio se usa el nombre del GameObject

    [Header("Municion")]
    [SerializeField] protected int municionMaxima = 10;
    [SerializeField] protected float tiempoRecarga = 1.5f;
    private int municionActual;
    private bool recargando = false;

    [Header("Area de recogida (trigger en el suelo)")]
    [Tooltip("Desplazamiento del centro del collider de recogida. Vector3.zero = no tocar el centro que ya tiene el collider en el editor")]
    [SerializeField] protected Vector3 offsetAreaRecogida = Vector3.zero;
    [Tooltip("Tamanio del area de recogida (solo aplica si el collider es un BoxCollider). Vector3.zero = no tocar el tamanio que ya tiene en el editor")]
    [SerializeField] protected Vector3 tamanoAreaRecogida = Vector3.zero;

    public int MunicionActual => municionActual;
    public int MunicionMaxima => municionMaxima;
    public bool Recargando => recargando;
    public string NombreMostrado => string.IsNullOrEmpty(nombreMostrado) ? gameObject.name : nombreMostrado;

    protected virtual void Awake()
    {
        municionActual = municionMaxima;
        AjustarAreaDeRecogida();
    }

    // Agranda/desplaza el collider de recogida para que no quede detras del modelo del arma
    // y el jugador no tenga que pisar el centro exacto para activarlo
    private void AjustarAreaDeRecogida()
    {
        Collider col = GetComponent<Collider>();
        if (col == null) return;

        col.isTrigger = true;

        // Vector3.zero en cualquiera de los dos campos significa "no tocar lo que ya hay",
        // asi no se pisa el collider que ya este bien ajustado en el editor para esta arma
        bool hayOffset = offsetAreaRecogida != Vector3.zero;
        bool hayTamano = tamanoAreaRecogida != Vector3.zero;

        if (col is BoxCollider box)
        {
            if (hayOffset) box.center = offsetAreaRecogida;
            if (hayTamano) box.size = tamanoAreaRecogida;
        }
        else if (col is SphereCollider sphere)
        {
            if (hayOffset) sphere.center = offsetAreaRecogida;
        }
        else if (col is CapsuleCollider capsule)
        {
            if (hayOffset) capsule.center = offsetAreaRecogida;
        }
    }

    //TERMINADO (El arma detecta al jugador cerca)
    protected virtual void OnTriggerEnter(Collider other)//Funcion para recoger el Arma si el jugador esta cercas (NO LE CAMBIEN EL NOMBRE PORFAS)
    {
        if (other.CompareTag("Player"))
        {
            // solo cuenta el jugador de esta PC, si no un jugador remoto cerca te deja recoger el arma a ti
            Personaje p = other.GetComponentInParent<Personaje>();
            if (p == null || !p.IsOwner) return;

            jugadorCerca = true;
            mano=p.transform.Find("Mano");
            Debug.Log("Cerca del arma, presiona C para recoger");
        }
    }
    protected virtual void OnTriggerExit(Collider other) //Pa saber que el jugador se alejo y ya no la esta tocando
    {
        if (other.CompareTag("Player"))
        {
            Personaje p = other.GetComponentInParent<Personaje>();
            if (p == null || !p.IsOwner) return;

            jugadorCerca = false;
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
        jugadorCerca = false; // con el collider apagado OnTriggerExit ya no se llama, si no se podia recoger otra vez
        recargando = false;
        municionActual = municionMaxima; // el arma se recoge con el cargador lleno
        Personaje jugador = mano.GetComponentInParent<Personaje>(); //Busca el script Personaje subiendo desde la mano
        if (jugador != null)
        {
            jugador.armaEquipada = this; //Le dice al personaje "esta es tu arma ahora"
            jugadorQueLaEquipo=jugador; //Para solucionar un problema que tomo 3 horas resolver...
            jugador.GetComponent<Red>().AvisarArmaRpc(gameObject.name); // por nombre, para que los demas peguen esta misma arma
        }
    }

    // para que los demas jugadores vean el trazo de un disparo que no es suyo
    public void MostrarDisparo(Vector3 inicio, Vector3 fin)
    {
        StartCoroutine(MostrarTrazo(inicio, fin));
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
        if (recargando) return; // no se puede disparar mientras recarga

        //¿Se quedo sin balas?
        if (municionActual <= 0)
        {
            Debug.Log("Sin municion, presiona R para recargar");
            return;
        }

        //¿Esta disparando muy rapido?
        if (Time.time < tiempoUltimoDisparo + (1f / Cadencia))
        {
            return;
        }

        tiempoUltimoDisparo = Time.time;
        municionActual--;

        //Usamos la direccion del JUGADOR (limpia, solo rota en Y) en vez de la del arma (rotada por la pose de la mano)
        Vector3 direccionDisparo = jugadorQueLaEquipo != null ? jugadorQueLaEquipo.transform.right : transform.right;

        direccionDisparo += new Vector3(
            0f,
            Random.Range(-Dispersion, Dispersion) * 0.1f,
            0f
        );

        //Debug.Log("Disparo desde: " + transform.position + " hacia direccion: " + direccionDisparo);

        // el rayo sale a la altura del arma pero en el mismo plano Z que el jugador,
        // si no, por el offset de la mano en Z, el rayo pasa por delante/detras de los demas sin tocarlos
        Vector3 origen = transform.position;
        if (jugadorQueLaEquipo != null) origen.z = jugadorQueLaEquipo.transform.position.z;

        Debug.DrawRay(origen, direccionDisparo * Alcance, Color.red, 2f);


        Vector3 puntoFinal = origen + direccionDisparo.normalized * Alcance;
        RaycastHit impacto;

        //Lanza el rayo para impactar Parametros(Origen ,   Direccon    ,   Resultado(Esta guardando que golpeo)   ,   Distancia)
        if (Physics.Raycast(origen, direccionDisparo, out impacto, Alcance))
        {
            puntoFinal = impacto.point;
            Debug.Log("Le diste a: " + impacto.collider.name + " en la posicion: " + impacto.point);

            Personaje personajeGolpeado = impacto.collider.GetComponentInParent<Personaje>(); // por si el collider esta en un hijo
            if (personajeGolpeado != null && personajeGolpeado != jugadorQueLaEquipo)
            {
                personajeGolpeado.RecibirDaño(jugadorQueLaEquipo);
            }
        }
        else
        {
            Debug.Log("Fallaste");
        }

        // el trazo se ve siempre (antes solo al fallar) y tambien en las PCs de los demas
        StartCoroutine(MostrarTrazo(transform.position, puntoFinal));
        if (jugadorQueLaEquipo != null) jugadorQueLaEquipo.MostrarDisparoRpc(transform.position, puntoFinal);
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
        recargando = false;
        municionActual = municionMaxima; // el arma se recoge con el cargador lleno
    }

    // Se llama al presionar R. Si ya esta recargando o el cargador esta lleno, no hace nada
    public void IniciarRecarga()
    {
        if (recargando || municionActual >= municionMaxima) return;
        StartCoroutine(RecargarAhoraSi());
    }

    private IEnumerator RecargarAhoraSi()
    {
        recargando = true;
        Debug.Log("Recargando...");
        yield return new WaitForSeconds(tiempoRecarga);
        municionActual = municionMaxima;
        recargando = false;
    }

    // Suelta el arma: la desancla del jugador y la coloca en posicion (el suelo)
    // Lo llama GestorArmas en todos los clientes cuando el jugador muere
    public void Soltar(Vector3 posicion)
    {
        // Limpiar referencia del jugador que la tenía
        if (jugadorQueLaEquipo != null)
        {
            if (jugadorQueLaEquipo.armaEquipada == this)
                jugadorQueLaEquipo.armaEquipada = null;
            jugadorQueLaEquipo = null;
        }
        mano = null;
        jugadorCerca = false;
        StopAllCoroutines(); // cancela una recarga en curso, si la habia
        recargando = false;
        municionActual = municionMaxima; // queda lista para el siguiente que la recoja

        transform.parent = null;
        transform.position = posicion;
        transform.rotation = Quaternion.identity; // orientación neutra en el suelo

        Collider col = GetComponent<Collider>();
        if (col != null) col.enabled = true;
    }
}