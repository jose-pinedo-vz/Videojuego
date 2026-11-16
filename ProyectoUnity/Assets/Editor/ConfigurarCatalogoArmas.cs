using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Armas.Editor
{
    /// <summary>
    /// Mantiene sincronizado Assets/Resources/Armas/CatalogoArmas.asset con los FBX de armas y caja de munición
    /// para garantizar que las compilaciones (.exe) incluyan todos los modelos y materiales.
    /// </summary>
    [InitializeOnLoad]
    public static class ConfigurarCatalogoArmas
    {
        private const string CARPETA_RECURSO = "Assets/Resources/Armas";
        private const string RUTA_ASSET = CARPETA_RECURSO + "/CatalogoArmas.asset";
        private const string RUTA_CAJA_MUNICION = "Assets/Modelos/caja municion/caja municion.fbx";
        private const string RUTA_BARRENO = "Assets/Modelos/barreno/barreno.fbx";

        static ConfigurarCatalogoArmas()
        {
            EditorApplication.delayCall += SincronizarSiFalta;
        }

        private static void SincronizarSiFalta()
        {
            if (Application.isPlaying) return;
            CatalogoArmas cat = AssetDatabase.LoadAssetAtPath<CatalogoArmas>(RUTA_ASSET);
            if (cat != null && cat.modelos != null && cat.modelos.Length >= GestorArmas.TotalModelosDefinidos
                && cat.prefabCajaMunicion != null && cat.prefabBarrenoProyectil != null)
            {
                bool completo = true;
                for (int i = 0; i < cat.modelos.Length; i++)
                {
                    if (cat.modelos[i] == null || cat.modelos[i].prefabModelo == null)
                    {
                        completo = false;
                        break;
                    }
                }
                if (completo) return;
            }
            ActualizarCatalogo(false);
        }

        [MenuItem("Herramientas/Armas/Actualizar Catálogo de Armas (Resources)", priority = 50)]
        public static void EjecutarMenu()
        {
            ActualizarCatalogo(true);
        }

        public static void ActualizarCatalogo(bool log)
        {
            if (!AssetDatabase.IsValidFolder("Assets/Resources"))
                AssetDatabase.CreateFolder("Assets", "Resources");
            if (!AssetDatabase.IsValidFolder(CARPETA_RECURSO))
                AssetDatabase.CreateFolder("Assets/Resources", "Armas");

            CatalogoArmas cat = AssetDatabase.LoadAssetAtPath<CatalogoArmas>(RUTA_ASSET);
            if (cat == null)
            {
                cat = ScriptableObject.CreateInstance<CatalogoArmas>();
                AssetDatabase.CreateAsset(cat, RUTA_ASSET);
            }

            List<(string nombre, string ruta)> definiciones = GestorArmas.ObtenerRutasModelos();
            var entradas = new EntradaModeloArma[definiciones.Count];
            for (int i = 0; i < definiciones.Count; i++)
            {
                string nombre = definiciones[i].nombre;
                string ruta = definiciones[i].ruta;
                entradas[i] = new EntradaModeloArma
                {
                    nombre = nombre,
                    rutaAsset = ruta,
                    prefabModelo = AssetDatabase.LoadAssetAtPath<GameObject>(ruta)
                };
            }

            cat.modelos = entradas;
            cat.prefabCajaMunicion = AssetDatabase.LoadAssetAtPath<GameObject>(RUTA_CAJA_MUNICION);
            cat.prefabBarrenoProyectil = AssetDatabase.LoadAssetAtPath<GameObject>(RUTA_BARRENO);

            EditorUtility.SetDirty(cat);
            AssetDatabase.SaveAssets();

            if (log)
            {
                Debug.Log($"[CatalogoArmas] Catálogo actualizado en {RUTA_ASSET} con {entradas.Length} modelos.");
            }
        }
    }
}
