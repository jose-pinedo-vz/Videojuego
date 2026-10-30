using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

// Menu "Herramientas" del editor: crea los spawns que falten dentro del mapa seleccionado.
// Quedan en fila, despues hay que moverlos a su lugar en el mapa.
public static class CrearSpawns
{
    private const int JUGADORES = 8;
    private const int ARMAS = 6;
    private const float SEPARACION = 5f;

    [MenuItem("Herramientas/Crear spawns de jugador en el mapa seleccionado")]
    private static void CrearSpawnsJugador()
    {
        Crear(GameManager.PREFIJO_JUGADOR, JUGADORES);
    }

    [MenuItem("Herramientas/Crear spawns de armas en el mapa seleccionado")]
    private static void CrearSpawnsArmas()
    {
        Crear(GameManager.PREFIJO_ARMA, ARMAS);
    }

    private static void Crear(string prefijo, int total)
    {
        GameObject mapa = Selection.activeGameObject;
        if (mapa == null)
        {
            Debug.LogWarning("Primero selecciona el mapa en la Hierarchy");
            return;
        }

        List<Transform> existentes = GameManager.BuscarPuntos(mapa, prefijo);

        // Se sigue desde el ultimo para quedar en el mismo plano Z donde caminan los jugadores
        Vector3 posicion = existentes.Count > 0
            ? existentes[existentes.Count - 1].position
            : mapa.transform.position + new Vector3(-SEPARACION, 3f, 5.5f);

        for (int i = existentes.Count + 1; i <= total; i++)
        {
            posicion += Vector3.right * SEPARACION;

            GameObject punto = new GameObject(prefijo + i);
            Undo.RegisterCreatedObjectUndo(punto, "Crear spawn");
            punto.transform.SetParent(mapa.transform);
            punto.transform.position = posicion;
        }

        Debug.Log(mapa.name + ": " + Mathf.Max(total, existentes.Count) + " spawns con prefijo " + prefijo);
    }
}
