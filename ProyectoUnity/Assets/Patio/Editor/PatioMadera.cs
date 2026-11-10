using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

// Muebles de madera del patio: banca, mesa, escalera y casa del perro.
//   Banca        asiento y patas de madera oscura con pintura verde azulada descascarada y moho
//   mesa.001     tablero con pintura azul grisacea saltada + 4 patas con otro desgaste
//   Escalera     2 largueros y 5 peldanos de madera vieja con salpicaduras de pintura y clavos oxidados; los peldanos tienen desgaste en el centro
//   Casa_perro   cuerpo pintado rojo granero, descascarado; el frente tiene la puerta en arco con marco, letrero "ROCKY", aranazos y esquina mordisqueada;
//                el techo lleva tejas de asfalto con musgo, tejas faltantes y chorreones.
// Cada pieza (tabla, pata, peldano) se mapea con su propio eje (veta a lo largo) y tiene un desplazamiento distinto. Las puntas usan las texturas de testa Patio_testa1..3.
internal static class PatioMadera
{
    private const string TEX = "Assets/Patio/Texturas/Patio_";
    private const string MAT = "Assets/Patio/Materiales/Patio_";
    private static readonly string[] NOMBRES = { "banca", "mesa", "mesa_pata", "escalera", "casa_madera", "casa_frente", "casa_techo", "testa1", "testa2", "testa3" };

    private static Material M(string n) { return AssetDatabase.LoadAssetAtPath<Material>(MAT + n + ".mat"); }

    public static void Aplicar(GameObject raiz)
    {
        int hechos = 0;
        foreach (MeshFilter mf in raiz.GetComponentsInChildren<MeshFilter>())
        {
            string n = mf.gameObject.name.ToLower();
            Renderer rend = mf.GetComponent<Renderer>();
            if (mf.sharedMesh == null || rend == null) continue;
            Material[] mats = null; int tipo = 0;
            if (n == "banca") { mats = new[] { M("banca"), M("testa2") }; tipo = 1; }
            else if (n == "mesa.001") { mats = new[] { M("mesa"), M("mesa_pata"), M("testa1") }; tipo = 2; }
            else if (n == "escalera") { mats = new[] { M("escalera"), M("testa3") }; tipo = 1; }
            else if (n == "casa_perro") { mats = new[] { M("casa_madera"), M("testa1"), M("casa_techo"), M("casa_frente") }; tipo = 3; }
            if (tipo == 0) continue;
            bool ok = true; foreach (Material m in mats) if (m == null) ok = false;
            if (!ok) { Debug.LogError("[Patio] faltan materiales de " + mf.gameObject.name + ", usa el menu Texturizar"); continue; }
            var piezas = PatioNaturaleza.Piezas(raiz, mf);
            var uvs = new List<Vector2[]>(); var capa = new List<int>(); var tris = new List<PatioNaturaleza.Tri>();
            if (tipo == 1) foreach (var pz in piezas) Madera(pz, 0, 1, uvs, capa, tris);
            else if (tipo == 2)
            {
                piezas.Sort((a, b) => Vol(b).CompareTo(Vol(a)));
                for (int i = 0; i < piezas.Count; i++) Madera(piezas[i], i == 0 ? 0 : 1, 2, uvs, capa, tris);
            }
            else
            {
                piezas.Sort((a, b) => b.Ctr.y.CompareTo(a.Ctr.y)); // la primera es el techo
                for (int i = 0; i < piezas.Count; i++) { if (i == 0) Techo(piezas[i], uvs, capa, tris); else Casa(piezas[i], uvs, capa, tris); }
            }
            PatioNaturaleza.Reconstruir(raiz, mf, tris, uvs, capa, mats.Length);
            rend.sharedMaterials = mats;
            hechos++;
        }
        Debug.Log("[Patio] banca, mesa, escalera y casa del perro texturizados: " + hechos);
    }

    private static float Vol(PatioNaturaleza.Pieza p) { Vector3 d = p.Hi - p.Lo; return d.x * d.y * d.z; }

    // madera con la veta a lo largo de la pieza (E1); capaLado / capaTesta son los indices de submalla
    private static void Madera(PatioNaturaleza.Pieza pz, int capaLado, int capaTesta, List<Vector2[]> uvs, List<int> capas, List<PatioNaturaleza.Tri> tris)
    {
        float oU = PatioNaturaleza.Azar(pz.Ctr, 1f), oV = PatioNaturaleza.Azar(pz.Ctr, 2.7f);
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
            tris.Add(t); uvs.Add(uv); capas.Add(punta ? capaTesta : capaLado);
        }
    }

    // cuerpo de la casa: el frente (mira a la camara) lleva la textura con la puerta; el resto, madera pintada
    private static void Casa(PatioNaturaleza.Pieza pz, List<Vector2[]> uvs, List<int> capas, List<PatioNaturaleza.Tri> tris)
    {
        float ax = Mathf.Max(1e-3f, pz.Hi.x - pz.Lo.x), ay = Mathf.Max(1e-3f, pz.Hi.y - pz.Lo.y);
        float oU = PatioNaturaleza.Azar(pz.Ctr, 1f), oV = PatioNaturaleza.Azar(pz.Ctr, 2.7f);
        foreach (var t in pz.T)
        {
            var uv = new Vector2[3]; int capa;
            if (t.G.z > 0.7f)
            {
                for (int c = 0; c < 3; c++)
                {
                    Vector3 d = t.P[c] - pz.Ctr;
                    uv[c] = new Vector2(Mathf.Clamp01(-d.x / ax + 0.5f), Mathf.Clamp01(d.y / ay + 0.5f));
                }
                capa = 3;
            }
            else
            {
                for (int c = 0; c < 3; c++)
                {
                    Vector3 d = t.P[c] - pz.Ctr;
                    float ang = Mathf.Abs(t.G.x) > Mathf.Abs(t.G.y) ? d.z : d.x;
                    uv[c] = new Vector2(ang + oU, d.y + oV);
                }
                capa = 0;
            }
            tris.Add(t); uvs.Add(uv); capas.Add(capa);
        }
    }

    // techo: las caras de arriba llevan tejas (1 m por repeticion); los lados, madera
    private static void Techo(PatioNaturaleza.Pieza pz, List<Vector2[]> uvs, List<int> capas, List<PatioNaturaleza.Tri> tris)
    {
        float oU = PatioNaturaleza.Azar(pz.Ctr, 1f), oV = PatioNaturaleza.Azar(pz.Ctr, 2.7f);
        foreach (var t in pz.T)
        {
            var uv = new Vector2[3]; bool arriba = t.G.y > 0.3f;
            for (int c = 0; c < 3; c++)
            {
                Vector3 d = t.P[c] - pz.Ctr;
                uv[c] = arriba ? new Vector2(-d.x + oU, d.z + oV) : new Vector2((Mathf.Abs(t.G.x) > Mathf.Abs(t.G.z) ? d.z : d.x) + oU, d.y + oV);
            }
            tris.Add(t); uvs.Add(uv); capas.Add(arriba ? 2 : 0);
        }
    }

    public static void Configurar()
    {
        foreach (string n in NOMBRES)
        {
            string tex = TEX + n + ".png";
            var imp = AssetImporter.GetAtPath(tex) as TextureImporter;
            if (imp == null) { Debug.LogError("[Patio] falta " + tex); continue; }
            imp.wrapMode = n == "casa_frente" ? TextureWrapMode.Clamp : TextureWrapMode.Repeat;
            imp.maxTextureSize = 2048; imp.anisoLevel = 8; imp.SaveAndReimport();
            string ruta = MAT + n + ".mat";
            Material m = AssetDatabase.LoadAssetAtPath<Material>(ruta);
            if (m == null) { m = new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(m, ruta); }
            m.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(tex));
            m.SetTextureScale("_BaseMap", Vector2.one);
            m.SetColor("_BaseColor", Color.white);
            m.SetTexture("_BumpMap", null); m.DisableKeyword("_NORMALMAP");
            m.SetFloat("_Metallic", 0f); m.SetFloat("_Smoothness", n == "casa_techo" ? 0.05f : 0.09f);
            EditorUtility.SetDirty(m);
        }
    }
}
