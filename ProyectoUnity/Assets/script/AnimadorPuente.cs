using UnityEngine;
using UnityEngine.InputSystem;
using Unity.Netcode;

public class AnimadorPuente : NetworkBehaviour
{
    private Animator animator;
    private CharacterController controlador;
    private Personaje personaje;

    private void Awake()
    {
        animator = GetComponent<Animator>();
        if (animator == null) animator = GetComponentInChildren<Animator>();
        controlador = GetComponent<CharacterController>();
        personaje = GetComponent<Personaje>();
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
        if (animator == null) return;
        if (Keyboard.current == null) return;

        bool enSuelo = personaje != null
            ? personaje.EnSuelo
            : (controlador != null && controlador.isGrounded);

        bool deslizando = personaje != null && personaje.EstaDeslizando;

        float mov = 0f;
        if (Keyboard.current.aKey.isPressed || Keyboard.current.dKey.isPressed || deslizando)
        {
            mov = 1f;
        }

        // Indica si el personaje está corriendo (en movimiento horizontal)
        bool estaCorriendo = (Keyboard.current.aKey.isPressed || Keyboard.current.dKey.isPressed) && !deslizando;

        animator.SetFloat("movimiento", mov);
        animator.SetBool("EstaEnSuelo", enSuelo);
        animator.SetBool("estaDeslizando", deslizando);
        animator.SetBool("estaCorriendo", estaCorriendo);

        // Solo saltar si está en suelo, no está deslizando, y presiona espacio
        if (enSuelo && !deslizando && Keyboard.current.spaceKey.wasPressedThisFrame)
        {
            animator.SetTrigger("Saltar");
        }
    }
}
