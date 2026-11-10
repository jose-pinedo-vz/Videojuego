using UnityEngine;

/// <summary>
/// Reglas de la partida que elige el host en la pantalla de configuracion antes de crear la sala.
/// Solo importa el valor del host: el servidor (GameManager) las aplica y la sala de Relay se crea con el maximo de jugadores.
/// Los clientes ven el tiempo por la NetworkVariable de GameManager, no necesitan estos valores.
/// </summary>
public static class ConfiguracionPartida
{
    public const int DURACION_MIN = 30;     // segundos por mapa
    public const int DURACION_MAX = 300;
    public const int DURACION_PASO = 15;

    public const int JUGADORES_MIN = 2;
    public const int JUGADORES_MAX = 4;

    public static int duracionSegundos = 60;      // por cada mapa
    public static int cantidadMapas = 0;          // 0 = todos los mapas
    public static int maxJugadores = JUGADORES_MAX;

    // cuantos mapas se juegan de verdad, dado cuantos existen en la escena
    public static int MapasAJugar(int mapasDisponibles)
    {
        int n = cantidadMapas <= 0 ? mapasDisponibles : cantidadMapas;
        return Mathf.Clamp(n, 1, Mathf.Max(1, mapasDisponibles));
    }

    public static void Restablecer()
    {
        duracionSegundos = 60;
        cantidadMapas = 0;
        maxJugadores = JUGADORES_MAX;
    }

    public static string FormatearDuracion(int segundos)
    {
        return (segundos / 60) + ":" + (segundos % 60).ToString("00");
    }
}
