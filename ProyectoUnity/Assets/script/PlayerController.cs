using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerController : NetworkBehaviour
{
    [Header("Movimiento")]
    [SerializeField] private float speed = 6f;
    [SerializeField] private float gravity = -9.81f;

    [Header("Cámara y Ratón")]
    [SerializeField] private float mouseSensitivity = 0.15f;
    private Transform cameraTransform;
    private float xRotation = 0f;

    private CharacterController controller;
    private Vector3 velocity;

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        // Si este personaje pertenece a este cliente, configuramos la cámara
        if (IsOwner)
        {
            Camera mainCam = Camera.main;
            if (mainCam != null)
            {
                cameraTransform = mainCam.transform;
                // Posicionar la cámara a la altura de los ojos de la cápsula
                cameraTransform.SetParent(transform);
                cameraTransform.localPosition = new Vector3(0f, 0.6f, 0f);
                cameraTransform.localRotation = Quaternion.identity;
            }

            // Bloquear el cursor al centro de la pantalla para jugar cómodo
            Cursor.lockState = CursorLockMode.Locked;
        }
    }

    private void Update()
    {
        // Solo el dueño de este personaje debe controlarlo
        if (!IsOwner) return;

        // --- 1. ROTACIÓN CON EL RATÓN ---
        if (Mouse.current != null && cameraTransform != null)
        {
            Vector2 mouseDelta = Mouse.current.delta.ReadValue();
            float mouseX = mouseDelta.x * mouseSensitivity;
            float mouseY = mouseDelta.y * mouseSensitivity;

            xRotation -= mouseY;
            xRotation = Mathf.Clamp(xRotation, -80f, 80f); // Limitar ángulo vertical

            cameraTransform.localRotation = Quaternion.Euler(xRotation, 0f, 0f);
            transform.Rotate(Vector3.up * mouseX);
        }

        // --- 2. MOVIMIENTO CON TECLADO ---
        float x = 0f;
        float z = 0f;
        float y=0f;

        if (Keyboard.current != null)
        {
            if (Keyboard.current.wKey.isPressed) z += 1f;
            if (Keyboard.current.sKey.isPressed) z -= 1f;
            if (Keyboard.current.dKey.isPressed) x += 1f;
            if (Keyboard.current.aKey.isPressed) x -= 1f;

            if (Keyboard.current.spaceKey.wasPressedThisFrame)  // salto
            {
                float jumpHeight = 2f; 
                velocity.y = Mathf.Sqrt(jumpHeight * -2f * gravity);
            }

        }

        Vector3 move = transform.right * x + transform.forward * z;
        controller.Move(move.normalized * speed * Time.deltaTime);

        // --- 3. GRAVEDAD ---
        if (controller.isGrounded && velocity.y < 0)
        {
            velocity.y = -2f;
        }

        velocity.y += gravity * Time.deltaTime;
        controller.Move(velocity * Time.deltaTime);
    }

    private void Awake()
    {
        controller = GetComponent<CharacterController>();
    }
}