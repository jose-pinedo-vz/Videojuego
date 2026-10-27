using UnityEngine;
using Unity.Netcode;
using System.Collections.Generic;

// Gestiona el spawn aleatorio de armas en el mapa.
// Para agregar armas nuevas: colocar el prefab en la escena y arrastrarlo al arreglo
// todasLasArmas en el Inspector. No hay que tocar este script.
// Máximo maxArmasActivas armas visibles a la vez, elegidas aleatoriamente al iniciar partida.
public class GestorArmas : NetworkBehaviour
{
    [Header("Configuración")]
    [Tooltip("Cuántas armas pueden estar en el suelo al mismo tiempo")]
    [SerializeField] private int maxArmasActivas = 4;

    [Header("Armas disponibles — arrastrar todas las que haya en la escena")]
    [SerializeField] private Arma[] todasLasArmas;

    [Header("Puntos de spawn de armas — deben ser distintos a los de jugadores")]
    [SerializeField] private Transform[] puntosSpawnArmas;

    // spawn que le tocó a cada arma en esta partida
    private Dictionary<string, Vector3> spawnAsignado = new Dictionary<string, Vector3>();

    public static GestorArmas Instancia { get; private set; }

    private void Awake()
    {
        Instancia = this;
    }

    // Llamado por GameManager.iniciarPartida() en el servidor
    public void IniciarRondaArmas()
    {
        if (!IsServer) return;

        // Ocultar todas
        foreach (Arma a in todasLasArmas)
            if (a != null) a.gameObject.SetActive(false);

        spawnAsignado.Clear();

        // Mezclar armas y spawns para aleatoriedad por partida
        List<Arma> disponibles = new List<Arma>(todasLasArmas);
        List<Transform> spawns    = new List<Transform>(puntosSpawnArmas);
        Mezclar(disponibles);
        Mezclar(spawns);

        int cantidad = Mathf.Min(maxArmasActivas, disponibles.Count, spawns.Count);
        for (int i = 0; i < cantidad; i++)
        {
            if (disponibles[i] == null || spawns[i] == null) continue;
            string nombre = disponibles[i].gameObject.name;
            Vector3 pos   = spawns[i].position;
            spawnAsignado[nombre] = pos;
            // Decirle a todos los clientes que activen esta arma en esa posición
            ActualizarArmaRpc(nombre, pos, true);
        }
    }

    // Llamado desde Personaje (via RPC al servidor) cuando el jugador muere
    public void SoltarArma(string nombreArma)
    {
        if (!IsServer) return;

        Vector3 pos;
        if (!spawnAsignado.TryGetValue(nombreArma, out pos))
        {
            // Si por alguna razón no está registrada, no se reactiva
            Debug.LogWarning($"GestorArmas: no hay spawn registrado para '{nombreArma}'");
            return;
        }
        ActualizarArmaRpc(nombreArma, pos, true);
    }

    // Activa o desactiva un arma en todos los clientes y la mueve a la posición indicada
    [Rpc(SendTo.ClientsAndHost)]
    private void ActualizarArmaRpc(string nombreArma, Vector3 posicion, bool activar)
    {
        Arma arma = BuscarArmaPorNombre(nombreArma);
        if (arma == null)
        {
            Debug.LogWarning($"GestorArmas: no se encontró el arma '{nombreArma}'");
            return;
        }

        if (activar)
        {
            arma.Soltar(posicion);          // desancla del jugador si estaba pegada
            arma.gameObject.SetActive(true);
        }
        else
        {
            arma.gameObject.SetActive(false);
        }
    }

    private Arma BuscarArmaPorNombre(string nombre)
    {
        foreach (Arma a in todasLasArmas)
            if (a != null && a.gameObject.name == nombre) return a;
        return null;
    }

    // Mezcla Fisher-Yates
    private static void Mezclar<T>(List<T> lista)
    {
        for (int i = lista.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            (lista[i], lista[j]) = (lista[j], lista[i]);
        }
    }
}
