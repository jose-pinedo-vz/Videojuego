using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

// Estantes de madera de la cochera: las tablas largas "tubos", "tubos.001" ... "tubos.019" (no confundir con "Tuberia1").
// Madera vieja y deteriorada: tablones grises y resecos, grietas largas, nudos, pintura azul verdosa descascarada, aureolas de humedad,
// moho, quemaduras, clavos oxidados con chorreos y astillas. Cada tabla usa 3 materiales (3 submallas):
//   arriba  (Cochera_estante_top)   superficie desgastada y sucia
//   abajo   (Cochera_estante_bajo)  cara de abajo oscura, con moho, telaranas y clavos
//   canto   (Cochera_estante_canto) borde y puntas de la tabla, con golpes y astillas
// Las texturas se repiten cada 2.0 m a lo largo de la tabla y cada 1.8 m a lo ancho (6 tablones de 30 cm); cada tabla tiene un desplazamiento distinto.
internal static class CocheraEstantes
{
    private const string TEX = "Assets/Cochera/Texturas/Cochera_estante_";
    private const string MAT = "Assets/Cochera/Materiales/Cochera_estante_";
    private const float TILE_U = 2.0f; // m por repeticion a lo largo
    private const float TILE_V = 1.8f; // m por repeticion a lo ancho
    private static readonly string[] CAPAS = { "top", "bajo", "canto" };

    private static bool EsEstante(string nombre) { return nombre.ToLower().StartsWith("tubos"); }

    public static void Aplicar(GameObject raiz)
    {
        Material[] mats3 = new Material[3];
        for (int i = 0; i < 3; i++) mats3[i] = AssetDatabase.LoadAssetAtPath<Material>(MAT + CAPAS[i] + ".mat");
        int hechos = 0;
        foreach (MeshFilter mf in raiz.GetComponentsInChildren<MeshFilter>())
        {
            if (!EsEstante(mf.gameObject.name)) continue;
            Mesh mesh = mf.sharedMesh;
            Renderer rend = mf.GetComponent<Renderer>();
            if (mesh == null || rend == null) continue;

            Vector3[] vert = mesh.vertices;
            Vector3[] nor = mesh.normals;
            bool hayNor = nor != null && nor.Length == vert.Length;

            // triangulos en el espacio del modelo (1 unidad = 1 m) y caja que envuelve la pieza
            var tris = new List<int[]>(); var P = new List<Vector3[]>(); var G = new List<Vector3>();
            Vector3 lo = new Vector3(1e9f, 1e9f, 1e9f), hi = new Vector3(-1e9f, -1e9f, -1e9f);
            for (int s = 0; s < mesh.subMeshCount; s++)
            {
                int[] t = mesh.GetTriangles(s);
                for (int i = 0; i + 2 < t.Length; i += 3)
                {
                    var p = new Vector3[3];
                    for (int c = 0; c < 3; c++)
                    {
                        p[c] = raiz.transform.InverseTransformPoint(mf.transform.TransformPoint(vert[t[i + c]]));
                        lo = Vector3.Min(lo, p[c]); hi = Vector3.Max(hi, p[c]);
                    }
                    Vector3 g = Vector3.Cross(p[1] - p[0], p[2] - p[0]).normalized;
                    if (hayNor)
                    {
                        Vector3 nm = nor[t[i]] + nor[t[i + 1]] + nor[t[i + 2]];
                        nm = raiz.transform.InverseTransformDirection(mf.transform.TransformDirection(nm));
                        if (nm.sqrMagnitude > 1e-6f && Vector3.Dot(g, nm) < 0f) g = -g; // la cara mira hacia donde apunta la normal de la malla
                    }
                    P.Add(p); G.Add(g);
                }
            }
            Vector3 sz = hi - lo;
            int fino = 0, largo = 0;
            for (int a = 1; a < 3; a++) { if (sz[a] < sz[fino]) fino = a; if (sz[a] > sz[largo]) largo = a; }
            if (fino == largo) largo = (fino + 1) % 3;
            int medio = 3 - fino - largo;
            float grosor = Mathf.Max(1e-4f, sz[fino]);
            Vector3 ctr = (lo + hi) * 0.5f;
            float off = Mathf.Abs(Mathf.Sin(ctr.x * 12.9898f + ctr.y * 78.233f + ctr.z * 37.719f) * 43758.5453f) % 1f;
            float off2 = (off * 7.31f) % 1f;

            var pos = new List<Vector3>(); var nrm = new List<Vector3>(); var uv = new List<Vector2>();
            var sub = new List<int>[3] { new List<int>(), new List<int>(), new List<int>() };
            for (int k = 0; k < P.Count; k++)
            {
                Vector3 g = G[k];
                int eje = 0; float mx = Mathf.Abs(g.x);
                if (Mathf.Abs(g.y) > mx) { eje = 1; mx = Mathf.Abs(g.y); }
                if (Mathf.Abs(g.z) > mx) { eje = 2; }
                int capa; int a1 = largo;
                if (eje == fino) capa = g[fino] > 0f ? 0 : 1;      // cara grande: arriba o abajo
                else { capa = 2; a1 = (eje == largo) ? medio : largo; } // canto
                for (int c = 0; c < 3; c++)
                {
                    Vector3 p = P[k][c];
                    Vector2 r;
                    if (capa == 2) r = new Vector2((p[a1] - lo[a1]) / TILE_U + off, Mathf.Clamp((p[fino] - lo[fino]) / grosor, 0.02f, 0.98f));
                    else r = new Vector2((p[largo] - lo[largo]) / TILE_U + off, (p[medio] - lo[medio]) / TILE_V + off2);
                    sub[capa].Add(pos.Count);
                    pos.Add(mf.transform.InverseTransformPoint(raiz.transform.TransformPoint(p)));
                    nrm.Add(mf.transform.InverseTransformDirection(raiz.transform.TransformDirection(g)).normalized);
                    uv.Add(r);
                }
            }
            mesh.Clear();
            mesh.indexFormat = pos.Count > 65000 ? UnityEngine.Rendering.IndexFormat.UInt32 : UnityEngine.Rendering.IndexFormat.UInt16;
            mesh.SetVertices(pos);
            mesh.SetNormals(nrm);
            mesh.SetUVs(0, uv);
            mesh.subMeshCount = 3;
            for (int s = 0; s < 3; s++) mesh.SetTriangles(sub[s], s);
            mesh.RecalculateBounds();
            mesh.RecalculateTangents();

            if (mats3[0] != null && mats3[1] != null && mats3[2] != null) rend.sharedMaterials = mats3;
            CocheraTexturizador.Tratados.Add(rend);
            hechos++;
        }
        Debug.Log("[Cochera] estantes de madera (tubos) texturizados: " + hechos);
    }

    public static void Configurar()
    {
        foreach (string capa in CAPAS)
        {
            string tex = TEX + capa + ".png";
            var imp = AssetImporter.GetAtPath(tex) as TextureImporter;
            if (imp == null) { Debug.LogError("[Cochera] falta " + tex); continue; }
            imp.wrapMode = TextureWrapMode.Repeat; imp.maxTextureSize = 2048; imp.anisoLevel = 8; imp.SaveAndReimport();
            string ruta = MAT + capa + ".mat";
            Material m = AssetDatabase.LoadAssetAtPath<Material>(ruta);
            if (m == null) { m = new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(m, ruta); }
            m.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(tex));
            m.SetTextureScale("_BaseMap", Vector2.one); // la repeticion va en las UV
            m.SetColor("_BaseColor", Color.white);
            m.SetTexture("_BumpMap", null); m.DisableKeyword("_NORMALMAP");
            m.SetFloat("_Metallic", 0f);
            m.SetFloat("_Smoothness", capa == "top" ? 0.1f : 0.05f);
            EditorUtility.SetDirty(m);
        }
    }
}
