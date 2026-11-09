using UnityEngine;
using UnityEngine.InputSystem;

// Animacion de los brazos con arma, arma pegada a la mano del gato y efectos de disparo.
// - Con arma equipada se activa la capa "Arma" del Animator (solo torso y brazos: apuntar y disparar),
//   las piernas siguen con correr, saltar o deslizar.
// - El punto "Mano" (donde Arma.cs coloca el arma) se pega a la mano de la animacion,
//   asi el arma sube y baja con el cuerpo en vez de quedarse clavada en un punto fijo.
// - Al disparar: el arma se echa atras, el gato se "aplasta" un instante, y salen destello, chispas, sonido y camara.
// No cambia nada de Personaje ni de Arma: solo mueve "Mano", maneja la capa de animacion y reacciona al trazo del disparo.
[DefaultExecutionOrder(-50)] // antes que Arma.LateUpdate, que es quien lee la posicion de Mano
public class AnimadorArma : MonoBehaviour
{
    private const string NombreCapa = "Arma";
    private const float VelocidadCapa = 8f; // cuanto tarda en subir/bajar el rifle

    private Personaje personaje;
    private Animator animator;
    private Transform mano;
    private Transform huesoMano;
    private Transform modelo;
    private Vector3 escalaModelo;
    private int capaArma = -1;
    private float pesoCapa;

    private Vector3 manoOriginal;      // posicion de Mano en el prefab, donde cada arma ya esta ajustada
    // donde queda el arma respecto al hueso de la mano (espacio del jugador): bajar para centrarla en el pecho y
    // adelantar para que el cañon salga hacia donde mira el gato
    [SerializeField] private Vector3 ajusteAgarre = new Vector3(0f, -0.23f, 0.14f);

    // retroceso del arma al disparar (metros) y "aplastado" del gato, ambos se apagan solos
    private float retroceso;
    private float retrocesoMaximo = 0.07f;

    private LineRenderer trazoArma;
    private bool trazoAnterior;

    // para detectar recoger, romper la caja y recargar
    private Arma armaAnterior;
    private bool enCajaAnterior;
    private bool recargandoAnterior;

    private void Awake()
    {
        personaje = GetComponent<Personaje>();
        // estos componentes se agregan aqui para que el prefab del jugador no necesite otros a mano
        if (GetComponent<AnimadorDeslizar>() == null) gameObject.AddComponent<AnimadorDeslizar>();
        if (GetComponent<SonidosPersonaje>() == null) gameObject.AddComponent<SonidosPersonaje>();
        animator = GetComponent<Animator>();
        if (animator == null) animator = GetComponentInChildren<Animator>();

        mano = transform.Find("Mano");
        if (mano != null) manoOriginal = mano.localPosition;

        modelo = BuscarModeloInicial();
        if (modelo != null) escalaModelo = modelo.localScale;

        RefrescarHuesosAnimator();
    }

    private Transform BuscarModeloInicial()
    {
        Transform gato = transform.Find("Personaje gato");
        if (gato != null) return gato;

        for (int i = 0; i < transform.childCount; i++)
        {
            Transform h = transform.GetChild(i);
            if (!CatalogoPersonajes.EsObjetoAuxiliarJugador(h))
                return h;
        }
        return null;
    }

    public void ActualizarModeloPersonaje(Transform nuevoModelo)
    {
        modelo = nuevoModelo != null ? nuevoModelo : BuscarModeloInicial();
        if (modelo != null) escalaModelo = modelo.localScale;
        RefrescarHuesosAnimator();
    }

    private void RefrescarHuesosAnimator()
    {
        animator = GetComponent<Animator>();
        if (animator == null) animator = GetComponentInChildren<Animator>();

        huesoMano = null;
        if (animator != null)
        {
            capaArma = animator.GetLayerIndex(NombreCapa);
            if (animator.isHuman)
                huesoMano = animator.GetBoneTransform(HumanBodyBones.RightHand);
        }

        if (huesoMano == null && modelo != null)
        {
            foreach (Transform t in modelo.GetComponentsInChildren<Transform>(true))
            {
                if (t.name.EndsWith("RightHand", System.StringComparison.OrdinalIgnoreCase))
                {
                    huesoMano = t;
                    break;
                }
            }
        }
    }

    private bool EsLocal()
    {
        return personaje == null || !personaje.IsSpawned || personaje.IsOwner;
    }

    private void Update()
    {
        if (animator == null || capaArma < 0) return;

        Arma arma = personaje != null ? personaje.armaEquipada : null;
        bool apuntando = arma != null && !arma.EnCaja;

        pesoCapa = Mathf.MoveTowards(pesoCapa, apuntando ? 1f : 0f, VelocidadCapa * Time.deltaTime);
        animator.SetLayerWeight(capaArma, pesoCapa);

        SonidosDeArma(arma);

        // el trazo del disparo se prende en el dueño y en los demas jugadores, sirve de aviso para animar
        LineRenderer trazo = apuntando ? arma.GetComponent<LineRenderer>() : null;
        if (trazo != trazoArma) { trazoArma = trazo; trazoAnterior = trazo != null && trazo.enabled; }
        bool disparando = trazoArma != null && trazoArma.enabled;
        if (disparando && !trazoAnterior) AlDisparar(arma, trazoArma);
        trazoAnterior = disparando;
    }

    // recoger, romper la caja, recargar y "clic" sin municion
    private void SonidosDeArma(Arma arma)
    {
        Vector3 pos = transform.position;

        if (arma != armaAnterior)
        {
            if (arma != null) Sonidos.Reproducir("recoger", pos, 0.8f);
            armaAnterior = arma;
            enCajaAnterior = arma != null && arma.EnCaja;
            recargandoAnterior = arma != null && arma.Recargando;
            retroceso = 0f;
            retrocesoMaximo = arma != null ? EfectosDisparo.PerfilDe(arma.name).retroceso : 0.07f;
        }
        if (arma == null) return;

        if (enCajaAnterior && !arma.EnCaja) Sonidos.Reproducir("romper_caja", pos, 1f);
        enCajaAnterior = arma.EnCaja;

        if (!recargandoAnterior && arma.Recargando) Sonidos.Reproducir("recarga", pos, 0.8f);
        recargandoAnterior = arma.Recargando;

        if (EsLocal() && !arma.EnCaja && !arma.Recargando && arma.MunicionActual <= 0
            && Keyboard.current != null && Keyboard.current.vKey.wasPressedThisFrame)
        {
            Sonidos.Reproducir("sin_municion", pos, 0.8f);
        }
    }

    private void AlDisparar(Arma arma, LineRenderer trazo)
    {
        animator.SetTrigger("Disparar");

        retroceso = retrocesoMaximo;
        EfectosDisparo.Disparo(arma, trazo.GetPosition(0), trazo.GetPosition(1), EsLocal());
        EstilizarTrazo(trazo, arma);
    }

    // el trazo es una linea fina: se hace mas gruesa en la boca y se afina hacia el final, como una estela de comic
    private static void EstilizarTrazo(LineRenderer trazo, Arma arma)
    {
        if (arma != null && arma.Modo == ModoDisparo.RayoContinuo)
        {
            trazo.widthMultiplier = arma.AnchoRayo;
            trazo.widthCurve = AnimationCurve.Constant(0f, 1f, 1f);
            return;
        }

        trazo.widthMultiplier = 1f;
        trazo.widthCurve = new AnimationCurve(new Keyframe(0f, 0.09f), new Keyframe(0.15f, 0.05f), new Keyframe(1f, 0.012f));
    }

    private void LateUpdate()
    {
        AplastarModelo();

        if (mano == null) return;

        if (huesoMano == null)
        {
            mano.localPosition = manoOriginal;
            return;
        }

        // el retroceso baja solo, rapido al inicio
        retroceso = Mathf.Lerp(retroceso, 0f, 1f - Mathf.Exp(-16f * Time.deltaTime));

        // posicion de la mano en el espacio del jugador: no cambia cuando el gato gira a izquierda o derecha
        Vector3 local = transform.InverseTransformPoint(huesoMano.position);

        if (pesoCapa < 0.01f)
        {
            mano.localPosition = manoOriginal;
            return;
        }

        // con el rifle levantado el arma va pegada a la mano y se mueve con la animacion;
        // al disparar se va hacia atras (y un poco arriba, como si el cañon saltara)
        Vector3 destino = local + ajusteAgarre;
        mano.localPosition = Vector3.Lerp(manoOriginal, destino, Mathf.Clamp01(pesoCapa))
                             + new Vector3(0f, retroceso * 0.3f, -retroceso);
    }

    // "squash and stretch": el gato se achata y se ensancha un instante con cada disparo
    private void AplastarModelo()
    {
        if (modelo == null) return;
        float k = retrocesoMaximo > 0.0001f ? Mathf.Clamp01(retroceso / retrocesoMaximo) : 0f;
        modelo.localScale = Vector3.Scale(escalaModelo, new Vector3(1f + 0.06f * k, 1f - 0.07f * k, 1f + 0.06f * k));
    }
}
