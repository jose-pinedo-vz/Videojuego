using Unity.Netcode;
using UnityEngine;

public class JugadorMovimiento : NetworkBehaviour
{
    [SerializeField] private float velocidad = 5f;
    private CharacterController controller;

    private void Awake()
    {
        controller = GetComponent<CharacterController>();
    }

    void Start()
    {
        // Pega este bloque aquí adentro
        Rigidbody rb = GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;
        }
    }

    private void Update()
    {
        // VITAL: Si no soy el dueño de este personaje, no ejecuto el input del teclado
        if (!IsOwner) return;

        float moveX = Input.GetAxis("Horizontal");
        float moveZ = Input.GetAxis("Vertical");

        Vector3 movimiento = transform.right * moveX + transform.forward * moveZ;
        
        if (controller != null)
        {
            controller.Move(movimiento * velocidad * Time.deltaTime);
        }
        else
        {
            transform.Translate(movimiento * velocidad * Time.deltaTime, Space.World);
        }
    }
}