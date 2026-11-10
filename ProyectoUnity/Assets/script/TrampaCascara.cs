using UnityEngine;

/// <summary>
/// Cascara de platano en el suelo:
/// - No hace daño de area; sirve como trampa en el piso.
/// - Cuando un enemigo pasa sobre ella, hace que no se pueda mover bien durante unos segundos (resbalon/ralentizacion severa).
/// </summary>
public class TrampaCascara : MonoBehaviour
{
    private Personaje lanzador;
    private Vector3 velocidad;
    private bool enPiso;
    private float tiempoVida = 35f;
    private float tiempoArmado = 0.25f;
    private float duracionEfecto = 3.5f;
    private bool consumida;
    private const float RADIO_EXPLOSION_VISUAL = 1.2f; // chica a proposito: la del barreno es de 4.4
    private float yInicial;
    private bool yaInicio;

    public void Inicializar(Personaje quienLanza, Vector3 velocidadInicial, float duracionResbalon = 3.5f)
    {
        lanzador = quienLanza;
        velocidad = velocidadInicial;
        duracionEfecto = duracionResbalon;
    }

    private void Update()
    {
        if (consumida) return;

        float dt = Time.deltaTime;
        tiempoVida -= dt;
        if (tiempoVida <= 0f)
        {
            Destroy(gameObject);
            return;
        }

        if (tiempoArmado > 0f)
            tiempoArmado -= dt;

        if (!yaInicio) { yInicial = transform.position.y; yaInicio = true; }

        // Caida al suelo hasta tocar una superficie solida
        if (!enPiso)
        {
            // si no hay nada debajo (mapa sin piso) no cae para siempre
            if (transform.position.y < yInicial - 40f) { Destroy(gameObject); return; }

            velocidad.y -= 19f * dt;
            Vector3 paso = velocidad * dt;
            float dist = paso.magnitude;

            if (dist > 0.0001f)
            {
                RaycastHit[] hits = Physics.RaycastAll(transform.position + Vector3.up * 0.1f, paso.normalized, dist + 0.15f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
                System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance)); // el golpe mas cercano primero
                foreach (RaycastHit h in hits)
                {
                    if (h.collider == null) continue;
                    if (h.collider.GetComponentInParent<Personaje>() != null) continue;
                    if (h.collider.GetComponentInParent<Arma>() != null) continue;
                    if (h.collider.GetComponentInParent<TrampaCascara>() != null) continue;

                    transform.position = new Vector3(h.point.x, h.point.y + 0.05f, transform.position.z);
                    velocidad = Vector3.zero;
                    enPiso = true;
                    AlCaerAlPiso();
                    break;
                }
            }

            if (!enPiso)
            {
                transform.position += paso;
            }
        }

        if (tiempoArmado > 0f) return;

        // Comprobar si un enemigo pisa la cascara
        Collider[] cercanos = Physics.OverlapSphere(transform.position, 0.85f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
        foreach (Collider col in cercanos)
        {
            if (col == null) continue;
            Personaje p = col.GetComponentInParent<Personaje>();
            if (p == null) continue;

            // No activar con el propio lanzador inmediatamente al soltarla
            if (p == lanzador && tiempoVida > 33.8f) continue;

            consumida = true;
            Sonidos.Reproducir("deslizar", transform.position, 0.95f, 1.35f);
            // mini explosion: se ve y se oye como una bomba, pero no hace dano; lo que hace es tirar al personaje (ragdoll)
            ArmaBarreno.CrearEfectoVisualExplosion(transform.position, RADIO_EXPLOSION_VISUAL);
            Sonidos.Reproducir("explosion", transform.position, 0.6f, 1.3f);
            p.RecibirResbalonPlatano(duracionEfecto, transform.position);
            Destroy(gameObject);
            return;
        }
    }

    // Se llama una sola vez cuando la cascara toca el suelo. Punto de enganche para mecanicas nuevas
    // (sonido, particulas, cambiar el efecto segun el tipo de suelo, etc.)
    protected virtual void AlCaerAlPiso()
    {
    }
}
