using UnityEngine;
using Unity.Netcode;
using Unity.Netcode.Components;
using Unity.Collections;
using TMPro;

public class Red : NetworkBehaviour
{
    public void MostrarJugador(bool visible)
    {
        foreach (Renderer r in GetComponentsInChildren<Renderer>(true))
        {
            r.enabled = visible;
        }
    }

    // Solo el servidor escribe, todos leen
    public NetworkVariable<FixedString32Bytes> nombreJugador = new NetworkVariable<FixedString32Bytes>(
        default,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);

    [SerializeField] private TextMeshPro textoNombre; // TMP 3D hijo del prefab

    private CharacterController cc;
    private NetworkTransform netTransform;

    private void Awake()
    {
        cc = GetComponent<CharacterController>();
        netTransform = GetComponent<NetworkTransform>();
    }

    public override void OnNetworkSpawn()
    {
         MostrarJugador(false);   // <-- NUEVA LÍNEA: el jugador nace invisible

        //nombreJugador.OnValueChanged += (viejo, nuevo) => ActualizarTextoNombre();

        nombreJugador.OnValueChanged += (viejo, nuevo) => ActualizarTextoNombre();
        ActualizarTextoNombre(); // por si el valor ya llegó

        // El dueño se registra ante el servidor (nombre + pedir spawn)
        if (IsOwner)
        {
            RegistrarJugadorServerRpc(string.IsNullOrEmpty(inicio.nombre) ? "Jugador" : inicio.nombre);
        }
    }

    private void ActualizarTextoNombre()
    {
        if (textoNombre != null) textoNombre.text = nombreJugador.Value.ToString();
    }

    private void LateUpdate()
    {
        // Billboard: el texto siempre mira hacia la cámara local
        if (textoNombre != null && Camera.main != null)
        {
            textoNombre.transform.forward = Camera.main.transform.forward;
        }
    }

    [Rpc(SendTo.Server)]
    private void RegistrarJugadorServerRpc(string nombre)
    {
        nombreJugador.Value = nombre;

        GameManager gm = FindFirstObjectByType<GameManager>();
        if (gm != null)
        {
            gm.agregarNombre(nombre);
            TeleportDuenioRpc(gm.AsignarSpawn());
        }
        else
        {
            Debug.LogError("No se encontró el GameManager");
        }
    }

    // Se ejecuta SOLO en el dueño (que es quien tiene autoridad del NetworkTransform)
    [Rpc(SendTo.Owner)]
    private void TeleportDuenioRpc(Vector3 posicion)
    {
        if (cc != null) cc.enabled = false;

        transform.position = posicion;
        if (netTransform != null)
            netTransform.Teleport(posicion, transform.rotation, transform.localScale);

        if (cc != null) cc.enabled = true;
    }
}