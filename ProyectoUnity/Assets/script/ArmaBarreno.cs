using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

/// <summary>
/// Barreno:
/// - Paquete de 3 barrenos.
/// - Al lanzarse con V tiene un efecto de area medio capaz de matar a los enemigos INCLUYENDO al portador.
/// - Si alguien le dispara al portador mientras porta barrenos activos, estos explotan inmediatamente.
/// </summary>
public class ArmaBarreno : Arma
{
    [Header("Configuracion de Barreno")]
    [SerializeField] private float radioExplosion = 4.4f;
    [SerializeField] private float tiempoMecha = 1.75f;
    [SerializeField] private float velocidadLanzamientoX = 9.5f;
    [SerializeField] private float velocidadLanzamientoY = 5.2f;

    private bool detonandoEnPortador;

    protected override void Start()
    {
        municionMaxima = 3;
        municionActual = 3;
        if (Cadencia <= 0f) Cadencia = 1.4f;
        if (tiempoRecarga <= 0f) tiempoRecarga = 1.6f;
        base.Start();
        ActualizarVisualPaquete();
    }

    protected override void AlCompletarRecarga()
    {
        base.AlCompletarRecarga();
        ActualizarVisualPaquete();
    }

    private void ActualizarVisualPaquete()
    {
        if (enCaja) return;
        Transform t1 = transform.Find("BarrenoExtra_1");
        Transform t2 = transform.Find("BarrenoExtra_2");
        if (t1 != null) t1.gameObject.SetActive(municionActual >= 2);
        if (t2 != null) t2.gameObject.SetActive(municionActual >= 3);

        // Si ya lanzo los 3 barrenos y no tiene ninguno en la mano, atenuar el modelo principal hasta que recargue con caja de municion
        MeshRenderer[] mrs = GetComponentsInChildren<MeshRenderer>(true);
        foreach (MeshRenderer mr in mrs)
        {
            if (mr == null || mr.gameObject.name == "CajaMaderaVisual") continue;
            if (mr.transform.IsChildOf(t1 != null ? t1 : transform) && t1 != null && mr.transform != transform && mr.transform.parent == t1) continue;
            if (mr.transform.IsChildOf(t2 != null ? t2 : transform) && t2 != null && mr.transform != transform && mr.transform.parent == t2) continue;
            mr.enabled = !enCaja && municionActual > 0;
        }
    }

    public override void Disparar()
    {
        if (enCaja)
        {
            base.Disparar();
            ActualizarVisualPaquete();
            return;
        }

        if (recargando || jugadorQueLaEquipo == null) return;
        if (municionActual <= 0)
        {
            Sonidos.Reproducir("sin_municion", transform.position, 0.85f);
            return;
        }

        if (Time.time < tiempoUltimoDisparo + (1f / Mathf.Max(0.2f, Cadencia)))
            return;

        tiempoUltimoDisparo = Time.time;
        municionActual--;
        ContadorDisparos++;
        ActualizarVisualPaquete();

        Personaje lanzador = jugadorQueLaEquipo;
        float dirX = lanzador.DireccionHorizontal;
        Vector3 origen = lanzador.transform.position + new Vector3(dirX * 0.55f, 1.1f, 0f);
        Vector3 velocidadInicial = new Vector3(dirX * velocidadLanzamientoX, velocidadLanzamientoY, 0f);

        UltimoInicioTrazo = origen;
        UltimoFinTrazo = origen + velocidadInicial.normalized * 1.2f;

        if (lanzador.IsSpawned && NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
        {
            lanzador.LanzarBarrenoRpc(origen, velocidadInicial);
        }
        else
        {
            InstanciarBarrenoLanzado(lanzador, origen, velocidadInicial, true, radioExplosion, tiempoMecha);
        }
    }

    /// <summary>
    /// Si alguien le dispara al portador mientras tiene barrenos disponibles, explota inmediatamente.
    /// </summary>
    public bool PuedeExplotarAlRecibirDisparo()
    {
        return !enCaja && municionActual > 0 && !detonandoEnPortador;
    }

    public void DetonarEnPortador(Personaje atacante)
    {
        if (detonandoEnPortador || jugadorQueLaEquipo == null) return;
        detonandoEnPortador = true;

        Personaje portador = jugadorQueLaEquipo;
        Vector3 centroExplosion = portador.transform.position + new Vector3(0f, 0.9f, 0f);
        municionActual = 0;
        ActualizarVisualPaquete();

        Personaje causante = atacante != null ? atacante : portador;

        if (portador.IsSpawned && NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
        {
            portador.DetonarBarrenoEnPosicionRpc(centroExplosion, radioExplosion);
        }

        EjecutarExplosionArea(centroExplosion, radioExplosion, causante, true);
        detonandoEnPortador = false;
    }

    public static void InstanciarBarrenoLanzado(Personaje lanzador, Vector3 origen, Vector3 velocidadInicial, bool aplicarDanoAutoritativo, float radio = 4.4f, float mecha = 1.75f)
    {
        GameObject proyectil = ModelosProceduralesArmas.InstanciarModeloBarreno(origen);
        Sonidos.Reproducir("tick", origen, 0.9f, 1.5f);
        ProyectilBarreno comp = proyectil.AddComponent<ProyectilBarreno>();
        comp.Inicializar(lanzador, velocidadInicial, radio, mecha, aplicarDanoAutoritativo);
    }

    public static void EjecutarExplosionArea(Vector3 centro, float radio, Personaje responsable, bool aplicarDanoAutoritativo)
    {
        CrearEfectoVisualExplosion(centro, radio);
        Sonidos.Reproducir("explosion", centro, 1.0f, Random.Range(0.88f, 1.04f));
        Sonidos.Reproducir("disparo_barret", centro, 0.85f, 0.65f);
        EfectosDisparo.Sacudir(0.28f);

        if (!aplicarDanoAutoritativo) return;

        // Daña y mata a todos los personajes dentro del radio medio, INCLUYENDO al propio portador/lanzador
        HashSet<Personaje> afectados = new HashSet<Personaje>();
        Collider[] hits = Physics.OverlapSphere(centro, radio, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
        foreach (Collider col in hits)
        {
            if (col == null) continue;
            Personaje victima = col.GetComponentInParent<Personaje>();
            if (victima == null || afectados.Contains(victima)) continue;

            afectados.Add(victima);
            Personaje atacanteEfectivo = responsable != null ? responsable : victima;
            victima.RecibirDañoExplosion(atacanteEfectivo);
        }
    }

    public static void CrearEfectoVisualExplosion(Vector3 centro, float radio)
    {
        GameObject raiz = new GameObject("ExplosionBarrenoFX");
        raiz.transform.position = centro;

        // Esfera expansiva de onda de choque
        GameObject esfera = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        esfera.transform.SetParent(raiz.transform, false);
        Collider col = esfera.GetComponent<Collider>();
        if (col != null) Destroy(col);

        MeshRenderer mr = esfera.GetComponent<MeshRenderer>();
        Material matOnda = null;
        if (mr != null)
        {
            Shader sh = Shader.Find("Sprites/Default");
            if (sh == null) sh = Shader.Find("Universal Render Pipeline/Unlit");
            matOnda = new Material(sh);
            matOnda.color = new Color(1f, 0.55f, 0.1f, 0.78f);
            mr.material = matOnda;
        }

        // Particulas de fuego y humo
        ParticleSystem ps = raiz.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = ps.main;
        main.duration = 0.45f;
        main.loop = false;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.25f, 0.65f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(3.5f, 11f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.25f, 0.65f);
        main.startColor = new ParticleSystem.MinMaxGradient(
            new Color(1f, 0.85f, 0.2f, 1f),
            new Color(0.95f, 0.25f, 0.05f, 0.9f));
        main.gravityModifier = 0.6f;
        main.simulationSpace = ParticleSystemSimulationSpace.World;

        var emission = ps.emission;
        emission.rateOverTime = 0f;
        emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 38) });

        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = 0.35f;

        var psr = raiz.GetComponent<ParticleSystemRenderer>();
        Shader shPart = Shader.Find("Sprites/Default");
        if (shPart != null) psr.material = new Material(shPart);

        ps.Play();

        AnimadorOndaExplosion anim = raiz.AddComponent<AnimadorOndaExplosion>();
        anim.Inicializar(esfera.transform, matOnda, radio);
        Destroy(raiz, 1.2f);
    }
}

public class ProyectilBarreno : MonoBehaviour
{
    private Personaje lanzador;
    private Vector3 velocidad;
    private float radioExplosion = 4.4f;
    private float tiempoRestante = 1.75f;
    private bool aplicarDano;
    private float temporizadorChispa;
    private bool exploto;

    public void Inicializar(Personaje quienLanza, Vector3 velInicial, float radio, float mecha, bool autoritativo)
    {
        lanzador = quienLanza;
        velocidad = velInicial;
        radioExplosion = radio;
        tiempoRestante = mecha;
        aplicarDano = autoritativo;
    }

    private void Update()
    {
        if (exploto) return;

        float dt = Time.deltaTime;
        tiempoRestante -= dt;
        temporizadorChispa += dt;

        if (temporizadorChispa >= 0.22f)
        {
            temporizadorChispa = 0f;
            Sonidos.Reproducir("tick", transform.position, 0.45f, 1.9f);
        }

        // Fisica balistica con rebote suave en piso/paredes
        velocidad.y -= 18f * dt;
        Vector3 paso = velocidad * dt;
        float dist = paso.magnitude;

        if (dist > 0.0001f)
        {
            if (Physics.SphereCast(transform.position, 0.14f, paso.normalized, out RaycastHit hit, dist, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
            {
                if (hit.collider != null && (lanzador == null || !hit.collider.transform.IsChildOf(lanzador.transform)))
                {
                    transform.position = hit.point + hit.normal * 0.14f;
                    velocidad = Vector3.Reflect(velocidad, hit.normal) * 0.35f;
                    velocidad.x *= 0.65f;
                }
                else
                {
                    transform.position += paso;
                }
            }
            else
            {
                transform.position += paso;
            }
        }

        transform.Rotate(240f * dt, 120f * dt, 380f * dt, Space.Self);

        if (tiempoRestante <= 0f)
        {
            exploto = true;
            ArmaBarreno.EjecutarExplosionArea(transform.position, radioExplosion, lanzador, aplicarDano);
            Destroy(gameObject);
        }
    }
}

public class AnimadorOndaExplosion : MonoBehaviour
{
    private Transform esfera;
    private Material material;
    private float radioMax;
    private float tiempo;
    private const float Duracion = 0.26f;

    public void Inicializar(Transform tEsfera, Material mat, float radio)
    {
        esfera = tEsfera;
        material = mat;
        radioMax = radio;
    }

    private void Update()
    {
        tiempo += Time.deltaTime;
        float t = Mathf.Clamp01(tiempo / Duracion);
        if (esfera != null)
        {
            float diametro = Mathf.Lerp(0.4f, radioMax * 2f, 1f - Mathf.Pow(1f - t, 3f));
            esfera.localScale = Vector3.one * diametro;
        }
        if (material != null)
        {
            Color c = material.color;
            c.a = Mathf.Lerp(0.78f, 0f, t);
            material.color = c;
        }
        if (t >= 1f && esfera != null)
        {
            esfera.gameObject.SetActive(false);
        }
    }
}
