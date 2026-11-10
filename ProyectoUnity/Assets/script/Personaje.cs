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
    // +1 cuando mira a la izquierda (+X), -1 cuando mira a la derecha (-X)
    public float DireccionHorizontal => (!IsSpawned || IsOwner)
        ? (direccionPersonaje >= 0f ? 1f : -1f)
        : (transform.forward.x >= 0f ? 1f : -1f);

    protected Vector3 velocidadCaida; //variable para ver si esta callendo o para mover en el eje y
    public Arma armaEquipada; //Para saber que arma esta usando
    protected Vector3 posicionInicial;
    protected Vector3 ultimaPosicionSegura; // ultimo punto donde estuvo pisando suelo, para recuperarse de una caida

    // Estados de empuje (Escudo Troyano / retroceso de disparo) y resbalon ragdoll (Cascara de Platano)
    private Vector3 velocidadEmpuje;
    public bool EstaResbalandoPlatano => EnRagdoll;

    // Almacen de cargador extra en el personaje (maximo 1 universal; se conserva al soltar/cambiar arma y se pierde al morir)
    public const int MAX_CARGADORES_EXTRA = 1;
    private int cargadoresExtra = 0;
    public int CargadoresExtra => cargadoresExtra;
    public bool PuedeGuardarCargadorExtra() => !muriendo && cargadoresExtra < MAX_CARGADORES_EXTRA;
    public bool AgregarCargadorExtra()
    {
        if (!PuedeGuardarCargadorExtra()) return false;
        cargadoresExtra = MAX_CARGADORES_EXTRA;
        return true;
    }
    public bool ConsumirCargadorExtra()
    {
        if (cargadoresExtra <= 0) return false;
        cargadoresExtra--;
        return true;
    }
    public void LimpiarCargadoresExtra() => cargadoresExtra = 0;

    // al aparecer en un mapa se espera un momento a que sus colliders esten listos y se revisa que el spawn tenga piso
    private bool revisarSpawnPendiente = false;
    private float revisarSpawnEn = 0f;
    private bool muriendo = false;
    private const float DURACION_MUERTE = 1.1f; // segundos que dura el modelo hecho pedazos antes de reaparecer
    private const int CUBOS_MUERTE = 22;       // cantidad de cubitos en que se rompe el modelo
    private const float DISTANCIA_CAIDA = 12f; // cuanto puede bajar del ultimo suelo pisado antes de considerarse caido del mapa

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
        ultimaPosicionSegura = posicionInicial;

        // el personaje es muy pequeño: con el skinWidth original (0.08, mas que su radio) se atoraba en paredes y esquinas
        controlador.skinWidth = 0.02f;
        controlador.minMoveDistance = 0f;
        controlador.stepOffset = 0.15f;
        transform.rotation = Quaternion.Euler(0f, direccionPersonaje > 0f ? 90f : -90f, 0f);

        RefrescarAnimator();
    }

    public void RefrescarAnimator()
    {
        animator = GetComponent<Animator>();
        if (animator == null) animator = GetComponentInChildren<Animator>();

        parametrosAnimator.Clear();
        if (animator != null && animator.runtimeAnimatorController != null)
        {
            foreach (AnimatorControllerParameter parametro in animator.parameters)
            {
                parametrosAnimator[parametro.name.ToLower()] = parametro.nameHash;
            }
        }
    }

    private bool TieneModeloVisible()
    {
        foreach (Renderer r in GetComponentsInChildren<Renderer>(false))
        {
            if (r == null || !r.enabled) continue;
            if (r is SkinnedMeshRenderer) return true;
            if (r is MeshRenderer
                && r.GetComponentInParent<Arma>() == null
                && r.GetComponent<TMPro.TMP_Text>() == null)
            {
                return true;
            }
        }
        return false;
    }

    protected virtual void Update() //Se actualiza siempre lo que esta dentro de esta funcion
    {

        if (IsSpawned && !IsOwner) return; // un objeto por equipo en red
        if (!TieneModeloVisible()) return; // espera a que empeise la partida
        if (muriendo) return; // en plena animacion de muerte no se mueve
        if (EnRagdoll) return; // tirado como muñeco, no se controla



        if (revisarSpawnPendiente)
        {
            if (revisarSpawnEn < 0f) revisarSpawnEn = Time.time + 0.2f; // empieza a contar cuando el jugador ya esta activo
            if (Time.time < revisarSpawnEn) return; // quieto mientras el mapa termina de activarse
            revisarSpawnPendiente = false;
            AjustarSpawnAlSuelo();
        }

        //Agarra el movimiento y la gravedad
        Mover();
        GravedadJugador();
        RecuperarDeCaida();
    
        //teclas especiales para funciones
        if (!estaDeslizando)
        {
        if (controlador.isGrounded && AjustesJuego.RecienPresionada(Accion.Saltar))
        {
            Saltar();
        }
        }

        if (armaEquipada != null && Keyboard.current != null)
        {
            bool presionandoV = AjustesJuego.Presionada(Accion.Disparar);
            bool presionoV = AjustesJuego.RecienPresionada(Accion.Disparar);
            bool soltoV = AjustesJuego.RecienSoltada(Accion.Disparar);

            armaEquipada.ProcesarEntradaDisparo(presionandoV, presionoV, soltoV);

            // Entrada secundaria (tecla F o clic derecho, ej. para lanzar el machete)
            if (armaEquipada != null)
            {
                bool presionoF = AjustesJuego.RecienPresionada(Accion.Secundaria)
                    || (Mouse.current != null && Mouse.current.rightButton.wasPressedThisFrame);
                bool presionandoF = AjustesJuego.Presionada(Accion.Secundaria)
                    || (Mouse.current != null && Mouse.current.rightButton.isPressed);
                armaEquipada.ProcesarEntradaSecundaria(presionoF, presionandoF);
            }
        }

        if (armaEquipada != null && !armaEquipada.EnCaja && AjustesJuego.RecienPresionada(Accion.Recargar)) //Recargar
        {
            armaEquipada.IniciarRecarga();
        }

        // Si tiene arma equipada y presiona C, la suelta
        if (armaEquipada != null && AjustesJuego.RecienPresionada(Accion.Recoger) && armaEquipada.TiempoRecogida <= 0f)
        {
            Debug.Log("Tecla C presionada para soltar arma");
            armaEquipada.SoltarVoluntariamente();
            armaEquipada = null;
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
        // Impulso de retroceso cuando es golpeado por un Escudo Troyano
        if (velocidadEmpuje.sqrMagnitude > 0.001f)
        {
            controlador.Move(new Vector3(velocidadEmpuje.x, 0f, 0f) * Time.deltaTime);
            velocidadEmpuje = Vector3.MoveTowards(velocidadEmpuje, Vector3.zero, 28f * Time.deltaTime);
        }

        if (estaDeslizando) return;
        
        float movimiento = 0f;
        if (AjustesJuego.Presionada(Accion.Izquierda)) 
        { 
            movimiento = 1f;
        } 
        else if (AjustesJuego.Presionada(Accion.Derecha)) 
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
        // gira al personaje en Y para que mire hacia la dirección del movimiento
        transform.rotation = Quaternion.Euler(0f, direccionPersonaje > 0 ? 90f : -90f, 0f);
        animMovimiento.Value = Mathf.Abs(movimiento);
        // estaCorriendo: se mueve horizontalmente sin deslizarse
        animCorriendo.Value = (movimiento != 0f) && !estaDeslizando;

        bool resbalandoPlatano = EstaResbalandoPlatano;
        bool shiftPresionado = AjustesJuego.Presionada(Accion.Deslizar); //Si se mantiene presionao
        bool shiftRecienPresionado = AjustesJuego.RecienPresionada(Accion.Deslizar); //Si solo lo presiono una vez
        
        if (shiftRecienPresionado && !estaAgachado && !resbalandoPlatano) //Si presiono shift y no esta agachado ni resbalando
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

        Vector3 direccion = new Vector3(movimiento, 0f, 0f); //Transforma el valor del movimiento (1 o -1) en un valor vectorial para que se mueva en esa direccion
        float velocidadActual = estaAgachado ? velicidadAgachado : velocidad; // agachado camina mas lento

        // Ralentizacion por portar arma pesada (ej. Francotirador Antitanques)
        if (armaEquipada != null && !armaEquipada.EnCaja)
        {
            velocidadActual *= (1f - armaEquipada.FactorRalentizacion);
        }

        // Si piso una cascara de platano, no se puede mover bien durante unos segundos
        if (resbalandoPlatano)
        {
            velocidadActual *= 0.22f;
            float tambaleo = Mathf.Sin(Time.time * 18f) * 0.28f;
            direccion.x += tambaleo;
        }

        controlador.Move(direccion * velocidadActual * Time.deltaTime); //Esta es la instruccion para que se mueva, hacia la direccion dada, con la velocidad dada y en cierto numero de frames

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

        // con un tiron de FPS el paso seria enorme y atravesaria el piso, por eso se limita
        float dt = Mathf.Min(Time.deltaTime, 0.033f);
        velocidadCaida.y = Mathf.Max(velocidadCaida.y - gravedad * dt, -30f);
        Vector3 pasoGravedad = velocidadCaida * dt;
        pasoGravedad.z = posicionInicial.z - transform.position.z; //Regresa al plano del spawn, el relieve del mapa empuja al personaje en Z. Va en este Move para no perder isGrounded
        controlador.Move(pasoGravedad);
        // si pega con un techo se cancela el impulso, si no se queda pegado en el aire
        if ((controlador.collisionFlags & CollisionFlags.Above) != 0 && velocidadCaida.y > 0f) velocidadCaida.y = 0f;
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

        if (AjustesJuego.Presionada(Accion.Deslizar)) //Si no dejo de presionar shift se queda agachado
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
        // Rotación garantizada cada frame en el dueño (o en escenas de prueba sin red)
        if (!IsSpawned || IsOwner)
        {
            if (direccionPersonaje < 0f)       transform.rotation = Quaternion.Euler(0f, -90f, 0f);
            else if (direccionPersonaje > 0f)  transform.rotation = Quaternion.Euler(0f, 90f, 0f);
        }

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
    /// <summary>
    /// Procesa un impacto de disparo o ataque considerando:
    /// 1) Si el personaje porta un Escudo Troyano que cubre la zona golpeada.
    /// 2) Si el personaje porta Barrenos activos (explotan al recibir un disparo).
    /// </summary>
    public virtual void RecibirImpactoDeDisparo(Personaje atacante, Vector3 puntoImpacto, Vector3 direccionDisparo)
    {
        if (muriendo) return;

        // 1. Verificar bloqueo del Escudo Troyano
        if (armaEquipada is ArmaEscudo escudo && escudo.BloqueaImpacto(puntoImpacto, direccionDisparo))
        {
            Debug.Log($"[Escudo] Impacto bloqueado en {name}");
            return;
        }

        // 2. Verificar si porta Barrenos (si alguien le dispara al portador con los barrenos, este explotara)
        if (armaEquipada is ArmaBarreno barreno && barreno.PuedeExplotarAlRecibirDisparo())
        {
            Debug.Log($"[Barreno] {name} recibio disparo portando barrenos -> ¡Detonacion inmediata!");
            barreno.DetonarEnPortador(atacante);
            return;
        }

        RecibirDaño(atacante);
    }

    public virtual void RecibirDaño(Personaje atacante)
    {
        if (muriendo) return;
        Debug.Log("Auch, me dio: "+atacante);

        // Si tambien tenia barrenos al recibir daño directo, detonar
        if (armaEquipada is ArmaBarreno barreno && barreno.PuedeExplotarAlRecibirDisparo())
        {
            barreno.DetonarEnPortador(atacante);
            return;
        }

        if (!IsSpawned)
        {
            StartCoroutine(MorirYReaparecer(posicionInicial));
            return;
        }

        // se le avisa al servidor, que es quien cuenta la baja y manda a morir al duenio de este personaje
        if (atacante != null) atacante.ReportarImpactoRpc(NetworkObject);
        else MorirDuenioRpc(posicionInicial);
    }

    /// <summary>
    /// Daño de explosion (ej. Barreno), capaz de matar tanto a enemigos como al propio portador.
    /// </summary>
    public virtual void RecibirDañoExplosion(Personaje atacante)
    {
        if (muriendo) return;
        if (!IsSpawned)
        {
            StartCoroutine(MorirYReaparecer(posicionInicial));
            return;
        }

        if (atacante != null && atacante.IsSpawned)
        {
            atacante.ReportarImpactoExplosionRpc(NetworkObject);
        }
        else
        {
            MorirDuenioRpc(posicionInicial);
        }
    }

    // lo llama el que disparo, se ejecuta en el servidor
    [Rpc(SendTo.Server)]
    private void ReportarImpactoRpc(NetworkObjectReference referenciaVictima)
    {
        if (!referenciaVictima.TryGet(out NetworkObject objetoVictima)) return;
        Personaje victima = objetoVictima.GetComponent<Personaje>();
        if (victima == null || victima == this) return;

        bajas.Value++; //Se le da un punto al que lo mato

        // el servidor elige en que otro spawn reaparece la victima
        GameManager gm = FindFirstObjectByType<GameManager>();
        Vector3 destino = gm != null ? gm.SpawnAleatorio(victima.transform.position) : victima.posicionInicial;
        victima.MorirDuenioRpc(destino);
    }

    [Rpc(SendTo.Server)]
    private void ReportarImpactoExplosionRpc(NetworkObjectReference referenciaVictima)
    {
        if (!referenciaVictima.TryGet(out NetworkObject objetoVictima)) return;
        Personaje victima = objetoVictima.GetComponent<Personaje>();
        if (victima == null) return;

        if (victima != this)
        {
            bajas.Value++;
        }

        GameManager gm = FindFirstObjectByType<GameManager>();
        Vector3 destino = gm != null ? gm.SpawnAleatorio(victima.transform.position) : victima.posicionInicial;
        victima.MorirDuenioRpc(destino);
    }

    public void RecibirEmpujeEscudo(Vector3 impulso)
    {
        if (!IsSpawned || IsOwner)
        {
            AplicarEmpujeLocal(impulso);
        }
        else
        {
            AplicarEmpujeOwnerRpc(impulso);
        }
    }

    [Rpc(SendTo.Owner)]
    private void AplicarEmpujeOwnerRpc(Vector3 impulso)
    {
        AplicarEmpujeLocal(impulso);
    }

    private void AplicarEmpujeLocal(Vector3 impulso)
    {
        velocidadEmpuje = new Vector3(impulso.x, 0f, 0f);
        if (impulso.y > 0f)
        {
            velocidadCaida.y = Mathf.Max(velocidadCaida.y, impulso.y);
        }
    }

    /// <summary>
    /// Retroceso fisico hacia atras al disparar armas pesadas o automaticas (AK-47, Barret, Antitanques).
    /// </summary>
    public void RecibirRetrocesoDisparo(float fuerzaHorizontal, float fuerzaVertical)
    {
        if (IsSpawned && !IsOwner) return;
        float dirAtras = -DireccionHorizontal;
        velocidadEmpuje.x = Mathf.Clamp(velocidadEmpuje.x + dirAtras * fuerzaHorizontal, -16f, 16f);
        if (fuerzaVertical > 0f)
        {
            if (controlador != null && controlador.isGrounded)
                velocidadCaida.y = Mathf.Max(velocidadCaida.y, fuerzaVertical);
            else
                velocidadCaida.y = Mathf.Clamp(velocidadCaida.y + fuerzaVertical * 0.45f, -30f, 12f);
        }
    }

    public void NotificarCambioMunicionRed(Arma arma)
    {
        if (arma == null || !IsSpawned || !IsOwner) return;
        SincronizarMunicionArmaRpc(arma.gameObject.name, arma.MunicionCruda, arma.DuracionBateriaActual);
    }

    [Rpc(SendTo.NotMe)]
    public void SincronizarMunicionArmaRpc(string nombreArma, int municion, float bateria)
    {
        if (GestorArmas.Instancia == null) return;
        Arma arma = GestorArmas.Instancia.BuscarArma(nombreArma);
        if (arma != null)
        {
            arma.EstablecerEstadoMunicion(municion, bateria);
        }
    }

    [Rpc(SendTo.Owner)]
    public void ReiniciarCargadoresNuevaPartidaRpc()
    {
        LimpiarCargadoresExtra();
    }

    // Pisar una cascara: explosion pequena que lo tira y lo deja como muñeco de plastilina (ragdoll) unos segundos.
    // No quita vida, no es mortal.
    private RagdollPlastilina ragdoll;
    public bool EnRagdoll => ragdoll != null && ragdoll.Activo;
    private const float DURACION_RAGDOLL = 3f;

    public void RecibirResbalonPlatano(float duracion)
    {
        RecibirResbalonPlatano(duracion, transform.position);
    }

    // origenCascara: donde estaba la cascara, de ahi sale la "explosion" que lo despide
    public void RecibirResbalonPlatano(float duracion, Vector3 origenCascara)
    {
        if (!IsSpawned || IsOwner) IniciarCaidaPlatano(origenCascara);
        else AplicarResbalonPlatanoOwnerRpc(origenCascara);
    }

    [Rpc(SendTo.Owner)]
    private void AplicarResbalonPlatanoOwnerRpc(Vector3 origenCascara)
    {
        IniciarCaidaPlatano(origenCascara);
    }

    private void IniciarCaidaPlatano(Vector3 origenCascara)
    {
        if (muriendo || EnRagdoll) return; // ya esta en el suelo o muriendo

        // sale despedido lejos de la cascara y hacia arriba; si estaba justo encima, hacia atras de donde mira
        float lado = transform.position.x - origenCascara.x;
        float dirX = Mathf.Abs(lado) > 0.05f ? Mathf.Sign(lado) : -DireccionHorizontal;
        Vector3 direccion = new Vector3(dirX * 0.6f, 1f, 0f);

        IniciarRagdoll(direccion, true);
        if (IsSpawned) IniciarRagdollVisualRpc(direccion);
    }

    // los demas jugadores ven el ragdoll en su pantalla
    [Rpc(SendTo.NotOwner)]
    private void IniciarRagdollVisualRpc(Vector3 direccion)
    {
        IniciarRagdoll(direccion, false);
    }

    private void IniciarRagdoll(Vector3 direccion, bool esDuenio)
    {
        if (ragdoll == null) ragdoll = gameObject.AddComponent<RagdollPlastilina>();
        RefrescarAnimator();
        ragdoll.Iniciar(this, animator, controlador, direccion, DURACION_RAGDOLL, esDuenio);
    }

    // lo llama el ragdoll al acabar: el duenio se levanta donde cayo, los demas solo recuperan el CharacterController
    public void AlTerminarRagdoll(Vector3 destino, bool esDuenioYLevantarse)
    {
        if (esDuenioYLevantarse)
        {
            Teletransportar(destino);
            ultimaPosicionSegura = destino;
        }
        else if (controlador != null)
        {
            controlador.enabled = true;
        }
    }

    [Rpc(SendTo.Everyone)]
    public void LanzarMacheteRpc(string nombreArma, Vector3 origen, float dirX)
    {
        if (GestorArmas.Instancia == null) return;
        Arma a = GestorArmas.Instancia.BuscarArma(nombreArma);
        if (a is ArmaMachete machete)
        {
            bool autoritativo = !IsSpawned || IsOwner;
            machete.EjecutarVueloMachete(this, origen, dirX, autoritativo);
        }
    }

    [Rpc(SendTo.Everyone)]
    public void LanzarBarrenoRpc(Vector3 origen, Vector3 velocidadInicial)
    {
        bool autoritativo = !IsSpawned || IsOwner;
        ArmaBarreno.InstanciarBarrenoLanzado(this, origen, velocidadInicial, autoritativo);
    }

    [Rpc(SendTo.NotMe)]
    public void DetonarBarrenoEnPosicionRpc(Vector3 centro, float radio)
    {
        ArmaBarreno.EjecutarExplosionArea(centro, radio, this, false);
    }

    [Rpc(SendTo.Everyone)]
    public void LanzarCascaraPlatanoRpc(Vector3 origen, Vector3 velocidadInicial, float duracionResbalon)
    {
        ArmaPlatanos.InstanciarCascaraEnEscena(this, origen, velocidadInicial, duracionResbalon);
    }

    [Rpc(SendTo.NotMe)]
    public void ActualizarEscudoEmpunadoRpc(bool empunado)
    {
        if (armaEquipada is ArmaEscudo escudo)
        {
            escudo.EstablecerEmpunadoRemoto(empunado);
        }
    }

    // el trazo del disparo para que lo vean los demas jugadores
    [Rpc(SendTo.NotMe)]
    public void MostrarDisparoRpc(Vector3 inicio, Vector3 fin)
    {
        if (armaEquipada != null) armaEquipada.MostrarDisparo(inicio, fin);
    }

    // Sincronizacion del rayo continuo (Raygun) para que los demas jugadores vean el haz activo
    [Rpc(SendTo.NotMe)]
    public void ActualizarEstadoRayoRpc(bool activo, Vector3 inicio, Vector3 fin, float ancho)
    {
        if (armaEquipada != null) armaEquipada.ActualizarRayoRemoto(activo, inicio, fin, ancho);
    }

    // Sincronizacion del sonido de carga de la Raygun para los demas jugadores
    [Rpc(SendTo.NotMe)]
    public void ActualizarCargaRayoRpc(bool cargando)
    {
        if (armaEquipada != null) armaEquipada.ActualizarCargaRayoRemoto(cargando);
    }

    [Rpc(SendTo.Owner)]
    private void MorirDuenioRpc(Vector3 destino)
    {
        if (muriendo) return;
        StartCoroutine(MorirYReaparecer(destino));
    }

    // Solo para que los demas vean la animacion de desaparicion
    [Rpc(SendTo.NotOwner)]
    private void AnimarMuerteRpc()
    {
        StartCoroutine(AnimacionMuerte());
    }

    // El modelo se rompe en cubitos de colores que salen disparados, caen con fisica y se encogen; despues reaparece
    private IEnumerator AnimacionMuerte()
    {
        Transform modelo = (animator != null && animator.transform != transform) ? animator.transform : transform;

        // renderizadores del cuerpo (sin el arma ni efectos)
        List<Renderer> cuerpo = new List<Renderer>();
        List<Renderer> etiquetas = new List<Renderer>();
        List<Material> materiales = new List<Material>();
        foreach (Renderer r in modelo.GetComponentsInChildren<Renderer>(false))
        {
            if (!(r is MeshRenderer) && !(r is SkinnedMeshRenderer)) continue;
            if (r.GetComponentInParent<Arma>() != null) continue;
            if (!r.enabled) continue;
            if (r.GetComponent<TMPro.TMP_Text>() != null)
            {
                etiquetas.Add(r);
                continue;
            }
            cuerpo.Add(r);
            foreach (Material m in r.sharedMaterials) if (m != null) materiales.Add(m);
        }

        Bounds area = new Bounds(transform.position + Vector3.up, Vector3.one * 0.5f);
        bool primero = true;
        foreach (Renderer r in cuerpo) { if (primero) { area = r.bounds; primero = false; } else area.Encapsulate(r.bounds); }

        foreach (Renderer r in cuerpo) r.enabled = false; // el modelo desaparece y en su lugar quedan los cubos
        foreach (Renderer r in etiquetas) if (r != null) r.enabled = false;

        List<GameObject> cubos = new List<GameObject>();
        for (int i = 0; i < CUBOS_MUERTE; i++)
        {
            GameObject cubo = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cubo.name = "CuboMuerte";
            float lado = Random.Range(0.12f, 0.26f);
            cubo.transform.localScale = Vector3.one * lado;
            cubo.transform.position = new Vector3(
                Random.Range(area.min.x, area.max.x),
                Random.Range(area.min.y, area.max.y),
                Mathf.Lerp(area.min.z, area.max.z, 0.5f) + Random.Range(-0.1f, 0.1f));
            cubo.transform.rotation = Random.rotation;

            MeshRenderer mr = cubo.GetComponent<MeshRenderer>();
            if (materiales.Count > 0) mr.sharedMaterial = materiales[Random.Range(0, materiales.Count)];
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            Collider col = cubo.GetComponent<Collider>();
            if (controlador != null) Physics.IgnoreCollision(col, controlador); // no empujan al jugador que muere

            Rigidbody rb = cubo.AddComponent<Rigidbody>();
            rb.mass = 0.2f;
            Vector3 hacia = (cubo.transform.position - area.center);
            hacia.z *= 0.2f; // se mantienen casi en el plano de juego
            rb.linearVelocity = hacia.normalized * Random.Range(1.5f, 3.5f) + Vector3.up * Random.Range(2f, 4.5f);
            rb.angularVelocity = Random.insideUnitSphere * 8f;
            cubos.Add(cubo);
        }

        float t = 0f;
        float inicioEncoger = DURACION_MUERTE * 0.6f;
        while (t < DURACION_MUERTE)
        {
            t += Time.deltaTime;
            if (t > inicioEncoger)
            {
                float k = Mathf.InverseLerp(inicioEncoger, DURACION_MUERTE, t);
                foreach (GameObject c in cubos) if (c != null) c.transform.localScale = c.transform.localScale * (1f - k * 0.15f);
            }
            yield return null;
        }
        foreach (GameObject c in cubos) if (c != null) Destroy(c);

        yield return new WaitForSeconds(0.25f); // sin cuerpo un momento antes de reaparecer
        foreach (Renderer r in cuerpo) if (r != null) r.enabled = true;
        foreach (Renderer r in etiquetas) if (r != null) r.enabled = true;
        modelo.localRotation = Quaternion.identity;
    }

    private IEnumerator MorirYReaparecer(Vector3 destino)
    {
        muriendo = true;
        if (EnRagdoll) ragdoll.Terminar(false); // si moria en pleno ragdoll, primero se arma el cuerpo de nuevo
        velocidadCaida = Vector3.zero;
        velocidadEmpuje = Vector3.zero;
        LimpiarCargadoresExtra();

        // Soltar el arma justo donde muere, antes de desaparecer
        if (armaEquipada != null) armaEquipada.SoltarVoluntariamente();
        armaEquipada = null;

        if (IsSpawned) AnimarMuerteRpc();
        yield return AnimacionMuerte();

        // reaparece en otro spawn
        Quaternion rotacionSpawn = Quaternion.Euler(0f, direccionPersonaje > 0f ? 90f : -90f, 0f);
        transform.rotation = rotacionSpawn;
        Teletransportar(destino);
        FijarSpawn(destino);
        muriendo = false;
    }

    public virtual void morir(Personaje atacante)
    {
        if (atacante != null) atacante.muertes++;
        if (!muriendo) StartCoroutine(MorirYReaparecer(posicionInicial));
    }
    // Si el jugador cae fuera del mapa vuelve al ultimo suelo que pisó
    private void RecuperarDeCaida()
    {
        if (controlador.isGrounded) ultimaPosicionSegura = transform.position;
        if (transform.position.y < ultimaPosicionSegura.y - DISTANCIA_CAIDA) Teletransportar(ultimaPosicionSegura);
    }

    // Coloca el spawn sobre el piso: si quedo dentro de un objeto lo sube hasta un hueco libre,
    // y si quedo flotando lo baja hasta la primera superficie solida (hasta 40 m abajo)
    private void AjustarSpawnAlSuelo()
    {
        Vector3 origen = transform.position;
        Vector3 p = origen;

        // 1) Si esta metido dentro de algo (banca, piedras...), subir hasta un lugar libre
        int intentos = 0;
        while (intentos < 40 && CapsulaOcupada(p))
        {
            p += Vector3.up * 0.25f;
            intentos++;
        }

        // 2) Bajar hasta la primera superficie solida que no sea un jugador ni un arma
        RaycastHit[] golpes = Physics.RaycastAll(p + Vector3.up * 0.2f, Vector3.down, 40f, ~0, QueryTriggerInteraction.Ignore);
        float mejor = float.MaxValue;
        Vector3 punto = p;
        bool hayPiso = false;
        foreach (RaycastHit h in golpes)
        {
            if (h.collider == null) continue;
            if (h.collider.GetComponentInParent<Personaje>() != null) continue;
            if (h.collider.GetComponentInParent<Arma>() != null) continue;
            if (h.distance < mejor) { mejor = h.distance; punto = h.point; hayPiso = true; }
        }

        if (!hayPiso)
        {
            Debug.LogWarning("El spawn " + origen + " no tiene ningun piso debajo");
            return;
        }

        Vector3 destino = new Vector3(origen.x, punto.y + 0.1f, origen.z);
        if ((destino - origen).sqrMagnitude < 0.0004f) return;

        Debug.Log("Spawn ajustado al piso: " + origen + " -> " + destino);
        posicionInicial = destino;
        ultimaPosicionSegura = destino;
        Teletransportar(destino);
    }

    // true si la capsula del jugador, puesta en la posicion p, choca con algo solido
    private bool CapsulaOcupada(Vector3 p)
    {
        float escala = Mathf.Max(Mathf.Abs(transform.lossyScale.x), Mathf.Abs(transform.lossyScale.y));
        float radio = Mathf.Max(0.05f, controlador.radius * escala * 0.9f);
        float alto = Mathf.Max(radio * 2f, controlador.height * escala);
        Vector3 centro = p + transform.rotation * Vector3.Scale(controlador.center, transform.lossyScale);
        Vector3 desp = Vector3.up * (alto * 0.5f - radio);
        Collider[] choques = Physics.OverlapCapsule(centro - desp, centro + desp, radio, ~0, QueryTriggerInteraction.Ignore);
        foreach (Collider c in choques)
        {
            if (c == null) continue;
            if (c.GetComponentInParent<Personaje>() != null) continue;
            if (c.GetComponentInParent<Arma>() != null) continue;
            return true;
        }
        return false;
    }

    // Mueve al jugador sin soltar el arma ni contar muerte
    protected void Teletransportar(Vector3 destino)
    {
        controlador.enabled = false;
        transform.position = destino;
        var nt = GetComponent<Unity.Netcode.Components.NetworkTransform>();
        if (nt != null) nt.Teleport(destino, transform.rotation, transform.localScale);
        velocidadCaida = Vector3.zero;
        controlador.enabled = true;
    }

    public void FijarSpawn(Vector3 pos)
    {
        posicionInicial = pos;
        ultimaPosicionSegura = pos;
        revisarSpawnPendiente = true;
        revisarSpawnEn = -1f;
        velocidadCaida = Vector3.zero; //Si iba cayendo en el mapa anterior, que no llegue con esa velocidad al nuevo
    }
}
