using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

/// <summary>
/// Machete:
/// - Cadencia de utilizacion alta, alcance muy corto.
/// - Rango de ataque de toda el area visual frontal hacia donde apunta el personaje.
/// - Si se lanza (tecla F, clic derecho o mantener V por 0.38s), vuela girando y mata al enemigo al impactar,
///   quedando en el suelo para poder recogerse de nuevo.
/// </summary>
public class ArmaMachete : Arma
{
    [Header("Configuracion de Machete")]
    [SerializeField] private float alturaAreaVisual = 2.35f;
    [SerializeField] private float velocidadLanzamiento = 19f;
    [SerializeField] private float distanciaMaximaLanzamiento = 16f;
    [SerializeField] private float tiempoMantenerParaLanzar = 0.38f;

    private float tiempoPresionandoV;
    private bool lanzadoEnEstePulso;
    private bool enVuelo;
    private float anguloTajada;
    private Coroutine corrutinaTajada;

    protected override void Start()
    {
        if (Cadencia <= 0f) Cadencia = 3.6f;
        if (Alcance <= 0f || Alcance > 5f) Alcance = 2.45f;
        Dispersion = 0f;
        municionMaxima = 0; // Cuerpo a cuerpo sin recarga por balas
        municionActual = 1;
        base.Start();
    }

    public override void ProcesarEntradaSecundaria(bool presionoEsteFrame, bool presionando)
    {
        if (enCaja || jugadorQueLaEquipo == null || enVuelo) return;
        if (presionoEsteFrame)
        {
            IniciarLanzamientoMachete();
        }
    }

    public override void ProcesarEntradaDisparo(bool presionando, bool presionoEsteFrame, bool soltoEsteFrame)
    {
        if (jugadorQueLaEquipo == null || enVuelo) return;

        if (enCaja)
        {
            if (presionoEsteFrame) base.ProcesarEntradaDisparo(presionando, presionoEsteFrame, soltoEsteFrame);
            return;
        }

        if (presionoEsteFrame)
        {
            tiempoPresionandoV = 0f;
            lanzadoEnEstePulso = false;
            Disparar(); // Tajada inmediata de alta cadencia
        }

        if (presionando)
        {
            tiempoPresionandoV += Time.deltaTime;
            if (!lanzadoEnEstePulso && tiempoPresionandoV >= tiempoMantenerParaLanzar)
            {
                lanzadoEnEstePulso = true;
                IniciarLanzamientoMachete();
                return;
            }
        }

        if (soltoEsteFrame)
        {
            tiempoPresionandoV = 0f;
        }
    }

    public override void Disparar()
    {
        if (enCaja)
        {
            base.Disparar();
            return;
        }

        if (jugadorQueLaEquipo == null || enVuelo) return;
        if (Time.time < tiempoUltimoDisparo + (1f / Mathf.Max(0.5f, Cadencia))) return;

        tiempoUltimoDisparo = Time.time;
        ContadorDisparos++;

        float dirX = jugadorQueLaEquipo.DireccionHorizontal;
        Vector3 posJugador = jugadorQueLaEquipo.transform.position;
        Vector3 centroVisualJugador = posJugador + new Vector3(0f, 0.95f, 0f);

        // Animacion local de tajada
        if (corrutinaTajada != null) StopCoroutine(corrutinaTajada);
        corrutinaTajada = StartCoroutine(AnimarTajada());

        // Detectar enemigos en TODA el area visual frontal hacia donde apunta el personaje
        HashSet<Personaje> golpeados = new HashSet<Personaje>();
        Vector3 centroCaja = centroVisualJugador + new Vector3(dirX * (Alcance * 0.5f), 0f, 0f);
        Vector3 mitadCaja = new Vector3(Alcance * 0.55f, alturaAreaVisual * 0.5f, 1.4f);

        Collider[] colisiones = Physics.OverlapBox(centroCaja, mitadCaja, Quaternion.identity, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
        foreach (Collider col in colisiones)
        {
            if (col == null) continue;
            Personaje enemigo = col.GetComponentInParent<Personaje>();
            if (enemigo == null || enemigo == jugadorQueLaEquipo || golpeados.Contains(enemigo)) continue;

            // Verificar que este frente al personaje (en su direccion de mirada)
            float deltaX = (enemigo.transform.position.x - posJugador.x) * dirX;
            if (deltaX >= -0.25f && deltaX <= Alcance + 0.35f)
            {
                golpeados.Add(enemigo);
                Vector3 puntoImpacto = enemigo.transform.position + new Vector3(0f, 0.9f, 0f);
                enemigo.RecibirImpactoDeDisparo(jugadorQueLaEquipo, puntoImpacto, new Vector3(dirX, 0f, 0f));
            }
        }

        // Trazo visual corto de arco de corte
        Vector3 inicioArco = centroVisualJugador + new Vector3(dirX * 0.55f, 0.55f, 0f);
        Vector3 finArco = centroVisualJugador + new Vector3(dirX * (Alcance * 0.85f), -0.45f, 0f);
        UltimoInicioTrazo = inicioArco;
        UltimoFinTrazo = finArco;
        MostrarDisparo(inicioArco, finArco);

        if (jugadorQueLaEquipo != null && jugadorQueLaEquipo.IsSpawned)
        {
            jugadorQueLaEquipo.MostrarDisparoRpc(inicioArco, finArco);
        }
    }

    private IEnumerator AnimarTajada()
    {
        float duracion = 0.15f;
        float t = 0f;
        while (t < duracion)
        {
            t += Time.deltaTime;
            float p = Mathf.Clamp01(t / duracion);
            anguloTajada = Mathf.Sin(p * Mathf.PI) * 75f;
            yield return null;
        }
        anguloTajada = 0f;
    }

    protected override void ActualizarRotacionEnMano()
    {
        base.ActualizarRotacionEnMano();
        if (Mathf.Abs(anguloTajada) > 0.01f && jugadorQueLaEquipo != null)
        {
            float dirX = jugadorQueLaEquipo.DireccionHorizontal;
            transform.rotation *= Quaternion.Euler(0f, 0f, -dirX * anguloTajada);
        }
    }

    private void IniciarLanzamientoMachete()
    {
        if (jugadorQueLaEquipo == null || enVuelo) return;

        Personaje lanzador = jugadorQueLaEquipo;
        float dirX = lanzador.DireccionHorizontal;
        Vector3 origenVuelo = lanzador.transform.position + new Vector3(dirX * 0.65f, 1.05f, 0f);

        Sonidos.Reproducir("disparo_machete", origenVuelo, 0.95f);

        if (lanzador.IsSpawned && NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
        {
            lanzador.LanzarMacheteRpc(gameObject.name, origenVuelo, dirX);
        }
        else
        {
            EjecutarVueloMachete(lanzador, origenVuelo, dirX, true);
        }
    }

    public void EjecutarVueloMachete(Personaje lanzador, Vector3 origen, float dirX, bool aplicarDanoAutoritativo)
    {
        if (enVuelo) return;

        // Desequipar del jugador
        if (jugadorQueLaEquipo != null)
        {
            if (jugadorQueLaEquipo.armaEquipada == this)
                jugadorQueLaEquipo.armaEquipada = null;
            jugadorQueLaEquipo = null;
        }
        mano = null;
        jugadorCerca = false;
        transform.parent = null;

        Collider col = GetComponent<Collider>();
        if (col != null) col.enabled = false;

        StartCoroutine(CorrutinaVueloMachete(lanzador, origen, Mathf.Sign(dirX), aplicarDanoAutoritativo));
    }

    private IEnumerator CorrutinaVueloMachete(Personaje lanzador, Vector3 origen, float dirX, bool aplicarDano)
    {
        enVuelo = true;
        transform.position = origen;
        Vector3 velocidad = new Vector3(dirX * velocidadLanzamiento, 1.25f, 0f);
        float distanciaRecorrida = 0f;
        Vector3 posFinal = origen;

        while (distanciaRecorrida < distanciaMaximaLanzamiento)
        {
            float dt = Time.deltaTime;
            velocidad.y -= 4.8f * dt; // Ligera caida balistica
            Vector3 paso = velocidad * dt;
            float distPaso = paso.magnitude;

            if (distPaso > 0.0001f)
            {
                RaycastHit[] impactos = Physics.SphereCastAll(
                    transform.position,
                    0.32f,
                    paso.normalized,
                    distPaso,
                    Physics.DefaultRaycastLayers,
                    QueryTriggerInteraction.Ignore);

                bool choco = false;
                foreach (RaycastHit hit in impactos)
                {
                    if (hit.collider == null) continue;
                    if (hit.collider.transform.IsChildOf(transform)) continue;

                    Personaje p = hit.collider.GetComponentInParent<Personaje>();
                    if (p != null)
                    {
                        if (p == lanzador) continue;

                        if (aplicarDano)
                        {
                            p.RecibirImpactoDeDisparo(lanzador, hit.point, new Vector3(dirX, 0f, 0f));
                        }
                        Sonidos.Reproducir("impacto", hit.point, 0.95f);
                        posFinal = hit.point;
                        choco = true;
                        break;
                    }
                    else if (hit.collider.GetComponentInParent<Arma>() == null)
                    {
                        // Pared o piso
                        Sonidos.Reproducir("impacto", hit.point, 0.75f);
                        posFinal = hit.point - new Vector3(dirX * 0.2f, 0f, 0f);
                        choco = true;
                        break;
                    }
                }

                if (choco) break;
            }

            transform.position += paso;
            posFinal = transform.position;
            distanciaRecorrida += distPaso;

            // Giro rapido en el aire
            transform.Rotate(0f, 0f, -dirX * 1080f * dt, Space.World);
            yield return null;
        }

        enVuelo = false;
        Soltar(posFinal);
    }
}
