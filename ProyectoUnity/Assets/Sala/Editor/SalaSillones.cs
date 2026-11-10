using UnityEditor;
using UnityEngine;

// Texturizar sillones de la sala (solo los dos sillones; el resto de sala.fbx no se toca).
// Sillon.002 (grande, material sillon1) = azul con alambres cobre y amarillo.
// Sillon.001 (chico, material sillon2)  = rosa con alambres acero y plateado.
// Version realista: tela tejida, manchas, rasgaduras con espuma y alambres con mapa de relieve (_n.png).
// Al importar Assets/sala.fbx se generan UV por proyeccion de caja (1 repeticion = 1.5 m) y se asigna el material nuevo.
// Parte del escenario Sala: se aplica con Herramientas -> Sala -> Texturizar toda la sala.
internal static class SalaSillones
{
    private const string FBX = "/sala.fbx";
    private const string CARPETA_TEX = "Assets/Sala/Texturas/";
    private const string CARPETA_MAT = "Assets/Sala/Materiales";
    private const float TILE = 1.5f; // metros por repeticion de la textura

    // sala.fbx tiene los materiales reasignados a "sala_sillon1" / "sala_sillon2", por eso se busca por "contiene"
    private static string ClavePorMaterial(string nombre)
    {
        string n = nombre.ToLower();
        if (n.Contains("sillon1")) return "azul";
        if (n.Contains("sillon2")) return "rosa";
        return null;
    }

    // Se ejecuta cada vez que Unity importa sala.fbx
    public static void Aplicar(GameObject raiz)
    {
        int ajustados = 0;
        foreach (MeshFilter mf in raiz.GetComponentsInChildren<MeshFilter>())
        {
            Mesh mesh = mf.sharedMesh;
            Renderer rend = mf.GetComponent<Renderer>();
            if (mesh == null || rend == null) continue;

            Material[] mats = rend.sharedMaterials;
            Vector3[] vert = mesh.vertices;
            Vector3[] norm = mesh.normals;
            Vector2[] uv = mesh.uv.Length == vert.Length ? mesh.uv : new Vector2[vert.Length];
            bool cambio = false;

            for (int s = 0; s < mesh.subMeshCount && s < mats.Length; s++)
            {
                if (mats[s] == null) continue;
                string clave = ClavePorMaterial(mats[s].name);
                if (clave == null) continue;

                Material nuevo = AssetDatabase.LoadAssetAtPath<Material>(CARPETA_MAT + "/Sala_sillon_" + clave + ".mat");
                if (nuevo != null) mats[s] = nuevo;

                foreach (int i in mesh.GetIndices(s))
                {
                    // posicion y normal en el espacio del modelo, para que 1 unidad = 1 metro
                    Vector3 p = raiz.transform.InverseTransformPoint(mf.transform.TransformPoint(vert[i]));
                    Vector3 n = norm.Length > i ? raiz.transform.InverseTransformDirection(mf.transform.TransformDirection(norm[i])) : Vector3.forward;
                    float ax = Mathf.Abs(n.x), ay = Mathf.Abs(n.y), az = Mathf.Abs(n.z);
                    Vector2 c;
                    if (az >= ax && az >= ay) c = new Vector2(p.x, p.y);       // de frente
                    else if (ax >= ay) c = new Vector2(p.z, p.y);               // costados
                    else c = new Vector2(p.x, p.z);                             // arriba y abajo
                    uv[i] = c / TILE;
                    cambio = true;
                }
                ajustados++;
            }
            if (cambio) { SalaTexturizador.AsignarUV(mesh, uv); rend.sharedMaterials = mats; }
        }
        Debug.Log("[TexturizarSillones] " + ajustados + " sillones texturizados");
    }


    public static void Configurar()
    {
        SalaTexturizador.AsegurarCarpetas();

        foreach (string clave in new[] { "azul", "rosa" })
        {
            string rutaTex = CARPETA_TEX + "Sala_sillon_" + clave + ".png";
            var imp = AssetImporter.GetAtPath(rutaTex) as TextureImporter;
            if (imp == null) { Debug.LogError("[TexturizarSillones] falta " + rutaTex); return; }
            imp.wrapMode = TextureWrapMode.Repeat; // la textura se repite sobre el sillon
            imp.maxTextureSize = 1024;
            imp.SaveAndReimport();

            string rutaN = CARPETA_TEX + "Sala_sillon_" + clave + "_n.png";
            var impN = AssetImporter.GetAtPath(rutaN) as TextureImporter;
            if (impN != null)
            {
                impN.textureType = TextureImporterType.NormalMap;
                impN.wrapMode = TextureWrapMode.Repeat;
                impN.maxTextureSize = 1024;
                impN.SaveAndReimport();
            }

            string rutaMat = CARPETA_MAT + "/Sala_sillon_" + clave + ".mat";
            Material m = AssetDatabase.LoadAssetAtPath<Material>(rutaMat);
            if (m == null)
            {
                m = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(m, rutaMat);
            }
            m.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(rutaTex));
            m.SetTextureScale("_BaseMap", Vector2.one); // la repeticion va en las UV
            m.SetColor("_BaseColor", Color.white);
            Texture2D nrm = AssetDatabase.LoadAssetAtPath<Texture2D>(rutaN);
            if (nrm != null)
            {
                m.SetTexture("_BumpMap", nrm);
                m.SetFloat("_BumpScale", 1.0f);
                m.EnableKeyword("_NORMALMAP");
            }
            m.SetFloat("_Smoothness", 0.12f);
            m.SetFloat("_Metallic", 0f);
            EditorUtility.SetDirty(m);
        }
        AssetDatabase.SaveAssets();

    }
}
