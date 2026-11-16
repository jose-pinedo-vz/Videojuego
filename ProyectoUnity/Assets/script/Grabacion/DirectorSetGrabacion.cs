using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using TMPro;
using Unity.Collections;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Grabacion
{
    /// <summary>
    /// Controla el Set de Grabacion en segundo plano: el jugador siempre juega con Main Camera (vista normal),
    /// mientras CamaraGrabacion captura al RenderTexture sin HUD ni controles, con vista PiP opcional (F11).
    /// </summary>
    [DefaultExecutionOrder(450)]
    public class DirectorSetGrabacion : MonoBehaviour
    {
        public const string NOMBRE_ESCENA_SET = "SetGrabacion";
        private const string PREF_TOMA = "SetGrabacion_Toma";
        private const string PREF_CALIDAD = "SetGrabacion_Calidad";
        private const string PREF_AUTO_GRABAR = "SetGrabacion_AutoGrabar";
        private const string PREF_PIP_ACTIVO = "SetGrabacion_MostrarPiP";
        private const string PREF_OCULTAR_NOMBRES = "SetGrabacion_OcultarNombres";
        private const string PREF_AUTO_MP4 = "SetGrabacion_AutoMp4";
        private const string PREF_VOLTEAR_Y = "SetGrabacion_VoltearY";
        private const int MAX_LECTURAS_EN_VUELO = 3;

        private struct PresetCalidad
        {
            public string Etiqueta;
            public int Ancho;
            public int Alto;
            public int Fps;
            public int CalidadJpg;
        }

        private static readonly PresetCalidad[] Presets =
        {
            new PresetCalidad { Etiqueta = "720p · 30 fps",  Ancho = 1280, Alto = 720,  Fps = 30, CalidadJpg = 82 },
            new PresetCalidad { Etiqueta = "1080p · 30 fps", Ancho = 1920, Alto = 1080, Fps = 30, CalidadJpg = 80 },
            new PresetCalidad { Etiqueta = "1080p · 60 fps", Ancho = 1920, Alto = 1080, Fps = 60, CalidadJpg = 78 }
        };

        public static DirectorSetGrabacion Instancia { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void InicializarAlCargar()
        {
            SceneManager.sceneLoaded -= AlCargarEscena;
            SceneManager.sceneLoaded += AlCargarEscena;
            VerificarEscenaActual(SceneManager.GetActiveScene());
        }

        private static void AlCargarEscena(Scene escena, LoadSceneMode modo)
        {
            VerificarEscenaActual(escena);
        }

        private static void VerificarEscenaActual(Scene escena)
        {
            if (!string.Equals(escena.name, NOMBRE_ESCENA_SET, StringComparison.OrdinalIgnoreCase)) return;

            DirectorSetGrabacion existente = FindAnyObjectByType<DirectorSetGrabacion>();
            if (existente != null) return;

            GameObject go = new GameObject("DirectorSetGrabacion");
            go.AddComponent<DirectorSetGrabacion>();
        }

        public static void NotificarInicioPartida()
        {
            if (Instancia != null) Instancia.AlIniciarPartida();
        }

        public static void NotificarFinPartida()
        {
            if (Instancia != null) Instancia.AlTerminarPartida();
        }

        public static void NotificarCambioEscena()
        {
            if (Instancia != null) Instancia.AlSalirDeEscena();
        }

        private CamaraSetGrabacion camaras;
        private CapturaAudioGrabacion capturaAudio;
        private Camera camaraPrincipal;
        private RenderTexture rtCaptura;
        private EscritorAVI escritor;

        private bool enPartida;
        private bool grabando;
        private bool pausado;
        private bool capturaPendienteEsteCuadro;
        private int indiceCalidad;
        private bool autoGrabar = true;
        private bool mostrarPiP = true;
        private bool ocultarNombres3D = true;
        private bool autoconvertirMp4 = true;
        private bool voltearVertical;
        private int indiceParte = 1;
        private string prefijoSesionActual;
        private double tiempoProximoFrame;
        private double tiempoProximoPreview;
        private int lecturasEnVuelo;
        private readonly System.Collections.Generic.List<Renderer> renderersCuerpoOcultosTemp = new System.Collections.Generic.List<Renderer>();
        private int anchoCaptura;
        private int altoCaptura;
        private int fpsCaptura;
        private int calidadJpgCaptura;
        private string mensajeEstado = "Listo para grabar en 2º plano";
        private readonly ConcurrentQueue<string> mensajesHilo = new ConcurrentQueue<string>();

        // UI aislada en ScreenSpaceOverlay (nunca entra al video de CamaraGrabacion)
        private Canvas canvasStudio;
        private GameObject panelMenuStudio;
        private GameObject panelMenuContenido;
        private TextMeshProUGUI txtBtnColapsar;
        private GameObject barraEnVivo;
        private GameObject panelPiP;
        private RawImage imagenPiP;
        private bool menuColapsado;

        private Button[] botonesCamara;
        private TextMeshProUGUI txtCalidad;
        private TextMeshProUGUI txtAutoGrabar;
        private TextMeshProUGUI txtPiPMenu;
        private TextMeshProUGUI txtOcultarNombres;
        private TextMeshProUGUI txtAutoMp4;
        private TextMeshProUGUI txtVoltearY;
        private TextMeshProUGUI txtOffset1P;
        private TextMeshProUGUI txtEstadoMenu;
        private TextMeshProUGUI txtRutaCarpeta;
        private TextMeshProUGUI txtBtnRecMenu;
        private TextMeshProUGUI txtBtnPausaMenu;

        private TextMeshProUGUI txtEstadoEnVivo;
        private TextMeshProUGUI txtCamaraEnVivo;
        private TextMeshProUGUI txtBtnRecVivo;
        private TextMeshProUGUI txtBtnPausaVivo;
        private TextMeshProUGUI txtBtnPiPVivo;
        private Image puntoRecVivo;

        private TMP_FontAsset fuenteUI;
        private Sprite spriteRedondeado;

        private void Awake()
        {
            if (Instancia != null && Instancia != this)
            {
                Destroy(gameObject);
                return;
            }
            Instancia = this;

            CargarPreferencias();

            camaras = GetComponent<CamaraSetGrabacion>();
            if (camaras == null) camaras = gameObject.AddComponent<CamaraSetGrabacion>();
            camaras.EstablecerToma((TomaCamara)Mathf.Clamp(PlayerPrefs.GetInt(PREF_TOMA, 0), 0, 3));

            AsegurarReferenciasEscena();
            ConstruirUIEstudio();
            RefrescarUI();
        }

        private void OnEnable()
        {
            RenderPipelineManager.beginCameraRendering += AlIniciarRenderCamara;
            RenderPipelineManager.endCameraRendering += AlTerminarRenderCamara;
        }

        private void OnDisable()
        {
            RenderPipelineManager.beginCameraRendering -= AlIniciarRenderCamara;
            RenderPipelineManager.endCameraRendering -= AlTerminarRenderCamara;
            RestaurarCuerpoObjetivoTemporalmente();
            Red.EstablecerOcultamientoTemporalPorRender(false);
            DetenerGrabacion("Grabacion cerrada");
            camaras?.EstablecerRenderActivo(false);
        }

        private void OnApplicationQuit()
        {
            DetenerGrabacion("Guardado al salir");
        }

        private void OnDestroy()
        {
            if (Instancia == this) Instancia = null;
            if (camaras != null) camaras.FijarTargetTexture(null);
            if (rtCaptura != null)
            {
                rtCaptura.Release();
                Destroy(rtCaptura);
                rtCaptura = null;
            }
        }

        private void CargarPreferencias()
        {
            indiceCalidad = Mathf.Clamp(PlayerPrefs.GetInt(PREF_CALIDAD, 0), 0, Presets.Length - 1);
            autoGrabar = PlayerPrefs.GetInt(PREF_AUTO_GRABAR, 1) == 1;
            mostrarPiP = PlayerPrefs.GetInt(PREF_PIP_ACTIVO, 1) == 1;
            ocultarNombres3D = PlayerPrefs.GetInt(PREF_OCULTAR_NOMBRES, 1) == 1;
            autoconvertirMp4 = PlayerPrefs.GetInt(PREF_AUTO_MP4, 1) == 1;
            voltearVertical = PlayerPrefs.GetInt(PREF_VOLTEAR_Y, 0) == 1;
        }

        private void GuardarPreferencias()
        {
            if (camaras != null) PlayerPrefs.SetInt(PREF_TOMA, (int)camaras.TomaActual);
            PlayerPrefs.SetInt(PREF_CALIDAD, indiceCalidad);
            PlayerPrefs.SetInt(PREF_AUTO_GRABAR, autoGrabar ? 1 : 0);
            PlayerPrefs.SetInt(PREF_PIP_ACTIVO, mostrarPiP ? 1 : 0);
            PlayerPrefs.SetInt(PREF_OCULTAR_NOMBRES, ocultarNombres3D ? 1 : 0);
            PlayerPrefs.SetInt(PREF_AUTO_MP4, autoconvertirMp4 ? 1 : 0);
            PlayerPrefs.SetInt(PREF_VOLTEAR_Y, voltearVertical ? 1 : 0);
            PlayerPrefs.Save();
        }

        private void AsegurarReferenciasEscena()
        {
            if (camaraPrincipal == null) camaraPrincipal = Camera.main;
            if (camaraPrincipal != null && capturaAudio == null)
            {
                capturaAudio = camaraPrincipal.GetComponent<CapturaAudioGrabacion>();
                if (capturaAudio == null) capturaAudio = camaraPrincipal.gameObject.AddComponent<CapturaAudioGrabacion>();
            }
            camaras?.AsegurarCamaras();
        }

        private void Update()
        {
            while (mensajesHilo.TryDequeue(out string msg))
            {
                mensajeEstado = msg;
            }

            ProcesarAtajosTeclado();

            if (grabando && !pausado && escritor != null && escritor.LimiteTamanoAlcanzado)
            {
                RotarArchivoSiLleno();
            }

            RefrescarEstadoEnVivo();
        }

        private void LateUpdate()
        {
            ActualizarCamaraSegundoPlano();
        }

        private void ProcesarAtajosTeclado()
        {
            Keyboard kb = Keyboard.current;
            if (kb == null) return;

            if (kb.f5Key.wasPressedThisFrame) SeleccionarCamara(TomaCamara.PrimeraPersona);
            else if (kb.f6Key.wasPressedThisFrame) SeleccionarCamara(TomaCamara.JuegoZoom);
            else if (kb.f7Key.wasPressedThisFrame) SeleccionarCamara(TomaCamara.TerceraPersona);
            else if (kb.f8Key.wasPressedThisFrame) SeleccionarCamara(TomaCamara.Normal);

            if (kb.f9Key.wasPressedThisFrame) AlternarGrabacion();
            if (kb.f10Key.wasPressedThisFrame && grabando) AlternarPausa();
            if (kb.f11Key.wasPressedThisFrame) AlternarPiP();
            if (enPartida && kb.tabKey.wasPressedThisFrame && camaras != null)
            {
                camaras.SiguienteObjetivo();
                RefrescarUI();
            }
        }

        private void AlIniciarPartida()
        {
            enPartida = true;
            if (camaras != null) camaras.HabilitadoEnPartida = true;
            if (autoGrabar && !grabando)
            {
                IniciarGrabacion();
            }
            RefrescarUI();
        }

        private void AlTerminarPartida()
        {
            enPartida = false;
            if (camaras != null) camaras.HabilitadoEnPartida = false;
            if (grabando)
            {
                DetenerGrabacion();
            }
            camaras?.EstablecerRenderActivo(false);
            RefrescarUI();
        }

        private void AlSalirDeEscena()
        {
            enPartida = false;
            if (camaras != null) camaras.HabilitadoEnPartida = false;
            if (grabando)
            {
                DetenerGrabacion("Grabacion guardada al volver al menu");
            }
            camaras?.EstablecerRenderActivo(false);
        }

        public void SeleccionarCamara(TomaCamara toma)
        {
            if (camaras != null) camaras.EstablecerToma(toma);
            GuardarPreferencias();
            RefrescarUI();
        }

        public void AlternarGrabacion()
        {
            if (grabando) DetenerGrabacion();
            else IniciarGrabacion();
        }

        public void AlternarPausa()
        {
            if (!grabando || escritor == null) return;
            pausado = !pausado;
            if (capturaAudio != null) capturaAudio.Pausado = pausado;
            escritor.EstablecerPausa(pausado);
            mensajeEstado = pausado ? "Grabacion en pausa" : "Grabando en 2º plano...";
            RefrescarUI();
        }

        public void AlternarPiP()
        {
            mostrarPiP = !mostrarPiP;
            GuardarPreferencias();
            RefrescarUI();
        }

        public string ObtenerCarpetaGrabaciones()
        {
            string baseDir = Application.isEditor
                ? Application.dataPath
                : (Directory.GetParent(Application.dataPath)?.FullName ?? Application.persistentDataPath);
            string carpeta = Path.Combine(baseDir, "Grabaciones");
            Directory.CreateDirectory(carpeta);
            return carpeta;
        }

        private void AbrirCarpetaGrabaciones()
        {
            string ruta = ObtenerCarpetaGrabaciones();
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = ruta,
                    UseShellExecute = true
                });
            }
            catch
            {
                Application.OpenURL("file:///" + ruta.Replace('\\', '/'));
            }
        }

        private void IniciarGrabacion()
        {
            if (grabando) return;
            AsegurarReferenciasEscena();
            if (camaraPrincipal == null || capturaAudio == null || camaras == null)
            {
                mensajeEstado = "Error: Camara o Audio no disponible";
                RefrescarUI();
                return;
            }

            PresetCalidad p = Presets[indiceCalidad];
            anchoCaptura = p.Ancho;
            altoCaptura = p.Alto;
            fpsCaptura = p.Fps;
            calidadJpgCaptura = p.CalidadJpg;

            AsegurarRenderTexture(anchoCaptura, altoCaptura);
            camaras.FijarTargetTexture(rtCaptura);

            indiceParte = 1;
            string camTag = CamaraSetGrabacion.CodigoCortoToma(camaras.TomaActual);
            string charTag = LimpiarNombreArchivo(camaras.NombrePersonajeArchivo());
            prefijoSesionActual = $"Trailer_{camTag}_{charTag}_{DateTime.Now:yyyyMMdd_HHmmss}";

            string rutaAvi = Path.Combine(ObtenerCarpetaGrabaciones(), prefijoSesionActual + ".avi");
            escritor = new EscritorAVI(rutaAvi, anchoCaptura, altoCaptura, fpsCaptura, capturaAudio.FrecuenciaMuestreo);
            capturaAudio.IniciarCaptura(pcm => escritor?.EncolarAudio(pcm));

            grabando = true;
            pausado = false;
            tiempoProximoFrame = Time.realtimeSinceStartupAsDouble;
            mensajeEstado = "Grabando " + Path.GetFileName(rutaAvi);
            RefrescarUI();
        }

        private void RotarArchivoSiLleno()
        {
            EscritorAVI anterior = escritor;
            capturaAudio?.DetenerCaptura();
            anterior?.Finalizar();

            indiceParte++;
            string rutaSiguiente = Path.Combine(ObtenerCarpetaGrabaciones(), $"{prefijoSesionActual}_parte{indiceParte}.avi");
            escritor = new EscritorAVI(rutaSiguiente, anchoCaptura, altoCaptura, fpsCaptura, capturaAudio.FrecuenciaMuestreo);
            capturaAudio?.IniciarCaptura(pcm => escritor?.EncolarAudio(pcm));

            if (anterior != null) ProcesarPostGrabacion(anterior.RutaArchivo);
            mensajeEstado = $"Grabando parte {indiceParte}: {Path.GetFileName(rutaSiguiente)}";
        }

        public void DetenerGrabacion(string estadoOpcional = null)
        {
            if (!grabando) return;
            grabando = false;
            pausado = false;
            capturaPendienteEsteCuadro = false;

            capturaAudio?.DetenerCaptura();

            EscritorAVI cerrar = escritor;
            escritor = null;
            string rutaGuardada = cerrar != null ? cerrar.RutaArchivo : null;
            cerrar?.Finalizar();

            if (!string.IsNullOrEmpty(rutaGuardada) && File.Exists(rutaGuardada))
            {
                mensajeEstado = estadoOpcional ?? ("Guardado: " + Path.GetFileName(rutaGuardada));
                ProcesarPostGrabacion(rutaGuardada);
            }
            else if (!string.IsNullOrEmpty(estadoOpcional))
            {
                mensajeEstado = estadoOpcional;
            }

            RefrescarUI();
        }

        private void ProcesarPostGrabacion(string rutaAvi)
        {
            if (!autoconvertirMp4 || !ConversorFFmpeg.EstaDisponible) return;
            mensajesHilo.Enqueue("Convirtiendo a .mp4...");
            ConversorFFmpeg.ConvertirEnSegundoPlano(rutaAvi, conservarAvi: true, (ok, salida) =>
            {
                if (ok) mensajesHilo.Enqueue("MP4 listo: " + Path.GetFileName(salida));
                else mensajesHilo.Enqueue("AVI guardado (MP4 fallo: " + salida + ")");
            });
        }

        private void AsegurarRenderTexture(int ancho, int alto)
        {
            if (rtCaptura != null && rtCaptura.width == ancho && rtCaptura.height == alto) return;
            if (rtCaptura != null)
            {
                camaras?.FijarTargetTexture(null);
                rtCaptura.Release();
                Destroy(rtCaptura);
            }

            var desc = new RenderTextureDescriptor(ancho, alto, GraphicsFormat.R8G8B8A8_SRGB, 24)
            {
                msaaSamples = 1,
                sRGB = true
            };
            rtCaptura = new RenderTexture(desc);
            rtCaptura.name = "SetGrabacion_RT";
            rtCaptura.Create();

            if (imagenPiP != null) imagenPiP.texture = rtCaptura;
        }

        private void ActualizarCamaraSegundoPlano()
        {
            AsegurarReferenciasEscena();
            if (camaras == null) return;

            bool pipVisibleAhora = enPartida && mostrarPiP;
            bool necesitaCamara = (grabando && !pausado) || pipVisibleAhora;
            if (!necesitaCamara)
            {
                capturaPendienteEsteCuadro = false;
                camaras.EstablecerRenderActivo(false);
                return;
            }

            PresetCalidad p = Presets[indiceCalidad];
            int w = grabando ? anchoCaptura : p.Ancho;
            int h = grabando ? altoCaptura : p.Alto;
            AsegurarRenderTexture(w, h);
            camaras.FijarTargetTexture(rtCaptura);

            double ahora = Time.realtimeSinceStartupAsDouble;
            if (grabando && !pausado)
            {
                capturaPendienteEsteCuadro = lecturasEnVuelo < MAX_LECTURAS_EN_VUELO
                    && (ahora + 0.002 >= tiempoProximoFrame);
                // Durante la grabacion solo se renderiza en los cuadros capturados (30/60 fps), evitando doble render PiP por frame
                camaras.EstablecerRenderActivo(capturaPendienteEsteCuadro);
            }
            else
            {
                capturaPendienteEsteCuadro = false;
                bool tickPreview = pipVisibleAhora && (ahora + 0.002 >= tiempoProximoPreview);
                if (tickPreview)
                {
                    double pasoPreview = 1.0 / Mathf.Max(1, p.Fps);
                    tiempoProximoPreview = Math.Max(ahora, tiempoProximoPreview + pasoPreview);
                }
                camaras.EstablecerRenderActivo(tickPreview);
            }
        }

        private void OcultarCuerpoObjetivoTemporalmente(Personaje objetivo)
        {
            RestaurarCuerpoObjetivoTemporalmente();
            if (objetivo == null) return;

            foreach (Renderer r in objetivo.GetComponentsInChildren<Renderer>(false))
            {
                if (r == null || !r.enabled) continue;
                if (r is LineRenderer || r is ParticleSystemRenderer) continue;
                if (r.GetComponentInParent<Arma>() != null) continue; // conserva el arma en mano visible en 1P

                r.enabled = false;
                renderersCuerpoOcultosTemp.Add(r);
            }
        }

        private void RestaurarCuerpoObjetivoTemporalmente()
        {
            for (int i = 0; i < renderersCuerpoOcultosTemp.Count; i++)
            {
                Renderer r = renderersCuerpoOcultosTemp[i];
                if (r != null) r.enabled = true;
            }
            renderersCuerpoOcultosTemp.Clear();
        }

        private void AlIniciarRenderCamara(ScriptableRenderContext ctx, Camera cam)
        {
            if (camaras == null || cam != camaras.CamaraCaptura) return;
            if (ocultarNombres3D)
            {
                Red.EstablecerOcultamientoTemporalPorRender(true);
            }
            if (camaras.TomaActual == TomaCamara.PrimeraPersona && camaras.ObjetivoActual != null)
            {
                OcultarCuerpoObjetivoTemporalmente(camaras.ObjetivoActual);
            }
        }

        private void AlTerminarRenderCamara(ScriptableRenderContext ctx, Camera cam)
        {
            if (camaras == null || cam != camaras.CamaraCaptura) return;

            // Restaurar siempre antes de que Main Camera dibuje el cuadro del jugador
            RestaurarCuerpoObjetivoTemporalmente();
            Red.EstablecerOcultamientoTemporalPorRender(false);

            if (!capturaPendienteEsteCuadro || !grabando || pausado || escritor == null || rtCaptura == null) return;
            capturaPendienteEsteCuadro = false;

            double ahora = Time.realtimeSinceStartupAsDouble;
            double paso = 1.0 / Mathf.Max(1, fpsCaptura);
            tiempoProximoFrame = Math.Max(ahora, tiempoProximoFrame + paso);
            lecturasEnVuelo++;

            bool invertirY = voltearVertical;
            int w = anchoCaptura;
            int h = altoCaptura;
            int cal = calidadJpgCaptura;

            AsyncGPUReadback.Request(rtCaptura, 0, TextureFormat.RGBA32, req =>
            {
                lecturasEnVuelo = Mathf.Max(0, lecturasEnVuelo - 1);
                if (req.hasError || !grabando || pausado || escritor == null) return;

                NativeArray<byte> crudo = req.GetData<byte>();
                if (!crudo.IsCreated || crudo.Length < w * h * 4) return;

                if (invertirY)
                {
                    byte[] copia = crudo.ToArray();
                    InvertirFilasVerticalmente(copia, w, h);
                    byte[] jpgFlip = ImageConversion.EncodeArrayToJPG(copia, GraphicsFormat.R8G8B8A8_SRGB, (uint)w, (uint)h, 0, cal);
                    escritor.EncolarVideo(jpgFlip);
                }
                else
                {
                    using (NativeArray<byte> jpg = ImageConversion.EncodeNativeArrayToJPG(crudo, GraphicsFormat.R8G8B8A8_SRGB, (uint)w, (uint)h, 0, cal))
                    {
                        if (jpg.IsCreated && jpg.Length > 0)
                        {
                            escritor.EncolarVideo(jpg.ToArray());
                        }
                    }
                }
            });
        }

        private static void InvertirFilasVerticalmente(byte[] rgba, int ancho, int alto)
        {
            int stride = ancho * 4;
            byte[] temp = new byte[stride];
            for (int y = 0; y < alto / 2; y++)
            {
                int top = y * stride;
                int bot = (alto - 1 - y) * stride;
                Buffer.BlockCopy(rgba, top, temp, 0, stride);
                Buffer.BlockCopy(rgba, bot, rgba, top, stride);
                Buffer.BlockCopy(temp, 0, rgba, bot, stride);
            }
        }

        private static string LimpiarNombreArchivo(string texto)
        {
            if (string.IsNullOrWhiteSpace(texto)) return "Trailer";
            foreach (char c in Path.GetInvalidFileNameChars()) texto = texto.Replace(c, '_');
            return texto.Trim().Replace(' ', '_');
        }

        // =====================================================================
        // Interfaz del Estudio (Canvas ScreenSpaceOverlay, nunca sale en video)
        // =====================================================================

        private void ConstruirUIEstudio()
        {
            TMP_Text muestra = FindAnyObjectByType<TMP_Text>(FindObjectsInactive.Include);
            fuenteUI = muestra != null ? muestra.font : null;

            GameObject goCanvas = new GameObject("SetGrabacion_StudioCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            goCanvas.transform.SetParent(transform, false);
            canvasStudio = goCanvas.GetComponent<Canvas>();
            canvasStudio.renderMode = RenderMode.ScreenSpaceOverlay;
            canvasStudio.sortingOrder = 500;

            CanvasScaler scaler = goCanvas.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            ConstruirPanelMenu(goCanvas.transform);
            ConstruirBarraEnVivo(goCanvas.transform);
            ConstruirMiniPiP(goCanvas.transform);
        }

        private void ConstruirPanelMenu(Transform raiz)
        {
            panelMenuStudio = CrearTarjeta(raiz, "PanelEstudioMenu", new Vector2(1f, 1f), new Vector2(-18f, -18f), new Vector2(360f, 700f));

            TextMeshProUGUI titulo = CrearTexto(panelMenuStudio.transform, "Titulo", "SET DE GRABACION", 20, true, new Color(1f, 0.62f, 0.11f), TextAlignmentOptions.Left);
            Anclar(titulo.rectTransform, new Vector2(0f, 1f), new Vector2(16f, -14f), new Vector2(260f, 28f));

            Button btnColapsar = CrearBoton(panelMenuStudio.transform, "BtnColapsar", "-", out txtBtnColapsar, new Vector2(36f, 28f), new Color(0.16f, 0.19f, 0.26f), Color.white, 18);
            Anclar((RectTransform)btnColapsar.transform, new Vector2(1f, 1f), new Vector2(-12f, -12f), new Vector2(36f, 28f));
            btnColapsar.onClick.AddListener(() =>
            {
                menuColapsado = !menuColapsado;
                panelMenuContenido.SetActive(!menuColapsado);
                ((RectTransform)panelMenuStudio.transform).sizeDelta = new Vector2(360f, menuColapsado ? 54f : 700f);
                txtBtnColapsar.text = menuColapsado ? "+" : "-";
            });

            panelMenuContenido = new GameObject("Contenido", typeof(RectTransform));
            panelMenuContenido.transform.SetParent(panelMenuStudio.transform, false);
            RectTransform rtCont = (RectTransform)panelMenuContenido.transform;
            rtCont.anchorMin = Vector2.zero;
            rtCont.anchorMax = Vector2.one;
            rtCont.offsetMin = Vector2.zero;
            rtCont.offsetMax = new Vector2(0f, -48f);

            float y = -2f;
            TextMeshProUGUI notaVista = CrearTexto(panelMenuContenido.transform, "NotaVista", "Juegas en vista normal; se graba en 2º plano:", 13, false, new Color(0.60f, 0.85f, 0.72f), TextAlignmentOptions.Left);
            Anclar(notaVista.rectTransform, new Vector2(0f, 1f), new Vector2(16f, y), new Vector2(328f, 22f));
            y -= 26f;

            TextMeshProUGUI subCam = CrearTexto(panelMenuContenido.transform, "SubCam", "Perspectiva de grabacion (F5-F8):", 14, false, new Color(0.68f, 0.72f, 0.82f), TextAlignmentOptions.Left);
            Anclar(subCam.rectTransform, new Vector2(0f, 1f), new Vector2(16f, y), new Vector2(328f, 20f));
            y -= 26f;

            botonesCamara = new Button[4];
            TomaCamara[] tomas = { TomaCamara.PrimeraPersona, TomaCamara.JuegoZoom, TomaCamara.TerceraPersona, TomaCamara.Normal };
            string[] etiquetas = { "[F5] 1ª Persona", "[F6] Con Zoom", "[F7] 3ª Persona", "[F8] Normal" };
            for (int i = 0; i < 4; i++)
            {
                TomaCamara tCaptura = tomas[i];
                int col = i % 2;
                int fila = i / 2;
                Button b = CrearBoton(panelMenuContenido.transform, "Cam_" + i, etiquetas[i], out _, new Vector2(160f, 36f), new Color(0.16f, 0.19f, 0.26f), Color.white, 15);
                Anclar((RectTransform)b.transform, new Vector2(0f, 1f), new Vector2(16f + col * 168f, y - fila * 42f), new Vector2(160f, 36f));
                b.onClick.AddListener(() => SeleccionarCamara(tCaptura));
                botonesCamara[i] = b;
            }
            y -= 92f;

            Button btnCalidad = CrearBoton(panelMenuContenido.transform, "BtnCalidad", "", out txtCalidad, new Vector2(328f, 34f), new Color(0.15f, 0.18f, 0.25f), Color.white, 15);
            Anclar((RectTransform)btnCalidad.transform, new Vector2(0f, 1f), new Vector2(16f, y), new Vector2(328f, 34f));
            btnCalidad.onClick.AddListener(() =>
            {
                if (grabando) return;
                indiceCalidad = (indiceCalidad + 1) % Presets.Length;
                GuardarPreferencias();
                RefrescarUI();
            });
            y -= 42f;

            Button bAuto = CrearBoton(panelMenuContenido.transform, "BtnAuto", "", out txtAutoGrabar, new Vector2(328f, 32f), new Color(0.15f, 0.18f, 0.25f), Color.white, 14);
            Anclar((RectTransform)bAuto.transform, new Vector2(0f, 1f), new Vector2(16f, y), new Vector2(328f, 32f));
            bAuto.onClick.AddListener(() => { autoGrabar = !autoGrabar; GuardarPreferencias(); RefrescarUI(); });
            y -= 38f;

            Button bPip = CrearBoton(panelMenuContenido.transform, "BtnPiP", "", out txtPiPMenu, new Vector2(328f, 32f), new Color(0.15f, 0.18f, 0.25f), Color.white, 14);
            Anclar((RectTransform)bPip.transform, new Vector2(0f, 1f), new Vector2(16f, y), new Vector2(328f, 32f));
            bPip.onClick.AddListener(AlternarPiP);
            y -= 38f;

            Button bNombres = CrearBoton(panelMenuContenido.transform, "BtnNombres", "", out txtOcultarNombres, new Vector2(328f, 32f), new Color(0.15f, 0.18f, 0.25f), Color.white, 14);
            Anclar((RectTransform)bNombres.transform, new Vector2(0f, 1f), new Vector2(16f, y), new Vector2(328f, 32f));
            bNombres.onClick.AddListener(() =>
            {
                ocultarNombres3D = !ocultarNombres3D;
                GuardarPreferencias();
                RefrescarUI();
            });
            y -= 38f;

            Button bMp4 = CrearBoton(panelMenuContenido.transform, "BtnMp4", "", out txtAutoMp4, new Vector2(328f, 32f), new Color(0.15f, 0.18f, 0.25f), Color.white, 14);
            Anclar((RectTransform)bMp4.transform, new Vector2(0f, 1f), new Vector2(16f, y), new Vector2(328f, 32f));
            bMp4.onClick.AddListener(() => { autoconvertirMp4 = !autoconvertirMp4; GuardarPreferencias(); RefrescarUI(); });
            y -= 38f;

            Button bFlip = CrearBoton(panelMenuContenido.transform, "BtnFlip", "", out txtVoltearY, new Vector2(160f, 30f), new Color(0.14f, 0.16f, 0.22f), new Color(0.8f, 0.85f, 0.92f), 13);
            Anclar((RectTransform)bFlip.transform, new Vector2(0f, 1f), new Vector2(16f, y), new Vector2(160f, 30f));
            bFlip.onClick.AddListener(() => { voltearVertical = !voltearVertical; GuardarPreferencias(); RefrescarUI(); });

            Button bDist1P = CrearBoton(panelMenuContenido.transform, "BtnDist1P", "", out txtOffset1P, new Vector2(160f, 30f), new Color(0.14f, 0.16f, 0.22f), new Color(0.8f, 0.85f, 0.92f), 13);
            Anclar((RectTransform)bDist1P.transform, new Vector2(0f, 1f), new Vector2(184f, y), new Vector2(160f, 30f));
            bDist1P.onClick.AddListener(() =>
            {
                if (camaras != null)
                {
                    float nuevo = camaras.OffsetFrontalPrimeraPersona + 0.03f;
                    camaras.OffsetFrontalPrimeraPersona = nuevo > 0.23f ? 0.04f : nuevo;
                }
                RefrescarUI();
            });
            y -= 42f;

            Button bRec = CrearBoton(panelMenuContenido.transform, "BtnRecMenu", "Grabar Ahora (F9)", out txtBtnRecMenu, new Vector2(198f, 38f), new Color(0.82f, 0.18f, 0.18f), Color.white, 15);
            Anclar((RectTransform)bRec.transform, new Vector2(0f, 1f), new Vector2(16f, y), new Vector2(198f, 38f));
            bRec.onClick.AddListener(AlternarGrabacion);

            Button bPausa = CrearBoton(panelMenuContenido.transform, "BtnPausaMenu", "Pausa (F10)", out txtBtnPausaMenu, new Vector2(122f, 38f), new Color(0.24f, 0.28f, 0.38f), Color.white, 14);
            Anclar((RectTransform)bPausa.transform, new Vector2(0f, 1f), new Vector2(222f, y), new Vector2(122f, 38f));
            bPausa.onClick.AddListener(AlternarPausa);
            y -= 46f;

            txtEstadoMenu = CrearTexto(panelMenuContenido.transform, "EstadoMenu", "", 13, false, new Color(0.55f, 0.95f, 0.65f), TextAlignmentOptions.Left);
            Anclar(txtEstadoMenu.rectTransform, new Vector2(0f, 1f), new Vector2(16f, y), new Vector2(328f, 36f));
            y -= 40f;

            txtRutaCarpeta = CrearTexto(panelMenuContenido.transform, "RutaCarpeta", "", 12, false, new Color(0.60f, 0.66f, 0.76f), TextAlignmentOptions.Left);
            Anclar(txtRutaCarpeta.rectTransform, new Vector2(0f, 1f), new Vector2(16f, y), new Vector2(328f, 38f));
            y -= 42f;

            Button bCarpeta = CrearBoton(panelMenuContenido.transform, "BtnCarpeta", "Abrir Carpeta Grabaciones", out _, new Vector2(328f, 34f), new Color(0.18f, 0.24f, 0.34f), Color.white, 14);
            Anclar((RectTransform)bCarpeta.transform, new Vector2(0f, 1f), new Vector2(16f, y), new Vector2(328f, 34f));
            bCarpeta.onClick.AddListener(AbrirCarpetaGrabaciones);
            y -= 40f;

            if (Application.CanStreamedLevelBeLoaded("Juego"))
            {
                Button bVolverJuego = CrearBoton(panelMenuContenido.transform, "BtnVolverJuego", "Volver a Escena Juego", out _, new Vector2(328f, 32f), new Color(0.16f, 0.18f, 0.24f), new Color(0.82f, 0.86f, 0.92f), 14);
                Anclar((RectTransform)bVolverJuego.transform, new Vector2(0f, 1f), new Vector2(16f, y), new Vector2(328f, 32f));
                bVolverJuego.onClick.AddListener(() =>
                {
                    DetenerGrabacion("Saliendo del Set");
                    var nm = Unity.Netcode.NetworkManager.Singleton;
                    if (nm != null && nm.IsListening) nm.Shutdown();
                    SceneManager.LoadScene("Juego");
                });
            }
        }

        private void ConstruirBarraEnVivo(Transform raiz)
        {
            barraEnVivo = CrearTarjeta(raiz, "BarraEstudioEnVivo", new Vector2(1f, 1f), new Vector2(-16f, -14f), new Vector2(564f, 84f));

            GameObject goDot = new GameObject("DotRec", typeof(RectTransform), typeof(Image));
            goDot.transform.SetParent(barraEnVivo.transform, false);
            Anclar((RectTransform)goDot.transform, new Vector2(0f, 1f), new Vector2(14f, -15f), new Vector2(14f, 14f));
            puntoRecVivo = goDot.GetComponent<Image>();
            puntoRecVivo.sprite = SpriteRedondeado();

            txtEstadoEnVivo = CrearTexto(barraEnVivo.transform, "EstadoVivo", "LISTO 00:00", 16, true, Color.white, TextAlignmentOptions.Left);
            Anclar(txtEstadoEnVivo.rectTransform, new Vector2(0f, 1f), new Vector2(36f, -10f), new Vector2(215f, 24f));

            txtCamaraEnVivo = CrearTexto(barraEnVivo.transform, "CamVivo", "Primera Persona · Cheto", 14, false, new Color(1f, 0.72f, 0.30f), TextAlignmentOptions.Right);
            Anclar(txtCamaraEnVivo.rectTransform, new Vector2(1f, 1f), new Vector2(-14f, -11f), new Vector2(290f, 22f));

            string[] etiquetas = { "F5:1P", "F6:Zoom", "F7:3P", "F8:Norm" };
            TomaCamara[] tomas = { TomaCamara.PrimeraPersona, TomaCamara.JuegoZoom, TomaCamara.TerceraPersona, TomaCamara.Normal };
            for (int i = 0; i < 4; i++)
            {
                TomaCamara tCaptura = tomas[i];
                Button b = CrearBoton(barraEnVivo.transform, "LiveCam_" + i, etiquetas[i], out _, new Vector2(72f, 30f), new Color(0.18f, 0.22f, 0.30f), Color.white, 13);
                Anclar((RectTransform)b.transform, new Vector2(0f, 0f), new Vector2(14f + i * 76f, 10f), new Vector2(72f, 30f));
                b.onClick.AddListener(() => SeleccionarCamara(tCaptura));
            }

            Button bRecV = CrearBoton(barraEnVivo.transform, "LiveRec", "F9 Rec", out txtBtnRecVivo, new Vector2(78f, 30f), new Color(0.80f, 0.18f, 0.18f), Color.white, 13);
            Anclar((RectTransform)bRecV.transform, new Vector2(0f, 0f), new Vector2(322f, 10f), new Vector2(78f, 30f));
            bRecV.onClick.AddListener(AlternarGrabacion);

            Button bPausaV = CrearBoton(barraEnVivo.transform, "LivePausa", "F10 Pausa", out txtBtnPausaVivo, new Vector2(78f, 30f), new Color(0.24f, 0.28f, 0.38f), Color.white, 13);
            Anclar((RectTransform)bPausaV.transform, new Vector2(0f, 0f), new Vector2(404f, 10f), new Vector2(78f, 30f));
            bPausaV.onClick.AddListener(AlternarPausa);

            Button bPipV = CrearBoton(barraEnVivo.transform, "LivePiP", "F11 PiP", out txtBtnPiPVivo, new Vector2(64f, 30f), new Color(0.20f, 0.24f, 0.33f), Color.white, 13);
            Anclar((RectTransform)bPipV.transform, new Vector2(0f, 0f), new Vector2(486f, 10f), new Vector2(64f, 30f));
            bPipV.onClick.AddListener(AlternarPiP);
        }

        private void ConstruirMiniPiP(Transform raiz)
        {
            panelPiP = CrearTarjeta(raiz, "PanelMiniPiP", new Vector2(1f, 1f), new Vector2(-16f, -106f), new Vector2(288f, 186f));

            TextMeshProUGUI etiqueta = CrearTexto(panelPiP.transform, "LblPiP", "CAMARA DE GRABACION (F11 ocultar)", 12, true, new Color(1f, 0.72f, 0.30f), TextAlignmentOptions.Center);
            Anclar(etiqueta.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -4f), new Vector2(276f, 20f));

            GameObject goRaw = new GameObject("VistaPiP", typeof(RectTransform), typeof(RawImage));
            goRaw.transform.SetParent(panelPiP.transform, false);
            Anclar((RectTransform)goRaw.transform, new Vector2(0.5f, 0f), new Vector2(0f, 6f), new Vector2(276f, 155f));
            imagenPiP = goRaw.GetComponent<RawImage>();
            imagenPiP.raycastTarget = false;
            panelPiP.SetActive(false);
        }

        private void RefrescarUI()
        {
            if (panelMenuStudio != null) panelMenuStudio.SetActive(!enPartida);
            if (barraEnVivo != null) barraEnVivo.SetActive(enPartida);
            if (panelPiP != null) panelPiP.SetActive(enPartida && mostrarPiP);

            int tomaIdx = camaras != null ? (int)camaras.TomaActual : 0;
            if (botonesCamara != null)
            {
                for (int i = 0; i < botonesCamara.Length; i++)
                {
                    if (botonesCamara[i] == null) continue;
                    Image img = botonesCamara[i].GetComponent<Image>();
                    bool activa = i == tomaIdx;
                    if (img != null) img.color = activa ? new Color(1f, 0.62f, 0.11f) : new Color(0.16f, 0.19f, 0.26f);
                    TMP_Text t = botonesCamara[i].GetComponentInChildren<TMP_Text>();
                    if (t != null) t.color = activa ? new Color(0.08f, 0.06f, 0.02f) : Color.white;
                }
            }

            if (txtCalidad != null) txtCalidad.text = "Calidad: " + Presets[indiceCalidad].Etiqueta + (grabando ? " (en uso)" : "");
            if (txtAutoGrabar != null) txtAutoGrabar.text = "Auto-grabar al iniciar partida: " + (autoGrabar ? "SI" : "NO");
            if (txtPiPMenu != null) txtPiPMenu.text = "Mini vista previa PiP (F11): " + (mostrarPiP ? "SI" : "NO");
            if (txtOcultarNombres != null) txtOcultarNombres.text = "Ocultar nombres 3D en video: " + (ocultarNombres3D ? "SI (Limpio)" : "NO");
            if (txtAutoMp4 != null)
            {
                string estadoFf = ConversorFFmpeg.EstaDisponible ? "FFmpeg OK" : "Sin FFmpeg (solo .avi)";
                txtAutoMp4.text = $"Auto .mp4: {(autoconvertirMp4 ? "SI" : "NO")} · {estadoFf}";
            }
            if (txtVoltearY != null) txtVoltearY.text = "Flip Y: " + (voltearVertical ? "SI" : "NO");
            if (txtOffset1P != null && camaras != null) txtOffset1P.text = $"Dist 1P: {camaras.OffsetFrontalPrimeraPersona:F2}m";
            if (txtRutaCarpeta != null) txtRutaCarpeta.text = "Salida: " + ObtenerCarpetaGrabaciones();
            if (txtBtnRecMenu != null) txtBtnRecMenu.text = grabando ? "Detener (F9)" : "Grabar Ahora (F9)";
            if (txtBtnPausaMenu != null) txtBtnPausaMenu.text = pausado ? "Reanudar (F10)" : "Pausar (F10)";
            if (txtBtnRecVivo != null) txtBtnRecVivo.text = grabando ? "F9 Stop" : "F9 Rec";
            if (txtBtnPausaVivo != null) txtBtnPausaVivo.text = pausado ? "F10 Cont." : "F10 Pausa";
            if (txtBtnPiPVivo != null) txtBtnPiPVivo.text = mostrarPiP ? "PiP:SI" : "PiP:NO";

            RefrescarEstadoEnVivo();
        }

        private void RefrescarEstadoEnVivo()
        {
            if (txtEstadoMenu != null) txtEstadoMenu.text = mensajeEstado;

            double segs = escritor != null ? escritor.DuracionSegundos : 0.0;
            int min = (int)(segs / 60.0);
            int sec = (int)(segs % 60.0);
            string crono = $"{min:00}:{sec:00}";

            if (txtEstadoEnVivo != null)
            {
                if (grabando)
                    txtEstadoEnVivo.text = pausado ? $"PAUSA  {crono}" : $"REC    {crono}";
                else
                    txtEstadoEnVivo.text = "LISTO · " + mensajeEstado;
            }

            if (puntoRecVivo != null)
            {
                if (!grabando) puntoRecVivo.color = new Color(0.50f, 0.55f, 0.62f);
                else if (pausado) puntoRecVivo.color = new Color(1f, 0.72f, 0.15f);
                else
                {
                    float parpadeo = 0.55f + 0.45f * Mathf.Sin(Time.unscaledTime * 6f);
                    puntoRecVivo.color = new Color(1f, 0.18f, 0.18f, parpadeo);
                }
            }

            if (txtCamaraEnVivo != null && camaras != null)
            {
                txtCamaraEnVivo.text = $"{CamaraSetGrabacion.NombreToma(camaras.TomaActual)} · {camaras.NombreObjetivoActual()}";
            }
        }

        private GameObject CrearTarjeta(Transform padre, string nombre, Vector2 ancla, Vector2 pos, Vector2 tam)
        {
            GameObject go = new GameObject(nombre, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(padre, false);
            Anclar((RectTransform)go.transform, ancla, pos, tam);
            Image img = go.GetComponent<Image>();
            img.sprite = SpriteRedondeado();
            img.type = Image.Type.Sliced;
            img.color = new Color(0.06f, 0.07f, 0.11f, 0.90f);
            return go;
        }

        private TextMeshProUGUI CrearTexto(Transform padre, string nombre, string contenido, float tamFuente, bool negrita, Color color, TextAlignmentOptions alineacion)
        {
            GameObject go = new GameObject(nombre, typeof(RectTransform), typeof(TextMeshProUGUI));
            go.transform.SetParent(padre, false);
            TextMeshProUGUI tmp = go.GetComponent<TextMeshProUGUI>();
            if (fuenteUI != null) tmp.font = fuenteUI;
            tmp.text = contenido;
            tmp.fontSize = tamFuente;
            tmp.fontStyle = negrita ? FontStyles.Bold : FontStyles.Normal;
            tmp.color = color;
            tmp.alignment = alineacion;
            tmp.raycastTarget = false;
            return tmp;
        }

        private Button CrearBoton(Transform padre, string nombre, string etiqueta, out TextMeshProUGUI textoSalida, Vector2 tam, Color fondo, Color colorTexto, float tamFuente)
        {
            GameObject go = new GameObject(nombre, typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(padre, false);
            ((RectTransform)go.transform).sizeDelta = tam;
            Image img = go.GetComponent<Image>();
            img.sprite = SpriteRedondeado();
            img.type = Image.Type.Sliced;
            img.color = fondo;

            Button btn = go.GetComponent<Button>();
            btn.targetGraphic = img;

            textoSalida = CrearTexto(go.transform, "Texto", etiqueta, tamFuente, true, colorTexto, TextAlignmentOptions.Center);
            RectTransform rt = textoSalida.rectTransform;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(6f, 2f);
            rt.offsetMax = new Vector2(-6f, -2f);
            return btn;
        }

        private static void Anclar(RectTransform rt, Vector2 ancla, Vector2 pos, Vector2 tam)
        {
            rt.anchorMin = ancla;
            rt.anchorMax = ancla;
            rt.pivot = ancla;
            rt.anchoredPosition = pos;
            rt.sizeDelta = tam;
        }

        private Sprite SpriteRedondeado()
        {
            if (spriteRedondeado != null) return spriteRedondeado;
            const int s = 32;
            const float r = 8f;
            Texture2D tex = new Texture2D(s, s, TextureFormat.RGBA32, false);
            for (int y = 0; y < s; y++)
            {
                for (int x = 0; x < s; x++)
                {
                    float dx = Mathf.Max(r - (x + 0.5f), (x + 0.5f) - (s - r), 0f);
                    float dy = Mathf.Max(r - (y + 0.5f), (y + 0.5f) - (s - r), 0f);
                    float a = Mathf.Clamp01(r - Mathf.Sqrt(dx * dx + dy * dy) + 0.5f);
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
                }
            }
            tex.Apply();
            spriteRedondeado = Sprite.Create(tex, new Rect(0, 0, s, s), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, new Vector4(r, r, r, r));
            return spriteRedondeado;
        }
    }
}
