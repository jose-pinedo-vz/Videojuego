using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Escenario PATIO: texturas, materiales y scripts en una sola carpeta (Assets/Patio).
//   Assets/Patio/Texturas   -> imagenes (PNG)
//   Assets/Patio/Materiales -> materiales (se crean solos)
//   Assets/Patio/Editor     -> scripts
// Al importar Patio.fbx este AssetPostprocessor genera UV y asigna materiales a los objetos que ya estan hechos.
// Hecho hasta ahora: nubes, arboles (copa + tronco), arbusto, cajas de madera, barda, piso, parrillas y muebles de madera y paredes laterales. Lo demas del patio queda como estaba.
// Uso: Herramientas -> Patio -> Texturizar nubes, arboles, arbusto, cajas, barda, piso, parrillas y muebles de madera y paredes laterales.
// Para quitarlo: borrar la carpeta Assets/Patio y reimportar Patio.fbx.
public class PatioTexturizador : AssetPostprocessor
{
    private const string FBX = "/patio.fbx";

    private void OnPostprocessModel(GameObject raiz)
    {
        if (!assetPath.ToLower().EndsWith(FBX)) return;
        PatioNaturaleza.Aplicar(raiz);
        PatioCajas.Aplicar(raiz);
        PatioBarda.Aplicar(raiz);
        PatioPiso.Aplicar(raiz);
        PatioParrilla.Aplicar(raiz);
        PatioMadera.Aplicar(raiz);
        PatioParedes.Aplicar(raiz);
    }

    public override uint GetVersion() { return 11; }

    // Corre DESPUES de CorregirMaterialesMapas (del equipo), que tambien reasigna materiales al importar
    public override int GetPostprocessOrder() { return 1000; }

    public static void AsegurarCarpetas()
    {
        if (!AssetDatabase.IsValidFolder("Assets/Patio")) AssetDatabase.CreateFolder("Assets", "Patio");
        if (!AssetDatabase.IsValidFolder("Assets/Patio/Texturas")) AssetDatabase.CreateFolder("Assets/Patio", "Texturas");
        if (!AssetDatabase.IsValidFolder("Assets/Patio/Materiales")) AssetDatabase.CreateFolder("Assets/Patio", "Materiales");
    }

    static string RutaFbx()
    {
        foreach (string p in AssetDatabase.GetAllAssetPaths())
            if (p.StartsWith("Assets/") && p.ToLower().EndsWith(FBX)) return p;
        return null;
    }

    [MenuItem("Herramientas/Patio/Texturizar nubes, arboles, arbusto, cajas, barda, piso, parrillas y muebles de madera y paredes laterales")]
    public static void TexturizarTodo()
    {
        AssetDatabase.Refresh();
        AsegurarCarpetas();
        PatioNaturaleza.Configurar();
        PatioCajas.Configurar();
        PatioBarda.Configurar();
        PatioPiso.Configurar();
        PatioParrilla.Configurar();
        PatioMadera.Configurar();
        PatioParedes.Configurar();
        AssetDatabase.SaveAssets();
        string fbx = RutaFbx();
        if (fbx == null) { Debug.LogError("[Patio] no encuentro Patio.fbx dentro de Assets"); return; }
        AssetDatabase.ImportAsset(fbx, ImportAssetOptions.ForceUpdate);
        Debug.Log("[Patio] patio texturizado (" + fbx + ")");
    }

    // Si la escena guarda materiales propios sobre estos objetos del Patio.fbx, esos mandan sobre los del FBX.
    // Este menu quita esa sustitucion SOLO en las nubes, arboles y arbusto de la escena abierta.
    [MenuItem("Herramientas/Patio/Quitar materiales viejos de nubes y arboles (escena abierta)")]
    public static void LimpiarEscena()
    {
        var escena = EditorSceneManager.GetActiveScene();
        int hechos = 0;
        foreach (Renderer r in Object.FindObjectsByType<Renderer>(FindObjectsInactive.Include))
        {
            if (!PatioNaturaleza.EsDeEstaTanda(r.gameObject.name)) continue;
            if (!PrefabUtility.IsPartOfPrefabInstance(r)) continue;
            Object fuente = PrefabUtility.GetCorrespondingObjectFromOriginalSource(r);
            if (fuente == null || !AssetDatabase.GetAssetPath(fuente).ToLower().EndsWith(FBX)) continue;
            var so = new SerializedObject(r);
            SerializedProperty prop = so.FindProperty("m_Materials");
            if (prop == null) continue;
            for (int i = 0; i < prop.arraySize; i++)
            {
                SerializedProperty el = prop.GetArrayElementAtIndex(i);
                if (el.prefabOverride) { PrefabUtility.RevertPropertyOverride(el, InteractionMode.AutomatedAction); hechos++; }
            }
            if (prop.prefabOverride) PrefabUtility.RevertPropertyOverride(prop, InteractionMode.AutomatedAction);
        }
        EditorSceneManager.MarkSceneDirty(escena);
        EditorSceneManager.SaveScene(escena);
        Debug.Log("[Patio] materiales viejos quitados en la escena " + escena.name + ": " + hechos);
    }
}
