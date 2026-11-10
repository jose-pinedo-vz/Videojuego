using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

// Cajas de madera del patio (cajamadera1.001 ... cajamadera1.009): 7 cajas, cada una con su propio diseno.
// Cada caja esta hecha de 6 piezas: el cuerpo (el cajon), el panel del frente y 4 listones del marco.
//   panel del frente -> Patio_cajaN_panel   tablas, riostras en X con clavos, letreros pintados con plantilla, etiquetas, cinta, sellos (cada caja distinta)
//   cuerpo y marco   -> Patio_cajaN_madera  madera con la veta a lo largo de cada pieza (pintada o al natural, segun la caja) + Patio_testaN en las puntas
// Cajas: 1 pino natural "FRAGIL" - 2 oscura y verde "No. 17" - 3 verde olivo "REPUESTOS" con cinta - 4 etiqueta "TOMATES" - 5 azul "PARTES" con aviso - 6 "HERRAMIENTAS" con sello - 7 quemada "INFLAMABLE".
internal static class PatioCajas
{
    private const string TEX = "Assets/Patio/Texturas/Patio_";
    private const string MAT = "Assets/Patio/Materiales/Patio_";
    private static readonly string[] SUFIJOS = { "001", "003", "004", "005", "007", "008", "009" };

    private static Material M(string n) { return AssetDatabase.LoadAssetAtPath<Material>(MAT + n + ".mat"); }

    public static void Aplicar(GameObject raiz)
    {
        int hechos = 0;
        foreach (MeshFilter mf in raiz.GetComponentsInChildren<MeshFilter>())
        {
            string n = mf.gameObject.name.ToLower();
            Renderer rend = mf.GetComponent<Renderer>();
            if (mf.sharedMesh == null || rend == null || !n.StartsWith("cajamadera")) continue;
            int k = -1;
            for (int i = 0; i < SUFIJOS.Length; i++) if (n.EndsWith(SUFIJOS[i])) k = i;
            if (k < 0) k = Mathf.Abs(n.GetHashCode()) % SUFIJOS.Length;
            int caja = k + 1;
            var mats = new[] { M("caja" + caja + "_madera"), M("testa" + ((caja - 1) % 3 + 1)), M("caja" + caja + "_panel") };
            bool ok = true; foreach (Material m in mats) if (m == null) ok = false;
            if (!ok) { Debug.LogError("[Patio] faltan materiales de " + mf.gameObject.name + ", usa el menu Texturizar"); continue; }

            List<PatioNaturaleza.Pieza> piezas = PatioNaturaleza.Piezas(raiz, mf);
            // volumen de cada pieza: la mayor es el cuerpo, la segunda el panel del frente
            piezas.Sort((a, b) => Vol(b).CompareTo(Vol(a)));
            var uvs = new List<Vector2[]>(); var capa = new List<int>(); var tris = new List<PatioNaturaleza.Tri>();
            for (int i = 0; i < piezas.Count; i++) Pieza(piezas[i], i == 1, uvs, capa, tris);
            PatioNaturaleza.Reconstruir(raiz, mf, tris, uvs, capa, 3);
            rend.sharedMaterials = mats;
            hechos++;
        }
        Debug.Log("[Patio] cajas de madera texturizadas: " + hechos);
    }

    private static float Vol(PatioNaturaleza.Pieza p) { Vector3 d = p.Hi - p.Lo; return d.x * d.y * d.z; }

    private static void Pieza(PatioNaturaleza.Pieza pz, bool esPanel, List<Vector2[]> uvs, List<int> capas, List<PatioNaturaleza.Tri> tris)
    {
        float offU = PatioNaturaleza.Azar(pz.Ctr, 1f), offV = PatioNaturaleza.Azar(pz.Ctr, 2.7f);
        float ax = Mathf.Max(1e-3f, pz.Hi.x - pz.Lo.x), ay = Mathf.Max(1e-3f, pz.Hi.y - pz.Lo.y);
        foreach (PatioNaturaleza.Tri t in pz.T)
        {
            var uv = new Vector2[3]; int capa;
            if (esPanel && t.G.z > 0.7f)
            {   // cara de frente (mira a la camara): la textura entera cubre el panel; derecha de la pantalla = -x
                for (int c = 0; c < 3; c++)
                {
                    Vector3 d = t.P[c] - pz.Ctr;
                    uv[c] = new Vector2(Mathf.Clamp01(-d.x / ax + 0.5f), Mathf.Clamp01(d.y / ay + 0.5f));
                }
                capa = 2;
            }
            else
            {   // madera: veta a lo largo de la pieza (E1), testa en las puntas
                float a1 = Mathf.Abs(Vector3.Dot(t.G, pz.E1)), a2 = Mathf.Abs(Vector3.Dot(t.G, pz.E2)), a3 = Mathf.Abs(Vector3.Dot(t.G, pz.E3));
                bool punta = a1 >= a2 && a1 >= a3;
                for (int c = 0; c < 3; c++)
                {
                    Vector3 d = t.P[c] - pz.Ctr;
                    float x1 = Vector3.Dot(d, pz.E1), x2 = Vector3.Dot(d, pz.E2), x3 = Vector3.Dot(d, pz.E3);
                    if (punta) uv[c] = new Vector2(x2 * 2f + offU, x3 * 2f + offV);
                    else if (a2 >= a3) uv[c] = new Vector2(x1 + offU, x3 + offV);
                    else uv[c] = new Vector2(x1 + offU, x2 + offV);
                }
                capa = punta ? 1 : 0;
            }
            tris.Add(t); uvs.Add(uv); capas.Add(capa);
        }
    }

    public static void Configurar()
    {
        var nombres = new List<string>();
        for (int i = 1; i <= 7; i++) { nombres.Add("caja" + i + "_madera"); nombres.Add("caja" + i + "_panel"); }
        for (int i = 1; i <= 3; i++) nombres.Add("testa" + i);
        foreach (string n in nombres)
        {
            string tex = TEX + n + ".png";
            var imp = AssetImporter.GetAtPath(tex) as TextureImporter;
            if (imp == null) { Debug.LogError("[Patio] falta " + tex); continue; }
            imp.wrapMode = n.EndsWith("_panel") ? TextureWrapMode.Clamp : TextureWrapMode.Repeat;
            imp.maxTextureSize = 2048; imp.anisoLevel = 8; imp.SaveAndReimport();
            string ruta = MAT + n + ".mat";
            Material m = AssetDatabase.LoadAssetAtPath<Material>(ruta);
            if (m == null) { m = new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(m, ruta); }
            m.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(tex));
            m.SetTextureScale("_BaseMap", Vector2.one);
            m.SetColor("_BaseColor", Color.white);
            m.SetTexture("_BumpMap", null); m.DisableKeyword("_NORMALMAP");
            m.SetFloat("_Metallic", 0f);
            m.SetFloat("_Smoothness", n.EndsWith("_panel") ? 0.1f : 0.07f);
            EditorUtility.SetDirty(m);
        }
    }
}
