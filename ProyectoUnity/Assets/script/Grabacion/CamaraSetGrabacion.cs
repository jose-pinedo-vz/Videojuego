using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace Grabacion
{
    public enum TomaCamara
    {
        PrimeraPersona = 0,
        JuegoZoom = 1,
        TerceraPersona = 2,
        Normal = 3
    }

    /// <summary>
    /// Administra la camara secundaria dedicada de grabacion en segundo plano.
    /// Nunca modifica Main Camera ni CamaraSeguir: el jugador siempre juega con la vista normal.
    /// </summary>
    [DefaultExecutionOrder(500)]
    public class CamaraSetGrabacion : MonoBehaviour
    {
        public TomaCamara TomaActual { get; private set; } = TomaCamara.PrimeraPersona;
        public bool HabilitadoEnPartida { get; set; }
        public float OffsetFrontalPrimeraPersona { get; set; } = 0.08f;
        public Camera CamaraCaptura => camaraGrabacion;
        public Personaje ObjetivoActual => objetivoActual;

        private Camera camaraPrincipal;
        private Camera camaraGrabacion;

        private Personaje objetivoActual;
        private Transform huesoCabezaCache;
        private Animator animatorCache;
        private int indiceObjetivoManual = -1;

        private Vector3 posSuavizada;
        private Quaternion rotSuavizada = Quaternion.Euler(0f, 180f, 0f);
        private float fovSuavizado = 60f;
        private float nearSuavizado = 0.3f;
        private bool estadoSuavizadoInicializado;

        private void Awake()
        {
            AsegurarCamaras();
        }

        private void OnDestroy()
        {
            if (camaraGrabacion != null)
            {
                camaraGrabacion.targetTexture = null;
                Destroy(camaraGrabacion.gameObject);
                camaraGrabacion = null;
            }
        }

        public void AsegurarCamaras()
        {
            if (camaraPrincipal == null) camaraPrincipal = Camera.main;

            if (camaraGrabacion == null)
            {
                GameObject goCam = new GameObject("CamaraGrabacion_Offscreen");
                goCam.transform.SetParent(transform, false);
                camaraGrabacion = goCam.AddComponent<Camera>();
                camaraGrabacion.enabled = false;
                SincronizarConfiguracionConPrincipal();
            }
        }

        private void SincronizarConfiguracionConPrincipal()
        {
            if (camaraGrabacion == null) return;
            if (camaraPrincipal == null) camaraPrincipal = Camera.main;

            if (camaraPrincipal != null)
            {
                camaraGrabacion.clearFlags = camaraPrincipal.clearFlags;
                camaraGrabacion.backgroundColor = camaraPrincipal.backgroundColor;
                camaraGrabacion.cullingMask = camaraPrincipal.cullingMask;
                camaraGrabacion.allowHDR = camaraPrincipal.allowHDR;
                camaraGrabacion.allowMSAA = false; // el RenderTexture de captura trabaja sin MSAA
                camaraGrabacion.farClipPlane = camaraPrincipal.farClipPlane;
                camaraGrabacion.depth = camaraPrincipal.depth - 1f;

                var urpMain = camaraPrincipal.GetUniversalAdditionalCameraData();
                var urpGrab = camaraGrabacion.GetUniversalAdditionalCameraData();
                if (urpMain != null && urpGrab != null)
                {
                    urpGrab.renderShadows = urpMain.renderShadows;
                    urpGrab.requiresColorOption = urpMain.requiresColorOption;
                    urpGrab.requiresDepthOption = urpMain.requiresDepthOption;
                    urpGrab.renderPostProcessing = urpMain.renderPostProcessing;
                    urpGrab.antialiasing = urpMain.antialiasing;
                    urpGrab.antialiasingQuality = urpMain.antialiasingQuality;
                    urpGrab.volumeLayerMask = urpMain.volumeLayerMask;
                    urpGrab.dithering = urpMain.dithering;
                    urpGrab.stopNaN = urpMain.stopNaN;
                }
            }
            else
            {
                camaraGrabacion.depth = -2f;
                camaraGrabacion.allowHDR = true;
                camaraGrabacion.allowMSAA = false;
            }
        }

        public void FijarTargetTexture(RenderTexture rt)
        {
            AsegurarCamaras();
            if (camaraGrabacion != null && camaraGrabacion.targetTexture != rt)
            {
                camaraGrabacion.targetTexture = rt;
            }
        }

        public void EstablecerRenderActivo(bool activo)
        {
            AsegurarCamaras();
            if (camaraGrabacion == null) return;
            bool puedeRenderizar = activo && camaraGrabacion.targetTexture != null;
            if (camaraGrabacion.enabled != puedeRenderizar)
            {
                camaraGrabacion.enabled = puedeRenderizar;
            }
        }

        public static string NombreToma(TomaCamara toma)
        {
            switch (toma)
            {
                case TomaCamara.PrimeraPersona: return "Primera Persona";
                case TomaCamara.JuegoZoom:      return "Juego con Zoom";
                case TomaCamara.TerceraPersona: return "Tercera Persona";
                case TomaCamara.Normal:         return "Camara Normal";
                default:                        return toma.ToString();
            }
        }

        public static string CodigoCortoToma(TomaCamara toma)
        {
            switch (toma)
            {
                case TomaCamara.PrimeraPersona: return "1P";
                case TomaCamara.JuegoZoom:      return "Zoom";
                case TomaCamara.TerceraPersona: return "3P";
                case TomaCamara.Normal:         return "Normal";
                default:                        return "Cam";
            }
        }

        public void EstablecerToma(TomaCamara nuevaToma)
        {
            if (TomaActual == nuevaToma) return;
            TomaActual = nuevaToma;
            estadoSuavizadoInicializado = false;
        }

        public void SiguienteObjetivo()
        {
            List<Personaje> candidatos = ObtenerCandidatosVisibles();
            if (candidatos.Count == 0) return;

            indiceObjetivoManual = (indiceObjetivoManual + 1) % candidatos.Count;
            FijarObjetivo(candidatos[indiceObjetivoManual]);
            estadoSuavizadoInicializado = false;
        }

        public string NombreObjetivoActual()
        {
            if (objetivoActual == null) return "Auto";
            Red red = objetivoActual.GetComponent<Red>();
            if (red != null) return red.ObtenerEtiquetaJugador();
            return CatalogoPersonajes.ObtenerSubnombre(objetivoActual.IndicePersonaje);
        }

        public string NombrePersonajeArchivo()
        {
            if (objetivoActual == null) return "Personaje";
            string sub = CatalogoPersonajes.ObtenerSubnombre(objetivoActual.IndicePersonaje);
            return string.IsNullOrEmpty(sub) ? "Personaje" : sub;
        }

        private void LateUpdate()
        {
            AsegurarCamaras();
            if (camaraGrabacion == null) return;

            if (!HabilitadoEnPartida)
            {
                CopiarDesdeCamaraPrincipal();
                return;
            }

            if (TomaActual == TomaCamara.Normal)
            {
                CopiarDesdeCamaraPrincipal();
                return;
            }

            ActualizarObjetivo();
            if (objetivoActual == null)
            {
                CopiarDesdeCamaraPrincipal();
                return;
            }

            switch (TomaActual)
            {
                case TomaCamara.PrimeraPersona:
                    AplicarPrimeraPersona();
                    break;
                case TomaCamara.JuegoZoom:
                    AplicarJuegoZoom();
                    break;
                case TomaCamara.TerceraPersona:
                    AplicarTerceraPersona();
                    break;
            }
        }

        private void CopiarDesdeCamaraPrincipal()
        {
            if (camaraPrincipal == null) camaraPrincipal = Camera.main;
            if (camaraPrincipal == null || camaraGrabacion == null) return;

            camaraGrabacion.transform.position = camaraPrincipal.transform.position;
            camaraGrabacion.transform.rotation = camaraPrincipal.transform.rotation;
            camaraGrabacion.fieldOfView = camaraPrincipal.fieldOfView;
            camaraGrabacion.nearClipPlane = camaraPrincipal.nearClipPlane;
            estadoSuavizadoInicializado = false;
        }

        private void ActualizarObjetivo()
        {
            List<Personaje> candidatos = ObtenerCandidatosVisibles();
            if (candidatos.Count == 0)
            {
                objetivoActual = null;
                huesoCabezaCache = null;
                animatorCache = null;
                return;
            }

            if (indiceObjetivoManual >= 0 && indiceObjetivoManual < candidatos.Count)
            {
                FijarObjetivo(candidatos[indiceObjetivoManual]);
                return;
            }

            Personaje duenio = null;
            for (int i = 0; i < candidatos.Count; i++)
            {
                if (!candidatos[i].IsSpawned || candidatos[i].IsOwner)
                {
                    duenio = candidatos[i];
                    break;
                }
            }

            FijarObjetivo(duenio != null ? duenio : candidatos[0]);
        }

        private List<Personaje> ObtenerCandidatosVisibles()
        {
            Personaje[] todos = FindObjectsByType<Personaje>();
            var lista = new List<Personaje>(todos.Length);
            for (int i = 0; i < todos.Length; i++)
            {
                Personaje p = todos[i];
                if (p == null || !p.gameObject.activeInHierarchy) continue;
                if (!p.IsSpawned || p.IsOwner || !p.EstaMuriendo)
                {
                    lista.Add(p);
                }
            }
            if (lista.Count == 0)
            {
                for (int i = 0; i < todos.Length; i++)
                {
                    if (todos[i] != null && todos[i].gameObject.activeInHierarchy) lista.Add(todos[i]);
                }
            }
            return lista;
        }

        private void FijarObjetivo(Personaje nuevo)
        {
            if (objetivoActual != nuevo)
            {
                objetivoActual = nuevo;
                huesoCabezaCache = null;
                animatorCache = null;
            }
        }

        private Transform ObtenerHuesoCabeza()
        {
            if (objetivoActual == null) return null;

            Animator animActual = objetivoActual.GetComponentInChildren<Animator>(false);
            if (huesoCabezaCache != null && animatorCache == animActual && huesoCabezaCache.gameObject.activeInHierarchy)
            {
                return huesoCabezaCache;
            }

            animatorCache = animActual;
            huesoCabezaCache = null;

            if (animActual != null && animActual.isHuman)
            {
                huesoCabezaCache = animActual.GetBoneTransform(HumanBodyBones.Head);
            }

            if (huesoCabezaCache == null)
            {
                foreach (Transform tr in objetivoActual.GetComponentsInChildren<Transform>(false))
                {
                    string n = tr.name;
                    if (n.IndexOf("Head", System.StringComparison.OrdinalIgnoreCase) >= 0 &&
                        n.IndexOf("End", System.StringComparison.OrdinalIgnoreCase) < 0)
                    {
                        huesoCabezaCache = tr;
                        break;
                    }
                }
            }

            return huesoCabezaCache;
        }

        private float ObtenerDireccionObjetivo()
        {
            if (objetivoActual == null) return 1f;
            float dir = objetivoActual.DireccionHorizontal;
            return Mathf.Abs(dir) > 0.1f ? Mathf.Sign(dir) : 1f;
        }

        private void AplicarPrimeraPersona()
        {
            float dt = Time.deltaTime;
            float dirObjetivo = ObtenerDireccionObjetivo();

            Transform cabeza = ObtenerHuesoCabeza();
            Vector3 posBaseCabeza = cabeza != null
                ? cabeza.position
                : objetivoActual.transform.position + Vector3.up * (objetivoActual.EstaAgachado ? 0.34f : 0.48f);

            Vector3 adelantePlano = new Vector3(dirObjetivo, 0f, 0f);
            if (objetivoActual.EnRagdoll && cabeza != null)
            {
                Vector3 fCabeza = Vector3.ProjectOnPlane(cabeza.forward, Vector3.up);
                if (fCabeza.sqrMagnitude > 0.05f) adelantePlano = fCabeza.normalized;
            }

            Vector3 posDeseada = posBaseCabeza + Vector3.up * 0.04f + adelantePlano * OffsetFrontalPrimeraPersona;
            // Leve sesgo en +Z para que al girar 180° la transicion pase por el lado abierto de la sala
            Vector3 dirMirada = (adelantePlano + new Vector3(0f, -0.05f, 0.02f)).normalized;
            Quaternion rotDeseada = Quaternion.LookRotation(dirMirada, Vector3.up);

            ActualizarTransformCamaraGrabacion(posDeseada, rotDeseada, 76f, 0.025f, 58f, 22f, dt);
        }

        private void AplicarJuegoZoom()
        {
            float dt = Time.deltaTime;
            float dir = ObtenerDireccionObjetivo();
            Vector3 centro = objetivoActual.transform.position + Vector3.up * 0.34f;
            float zPrincipal = camaraPrincipal != null ? camaraPrincipal.transform.position.z : centro.z + 13.34f;
            float zCam = zPrincipal > centro.z + 5.8f ? centro.z + 5.6f : zPrincipal;

            Vector3 posDeseada = new Vector3(centro.x + dir * 0.55f, centro.y + 0.18f, zCam);
            Quaternion rotDeseada = Quaternion.Euler(0f, 180f, 0f);

            ActualizarTransformCamaraGrabacion(posDeseada, rotDeseada, 23f, 0.15f, 11f, 14f, dt);
        }

        private void AplicarTerceraPersona()
        {
            float dt = Time.deltaTime;
            float dir = ObtenerDireccionObjetivo();

            Vector3 pivote = objetivoActual.transform.position + Vector3.up * 0.42f;
            Vector3 deseado = pivote + new Vector3(-dir * 1.28f, 0.40f, 0.92f);

            Vector3 vector = deseado - pivote;
            float dist = vector.magnitude;
            if (dist > 0.05f)
            {
                RaycastHit[] hits = Physics.SphereCastAll(pivote, 0.11f, vector / dist, dist, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
                float mejor = dist;
                for (int i = 0; i < hits.Length; i++)
                {
                    RaycastHit h = hits[i];
                    if (h.collider == null || h.distance <= 0.05f) continue;
                    if (h.collider.GetComponentInParent<Personaje>() != null) continue;
                    if (h.collider.GetComponentInParent<Arma>() != null) continue;
                    if (h.distance < mejor) mejor = h.distance;
                }
                if (mejor < dist)
                {
                    deseado = pivote + (vector / dist) * Mathf.Max(0.28f, mejor - 0.05f);
                }
            }

            Vector3 puntoMira = pivote + new Vector3(dir * 1.85f, -0.06f, 0f);
            Quaternion rotDeseada = Quaternion.LookRotation((puntoMira - deseado).normalized, Vector3.up);

            ActualizarTransformCamaraGrabacion(deseado, rotDeseada, 58f, 0.06f, 12f, 12f, dt);
        }

        private void ActualizarTransformCamaraGrabacion(
            Vector3 posDeseada,
            Quaternion rotDeseada,
            float fovDeseado,
            float nearDeseado,
            float velPos,
            float velRot,
            float dt)
        {
            if (!estadoSuavizadoInicializado)
            {
                posSuavizada = posDeseada;
                rotSuavizada = rotDeseada;
                fovSuavizado = fovDeseado;
                nearSuavizado = nearDeseado;
                estadoSuavizadoInicializado = true;
            }
            else
            {
                posSuavizada = Vector3.Lerp(posSuavizada, posDeseada, 1f - Mathf.Exp(-velPos * dt));
                rotSuavizada = Quaternion.Slerp(rotSuavizada, rotDeseada, 1f - Mathf.Exp(-velRot * dt));
                fovSuavizado = Mathf.Lerp(fovSuavizado, fovDeseado, 1f - Mathf.Exp(-10f * dt));
                nearSuavizado = nearDeseado;
            }

            camaraGrabacion.transform.position = posSuavizada;
            camaraGrabacion.transform.rotation = rotSuavizada;
            camaraGrabacion.fieldOfView = fovSuavizado;
            camaraGrabacion.nearClipPlane = nearSuavizado;
        }
    }
}
