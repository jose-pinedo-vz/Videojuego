using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

// Parrillas del patio (Parrilla y Parrilla.001): cada una es una olla redonda (cuenco + tapa) con su pata.
//   Parrilla     -> Patio_parrilla_a  esmalte negro quemado, oxido en el cuenco, ceniza, tapa con huellas de calor
//   Parrilla.001 -> Patio_parrilla_b  esmalte verde oscuro descascarado, mas oxidada
//   Las dos llevan banda de union con tornillos a media altura, ventilas con ceniza abajo, bisagra/manija, chorreones de grasa y oxido, abolladuras y rayones.
//   La pata usa Patio_metal_oxido (lamina oxidada).
// La olla se mapea por longitud (alrededor) y altura; el seno de la textura queda a media altura de la olla.
internal static class PatioParrilla
{
    private const string TEX = "Assets/Patio/Texturas/Patio_";
    private const string MAT = "Assets/Patio/Materiales/Patio_";
    private static readonly string[] NOMBRES = { "parrilla_a", "parrilla_b" };

    private static Material M(string n) { return AssetDatabase.LoadAssetAtPath<Material>(MAT + n + ".mat"); }

    public static void Aplicar(GameObject raiz)
    {
        int hechos = 0;
        foreach (MeshFilter mf in raiz.GetComponentsInChildren<MeshFilter>())
        {
            string n = mf.gameObject.name.ToLower();
            Renderer rend = mf.GetComponent<Renderer>();
            if (mf.sharedMesh == null || rend == null || !n.StartsWith("parrilla")) continue;
            string v = n.EndsWith("001") ? "b" : "a";
            Material[] mats = { M("parrilla_" + v), M("metal_oxido") };
            if (mats[0] == null || mats[1] == null) { Debug.LogError("[Patio] faltan materiales de " + mf.gameObject.name + ", usa el menu Texturizar"); continue; }
            var piezas = PatioNaturaleza.Piezas(raiz, mf);
            // la pieza de mayor volumen es la olla; la otra es la pata
            piezas.Sort((a, b) => Vol(b).CompareTo(Vol(a)));
            var uvs = new List<Vector2[]>(); var capa = new List<int>(); var tris = new List<PatioNaturaleza.Tri>();
            for (int i = 0; i < piezas.Count; i++) { if (i == 0) Olla(piezas[i], uvs, capa, tris); else Pata(piezas[i], uvs, capa, tris); }
            PatioNaturaleza.Reconstruir(raiz, mf, tris, uvs, capa, 2);
            rend.sharedMaterials = mats;
            hechos++;
        }
        Debug.Log("[Patio] parrillas texturizadas: " + hechos);
    }

    private static float Vol(PatioNaturaleza.Pieza p) { Vector3 d = p.Hi - p.Lo; return d.x * d.y * d.z; }

    private static void Olla(PatioNaturaleza.Pieza pz, List<Vector2[]> uvs, List<int> capas, List<PatioNaturaleza.Tri> tris)
    {
        float h = Mathf.Max(1e-3f, pz.Hi.y - pz.Lo.y);
        foreach (var t in pz.T)
        {
            var uv = new Vector2[3]; var ang = new float[3];
            for (int c = 0; c < 3; c++)
            {
                Vector3 d = t.P[c] - pz.Ctr;
                ang[c] = Mathf.Atan2(d.z, -d.x) / (2f * Mathf.PI);   // izquierda a derecha de la pantalla (derecha = -x)
                uv[c].y = Mathf.Clamp((t.P[c].y - pz.Lo.y) / h, 0.01f, 0.99f);
            }
            float mn = Mathf.Min(ang[0], Mathf.Min(ang[1], ang[2])), mx = Mathf.Max(ang[0], Mathf.Max(ang[1], ang[2]));
            for (int c = 0; c < 3; c++) { float a = ang[c]; if (mx - mn > 0.5f && a < 0f) a += 1f; uv[c].x = a; }
            tris.Add(t); uvs.Add(uv); capas.Add(0);
        }
    }

    private static void Pata(PatioNaturaleza.Pieza pz, List<Vector2[]> uvs, List<int> capas, List<PatioNaturaleza.Tri> tris)
    {
        float oU = PatioNaturaleza.Azar(pz.Ctr, 2.1f), oV = PatioNaturaleza.Azar(pz.Ctr, 3.7f);
        foreach (var t in pz.T)
        {
            float ax = Mathf.Abs(t.G.x), ay = Mathf.Abs(t.G.y), az = Mathf.Abs(t.G.z);
            var uv = new Vector2[3];
            for (int c = 0; c < 3; c++)
            {
                Vector3 p = t.P[c] - pz.Ctr;
                Vector2 q = (ay >= ax && ay >= az) ? new Vector2(p.x, p.z) : (ax >= az ? new Vector2(p.z, p.y) : new Vector2(p.x, p.y));
                uv[c] = q / 0.6f + new Vector2(oU, oV);
            }
            tris.Add(t); uvs.Add(uv); capas.Add(1);
        }
    }

    public static void Configurar()
    {
        foreach (string n in NOMBRES)
        {
            string tex = TEX + n + ".png";
            var imp = AssetImporter.GetAtPath(tex) as TextureImporter;
            if (imp == null) { Debug.LogError("[Patio] falta " + tex); continue; }
            imp.wrapMode = TextureWrapMode.Repeat; imp.maxTextureSize = 2048; imp.anisoLevel = 8; imp.SaveAndReimport();
            string ruta = MAT + n + ".mat";
            Material m = AssetDatabase.LoadAssetAtPath<Material>(ruta);
            if (m == null) { m = new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(m, ruta); }
            m.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(tex));
            m.SetTextureScale("_BaseMap", Vector2.one);
            m.SetColor("_BaseColor", Color.white);
            m.SetTexture("_BumpMap", null); m.DisableKeyword("_NORMALMAP");
            m.SetFloat("_Metallic", 0.25f); m.SetFloat("_Smoothness", 0.35f);
            EditorUtility.SetDirty(m);
        }
    }
}
