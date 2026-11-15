using UnityEngine;
using UnityEngine.InputSystem;
using Unity.Netcode;

// Se ejecuta inmediatamente despues de Personaje.Update (orden 0) y antes de la evaluacion del Animator,
// leyendo la fisica y el estado de salto autoritativo de Personaje sin desincronizarse.
[DefaultExecutionOrder(5)]
public class AnimadorPuente : NetworkBehaviour
{
    private static readonly int ParamMovimiento = Animator.StringToHash("movimiento");
    private static readonly int ParamSuelo = Animator.StringToHash("EstaEnSuelo");
    private static readonly int ParamDeslizando = Animator.StringToHash("estaDeslizando");
    private static readonly int ParamCorriendo = Animator.StringToHash("estaCorriendo");
    private static readonly int ParamSaltar = Animator.StringToHash("Saltar");
    private static readonly int ParamVelocidadAnim = Animator.StringToHash("velocidadAnim");
    private static readonly int ParamAgachado = Animator.StringToHash("estaAgachado");

    private Animator animator;
    private CharacterController controlador;
    private Personaje personaje;

    private bool tieneMovimiento;
    private bool tieneSuelo;
    private bool tieneDeslizando;
    private bool tieneCorriendo;
    private bool tieneSaltar;
    private bool tieneVelocidadAnim;
    private bool tieneAgachado;

    private int ultimoSaltoAplicado;
    private float movFallback;

    private void Awake()
    {
        controlador = GetComponent<CharacterController>();
        personaje = GetComponent<Personaje>();
        RefrescarAnimator();
    }

    public void RefrescarAnimator()
    {
        animator = GetComponent<Animator>();
        if (animator == null) animator = GetComponentInChildren<Animator>();

        tieneMovimiento = false;
        tieneSuelo = false;
        tieneDeslizando = false;
        tieneCorriendo = false;
        tieneSaltar = false;
        tieneVelocidadAnim = false;
        tieneAgachado = false;

        if (animator != null && animator.runtimeAnimatorController != null)
        {
            foreach (AnimatorControllerParameter p in animator.parameters)
            {
                int h = p.nameHash;
                if (h == ParamMovimiento) tieneMovimiento = true;
                else if (h == ParamSuelo) tieneSuelo = true;
                else if (h == ParamDeslizando) tieneDeslizando = true;
                else if (h == ParamCorriendo) tieneCorriendo = true;
                else if (h == ParamSaltar) tieneSaltar = true;
                else if (h == ParamVelocidadAnim) tieneVelocidadAnim = true;
                else if (h == ParamAgachado) tieneAgachado = true;
            }
        }

        ultimoSaltoAplicado = personaje != null ? personaje.ContadorSaltosAnim : 0;
    }

    private bool PuedeAnimar()
    {
        // En red solo el dueño escribe parámetros; sin spawn (escena de prueba) anima en local.
        if (IsSpawned) return IsOwner;
        return true;
    }

    private void Update()
    {
        if (!PuedeAnimar()) return;
        if (animator == null || !animator.isActiveAndEnabled || animator.runtimeAnimatorController == null) return;

        if (personaje != null)
        {
            if (personaje.EsperandoControlAnim)
            {
                AplicarParametros(0f, true, false, false, false, 1f);
                if (tieneSaltar) animator.ResetTrigger(ParamSaltar);
                ultimoSaltoAplicado = personaje.ContadorSaltosAnim;
                return;
            }

            AplicarParametros(
                personaje.MovimientoAnim,
                personaje.EnSuelo,
                personaje.EstaDeslizando,
                personaje.EstaCorriendoAnim,
                personaje.EstaAgachado,
                personaje.VelocidadCarreraAnim);

            int saltoActual = personaje.ContadorSaltosAnim;
            if (tieneSaltar && saltoActual != ultimoSaltoAplicado)
            {
                ultimoSaltoAplicado = saltoActual;
                animator.ResetTrigger(ParamSaltar);
                animator.SetTrigger(ParamSaltar);
            }
            return;
        }

        // Respaldo para escenas de prueba sin componente Personaje
        if (Keyboard.current == null) return;
        bool enSuelo = controlador != null && controlador.isGrounded;
        float objetivoMov = (AjustesJuego.Presionada(Accion.Izquierda) || AjustesJuego.Presionada(Accion.Derecha)) ? 1f : 0f;
        movFallback = Mathf.MoveTowards(movFallback, objetivoMov, (objetivoMov > 0.1f ? 32f : 18f) * Time.deltaTime);
        bool estaCorriendo = objetivoMov > 0.1f || movFallback > 0.1f;

        AplicarParametros(movFallback, enSuelo, false, estaCorriendo, false, 1f);

        if (tieneSaltar && enSuelo && AjustesJuego.RecienPresionada(Accion.Saltar))
        {
            animator.ResetTrigger(ParamSaltar);
            animator.SetTrigger(ParamSaltar);
        }
    }

    private void AplicarParametros(float mov, bool enSuelo, bool deslizando, bool corriendo, bool agachado, float velAnim)
    {
        if (tieneMovimiento) animator.SetFloat(ParamMovimiento, mov);
        if (tieneSuelo) animator.SetBool(ParamSuelo, enSuelo);
        if (tieneDeslizando) animator.SetBool(ParamDeslizando, deslizando);
        if (tieneCorriendo) animator.SetBool(ParamCorriendo, corriendo);
        if (tieneAgachado) animator.SetBool(ParamAgachado, agachado);
        if (tieneVelocidadAnim) animator.SetFloat(ParamVelocidadAnim, velAnim);
    }
}
