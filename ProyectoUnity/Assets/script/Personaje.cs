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
    public bool EnSuelo        => ObtenerEnSueloEstable();
    public bool EstaAgachado   => estaAgachado;
    public float MovimientoAnim        => movimientoSuavizado;
    public float VelocidadCarreraAnim  => factorVelocidadAnim;
    public bool EstaCorriendoAnim      => !estaDeslizando && !muriendo && !EnRagdoll && !revisarSpawnPendiente && movimientoSuavizado > 0.1f;
    public int ContadorSaltosAnim      => contadorSaltosLocal;
    public bool EsperandoControlAnim   => muriendo || EnRagdoll || revisarSpawnPendiente || !TieneModeloVisible();
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

    // Identidad del personaje y pasivas (0: Cheto, 1: Kaiser, 2: Dorito, 3: CR7)
    private Red redCache;
    private float tiempoFinInmunidadEsquive = 0f;
    private float proximaVozManual = 0f;
    private float proximaVozDano = 0f;
    public int IndicePersonaje
    {
        get
        {
            if (redCache == null) redCache = GetComponent<Red>();
            return (redCache != null && redCache.IndicePersonajeActual >= 0) ? redCache.IndicePersonajeActual : 0;
        }
    }
    public bool EsCheto  => IndicePersonaje == 0;
    public bool EsKaiser => IndicePersonaje == 1;
    public bool EsDorito => IndicePersonaje == 2;
    public bool EsCR7    => IndicePersonaje == 3;
    public float MultiplicadorLanzamiento => EsKaiser ? 1.40f : 1.0f;
    public bool EnVentanaEsquiveDorito => Time.time < tiempoFinInmunidadEsquive;

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
    private const float DURACION_INMUNIDAD_SPAWN = 3.0f; // inmunidad temporal al reaparecer (se cancela al disparar/atacar)
    private const int CUBOS_MUERTE = 22;       // cantidad de cubitos en que se rompe el modelo
    private const float DISTANCIA_CAIDA = 12f; // cuanto puede bajar del ultimo suelo pisado antes de considerarse caido del mapa
    private GameManager gameManagerCache;
    private float tiempoUltimoRescateMapa = -10f;
    private bool rescateFueraMapaPendiente = false;

    public bool EstaMuriendo => muriendo;

    // Inmunidad temporal tras reaparecer: el duenio la controla, todos la ven (aura protectora) y el servidor la valida
    public NetworkVariable<bool> inmune = new NetworkVariable<bool>(false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
    private bool inmuneLocal = false;
    private float tiempoFinInmunidadLocal = 0f;
    private Coroutine corrutinaInmunidad;
    private GameObject auraInmunidadObj;
    private Material auraInmunidadMat;
    private Vector3 escalaBaseAura = Vector3.one * 2.1f;

    public bool EstaInmune => IsSpawned ? inmune.Value : inmuneLocal;
    public float TiempoRestanteInmunidad => EstaInmune ? Mathf.Max(0f, tiempoFinInmunidadLocal - Time.time) : 0f;

    // bajas (kills) del jugador, solo el servidor las escribe y todos las leen para el marcador
    public NetworkVariable<int> bajas = new NetworkVariable<int>(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    // estado de animacion: el duenio lo escribe y todos lo aplican a su Animator, asi los demas ven las animaciones
    private NetworkVariable<float> animMovimiento  = new NetworkVariable<float>(0f,    NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
    private NetworkVariable<bool>  animEnSuelo      = new NetworkVariable<bool>(true,  NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
    private NetworkVariable<bool>  animSaltar       = new NetworkVariable<bool>(false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
    private NetworkVariable<bool>  animDeslizando   = new NetworkVariable<bool>(false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
    private NetworkVariable<bool>  animAgachado     = new NetworkVariable<bool>(false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
    private NetworkVariable<bool>  animCorriendo    = new NetworkVariable<bool>(false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
    private NetworkVariable<int>   animContadorSaltos   = new NetworkVariable<int>(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
    private NetworkVariable<float> animVelocidadCarrera = new NetworkVariable<float>(1f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);

    protected Animator animator;
    // nombre en minusculas -> hash del parametro real, para que "saltar" encuentre "Saltar" y "estaEnSuelo" encuentre "EstaEnSuelo"
    private Dictionary<string, int> parametrosAnimator = new Dictionary<string, int>();
    // para detectar el flanco ascendente de animSaltar (Trigger, no Bool en el controller nuevo)
    private bool prevAnimSaltar = false;
    private int contadorSaltosLocal = 0;
    private int prevContadorSaltosCliente = 0;
    private float tiempoUltimoSuelo = -10f;
    private float tiempoBloqueoSueloSalto = -10f;
    private float velocidadHorizontalX = 0f;
    private float movimientoSuavizado = 0f;
    private float factorVelocidadAnim = 1f;
    private float tiempoRestanteDeslizar = 0f;
    private float dirDeslizarX = 1f;
    private HashSet<Personaje> embestidosCR7EnDeslice;

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

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();
        prevContadorSaltosCliente = animContadorSaltos.Value;
        prevAnimSaltar = animSaltar.Value;
        inmune.OnValueChanged += AlCambiarInmunidad;
        AplicarVisualInmunidad(inmune.Value);
    }

    public override void OnNetworkDespawn()
    {
        inmune.OnValueChanged -= AlCambiarInmunidad;
        DesactivarInmunidadSpawn();
        base.OnNetworkDespawn();
    }

    private void AlCambiarInmunidad(bool viejo, bool nuevo)
    {
        AplicarVisualInmunidad(nuevo);
    }

    private void AsegurarAuraInmunidad()
    {
        if (auraInmunidadObj != null) return;

        Transform existente = transform.Find("AuraInmunidad");
        if (existente != null)
        {
            auraInmunidadObj = existente.gameObject;
            MeshRenderer mrExistente = auraInmunidadObj.GetComponent<MeshRenderer>();
            if (mrExistente != null) auraInmunidadMat = mrExistente.material;
            return;
        }

        auraInmunidadObj = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        auraInmunidadObj.name = "AuraInmunidad";
        auraInmunidadObj.transform.SetParent(transform, false);

        Collider colAura = auraInmunidadObj.GetComponent<Collider>();
        if (colAura != null) Destroy(colAura);

        Vector3 centroLocal = controlador != null ? controlador.center : new Vector3(0f, 0.9f, 0f);
        float altoLocal = controlador != null ? Mathf.Max(1.0f, controlador.height) : 1.8f;
        auraInmunidadObj.transform.localPosition = centroLocal;
        float diametro = altoLocal * 1.25f;
        escalaBaseAura = new Vector3(diametro, diametro, diametro);
        auraInmunidadObj.transform.localScale = escalaBaseAura;

        MeshRenderer mr = auraInmunidadObj.GetComponent<MeshRenderer>();
        if (mr != null)
        {
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;
            Shader sh = Shader.Find("Sprites/Default");
            if (sh == null) sh = Shader.Find("Universal Render Pipeline/Unlit");
            auraInmunidadMat = new Material(sh);
            auraInmunidadMat.color = new Color(0.32f, 0.92f, 1.0f, 0.32f);
            mr.material = auraInmunidadMat;
        }

        auraInmunidadObj.SetActive(false);
    }

    private void AplicarVisualInmunidad(bool activo)
    {
        if (!activo && auraInmunidadObj == null) return;
        AsegurarAuraInmunidad();
        if (auraInmunidadObj != null) auraInmunidadObj.SetActive(activo);
    }

    private void ActualizarPulsoAuraInmunidad()
    {
        if (!EstaInmune)
        {
            if (auraInmunidadObj != null && auraInmunidadObj.activeSelf)
                auraInmunidadObj.SetActive(false);
            return;
        }

        AsegurarAuraInmunidad();
        if (auraInmunidadObj == null) return;
        if (!auraInmunidadObj.activeSelf) auraInmunidadObj.SetActive(true);

        float pulso = 0.5f + 0.5f * Mathf.Sin(Time.time * 12f);
        auraInmunidadObj.transform.localScale = escalaBaseAura * Mathf.Lerp(0.95f, 1.08f, pulso);

        if (auraInmunidadMat != null)
        {
            float restante = TiempoRestanteInmunidad;
            float alpha = restante > 0f && restante < 1.0f && (!IsSpawned || IsOwner)
                ? Mathf.Lerp(0.14f, 0.45f, 0.5f + 0.5f * Mathf.Sin(Time.time * 24f))
                : Mathf.Lerp(0.22f, 0.42f, pulso);
            auraInmunidadMat.color = new Color(0.32f, 0.92f, 1.0f, alpha);
        }
    }

    public void ActivarInmunidadSpawn(float duracion = DURACION_INMUNIDAD_SPAWN)
    {
        if (IsSpawned && !IsOwner) return;
        DesactivarInmunidadSpawn();
        corrutinaInmunidad = StartCoroutine(CorrutinaInmunidadSpawn(duracion));
    }

    public void DesactivarInmunidadSpawn()
    {
        if (corrutinaInmunidad != null)
        {
            StopCoroutine(corrutinaInmunidad);
            corrutinaInmunidad = null;
        }
        tiempoFinInmunidadLocal = 0f;
        inmuneLocal = false;
        if (IsSpawned && IsOwner && inmune.Value)
        {
            inmune.Value = false;
        }
        AplicarVisualInmunidad(false);
    }

    public void CancelarInmunidadPorAtaque()
    {
        if (!EstaInmune) return;
        if (IsSpawned && !IsOwner) return;
        DesactivarInmunidadSpawn();
    }

    private IEnumerator CorrutinaInmunidadSpawn(float duracion)
    {
        inmuneLocal = true;
        tiempoFinInmunidadLocal = Time.time + duracion;
        if (IsSpawned && IsOwner) inmune.Value = true;
        AplicarVisualInmunidad(true);

        yield return new WaitForSeconds(duracion);

        inmuneLocal = false;
        tiempoFinInmunidadLocal = 0f;
        if (IsSpawned && IsOwner) inmune.Value = false;
        AplicarVisualInmunidad(false);
        corrutinaInmunidad = null;
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
            if (r.gameObject.name == "AuraInmunidad") continue;
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
        if (!TieneModeloVisible() || muriendo)
        {
            LimpiarEstadoMovimientoAnim();
            return;
        }

        if (!revisarSpawnPendiente && VerificarLimitesMapaEnFrame())
        {
            LimpiarEstadoMovimientoAnim();
            return;
        }

        if (EnRagdoll)
        {
            LimpiarEstadoMovimientoAnim();
            return;
        }

        if (revisarSpawnPendiente)
        {
            if (revisarSpawnEn < 0f) revisarSpawnEn = Time.time + 0.2f; // empieza a contar cuando el jugador ya esta activo
            if (Time.time < revisarSpawnEn)
            {
                LimpiarEstadoMovimientoAnim();
                return; // quieto mientras el mapa termina de activarse
            }
            revisarSpawnPendiente = false;
            AjustarSpawnAlSuelo();
        }

        //Agarra el movimiento y la gravedad
        Mover();
        GravedadJugador();
        ActualizarRegistroSuelo();
        RecuperarDeCaida();
    
        //teclas especiales para funciones
        if (!estaDeslizando)
        {
            if (PuedeSaltarAhora() && AjustesJuego.RecienPresionada(Accion.Saltar))
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

        // el salto termina cuando vuelve a tocar el suelo (con estabilizador contra micro-huecos del terreno)
        bool sueloAnim = ObtenerEnSueloEstable();
        animEnSuelo.Value = sueloAnim;
        if (sueloAnim && velocidadCaida.y <= 0f)
        {
            animSaltar.Value = false;
        }

        // Grito / voz del personaje con la tecla G
        if (Keyboard.current != null && Keyboard.current.gKey.wasPressedThisFrame)
        {
            SolicitarGritoAnimal();
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

        if (estaDeslizando)
        {
            ActualizarDeslizarEnFrame();
            return;
        }
        
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

        float velocidadActual = estaAgachado ? velicidadAgachado : velocidad; // agachado camina mas lento

        // Pasiva de Cheto (el gato naranja): corre 25% mas rapido
        if (EsCheto)
        {
            velocidadActual *= 1.25f;
        }

        // Ralentizacion por portar arma pesada (ej. Francotirador Antitanques)
        if (armaEquipada != null && !armaEquipada.EnCaja)
        {
            velocidadActual *= (1f - armaEquipada.FactorRalentizacion);
        }

        // Si piso una cascara de platano, no se puede mover bien durante unos segundos
        float dt = Mathf.Min(Time.deltaTime, 0.033f);
        if (resbalandoPlatano)
        {
            velocidadActual *= 0.22f;
        }

        bool enSueloParaControl = ObtenerEnSueloEstable();
        if (enSueloParaControl)
        {
            // En tierra conserva respuesta inmediata y nitida
            velocidadHorizontalX = movimiento * velocidadActual;
        }
        else
        {
            // En el aire conserva el impulso del despegue con correccion gradual en lugar de acelerar/frenar de golpe
            if (Mathf.Abs(movimiento) > 0.01f)
            {
                bool mismoSentido = Mathf.Abs(velocidadHorizontalX) < 0.05f || Mathf.Sign(movimiento) == Mathf.Sign(velocidadHorizontalX);
                float topeAire = velocidadActual * 0.65f;
                float objetivoAire = mismoSentido && Mathf.Abs(velocidadHorizontalX) > topeAire
                    ? Mathf.Sign(movimiento) * Mathf.Clamp(Mathf.Abs(velocidadHorizontalX), topeAire, velocidadActual)
                    : movimiento * topeAire;
                float tasaAire = mismoSentido ? 14f : 24f;
                velocidadHorizontalX = Mathf.MoveTowards(velocidadHorizontalX, objetivoAire, tasaAire * dt);
            }
            else
            {
                velocidadHorizontalX = Mathf.MoveTowards(velocidadHorizontalX, 0f, 3.8f * dt);
            }
        }

        float tambaleoX = resbalandoPlatano ? Mathf.Sin(Time.time * 18f) * (velocidadActual * 0.28f) : 0f;
        float desplazamientoX = (velocidadHorizontalX + tambaleoX) * dt;
        float xAntesMove = transform.position.x;
        controlador.Move(new Vector3(desplazamientoX, 0f, 0f)); //Esta es la instruccion para que se mueva, hacia la direccion dada, con la velocidad dada y en cierto numero de frames

        // Si choco frontalmente contra una pared y no avanzo, cancela la inercia contra ese muro
        if ((controlador.collisionFlags & CollisionFlags.Sides) != 0
            && Mathf.Abs(transform.position.x - xAntesMove) < Mathf.Abs(desplazamientoX) * 0.25f)
        {
            velocidadHorizontalX = 0f;
        }

        // Suavizado ligero (~50 ms) para absorber huecos de 1-2 frames al cambiar A<->D sin perder respuesta
        float velNorm = velocidad > 0.01f ? Mathf.Clamp01(Mathf.Abs(velocidadHorizontalX) / velocidad) : Mathf.Abs(movimiento);
        float objetivoMov = enSueloParaControl ? Mathf.Abs(movimiento) : velNorm;
        float tasaMov = objetivoMov > 0.1f ? 32f : 18f;
        movimientoSuavizado = Mathf.MoveTowards(movimientoSuavizado, objetivoMov, tasaMov * dt);

        float factorVel = velocidad > 0.01f ? Mathf.Clamp(velocidadActual / velocidad, 0.55f, 1.35f) : 1f;
        factorVelocidadAnim = Mathf.MoveTowards(factorVelocidadAnim, objetivoMov > 0.01f ? factorVel : 1f, 12f * dt);

        EscribirFloatRed(animMovimiento, movimientoSuavizado);
        animCorriendo.Value = (objetivoMov > 0.01f || movimientoSuavizado > 0.1f) && !estaDeslizando;
        EscribirFloatRed(animVelocidadCarrera, factorVelocidadAnim);

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
    private void ActualizarRegistroSuelo()
    {
        if (controlador != null && controlador.enabled && controlador.isGrounded && Time.time >= tiempoBloqueoSueloSalto)
        {
            tiempoUltimoSuelo = Time.time;
        }
    }

    private bool PuedeSaltarAhora()
    {
        if (controlador == null || !controlador.enabled) return false;
        if (controlador.isGrounded) return true;
        return velocidadCaida.y <= 0f && Time.time >= tiempoBloqueoSueloSalto && (Time.time - tiempoUltimoSuelo) <= 0.06f;
    }

    private bool ObtenerEnSueloEstable()
    {
        if (controlador == null || !controlador.enabled) return true;
        if (Time.time < tiempoBloqueoSueloSalto) return false;
        if (controlador.isGrounded) return true;
        return velocidadCaida.y <= 0.1f && (Time.time - tiempoUltimoSuelo) <= 0.09f;
    }

    private static void EscribirFloatRed(NetworkVariable<float> netVar, float valor, float umbral = 0.04f)
    {
        if (netVar == null) return;
        if (Mathf.Approximately(valor, 0f))
        {
            if (!Mathf.Approximately(netVar.Value, 0f)) netVar.Value = 0f;
            return;
        }
        if (Mathf.Abs(netVar.Value - valor) >= umbral)
        {
            netVar.Value = valor;
        }
    }

    private void LimpiarEstadoMovimientoAnim()
    {
        estaDeslizando = false;
        tiempoRestanteDeslizar = 0f;
        estaAgachado = false;
        velocidadHorizontalX = 0f;
        movimientoSuavizado = 0f;
        factorVelocidadAnim = 1f;

        if (!IsSpawned || IsOwner)
        {
            animDeslizando.Value = false;
            animAgachado.Value = false;
            animCorriendo.Value = false;
            EscribirFloatRed(animMovimiento, 0f, 0.005f);
            if (!Mathf.Approximately(animVelocidadCarrera.Value, 1f)) animVelocidadCarrera.Value = 1f;
            animEnSuelo.Value = true;
            animSaltar.Value = false;
        }
    }

    protected virtual void Saltar()
    {
        // Pasiva de Cheto (el gato naranja): salta 25% mas alto
        velocidadCaida.y = salto * (EsCheto ? 1.25f : 1.0f);
        tiempoUltimoSuelo = -10f;
        tiempoBloqueoSueloSalto = Time.time + 0.11f;
        contadorSaltosLocal++;
        animSaltar.Value = true;
        animContadorSaltos.Value = contadorSaltosLocal;
        animEnSuelo.Value = false;
    }
    //TERMINADO DESLIZARSE
    protected virtual void Deslizar(float movimiento)
    {
        if (estaDeslizando) return; // Para que no castee mucho la tecla de deslizarse
        estaDeslizando = true;
        dirDeslizarX = Mathf.Abs(movimiento) > 0.01f ? Mathf.Sign(movimiento) : (direccionPersonaje >= 0f ? 1f : -1f);
        tiempoRestanteDeslizar = duracionDeslizar;
        if (EsCR7)
        {
            if (embestidosCR7EnDeslice == null) embestidosCR7EnDeslice = new HashSet<Personaje>();
            else embestidosCR7EnDeslice.Clear();
        }
        movimientoSuavizado = 1f;
        factorVelocidadAnim = 1f;
        velocidadHorizontalX = dirDeslizarX * velocidadDeslizar;
        animDeslizando.Value = true;
        animMovimiento.Value = 1f;
        animCorriendo.Value = false;
        animVelocidadCarrera.Value = 1f;
    }

    private void ActualizarDeslizarEnFrame()
    {
        float dt = Mathf.Min(Time.deltaTime, 0.033f);
        Vector3 direccionDeslizar = new Vector3(dirDeslizarX, 0f, 0f);
        controlador.Move(direccionDeslizar * velocidadDeslizar * dt);

        // Pasiva de CR7 (la cabra): al deslizarse hacia un enemigo, lo embiste y lo tumba al suelo
        if (EsCR7 && embestidosCR7EnDeslice != null)
        {
            DetectarEmbestidaCR7EnDeslice(dirDeslizarX, embestidosCR7EnDeslice);
        }

        movimientoSuavizado = 1f;
        factorVelocidadAnim = 1f;
        velocidadHorizontalX = dirDeslizarX * velocidadDeslizar;
        animDeslizando.Value = true;
        animMovimiento.Value = 1f;
        animCorriendo.Value = false;
        animVelocidadCarrera.Value = 1f;

        tiempoRestanteDeslizar -= dt;
        if (tiempoRestanteDeslizar <= 0f)
        {
            estaDeslizando = false;
            tiempoRestanteDeslizar = 0f;
            velocidadHorizontalX = dirDeslizarX * (velocidad * 0.7f);
            animDeslizando.Value = false;

            if (AjustesJuego.Presionada(Accion.Deslizar))
            {
                Agachar(true);
            }
        }
    }

    private void DetectarEmbestidaCR7EnDeslice(float dirX, HashSet<Personaje> embestidos)
    {
        Vector3 centro = transform.position + new Vector3(dirX * 0.65f, 0.8f, 0f);
        Vector3 mitadCaja = new Vector3(0.6f, 0.75f, 1.15f);
        Collider[] hits = Physics.OverlapBox(centro, mitadCaja, Quaternion.identity, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);

        Vector3 origenOjos = transform.position + Vector3.up * 0.85f;
        foreach (Collider col in hits)
        {
            if (col == null) continue;
            Personaje rival = col.GetComponentInParent<Personaje>();
            if (rival == null || rival == this || embestidos.Contains(rival)) continue;
            if (rival.EstaMuriendo || rival.EstaInmune || rival.EnRagdoll) continue;

            float deltaX = (rival.transform.position.x - transform.position.x) * dirX;
            if (deltaX < -0.2f || deltaX > 1.45f) continue;

            // Verificar que no haya una pared solida entre CR7 y el rival
            Vector3 destinoRival = rival.transform.position + Vector3.up * 0.85f;
            if (HayMuroEntrePuntos(origenOjos, destinoRival)) continue;

            embestidos.Add(rival);
            CancelarInmunidadPorAtaque();
            Vector3 impulso = new Vector3(dirX * 14.0f, 4.0f, 0f);
            rival.RecibirEmbestidaCR7(this, impulso);
        }
    }

    public bool HayMuroEntrePuntos(Vector3 a, Vector3 b)
    {
        Vector3 dir = b - a;
        float dist = dir.magnitude;
        if (dist <= 0.05f) return false;
        RaycastHit[] impactos = Physics.RaycastAll(a, dir / dist, dist, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
        foreach (RaycastHit h in impactos)
        {
            if (h.collider == null || h.distance <= 0.04f) continue;
            if (h.collider.GetComponentInParent<Personaje>() != null) continue;
            if (h.collider.GetComponentInParent<Arma>() != null) continue;
            if (h.collider.GetComponentInParent<TrampaCascara>() != null) continue;
            return true;
        }
        return false;
    }

    public void RecibirEmbestidaCR7(Personaje atacante, Vector3 impulso)
    {
        if (muriendo || EstaInmune || EnRagdoll) return;

        Vector3 posEfecto = transform.position + Vector3.up * 1.25f;
        Sonidos.Reproducir("impacto", posEfecto, 1.0f, 0.9f);
        if (atacante != null)
        {
            SonidosPersonaje.ReproducirVoz(atacante.IndicePersonaje, false, atacante.transform.position, 0.9f);
        }
        EfectosDisparo.MostrarTextoComic("¡SIUUU!", posEfecto, new Color(1f, 0.82f, 0.18f));
        EfectosDisparo.MostrarDestelloImpacto(transform.position + Vector3.up * 0.9f, new Color(1f, 0.78f, 0.2f), 0.38f);
        if (IsSpawned) MostrarEfectoTextoRedRpc("¡SIUUU!", posEfecto, new Color(1f, 0.82f, 0.18f));

        if (atacante != null && (!atacante.IsSpawned || atacante.IsOwner))
        {
            HUD.RegistrarHitmarker(false);
        }

        RecibirEmpujeEscudo(impulso);
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
        ActualizarPulsoAuraInmunidad();
    }

    // Aplica el estado sincronizado al Animator local.
    // En el dueño, AnimadorPuente ya lo hace; aquí manejamos los parámetros que solo
    // Personaje conoce (por NetworkVariable) para que todos los observadores vean
    // las animaciones correctas.
    private void ActualizarAnimator()
    {
        if (animator == null || !IsSpawned) return;
        if (IsOwner) return; // el dueño usa AnimadorPuente que lo maneja con input directo
        if (!animator.isActiveAndEnabled || animator.runtimeAnimatorController == null) return;

        AsignarFloat("movimiento",    animMovimiento.Value);
        AsignarFloat("velocidadAnim", animVelocidadCarrera.Value);
        AsignarBool("EstaEnSuelo",    animEnSuelo.Value);
        AsignarBool("estaDeslizando", animDeslizando.Value);
        AsignarBool("estaAgachado",   animAgachado.Value);
        AsignarBool("estaCorriendo",  animCorriendo.Value);

        // Disparar salto por contador monotónico (nunca pierde saltos rápidos entre ticks de red)
        bool saltoPorContador = animContadorSaltos.Value != prevContadorSaltosCliente;
        bool saltoPorBool = animSaltar.Value && !prevAnimSaltar;
        if (saltoPorContador || saltoPorBool)
        {
            if (parametrosAnimator.TryGetValue("saltar", out int hashSaltar))
            {
                animator.ResetTrigger(hashSaltar);
                animator.SetTrigger(hashSaltar);
            }
        }
        prevContadorSaltosCliente = animContadorSaltos.Value;
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
    /// 1) Si el personaje porta un Escudo Troyano que cubre la zona golpeada (los francos pesados tumban al portador).
    /// 2) Si el personaje es Dorito (Rata) y activa su pasiva de evasión letal (30% armado / 40% desarmado).
    /// 3) Si el personaje porta Barrenos activos (explotan al recibir un disparo).
    /// </summary>
    public virtual void RecibirImpactoDeDisparo(Personaje atacante, Vector3 puntoImpacto, Vector3 direccionDisparo, bool esCalibrePesado = false)
    {
        if (muriendo) return;
        if (EstaInmune || EnVentanaEsquiveDorito)
        {
            Sonidos.Reproducir("impacto", puntoImpacto, 0.55f, 1.55f);
            return;
        }

        // 1. Verificar bloqueo del Escudo Troyano
        if (armaEquipada is ArmaEscudo escudo && escudo.BloqueaImpacto(puntoImpacto, direccionDisparo))
        {
            if (esCalibrePesado)
            {
                // Bala calibre .50 (Barret / Antitanques): el escudo salva la vida pero la energia cinetica tumba al portador
                float dirGolpe = Mathf.Abs(direccionDisparo.x) > 0.01f
                    ? Mathf.Sign(direccionDisparo.x)
                    : (atacante != null ? atacante.DireccionHorizontal : 1f);
                Vector3 impulsoPesado = new Vector3(dirGolpe * 14.5f, 4.2f, 0f);
                Vector3 posTexto = puntoImpacto + Vector3.up * 0.35f;
                EfectosDisparo.MostrarTextoComic("¡GUARDIA ROTA!", posTexto, new Color(1f, 0.55f, 0.15f));
                if (IsSpawned) MostrarEfectoTextoRedRpc("¡GUARDIA ROTA!", posTexto, new Color(1f, 0.55f, 0.15f));
                RecibirEmpujeEscudo(impulsoPesado);
                if (atacante != null && (!atacante.IsSpawned || atacante.IsOwner))
                {
                    HUD.RegistrarHitmarker(false);
                }
            }
            Debug.Log($"[Escudo] Impacto bloqueado en {name}");
            return;
        }

        // 2. Pasiva de Dorito (la rata): probabilidad de esquivar un disparo letal (30% armado / 40% desarmado)
        if (IntentarEsquiveDorito(puntoImpacto))
        {
            return;
        }

        // 3. Verificar si porta Barrenos (si alguien le dispara al portador con los barrenos, este explotara)
        if (armaEquipada is ArmaBarreno barreno && barreno.PuedeExplotarAlRecibirDisparo())
        {
            Debug.Log($"[Barreno] {name} recibio disparo portando barrenos -> ¡Detonacion inmediata!");
            barreno.DetonarEnPortador(atacante);
            return;
        }

        RecibirDaño(atacante);
    }

    private bool IntentarEsquiveDorito(Vector3 puntoImpacto)
    {
        if (!EsDorito) return false;
        if (EnVentanaEsquiveDorito) return true;

        bool desarmado = armaEquipada == null || armaEquipada.EnCaja;
        float probEsquive = desarmado ? 0.40f : 0.30f;
        if (Random.value < probEsquive)
        {
            tiempoFinInmunidadEsquive = Time.time + 0.42f;
            EjecutarEfectoEsquiveLocal(puntoImpacto);
            if (IsSpawned) NotificarEsquiveDoritoRpc(puntoImpacto);
            return true;
        }
        return false;
    }

    private void EjecutarEfectoEsquiveLocal(Vector3 puntoImpacto)
    {
        Vector3 posTexto = transform.position + Vector3.up * 1.3f;
        Sonidos.Reproducir("deslizar", puntoImpacto, 0.95f, 1.65f);
        SonidosPersonaje.ReproducirVoz(IndicePersonaje, false, transform.position, 0.85f);
        EfectosDisparo.MostrarTextoComic("¡ESQUIVADO!", posTexto, new Color(1f, 0.92f, 0.22f));
        EfectosDisparo.MostrarDestelloImpacto(puntoImpacto, new Color(1f, 0.95f, 0.35f), 0.32f);
    }

    [Rpc(SendTo.NotMe)]
    private void NotificarEsquiveDoritoRpc(Vector3 puntoImpacto)
    {
        tiempoFinInmunidadEsquive = Time.time + 0.42f;
        EjecutarEfectoEsquiveLocal(puntoImpacto);
    }

    [Rpc(SendTo.NotMe)]
    private void MostrarEfectoTextoRedRpc(string texto, Vector3 pos, Color color)
    {
        EfectosDisparo.MostrarTextoComic(texto, pos, color);
    }

    public virtual void RecibirDaño(Personaje atacante)
    {
        if (muriendo || EstaInmune || EnVentanaEsquiveDorito) return;
        Debug.Log("Auch, me dio: "+atacante);

        // Si tambien tenia barrenos al recibir daño directo, detonar
        if (armaEquipada is ArmaBarreno barreno && barreno.PuedeExplotarAlRecibirDisparo())
        {
            barreno.DetonarEnPortador(atacante);
            return;
        }

        if (atacante != null && (!atacante.IsSpawned || atacante.IsOwner))
        {
            Red r = GetComponent<Red>();
            string etiquetaVictima = r != null ? r.ObtenerEtiquetaJugador() : name;
            HUD.RegistrarBajaLocal(etiquetaVictima);
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
        if (muriendo || EstaInmune) return;

        if (atacante != null && atacante != this && (!atacante.IsSpawned || atacante.IsOwner))
        {
            Red r = GetComponent<Red>();
            string etiquetaVictima = r != null ? r.ObtenerEtiquetaJugador() : name;
            HUD.RegistrarBajaLocal(etiquetaVictima);
        }

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
        if (victima == null || victima == this || victima.EstaInmune || victima.EstaMuriendo) return;

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
        if (victima == null || victima.EstaInmune || victima.EstaMuriendo) return;

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
        if (muriendo || EstaInmune) return;
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
        // el golpe del escudo lo tira como muñeco (ragdoll); otros empujes (retroceso de armas) solo lo desplazan
        if (impulso.sqrMagnitude > 0.01f && AplicarCaidaPorEscudo(impulso)) return;

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
        DesactivarInmunidadSpawn();
    }

    // Pisar una cascara: explosion pequena que lo tira y lo deja como muñeco de plastilina (ragdoll) unos segundos.
    // No quita vida, no es mortal.
    private RagdollPlastilina ragdoll;
    public bool EnRagdoll => ragdoll != null && ragdoll.Activo;
    public float Gravedad => gravedad; // el ragdoll cae con la misma gravedad que el personaje
    private const float DURACION_RAGDOLL = 2.5f; // maximo tirado; se levanta antes si ya quedo quieto

    public void RecibirResbalonPlatano(float duracion)
    {
        RecibirResbalonPlatano(duracion, transform.position);
    }

    // origenCascara: donde estaba la cascara que pisó
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
        if (muriendo || EstaInmune || EnRagdoll) return; // ya esta en el suelo, inmune o muriendo

        // resbala: sale despedido hacia donde caminaba (adelante y arriba) y cae de espaldas
        float dirX = DireccionHorizontal < 0f ? -1f : 1f;
        Vector3 direccion = new Vector3(dirX, 1f, dirX); // x: hacia donde sale despedido, z: hacia donde mira

        IniciarRagdoll(direccion, true);
        if (IsSpawned) IniciarRagdollVisualRpc(direccion);
    }

    // golpe de escudo: sale despedido en la direccion del golpe y cae como en el resbalon
    private bool AplicarCaidaPorEscudo(Vector3 impulso)
    {
        if (muriendo || EnRagdoll) return true; // ya esta en el suelo: no se acumula otro empuje
        float sentido = impulso.x < 0f ? -1f : 1f;
        float mira = DireccionHorizontal < 0f ? -1f : 1f;
        Vector3 direccion = new Vector3(sentido, 1f, mira);
        IniciarRagdoll(direccion, true);
        if (IsSpawned) IniciarRagdollVisualRpc(direccion);
        return true;
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
        EmitirVozDanoConCooldown(0.85f);
        ragdoll.Iniciar(this, animator, controlador, direccion, DURACION_RAGDOLL, esDuenio);
    }

    // lo llama el ragdoll al acabar: el duenio se levanta donde cayo, los demas solo recuperan el CharacterController
    public void AlTerminarRagdoll(Vector3 destino, bool esDuenioYLevantarse)
    {
        prevContadorSaltosCliente = animContadorSaltos.Value;
        prevAnimSaltar = animSaltar.Value;
        if (animator != null && animator.isActiveAndEnabled && animator.runtimeAnimatorController != null)
        {
            if (parametrosAnimator.TryGetValue("saltar", out int hashSaltar))
                animator.ResetTrigger(hashSaltar);
        }

        if (esDuenioYLevantarse)
        {
            if (!EsPosicionSeguraEnMapa(destino))
            {
                RescatarDeFueraDeMapa();
                return;
            }
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
        EmitirVozDanoConCooldown(0.9f);
        Transform modelo = (animator != null && animator.transform != transform) ? animator.transform : transform;

        // renderizadores del cuerpo (sin el arma ni efectos)
        List<Renderer> cuerpo = new List<Renderer>();
        List<Renderer> etiquetas = new List<Renderer>();
        List<Material> materiales = new List<Material>();
        foreach (Renderer r in modelo.GetComponentsInChildren<Renderer>(false))
        {
            if (!(r is MeshRenderer) && !(r is SkinnedMeshRenderer)) continue;
            if (r.gameObject.name == "AuraInmunidad") continue;
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
        LimpiarEstadoMovimientoAnim();
        DesactivarInmunidadSpawn();
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
        ActivarInmunidadSpawn(DURACION_INMUNIDAD_SPAWN);
    }

    public virtual void morir(Personaje atacante)
    {
        if (atacante != null) atacante.muertes++;
        if (!muriendo) StartCoroutine(MorirYReaparecer(posicionInicial));
    }
    private GameManager ObtenerGameManager()
    {
        if (gameManagerCache == null) gameManagerCache = FindAnyObjectByType<GameManager>();
        return gameManagerCache;
    }

    private static bool HaySueloDebajo(Vector3 pos, float distancia)
    {
        RaycastHit[] golpes = Physics.RaycastAll(pos + Vector3.up * 0.35f, Vector3.down, distancia + 0.35f, ~0, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < golpes.Length; i++)
        {
            Collider c = golpes[i].collider;
            if (c == null) continue;
            if (c.GetComponentInParent<Personaje>() != null) continue;
            if (c.GetComponentInParent<Arma>() != null) continue;
            return true;
        }
        return false;
    }

    private bool PosicionDentroDeLimites(Vector3 pos, Bounds lim, float planoZ)
    {
        const float margenX = 5f;
        const float margenAbajo = 4f;
        const float margenArriba = 22f;
        const float margenZ = 10f;

        if (pos.x < lim.min.x - margenX || pos.x > lim.max.x + margenX) return false;
        if (pos.y < lim.min.y - margenAbajo || pos.y > lim.max.y + margenArriba) return false;
        if (Mathf.Abs(pos.z - planoZ) > margenZ) return false;
        return true;
    }

    private bool EsPosicionSeguraEnMapa(Vector3 pos)
    {
        GameManager gm = ObtenerGameManager();
        if (gm != null && gm.ObtenerLimitesMapaActivo(out Bounds lim, out float planoZ))
        {
            if (!PosicionDentroDeLimites(pos, lim, planoZ)) return false;
        }
        return HaySueloDebajo(pos, 18f);
    }

    private bool VerificarLimitesMapaEnFrame()
    {
        if (rescateFueraMapaPendiente)
        {
            if (Time.time - tiempoUltimoRescateMapa >= 1.5f)
            {
                rescateFueraMapaPendiente = false;
                CompletarRescateEnSpawn(EsPosicionSeguraEnMapa(ultimaPosicionSegura) ? ultimaPosicionSegura : posicionInicial);
            }
            return true;
        }

        if (Time.time - tiempoUltimoRescateMapa < 0.35f) return false;

        Vector3 pos = (EnRagdoll && ragdoll.TieneCadera) ? ragdoll.PosicionCadera : transform.position;
        if (float.IsNaN(pos.x) || float.IsNaN(pos.y) || float.IsNaN(pos.z) ||
            float.IsInfinity(pos.x) || float.IsInfinity(pos.y) || float.IsInfinity(pos.z))
        {
            RescatarDeFueraDeMapa();
            return true;
        }

        GameManager gm = ObtenerGameManager();
        if (gm != null && gm.ObtenerLimitesMapaActivo(out Bounds limites, out float planoZ))
        {
            if (!PosicionDentroDeLimites(pos, limites, planoZ))
            {
                RescatarDeFueraDeMapa();
                return true;
            }

            if (pos.y < ultimaPosicionSegura.y - DISTANCIA_CAIDA && !HaySueloDebajo(pos, 22f))
            {
                RescatarDeFueraDeMapa();
                return true;
            }
        }
        else if (pos.y < ultimaPosicionSegura.y - DISTANCIA_CAIDA && !HaySueloDebajo(pos, 22f))
        {
            RescatarDeFueraDeMapa();
            return true;
        }

        return false;
    }

    private void RescatarDeFueraDeMapa()
    {
        if (muriendo || !gameObject.activeInHierarchy) return;
        if (rescateFueraMapaPendiente) return;

        rescateFueraMapaPendiente = true;
        tiempoUltimoRescateMapa = Time.time;

        if (EnRagdoll)
        {
            ragdoll.Terminar(false);
            if (IsSpawned) CancelarRagdollVisualRpc();
        }

        velocidadCaida = Vector3.zero;
        velocidadEmpuje = Vector3.zero;
        velocidadHorizontalX = 0f;
        estaDeslizando = false;
        tiempoRestanteDeslizar = 0f;

        ActivarInmunidadSpawn(1.2f);

        if (IsSpawned)
        {
            SolicitarRescateFueraDeMapaServerRpc();
        }
        else
        {
            GameManager gm = ObtenerGameManager();
            Vector3 destino = gm != null ? gm.SpawnAleatorio(transform.position) : ultimaPosicionSegura;
            if (!EsPosicionSeguraEnMapa(destino))
            {
                destino = EsPosicionSeguraEnMapa(posicionInicial) ? posicionInicial : ultimaPosicionSegura;
            }
            CompletarRescateEnSpawn(destino);
        }
    }

    [Rpc(SendTo.Server)]
    private void SolicitarRescateFueraDeMapaServerRpc()
    {
        GameManager gm = ObtenerGameManager();
        Vector3 destino = gm != null ? gm.SpawnAleatorio(transform.position) : ultimaPosicionSegura;
        if (!EsPosicionSeguraEnMapa(destino))
        {
            destino = EsPosicionSeguraEnMapa(posicionInicial) ? posicionInicial : ultimaPosicionSegura;
        }

        if (redCache == null) redCache = GetComponent<Red>();
        if (redCache != null && IsSpawned)
        {
            redCache.RescatarEnSpawnDuenioRpc(destino);
        }
        else
        {
            CompletarRescateEnSpawn(destino);
        }
    }

    [Rpc(SendTo.NotOwner)]
    private void CancelarRagdollVisualRpc()
    {
        if (EnRagdoll) ragdoll.Terminar(false);
    }

    public void CompletarRescateEnSpawn(Vector3 destino)
    {
        if (IsSpawned && !IsOwner) return;

        rescateFueraMapaPendiente = false;
        tiempoUltimoRescateMapa = Time.time;

        if (EnRagdoll)
        {
            ragdoll.Terminar(false);
            if (IsSpawned) CancelarRagdollVisualRpc();
        }

        if (!EsPosicionSeguraEnMapa(destino))
        {
            destino = EsPosicionSeguraEnMapa(ultimaPosicionSegura) ? ultimaPosicionSegura : posicionInicial;
        }

        Teletransportar(destino);
        ultimaPosicionSegura = destino;
        velocidadCaida = Vector3.zero;
        velocidadEmpuje = Vector3.zero;
        velocidadHorizontalX = 0f;
        estaDeslizando = false;
        tiempoRestanteDeslizar = 0f;
        revisarSpawnPendiente = false;
        LimpiarEstadoMovimientoAnim();
        AjustarSpawnAlSuelo();
        Debug.Log("[Personaje] Rescatado fuera de limites hacia spawn seguro: " + transform.position);
    }

    // Si el jugador cae fuera del mapa vuelve a un spawn valido sin contar muerte ni soltar su arma
    private void RecuperarDeCaida()
    {
        if (controlador.isGrounded && EsPosicionSeguraEnMapa(transform.position))
        {
            ultimaPosicionSegura = transform.position;
        }
        if (transform.position.y < ultimaPosicionSegura.y - DISTANCIA_CAIDA)
        {
            if (!HaySueloDebajo(transform.position, 22f))
            {
                RescatarDeFueraDeMapa();
            }
            else
            {
                Teletransportar(ultimaPosicionSegura);
            }
        }
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

    private void SolicitarGritoAnimal()
    {
        if (Time.time < proximaVozManual) return;
        proximaVozManual = Time.time + 0.9f;
        int variante = Random.Range(1, 3);
        SonidosPersonaje.ReproducirVoz(IndicePersonaje, false, transform.position, 0.95f, variante);
        if (IsSpawned) EmitirVozAnimalRpc(variante);
    }

    [Rpc(SendTo.NotMe)]
    private void EmitirVozAnimalRpc(int variante)
    {
        SonidosPersonaje.ReproducirVoz(IndicePersonaje, false, transform.position, 0.95f, variante);
    }

    private void EmitirVozDanoConCooldown(float volumen)
    {
        if (Time.time < proximaVozDano) return;
        proximaVozDano = Time.time + 1.4f;
        SonidosPersonaje.ReproducirVoz(IndicePersonaje, true, transform.position, volumen);
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
        velocidadHorizontalX = 0f;
        controlador.enabled = true;
    }

    public void FijarSpawn(Vector3 pos)
    {
        posicionInicial = pos;
        ultimaPosicionSegura = pos;
        revisarSpawnPendiente = true;
        revisarSpawnEn = -1f;
        velocidadCaida = Vector3.zero; //Si iba cayendo en el mapa anterior, que no llegue con esa velocidad al nuevo
        velocidadHorizontalX = 0f;
    }
}
