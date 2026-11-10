using UnityEditor;
using UnityEngine;

// Relleno temporal: todo objeto de la cochera que todavia no tiene su propia textura recibe una textura sencilla
// (acero / madera / goma) en vez del rosa de "material faltante". Se va quitando objeto por objeto conforme se texturiza cada uno.
// No toca la pared de fondo ("pared").
internal static class CocheraRelleno
{
    private const string TEX = "Assets/Cochera/Texturas/Cochera_relleno_";
    private const string MAT = "Assets/Cochera/Materiales/Cochera_relleno_";
    private const float TILE = 1f; // metros por repeticion

    private static string Tipo(string objeto)
    {
        string n = objeto.ToLower();
        if (n == "pared") return null;
        if (n.StartsWith("botebas") || n.Contains("llanta")) return "goma";
        if (n.StartsWith("banca") || n.StartsWith("mesapequ")) return "tablon";
        return "chapa";
    }

    public static void Aplicar(GameObject raiz)
    {
        int hechos = 0;
        foreach (MeshFilter mf in raiz.GetComponentsInChildren<MeshFilter>())
        {
            Renderer rend = mf.GetComponent<Renderer>();
            Mesh mesh = mf.sharedMesh;
            if (rend == null || mesh == null || CocheraTexturizador.Tratados.Contains(rend)) continue;
            string tipo = Tipo(mf.gameObject.name);
            if (tipo == null) continue;
            Material mat = AssetDatabase.LoadAssetAtPath<Material>(MAT + tipo + ".mat");
            if (mat == null) continue;

            // UV por proyeccion de caja, en metros (por vertice: lo suficiente para un relleno)
            Vector3[] v = mesh.vertices;
            Vector2[] uv = new Vector2[v.Length];
            for (int s = 0; s < mesh.subMeshCount; s++)
            {
                int[] t = mesh.GetTriangles(s);
                for (int i = 0; i + 2 < t.Length; i += 3)
                {
                    Vector3 a = raiz.transform.InverseTransformPoint(mf.transform.TransformPoint(v[t[i]]));
                    Vector3 b = raiz.transform.InverseTransformPoint(mf.transform.TransformPoint(v[t[i + 1]]));
                    Vector3 c = raiz.transform.InverseTransformPoint(mf.transform.TransformPoint(v[t[i + 2]]));
                    Vector3 g = Vector3.Cross(b - a, c - a);
                    float ax = Mathf.Abs(g.x), ay = Mathf.Abs(g.y), az = Mathf.Abs(g.z);
                    for (int k = 0; k < 3; k++)
                    {
                        Vector3 p = k == 0 ? a : (k == 1 ? b : c);
                        Vector2 r = (ax >= ay && ax >= az) ? new Vector2(p.z, p.y) : (ay >= az ? new Vector2(p.x, p.z) : new Vector2(p.x, p.y));
                        uv[t[i + k]] = r / TILE;
                    }
                }
            }
            CocheraTexturizador.AsignarUV(mesh, uv);
            var mats = new Material[Mathf.Max(1, mesh.subMeshCount)];
            for (int s = 0; s < mats.Length; s++) mats[s] = mat;
            rend.sharedMaterials = mats;
            hechos++;
        }
        Debug.Log("[Cochera] objetos con textura de relleno (aun sin textura propia): " + hechos);
    }

    public static void Configurar()
    {
        foreach (string tipo in new[] { "chapa", "tablon", "goma" })
        {
            string tex = TEX + tipo + ".png";
            var imp = AssetImporter.GetAtPath(tex) as TextureImporter;
            if (imp == null) { Debug.LogError("[Cochera] falta " + tex); continue; }
            imp.wrapMode = TextureWrapMode.Repeat; imp.maxTextureSize = 1024; imp.anisoLevel = 4; imp.SaveAndReimport();
            Material m = AssetDatabase.LoadAssetAtPath<Material>(MAT + tipo + ".mat");
            if (m == null) { m = new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(m, MAT + tipo + ".mat"); }
            m.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(tex));
            m.SetTextureScale("_BaseMap", Vector2.one);
            m.SetColor("_BaseColor", Color.white);
            m.SetTexture("_BumpMap", null); m.DisableKeyword("_NORMALMAP");
            m.SetFloat("_Metallic", tipo == "chapa" ? 0.3f : 0f);
            m.SetFloat("_Smoothness", tipo == "goma" ? 0.1f : (tipo == "chapa" ? 0.35f : 0.08f));
            EditorUtility.SetDirty(m);
        }
    }
}
