using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

// Piso del patio y lo que esta tirado encima.
//   piso            losas de concreto de 2.5 x 2.5 m (la segunda fila desfasada), juntas con tierra y pasto, grietas, desportillados, manchas de humedad,
//                   hollin bajo las dos parrillas, ceniza, oxido, hojas secas, tierra y hierba junto a la barda. UNA sola textura de 4096 x 1024 para los 20 x 5 m: no se repite.
//   cuadros_piso    5 baldosas decoradas de 0.5 m (esmalte azul verdoso con rombo, saltado y agrietado).
//   Piedras, pied   piedras de tres tipos (granito gris, arenisca, basalto oscuro) repartidas al azar, con granos, grietas y liquen.
//   Tablas_madera   4 tablones en el suelo, madera vieja con la veta a lo largo y restos de pintura azul / moho.
//   ESC_*           escombros de concreto (ESC_01_metal: caja de lamina oxidada).
internal static class PatioPiso
{
    private const string TEX = "Assets/Patio/Texturas/Patio_";
    private const string MAT = "Assets/Patio/Materiales/Patio_";
    private static readonly string[] NOMBRES = { "piso", "cuadro", "piedra1", "piedra2", "piedra3", "tablon1", "tablon2", "escombro", "metal_oxido" };

    private static Material M(string n) { return AssetDatabase.LoadAssetAtPath<Material>(MAT + n + ".mat"); }

    public static void Aplicar(GameObject raiz)
    {
        int hechos = 0;
        foreach (MeshFilter mf in raiz.GetComponentsInChildren<MeshFilter>())
        {
            string n = mf.gameObject.name.ToLower();
            Renderer rend = mf.GetComponent<Renderer>();
            if (mf.sharedMesh == null || rend == null) continue;
            int tipo = 0; Material[] mats = null;
            if (n == "piso") { tipo = 1; mats = new[] { M("piso") }; }
            else if (n == "cuadros_piso") { tipo = 2; mats = new[] { M("cuadro") }; }
            else if (n == "piedras" || n == "pied") { tipo = 3; mats = new[] { M("piedra1"), M("piedra2"), M("piedra3") }; }
            else if (n == "tablas_madera") { tipo = 4; mats = new[] { M("tablon1"), M("tablon2"), M("testa1") }; }
            else if (n.StartsWith("esc_")) { tipo = 5; mats = new[] { n.EndsWith("metal") ? M("metal_oxido") : M("escombro") }; }
            if (tipo == 0) continue;
            bool ok = true; foreach (Material m in mats) if (m == null) ok = false;
            if (!ok) { Debug.LogError("[Patio] faltan materiales de " + mf.gameObject.name + ", usa el menu Texturizar"); continue; }

            var piezas = PatioNaturaleza.Piezas(raiz, mf);
            var uvs = new List<Vector2[]>(); var capa = new List<int>(); var tris = new List<PatioNaturaleza.Tri>();
            foreach (var pz in piezas)
            {
                if (tipo == 1) Piso(pz, uvs, capa, tris);
                else if (tipo == 2) Baldosa(pz, uvs, capa, tris);
                else if (tipo == 3) Piedra(pz, uvs, capa, tris);
                else if (tipo == 4) Tablon(pz, uvs, capa, tris);
                else Caja(pz, uvs, capa, tris);
            }
            PatioNaturaleza.Reconstruir(raiz, mf, tris, uvs, capa, mats.Length);
            rend.sharedMaterials = mats;
            hechos++;
        }
        Debug.Log("[Patio] piso y objetos del suelo texturizados: " + hechos);
    }

    // piso de 20 x 5 m: x de -10 a 10, z de -2.5 a 2.5. Izquierda de la imagen = izquierda de la pantalla (+x); arriba = el fondo (-z)
    private static void Piso(PatioNaturaleza.Pieza pz, List<Vector2[]> uvs, List<int> capas, List<PatioNaturaleza.Tri> tris)
    {
        foreach (var t in pz.T)
        {
            var uv = new Vector2[3];
            for (int c = 0; c < 3; c++) uv[c] = new Vector2(Mathf.Clamp01((10f - t.P[c].x) / 20f), Mathf.Clamp01((2.5f - t.P[c].z) / 5f));
            tris.Add(t); uvs.Add(uv); capas.Add(0);
        }
    }

    private static void Baldosa(PatioNaturaleza.Pieza pz, List<Vector2[]> uvs, List<int> capas, List<PatioNaturaleza.Tri> tris)
    {
        float ax = Mathf.Max(1e-3f, pz.Hi.x - pz.Lo.x), az = Mathf.Max(1e-3f, pz.Hi.z - pz.Lo.z);
        foreach (var t in pz.T)
        {
            var uv = new Vector2[3]; bool cara = t.G.y > 0.7f;
            for (int c = 0; c < 3; c++)
            {
                Vector3 d = t.P[c] - pz.Ctr;
                uv[c] = cara ? new Vector2(Mathf.Clamp01(-d.x / ax + 0.5f), Mathf.Clamp01(-d.z / az + 0.5f)) : new Vector2(0.03f, 0.5f);
            }
            tris.Add(t); uvs.Add(uv); capas.Add(0);
        }
    }

    // piedras: proyeccion por caras (0.25 m por repeticion), un tipo de piedra por piedra
    private static void Piedra(PatioNaturaleza.Pieza pz, List<Vector2[]> uvs, List<int> capas, List<PatioNaturaleza.Tri> tris)
    {
        int tipo = Mathf.Min(2, (int)(PatioNaturaleza.Azar(pz.Ctr, 1f) * 3f));
        float oU = PatioNaturaleza.Azar(pz.Ctr, 2.1f), oV = PatioNaturaleza.Azar(pz.Ctr, 3.7f);
        foreach (var t in pz.T)
        {
            Vector3 g = (t.N[0] + t.N[1] + t.N[2]).normalized;
            float ax = Mathf.Abs(g.x), ay = Mathf.Abs(g.y), az = Mathf.Abs(g.z);
            var uv = new Vector2[3];
            for (int c = 0; c < 3; c++)
            {
                Vector3 p = t.P[c] - pz.Ctr;
                Vector2 q = (ay >= ax && ay >= az) ? new Vector2(p.x, p.z) : (ax >= az ? new Vector2(p.z, p.y) : new Vector2(p.x, p.y));
                uv[c] = q / 0.25f + new Vector2(oU, oV);
            }
            tris.Add(t); uvs.Add(uv); capas.Add(tipo);
        }
    }

    // tablones: veta a lo largo de la pieza, testa en las puntas
    private static void Tablon(PatioNaturaleza.Pieza pz, List<Vector2[]> uvs, List<int> capas, List<PatioNaturaleza.Tri> tris)
    {
        int v = PatioNaturaleza.Azar(pz.Ctr, 1f) < 0.5f ? 0 : 1;
        float oU = PatioNaturaleza.Azar(pz.Ctr, 2.1f), oV = PatioNaturaleza.Azar(pz.Ctr, 3.7f);
        foreach (var t in pz.T)
        {
            float a1 = Mathf.Abs(Vector3.Dot(t.G, pz.E1)), a2 = Mathf.Abs(Vector3.Dot(t.G, pz.E2)), a3 = Mathf.Abs(Vector3.Dot(t.G, pz.E3));
            bool punta = a1 >= a2 && a1 >= a3;
            var uv = new Vector2[3];
            for (int c = 0; c < 3; c++)
            {
                Vector3 d = t.P[c] - pz.Ctr;
                float x1 = Vector3.Dot(d, pz.E1), x2 = Vector3.Dot(d, pz.E2), x3 = Vector3.Dot(d, pz.E3);
                if (punta) uv[c] = new Vector2(x2 * 2f + oU, x3 * 2f + oV);
                else if (a2 >= a3) uv[c] = new Vector2(x1 + oU, x3 + oV);
                else uv[c] = new Vector2(x1 + oU, x2 + oV);
            }
            tris.Add(t); uvs.Add(uv); capas.Add(punta ? 2 : v);
        }
    }

    // cajas de escombro: proyeccion por caras (0.6 m por repeticion)
    private static void Caja(PatioNaturaleza.Pieza pz, List<Vector2[]> uvs, List<int> capas, List<PatioNaturaleza.Tri> tris)
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
            tris.Add(t); uvs.Add(uv); capas.Add(0);
        }
    }

    public static void Configurar()
    {
        foreach (string n in NOMBRES)
        {
            string tex = TEX + n + ".png";
            var imp = AssetImporter.GetAtPath(tex) as TextureImporter;
            if (imp == null) { Debug.LogError("[Patio] falta " + tex); continue; }
            imp.wrapMode = (n == "piso" || n == "cuadro") ? TextureWrapMode.Clamp : TextureWrapMode.Repeat;
            imp.maxTextureSize = n == "piso" ? 4096 : 2048; imp.anisoLevel = 8; imp.SaveAndReimport();
            string ruta = MAT + n + ".mat";
            Material m = AssetDatabase.LoadAssetAtPath<Material>(ruta);
            if (m == null) { m = new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(m, ruta); }
            m.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(tex));
            m.SetTextureScale("_BaseMap", Vector2.one);
            m.SetColor("_BaseColor", Color.white);
            m.SetTexture("_BumpMap", null); m.DisableKeyword("_NORMALMAP");
            m.SetFloat("_Metallic", n == "metal_oxido" ? 0.4f : 0f);
            m.SetFloat("_Smoothness", n == "metal_oxido" ? 0.25f : n == "cuadro" ? 0.35f : 0.07f);
            EditorUtility.SetDirty(m);
        }
    }
}
