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
    public static void iniciarHost()
    {
        // crea el host de la secion, y se encarga de iniciar la secion de red

        if(NetworkManager.Singleton != null)
        {
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
            // se asigna una sala con los maximos jugadores 
            Allocation asignacion=await RelayService.Instance.CreateAllocationAsync(maxPlayers); 
            string codigoSala=await RelayService.Instance.GetJoinCodeAsync(asignacion.AllocationId); // codigo

            // configuracion de la secion de red
            NetworkManager.Singleton.GetComponent<UnityTransport>().SetRelayServerData(
                asignacion.RelayServer.IpV4,(ushort)asignacion.RelayServer.Port,asignacion.AllocationIdBytes,asignacion.Key,asignacion.ConnectionData
            );

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
            // asignacin del cliente usando el codigo
            JoinAllocation asignacion=await RelayService.Instance.JoinAllocationAsync(codigoSala);

            NetworkManager.Singleton.GetComponent<UnityTransport>().SetRelayServerData(
                asignacion.RelayServer.IpV4,(ushort)asignacion.RelayServer.Port,asignacion.AllocationIdBytes,asignacion.Key,asignacion.ConnectionData,asignacion.HostConnectionData
            );

            return NetworkManager.Singleton.StartClient(); // se incia el cliente
        }
        catch(Exception e)
        {
            Debug.Log($"Ha ocurrido un error: {e}");
            return false;
        }
    }
}