using UnityEngine;
using Unity.Netcode;

public class CamaraSeguir : NetworkBehaviour
{
    [SerializeField] private float alturaExtra = 1f;
    [SerializeField] private float suavizado = 8f;

    private Transform cam;
    private float zOriginal;

    public override void OnNetworkSpawn()
    {
        // Si este personaje no es mio, no toco la cámara
        if (!IsOwner) { enabled = false; return; }

        if (Camera.main != null)
        {
            cam = Camera.main.transform;
            zOriginal = cam.position.z;   // la camara conserva su distancia actual
        }
    }

    private void LateUpdate()
    {
        if (cam == null) return;

        Vector3 destino = new Vector3(transform.position.x, transform.position.y + alturaExtra, zOriginal);
        cam.position = Vector3.Lerp(cam.position, destino, suavizado * Time.deltaTime);
    }
}