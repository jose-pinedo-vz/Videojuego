using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// Pantallas "Ajustes de volumen" y "Ajustes de teclado" del menu principal.
// Se crean por codigo (la escena no se toca) y usan el estilo de EstiloUI. Solo se pueden abrir desde el menu, nunca en partida.
public static partial class EstiloUI
{
    private static void CrearMenuAjustes(Transform canvas, Transform panelInicio, inicio menu, TMP_FontAsset fuente)
    {
        if (panelInicio.Find("btn_ajustesVolumen") == null)
        {
            Button bVol = CrearBotonSimple(panelInicio, "btn_ajustesVolumen", fuente, "Ajustes de volumen");
            bVol.onClick.AddListener(menu.abrirAjustesVolumen);
            EstiloBoton(bVol, false, new Vector2(0, -40), new Vector2(420, 64));

            Button bTec = CrearBotonSimple(panelInicio, "btn_ajustesTeclado", fuente, "Ajustes de teclado");
            bTec.onClick.AddListener(menu.abrirAjustesTeclado);
            EstiloBoton(bTec, false, new Vector2(0, -125), new Vector2(420, 64));
        }

        CrearPanelVolumen(canvas, menu, fuente);
        CrearPanelTeclado(canvas, menu, fuente);
    }

    private static GameObject NuevoPanelAjustes(Transform canvas, string nombre, Vector2 tarjeta)
    {
        GameObject panel = new GameObject(nombre, typeof(RectTransform), typeof(Image));
        panel.transform.SetParent(canvas, false);
        RectTransform rp = (RectTransform)panel.transform;
        rp.anchorMin = Vector2.zero;
        rp.anchorMax = Vector2.one;
        rp.offsetMin = rp.offsetMax = Vector2.zero;
        EstiloPanel(panel.transform, tarjeta);
        return panel;
    }

    // ---------------------------------------------------------------- volumen

    private static void CrearPanelVolumen(Transform canvas, inicio menu, TMP_FontAsset fuente)
    {
        Transform existente = canvas.Find("AjustesVolumen");
        if (existente != null) { menu.PanelAjustesVolumen = existente.gameObject; return; }

        GameObject panel = NuevoPanelAjustes(canvas, "AjustesVolumen", new Vector2(820, 860));
        Transform t = panel.transform;

        EstiloTitulo(CrearTextoSimple(t, "Titulo", fuente, "Ajustes de volumen"), new Vector2(0, 360), new Vector2(740, 70), 44);

        // cada fila: nombre arriba a la izquierda, porcentaje arriba a la derecha y la barra debajo (nada se empalma)
        string[] nombres = { "Volumen general", "Música del lobby", "Música de juego", "Sonido de armas", "Efectos" };
        float[] inicial = { AjustesJuego.General, AjustesJuego.MusicaLobby, AjustesJuego.Musica, AjustesJuego.Armas, AjustesJuego.Efectos };
        Action<float>[] fijar = { AjustesJuego.FijarGeneral, AjustesJuego.FijarMusicaLobby, AjustesJuego.FijarMusica, AjustesJuego.FijarArmas, AjustesJuego.FijarEfectos };
        Func<float>[] actual = { () => AjustesJuego.General, () => AjustesJuego.MusicaLobby, () => AjustesJuego.Musica, () => AjustesJuego.Armas, () => AjustesJuego.Efectos };

        int n = nombres.Length;
        Slider[] sliders = new Slider[n];
        TextMeshProUGUI[] valores = new TextMeshProUGUI[n];
        for (int i = 0; i < n; i++)
            sliders[i] = FilaVolumen(t, fuente, nombres[i], 250 - i * 105, inicial[i], fijar[i], out valores[i]);

        TextMeshProUGUI nota = CrearTextoSimple(t, "Nota", fuente, "La música puede subir hasta 3.5 veces su volumen original.");
        EstiloTexto(nota, new Vector2(0, -290), new Vector2(740, 36), 20, false, TextoTenue, TextAlignmentOptions.Center);

        Button restablecer = CrearBotonSimple(t, "btn_restablecerVolumen", fuente, "Restablecer");
        EstiloBoton(restablecer, false, new Vector2(-135, -360), new Vector2(240, 60));
        restablecer.onClick.AddListener(() =>
        {
            AjustesJuego.RestablecerVolumen();
            for (int i = 0; i < n; i++)
            {
                sliders[i].SetValueWithoutNotify(actual[i]());
                valores[i].text = Porcentaje(actual[i]());
            }
            AjustesJuego.GuardarSiHuboCambios();
        });

        Button atras = CrearBotonSimple(t, "btn_atrasVolumen", fuente, "Atrás");
        EstiloBoton(atras, true, new Vector2(135, -360), new Vector2(240, 60));
        atras.onClick.AddListener(menu.cerrarAjustes);

        menu.PanelAjustesVolumen = panel;
        panel.SetActive(false);
    }

    private static string Porcentaje(float v)
    {
        return Mathf.RoundToInt(v * 100f) + "%";
    }

    // etiqueta, slider y porcentaje; el valor se aplica al instante y se guarda al soltar el slider
    private static Slider FilaVolumen(Transform panel, TMP_FontAsset fuente, string etiqueta, float y, float inicial,
        Action<float> fijar, out TextMeshProUGUI valor)
    {
        TextMeshProUGUI txtEtiqueta = CrearTextoSimple(panel, "Etiqueta_" + etiqueta, fuente, etiqueta);
        EstiloTexto(txtEtiqueta, new Vector2(-350, y + 26), new Vector2(480, 40), 28, false, TextoClaro, TextAlignmentOptions.MidlineLeft);
        RectTransform re = txtEtiqueta.rectTransform;
        re.pivot = new Vector2(0f, 0.5f);
        re.anchoredPosition = new Vector2(-350, y + 26);

        TextMeshProUGUI txtValor = CrearTextoSimple(panel, "Valor_" + etiqueta, fuente, Porcentaje(inicial));
        EstiloTexto(txtValor, new Vector2(300, y + 26), new Vector2(110, 40), 30, true, Acento, TextAlignmentOptions.MidlineRight);
        RectTransform rv = txtValor.rectTransform;
        rv.pivot = new Vector2(1f, 0.5f);
        rv.anchoredPosition = new Vector2(350, y + 26);
        valor = txtValor;

        Slider slider = CrearSlider(panel, "Slider_" + etiqueta, new Vector2(0, y - 22), new Vector2(700, 32), inicial);
        slider.onValueChanged.AddListener(v =>
        {
            fijar(v);
            txtValor.text = Porcentaje(v);
        });

        // al soltar: se guarda y suena un clic para oir el nuevo volumen de efectos
        EventTrigger disparador = slider.gameObject.AddComponent<EventTrigger>();
        EventTrigger.Entry soltar = new EventTrigger.Entry { eventID = EventTriggerType.PointerUp };
        soltar.callback.AddListener(_ =>
        {
            AjustesJuego.GuardarSiHuboCambios();
            Sonidos.Reproducir("ui_click");
        });
        disparador.triggers.Add(soltar);
        return slider;
    }

    // slider armado a mano: pista fina, relleno de acento y manija redonda
    private static Slider CrearSlider(Transform padre, string nombre, Vector2 pos, Vector2 tam, float valor)
    {
        GameObject go = new GameObject(nombre, typeof(RectTransform), typeof(Slider));
        go.transform.SetParent(padre, false);
        Anclar((RectTransform)go.transform, new Vector2(0.5f, 0.5f), pos, tam);

        // pista (tambien recibe el clic para saltar a ese punto)
        Image fondo = NuevaImagen(go.transform, "Pista", CampoTexto, true);
        RectTransform rf = fondo.rectTransform;
        rf.anchorMin = new Vector2(0f, 0.3f); rf.anchorMax = new Vector2(1f, 0.7f);
        rf.offsetMin = rf.offsetMax = Vector2.zero;

        RectTransform areaRelleno = new GameObject("Area de relleno", typeof(RectTransform)).GetComponent<RectTransform>();
        areaRelleno.SetParent(go.transform, false);
        areaRelleno.anchorMin = new Vector2(0f, 0.3f); areaRelleno.anchorMax = new Vector2(1f, 0.7f);
        areaRelleno.offsetMin = new Vector2(0, 0); areaRelleno.offsetMax = new Vector2(-tam.y * 0.5f, 0);

        Image relleno = NuevaImagen(areaRelleno, "Relleno", Acento, false);
        relleno.rectTransform.anchorMin = Vector2.zero; relleno.rectTransform.anchorMax = new Vector2(0f, 1f);
        relleno.rectTransform.offsetMin = relleno.rectTransform.offsetMax = Vector2.zero;

        RectTransform areaManija = new GameObject("Area de manija", typeof(RectTransform)).GetComponent<RectTransform>();
        areaManija.SetParent(go.transform, false);
        areaManija.anchorMin = Vector2.zero; areaManija.anchorMax = Vector2.one;
        areaManija.offsetMin = new Vector2(tam.y * 0.5f, 0); areaManija.offsetMax = new Vector2(-tam.y * 0.5f, 0);

        Image manija = NuevaImagen(areaManija, "Manija", TextoClaro, true);
        manija.rectTransform.sizeDelta = new Vector2(tam.y, 0f);

        Slider s = go.GetComponent<Slider>();
        s.fillRect = relleno.rectTransform;
        s.handleRect = manija.rectTransform;
        s.targetGraphic = manija;
        s.direction = Slider.Direction.LeftToRight;
        s.minValue = 0f;
        s.maxValue = 1f;
        s.wholeNumbers = false;
        s.navigation = new Navigation { mode = Navigation.Mode.None };
        s.SetValueWithoutNotify(valor);
        return s;
    }

    private static Image NuevaImagen(Transform padre, string nombre, Color color, bool recibeClic)
    {
        GameObject go = new GameObject(nombre, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(padre, false);
        Image img = go.GetComponent<Image>();
        img.sprite = SpriteRedondeado();
        img.type = Image.Type.Sliced;
        img.color = color;
        img.raycastTarget = recibeClic;
        return img;
    }

    // ---------------------------------------------------------------- teclado

    private static void CrearPanelTeclado(Transform canvas, inicio menu, TMP_FontAsset fuente)
    {
        Transform existente = canvas.Find("AjustesTeclado");
        if (existente != null) { menu.PanelAjustesTeclado = existente.gameObject; return; }

        GameObject panel = NuevoPanelAjustes(canvas, "AjustesTeclado", new Vector2(820, 800));
        Transform t = panel.transform;

        EstiloTitulo(CrearTextoSimple(t, "Titulo", fuente, "Ajustes de teclado"), new Vector2(0, 340), new Vector2(740, 70), 44);

        int total = Enum.GetValues(typeof(Accion)).Length;
        TextMeshProUGUI[] textosTecla = new TextMeshProUGUI[total];
        Button[] botones = new Button[total];
        TextMeshProUGUI mensaje = CrearTextoSimple(t, "Mensaje", fuente, "Haz clic en una tecla y presiona la nueva. Esc cancela.");
        EstiloTexto(mensaje, new Vector2(0, -290), new Vector2(760, 60), 22, false, TextoTenue, TextAlignmentOptions.Center);

        CapturaTecla captura = panel.AddComponent<CapturaTecla>();
        captura.enabled = false;

        Action refrescar = () =>
        {
            for (int i = 0; i < total; i++) textosTecla[i].text = AjustesJuego.NombreTecla((Accion)i);
        };

        for (int i = 0; i < total; i++)
        {
            Accion accion = (Accion)i;
            float y = 255 - i * 60;

            TextMeshProUGUI etiqueta = CrearTextoSimple(t, "Etiqueta_" + accion, fuente, AjustesJuego.NombreAccion(accion));
            EstiloTexto(etiqueta, new Vector2(-370, y), new Vector2(380, 50), 28, false, TextoClaro, TextAlignmentOptions.MidlineLeft);
            RectTransform re = etiqueta.rectTransform;
            re.pivot = new Vector2(0f, 0.5f);
            re.anchoredPosition = new Vector2(-370, y);

            Button b = CrearBotonSimple(t, "btn_tecla_" + accion, fuente, "");
            EstiloBoton(b, false, new Vector2(230, y), new Vector2(240, 50));
            TextMeshProUGUI txt = b.GetComponentInChildren<TextMeshProUGUI>(true);
            txt.fontSize = 26;
            textosTecla[i] = txt;
            botones[i] = b;

            int indice = i;
            b.onClick.AddListener(() =>
            {
                if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null); // Enter/Espacio no deben volver a "pulsar" el boton
                captura.Cancelar();
                refrescar();
                textosTecla[indice].text = "Presiona una tecla...";
                mensaje.text = "Presiona la nueva tecla para: " + AjustesJuego.NombreAccion(accion) + "  (Esc cancela)";
                captura.Empezar(accion, intercambiada =>
                {
                    refrescar();
                    if (intercambiada.HasValue)
                        mensaje.text = "Esa tecla la usaba \"" + AjustesJuego.NombreAccion(intercambiada.Value) + "\"; se intercambiaron.";
                    else
                        mensaje.text = "Listo. Haz clic en otra acción para cambiarla.";
                }, () =>
                {
                    refrescar();
                    mensaje.text = "Haz clic en una tecla y presiona la nueva. Esc cancela.";
                });
            });
        }

        Button restablecer = CrearBotonSimple(t, "btn_restablecerTeclas", fuente, "Restablecer");
        EstiloBoton(restablecer, false, new Vector2(-135, -350), new Vector2(240, 60));
        restablecer.onClick.AddListener(() =>
        {
            captura.Cancelar();
            AjustesJuego.RestablecerTeclas();
            AjustesJuego.GuardarSiHuboCambios();
            refrescar();
            mensaje.text = "Teclas restablecidas.";
        });

        Button atras = CrearBotonSimple(t, "btn_atrasTeclado", fuente, "Atrás");
        EstiloBoton(atras, true, new Vector2(135, -350), new Vector2(240, 60));
        atras.onClick.AddListener(menu.cerrarAjustes);

        // al abrir el panel siempre se muestran las teclas actuales
        panel.AddComponent<RefrescoAlAbrir>().alAbrir = () =>
        {
            captura.Cancelar();
            refrescar();
            mensaje.text = "Haz clic en una tecla y presiona la nueva. Esc cancela.";
        };

        menu.PanelAjustesTeclado = panel;
        panel.SetActive(false);
    }
}

// Espera la proxima tecla SOLO mientras se esta reasignando una accion (el resto del tiempo esta apagado y no consume nada).
public class CapturaTecla : MonoBehaviour
{
    private Accion accion;
    private Action<Accion?> alAsignar;
    private Action alCancelar;

    public void Empezar(Accion cual, Action<Accion?> asignada, Action cancelada)
    {
        accion = cual;
        alAsignar = asignada;
        alCancelar = cancelada;
        enabled = true;
    }

    public void Cancelar()
    {
        if (!enabled) return;
        enabled = false;
        Action cb = alCancelar;
        alAsignar = null;
        alCancelar = null;
        if (cb != null) cb();
    }

    private void OnDisable()
    {
        alAsignar = null;
        alCancelar = null;
    }

    private void Update()
    {
        Keyboard teclado = Keyboard.current;
        if (teclado == null) return;
        if (!teclado.anyKey.wasPressedThisFrame) return; // casi siempre sale aqui, sin recorrer las teclas

        if (teclado.escapeKey.wasPressedThisFrame) { Cancelar(); return; }

        foreach (var control in teclado.allKeys)
        {
            if (!control.wasPressedThisFrame) continue;
            if (!AjustesJuego.AsignarTecla(accion, control.keyCode, out Accion? otra)) continue; // tecla no permitida: sigue esperando

            AjustesJuego.GuardarSiHuboCambios(); // se guarda en cuanto cambia
            Action<Accion?> cb = alAsignar;
            enabled = false;
            if (cb != null) cb(otra);
            return;
        }
    }
}

// avisa cada vez que se activa el panel, para mostrar los datos actuales
public class RefrescoAlAbrir : MonoBehaviour
{
    public Action alAbrir;
    private void OnEnable()
    {
        if (alAbrir != null) alAbrir();
    }
}
