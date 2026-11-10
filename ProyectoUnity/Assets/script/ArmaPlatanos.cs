using Unity.Netcode;
using UnityEngine;

/// <summary>
/// Penca de Platano:
/// - Viene en pencas de 5 platanos (usa los modelos penca.fbx y cascara.fbx).
/// - No tiene daño de area; al usar V lanza una cascara al suelo como trampa que impide moverse bien a los enemigos.
/// </summary>
public class ArmaPlatanos : Arma
{
    [Header("Configuracion de Penca de Platano")]
    [SerializeField] private float distanciaDetras = 0.5f;     // que tan atras del personaje aparece
    [SerializeField] private float alturaSuelta = 0.4f;        // a que altura sale (cae al piso)
    [SerializeField] private float velocidadHaciaAtras = 1.0f; // empujoncito hacia atras al soltarla
    [SerializeField] private float velocidadLanzamientoY = 1.2f;
    [SerializeField] private float duracionResbalonSegundos = 3.5f;

    protected override void Start()
    {
        if (municionMaxima <= 0)
        {
            municionMaxima = 5;
            municionActual = 5;
        }
        if (Cadencia <= 0f) Cadencia = 2.2f;
        if (tiempoRecarga <= 0f) tiempoRecarga = 1.2f;
        base.Start();
        ActualizarPlatanosVisibles();
    }

    protected override void ActualizarVisualMunicion()
    {
        base.ActualizarVisualMunicion();
        ActualizarPlatanosVisibles();
    }

    protected override void AlCompletarRecarga()
    {
        base.AlCompletarRecarga();
        ActualizarPlatanosVisibles();
    }

    private void ActualizarPlatanosVisibles()
    {
        if (enCaja) return;

        Transform contenedor = transform.Find("MallaPencaProcedural");
        if (contenedor != null)
        {
            for (int i = 0; i < 5; i++)
            {
                Transform platano = contenedor.Find($"Platano_{i}");
                if (platano != null)
                {
                    platano.gameObject.SetActive(i < municionActual);
                }
            }
        }
    }

    public override void Disparar()
    {
        if (enCaja)
        {
            base.Disparar();
            ActualizarPlatanosVisibles();
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
        ActualizarPlatanosVisibles();

        Personaje lanzador = jugadorQueLaEquipo;
        float dirX = lanzador.DireccionHorizontal;
        // la cascara se suelta DETRAS del personaje (contra la direccion en que mira) y cae al piso
        Vector3 origen = lanzador.transform.position + new Vector3(-dirX * distanciaDetras, alturaSuelta, 0f);
        Vector3 velocidad = new Vector3(-dirX * velocidadHaciaAtras, velocidadLanzamientoY, 0f);

        UltimoInicioTrazo = origen;
        UltimoFinTrazo = origen + velocidad.normalized * 0.8f;

        if (lanzador.IsSpawned && NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
        {
            lanzador.NotificarCambioMunicionRed(this);
            lanzador.LanzarCascaraPlatanoRpc(origen, velocidad, duracionResbalonSegundos);
        }
        else
        {
            InstanciarCascaraEnEscena(lanzador, origen, velocidad, duracionResbalonSegundos);
        }
    }

    public static void InstanciarCascaraEnEscena(Personaje lanzador, Vector3 origen, Vector3 velocidadInicial, float duracionResbalon = 3.5f)
    {
        GameObject goCascara = ModelosProceduralesArmas.InstanciarModeloCascara(origen);
        Sonidos.Reproducir("soltar", origen, 0.85f, 1.25f);
        TrampaCascara trampa = goCascara.AddComponent<TrampaCascara>();
        trampa.Inicializar(lanzador, velocidadInicial, duracionResbalon);
    }
}
