using UnityEditor;
using UnityEngine;

// Texturizar las paredes laterales de la sala: Paredizq y Paredderec (la pared de fondo "Pared" NO se toca).
// Cada pared lleva su propia textura de 5 m x 10 m: papel tapiz envejecido, zocalo con moldura y rodapie, yeso y ladrillo a la vista
// donde se despega el papel, humedad que sube del piso, goteras, moho, grietas, manchas de agua y mugre. Con mapa de relieve.
// Paredizq = papel verde grisaceo con zocalo cafe | Paredderec = papel crema con zocalo azul petroleo.
// Al importar Assets/sala.fbx se generan UV (z a lo largo, y hacia arriba, en metros) y se asigna el material.
// Parte del escenario Sala: se aplica con Herramientas -> Sala -> Texturizar toda la sala.
internal static class SalaParedes
{
    private const string FBX = "/sala.fbx";
    private const string CARPETA_TEX = "Assets/Sala/Texturas/";
    private const string CARPETA_MAT = "Assets/Sala/Materiales";
    private const float ANCHO = 5f;  // profundidad de la pared (z), en metros
    private const float ALTO = 10f;  // altura (y), en metros

    private static string Clave(string objeto)
    {
        string n = objeto.ToLower();
        if (n.StartsWith("paredizq")) return "izq";
        if (n.StartsWith("paredder")) return "der";
        return null; // "Pared" (fondo), techo, piso: sin tocar
    }

    public static void Aplicar(GameObject raiz)
    {
        int hechos = 0;
        foreach (MeshFilter mf in raiz.GetComponentsInChildren<MeshFilter>())
        {
            string clave = Clave(mf.gameObject.name);
            if (clave == null) continue;
            Mesh mesh = mf.sharedMesh;
            Renderer rend = mf.GetComponent<Renderer>();
            if (mesh == null || rend == null) continue;
            Material mat = AssetDatabase.LoadAssetAtPath<Material>(CARPETA_MAT + "/Sala_pared_" + clave + ".mat");

            Vector3[] vert = mesh.vertices;
            Vector2[] uv = new Vector2[vert.Length];
            for (int i = 0; i < vert.Length; i++)
            {
                Vector3 p = raiz.transform.InverseTransformPoint(mf.transform.TransformPoint(vert[i])); // modelo: 1 unidad = 1 m
                uv[i] = new Vector2((p.z + ANCHO * 0.5f) / ANCHO, p.y / ALTO);
            }
            SalaTexturizador.AsignarUV(mesh, uv);

            if (mat != null)
            {
                var mats = new Material[Mathf.Max(1, mesh.subMeshCount)]; // las paredes del FBX no traen material
                for (int s = 0; s < mats.Length; s++) mats[s] = mat;
                rend.sharedMaterials = mats;
            }
            hechos++;
        }
        Debug.Log("[TexturizarParedes] " + hechos + " paredes laterales texturizadas");
    }


    public static void Configurar()
    {
        SalaTexturizador.AsegurarCarpetas();
        foreach (string clave in new[] { "izq", "der" })
        {
            string baseNombre = "Sala_pared_" + clave;
            var imp = AssetImporter.GetAtPath(CARPETA_TEX + baseNombre + ".png") as TextureImporter;
            if (imp == null) { Debug.LogError("[TexturizarParedes] falta " + CARPETA_TEX + baseNombre + ".png"); return; }
            imp.wrapMode = TextureWrapMode.Clamp; imp.maxTextureSize = 2048; imp.anisoLevel = 8; imp.SaveAndReimport();
            var impN = AssetImporter.GetAtPath(CARPETA_TEX + baseNombre + "_n.png") as TextureImporter;
            if (impN != null) { impN.textureType = TextureImporterType.NormalMap; impN.wrapMode = TextureWrapMode.Clamp; impN.maxTextureSize = 2048; impN.anisoLevel = 8; impN.SaveAndReimport(); }

            string rutaMat = CARPETA_MAT + "/" + baseNombre + ".mat";
            Material m = AssetDatabase.LoadAssetAtPath<Material>(rutaMat);
            if (m == null) { m = new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(m, rutaMat); }
            m.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(CARPETA_TEX + baseNombre + ".png"));
            m.SetTextureScale("_BaseMap", Vector2.one);
            m.SetColor("_BaseColor", Color.white);
            Texture2D nrm = AssetDatabase.LoadAssetAtPath<Texture2D>(CARPETA_TEX + baseNombre + "_n.png");
            if (nrm != null) { m.SetTexture("_BumpMap", nrm); m.SetFloat("_BumpScale", 1.0f); m.EnableKeyword("_NORMALMAP"); }
            m.SetFloat("_Metallic", 0f);
            m.SetFloat("_Smoothness", 0.05f);
            EditorUtility.SetDirty(m);
        }
        AssetDatabase.SaveAssets();

    }
}
