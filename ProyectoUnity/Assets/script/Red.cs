using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;
using Unity.Netcode.Components;
using Unity.Collections;
using TMPro;

[DefaultExecutionOrder(20)]
public class Red : NetworkBehaviour
{
    private static readonly HashSet<int> personajesUsadosOffline = new HashSet<int>();

    private bool jugadorVisible = true;
    private int indicePersonajeAplicado = -1;

    public void MostrarJugador(bool visible)
    {
        jugadorVisible = visible;
        foreach (Renderer r in GetComponentsInChildren<Renderer>(true))
        {
            if (r == null) continue;
            if (r is LineRenderer || r is ParticleSystemRenderer) continue;

            Arma armaPadre = r.GetComponentInParent<Arma>();
            if (armaPadre != null)
            {
                if (!visible) r.enabled = false;
                else armaPadre.ColocarEnCaja(armaPadre.EnCaja);
                continue;
            }

            r.enabled = visible;
        }
    }

    // Solo el servidor escribe, todos leen
    public NetworkVariable<FixedString32Bytes> nombreJugador = new NetworkVariable<FixedString32Bytes>(
        default,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);

    // Índice del personaje asignado aleatoriamente sin repetir (0: Cheto, 1: Kaiser, 2: Dorito, 3: CR7)
    public NetworkVariable<int> indicePersonaje = new NetworkVariable<int>(
        -1,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);

    [SerializeField] private TextMeshPro textoNombre; // TMP 3D hijo del prefab

    private CharacterController cc;
    private NetworkTransform netTransform;

    public int IndicePersonajeActual => indicePersonaje.Value >= 0 ? indicePersonaje.Value : indicePersonajeAplicado;
    public string SubnombrePersonaje => CatalogoPersonajes.ObtenerSubnombre(IndicePersonajeActual);

    public string ObtenerEtiquetaJugador()
    {
        string enRed = nombreJugador != null ? nombreJugador.Value.ToString() : "";
        if (!string.IsNullOrEmpty(enRed))
        {
            return IndicePersonajeActual >= 0
                ? CatalogoPersonajes.FormatearNombreConSubnombre(enRed, IndicePersonajeActual)
                : enRed;
        }
        string baseLocal = string.IsNullOrWhiteSpace(inicio.nombre) ? "Jugador" : inicio.nombre;
        return CatalogoPersonajes.FormatearNombreConSubnombre(baseLocal, IndicePersonajeActual);
    }

    private void Awake()
    {
        cc = GetComponent<CharacterController>();
        netTransform = GetComponent<NetworkTransform>();
        AsegurarEtiquetaNombre3D();
    }

    private void Start()
    {
        // En escenas de prueba sin red activa, asignar también uno de los 4 personajes al azar sin repetir
        if (!IsSpawned && (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsListening))
        {
            jugadorVisible = true;
            int idxLocal = ElegirPersonajeOfflineUnico();
            AplicarPersonajeVisual(idxLocal);
            if (textoNombre != null)
            {
                string baseLocal = string.IsNullOrWhiteSpace(inicio.nombre) ? "Jugador" : inicio.nombre;
                textoNombre.text = CatalogoPersonajes.FormatearNombreConSubnombre(baseLocal, idxLocal);
            }
        }
    }

    private static int ElegirPersonajeOfflineUnico()
    {
        if (personajesUsadosOffline.Count >= CatalogoPersonajes.TOTAL_PERSONAJES)
            personajesUsadosOffline.Clear();

        List<int> libres = new List<int>();
        for (int i = 0; i < CatalogoPersonajes.TOTAL_PERSONAJES; i++)
        {
            if (!personajesUsadosOffline.Contains(i)) libres.Add(i);
        }

        int elegido = libres.Count > 0 ? libres[Random.Range(0, libres.Count)] : Random.Range(0, CatalogoPersonajes.TOTAL_PERSONAJES);
        personajesUsadosOffline.Add(elegido);
        return elegido;
    }

    private void AsegurarEtiquetaNombre3D()
    {
        if (textoNombre != null) return;

        Transform existente = transform.Find("EtiquetaNombre3D");
        if (existente != null)
        {
            textoNombre = existente.GetComponent<TextMeshPro>();
            if (textoNombre != null) return;
        }

        GameObject goTexto = new GameObject("EtiquetaNombre3D");
        goTexto.transform.SetParent(transform, false);
        goTexto.transform.localPosition = new Vector3(0f, 0.78f, 0f);
        goTexto.transform.localScale = Vector3.one;

        textoNombre = goTexto.AddComponent<TextMeshPro>();
        textoNombre.fontSize = 1.45f;
        textoNombre.fontStyle = FontStyles.Bold;
        textoNombre.alignment = TextAlignmentOptions.Center;
        textoNombre.textWrappingMode = TextWrappingModes.NoWrap;
        textoNombre.color = new Color(1f, 0.97f, 0.86f, 1f);
        textoNombre.outlineWidth = 0.22f;
        textoNombre.outlineColor = new Color32(25, 20, 18, 255);
        textoNombre.raycastTarget = false;

        RectTransform rt = textoNombre.GetComponent<RectTransform>();
        if (rt != null) rt.sizeDelta = new Vector2(4f, 0.6f);

        MeshRenderer mr = textoNombre.GetComponent<MeshRenderer>();
        if (mr != null) mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
    }

    public override void OnNetworkSpawn()
    {
        MostrarJugador(false); // el jugador nace invisible hasta que inicia la partida

        nombreJugador.OnValueChanged += AlCambiarNombre;
        indicePersonaje.OnValueChanged += AlCambiarPersonaje;

        if (indicePersonaje.Value >= 0)
        {
            AplicarPersonajeVisual(indicePersonaje.Value);
        }

        ActualizarTextoNombre(); // por si el valor ya llegó

        // El dueño se registra ante el servidor (nombre + pedir personaje único + pedir spawn)
        if (IsOwner)
        {
            RegistrarJugadorServerRpc(string.IsNullOrWhiteSpace(inicio.nombre) ? "Jugador" : inicio.nombre.Trim());
        }
    }

    public override void OnNetworkDespawn()
    {
        nombreJugador.OnValueChanged -= AlCambiarNombre;
        indicePersonaje.OnValueChanged -= AlCambiarPersonaje;
    }

    public override void OnDestroy()
    {
        if (indicePersonajeAplicado >= 0)
        {
            personajesUsadosOffline.Remove(indicePersonajeAplicado);
        }
        base.OnDestroy();
    }

    private void AlCambiarNombre(FixedString32Bytes viejo, FixedString32Bytes nuevo)
    {
        ActualizarTextoNombre();
    }

    private void AlCambiarPersonaje(int viejo, int nuevo)
    {
        if (nuevo >= 0)
        {
            AplicarPersonajeVisual(nuevo);
            ActualizarTextoNombre();
        }
    }

    private void AplicarPersonajeVisual(int indice)
    {
        if (indice < 0) return;
        indicePersonajeAplicado = indice;

        CatalogoPersonajes catalogo = CatalogoPersonajes.Obtener();
        if (catalogo != null)
        {
            catalogo.AplicarPersonajeA(gameObject, indice, IsSpawned, jugadorVisible);
        }
    }

    private void ActualizarTextoNombre()
    {
        if (textoNombre == null) return;

        string texto = nombreJugador.Value.ToString();
        if (string.IsNullOrEmpty(texto) && IndicePersonajeActual >= 0)
        {
            texto = CatalogoPersonajes.FormatearNombreConSubnombre("Jugador", IndicePersonajeActual);
        }
        else if (!string.IsNullOrEmpty(texto) && IndicePersonajeActual >= 0)
        {
            texto = CatalogoPersonajes.FormatearNombreConSubnombre(texto, IndicePersonajeActual);
        }

        textoNombre.text = texto;
    }

    private void LateUpdate()
    {
        // Billboard: el texto siempre mira hacia la cámara local sin espejearse al girar el personaje
        if (textoNombre != null && Camera.main != null)
        {
            textoNombre.transform.rotation = Camera.main.transform.rotation;
        }
    }

    [Rpc(SendTo.Server)]
    private void RegistrarJugadorServerRpc(string nombre)
    {
        GameManager gm = FindFirstObjectByType<GameManager>();
        int idxPersonaje = gm != null
            ? gm.AsignarPersonajeUnico(OwnerClientId)
            : Random.Range(0, CatalogoPersonajes.TOTAL_PERSONAJES);

        indicePersonaje.Value = idxPersonaje;

        string nombreFormateado = CatalogoPersonajes.FormatearNombreConSubnombre(nombre, idxPersonaje);
        nombreJugador.Value = nombreFormateado;

        if (gm != null)
        {
            gm.RegistrarJugadorEnSala(OwnerClientId, nombreFormateado);
            TeleportDuenioRpc(gm.AsignarSpawn());
        }
        else
        {
            Debug.LogError("No se encontró el GameManager");
        }
    }

    // Se ejecuta SOLO en el dueño (que es quien tiene autoridad del NetworkTransform)
    [Rpc(SendTo.Owner)]
    public void TeleportDuenioRpc(Vector3 posicion)
    {
        if (cc != null) cc.enabled = false;

        transform.position = posicion;
        if (netTransform != null)
            netTransform.Teleport(posicion, transform.rotation, transform.localScale);

        if (cc != null) cc.enabled = true;
        Personaje p = GetComponent<Personaje>();
        if (p != null) p.FijarSpawn(posicion);
    }

    [Rpc(SendTo.NotOwner)]
    public void AvisarArmaRpc(string nombreArma)
    {
        // se busca por nombre porque las armas no son NetworkObject; cada arma en la escena debe tener nombre unico
        foreach (Arma arma in FindObjectsByType<Arma>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (arma.name == nombreArma)
            {
                arma.AdjuntarA(transform.Find("Mano"), GetComponent<Personaje>());
                return;
            }
        }
        Debug.LogWarning("No se encontro el arma: " + nombreArma);
    }

    [Rpc(SendTo.NotOwner)]
    public void AvisarRomperCajaRpc(string nombreArma)
    {
        foreach (Arma arma in FindObjectsByType<Arma>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (arma.name == nombreArma)
            {
                arma.AbrirCajaLocal();
                return;
            }
        }
    }

    [Rpc(SendTo.Server)]
    public void SoltarArmaEnPosicionServerRpc(string nombreArma, Vector3 posicion, bool enCaja)
    {
        GestorArmas ga = GestorArmas.Instancia;
        if (ga != null && ga.IsSpawned)
        {
            ga.SoltarArmaEnPosicion(nombreArma, posicion, enCaja);
        }
        else
        {
            SoltarArmaClientesRpc(nombreArma, posicion, enCaja);
        }
    }

    [Rpc(SendTo.ClientsAndHost)]
    private void SoltarArmaClientesRpc(string nombreArma, Vector3 posicion, bool enCaja)
    {
        foreach (Arma arma in FindObjectsByType<Arma>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (arma.name == nombreArma)
            {
                arma.ColocarEnCaja(enCaja);
                arma.Soltar(posicion);
                arma.gameObject.SetActive(true);
                return;
            }
        }
    }
}
