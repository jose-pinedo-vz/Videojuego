using UnityEngine;
using TMPro;

// Muestra en pantalla la municion y el arma del jugador local, ademas de un marcador
// con los jugadores conectados y sus bajas. Los campos de texto se asignan en el
// Inspector sobre el Canvas del juego.
public class HUD : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI txt_municion;
    [SerializeField] private TextMeshProUGUI txt_arma;
    [SerializeField] private TextMeshProUGUI txt_marcador;

    private Personaje jugadorLocal;

    private void Update()
    {
        if (jugadorLocal == null || !jugadorLocal.IsSpawned)
        {
            jugadorLocal = BuscarJugadorLocal();
        }

        ActualizarMunicionYArma();
        ActualizarMarcador();
    }

    private Personaje BuscarJugadorLocal()
    {
        foreach (Personaje p in FindObjectsByType<Personaje>(FindObjectsSortMode.None))
        {
            if (p.IsOwner) return p;
        }
        return null;
    }

    private void ActualizarMunicionYArma()
    {
        Arma arma = jugadorLocal != null ? jugadorLocal.armaEquipada : null;

        if (txt_arma != null)
            txt_arma.text = arma != null ? arma.NombreMostrado : "Sin arma";

        if (txt_municion != null)
        {
            txt_municion.text = arma == null
                ? "- / -"
                : arma.Recargando
                    ? "Recargando..."
                    : arma.MunicionActual + " / " + arma.MunicionMaxima;
        }
    }

    // Lista de jugadores ordenada por bajas, se actualiza en vivo durante la partida
    private void ActualizarMarcador()
    {
        if (txt_marcador == null) return;

        Personaje[] jugadores = FindObjectsByType<Personaje>(FindObjectsSortMode.None);
        System.Array.Sort(jugadores, (a, b) => b.bajas.Value.CompareTo(a.bajas.Value));

        string texto = "";
        foreach (Personaje p in jugadores)
        {
            Red r = p.GetComponent<Red>();
            string nombreJugador = r != null ? r.nombreJugador.Value.ToString() : p.name;
            texto += nombreJugador + "  -  " + p.bajas.Value + " bajas\n";
        }
        txt_marcador.text = texto;
    }
}
