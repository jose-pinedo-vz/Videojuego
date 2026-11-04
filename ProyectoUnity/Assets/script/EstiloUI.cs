using System.Collections.Generic;
using System.Reflection;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Da orden, colores y estilo a toda la interfaz (menus, HUD y pantalla final) al cargar la escena.
// No hay que agregarlo a ningun objeto: se ejecuta solo si la escena tiene el script `inicio`.
// Solo cambia posiciones, tamanos y colores; los textos y botones siguen siendo los de la escena.
public static class EstiloUI
{
    // paleta
    private static readonly Color FondoPanel   = Hex("0E1117");
    private static readonly Color Tarjeta      = Hex("181C27");
    private static readonly Color CampoTexto   = Hex("232A3D");
    private static readonly Color Acento       = Hex("FF9F1C");
    private static readonly Color AcentoClaro  = Hex("FFB84D");
    private static readonly Color AcentoOscuro = Hex("D9830B");
    private static readonly Color Secundario   = Hex("2A3042");
    private static readonly Color SecundarioHover = Hex("3A4260");
    private static readonly Color TextoClaro   = Hex("F2F4F8");
    private static readonly Color TextoTenue   = Hex("9AA3B5");
    private static readonly Color TextoSobreAcento = Hex("1A1205");
    private static readonly Color FondoHUD     = new Color(0.06f, 0.07f, 0.10f, 0.72f);

    private static Sprite redondeado;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Iniciar()
    {
        // este metodo solo corre al abrir el juego; al recargar la escena (volver al menu)
        // hay que aplicar el estilo otra vez o se ve la interfaz vieja
        UnityEngine.SceneManagement.SceneManager.sceneLoaded -= AlCargarEscena;
        UnityEngine.SceneManagement.SceneManager.sceneLoaded += AlCargarEscena;
        Aplicar();
    }

    private static void AlCargarEscena(UnityEngine.SceneManagement.Scene escena, UnityEngine.SceneManagement.LoadSceneMode modo)
    {
        Aplicar();
    }

    private static void Aplicar()
    {
        inicio menu = Object.FindFirstObjectByType<inicio>(FindObjectsInactive.Include);
        if (menu == null) return;

        Transform canvas = menu.transform;
        ConfigurarEscalado(canvas);

        // ---- Inicio: nombre y botones de jugar / salir ----
        Transform p = canvas.Find("Inicio");
        EstiloPanel(p, new Vector2(640, 520));
        EstiloTitulo(Texto(p, "Text (TMP)"), new Vector2(0, 170), new Vector2(560, 70), 44);
        EstiloInput(Input(p, "InpuntNombre"), new Vector2(0, 60), new Vector2(420, 60));
        EstiloBoton(Boton(p, "btn_iniciar"), true, new Vector2(0, -60), new Vector2(420, 70));
        EstiloBoton(Boton(p, "btn_salir"), false, new Vector2(0, -155), new Vector2(420, 70));

        // ---- Seleccion: crear sala o unirse ----
        p = canvas.Find("Seleccion");
        EstiloPanel(p, new Vector2(640, 400));
        CrearTitulo(p, "Elige cómo jugar", new Vector2(0, 120), new Vector2(560, 70), 44);
        EstiloBoton(Boton(p, "btn_crearSala"), true, new Vector2(0, 10), new Vector2(420, 70));
        EstiloBoton(Boton(p, "btn_UnirmeSale"), false, new Vector2(0, -90), new Vector2(420, 70));

        // ---- Host: codigo de sala, jugadores y empezar ----
        p = canvas.Find("CrearHost");
        EstiloPanel(p, new Vector2(720, 560));
        EstiloTexto(Texto(p, "codigo"), new Vector2(0, 190), new Vector2(620, 80), 52, true, Acento, TextAlignmentOptions.Center);
        EstiloTexto(Texto(p, "nombre"), new Vector2(0, 40), new Vector2(560, 210), 28, false, TextoClaro, TextAlignmentOptions.Top);
        EstiloBoton(Boton(p, "btn_playGame"), true, new Vector2(0, -210), new Vector2(420, 72));

        // ---- Cliente: escribir el codigo ----
        p = canvas.Find("CrearCliente");
        EstiloPanel(p, new Vector2(640, 480));
        EstiloTexto(Texto(p, "Text (TMP)"), new Vector2(0, 170), new Vector2(560, 70), 30, true, TextoClaro, TextAlignmentOptions.Center);
        EstiloInput(Input(p, "ingresoCodigo"), new Vector2(0, 40), new Vector2(420, 64));
        EstiloBoton(Boton(p, "btn_unirmeSala"), true, new Vector2(0, -90), new Vector2(420, 70));

        // ---- Pantalla de carga: lista de jugadores conectados ----
        p = canvas.Find("pantalla de carga");
        EstiloPanel(p, new Vector2(720, 520));
        EstilizarCarga(p);

        // ---- Pantalla final: marcador ----
        p = canvas.Find("FinPartida");
        EstiloPanel(p, new Vector2(800, 680));
        CrearTitulo(p, "Partida terminada", new Vector2(0, 270), new Vector2(700, 70), 46);
        TextMeshProUGUI resultados = Texto(p, "Text (TMP)");
        EstiloTexto(resultados, new Vector2(0, 20), new Vector2(700, 400), 36, false, TextoClaro, TextAlignmentOptions.Top);
        if (resultados != null) resultados.richText = true;
        CrearBotonVolver(p, menu);

        // ---- HUD de la partida ----
        EstilizarHUD(canvas, menu);
    }

    // boton de la pantalla final: cierra la sala y regresa al menu principal
    private static void CrearBotonVolver(Transform panel, inicio menu)
    {
        if (panel == null || panel.Find("btn_menuPrincipal") != null) return;

        TextMeshProUGUI referencia = panel.GetComponentInChildren<TextMeshProUGUI>(true);

        GameObject go = new GameObject("btn_menuPrincipal", typeof(RectTransform), typeof(Image), typeof(Button));
        go.transform.SetParent(panel, false);
        Button boton = go.GetComponent<Button>();
        boton.onClick.AddListener(menu.volverAlMenu);

        GameObject goTxt = new GameObject("Text (TMP)", typeof(RectTransform));
        goTxt.transform.SetParent(go.transform, false);
        TextMeshProUGUI txt = goTxt.AddComponent<TextMeshProUGUI>();
        if (referencia != null) txt.font = referencia.font;
        txt.text = "Menú Principal";
        txt.raycastTarget = false;
        RectTransform rt = txt.rectTransform;
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;

        EstiloBoton(boton, true, new Vector2(0, -270), new Vector2(420, 72));
    }

    // el Canvas escala con la pantalla para que se vea igual en cualquier resolucion
    private static void ConfigurarEscalado(Transform canvas)
    {
        CanvasScaler escala = canvas.GetComponent<CanvasScaler>();
        if (escala == null) return;
        escala.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        escala.referenceResolution = new Vector2(1920, 1080);
        escala.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        escala.matchWidthOrHeight = 0.5f;
    }

    // dos textos de titulo (arriba) y la lista de jugadores (abajo)
    private static void EstilizarCarga(Transform p)
    {
        if (p == null) return;

        List<TextMeshProUGUI> etiquetas = new List<TextMeshProUGUI>();
        TextMeshProUGUI lista = null;
        foreach (Transform hijo in p)
        {
            TextMeshProUGUI t = hijo.GetComponent<TextMeshProUGUI>();
            if (t == null) continue;
            if (hijo.name == "outputPlayers") lista = t;
            else etiquetas.Add(t);
        }
        etiquetas.Sort((a, b) => b.rectTransform.anchoredPosition.y.CompareTo(a.rectTransform.anchoredPosition.y));

        if (etiquetas.Count > 0)
            EstiloTitulo(etiquetas[0], new Vector2(0, 170), new Vector2(620, 70), 40);
        if (etiquetas.Count > 1)
            EstiloTexto(etiquetas[1], new Vector2(0, 105), new Vector2(620, 50), 26, false, TextoTenue, TextAlignmentOptions.Center);
        if (lista != null)
            EstiloTexto(lista, new Vector2(0, -30), new Vector2(560, 240), 30, false, TextoClaro, TextAlignmentOptions.Top);
    }

    // tiempo arriba al centro, marcador arriba a la izquierda, arma y municion abajo a la derecha
    private static void EstilizarHUD(Transform canvas, inicio menu)
    {
        if (menu.txt_tiempo != null)
        {
            RectTransform rt = menu.txt_tiempo.rectTransform;
            EstiloTexto(menu.txt_tiempo, new Vector2(0, -50), new Vector2(240, 60), 38, true, TextoClaro, TextAlignmentOptions.Center);
            Anclar(rt, new Vector2(0.5f, 1f), new Vector2(0, -50), new Vector2(240, 60));
            CrearFondoHUD(rt, new Vector2(16, 6), rt.gameObject);
        }

        Transform marcador = canvas.Find("marcador");
        if (marcador != null)
        {
            TextMeshProUGUI t = marcador.GetComponent<TextMeshProUGUI>();
            RectTransform rt = t.rectTransform;
            EstiloTexto(t, Vector2.zero, new Vector2(340, 170), 22, false, TextoClaro, TextAlignmentOptions.TopLeft);
            Anclar(rt, new Vector2(0f, 1f), new Vector2(40, -40), new Vector2(340, 170));
            CrearFondoHUD(rt, new Vector2(16, 12), rt.gameObject);
        }

        HUD hud = canvas.GetComponent<HUD>();
        if (hud == null) return;
        TextMeshProUGUI arma = CampoHUD(hud, "txt_arma");
        TextMeshProUGUI municion = CampoHUD(hud, "txt_municion");

        if (arma != null)
        {
            EstiloTexto(arma, Vector2.zero, new Vector2(320, 40), 26, false, TextoTenue, TextAlignmentOptions.Right);
            Anclar(arma.rectTransform, new Vector2(1f, 0f), new Vector2(-40, 120), new Vector2(320, 40));
        }
        if (municion != null)
        {
            EstiloTexto(municion, Vector2.zero, new Vector2(320, 60), 40, true, Acento, TextAlignmentOptions.Right);
            Anclar(municion.rectTransform, new Vector2(1f, 0f), new Vector2(-40, 60), new Vector2(320, 60));
        }
        if (arma != null && municion != null)
        {
            // un solo cuadro detras de las dos lineas
            RectTransform fondo = CrearFondoHUD(municion.rectTransform, new Vector2(16, 8), arma.gameObject);
            fondo.anchorMin = fondo.anchorMax = fondo.pivot = new Vector2(1f, 0f);
            fondo.sizeDelta = new Vector2(352, 120);
            fondo.anchoredPosition = new Vector2(-24, 50);
        }
    }

    private static TextMeshProUGUI CampoHUD(HUD hud, string campo)
    {
        FieldInfo f = typeof(HUD).GetField(campo, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
        return f != null ? f.GetValue(hud) as TextMeshProUGUI : null;
    }

    // ---------- busqueda de elementos ----------

    private static TextMeshProUGUI Texto(Transform padre, string nombre)
    {
        Transform t = padre != null ? padre.Find(nombre) : null;
        return t != null ? t.GetComponent<TextMeshProUGUI>() : null;
    }

    private static Button Boton(Transform padre, string nombre)
    {
        Transform t = padre != null ? padre.Find(nombre) : null;
        return t != null ? t.GetComponent<Button>() : null;
    }

    private static TMP_InputField Input(Transform padre, string nombre)
    {
        Transform t = padre != null ? padre.Find(nombre) : null;
        return t != null ? t.GetComponent<TMP_InputField>() : null;
    }

    // ---------- estilos ----------

    // fondo oscuro a pantalla completa y una tarjeta detras del contenido
    private static void EstiloPanel(Transform panel, Vector2 tamanoTarjeta)
    {
        if (panel == null) return;

        Image fondo = panel.GetComponent<Image>();
        if (fondo != null)
        {
            fondo.sprite = null;
            fondo.color = FondoPanel;
        }

        Transform existente = panel.Find("Tarjeta");
        RectTransform tarjeta;
        if (existente != null)
        {
            tarjeta = (RectTransform)existente;
        }
        else
        {
            GameObject go = new GameObject("Tarjeta", typeof(RectTransform), typeof(Image));
            tarjeta = (RectTransform)go.transform;
            tarjeta.SetParent(panel, false);
            tarjeta.SetAsFirstSibling();
            Image img = go.GetComponent<Image>();
            img.sprite = SpriteRedondeado();
            img.type = Image.Type.Sliced;
            img.color = Tarjeta;
            img.raycastTarget = false;
        }
        Anclar(tarjeta, new Vector2(0.5f, 0.5f), Vector2.zero, tamanoTarjeta);
    }

    private static void EstiloTitulo(TextMeshProUGUI t, Vector2 pos, Vector2 tam, float tamano)
    {
        EstiloTexto(t, pos, tam, tamano, true, TextoClaro, TextAlignmentOptions.Center);
    }

    private static void CrearTitulo(Transform padre, string texto, Vector2 pos, Vector2 tam, float tamano)
    {
        if (padre == null || padre.Find("Titulo") != null) return;

        // se toma la fuente de un texto existente del panel
        TextMeshProUGUI referencia = padre.GetComponentInChildren<TextMeshProUGUI>(true);
        GameObject go = new GameObject("Titulo", typeof(RectTransform));
        go.transform.SetParent(padre, false);
        TextMeshProUGUI t = go.AddComponent<TextMeshProUGUI>();
        if (referencia != null) t.font = referencia.font;
        t.text = texto;
        t.raycastTarget = false;
        EstiloTitulo(t, pos, tam, tamano);
    }

    private static void EstiloTexto(TextMeshProUGUI t, Vector2 pos, Vector2 tam, float tamano, bool negrita, Color color, TextAlignmentOptions alineacion)
    {
        if (t == null) return;
        Anclar(t.rectTransform, new Vector2(0.5f, 0.5f), pos, tam);
        t.fontSize = tamano;
        t.fontStyle = negrita ? FontStyles.Bold : FontStyles.Normal;
        t.color = color;
        t.alignment = alineacion;
        t.enableAutoSizing = false;
        t.textWrappingMode = TextWrappingModes.Normal;
        t.overflowMode = TextOverflowModes.Overflow;
    }

    private static void EstiloBoton(Button b, bool principal, Vector2 pos, Vector2 tam)
    {
        if (b == null) return;
        Anclar((RectTransform)b.transform, new Vector2(0.5f, 0.5f), pos, tam);

        Image img = b.GetComponent<Image>();
        if (img != null)
        {
            img.sprite = SpriteRedondeado();
            img.type = Image.Type.Sliced;
            img.color = Color.white;
            b.targetGraphic = img;
        }

        Color normal = principal ? Acento : Secundario;
        ColorBlock c = b.colors;
        c.normalColor = normal;
        c.highlightedColor = principal ? AcentoClaro : SecundarioHover;
        c.selectedColor = c.highlightedColor;
        c.pressedColor = principal ? AcentoOscuro : Hex("1C2133");
        c.disabledColor = new Color(normal.r, normal.g, normal.b, 0.4f);
        c.colorMultiplier = 1f;
        c.fadeDuration = 0.1f;
        b.colors = c;
        b.transition = Selectable.Transition.ColorTint;

        TextMeshProUGUI txt = b.GetComponentInChildren<TextMeshProUGUI>(true);
        if (txt != null)
        {
            txt.fontSize = 30;
            txt.fontStyle = FontStyles.Bold;
            txt.color = principal ? TextoSobreAcento : TextoClaro;
            txt.alignment = TextAlignmentOptions.Center;
            txt.enableAutoSizing = false;
        }
    }

    private static void EstiloInput(TMP_InputField campo, Vector2 pos, Vector2 tam)
    {
        if (campo == null) return;
        Anclar((RectTransform)campo.transform, new Vector2(0.5f, 0.5f), pos, tam);

        Image img = campo.GetComponent<Image>();
        if (img != null)
        {
            img.sprite = SpriteRedondeado();
            img.type = Image.Type.Sliced;
            img.color = CampoTexto;
        }

        if (campo.textViewport != null)
        {
            campo.textViewport.anchorMin = Vector2.zero;
            campo.textViewport.anchorMax = Vector2.one;
            campo.textViewport.offsetMin = new Vector2(16, 6);
            campo.textViewport.offsetMax = new Vector2(-16, -6);
        }

        float tamano = tam.y >= 60 ? 28 : 24;
        if (campo.textComponent != null)
        {
            campo.textComponent.fontSize = tamano;
            campo.textComponent.color = TextoClaro;
            campo.textComponent.alignment = TextAlignmentOptions.Center;
        }
        TMP_Text placeholder = campo.placeholder as TMP_Text;
        if (placeholder != null)
        {
            placeholder.fontSize = tamano;
            placeholder.fontStyle = FontStyles.Italic;
            placeholder.color = TextoTenue;
            placeholder.alignment = TextAlignmentOptions.Center;
        }

        campo.customCaretColor = true;
        campo.caretColor = Acento;
        campo.selectionColor = new Color(Acento.r, Acento.g, Acento.b, 0.35f);
        campo.caretWidth = 3;
    }

    // ---------- utilidades ----------

    private static void Anclar(RectTransform rt, Vector2 ancla, Vector2 pos, Vector2 tam)
    {
        rt.anchorMin = ancla;
        rt.anchorMax = ancla;
        rt.pivot = ancla;
        rt.anchoredPosition = pos;
        rt.sizeDelta = tam;
    }

    // cuadro semitransparente detras de un texto del HUD, se oculta junto con `seguir`
    private static RectTransform CrearFondoHUD(RectTransform objetivo, Vector2 relleno, GameObject seguir)
    {
        Transform existente = objetivo.parent.Find("Fondo_" + objetivo.name);
        if (existente != null) return (RectTransform)existente;

        GameObject go = new GameObject("Fondo_" + objetivo.name, typeof(RectTransform), typeof(Image));
        RectTransform rt = (RectTransform)go.transform;
        rt.SetParent(objetivo.parent, false);
        rt.SetSiblingIndex(objetivo.GetSiblingIndex());

        rt.anchorMin = objetivo.anchorMin;
        rt.anchorMax = objetivo.anchorMax;
        rt.pivot = objetivo.pivot;
        rt.sizeDelta = objetivo.sizeDelta + relleno * 2f;
        rt.anchoredPosition = objetivo.anchoredPosition;

        Image img = go.GetComponent<Image>();
        img.sprite = SpriteRedondeado();
        img.type = Image.Type.Sliced;
        img.color = FondoHUD;
        img.raycastTarget = false;

        go.AddComponent<SeguirVisibilidad>().objetivo = seguir;
        return rt;
    }

    // rectangulo con esquinas redondeadas para usar con Image tipo Sliced
    private static Sprite SpriteRedondeado()
    {
        if (redondeado != null) return redondeado;

        const int tam = 48;
        const float radio = 14f;
        Texture2D tex = new Texture2D(tam, tam, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;
        tex.wrapMode = TextureWrapMode.Clamp;

        for (int y = 0; y < tam; y++)
        {
            for (int x = 0; x < tam; x++)
            {
                // distancia al centro de la esquina mas cercana
                float dx = Mathf.Max(radio - (x + 0.5f), (x + 0.5f) - (tam - radio), 0f);
                float dy = Mathf.Max(radio - (y + 0.5f), (y + 0.5f) - (tam - radio), 0f);
                float dist = Mathf.Sqrt(dx * dx + dy * dy);
                float alfa = Mathf.Clamp01(radio - dist + 0.5f);
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, alfa));
            }
        }
        tex.Apply();

        redondeado = Sprite.Create(tex, new Rect(0, 0, tam, tam), new Vector2(0.5f, 0.5f), 100f, 0,
            SpriteMeshType.FullRect, new Vector4(radio, radio, radio, radio));
        return redondeado;
    }

    private static Color Hex(string hex)
    {
        ColorUtility.TryParseHtmlString("#" + hex, out Color c);
        return c;
    }
}

// Mantiene un fondo del HUD visible solo cuando su texto esta visible
public class SeguirVisibilidad : MonoBehaviour
{
    public GameObject objetivo;

    private void Update()
    {
        if (objetivo == null) { Destroy(gameObject); return; }
        // se oculta con el CanvasRenderer para que Update siga corriendo aunque el texto este apagado
        CanvasRenderer cr = GetComponent<CanvasRenderer>();
        if (cr != null) cr.cull = !objetivo.activeInHierarchy;
    }
}
