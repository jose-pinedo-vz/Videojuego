using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Escenario COCHERA: texturas, materiales y scripts de texturizado de Cochera.fbx en una sola carpeta (Assets/Cochera).
//  Assets/Cochera/Texturas   -> imagenes (PNG)
//  Assets/Cochera/Materiales -> materiales (se crean solos)
//  Assets/Cochera/Editor     -> scripts (uno por grupo de objetos) + este archivo
// Al importar Assets/Cochera.fbx este AssetPostprocessor genera UV y asigna materiales a cada grupo de objetos.
// NO toca: la pared de fondo ("pared"), ni Juego.unity.
// Uso: Herramientas -> Cochera -> Texturizar toda la cochera.
// Para quitarlo todo: borrar la carpeta Assets/Cochera y reimportar Cochera.fbx.
public class CocheraTexturizador : AssetPostprocessor
{
    private const string FBX = "/cochera.fbx";

    // renderers que ya recibieron su textura propia (los demas reciben el material de relleno)
    public static readonly HashSet<Renderer> Tratados = new HashSet<Renderer>();

    private void OnPostprocessModel(GameObject raiz)
    {
        if (!assetPath.ToLower().EndsWith(FBX)) return;
        Tratados.Clear();
        CocheraBase.Aplicar(raiz);
        CocheraCajas.Aplicar(raiz);
        CocheraEstantes.Aplicar(raiz);
        CocheraObjetos.Aplicar(raiz);
        CocheraRelleno.Aplicar(raiz);
    }

    // Subir este numero fuerza a Unity a reimportar Cochera.fbx cuando se edita un script
    public override uint GetVersion() { return 7; }

    // Corre DESPUES de CorregirMaterialesMapas (del equipo), que tambien reasigna materiales al importar Cochera.fbx
    public override int GetPostprocessOrder() { return 1000; }

    // UV seguras (sin NaN/infinito) y tangentes recalculadas para que el mapa de normales no genere pixeles invalidos
    public static void AsignarUV(Mesh mesh, Vector2[] uv)
    {
        for (int i = 0; i < uv.Length; i++)
            if (float.IsNaN(uv[i].x) || float.IsNaN(uv[i].y) || float.IsInfinity(uv[i].x) || float.IsInfinity(uv[i].y)) uv[i] = Vector2.zero;
        mesh.uv = uv;
        mesh.RecalculateTangents();
    }

    public static void AsegurarCarpetas()
    {
        if (!AssetDatabase.IsValidFolder("Assets/Cochera")) AssetDatabase.CreateFolder("Assets", "Cochera");
        if (!AssetDatabase.IsValidFolder("Assets/Cochera/Texturas")) AssetDatabase.CreateFolder("Assets/Cochera", "Texturas");
        if (!AssetDatabase.IsValidFolder("Assets/Cochera/Materiales")) AssetDatabase.CreateFolder("Assets/Cochera", "Materiales");
    }

    // Crea o actualiza un material URP Lit con textura (y mapa de normales opcional)
    public static Material CrearMaterial(string ruta, string texBase, string texNormal, float brillo)
    {
        Texture2D t = AssetDatabase.LoadAssetAtPath<Texture2D>(texBase);
        if (t == null) { Debug.LogError("[Cochera] falta " + texBase); return null; }
        var imp = AssetImporter.GetAtPath(texBase) as TextureImporter;
        if (imp != null) { imp.wrapMode = TextureWrapMode.Repeat; imp.maxTextureSize = 2048; imp.anisoLevel = 8; imp.SaveAndReimport(); }
        if (texNormal != null)
        {
            var impN = AssetImporter.GetAtPath(texNormal) as TextureImporter;
            if (impN != null) { impN.textureType = TextureImporterType.NormalMap; impN.wrapMode = TextureWrapMode.Repeat; impN.maxTextureSize = 2048; impN.SaveAndReimport(); }
        }
        Material m = AssetDatabase.LoadAssetAtPath<Material>(ruta);
        if (m == null) { m = new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(m, ruta); }
        m.SetTexture("_BaseMap", t);
        m.SetTextureScale("_BaseMap", Vector2.one); // la repeticion va en las UV
        m.SetColor("_BaseColor", Color.white);
        Texture2D n = texNormal != null ? AssetDatabase.LoadAssetAtPath<Texture2D>(texNormal) : null;
        if (n != null) { m.SetTexture("_BumpMap", n); m.SetFloat("_BumpScale", 1f); m.EnableKeyword("_NORMALMAP"); }
        else { m.SetTexture("_BumpMap", null); m.DisableKeyword("_NORMALMAP"); }
        m.SetFloat("_Metallic", 0f);
        m.SetFloat("_Smoothness", brillo);
        EditorUtility.SetDirty(m);
        return m;
    }

    [MenuItem("Herramientas/Cochera/Texturizar toda la cochera")]
    public static void TexturizarTodo()
    {
        AssetDatabase.Refresh();
        AsegurarCarpetas();
        CocheraBase.Configurar();
        CocheraCajas.Configurar();
        CocheraEstantes.Configurar();
        CocheraObjetos.Configurar();
        CocheraRelleno.Configurar();
        AssetDatabase.SaveAssets();

        string fbx = "Assets/Cochera.fbx";
        if (AssetDatabase.LoadAssetAtPath<GameObject>(fbx) == null) { Debug.LogError("[Cochera] no encuentro " + fbx); return; }
        AssetDatabase.ImportAsset(fbx, ImportAssetOptions.ForceUpdate);
        Debug.Log("[Cochera] cochera texturizada");
    }

    // La escena Juego.unity guarda materiales propios (los T_*.mat viejos) sobre la instancia de Cochera.fbx, y esos mandan sobre los del FBX.
    // Si esos materiales se borraron (o simplemente estorban), todo sale rosa. Este menu quita esa sustitucion en la escena.
    [MenuItem("Herramientas/Cochera/Quitar materiales viejos de la escena (Juego.unity)")]
    public static void LimpiarEscena()
    {
        const string ruta = "Assets/Scenes/Juego.unity";
        var escena = EditorSceneManager.GetActiveScene();
        if (escena.path != ruta)
        {
            if (!EditorUtility.DisplayDialog("Cochera", "Se abrira Juego.unity para quitar los materiales viejos de la cochera. Los cambios de la escena actual se guardaran antes. ¿Continuar?", "Continuar", "Cancelar")) return;
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            escena = EditorSceneManager.OpenScene(ruta, OpenSceneMode.Single);
        }
        int hechos = 0;
        foreach (Renderer r in Object.FindObjectsByType<Renderer>(FindObjectsInactive.Include))
        {
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
        Debug.Log("[Cochera] materiales viejos quitados de la escena: " + hechos + ". Ahora usa Herramientas > Cochera > Texturizar toda la cochera");
    }
}
