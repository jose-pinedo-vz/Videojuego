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

        // Caida al suelo hasta tocar una superficie solida
        if (!enPiso)
        {
            velocidad.y -= 19f * dt;
            Vector3 paso = velocidad * dt;
            float dist = paso.magnitude;

            if (dist > 0.0001f)
            {
                RaycastHit[] hits = Physics.RaycastAll(transform.position + Vector3.up * 0.1f, paso.normalized, dist + 0.15f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
                foreach (RaycastHit h in hits)
                {
                    if (h.collider == null) continue;
                    if (h.collider.GetComponentInParent<Personaje>() != null) continue;
                    if (h.collider.GetComponentInParent<Arma>() != null) continue;
                    if (h.collider.GetComponentInParent<TrampaCascara>() != null) continue;

                    transform.position = new Vector3(h.point.x, h.point.y + 0.05f, transform.position.z);
                    velocidad = Vector3.zero;
                    enPiso = true;
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
            p.RecibirResbalonPlatano(duracionEfecto);
            Destroy(gameObject);
            return;
        }
    }
}
