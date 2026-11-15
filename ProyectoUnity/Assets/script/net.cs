using UnityEngine;
using Unity.Netcode;
using System;
using Unity.Netcode.Transports.UTP;
using Unity.Services.Core;
using Unity.Services.Authentication;
using Unity.Services.Relay;
using Unity.Services.Relay.Models;

using System.Threading.Tasks;

public class NetWork:MonoBehaviour
{
    // Hashes de GameManager en Juego.unity y SetGrabacion.unity (por si SetGrabacion se re-guarda en Editor)
    private const uint HashGameManagerJuego = 519549695u;
    private const uint HashGameManagerSetGrabacion = 3978322295u;
    private static readonly ManejadorGameManagerEscena ManejadorGameManager = new ManejadorGameManagerEscena();

    private void Awake()
    {
        ConfigurarRedMultiEscena();
    }

    private void Start()
    {
        ConfigurarRedMultiEscena();
    }

    /// <summary>
    /// Desactiva la sincronizacion forzada de escenas de Netcode y enlaza el GameManager
    /// local de la escena activa para poder jugar entre Juego.unity y SetGrabacion.unity.
    /// </summary>
    public static void ConfigurarRedMultiEscena()
    {
        NetworkManager nm = NetworkManager.Singleton;
        if (nm == null)
        {
            nm = FindAnyObjectByType<NetworkManager>(FindObjectsInactive.Include);
        }
        if (nm == null) return;

        if (!nm.IsListening && nm.NetworkConfig != null)
        {
            nm.NetworkConfig.EnableSceneManagement = false;
        }

        if (nm.PrefabHandler == null) return;

        nm.PrefabHandler.AddHandler(HashGameManagerJuego, ManejadorGameManager);
        nm.PrefabHandler.AddHandler(HashGameManagerSetGrabacion, ManejadorGameManager);

        GameManager gmLocal = FindAnyObjectByType<GameManager>(FindObjectsInactive.Include);
        if (gmLocal != null && gmLocal.TryGetComponent(out NetworkObject gmNetObj) && gmNetObj.PrefabIdHash != 0)
        {
            nm.PrefabHandler.AddHandler(gmNetObj.PrefabIdHash, ManejadorGameManager);
        }
    }

    private sealed class ManejadorGameManagerEscena : INetworkPrefabInstanceHandler
    {
        public NetworkObject Instantiate(ulong ownerClientId, Vector3 position, Quaternion rotation)
        {
            GameManager gm = UnityEngine.Object.FindAnyObjectByType<GameManager>(FindObjectsInactive.Include);
            if (gm == null)
            {
                Debug.LogError("[NetWork] No se encontro GameManager en la escena activa.");
                return null;
            }

            if (!gm.gameObject.activeSelf)
            {
                gm.gameObject.SetActive(true);
            }

            return gm.GetComponent<NetworkObject>();
        }

        public void Destroy(NetworkObject networkObject)
        {
            // Se conserva el GameManager local de la escena porque guarda referencias de UI y mapa.
        }
    }

    public static void iniciarHost()
    {
        // crea el host de la secion, y se encarga de iniciar la secion de red

        if(NetworkManager.Singleton != null)
        {
            ConfigurarRedMultiEscena();
            NetworkManager.Singleton.StartHost();
            Debug.Log("Secion iniciada");
        }
        else
        {
            Debug.Log("No se encontro el host");
        }
    }


    // se encarga de iniciar la secion de red y crear la sala
    // genera el codigo de acceso
    // Task<string> es el valor que devuelve una funcion asincrona 
    public async Task<string> iniciarRelay(int maxPlayers=4)
    {
        try
        {
            ConfigurarRedMultiEscena();

            // se asigna una sala con los maximos jugadores 
            // el numero que pide Relay son las conexiones de clientes, sin contar al host
            Allocation asignacion=await RelayService.Instance.CreateAllocationAsync(Mathf.Max(1, maxPlayers - 1)); 
            string codigoSala=await RelayService.Instance.GetJoinCodeAsync(asignacion.AllocationId); // codigo

            // configuracion de la secion de red
            NetworkManager.Singleton.GetComponent<UnityTransport>().SetRelayServerData(
                asignacion.RelayServer.IpV4,(ushort)asignacion.RelayServer.Port,asignacion.AllocationIdBytes,asignacion.Key,asignacion.ConnectionData
            );

            ConfigurarRedMultiEscena();
            NetworkManager.Singleton.StartHost(); // se incia el host
            Debug.Log($"Secion iniciada, el codigo de la sala es: {codigoSala}");
            return codigoSala;
        }
        catch(Exception e)
        {
            Debug.Log($"Ha ocurrido un error: {e}");
            return null;
        }
    }


    // conectar el cliente con el host con el codigo propuesto por el jost
    public async Task<bool> unirseAlHost(string codigoSala)
    {
        try
        {
            ConfigurarRedMultiEscena();

            // asignacin del cliente usando el codigo
            JoinAllocation asignacion=await RelayService.Instance.JoinAllocationAsync(codigoSala);

            NetworkManager.Singleton.GetComponent<UnityTransport>().SetRelayServerData(
                asignacion.RelayServer.IpV4,(ushort)asignacion.RelayServer.Port,asignacion.AllocationIdBytes,asignacion.Key,asignacion.ConnectionData,asignacion.HostConnectionData
            );

            ConfigurarRedMultiEscena();
            return NetworkManager.Singleton.StartClient(); // se incia el cliente
        }
        catch(Exception e)
        {
            Debug.Log($"Ha ocurrido un error: {e}");
            return false;
        }
    }
}


