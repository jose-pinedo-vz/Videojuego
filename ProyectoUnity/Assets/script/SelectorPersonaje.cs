using UnityEngine;
using UnityEngine.UI;
using TMPro;

// Selector de personaje en el lobby (solo dueño local). Crea una fila de 4 botones dentro de la
// pantalla donde se ven los nombres: "CrearHost" (host) y "pantalla de carga" (invitados).
// Los personajes que ya tiene otro jugador salen bloqueados. El servidor valida el cambio.
public class SelectorPersonaje : MonoBehaviour
{
    private static readonly Color colorLibre   = new Color(0.165f, 0.188f, 0.259f);
    private static readonly Color colorMio     = new Color(1f, 0.624f, 0.11f);
    private static readonly Color colorOcupado = new Color(0.12f, 0.13f, 0.17f);

    private Red red;

    // Vista previa grande: un modelo en una zona lejana, visto por una camara que dibuja en una RenderTexture
    private static readonly Vector3 ZonaPrevia = new Vector3(5000f, 5000f, 5000f);
    private RenderTexture texturaPrevia;
    private Camera camaraPrevia;
    private Transform raizPrevia, modeloPrevio;
    private int indicePrevio = -1;
    private float tiempoCambio;
    // Giro extra en Y por personaje para que mire de frente a la camara (0: Cheto, 1: Kaiser, 2: Dorito, 3: CR7)
    private static readonly float[] GiroPrevio = { 0f, 0f, 0f, 0f };
    private readonly System.Collections.Generic.List<Fila> filas = new System.Collections.Generic.List<Fila>();

    private class Fila
    {
        public GameObject raiz;
        public Image[] fondos = new Image[CatalogoPersonajes.TOTAL_PERSONAJES];
        public TextMeshProUGUI[] textos = new TextMeshProUGUI[CatalogoPersonajes.TOTAL_PERSONAJES];
        public Button[] botones = new Button[CatalogoPersonajes.TOTAL_PERSONAJES];
        public TextMeshProUGUI nombrePrevio;
    }

    private void Awake()
    {
        red = GetComponent<Red>();
    }

    private void Start()
    {
        inicio menu = FindFirstObjectByType<inicio>(FindObjectsInactive.Include);
        if (menu == null) return;
        CrearVistaPrevia();
        CrearFila(menu.transform.Find("CrearHost"), -125f);
        CrearFila(menu.transform.Find("pantalla de carga"), -190f);
    }

    private void CrearFila(Transform panel, float y)
    {
        if (panel == null) return;

        // Ensanchar la tarjeta del panel: a la izquierda el modelo, a la derecha lo que ya habia
        float altoTarjeta;
        float xIzquierda = AmpliarTarjeta(panel, AnchoZonaModelo, out altoTarjeta);

        Fila f = new Fila();
        GameObject go = new GameObject("SelectorPersonaje", typeof(RectTransform));
        RectTransform rt = (RectTransform)go.transform;
        rt.SetParent(panel, false);
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = new Vector2(AnchoZonaModelo / 2f, y);
        rt.sizeDelta = new Vector2(640f, 56f);
        f.raiz = go;

        const int total = CatalogoPersonajes.TOTAL_PERSONAJES;
        float ancho = 150f, sep = 13f;
        float x0 = -(total * ancho + (total - 1) * sep) / 2f + ancho / 2f;

        for (int i = 0; i < total; i++)
        {
            int indice = i;
            GameObject b = new GameObject("Personaje_" + i, typeof(RectTransform), typeof(Image), typeof(Button));
            RectTransform brt = (RectTransform)b.transform;
            brt.SetParent(rt, false);
            brt.anchorMin = brt.anchorMax = brt.pivot = new Vector2(0.5f, 0.5f);
            brt.anchoredPosition = new Vector2(x0 + i * (ancho + sep), 0f);
            brt.sizeDelta = new Vector2(ancho, 52f);

            f.fondos[i] = b.GetComponent<Image>();
            f.botones[i] = b.GetComponent<Button>();
            f.botones[i].targetGraphic = f.fondos[i];
            f.botones[i].onClick.AddListener(() => Pedir(indice));

            GameObject t = new GameObject("Texto", typeof(RectTransform), typeof(TextMeshProUGUI));
            RectTransform trt = (RectTransform)t.transform;
            trt.SetParent(brt, false);
            trt.anchorMin = Vector2.zero; trt.anchorMax = Vector2.one;
            trt.offsetMin = trt.offsetMax = Vector2.zero;
            TextMeshProUGUI tmp = t.GetComponent<TextMeshProUGUI>();
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.fontSize = 22;
            tmp.fontStyle = FontStyles.Bold;
            tmp.raycastTarget = false;
            f.textos[i] = tmp;
        }

        // Recuadro grande a la izquierda con el personaje elegido
        GameObject tarjeta = new GameObject("VistaPreviaPersonaje", typeof(RectTransform), typeof(Image));
        RectTransform trj = (RectTransform)tarjeta.transform;
        trj.SetParent(panel, false);
        trj.anchorMin = trj.anchorMax = trj.pivot = new Vector2(0.5f, 0.5f);
        float altoPrevia = Mathf.Max(300f, altoTarjeta - 60f);
        trj.anchoredPosition = new Vector2(xIzquierda, 0f);
        trj.sizeDelta = new Vector2(AnchoZonaModelo - 40f, altoPrevia);
        tarjeta.GetComponent<Image>().color = ColorFondoPrevia;
        tarjeta.GetComponent<Image>().raycastTarget = false;

        GameObject raw = new GameObject("Modelo", typeof(RectTransform), typeof(RawImage));
        RectTransform rrt = (RectTransform)raw.transform;
        rrt.SetParent(trj, false);
        rrt.anchorMin = rrt.anchorMax = rrt.pivot = new Vector2(0.5f, 0.5f);
        rrt.anchoredPosition = new Vector2(0f, 25f);
        rrt.sizeDelta = new Vector2(AnchoZonaModelo - 60f, altoPrevia - 70f);
        RawImage ri = raw.GetComponent<RawImage>();
        ri.texture = texturaPrevia;
        ri.raycastTarget = false;

        GameObject n = new GameObject("Nombre", typeof(RectTransform), typeof(TextMeshProUGUI));
        RectTransform nrt = (RectTransform)n.transform;
        nrt.SetParent(trj, false);
        nrt.anchorMin = nrt.anchorMax = nrt.pivot = new Vector2(0.5f, 0f);
        nrt.anchoredPosition = new Vector2(0f, 10f);
        nrt.sizeDelta = new Vector2(AnchoZonaModelo - 60f, 50f);
        f.nombrePrevio = n.GetComponent<TextMeshProUGUI>();
        f.nombrePrevio.alignment = TextAlignmentOptions.Center;
        f.nombrePrevio.fontSize = 34;
        f.nombrePrevio.fontStyle = FontStyles.Bold;
        f.nombrePrevio.color = new Color(1f, 0.624f, 0.11f);
        f.nombrePrevio.raycastTarget = false;

        filas.Add(f);
    }

    private const float AnchoZonaModelo = 440f;
    private static readonly Color ColorFondoPrevia = new Color(0.094f, 0.11f, 0.153f, 1f);
    private static readonly string[] CapasTarjeta = { "Sombra", "Borde", "Tarjeta", "Franja" };

    // Agranda las capas de fondo de la tarjeta (creadas por EstiloUI) y mueve el contenido a la derecha.
    // Devuelve la X del centro de la zona izquierda libre.
    private static float AmpliarTarjeta(Transform panel, float extra, out float alto)
    {
        alto = 500f;
        Transform t = panel.Find("Tarjeta");
        if (t != null) alto = ((RectTransform)t).sizeDelta.y;
        float anchoOriginal = t != null ? ((RectTransform)t).sizeDelta.x : 720f;

        foreach (Transform hijo in panel)
        {
            RectTransform rt = hijo as RectTransform;
            if (rt == null) continue;
            if (System.Array.IndexOf(CapasTarjeta, hijo.name) >= 0)
                rt.sizeDelta += new Vector2(extra, 0f);
            else
                rt.anchoredPosition += new Vector2(extra / 2f, 0f);
        }
        return -(anchoOriginal + extra) / 2f + extra / 2f;
    }

    private void CrearVistaPrevia()
    {
        texturaPrevia = new RenderTexture(512, 640, 24, RenderTextureFormat.ARGB32);

        raizPrevia = new GameObject("VistaPreviaPersonaje_Escena").transform;
        raizPrevia.position = ZonaPrevia;

        GameObject goCam = new GameObject("CamaraPrevia");
        goCam.transform.SetParent(raizPrevia, false);
        goCam.transform.localPosition = new Vector3(0f, 1f, 4.2f);
        goCam.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
        camaraPrevia = goCam.AddComponent<Camera>();
        camaraPrevia.clearFlags = CameraClearFlags.SolidColor;
        camaraPrevia.backgroundColor = ColorFondoPrevia;
        camaraPrevia.fieldOfView = 30f;
        camaraPrevia.nearClipPlane = 0.1f;
        camaraPrevia.farClipPlane = 20f;
        camaraPrevia.targetTexture = texturaPrevia;
        camaraPrevia.enabled = false;
        // no es la camara principal: sin AudioListener
        
        // luces locales (de alcance corto para no afectar el resto de la escena)
        CrearLuz(new Vector3(2f, 3f, 3.5f), 1.6f, new Color(1f, 0.95f, 0.9f));
        CrearLuz(new Vector3(-2.5f, 1.5f, 2.5f), 0.9f, new Color(0.6f, 0.75f, 1f));
    }

    private void CrearLuz(Vector3 posLocal, float intensidad, Color color)
    {
        GameObject g = new GameObject("LuzPrevia");
        g.transform.SetParent(raizPrevia, false);
        g.transform.localPosition = posLocal;
        Light l = g.AddComponent<Light>();
        l.type = LightType.Point;
        l.range = 14f;
        l.intensity = intensidad * 6f;
        l.color = color;
        l.shadows = LightShadows.None;
    }

    private void MostrarModeloPrevio(int indice)
    {
        indicePrevio = indice;
        tiempoCambio = Time.time;
        if (modeloPrevio != null) Destroy(modeloPrevio.gameObject);
        modeloPrevio = null;

        CatalogoPersonajes cat = CatalogoPersonajes.Obtener();
        EntradaPersonaje e = cat != null ? cat.ObtenerEntrada(indice) : null;
        if (e == null || e.prefabVisual == null) return;

        GameObject go = Instantiate(e.prefabVisual, raizPrevia);
        go.name = "ModeloPrevio";
        float giro = indice >= 0 && indice < GiroPrevio.Length ? GiroPrevio[indice] : 0f;
        go.transform.localRotation = Quaternion.Euler(0f, giro, 0f);
        go.transform.localScale = e.escalaLocal;
        go.transform.localPosition = Vector3.zero;

        foreach (MonoBehaviour m in go.GetComponentsInChildren<MonoBehaviour>(true)) Destroy(m);
        foreach (Collider c in go.GetComponentsInChildren<Collider>(true)) Destroy(c);

        // Ajustar tamano y apoyar los pies en y=0, centrado
        Bounds b = default; bool hay = false;
        foreach (Renderer rd in go.GetComponentsInChildren<Renderer>(true))
        {
            if (!hay) { b = rd.bounds; hay = true; } else b.Encapsulate(rd.bounds);
        }
        if (hay && b.size.y > 0.0001f)
        {
            float k = 1.9f / b.size.y;
            go.transform.localScale = e.escalaLocal * k;
            hay = false;
            foreach (Renderer rd in go.GetComponentsInChildren<Renderer>(true))
            {
                if (!hay) { b = rd.bounds; hay = true; } else b.Encapsulate(rd.bounds);
            }
            go.transform.position += new Vector3(raizPrevia.position.x - b.center.x, raizPrevia.position.y - b.min.y, raizPrevia.position.z - b.center.z);
        }
        modeloPrevio = go.transform;

        // Animacion con personalidad (se levanta, saluda, mira alrededor, da saltitos...)
        AnimacionLobby anim = go.AddComponent<AnimacionLobby>();
        anim.Iniciar(e.avatar, cat, raizPrevia.position);
    }

    private void OnDestroy()
    {
        if (texturaPrevia != null) texturaPrevia.Release();
        if (raizPrevia != null) Destroy(raizPrevia.gameObject);
    }

    private bool OcupadoPorOtro(int indice)
    {
        foreach (Red otro in FindObjectsByType<Red>(FindObjectsSortMode.None))
        {
            if (otro != red && otro.IsSpawned && otro.IndicePersonajeActual == indice) return true;
        }
        return false;
    }

    private void Pedir(int indice)
    {
        if (red == null || !red.IsSpawned || indice == red.IndicePersonajeActual || OcupadoPorOtro(indice)) return;
        red.CambiarPersonajeServerRpc(indice);
    }

    private void Update()
    {
        if (red == null || !red.IsSpawned) return;

        // vista previa: solo se renderiza mientras el lobby esta visible
        bool lobbyVisible = false;
        foreach (Fila fv in filas) if (fv.raiz != null && fv.raiz.activeInHierarchy) lobbyVisible = true;
        if (camaraPrevia != null) camaraPrevia.enabled = lobbyVisible;
        if (lobbyVisible)
        {
            int actual = red.IndicePersonajeActual;
            if (actual >= 0 && actual != indicePrevio) MostrarModeloPrevio(actual);
        }

        bool[] ocupados = new bool[CatalogoPersonajes.TOTAL_PERSONAJES];
        for (int i = 0; i < ocupados.Length; i++) ocupados[i] = OcupadoPorOtro(i);

        foreach (Fila f in filas)
        {
            if (f.raiz == null || !f.raiz.activeInHierarchy) continue;
            if (f.nombrePrevio != null)
                f.nombrePrevio.text = CatalogoPersonajes.ObtenerSubnombre(red.IndicePersonajeActual);
            for (int i = 0; i < ocupados.Length; i++)
            {
                bool mio = i == red.IndicePersonajeActual;
                f.botones[i].interactable = !mio && !ocupados[i];
                f.fondos[i].color = mio ? colorMio : (ocupados[i] ? colorOcupado : colorLibre);
                f.textos[i].text = CatalogoPersonajes.ObtenerSubnombre(i) + (ocupados[i] ? "\n<size=14>ocupado</size>" : "");
                f.textos[i].color = mio ? Color.black : (ocupados[i] ? new Color(0.5f, 0.5f, 0.55f) : Color.white);
            }
        }
    }
}
