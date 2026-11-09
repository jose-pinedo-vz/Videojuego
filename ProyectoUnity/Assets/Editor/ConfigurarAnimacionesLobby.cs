using UnityEditor;
using UnityEngine;

// Configura automaticamente las animaciones del lobby:
// - "Stand Up.fbx" se importa como Humanoid (para que sirva en los 4 personajes) y sin ciclo.
// - Asigna "Stand Up" y "Standing Idle" en CatalogoPersonajes (clipLevantarse / clipReposo),
//   asi quedan referenciadas desde Resources y funcionan tambien en el build.
public class ConfigurarAnimacionesLobby : AssetPostprocessor
{
    private const string RUTA_LEVANTARSE = "Assets/Animations/Stand Up.fbx";
    private const string RUTA_REPOSO = "Assets/Animations/Standing Idle.fbx";
    private const string RUTA_SALTITO = "Assets/Animations/vertical jump.fbx";
    private const string RUTA_CATALOGO = "Assets/Resources/Personajes/CatalogoPersonajes.asset";

    private void OnPreprocessModel()
    {
        if (assetPath != RUTA_LEVANTARSE) return;
        ModelImporter imp = (ModelImporter)assetImporter;
        if (imp.importSettingsMissing || imp.animationType != ModelImporterAnimationType.Human)
        {
            imp.animationType = ModelImporterAnimationType.Human;
            imp.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
        }
    }

    private void OnPreprocessAnimation()
    {
        if (assetPath != RUTA_LEVANTARSE) return;
        ModelImporter imp = (ModelImporter)assetImporter;
        ModelImporterClipAnimation[] clips = imp.clipAnimations;
        if (clips == null || clips.Length == 0) clips = imp.defaultClipAnimations;
        foreach (ModelImporterClipAnimation c in clips)
        {
            c.loopTime = false;
            // que no se desplace ni gire el cuerpo fuera del recuadro
            c.lockRootRotation = true;
            c.lockRootHeightY = false;
            c.lockRootPositionXZ = true;
            c.keepOriginalOrientation = true;
            c.keepOriginalPositionXZ = true;
        }
        imp.clipAnimations = clips;
    }

    private static void OnPostprocessAllAssets(string[] importados, string[] borrados, string[] movidos, string[] origenMovidos)
    {
        EditorApplication.delayCall -= AsignarEnCatalogo;
        EditorApplication.delayCall += AsignarEnCatalogo;
    }

    [MenuItem("Herramientas/Asignar animaciones del lobby")]
    private static void AsignarEnCatalogo()
    {
        CatalogoPersonajes cat = AssetDatabase.LoadAssetAtPath<CatalogoPersonajes>(RUTA_CATALOGO);
        if (cat == null) return;

        bool cambio = false;
        if (cat.clipLevantarse == null)
        {
            cat.clipLevantarse = PrimerClip(RUTA_LEVANTARSE);
            cambio |= cat.clipLevantarse != null;
        }
        if (cat.clipReposo == null)
        {
            cat.clipReposo = PrimerClip(RUTA_REPOSO);
            cambio |= cat.clipReposo != null;
        }

        if (cat.clipSaltito == null)
        {
            cat.clipSaltito = PrimerClip(RUTA_SALTITO);
            cambio |= cat.clipSaltito != null;
        }

        if (cambio)
        {
            EditorUtility.SetDirty(cat);
            AssetDatabase.SaveAssetIfDirty(cat);
            Debug.Log("ConfigurarAnimacionesLobby: animaciones del lobby asignadas en CatalogoPersonajes.");
        }
    }

    private static AnimationClip PrimerClip(string ruta)
    {
        foreach (Object o in AssetDatabase.LoadAllAssetsAtPath(ruta))
        {
            if (o is AnimationClip c && !c.name.StartsWith("__preview__")) return c;
        }
        return null;
    }
}
