using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Grabacion.Editor
{
    /// <summary>
    /// Herramientas de Editor para clonar Juego.unity en SetGrabacion.unity y gestionar EditorBuildSettings.
    /// </summary>
    [InitializeOnLoad]
    public static class ConfigurarSetGrabacion
    {
        private const string RUTA_ESCENA_JUEGO = "Assets/Scenes/Juego.unity";
        private const string RUTA_ESCENA_SET = "Assets/Scenes/SetGrabacion.unity";
        private const string CARPETA_GRABACIONES = "Assets/Grabaciones";

        static ConfigurarSetGrabacion()
        {
            EditorApplication.delayCall += InicializarSiFalta;
        }

        private static void InicializarSiFalta()
        {
            if (Application.isPlaying) return;

            if (!AssetDatabase.IsValidFolder(CARPETA_GRABACIONES))
            {
                Directory.CreateDirectory(CARPETA_GRABACIONES);
                AssetDatabase.Refresh();
            }

            if (!File.Exists(RUTA_ESCENA_SET) && File.Exists(RUTA_ESCENA_JUEGO))
            {
                CopiarEscenaLimpia();
                AplicarOrdenBuild(iniciarEnSet: false, incluirSet: true, mostrarLog: true);
            }
        }

        [MenuItem("Herramientas/Set de Grabación/Crear o Actualizar Escena SetGrabacion", priority = 10)]
        public static void CrearOActualizarEscenaSet()
        {
            if (Application.isPlaying)
            {
                EditorUtility.DisplayDialog("Set de Grabación", "Sal del Play Mode antes de actualizar la escena.", "Entendido");
                return;
            }

            if (!File.Exists(RUTA_ESCENA_JUEGO))
            {
                Debug.LogError("[SetGrabacion] No se encontro " + RUTA_ESCENA_JUEGO);
                return;
            }

            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            string escenaPrevia = SceneManager.GetActiveScene().path;
            CopiarEscenaLimpia();

            // Añadir objeto explicito DirectorSetGrabacion en la jerarquia de SetGrabacion.unity
            Scene escenaSet = EditorSceneManager.OpenScene(RUTA_ESCENA_SET, OpenSceneMode.Single);
            if (Object.FindAnyObjectByType<Grabacion.DirectorSetGrabacion>() == null)
            {
                GameObject go = new GameObject("DirectorSetGrabacion");
                go.AddComponent<Grabacion.DirectorSetGrabacion>();
                EditorSceneManager.MarkSceneDirty(escenaSet);
            }
            EditorSceneManager.SaveScene(escenaSet, RUTA_ESCENA_SET);

            AplicarOrdenBuild(iniciarEnSet: false, incluirSet: true, mostrarLog: true);

            if (!string.IsNullOrEmpty(escenaPrevia) && File.Exists(escenaPrevia) && escenaPrevia != RUTA_ESCENA_SET)
            {
                EditorSceneManager.OpenScene(escenaPrevia, OpenSceneMode.Single);
            }

            Debug.Log("[SetGrabacion] Escena clonada y actualizada correctamente en " + RUTA_ESCENA_SET);
        }

        [MenuItem("Herramientas/Set de Grabación/Build: Iniciar en Juego (Normal)", priority = 30)]
        public static void BuildIniciarEnJuego()
        {
            AsegurarEscenaExiste();
            AplicarOrdenBuild(iniciarEnSet: false, incluirSet: true, mostrarLog: true);
        }

        [MenuItem("Herramientas/Set de Grabación/Build: Iniciar directo en SetGrabacion (.exe Tráiler)", priority = 31)]
        public static void BuildIniciarEnSetGrabacion()
        {
            AsegurarEscenaExiste();
            AplicarOrdenBuild(iniciarEnSet: true, incluirSet: true, mostrarLog: true);
        }

        [MenuItem("Herramientas/Set de Grabación/Build: Excluir SetGrabacion (Build Final)", priority = 32)]
        public static void BuildExcluirSetGrabacion()
        {
            AplicarOrdenBuild(iniciarEnSet: false, incluirSet: false, mostrarLog: true);
        }

        /// <summary>
        /// Punto de entrada opcional para ejecutarse por linea de comandos (-executeMethod).
        /// </summary>
        public static void EjecutarDesdeBatch()
        {
            CopiarEscenaLimpia();
            Scene escenaSet = EditorSceneManager.OpenScene(RUTA_ESCENA_SET, OpenSceneMode.Single);
            if (Object.FindAnyObjectByType<Grabacion.DirectorSetGrabacion>() == null)
            {
                GameObject go = new GameObject("DirectorSetGrabacion");
                go.AddComponent<Grabacion.DirectorSetGrabacion>();
            }
            EditorSceneManager.SaveScene(escenaSet, RUTA_ESCENA_SET);
            AplicarOrdenBuild(iniciarEnSet: false, incluirSet: true, mostrarLog: true);
            AssetDatabase.SaveAssets();
        }

        private static void AsegurarEscenaExiste()
        {
            if (!File.Exists(RUTA_ESCENA_SET) && File.Exists(RUTA_ESCENA_JUEGO))
            {
                CopiarEscenaLimpia();
            }
        }

        private static void CopiarEscenaLimpia()
        {
            if (File.Exists(RUTA_ESCENA_SET))
            {
                AssetDatabase.DeleteAsset(RUTA_ESCENA_SET);
            }
            AssetDatabase.CopyAsset(RUTA_ESCENA_JUEGO, RUTA_ESCENA_SET);
            AssetDatabase.Refresh();
        }

        private static void AplicarOrdenBuild(bool iniciarEnSet, bool incluirSet, bool mostrarLog)
        {
            var nuevas = new List<EditorBuildSettingsScene>();

            if (incluirSet && File.Exists(RUTA_ESCENA_SET) && iniciarEnSet)
            {
                nuevas.Add(new EditorBuildSettingsScene(RUTA_ESCENA_SET, true));
            }
            if (File.Exists(RUTA_ESCENA_JUEGO))
            {
                nuevas.Add(new EditorBuildSettingsScene(RUTA_ESCENA_JUEGO, true));
            }
            if (incluirSet && File.Exists(RUTA_ESCENA_SET) && !iniciarEnSet)
            {
                nuevas.Add(new EditorBuildSettingsScene(RUTA_ESCENA_SET, true));
            }

            // Conservar cualquier otra escena adicional que existiera previamente en Build Settings
            foreach (EditorBuildSettingsScene s in EditorBuildSettings.scenes)
            {
                if (s == null || string.IsNullOrEmpty(s.path)) continue;
                if (s.path == RUTA_ESCENA_JUEGO || s.path == RUTA_ESCENA_SET) continue;
                nuevas.Add(s);
            }

            EditorBuildSettings.scenes = nuevas.ToArray();
            if (mostrarLog)
            {
                string modo = !incluirSet
                    ? "Solo Juego (SetGrabacion excluida)"
                    : (iniciarEnSet ? "SetGrabacion como escena [0], Juego en [1]" : "Juego como escena [0], SetGrabacion en [1]");
                Debug.Log("[SetGrabacion] Build Settings actualizado: " + modo);
            }
        }
    }
}
