using Unity.Netcode;
using UnityEngine;

/// <summary>
/// Penca de Platano:
/// - Viene en pencas de 5 platanos (usa los modelos penca.fbx y cascara.fbx).
/// - Al usar V el personaje se come un platano y su cascara cae al piso (copia del modelo cascara.fbx).
/// - Quien pise una cascara resbala y cae como muñeco (ragdoll). No hay explosion ni daño.
/// </summary>
public class ArmaPlatanos : Arma
{
    [Header("Configuracion de Penca de Platano")]
    [SerializeField] private float distanciaFrente = 0.55f;       // sale al frente del personaje (como los barrenos)
    [SerializeField] private float alturaLanzamiento = 1.05f;     // altura de la mano/pecho al arrojar
    [SerializeField] private float velocidadLanzamientoX = 8.4f;  // impulso horizontal hacia adelante
    [SerializeField] private float velocidadLanzamientoY = 4.6f;  // arco parabolico hacia arriba
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
        lanzador.CancelarInmunidadPorAtaque();
        float dirX = lanzador.DireccionHorizontal;
        float multLanzamiento = lanzador.MultiplicadorLanzamiento; // Pasiva de Kaiser: lanza 40% mas lejos

        // Arroja la cascara hacia adelante en parabola lejos del personaje (igual que los barrenos)
        Vector3 origen = lanzador.transform.position + new Vector3(dirX * distanciaFrente, alturaLanzamiento, 0f);
        Vector3 velocidad = new Vector3(
            dirX * Mathf.Max(6.5f, velocidadLanzamientoX) * multLanzamiento,
            Mathf.Max(3.8f, velocidadLanzamientoY) * multLanzamiento,
            0f);

        UltimoInicioTrazo = origen;
        UltimoFinTrazo = origen + velocidad.normalized * 1.1f;

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
