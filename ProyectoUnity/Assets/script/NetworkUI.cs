using Unity.Netcode;
using UnityEngine;

public class NetworkUI : MonoBehaviour
{
    private void OnGUI()
    {
        if (NetworkManager.Singleton == null) return;

        GUILayout.BeginArea(new Rect(10, 10, 300, 300));
        bool isConnected = NetworkManager.Singleton.IsClient || NetworkManager.Singleton.IsServer;

        if (!isConnected)
        {
            if (GUILayout.Button("Host"))
            {
                NetworkManager.Singleton.StartHost();
            }
            if (GUILayout.Button("Server"))
            {
                NetworkManager.Singleton.StartServer();
            }
            if (GUILayout.Button("Client"))
            {
                NetworkManager.Singleton.StartClient();
            }
        }
        else
        {
            GUILayout.Label("Estado: Conectado como " + 
                (NetworkManager.Singleton.IsHost ? "Host" : 
                (NetworkManager.Singleton.IsServer ? "Server" : "Client")));
        }
        GUILayout.EndArea();
    }
}