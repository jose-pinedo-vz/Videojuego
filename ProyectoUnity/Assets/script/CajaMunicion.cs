using Unity.Netcode;
using UnityEngine;

/// <summary>
/// Caja de Municion:
/// - Aparece a lo largo del mapa igual que las cajas de armas.
/// - Se recoge AUTOMATICAMENTE al pasar por encima de ella (sin presionar tecla).
/// - Otorga un cargador completo de balas para cualquier arma que el jugador tenga equipada.
/// - Reaparece cada cierto tiempo en una ubicacion distinta del mapa.
/// </summary>
public class CajaMunicion : MonoBehaviour
{
    public int IndiceCaja { get; private set; } = -1;
    public int IndicePuntoSpawnActual { get; set; } = -1;
    public bool EstaDisponible { get; private set; } = true;

    private bool cayendo;
    private float velocidadCaida;
    private const float Gravedad = 22f;
    private const float DistanciaMaxCaida = 30f;
    private float faseFlotacion;

    public void Inicializar(int indiceCaja, int indiceSpawn, Vector3 posicion)
    {
        IndiceCaja = indiceCaja;
        IndicePuntoSpawnActual = indiceSpawn;
        faseFlotacion = indiceCaja * 1.3f;

        ModelosProceduralesArmas.AsegurarMallaCajaMunicion(gameObject);
        ConfigurarColliderTrigger();
        ColocarEnPosicion(posicion);
    }

    private void Awake()
    {
        ModelosProceduralesArmas.AsegurarMallaCajaMunicion(gameObject);
        ConfigurarColliderTrigger();
    }

    private void ConfigurarColliderTrigger()
    {
        BoxCollider box = GetComponent<BoxCollider>();
        if (box == null) box = gameObject.AddComponent<BoxCollider>();
        box.isTrigger = true;
        box.center = Vector3.zero;
        box.size = new Vector3(1.35f, 1.15f, 1.35f);
    }

    public void ColocarEnPosicion(Vector3 posicion)
    {
        transform.position = posicion;
        EstablecerDisponible(true);
        velocidadCaida = 0f;
        cayendo = BuscarPiso(transform.position + Vector3.up * 0.4f, DistanciaMaxCaida, out _);
    }

    public void EstablecerDisponible(bool disponible)
    {
        EstaDisponible = disponible;
        MeshRenderer[] renderers = GetComponentsInChildren<MeshRenderer>(true);
        foreach (MeshRenderer mr in renderers)
        {
            if (mr != null) mr.enabled = disponible;
        }
        Collider col = GetComponent<Collider>();
        if (col != null) col.enabled = disponible;
    }

    private void Update()
    {
        if (!EstaDisponible) return;

        if (cayendo)
        {
            ActualizarCaida();
        }
        else
        {
            // Giro suave para distinguir visualmente la caja de municion
            transform.Rotate(0f, 38f * Time.deltaTime, 0f, Space.World);
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        IntentarRecogerAutomaticamente(other);
    }

    private void OnTriggerStay(Collider other)
    {
        IntentarRecogerAutomaticamente(other);
    }

    private void IntentarRecogerAutomaticamente(Collider other)
    {
        if (!EstaDisponible || other == null) return;
        if (!other.CompareTag("Player")) return;

        Personaje jugador = other.GetComponentInParent<Personaje>();
        if (jugador == null) return;
        if (jugador.IsSpawned && !jugador.IsOwner) return;

        Arma arma = jugador.armaEquipada;
        if (arma == null || arma.EnCaja) return;

        if (arma.RecargarConCajaMunicion())
        {
            Sonidos.Reproducir("recarga", transform.position, 0.95f, 1.12f);
            EstablecerDisponible(false);

            if (GestorArmas.Instancia != null)
            {
                GestorArmas.Instancia.NotificarCajaMunicionConsumida(this, jugador);
            }
        }
    }

    private bool BuscarPiso(Vector3 origen, float distancia, out float yPiso)
    {
        yPiso = 0f;
        RaycastHit[] hits = Physics.RaycastAll(origen, Vector3.down, distancia, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
        float mejor = float.MaxValue;
        bool hay = false;
        foreach (RaycastHit h in hits)
        {
            if (h.collider == null) continue;
            if (h.collider.GetComponentInParent<Personaje>() != null) continue;
            if (h.collider.GetComponentInParent<Arma>() != null) continue;
            if (h.collider.GetComponentInParent<CajaMunicion>() != null) continue;
            if (h.distance < mejor)
            {
                mejor = h.distance;
                yPiso = h.point.y;
                hay = true;
            }
        }
        return hay;
    }

    private void ActualizarCaida()
    {
        velocidadCaida += Gravedad * Time.deltaTime;
        float paso = velocidadCaida * Time.deltaTime;
        Vector3 pos = transform.position;
        float mitadAlto = 0.20f;

        if (BuscarPiso(pos + Vector3.up * 0.35f, DistanciaMaxCaida, out float yPiso))
        {
            float yFinal = yPiso + mitadAlto;
            if (pos.y - paso <= yFinal)
            {
                transform.position = new Vector3(pos.x, yFinal, pos.z);
                cayendo = false;
                velocidadCaida = 0f;
                return;
            }
            transform.position += Vector3.down * paso;
        }
        else
        {
            cayendo = false;
        }
    }
}
