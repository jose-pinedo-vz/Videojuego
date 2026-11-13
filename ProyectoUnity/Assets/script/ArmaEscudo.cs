using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

/// <summary>
/// Escudo Troyano:
/// - Sin empuñar: cubre la mayor parte del cuerpo a excepcion de la cabeza hacia donde este mirando el personaje,
///   permitiendo caminar o correr con normalidad.
/// - Empuñado (manteniendo o pulsando V): cubre todo el cuerpo (incluida la cabeza) y realiza golpes hacia los enemigos
///   que no los matan, pero los empujan hacia atras.
/// </summary>
public class ArmaEscudo : Arma
{
    [Header("Configuracion de Escudo Troyano")]
    [SerializeField] private float alcanceGolpe = 2.25f;
    [SerializeField] private float fuerzaEmpujeHorizontal = 14.5f;
    [SerializeField] private float fuerzaEmpujeVertical = 4.2f;
    [SerializeField] private float alturaLimiteSinEmpunar = 1.30f; // Arriba de 1.30m es la cabeza descubierta

    private bool manteniendoEmpunado;
    private float tiempoFinGolpe;
    private float avanceGolpeActual;

    public bool EstaEmpunado => !enCaja && (manteniendoEmpunado || Time.time < tiempoFinGolpe);

    protected override void Start()
    {
        if (Cadencia <= 0f) Cadencia = 2.0f;
        if (Alcance <= 0f || Alcance > 5f) Alcance = alcanceGolpe;
        municionMaxima = 0;
        municionActual = 1;
        Relantizacion = 0f; // Se puede correr o caminar con normalidad
        base.Start();
    }

    public override void ProcesarEntradaDisparo(bool presionando, bool presionoEsteFrame, bool soltoEsteFrame)
    {
        if (jugadorQueLaEquipo == null) return;

        if (enCaja)
        {
            if (presionoEsteFrame) base.ProcesarEntradaDisparo(presionando, presionoEsteFrame, soltoEsteFrame);
            return;
        }

        bool estadoPrevio = manteniendoEmpunado;
        manteniendoEmpunado = presionando;

        if (estadoPrevio != manteniendoEmpunado && jugadorQueLaEquipo.IsSpawned && jugadorQueLaEquipo.IsOwner)
        {
            jugadorQueLaEquipo.ActualizarEscudoEmpunadoRpc(manteniendoEmpunado);
        }

        if (presionoEsteFrame)
        {
            Disparar();
        }
    }

    public void EstablecerEmpunadoRemoto(bool empunado)
    {
        manteniendoEmpunado = empunado;
    }

    public override void Disparar()
    {
        if (enCaja)
        {
            base.Disparar();
            return;
        }

        if (jugadorQueLaEquipo == null) return;
        if (Time.time < tiempoUltimoDisparo + (1f / Mathf.Max(0.4f, Cadencia))) return;

        tiempoUltimoDisparo = Time.time;
        tiempoFinGolpe = Time.time + 0.36f;
        ContadorDisparos++;
        jugadorQueLaEquipo.CancelarInmunidadPorAtaque();

        float dirX = jugadorQueLaEquipo.DireccionHorizontal;
        Vector3 posJugador = jugadorQueLaEquipo.transform.position;
        Vector3 centroGolpe = posJugador + new Vector3(dirX * (alcanceGolpe * 0.55f), 0.95f, 0f);
        Vector3 mitadCaja = new Vector3(alcanceGolpe * 0.6f, 1.15f, 1.35f);

        HashSet<Personaje> empujados = new HashSet<Personaje>();
        Collider[] hits = Physics.OverlapBox(centroGolpe, mitadCaja, Quaternion.identity, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
        foreach (Collider col in hits)
        {
            if (col == null) continue;
            Personaje enemigo = col.GetComponentInParent<Personaje>();
            if (enemigo == null || enemigo == jugadorQueLaEquipo || empujados.Contains(enemigo)) continue;

            float deltaX = (enemigo.transform.position.x - posJugador.x) * dirX;
            if (deltaX >= -0.3f && deltaX <= alcanceGolpe + 0.4f)
            {
                empujados.Add(enemigo);
                Vector3 impulso = new Vector3(dirX * fuerzaEmpujeHorizontal, fuerzaEmpujeVertical, 0f);
                enemigo.RecibirEmpujeEscudo(impulso);
                Sonidos.Reproducir("impacto", enemigo.transform.position + Vector3.up * 0.9f, 0.95f, 0.85f);
            }
        }

        Vector3 inicioTrazo = posJugador + new Vector3(dirX * 0.45f, 0.95f, 0f);
        Vector3 finTrazo = posJugador + new Vector3(dirX * 1.15f, 0.95f, 0f);
        UltimoInicioTrazo = inicioTrazo;
        UltimoFinTrazo = finTrazo;

        if (jugadorQueLaEquipo.IsSpawned)
        {
            jugadorQueLaEquipo.MostrarDisparoRpc(inicioTrazo, finTrazo);
        }
    }

    /// <summary>
    /// Determina si el escudo bloquea un impacto entrante.
    /// - Sin empuñar: cubre el frente del cuerpo excepto la cabeza (alturaRelativa <= alturaLimiteSinEmpunar).
    /// - Empuñado: cubre todo el cuerpo (incluida la cabeza) hacia donde mira el personaje.
    /// </summary>
    public bool BloqueaImpacto(Vector3 puntoImpacto, Vector3 direccionDisparo)
    {
        if (enCaja || jugadorQueLaEquipo == null) return false;

        float dirMirada = jugadorQueLaEquipo.DireccionHorizontal;
        // El disparo viene de frente si viaja en sentido opuesto a hacia donde mira el portador
        bool vieneDeFrente = (direccionDisparo.x * dirMirada) < 0.15f;
        if (!vieneDeFrente) return false;

        if (EstaEmpunado)
        {
            Sonidos.Reproducir("impacto", puntoImpacto, 0.95f, 1.35f);
            return true; // Cubre todo el cuerpo incluida la cabeza
        }

        // Sin empuñar: cubre la mayor parte del cuerpo a excepcion de la cabeza
        float alturaRelativa = puntoImpacto.y - jugadorQueLaEquipo.transform.position.y;
        if (alturaRelativa <= alturaLimiteSinEmpunar)
        {
            Sonidos.Reproducir("impacto", puntoImpacto, 0.95f, 1.35f);
            return true;
        }

        return false; // Impacto en la cabeza pasa libremente
    }

    protected override void LateUpdate()
    {
        base.LateUpdate();

        if (jugadorQueLaEquipo == null || enCaja) return;

        float dirX = jugadorQueLaEquipo.DireccionHorizontal;
        float progresoGolpe = Time.time < tiempoFinGolpe
            ? Mathf.Sin(Mathf.Clamp01(1f - (tiempoFinGolpe - Time.time) / 0.36f) * Mathf.PI)
            : 0f;

        avanceGolpeActual = progresoGolpe * 0.42f;
        float elevacion = EstaEmpunado ? 0.18f : -0.14f;

        transform.position += new Vector3(dirX * (0.22f + avanceGolpeActual), elevacion, 0f);
    }

    public override void Soltar(Vector3 posicion)
    {
        manteniendoEmpunado = false;
        tiempoFinGolpe = 0f;
        base.Soltar(posicion);
    }
}
