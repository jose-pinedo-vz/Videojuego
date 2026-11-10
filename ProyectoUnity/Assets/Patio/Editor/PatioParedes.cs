using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

// Paredes laterales del patio (Paredizq a la izquierda de la pantalla, Paredder a la derecha).
// Estilo pintado como el fondo (Pared): tablas traslapadas con contorno de tinta, colores apagados y oscuros del fondo (rojo apagado a la izquierda, azul pizarra a la derecha),
//   pintura descascarada que deja ver la madera gris, humedad y musgo en la base, chorreones de hollin, grietas y clavos. Textura unica de 1536 x 3072 que cubre los 5 x 10 m.
// Las otras caras (afuera, arriba, cantos) llevan un repellado liso envejecido (Patio_pared_lado).
// No toca la pared del fondo ("Pared") ni el techo.
internal static class PatioParedes
{
    private const string TEX = "Assets/Patio/Texturas/Patio_pared_";
    private const string MAT = "Assets/Patio/Materiales/Patio_pared_";
    private static readonly string[] NOMBRES = { "izq", "der", "lado" };

    private static Material M(string n) { return AssetDatabase.LoadAssetAtPath<Material>(MAT + n + ".mat"); }

    public static void Aplicar(GameObject raiz)
    {
        int hechos = 0;
        foreach (MeshFilter mf in raiz.GetComponentsInChildren<MeshFilter>())
        {
            string n = mf.gameObject.name.ToLower();
            Renderer rend = mf.GetComponent<Renderer>();
            if (mf.sharedMesh == null || rend == null) continue;
            if (n != "paredizq" && n != "paredder") continue;
            Material[] mats = { M(n == "paredizq" ? "izq" : "der"), M("lado") };
            if (mats[0] == null || mats[1] == null) { Debug.LogError("[Patio] faltan materiales de " + mf.gameObject.name + ", usa el menu Texturizar"); continue; }
            var piezas = PatioNaturaleza.Piezas(raiz, mf);
            var uvs = new List<Vector2[]>(); var capa = new List<int>(); var tris = new List<PatioNaturaleza.Tri>();
            foreach (var pz in piezas) Pared(pz, uvs, capa, tris);
            PatioNaturaleza.Reconstruir(raiz, mf, tris, uvs, capa, 2);
            rend.sharedMaterials = mats;
            hechos++;
        }
        Debug.Log("[Patio] paredes laterales texturizadas: " + hechos);
    }

    private static void Pared(PatioNaturaleza.Pieza pz, List<Vector2[]> uvs, List<int> capas, List<PatioNaturaleza.Tri> tris)
    {
        // la cara de adentro mira al centro del patio: normal +x si la pared esta en x<0, -x si esta en x>0
        float signo = pz.Ctr.x < 0f ? 1f : -1f;
        Vector3 mira = new Vector3(-signo, 0f, 0f);                 // hacia donde mira quien ve la pared desde adentro
        Vector3 derecha = Vector3.Cross(Vector3.up, mira);          // su derecha
        float L = Mathf.Max(1e-3f, pz.Hi.z - pz.Lo.z), Hh = Mathf.Max(1e-3f, pz.Hi.y - pz.Lo.y);
        float oU = PatioNaturaleza.Azar(pz.Ctr, 1f), oV = PatioNaturaleza.Azar(pz.Ctr, 2.7f);
        foreach (var t in pz.T)
        {
            var uv = new Vector2[3]; bool dentro = t.G.x * signo > 0.7f;
            for (int c = 0; c < 3; c++)
            {
                Vector3 d = t.P[c] - pz.Ctr;
                if (dentro) uv[c] = new Vector2(Mathf.Clamp01(Vector3.Dot(d, derecha) / L + 0.5f), Mathf.Clamp01((t.P[c].y - pz.Lo.y) / Hh));
                else
                {
                    float ax = Mathf.Abs(t.G.x), ay = Mathf.Abs(t.G.y), az = Mathf.Abs(t.G.z);
                    Vector2 q = (ay >= ax && ay >= az) ? new Vector2(d.x, d.z) : (ax >= az ? new Vector2(d.z, d.y) : new Vector2(d.x, d.y));
                    uv[c] = q / 2f + new Vector2(oU, oV);
                }
            }
            tris.Add(t); uvs.Add(uv); capas.Add(dentro ? 0 : 1);
        }
    }

    public static void Configurar()
    {
        foreach (string n in NOMBRES)
        {
            string tex = TEX + n + ".png";
            var imp = AssetImporter.GetAtPath(tex) as TextureImporter;
            if (imp == null) { Debug.LogError("[Patio] falta " + tex); continue; }
            imp.wrapMode = n == "lado" ? TextureWrapMode.Repeat : TextureWrapMode.Clamp;
            imp.maxTextureSize = n == "lado" ? 2048 : 4096; imp.anisoLevel = 8; imp.SaveAndReimport();
            string ruta = MAT + n + ".mat";
            Material m = AssetDatabase.LoadAssetAtPath<Material>(ruta);
            if (m == null) { m = new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(m, ruta); }
            m.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(tex));
            m.SetTextureScale("_BaseMap", Vector2.one);
            m.SetColor("_BaseColor", Color.white);
            m.SetTexture("_BumpMap", null); m.DisableKeyword("_NORMALMAP");
            m.SetFloat("_Metallic", 0f); m.SetFloat("_Smoothness", 0.04f);
            EditorUtility.SetDirty(m);
        }
    }
}
