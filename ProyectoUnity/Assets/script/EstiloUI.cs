using System.Collections.Generic;
using System.Reflection;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Da orden, colores y estilo a toda la interfaz (menus, HUD y pantalla final) al cargar la escena.
// No hay que agregarlo a ningun objeto: se ejecuta solo si la escena tiene el script `inicio`.
// Solo cambia posiciones, tamanos y colores; los textos y botones siguen siendo los de la escena.
public static partial class EstiloUI
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
    private static readonly Color FondoHUD     = new Color(0.06f, 0.07f, 0.10f, 0.85f);

    private static Sprite redondeado;
    private static Sprite degradado;
    private static TMP_FontAsset fuenteBase;

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
        EstiloPanel(p, new Vector2(640, 700));
        EstiloTitulo(Texto(p, "Text (TMP)"), new Vector2(0, 270), new Vector2(560, 70), 44);
        EstiloInput(Input(p, "InpuntNombre"), new Vector2(0, 165), new Vector2(420, 60));
        EstiloBoton(Boton(p, "btn_iniciar"), true, new Vector2(0, 55), new Vector2(420, 70));
        EstiloBoton(Boton(p, "btn_salir"), false, new Vector2(0, -225), new Vector2(420, 70));
        // entre Jugar y Salir: ajustes de volumen y de teclado (se crean por codigo)
        Button refBoton = Boton(p, "btn_iniciar");
        TMP_Text refTexto = refBoton != null ? refBoton.GetComponentInChildren<TMP_Text>(true) : null;
        if (p != null) CrearMenuAjustes(canvas, p, menu, refTexto != null ? refTexto.font : null);

        // ---- Seleccion: crear sala o unirse ----
        p = canvas.Find("Seleccion");
        EstiloPanel(p, new Vector2(640, 400));
        CrearTitulo(p, "Elige cómo jugar", new Vector2(0, 120), new Vector2(560, 70), 44);
        EstiloBoton(Boton(p, "btn_crearSala"), true, new Vector2(0, 10), new Vector2(420, 70));
        EstiloBoton(Boton(p, "btn_UnirmeSale"), false, new Vector2(0, -90), new Vector2(420, 70));
        CrearBotonAtras(p, "btn_atrasSeleccion", menu.atrasDeSeleccion, new Vector2(0, -165), new Vector2(420, 60));

        // ---- Host: codigo de sala, jugadores y empezar ----
        p = canvas.Find("CrearHost");
        EstiloPanel(p, new Vector2(720, 660));
        EstiloTexto(Texto(p, "codigo"), new Vector2(0, 190), new Vector2(620, 80), 52, true, Acento, TextAlignmentOptions.Center);
        EstiloTexto(Texto(p, "nombre"), new Vector2(0, 40), new Vector2(560, 210), 28, false, TextoClaro, TextAlignmentOptions.Top);
        EstiloBoton(Boton(p, "btn_playGame"), true, new Vector2(0, -210), new Vector2(420, 72));
        CrearBotonAtras(p, "btn_atrasHost", menu.atrasDeHost, new Vector2(0, -290), new Vector2(420, 60));

        // ---- Cliente: escribir el codigo ----
        p = canvas.Find("CrearCliente");
        EstiloPanel(p, new Vector2(640, 480));
        EstiloTexto(Texto(p, "Text (TMP)"), new Vector2(0, 170), new Vector2(560, 70), 30, true, TextoClaro, TextAlignmentOptions.Center);
        EstiloInput(Input(p, "ingresoCodigo"), new Vector2(0, 40), new Vector2(420, 64));
        EstiloBoton(Boton(p, "btn_unirmeSala"), true, new Vector2(0, -90), new Vector2(420, 70));
        CrearBotonAtras(p, "btn_atrasCliente", menu.atrasDeCliente, new Vector2(0, -170), new Vector2(420, 60));

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

        // ---- cortina y nombre del mapa al empezar cada ronda ----
        CrearTransicionMapa(canvas, menu);

        // ---- reglas de la partida (duracion, mapas, jugadores), antes de crear la sala ----
        CrearPanelConfiguracion(canvas, menu);
    }

    // Pantalla de configuracion creada por codigo (la escena no se toca). Tres filas con - / + y botones Crear sala / Atras
    private static void CrearPanelConfiguracion(Transform canvas, inicio menu)
    {
        Transform existente = canvas.Find("Configuracion");
        if (existente != null) { menu.PanelConfiguracion = existente.gameObject; return; }

        TMP_FontAsset fuente = menu.txt_tiempo != null ? menu.txt_tiempo.font : null;
        if (fuente == null) fuente = menu.txt_inicio != null ? menu.txt_inicio.font : null;

        GameObject panel = new GameObject("Configuracion", typeof(RectTransform), typeof(Image));
        panel.transform.SetParent(canvas, false);
        RectTransform rp = (RectTransform)panel.transform;
        rp.anchorMin = Vector2.zero;
        rp.anchorMax = Vector2.one;
        rp.offsetMin = rp.offsetMax = Vector2.zero;
        EstiloPanel(panel.transform, new Vector2(760, 680));

        TextMeshProUGUI titulo = CrearTextoSimple(panel.transform, "Titulo", fuente, "Configurar partida");
        EstiloTitulo(titulo, new Vector2(0, 270), new Vector2(680, 70), 44);

        int mapasDisponibles = Mathf.Max(1, menu.Mapas.Count);
        if (ConfiguracionPartida.cantidadMapas <= 0) ConfiguracionPartida.cantidadMapas = mapasDisponibles;

        // fila 1: duracion de cada mapa
        TextMeshProUGUI vDuracion = null;
        FilaConfiguracion(panel.transform, fuente, "Duración por mapa", 130,
            () => ConfiguracionPartida.FormatearDuracion(ConfiguracionPartida.duracionSegundos),
            d => ConfiguracionPartida.duracionSegundos = Mathf.Clamp(ConfiguracionPartida.duracionSegundos + d * ConfiguracionPartida.DURACION_PASO, ConfiguracionPartida.DURACION_MIN, ConfiguracionPartida.DURACION_MAX),
            out vDuracion);

        // fila 2: cuantos mapas se juegan
        TextMeshProUGUI vMapas = null;
        FilaConfiguracion(panel.transform, fuente, "Mapas a jugar", 20,
            () => ConfiguracionPartida.MapasAJugar(mapasDisponibles).ToString() + " / " + mapasDisponibles,
            d => ConfiguracionPartida.cantidadMapas = Mathf.Clamp(ConfiguracionPartida.MapasAJugar(mapasDisponibles) + d, 1, mapasDisponibles),
            out vMapas);

        // fila 3: jugadores maximos, de 2 a 4
        TextMeshProUGUI vJugadores = null;
        FilaConfiguracion(panel.transform, fuente, "Jugadores máximos", -90,
            () => ConfiguracionPartida.maxJugadores.ToString(),
            d => ConfiguracionPartida.maxJugadores = Mathf.Clamp(ConfiguracionPartida.maxJugadores + d, ConfiguracionPartida.JUGADORES_MIN, ConfiguracionPartida.JUGADORES_MAX),
            out vJugadores);

        Button crear = CrearBotonSimple(panel.transform, "btn_confirmarConfig", fuente, "Crear sala");
        crear.onClick.AddListener(menu.confirmarConfiguracion);
        EstiloBoton(crear, true, new Vector2(0, -205), new Vector2(420, 70));

        Button atras = CrearBotonSimple(panel.transform, "btn_atrasConfig", fuente, "Atrás");
        atras.onClick.AddListener(menu.cancelarConfiguracion);
        EstiloBoton(atras, false, new Vector2(0, -290), new Vector2(420, 64));

        menu.PanelConfiguracion = panel;
        panel.SetActive(false);
    }

    // etiqueta a la izquierda y selector "- valor +" a la derecha; cambio(+1/-1) ajusta el dato y el texto se refresca solo
    private static void FilaConfiguracion(Transform panel, TMP_FontAsset fuente, string etiqueta, float y,
        System.Func<string> leer, System.Action<int> cambio, out TextMeshProUGUI valor)
    {
        TextMeshProUGUI txtEtiqueta = CrearTextoSimple(panel, "Etiqueta_" + etiqueta, fuente, etiqueta);
        EstiloTexto(txtEtiqueta, new Vector2(-340, y), new Vector2(320, 60), 28, false, TextoClaro, TextAlignmentOptions.MidlineLeft);
        txtEtiqueta.rectTransform.pivot = new Vector2(0f, 0.5f);
        txtEtiqueta.rectTransform.anchorMin = txtEtiqueta.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        txtEtiqueta.rectTransform.anchoredPosition = new Vector2(-340, y);

        TextMeshProUGUI txtValor = CrearTextoSimple(panel, "Valor_" + etiqueta, fuente, leer());
        EstiloTexto(txtValor, new Vector2(150, y), new Vector2(150, 60), 34, true, Acento, TextAlignmentOptions.Center);
        valor = txtValor;

        Button menos = CrearBotonSimple(panel, "btn_menos_" + etiqueta, fuente, "-");
        EstiloBoton(menos, false, new Vector2(40, y), new Vector2(64, 60));
        menos.onClick.AddListener(() => { cambio(-1); txtValor.text = leer(); });

        Button mas = CrearBotonSimple(panel, "btn_mas_" + etiqueta, fuente, "+");
        EstiloBoton(mas, false, new Vector2(260, y), new Vector2(64, 60));
        mas.onClick.AddListener(() => { cambio(1); txtValor.text = leer(); });
    }

    private static TextMeshProUGUI CrearTextoSimple(Transform padre, string nombre, TMP_FontAsset fuente, string texto)
    {
        GameObject go = new GameObject(nombre, typeof(RectTransform));
        go.transform.SetParent(padre, false);
        TextMeshProUGUI t = go.AddComponent<TextMeshProUGUI>();
        if (fuente != null) t.font = fuente;
        t.text = texto;
        t.raycastTarget = false;
        return t;
    }

    private static Button CrearBotonSimple(Transform padre, string nombre, TMP_FontAsset fuente, string texto)
    {
        GameObject go = new GameObject(nombre, typeof(RectTransform), typeof(Image), typeof(Button));
        go.transform.SetParent(padre, false);
        TextMeshProUGUI t = CrearTextoSimple(go.transform, "Text (TMP)", fuente, texto);
        RectTransform rt = t.rectTransform;
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
        return go.GetComponent<Button>();
    }


    // cortina negra que se abre y cartel con el nombre del mapa, se dispara solo cuando cambia el mapa activo
    private static void CrearTransicionMapa(Transform canvas, inicio menu)
    {
        if (canvas.Find("TransicionMapa") != null) return;

        TMP_FontAsset fuente = menu.txt_tiempo != null ? menu.txt_tiempo.font : null;

        GameObject raiz = new GameObject("TransicionMapa", typeof(RectTransform));
        raiz.transform.SetParent(canvas, false);
        RectTransform rr = (RectTransform)raiz.transform;
        rr.anchorMin = Vector2.zero;
        rr.anchorMax = Vector2.one;
        rr.offsetMin = rr.offsetMax = Vector2.zero;

        // cortina a pantalla completa
        GameObject goCortina = new GameObject("Cortina", typeof(RectTransform), typeof(Image));
        goCortina.transform.SetParent(raiz.transform, false);
        RectTransform rc = (RectTransform)goCortina.transform;
        rc.anchorMin = Vector2.zero;
        rc.anchorMax = Vector2.one;
        rc.offsetMin = rc.offsetMax = Vector2.zero;
        Image cortina = goCortina.GetComponent<Image>();
        cortina.color = new Color(0.03f, 0.04f, 0.06f, 0f);
        cortina.raycastTarget = false;

        // cartel arriba al centro: sombra, borde, tarjeta, franja de acento y textos
        RectTransform cartel = CapaTarjeta(raiz.transform, "Cartel", Tarjeta, new Vector2(620, 120), Vector2.zero);
        cartel.anchorMin = cartel.anchorMax = cartel.pivot = new Vector2(0.5f, 1f);
        cartel.anchoredPosition = new Vector2(0, 160);
        cartel.SetAsLastSibling();

        RectTransform borde = CapaTarjeta(cartel, "Borde", Secundario, new Vector2(624, 124), Vector2.zero);
        borde.SetAsFirstSibling();
        RectTransform franja = CapaTarjeta(cartel, "Franja", Acento, new Vector2(540, 6), new Vector2(0, -3));
        franja.anchorMin = franja.anchorMax = franja.pivot = new Vector2(0.5f, 1f);
        franja.anchoredPosition = new Vector2(0, 0);

        TextMeshProUGUI etiqueta = CrearTextoHUD(cartel, "Etiqueta", fuente, null);
        EstiloTexto(etiqueta, Vector2.zero, new Vector2(560, 28), 20, true, Acento, TextAlignmentOptions.Center);
        etiqueta.text = "MAPA";
        etiqueta.characterSpacing = 10f;
        Anclar(etiqueta.rectTransform, new Vector2(0.5f, 1f), new Vector2(0, -20), new Vector2(560, 28));

        TextMeshProUGUI nombre = CrearTextoHUD(cartel, "Nombre", fuente, null);
        EstiloTexto(nombre, Vector2.zero, new Vector2(580, 64), 52, true, TextoClaro, TextAlignmentOptions.Center);
        nombre.characterSpacing = 3f;
        Anclar(nombre.rectTransform, new Vector2(0.5f, 1f), new Vector2(0, -50), new Vector2(580, 64));

        cartel.gameObject.SetActive(false);
        raiz.AddComponent<TransicionMapa>().Configurar(menu, cortina, cartel, nombre);
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

    // boton "Atras" secundario para las pantallas de seleccion, host y cliente
    private static void CrearBotonAtras(Transform panel, string nombre, UnityEngine.Events.UnityAction accion, Vector2 pos, Vector2 tam)
    {
        if (panel == null || panel.Find(nombre) != null) return;

        TextMeshProUGUI referencia = panel.GetComponentInChildren<TextMeshProUGUI>(true);

        GameObject go = new GameObject(nombre, typeof(RectTransform), typeof(Image), typeof(Button));
        go.transform.SetParent(panel, false);
        Button boton = go.GetComponent<Button>();
        boton.onClick.AddListener(accion);

        GameObject goTxt = new GameObject("Text (TMP)", typeof(RectTransform));
        goTxt.transform.SetParent(go.transform, false);
        TextMeshProUGUI txt = goTxt.AddComponent<TextMeshProUGUI>();
        if (referencia != null) txt.font = referencia.font;
        txt.text = "Atrás";
        txt.raycastTarget = false;
        RectTransform rt = txt.rectTransform;
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;

        EstiloBoton(boton, false, pos, tam);
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

    // tiempo arriba al centro, marcador arriba a la izquierda, arma y municion abajo a la derecha,
    // controles abajo al centro
    private static void EstilizarHUD(Transform canvas, inicio menu)
    {
        TMP_FontAsset fuente = menu.txt_tiempo != null ? menu.txt_tiempo.font : null;

        // ---- tiempo ----
        if (menu.txt_tiempo != null)
        {
            RectTransform rt = menu.txt_tiempo.rectTransform;
            EstiloTexto(menu.txt_tiempo, Vector2.zero, new Vector2(220, 64), 42, true, TextoClaro, TextAlignmentOptions.Center);
            Anclar(rt, new Vector2(0.5f, 1f), new Vector2(0, -32), new Vector2(220, 64));
            RectTransform fondo = CrearFondoHUD(rt, Vector2.zero, rt.gameObject);
            if (menu.txt_tiempo.GetComponent<HudTiempo>() == null) menu.txt_tiempo.gameObject.AddComponent<HudTiempo>().Configurar(fondo, Acento, Hex("FF5252"), TextoClaro);
        }

        // ---- marcador ----
        Transform marcador = canvas.Find("marcador");
        if (marcador != null)
        {
            TextMeshProUGUI lista = marcador.GetComponent<TextMeshProUGUI>();
            RectTransform rt = lista.rectTransform;
            EstiloTexto(lista, Vector2.zero, new Vector2(300, 120), 24, false, TextoClaro, TextAlignmentOptions.TopLeft);
            lista.richText = true;
            lista.lineSpacing = 12;
            Anclar(rt, new Vector2(0f, 1f), new Vector2(52, -80), new Vector2(300, 120));
            RectTransform fondo = CrearFondoHUD(rt, Vector2.zero, rt.gameObject);
            fondo.anchoredPosition = new Vector2(32, -32);
            fondo.sizeDelta = new Vector2(340, 140);

            TextMeshProUGUI encabezado = CrearTextoHUD(canvas, "Titulo_marcador", fuente, lista.gameObject);
            EstiloTexto(encabezado, Vector2.zero, new Vector2(300, 28), 18, true, Acento, TextAlignmentOptions.TopLeft);
            encabezado.text = "MARCADOR";
            encabezado.characterSpacing = 6f;
            Anclar(encabezado.rectTransform, new Vector2(0f, 1f), new Vector2(52, -46), new Vector2(300, 28));

            if (lista.GetComponent<HudMarcador>() == null) lista.gameObject.AddComponent<HudMarcador>().Configurar(fondo, Acento, TextoTenue);
        }

        HUD hud = canvas.GetComponent<HUD>();
        if (hud != null) EstilizarArma(canvas, hud, fuente, menu);

        // ---- ayuda de controles ----
        if (canvas.Find("Controles") == null && menu.txt_tiempo != null)
        {
            TextMeshProUGUI txt = CrearTextoHUD(canvas, "Controles", fuente, menu.txt_tiempo.gameObject);
            EstiloTexto(txt, Vector2.zero, new Vector2(1040, 36), 20, false, TextoTenue, TextAlignmentOptions.Center);
            txt.richText = true;
            string a = "<color=#FF9F1C><b>";
            string b = "</b></color> ";
            txt.text = a + "A/D" + b + "mover     " + a + "ESPACIO" + b + "saltar     " + a + "SHIFT" + b + "deslizar     "
                     + a + "C" + b + "recoger/soltar     " + a + "V" + b + "disparar     " + a + "R" + b + "recargar     " + a + "M" + b + "música";
            Anclar(txt.rectTransform, new Vector2(0.5f, 0f), new Vector2(0, 28), new Vector2(1040, 36));
            RectTransform fondo = CrearFondoHUD(txt.rectTransform, new Vector2(24, 4), menu.txt_tiempo.gameObject);
            fondo.GetComponentInChildren<Image>().color = new Color(0.04f, 0.05f, 0.08f, 0.55f);
            Transform acento = fondo.Find("Acento");
            if (acento != null) Object.Destroy(acento.gameObject);
        }
    }

    // arma y municion: los textos no estaban asignados en la escena, se crean y se le entregan al HUD
    private static void EstilizarArma(Transform canvas, HUD hud, TMP_FontAsset fuente, inicio menu)
    {
        TextMeshProUGUI arma = CampoHUD(hud, "txt_arma");
        TextMeshProUGUI municion = CampoHUD(hud, "txt_municion");
        GameObject sigue = menu.txt_tiempo != null ? menu.txt_tiempo.gameObject : null;

        if (arma == null)
        {
            arma = CrearTextoHUD(canvas, "txt_arma", fuente, sigue);
            AsignarCampoHUD(hud, "txt_arma", arma);
        }
        if (municion == null)
        {
            municion = CrearTextoHUD(canvas, "txt_municion", fuente, sigue);
            AsignarCampoHUD(hud, "txt_municion", municion);
        }

        EstiloTexto(arma, Vector2.zero, new Vector2(300, 30), 22, true, TextoTenue, TextAlignmentOptions.Left);
        arma.characterSpacing = 3f;
        Anclar(arma.rectTransform, new Vector2(1f, 0f), new Vector2(-48, 120), new Vector2(300, 30));

        EstiloTexto(municion, Vector2.zero, new Vector2(300, 64), 50, true, Acento, TextAlignmentOptions.Left);
        Anclar(municion.rectTransform, new Vector2(1f, 0f), new Vector2(-48, 64), new Vector2(300, 64));
        municion.overflowMode = TextOverflowModes.Truncate;
        municion.textWrappingMode = TextWrappingModes.NoWrap;

        // un solo cuadro detras de las dos lineas y la barra de municion
        RectTransform fondo = CrearFondoHUD(municion.rectTransform, Vector2.zero, arma.gameObject);
        fondo.anchorMin = fondo.anchorMax = fondo.pivot = new Vector2(1f, 0f);
        fondo.sizeDelta = new Vector2(340, 150);
        fondo.anchoredPosition = new Vector2(-32, 32);
        Transform acentoFondo = fondo.Find("Acento");
        if (acentoFondo != null) ((RectTransform)acentoFondo).sizeDelta = new Vector2(5, 126);

        if (canvas.Find("Barra_municion") == null)
        {
            RectTransform pista = CrearBarra(canvas, "Barra_municion", new Color(1f, 1f, 1f, 0.12f));
            pista.anchorMin = pista.anchorMax = pista.pivot = new Vector2(1f, 0f);
            pista.sizeDelta = new Vector2(300, 8);
            pista.anchoredPosition = new Vector2(-48, 46);

            RectTransform relleno = CrearBarra(pista, "Relleno", Acento);
            relleno.anchorMin = Vector2.zero;
            relleno.anchorMax = Vector2.one;
            relleno.offsetMin = relleno.offsetMax = Vector2.zero;

            pista.gameObject.AddComponent<SeguirVisibilidad>().objetivo = arma.gameObject;
            pista.gameObject.AddComponent<HudMunicion>().Configurar(relleno, Acento, Hex("FF5252"));
        }
    }

    private static void AsignarCampoHUD(HUD hud, string campo, TextMeshProUGUI valor)
    {
        FieldInfo f = typeof(HUD).GetField(campo, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
        if (f != null) f.SetValue(hud, valor);
    }

    // texto nuevo del HUD, se oculta junto con `seguir`
    private static TextMeshProUGUI CrearTextoHUD(Transform padre, string nombre, TMP_FontAsset fuente, GameObject seguir)
    {
        Transform existente = padre.Find(nombre);
        if (existente != null) return existente.GetComponent<TextMeshProUGUI>();

        GameObject go = new GameObject(nombre, typeof(RectTransform));
        go.transform.SetParent(padre, false);
        TextMeshProUGUI t = go.AddComponent<TextMeshProUGUI>();
        if (fuente != null) t.font = fuente;
        t.raycastTarget = false;
        if (seguir != null && seguir != go) go.AddComponent<SeguirVisibilidad>().objetivo = seguir;
        return t;
    }

    private static RectTransform CrearBarra(Transform padre, string nombre, Color color)
    {
        GameObject go = new GameObject(nombre, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(padre, false);
        Image img = go.GetComponent<Image>();
        img.sprite = SpriteRedondeado();
        img.type = Image.Type.Sliced;
        img.color = color;
        img.raycastTarget = false;
        return (RectTransform)go.transform;
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
            fondo.sprite = SpriteDegradado();
            fondo.type = Image.Type.Simple;
            fondo.color = Color.white;
        }

        Transform existente = panel.Find("Tarjeta");
        RectTransform tarjeta;
        if (existente != null)
        {
            tarjeta = (RectTransform)existente;
        }
        else
        {
            // capas de atras hacia adelante: sombra, borde, tarjeta y franja de acento arriba
            CapaTarjeta(panel, "Sombra", new Color(0f, 0f, 0f, 0.45f), tamanoTarjeta + new Vector2(36, 36), new Vector2(0, -14));
            CapaTarjeta(panel, "Borde", Secundario, tamanoTarjeta + new Vector2(4, 4), Vector2.zero);
            tarjeta = CapaTarjeta(panel, "Tarjeta", Tarjeta, tamanoTarjeta, Vector2.zero);

            CapaTarjeta(panel, "Franja", Acento, new Vector2(tamanoTarjeta.x - 80, 6), new Vector2(0, tamanoTarjeta.y / 2f - 3));

            // el panel aparece con un fundido suave cada vez que se activa
            if (panel.GetComponent<PanelAnimado>() == null) panel.gameObject.AddComponent<PanelAnimado>();
        }
        Anclar(tarjeta, new Vector2(0.5f, 0.5f), Vector2.zero, tamanoTarjeta);
    }

    // imagen redondeada que no bloquea clics, se coloca detras del contenido
    private static RectTransform CapaTarjeta(Transform panel, string nombre, Color color, Vector2 tam, Vector2 pos)
    {
        GameObject go = new GameObject(nombre, typeof(RectTransform), typeof(Image));
        RectTransform rt = (RectTransform)go.transform;
        rt.SetParent(panel, false);
        Image img = go.GetComponent<Image>();
        img.sprite = SpriteRedondeado();
        img.type = Image.Type.Sliced;
        img.color = color;
        img.raycastTarget = false;
        Anclar(rt, new Vector2(0.5f, 0.5f), pos, tam);

        // quedan en orden de creacion detras del contenido existente
        int indice = 0;
        foreach (Transform hermano in panel)
        {
            if (hermano == rt) break;
            if (hermano.name == "Sombra" || hermano.name == "Borde" || hermano.name == "Tarjeta" || hermano.name == "Franja") indice++;
        }
        rt.SetSiblingIndex(indice);
        return rt;
    }

    // degradado vertical suave para el fondo de los menus
    private static Sprite SpriteDegradado()
    {
        if (degradado != null) return degradado;

        Texture2D tex = new Texture2D(4, 128, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;
        tex.filterMode = FilterMode.Bilinear;
        Color arriba = Hex("1B2030");
        Color abajo = Hex("07090D");
        for (int y = 0; y < tex.height; y++)
        {
            Color c = Color.Lerp(abajo, arriba, (float)y / (tex.height - 1));
            for (int x = 0; x < tex.width; x++) tex.SetPixel(x, y, c);
        }
        tex.Apply();
        degradado = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f));
        return degradado;
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

        if (b.GetComponent<BotonAnimado>() == null) b.gameObject.AddComponent<BotonAnimado>();

        TextMeshProUGUI txt = b.GetComponentInChildren<TextMeshProUGUI>(true);
        if (txt != null)
        {
            txt.fontSize = 30;
            txt.fontStyle = FontStyles.Bold;
            txt.characterSpacing = 2f;
            // misma fuente en todos los botones (el de "Jugar" usaba otra y se veia distinto)
            TextMeshProUGUI salir = b.transform.parent != null ? b.transform.parent.GetComponentInChildren<TextMeshProUGUI>(true) : null;
            if (fuenteBase == null && salir != null) fuenteBase = salir.font;
            if (fuenteBase != null) txt.font = fuenteBase;
            txt.raycastTarget = false;
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
            if (placeholder.text == "Enter text...") placeholder.text = "Escribe aquí...";
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

        // linea de acento a la izquierda del cuadro
        GameObject borde = new GameObject("Acento", typeof(RectTransform), typeof(Image));
        RectTransform brt = (RectTransform)borde.transform;
        brt.SetParent(rt, false);
        brt.anchorMin = new Vector2(0f, 0.5f);
        brt.anchorMax = new Vector2(0f, 0.5f);
        brt.pivot = new Vector2(0f, 0.5f);
        brt.sizeDelta = new Vector2(5, rt.sizeDelta.y - 24);
        brt.anchoredPosition = new Vector2(8, 0);
        Image bimg = borde.GetComponent<Image>();
        bimg.sprite = SpriteRedondeado();
        bimg.type = Image.Type.Sliced;
        bimg.color = Acento;
        bimg.raycastTarget = false;

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

// Fundido suave cada vez que el panel se activa
public class PanelAnimado : MonoBehaviour
{
    private CanvasGroup grupo;
    private float tiempo;
    private const float Duracion = 0.25f;

    private void OnEnable()
    {
        grupo = GetComponent<CanvasGroup>();
        if (grupo == null) grupo = gameObject.AddComponent<CanvasGroup>();
        grupo.alpha = 0f;
        tiempo = 0f;
        // la pantalla final tiene su fanfarria, los demas paneles un "swish" suave
        Sonidos.Reproducir(gameObject.name == "FinPartida" ? "fin_partida" : "panel", gameObject.name == "FinPartida" ? 0.9f : 0.5f);
    }

    private void Update()
    {
        if (grupo == null || grupo.alpha >= 1f) return;
        tiempo += Time.unscaledDeltaTime;
        float t = Mathf.Clamp01(tiempo / Duracion);
        grupo.alpha = 1f - (1f - t) * (1f - t);
    }
}

// Los botones crecen un poco al pasar el mouse y se hunden al pulsarlos
[RequireComponent(typeof(Button))]
public class BotonAnimado : MonoBehaviour, UnityEngine.EventSystems.IPointerEnterHandler, UnityEngine.EventSystems.IPointerExitHandler,
    UnityEngine.EventSystems.IPointerDownHandler, UnityEngine.EventSystems.IPointerUpHandler, UnityEngine.EventSystems.IPointerClickHandler
{
    private float objetivo = 1f;
    private bool encima;

    public void OnPointerEnter(UnityEngine.EventSystems.PointerEventData e)
    {
        encima = true;
        objetivo = 1.04f;
        Sonidos.Reproducir("ui_hover", 0.5f);
    }

    public void OnPointerClick(UnityEngine.EventSystems.PointerEventData e) { Sonidos.Reproducir("ui_click", 0.8f); }
    public void OnPointerExit(UnityEngine.EventSystems.PointerEventData e) { encima = false; objetivo = 1f; }
    public void OnPointerDown(UnityEngine.EventSystems.PointerEventData e) { objetivo = 0.97f; }
    public void OnPointerUp(UnityEngine.EventSystems.PointerEventData e) { objetivo = encima ? 1.04f : 1f; }

    private void OnDisable() { transform.localScale = Vector3.one; objetivo = 1f; encima = false; }

    private void Update()
    {
        float actual = transform.localScale.x;
        if (Mathf.Approximately(actual, objetivo)) return;
        float nuevo = Mathf.Lerp(actual, objetivo, 1f - Mathf.Exp(-18f * Time.unscaledDeltaTime));
        transform.localScale = new Vector3(nuevo, nuevo, 1f);
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
        bool ocultar = !objetivo.activeInHierarchy;
        foreach (CanvasRenderer cr in GetComponentsInChildren<CanvasRenderer>())
        {
            cr.cull = ocultar;
        }
    }
}

// Tiempo en formato m:ss; se pone rojo y late en los ultimos 10 segundos
public class HudTiempo : MonoBehaviour
{
    private TextMeshProUGUI texto;
    private RectTransform fondo;
    private Color normal, urgente, textoNormal;
    private int ultimoTick = -1;

    public void Configurar(RectTransform fondoTiempo, Color colorNormal, Color colorUrgente, Color colorTexto)
    {
        texto = GetComponent<TextMeshProUGUI>();
        fondo = fondoTiempo;
        normal = colorNormal;
        urgente = colorUrgente;
        textoNormal = colorTexto;
    }

    private void LateUpdate() // despues de que GameManager escribe el texto
    {
        if (texto == null) return;

        string crudo = System.Text.RegularExpressions.Regex.Replace(texto.text, "<.*?>", "");
        if (float.TryParse(crudo.Trim(), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float segundos))
        {
            int total = Mathf.CeilToInt(Mathf.Max(0f, segundos));
            bool poco = total <= 10;
            texto.text = (total / 60) + ":" + (total % 60).ToString("00");
            texto.color = poco ? urgente : textoNormal;
            if (poco && total != ultimoTick && total > 0) Sonidos.Reproducir("tick", 0.6f);
            ultimoTick = total;

            float pulso = poco ? 1f + 0.06f * Mathf.Abs(Mathf.Sin(Time.unscaledTime * 4f)) : 1f;
            transform.localScale = Vector3.one * pulso;
            if (fondo != null)
            {
                fondo.localScale = Vector3.one * pulso;
                Transform acento = fondo.Find("Acento");
                if (acento != null) acento.GetComponent<Image>().color = poco ? urgente : normal;
            }
        }
    }
}

// Marcador con puesto, nombre y bajas alineadas; el cuadro se ajusta al numero de jugadores
public class HudMarcador : MonoBehaviour
{
    private TextMeshProUGUI lista;
    private RectTransform fondo;
    private Color acento, tenue;

    public void Configurar(RectTransform fondoMarcador, Color colorAcento, Color colorTenue)
    {
        lista = GetComponent<TextMeshProUGUI>();
        fondo = fondoMarcador;
        acento = colorAcento;
        tenue = colorTenue;
    }

    private void LateUpdate() // despues de HUD.Update, que reescribe la lista cada cuadro
    {
        if (lista == null) return;

        System.Text.StringBuilder sb = new System.Text.StringBuilder();
        int puesto = 0;
        foreach (string linea in lista.text.Split('\n'))
        {
            if (string.IsNullOrWhiteSpace(linea)) continue;
            int sep = linea.LastIndexOf("  -  ");
            if (sep < 0) { sb.Append(linea).Append('\n'); continue; }

            string nombre = linea.Substring(0, sep);
            string bajas = linea.Substring(sep + 5).Replace(" bajas", "").Trim();
            puesto++;
            string colorPuesto = puesto == 1 ? "FFC857" : ColorUtility.ToHtmlStringRGB(tenue); // el primero en dorado
            sb.Append("<color=#").Append(colorPuesto).Append("><b>").Append(puesto).Append("</b></color>  ")
              .Append(nombre)
              .Append("<pos=88%><b><color=#").Append(ColorUtility.ToHtmlStringRGB(acento)).Append('>').Append(bajas).Append("</color></b>\n");
        }
        lista.text = sb.ToString().TrimEnd('\n');

        if (fondo != null)
        {
            float alto = 64 + lista.GetPreferredValues(lista.text).y + 16;
            fondo.sizeDelta = new Vector2(340, alto);
            Transform barra = fondo.Find("Acento");
            if (barra != null) ((RectTransform)barra).sizeDelta = new Vector2(5, alto - 24);
        }
    }
}

// Barra de municion del arma del jugador local
public class HudMunicion : MonoBehaviour
{
    private RectTransform relleno;
    private Image imagen;
    private Color normal, bajo;
    private Personaje jugador;
    private float actual = 1f;

    public void Configurar(RectTransform barraRelleno, Color colorNormal, Color colorBajo)
    {
        relleno = barraRelleno;
        imagen = barraRelleno.GetComponent<Image>();
        normal = colorNormal;
        bajo = colorBajo;
    }

    private void LateUpdate()
    {
        if (relleno == null) return;
        if (jugador == null || (jugador.IsSpawned && !jugador.IsOwner))
        {
            jugador = null;
            foreach (Personaje p in FindObjectsByType<Personaje>(FindObjectsSortMode.None))
            {
                if (!p.IsSpawned || p.IsOwner) { jugador = p; break; }
            }
        }

        Arma arma = jugador != null ? jugador.armaEquipada : null;
        float objetivo = 0f;
        if (arma != null && !arma.EnCaja && arma.MunicionMaxima > 0)
        {
            if (arma.Recargando) objetivo = 0f;
            else if (arma.Modo == ModoDisparo.RayoContinuo && arma.CargandoRayo)
                objetivo = arma.TiempoCargaActual / Mathf.Max(arma.TiempoCargaRequerido, 0.01f);
            else
                objetivo = (float)arma.MunicionActual / arma.MunicionMaxima;
        }

        actual = Mathf.MoveTowards(actual, objetivo, 3f * Time.unscaledDeltaTime);
        relleno.anchorMax = new Vector2(Mathf.Clamp01(actual), 1f);
        imagen.color = actual < 0.25f ? bajo : normal;
    }
}

// Al cambiar el mapa activo: la pantalla arranca en negro, se abre, y baja un cartel con el nombre del mapa.
// Solo observa que mapa esta encendido; no cambia nada de la logica de las rondas.
public class TransicionMapa : MonoBehaviour
{
    private inicio menu;
    private Image cortina;
    private RectTransform cartel;
    private TextMeshProUGUI nombre;
    private List<GameObject> mapas = new List<GameObject>();
    private string ultimoMapa;
    private Coroutine actual;

    private const float YOculto = 160f;
    private const float YVisible = -40f;

    public void Configurar(inicio inicioMenu, Image cortinaPantalla, RectTransform cartelMapa, TextMeshProUGUI textoNombre)
    {
        menu = inicioMenu;
        cortina = cortinaPantalla;
        cartel = cartelMapa;
        nombre = textoNombre;
    }

    private void Update()
    {
        if (menu == null) return;
        if (mapas.Count == 0) mapas = menu.Mapas;

        // solo cuenta durante la partida (el tiempo se muestra al empezar cada ronda)
        string activo = null;
        if (menu.txt_tiempo != null && menu.txt_tiempo.gameObject.activeInHierarchy)
        {
            foreach (GameObject m in mapas)
            {
                if (m != null && m.activeSelf) { activo = m.name; break; }
            }
        }

        if (activo == ultimoMapa) return;
        ultimoMapa = activo;
        if (activo == null) { Ocultar(); return; }

        if (actual != null) StopCoroutine(actual);
        actual = StartCoroutine(Reproducir(NombreLegible(activo)));
    }

    private void OnDisable()
    {
        Ocultar();
    }

    private void Ocultar()
    {
        if (actual != null) { StopCoroutine(actual); actual = null; }
        if (cortina != null) cortina.color = new Color(cortina.color.r, cortina.color.g, cortina.color.b, 0f);
        if (cartel != null) cartel.gameObject.SetActive(false);
    }

    private System.Collections.IEnumerator Reproducir(string texto)
    {
        nombre.text = texto;
        Sonidos.Reproducir("inicio_ronda", 0.7f);
        cartel.gameObject.SetActive(true);
        cartel.anchoredPosition = new Vector2(0, YOculto);
        SetCortina(1f);

        // un instante en negro mientras el jugador aparece en el mapa nuevo
        yield return Esperar(0.3f);

        // la pantalla se abre y el cartel baja con un pequeno rebote
        for (float t = 0f; t < 1f; t += Time.unscaledDeltaTime / 0.8f)
        {
            SetCortina(1f - Mathf.SmoothStep(0f, 1f, t));
            float baja = Mathf.Clamp01(t * 1.4f);
            cartel.anchoredPosition = new Vector2(0, Mathf.LerpUnclamped(YOculto, YVisible, EaseOutBack(baja)));
            yield return null;
        }
        SetCortina(0f);
        cartel.anchoredPosition = new Vector2(0, YVisible);

        yield return Esperar(2.4f);

        // el cartel sube y desaparece
        for (float t = 0f; t < 1f; t += Time.unscaledDeltaTime / 0.45f)
        {
            cartel.anchoredPosition = new Vector2(0, Mathf.Lerp(YVisible, YOculto, t * t));
            yield return null;
        }
        cartel.gameObject.SetActive(false);
        actual = null;
    }

    private static System.Collections.IEnumerator Esperar(float segundos)
    {
        for (float t = 0f; t < segundos; t += Time.unscaledDeltaTime) yield return null;
    }

    private void SetCortina(float alfa)
    {
        Color c = cortina.color;
        cortina.color = new Color(c.r, c.g, c.b, alfa);
    }

    private static float EaseOutBack(float t)
    {
        const float c1 = 1.70158f;
        const float c3 = c1 + 1f;
        return 1f + c3 * Mathf.Pow(t - 1f, 3f) + c1 * Mathf.Pow(t - 1f, 2f);
    }

    // "ecenaCochera" -> "Cochera", "ecenaPatioTrasero" -> "Patio Trasero"
    private static string NombreLegible(string objeto)
    {
        string n = objeto;
        if (n.StartsWith("ecena", System.StringComparison.OrdinalIgnoreCase)) n = n.Substring(5);
        n = n.Replace("_", " ").Trim();
        if (n.Length == 0) return objeto;

        System.Text.StringBuilder sb = new System.Text.StringBuilder();
        for (int i = 0; i < n.Length; i++)
        {
            if (i > 0 && char.IsUpper(n[i]) && char.IsLower(n[i - 1])) sb.Append(' ');
            sb.Append(i == 0 ? char.ToUpper(n[i]) : n[i]);
        }
        string resultado = sb.ToString();
        if (resultado == "Cosina") resultado = "Cocina"; // el objeto de la escena esta mal escrito
        return resultado;
    }
}
