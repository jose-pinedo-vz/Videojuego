using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

// Barda (cerca de tablas) del patio.
//   barda (139 tablas sueltas): cada tabla se mapea completa con una textura de tabla de 0.16 x 2.5 m. Hay 6 diseños que se reparten al azar:
//     1 pintura roja descascarada - 2 naranja desvanecida - 3 cafe oscuro con moho - 4 madera gris sin pintura - 5 encalada y saltada - 6 verde desvanecida.
//   Cada tabla trae veta vertical, nudos, grietas largas, clavos oxidados con chorreones, manchas de agua arriba y lodo/moho abajo. Las puntas usan Patio_barda_testa.
//   BARDA_01_tubo y BARDA_07_tubo: tubos de fierro oxidados.
internal static class PatioBarda
{
    private const string TEX = "Assets/Patio/Texturas/Patio_barda_";
    private const string MAT = "Assets/Patio/Materiales/Patio_barda_";
    private static readonly string[] NOMBRES = { "1", "2", "3", "4", "5", "6", "testa", "tubo", "tubo_tapa" };

    private static Material M(string n) { return AssetDatabase.LoadAssetAtPath<Material>(MAT + n + ".mat"); }

    public static void Aplicar(GameObject raiz)
    {
        int hechos = 0;
        foreach (MeshFilter mf in raiz.GetComponentsInChildren<MeshFilter>())
        {
            string n = mf.gameObject.name.ToLower();
            Renderer rend = mf.GetComponent<Renderer>();
            if (mf.sharedMesh == null || rend == null) continue;
            bool tablas = n == "barda", tubo = n.StartsWith("barda_");
            if (!tablas && !tubo) continue;
            Material[] mats = tablas
                ? new[] { M("1"), M("2"), M("3"), M("4"), M("5"), M("6"), M("testa") }
                : new[] { M("tubo"), M("tubo_tapa") };
            bool ok = true; foreach (Material m in mats) if (m == null) ok = false;
            if (!ok) { Debug.LogError("[Patio] faltan materiales de " + mf.gameObject.name + ", usa el menu Texturizar"); continue; }
            var piezas = PatioNaturaleza.Piezas(raiz, mf);
            var uvs = new List<Vector2[]>(); var capa = new List<int>(); var tris = new List<PatioNaturaleza.Tri>();
            foreach (var pz in piezas)
            {
                if (tablas) Tabla(pz, uvs, capa, tris); else Tubo(pz, uvs, capa, tris);
            }
            PatioNaturaleza.Reconstruir(raiz, mf, tris, uvs, capa, mats.Length);
            rend.sharedMaterials = mats;
            hechos++;
        }
        Debug.Log("[Patio] barda texturizada: " + hechos + " objetos");
    }

    // ---------- tabla: la textura entera cubre el ancho; v de abajo (0) a arriba (1) ----------
    private static void Tabla(PatioNaturaleza.Pieza pz, List<Vector2[]> uvs, List<int> capas, List<PatioNaturaleza.Tri> tris)
    {
        Vector3 e1 = pz.E1, e2 = pz.E2, e3 = pz.E3;
        if (e1.y < 0f) e1 = -e1; // el eje largo apunta hacia arriba
        float l1 = 1e-3f, l2 = 1e-3f, l3 = 1e-3f;
        foreach (var t in pz.T) for (int i = 0; i < 3; i++)
        {
            Vector3 d = t.P[i] - pz.Ctr;
            l1 = Mathf.Max(l1, Mathf.Abs(Vector3.Dot(d, e1))); l2 = Mathf.Max(l2, Mathf.Abs(Vector3.Dot(d, e2))); l3 = Mathf.Max(l3, Mathf.Abs(Vector3.Dot(d, e3)));
        }
        int variante = Mathf.Min(5, (int)(PatioNaturaleza.Azar(pz.Ctr, 1f) * 6f));
        float off = PatioNaturaleza.Azar(pz.Ctr, 2.7f);
        foreach (var t in pz.T)
        {
            float a1 = Mathf.Abs(Vector3.Dot(t.G, e1)), a2 = Mathf.Abs(Vector3.Dot(t.G, e2)), a3 = Mathf.Abs(Vector3.Dot(t.G, e3));
            var uv = new Vector2[3]; int capa;
            for (int c = 0; c < 3; c++)
            {
                Vector3 d = t.P[c] - pz.Ctr;
                float x1 = Vector3.Dot(d, e1), x2 = Vector3.Dot(d, e2), x3 = Vector3.Dot(d, e3);
                float v = Mathf.Clamp(x1 / (2f * l1) + 0.5f, 0.01f, 0.99f);
                if (a1 > a2 && a1 > a3) uv[c] = new Vector2(x2 * 3f + off, x3 * 3f + off);                                   // punta (testa)
                else if (a2 > a3) uv[c] = new Vector2(0.4f + (x3 / (2f * l3) + 0.5f) * 0.2f, v);                              // canto angosto
                else uv[c] = new Vector2(Mathf.Clamp(x2 / (2f * l2) + 0.5f, 0.01f, 0.99f), v);                                // cara ancha
            }
            capa = (a1 > a2 && a1 > a3) ? 6 : variante;
            tris.Add(t); uvs.Add(uv); capas.Add(capa);
        }
    }

    // ---------- tubo: lados por angulo, tapas planas; el eje es donde se acumulan las normales de las tapas ----------
    private static void Tubo(PatioNaturaleza.Pieza pz, List<Vector2[]> uvs, List<int> capas, List<PatioNaturaleza.Tri> tris)
    {
        float[,] T3 = new float[3, 3];
        foreach (var t in pz.T)
        {
            float ar = Vector3.Cross(t.P[1] - t.P[0], t.P[2] - t.P[0]).magnitude;
            for (int a = 0; a < 3; a++) for (int b = 0; b < 3; b++) T3[a, b] += ar * t.G[a] * t.G[b];
        }
        Vector3[] e; float[] val; PatioNaturaleza.Jacobi(T3, out e, out val);
        int im = 0; for (int i = 1; i < 3; i++) if (val[i] > val[im]) im = i;
        Vector3 ax = e[im];
        Vector3 p = Vector3.Cross(ax, Mathf.Abs(ax.y) < 0.9f ? Vector3.up : Vector3.right).normalized; Vector3 q = Vector3.Cross(ax, p);
        float lo = 1e9f, rad = 0f;
        foreach (var t in pz.T) for (int i = 0; i < 3; i++)
        {
            Vector3 d = t.P[i] - pz.Ctr; float h = Vector3.Dot(d, ax); lo = Mathf.Min(lo, h);
            rad = Mathf.Max(rad, (d - ax * h).magnitude);
        }
        rad = Mathf.Max(rad, 0.02f);
        int rep = Mathf.Max(1, Mathf.RoundToInt(2f * Mathf.PI * rad / 0.8f));
        float off = PatioNaturaleza.Azar(pz.Ctr, 1f);
        foreach (var t in pz.T)
        {
            bool tapa = Mathf.Abs(Vector3.Dot(t.G, ax)) > 0.9f;
            var uv = new Vector2[3]; var ang = new float[3];
            for (int c = 0; c < 3; c++)
            {
                Vector3 d = t.P[c] - pz.Ctr;
                if (tapa) uv[c] = new Vector2(Vector3.Dot(d, p) / (2f * rad) + 0.5f, Vector3.Dot(d, q) / (2f * rad) + 0.5f);
                else
                {
                    ang[c] = Mathf.Atan2(Vector3.Dot(d, q), Vector3.Dot(d, p)) / (2f * Mathf.PI);
                    uv[c].y = (Vector3.Dot(d, ax) - lo) / 1.6f + off;
                }
            }
            if (!tapa)
            {
                float mn = Mathf.Min(ang[0], Mathf.Min(ang[1], ang[2])), mx = Mathf.Max(ang[0], Mathf.Max(ang[1], ang[2]));
                for (int c = 0; c < 3; c++) { float a = ang[c]; if (mx - mn > 0.5f && a < 0f) a += 1f; uv[c].x = a * rep + off; }
            }
            tris.Add(t); uvs.Add(uv); capas.Add(tapa ? 1 : 0);
        }
    }

    public static void Configurar()
    {
        foreach (string n in NOMBRES)
        {
            string tex = TEX + n + ".png";
            var imp = AssetImporter.GetAtPath(tex) as TextureImporter;
            if (imp == null) { Debug.LogError("[Patio] falta " + tex); continue; }
            bool tabla = n.Length == 1;
            imp.wrapMode = (n == "tubo_tapa" || tabla) ? TextureWrapMode.Clamp : TextureWrapMode.Repeat;
            imp.maxTextureSize = 2048; imp.anisoLevel = 8; imp.SaveAndReimport();
            string ruta = MAT + n + ".mat";
            Material m = AssetDatabase.LoadAssetAtPath<Material>(ruta);
            if (m == null) { m = new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(m, ruta); }
            m.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(tex));
            m.SetTextureScale("_BaseMap", Vector2.one);
            m.SetColor("_BaseColor", Color.white);
            m.SetTexture("_BumpMap", null); m.DisableKeyword("_NORMALMAP");
            m.SetFloat("_Metallic", n.StartsWith("tubo") ? 0.5f : 0f);
            m.SetFloat("_Smoothness", n.StartsWith("tubo") ? 0.3f : 0.06f);
            EditorUtility.SetDirty(m);
        }
    }
}
