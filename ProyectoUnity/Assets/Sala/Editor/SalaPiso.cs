using UnityEditor;
using UnityEngine;

// Texturizar el piso de la sala (solo el objeto "Piso"; el resto de sala.fbx no se toca).
// Piso de tablones de madera viejos: veta, juntas, clavos, grietas, manchas de humedad, moho, barniz gastado, rayones y mugre.
// Las tablas corren a lo largo del cuarto (eje x). 1 repeticion = 5 m x 2.5 m.
// Al importar Assets/sala.fbx se generan UV (proyeccion desde arriba, en metros) y se asigna el material nuevo.
// Parte del escenario Sala: se aplica con Herramientas -> Sala -> Texturizar toda la sala.
internal static class SalaPiso
{
    private const string FBX = "/sala.fbx";
    private const string RUTA_TEX = "Assets/Sala/Texturas/Sala_piso.png";
    private const string RUTA_NRM = "Assets/Sala/Texturas/Sala_piso_n.png";
    private const string CARPETA_MAT = "Assets/Sala/Materiales";
    private const string RUTA_MAT = "Assets/Sala/Materiales/Sala_piso.mat";
    private const float TILE_X = 5.0f; // metros por repeticion a lo largo de las tablas
    private const float TILE_Z = 2.5f; // metros por repeticion a lo ancho (16 tablas)

    public static void Aplicar(GameObject raiz)
    {
        Material mat = AssetDatabase.LoadAssetAtPath<Material>(RUTA_MAT);
        int hechos = 0;
        foreach (MeshFilter mf in raiz.GetComponentsInChildren<MeshFilter>())
        {
            if (mf.gameObject.name.ToLower() != "piso") continue;
            Mesh mesh = mf.sharedMesh;
            Renderer rend = mf.GetComponent<Renderer>();
            if (mesh == null || rend == null) continue;

            Vector3[] vert = mesh.vertices;
            Vector2[] uv = new Vector2[vert.Length];
            for (int i = 0; i < vert.Length; i++)
            {
                // posicion en el espacio del modelo (1 unidad = 1 m): x a lo largo del cuarto, z a lo ancho
                Vector3 p = raiz.transform.InverseTransformPoint(mf.transform.TransformPoint(vert[i]));
                uv[i] = new Vector2(p.x / TILE_X, p.z / TILE_Z);
            }
            SalaTexturizador.AsignarUV(mesh, uv);

            if (mat != null)
            {
                var mats = new Material[Mathf.Max(1, mesh.subMeshCount)]; // el piso del FBX no trae material
                for (int s = 0; s < mats.Length; s++) mats[s] = mat;
                rend.sharedMaterials = mats;
            }
            hechos++;
        }
        Debug.Log("[TexturizarPiso] " + hechos + " piso texturizado");
    }


    public static void Configurar()
    {
        SalaTexturizador.AsegurarCarpetas();

        var imp = AssetImporter.GetAtPath(RUTA_TEX) as TextureImporter;
        if (imp == null) { Debug.LogError("[TexturizarPiso] falta " + RUTA_TEX); return; }
        imp.wrapMode = TextureWrapMode.Repeat; imp.maxTextureSize = 2048; imp.anisoLevel = 8; imp.SaveAndReimport();
        var impN = AssetImporter.GetAtPath(RUTA_NRM) as TextureImporter;
        if (impN != null) { impN.textureType = TextureImporterType.NormalMap; impN.wrapMode = TextureWrapMode.Repeat; impN.maxTextureSize = 2048; impN.anisoLevel = 8; impN.SaveAndReimport(); }

        Material m = AssetDatabase.LoadAssetAtPath<Material>(RUTA_MAT);
        if (m == null) { m = new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(m, RUTA_MAT); }
        m.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(RUTA_TEX));
        m.SetTextureScale("_BaseMap", Vector2.one); // la repeticion va en las UV
        m.SetColor("_BaseColor", Color.white);
        Texture2D nrm = AssetDatabase.LoadAssetAtPath<Texture2D>(RUTA_NRM);
        if (nrm != null) { m.SetTexture("_BumpMap", nrm); m.SetFloat("_BumpScale", 1.0f); m.EnableKeyword("_NORMALMAP"); }
        m.SetFloat("_Metallic", 0f);
        m.SetFloat("_Smoothness", 0.15f);
        EditorUtility.SetDirty(m);
        AssetDatabase.SaveAssets();

    }
}
