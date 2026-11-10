using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

// Texturizar la lampara de arriba (objeto "Candelabro" de sala.fbx; el resto no se toca).
// Barra y varilla = laton viejo con oxido. Las 3 pantallas = tela amarillenta, cada una con un diseno distinto
// (quemadura, rotura, o ambas) tomado de un atlas de 3 columnas.
// Parte del escenario Sala: se aplica con Herramientas -> Sala -> Texturizar toda la sala.
internal static class SalaCandelabro
{
    private const string FBX = "/sala.fbx";
    private const string CARPETA_TEX = "Assets/Sala/Texturas/";
    private const string CARPETA_MAT = "Assets/Sala/Materiales";
    private const float TILE_METAL = 0.5f;
    private const int DISENOS = 3;

    public static void Aplicar(GameObject raiz)
    {
        Material metal = AssetDatabase.LoadAssetAtPath<Material>(CARPETA_MAT + "/Sala_candelabro_metal.mat");
        Material pantallas = AssetDatabase.LoadAssetAtPath<Material>(CARPETA_MAT + "/Sala_candelabro_pantallas.mat");
        int hechos = 0;

        foreach (MeshFilter mf in raiz.GetComponentsInChildren<MeshFilter>())
        {
            if (!mf.gameObject.name.ToLower().StartsWith("candelabro")) continue;
            Mesh mesh = mf.sharedMesh;
            Renderer rend = mf.GetComponent<Renderer>();
            if (mesh == null || rend == null) continue;

            Material[] mats = rend.sharedMaterials;
            Vector3[] vert = mesh.vertices;
            Vector2[] uv = mesh.uv.Length == vert.Length ? mesh.uv : new Vector2[vert.Length];

            for (int s = 0; s < mesh.subMeshCount && s < mats.Length; s++)
            {
                if (mats[s] == null) continue;
                string nom = mats[s].name.ToLower();
                bool esPantalla = nom.Contains("tela");
                bool esMetal = nom.Contains("cocreto");
                if (!esPantalla && !esMetal) continue;

                int[] tris = mesh.GetTriangles(s);
                var P = new Dictionary<int, Vector3>();
                foreach (int i in tris)
                    if (!P.ContainsKey(i)) P[i] = raiz.transform.InverseTransformPoint(mf.transform.TransformPoint(vert[i]));

                // cada pantalla = grupo de triangulos separados en x (hueco > 0.2 m); se numeran de izquierda a derecha
                int nT = tris.Length / 3;
                var centroX = new float[nT];
                for (int t = 0; t < nT; t++) centroX[t] = (P[tris[3 * t]].x + P[tris[3 * t + 1]].x + P[tris[3 * t + 2]].x) / 3f;
                var orden = new List<float>(centroX); orden.Sort();
                var cortes = new List<float>();
                for (int k = 1; k < orden.Count; k++) if (orden[k] - orden[k - 1] > 0.2f) cortes.Add((orden[k] + orden[k - 1]) * 0.5f);
                var grupo = new int[nT];
                var gmn = new Dictionary<int, Vector3>(); var gmx = new Dictionary<int, Vector3>();
                for (int t = 0; t < nT; t++)
                {
                    int g = 0; foreach (float c in cortes) if (centroX[t] > c) g++;
                    grupo[t] = g;
                    for (int c = 0; c < 3; c++)
                    {
                        Vector3 p = P[tris[3 * t + c]];
                        gmn[g] = gmn.ContainsKey(g) ? Vector3.Min(gmn[g], p) : p;
                        gmx[g] = gmx.ContainsKey(g) ? Vector3.Max(gmx[g], p) : p;
                    }
                }

                for (int t = 0; t < nT; t++)
                {
                    int g = grupo[t];
                    Vector3 mn = gmn[g], mx = gmx[g], sz = Vector3.Max(mx - mn, new Vector3(1e-4f, 1e-4f, 1e-4f));
                    Vector3 n = Vector3.Cross(P[tris[3 * t + 1]] - P[tris[3 * t]], P[tris[3 * t + 2]] - P[tris[3 * t]]).normalized;
                    float ax = Mathf.Abs(n.x), ay = Mathf.Abs(n.y), az = Mathf.Abs(n.z);
                    for (int c = 0; c < 3; c++)
                    {
                        int i = tris[3 * t + c];
                        Vector3 q = P[i];
                        Vector2 r;
                        if (esPantalla)
                        {
                            float u, v;
                            if (az >= ax && az >= ay) { u = (q.x - mn.x) / sz.x; v = (q.y - mn.y) / sz.y; }
                            else if (ax >= ay) { u = (q.z - mn.z) / sz.z; v = (q.y - mn.y) / sz.y; }
                            else { u = (q.x - mn.x) / sz.x; v = (q.z - mn.z) / sz.z; }
                            int col = g % DISENOS;
                            r = new Vector2((col + 0.02f + Mathf.Clamp01(u) * 0.96f) / DISENOS, Mathf.Clamp01(v));
                        }
                        else
                        {
                            if (az >= ax && az >= ay) r = new Vector2(q.x, q.y) / TILE_METAL;
                            else if (ax >= ay) r = new Vector2(q.z, q.y) / TILE_METAL;
                            else r = new Vector2(q.x, q.z) / TILE_METAL;
                        }
                        uv[i] = r;
                    }
                }
                Material nuevo = esPantalla ? pantallas : metal;
                if (nuevo != null) mats[s] = nuevo;
                hechos++;
            }
            SalaTexturizador.AsignarUV(mesh, uv);
            rend.sharedMaterials = mats;
        }
        Debug.Log("[TexturizarCandelabro] " + hechos + " partes texturizadas");
    }


    public static void Configurar()
    {
        SalaTexturizador.AsegurarCarpetas();
        Crear("metal", TextureWrapMode.Repeat, 0.25f, 0.3f);
        Crear("pantallas", TextureWrapMode.Clamp, 0.0f, 0.05f);
        AssetDatabase.SaveAssets();

    }

    private static void Crear(string clave, TextureWrapMode wrap, float metalico, float brillo)
    {
        string rutaTex = CARPETA_TEX + "Sala_candelabro_" + clave + ".png";
        var imp = AssetImporter.GetAtPath(rutaTex) as TextureImporter;
        if (imp == null) { Debug.LogError("[TexturizarCandelabro] falta " + rutaTex); return; }
        imp.wrapMode = wrap;
        imp.maxTextureSize = 2048;
        imp.SaveAndReimport();

        string rutaMat = CARPETA_MAT + "/Sala_candelabro_" + clave + ".mat";
        Material m = AssetDatabase.LoadAssetAtPath<Material>(rutaMat);
        if (m == null) { m = new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(m, rutaMat); }
        m.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(rutaTex));
        m.SetTextureScale("_BaseMap", Vector2.one);
        m.SetColor("_BaseColor", Color.white);
        m.SetFloat("_Metallic", metalico);
        m.SetFloat("_Smoothness", brillo);
        EditorUtility.SetDirty(m);
    }
}
