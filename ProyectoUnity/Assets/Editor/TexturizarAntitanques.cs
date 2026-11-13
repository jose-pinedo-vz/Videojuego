using UnityEditor;
using UnityEngine;

/// <summary>
/// Herramienta de editor: el francotirador antitanques (Assets/Modelos/Antitanques/antitanques.fbx, un AWM) venia sin texturas
/// (apuntaban a una carpeta de otra PC). Las texturas se hornearon en Blender y estan en Antitanques/Texturas.
/// Este script crea un material URP por cada material del FBX con su textura y se los asigna al modelo.
/// Se aplica solo al abrir el proyecto si todavia no esta hecho. Menu: Herramientas/Texturizar antitanques
/// </summary>
public static class TexturizarAntitanques
{
    private const string RUTA_FBX = "Assets/Modelos/Antitanques/antitanques.fbx";
    private const string CARPETA_TEX = "Assets/Modelos/Antitanques/Texturas/";
    private const string CARPETA_MAT = "Assets/Modelos/Antitanques/Materiales/";

    // material del FBX -> (textura, metalico, suavidad)
    private static readonly (string mat, string tex, float metal, float suave)[] MATERIALES =
    {
        ("MI_AWM.1",       "awm_MI_AWM_1",       0.35f, 0.45f), // cuerpo: culata verde oliva, canon de metal
        ("MI_AWM",         "awm_MI_AWM",         0.80f, 0.55f), // cargador
        ("CQBSS",          "awm_CQBSS",          0.30f, 0.40f), // mira
        ("CQB_GlassInner", "awm_CQB_GlassInner", 0.00f, 0.95f), // lente
        ("Mat.1",          "awm_Mat_1",          0.20f, 0.40f), // tapas de la mira
        ("material",       "awm_material",       0.20f, 0.40f),
    };

    [InitializeOnLoadMethod]
    private static void AplicarSiFalta()
    {
        EditorApplication.delayCall += () =>
        {
            ModelImporter imp = AssetImporter.GetAtPath(RUTA_FBX) as ModelImporter;
            if (imp == null) return;
            if (imp.GetExternalObjectMap().Count >= MATERIALES.Length) return; // ya aplicado
            Debug.Log(Aplicar());
        };
    }

    [MenuItem("Herramientas/Texturizar antitanques")]
    public static void Ejecutar()
    {
        Debug.Log(Aplicar());
    }

    public static string Aplicar()
    {
        ModelImporter imp = AssetImporter.GetAtPath(RUTA_FBX) as ModelImporter;
        if (imp == null) return "No se encontro " + RUTA_FBX;
        if (!AssetDatabase.IsValidFolder(CARPETA_MAT.TrimEnd('/')))
            AssetDatabase.CreateFolder("Assets/Modelos/Antitanques", "Materiales");

        Shader sh = Shader.Find("Universal Render Pipeline/Lit");
        int hechos = 0;
        foreach (var d in MATERIALES)
        {
            Texture2D tex = AssetDatabase.LoadAssetAtPath<Texture2D>(CARPETA_TEX + d.tex + ".png");
            if (tex == null) { Debug.LogWarning("Falta la textura " + d.tex); continue; }

            string rutaMat = CARPETA_MAT + "AWM_" + d.mat.Replace(".", "_") + ".mat";
            Material m = AssetDatabase.LoadAssetAtPath<Material>(rutaMat);
            if (m == null)
            {
                m = new Material(sh);
                AssetDatabase.CreateAsset(m, rutaMat);
            }
            if (m.HasProperty("_BaseMap")) m.SetTexture("_BaseMap", tex);
            m.mainTexture = tex;
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", Color.white);
            if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", d.metal);
            if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", d.suave);
            EditorUtility.SetDirty(m);

            imp.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), d.mat), m);
            hechos++;
        }
        AssetDatabase.SaveAssets();
        imp.SaveAndReimport();
        return "Antitanques texturizado: " + hechos + " materiales en " + CARPETA_MAT;
    }
}
