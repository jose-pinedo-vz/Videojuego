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
    private float pesoTajada;
    private float dirTajada = 1f;
    private Coroutine corrutinaTajada;

    protected override bool EstaEnVuelo => enVuelo;

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
        jugadorQueLaEquipo.CancelarInmunidadPorAtaque();

        float dirX = jugadorQueLaEquipo.DireccionHorizontal;
        Vector3 posJugador = jugadorQueLaEquipo.transform.position;
        Vector3 centroVisualJugador = posJugador + new Vector3(0f, 0.95f, 0f);

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

        // Referencia de arco para sonido/texto comic y animacion en abanico (sin trazo recto de bala)
        Vector3 inicioArco = centroVisualJugador + new Vector3(dirX * 0.45f, 0.55f, 0f);
        Vector3 finArco = centroVisualJugador + new Vector3(dirX * (Alcance * 0.88f), -0.45f, 0f);
        UltimoInicioTrazo = inicioArco;
        UltimoFinTrazo = finArco;
        IniciarBarridoAbanico(dirX);

        if (jugadorQueLaEquipo != null && jugadorQueLaEquipo.IsSpawned)
        {
            jugadorQueLaEquipo.MostrarDisparoRpc(inicioArco, finArco);
        }
    }

    public override void MostrarDisparo(Vector3 inicio, Vector3 fin)
    {
        ContadorDisparos++;
        UltimoInicioTrazo = inicio;
        UltimoFinTrazo = fin;

        float dirX = Mathf.Abs(fin.x - inicio.x) > 0.01f
            ? Mathf.Sign(fin.x - inicio.x)
            : (jugadorQueLaEquipo != null ? jugadorQueLaEquipo.DireccionHorizontal : 1f);
        IniciarBarridoAbanico(dirX);
    }

    private void IniciarBarridoAbanico(float dirX)
    {
        dirTajada = dirX >= 0f ? 1f : -1f;
        if (corrutinaTajada != null) StopCoroutine(corrutinaTajada);
        corrutinaTajada = StartCoroutine(AnimarTajadaAbanico());

        if (jugadorQueLaEquipo != null)
        {
            EfectoAbanicoCorte.Crear(jugadorQueLaEquipo.transform, dirTajada, Mathf.Max(1.8f, Alcance * 0.92f));
        }
    }

    private IEnumerator AnimarTajadaAbanico()
    {
        const float duracionBarrido = 0.14f;
        const float duracionRetorno = 0.07f;

        // Barrido amplio en abanico desde arriba (+84 grados) hacia abajo (-76 grados)
        float t = 0f;
        while (t < duracionBarrido)
        {
            t += Time.deltaTime;
            float p = Mathf.Clamp01(t / duracionBarrido);
            float suave = 1f - Mathf.Pow(1f - p, 2.4f);
            pesoTajada = 1f;
            anguloTajada = Mathf.Lerp(84f, -76f, suave);
            yield return null;
        }

        // Retorno suave a la posicion normal de agarre
        t = 0f;
        while (t < duracionRetorno)
        {
            t += Time.deltaTime;
            float p = Mathf.Clamp01(t / duracionRetorno);
            pesoTajada = 1f - p;
            anguloTajada = Mathf.Lerp(-76f, 0f, p);
            yield return null;
        }

        anguloTajada = 0f;
        pesoTajada = 0f;
        corrutinaTajada = null;
    }

    protected override void ActualizarRotacionEnMano()
    {
        base.ActualizarRotacionEnMano();
        if (pesoTajada > 0.001f && jugadorQueLaEquipo != null)
        {
            float dirX = jugadorQueLaEquipo.DireccionHorizontal;
            float rad = anguloTajada * Mathf.Deg2Rad;

            // Desplaza el machete describiendo un arco semicircular frontal hacia el enemigo
            Vector3 pivote = transform.position;
            transform.RotateAround(pivote, Vector3.forward, dirX * anguloTajada);
            Vector3 avanceArco = new Vector3(
                dirX * Mathf.Cos(rad) * 0.34f * pesoTajada,
                Mathf.Sin(rad) * 0.28f * pesoTajada,
                0f);
            transform.position += avanceArco;
        }
    }

    private void IniciarLanzamientoMachete()
    {
        if (jugadorQueLaEquipo == null || enVuelo) return;

        Personaje lanzador = jugadorQueLaEquipo;
        lanzador.CancelarInmunidadPorAtaque();
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

/// <summary>
/// Genera y anima el arco luminoso en forma de abanico frente al personaje cuando realiza un tajo con el machete.
/// </summary>
public class EfectoAbanicoCorte : MonoBehaviour
{
    private const int SEGMENTOS = 18;
    private const float ANGULO_INICIO = 78f;
    private const float ANGULO_FIN = -74f;
    private const float DURACION_DESPLIEGUE = 0.11f;
    private const float DURACION_TOTAL = 0.22f;

    private Transform seguirJugador;
    private float direccion = 1f;
    private float radioExterior = 2.25f;
    private float tiempo = 0f;
    private Mesh malla;
    private Material material;
    private Vector3[] vertices;
    private Color[] colores;
    private int[] triangulos;

    public static void Crear(Transform jugador, float dirX, float radio)
    {
        if (jugador == null) return;
        GameObject go = new GameObject("AbanicoCorteMachete");
        EfectoAbanicoCorte efecto = go.AddComponent<EfectoAbanicoCorte>();
        efecto.Inicializar(jugador, dirX >= 0f ? 1f : -1f, radio);
    }

    private void Inicializar(Transform jugador, float dirX, float radio)
    {
        seguirJugador = jugador;
        direccion = dirX;
        radioExterior = Mathf.Clamp(radio, 1.4f, 3.2f);

        MeshFilter mf = gameObject.AddComponent<MeshFilter>();
        MeshRenderer mr = gameObject.AddComponent<MeshRenderer>();
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        mr.receiveShadows = false;

        Shader sh = Shader.Find("Sprites/Default");
        if (sh == null) sh = Shader.Find("Universal Render Pipeline/Unlit");
        material = new Material(sh);
        material.color = Color.white;
        mr.sharedMaterial = material;

        malla = new Mesh();
        malla.name = "MallaAbanicoCorte";
        mf.sharedMesh = malla;

        int numPuntos = SEGMENTOS + 1;
        vertices = new Vector3[numPuntos * 2];
        colores = new Color[numPuntos * 2];
        triangulos = new int[SEGMENTOS * 6];
        for (int i = 0; i < SEGMENTOS; i++)
        {
            int v = i * 2;
            int t = i * 6;
            triangulos[t + 0] = v;
            triangulos[t + 1] = v + 1;
            triangulos[t + 2] = v + 2;
            triangulos[t + 3] = v + 2;
            triangulos[t + 4] = v + 1;
            triangulos[t + 5] = v + 3;
        }

        ActualizarPosicion();
        ActualizarMalla(0.18f, 1f);
    }

    private void Update()
    {
        tiempo += Time.deltaTime;
        if (tiempo >= DURACION_TOTAL)
        {
            Destroy(gameObject);
            return;
        }

        ActualizarPosicion();

        float avanceDespliegue = Mathf.Clamp01(tiempo / DURACION_DESPLIEGUE);
        float progresoArco = 1f - Mathf.Pow(1f - avanceDespliegue, 2.2f);
        float opacidad = tiempo <= DURACION_DESPLIEGUE
            ? 1f
            : 1f - Mathf.InverseLerp(DURACION_DESPLIEGUE, DURACION_TOTAL, tiempo);

        ActualizarMalla(Mathf.Clamp(progresoArco, 0.12f, 1f), opacidad);
    }

    private void ActualizarPosicion()
    {
        if (seguirJugador == null) return;
        Vector3 basePos = seguirJugador.position;
        transform.position = new Vector3(basePos.x + direccion * 0.12f, basePos.y + 0.96f, basePos.z - 0.10f);
    }

    private void ActualizarMalla(float fraccionArco, float opacidad)
    {
        float anguloActualFin = Mathf.Lerp(ANGULO_INICIO, ANGULO_FIN, fraccionArco);

        for (int i = 0; i <= SEGMENTOS; i++)
        {
            float t = (float)i / SEGMENTOS;
            float anguloDeg = Mathf.Lerp(ANGULO_INICIO, anguloActualFin, t);
            float rad = anguloDeg * Mathf.Deg2Rad;

            // El grosor de la media luna es mas ancho hacia la zona media/frontal y fino en la estela inicial
            float anchuraAbanico = Mathf.Lerp(0.18f, radioExterior * 0.62f, Mathf.Sin(Mathf.Pow(t, 0.75f) * Mathf.PI * 0.88f));
            float rExt = radioExterior;
            float rInt = Mathf.Max(0.28f, rExt - anchuraAbanico);

            float cos = Mathf.Cos(rad);
            float sin = Mathf.Sin(rad);

            int idx = i * 2;
            vertices[idx] = new Vector3(direccion * cos * rInt, sin * rInt, 0f);
            vertices[idx + 1] = new Vector3(direccion * cos * rExt, sin * rExt, 0f);

            // Degradado luminoso tipo abanico de acero blanco/celeste hacia la cola transparente
            float fadeCola = Mathf.Pow(t, 1.35f) * opacidad;
            colores[idx] = new Color(0.45f, 0.90f, 1.0f, 0.28f * fadeCola);
            colores[idx + 1] = new Color(0.95f, 0.98f, 1.0f, 0.94f * fadeCola);
        }

        malla.Clear();
        malla.vertices = vertices;
        malla.colors = colores;
        malla.triangles = triangulos;
        malla.RecalculateBounds();
    }

    private void OnDestroy()
    {
        if (malla != null) Destroy(malla);
        if (material != null) Destroy(material);
    }
}
