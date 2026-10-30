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

    // Prende o apaga los textos del HUD, lo llama inicio al empezar y terminar la partida
    public void Mostrar(bool visible)
    {
        if (txt_municion != null) txt_municion.gameObject.SetActive(visible);
        if (txt_arma != null) txt_arma.gameObject.SetActive(visible);
        if (txt_marcador != null) txt_marcador.gameObject.SetActive(visible);
    }

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
    if (jugadores == null || jugadores.Length == 0)
    {
        txt_marcador.text = "Buscando jugadores...";
        return;
    }

    // Ordenar de mayor a menor bajas de forma segura
    System.Array.Sort(jugadores, (a, b) => {
        int bajasA = (a != null && a.bajas != null) ? a.bajas.Value : 0;
        int bajasB = (b.bajas != null) ? b.bajas.Value : 0;
        return bajasB.CompareTo(bajasA);
    });

    string texto = "";
    foreach (Personaje p in jugadores)
    {
        if (p == null) continue;
        Red r = p.GetComponent<Red>();
        string nombreJugador = (r != null && r.nombreJugador != null) ? r.nombreJugador.Value.ToString() : p.name;
        int bajasVal = (p.bajas != null) ? p.bajas.Value : 0;
        texto += nombreJugador + "  -  " + bajasVal + " bajas\n";
    }
    txt_marcador.text = texto;
}
}
