using UnityEngine;

// Sonidos del gato: pasos al correr, salto, aterrizaje y deslizar.
// Lee los parametros del Animator (el dueño los escribe con sus teclas y los demas con los datos de red),
// asi todos los jugadores se escuchan igual. Los lejanos suenan mas bajito. No cambia nada de Personaje.
public class SonidosPersonaje : MonoBehaviour
{
    private static readonly int ParamMovimiento = Animator.StringToHash("movimiento");
    private static readonly int ParamSuelo = Animator.StringToHash("EstaEnSuelo");
    private static readonly int ParamDeslizando = Animator.StringToHash("estaDeslizando");

    private const float TiempoEntrePasos = 0.27f;
    private const float VelocidadSalto = 4f;   // m/s hacia arriba para contar como salto (subir una rampa es menos)
    private const float TiempoCaida = 0.15f;   // en el aire tanto tiempo cuenta como caida aunque no se haya saltado

    private static readonly string[] PrefijosAnimales = { "gato", "perro", "raton", "cabra" };

    private Animator animator;
    private Personaje personaje;
    private bool tieneParametros;
    private bool enAire;
    private bool deslizandoAntes;
    private float tiempoEnAire;
    private float proximoPaso;
    private float ultimaY;
    private int alternar;
    private float proximaVozSalto;

    /// <summary>
    /// Resuelve el prefijo del archivo de voz para el indice de personaje dado (0=gato, 1=perro, 2=raton, 3=cabra).
    /// </summary>
    public static string PrefijoVoz(int indicePersonaje)
    {
        if (indicePersonaje < 0 || indicePersonaje >= PrefijosAnimales.Length)
            return PrefijosAnimales[0];
        return PrefijosAnimales[indicePersonaje];
    }

    /// <summary>
    /// Obtiene el nombre del clip de voz animal segun variante (0=aleatorio 1/2, 1/2=especifica) o daño.
    /// </summary>
    public static string ClipVoz(int indicePersonaje, bool dano, int variante = 0)
    {
        string prefijo = PrefijoVoz(indicePersonaje);
        if (dano) return "voz_" + prefijo + "_dano";
        int idx = variante == 1 || variante == 2 ? variante : Random.Range(1, 3);
        return "voz_" + prefijo + "_" + idx;
    }

    public static void ReproducirVoz(int indicePersonaje, bool dano, Vector3 posicion, float volumen = 0.9f, int variante = 0)
    {
        string clip = ClipVoz(indicePersonaje, dano, variante);
        if (Sonidos.Existe(clip))
            Sonidos.Reproducir(clip, posicion, volumen);
    }

    public static void ReproducirVozLocal(int indicePersonaje, bool dano, float volumen = 0.85f, int variante = 0)
    {
        string clip = ClipVoz(indicePersonaje, dano, variante);
        if (Sonidos.Existe(clip))
            Sonidos.Reproducir(clip, volumen);
    }

    private void Awake()
    {
        personaje = GetComponent<Personaje>();
        if (personaje == null) personaje = GetComponentInParent<Personaje>();
        animator = GetComponent<Animator>();
        if (animator == null) animator = GetComponentInChildren<Animator>();
        if (animator == null) { enabled = false; return; }

        int encontrados = 0;
        foreach (AnimatorControllerParameter p in animator.parameters)
        {
            if (p.nameHash == ParamMovimiento || p.nameHash == ParamSuelo || p.nameHash == ParamDeslizando) encontrados++;
        }
        tieneParametros = encontrados == 3;
        ultimaY = transform.position.y;
    }

    private void Update()
    {
        if (!tieneParametros || animator == null || !animator.isActiveAndEnabled || animator.runtimeAnimatorController == null) return;

        float dt = Mathf.Max(Time.deltaTime, 0.0001f);
        float velocidadY = (transform.position.y - ultimaY) / dt;
        ultimaY = transform.position.y;

        bool suelo = animator.GetBool(ParamSuelo);
        bool deslizando = animator.GetBool(ParamDeslizando);
        float movimiento = animator.GetFloat(ParamMovimiento);
        Vector3 pos = transform.position;

        // salto, caida y aterrizaje
        if (!suelo)
        {
            tiempoEnAire += dt;
            if (!enAire && (velocidadY > VelocidadSalto || tiempoEnAire > TiempoCaida))
            {
                enAire = true;
                if (velocidadY > VelocidadSalto)
                {
                    Sonidos.Reproducir("salto", pos, 0.7f);
                    if (Time.time >= proximaVozSalto && Random.value < 0.25f)
                    {
                        proximaVozSalto = Time.time + 5.5f;
                        int idx = personaje != null ? personaje.IndicePersonaje : 0;
                        ReproducirVoz(idx, false, pos, 0.55f);
                    }
                }
            }
        }
        else
        {
            if (enAire) Sonidos.Reproducir("aterrizar", pos, 0.7f);
            enAire = false;
            tiempoEnAire = 0f;
        }

        // deslizar
        if (deslizando && !deslizandoAntes) Sonidos.Reproducir("deslizar", pos, 0.8f);
        deslizandoAntes = deslizando;

        // pasos
        if (suelo && !deslizando && movimiento > 0.1f)
        {
            proximoPaso -= dt;
            if (proximoPaso <= 0f)
            {
                alternar = 1 - alternar;
                Sonidos.Reproducir(alternar == 0 ? "paso1" : "paso2", pos, 0.35f);
                proximoPaso = TiempoEntrePasos;
            }
        }
        else
        {
            proximoPaso = 0.05f; // el primer paso suena casi enseguida al arrancar
        }
    }
}
