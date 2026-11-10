using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

// Nubes, arboles y arbusto del patio.
//   Nubes   (Nube.001 .. Nube.005): cada nube es un grupo de bolas; textura de nube con degradado (abajo gris azulado con reflejo calido del fuego, arriba blanca)
//           y relieve de algodon. Dos variantes: A (blanco calido) y B (tormenta gris azulada).
//   Arboles (Arbol, Arbol2, Arbol3) y Arbusto: las copas (esferas) llevan follaje de hojas individuales y los troncos/ramas corteza con surcos, nudos, musgo y liquen.
//           Cada arbol tiene su propio verde y su propia corteza; el arbusto lleva frutitos rojos.
// Cada bola de la nube y cada copa recibe un desplazamiento distinto para que no se repita el mismo dibujo.
internal static class PatioNaturaleza
{
    private const string TEX = "Assets/Patio/Texturas/Patio_";
    private const string MAT = "Assets/Patio/Materiales/Patio_";

    // nombre, brillo
    private static readonly string[] NOMBRES = { "nube_a", "nube_b", "hojas_a", "hojas_b", "hojas_c", "hojas_d", "corteza_a", "corteza_b", "corteza_c", "corteza_d" };

    internal class Tri { public Vector3[] P = new Vector3[3]; public Vector3[] N = new Vector3[3]; public Vector3 G; }
    internal class Pieza { public Vector3 Ctr, E1, E2, E3, Lo, Hi; public List<Tri> T = new List<Tri>(); }

    public static bool EsDeEstaTanda(string nombre)
    {
        string n = nombre.ToLower();
        return n == "paredder" || n == "paredizq" || n == "banca" || n == "mesa.001" || n == "escalera" || n == "casa_perro" || n.StartsWith("parrilla") || n == "piso" || n == "cuadros_piso" || n == "piedras" || n == "pied" || n == "tablas_madera" || n.StartsWith("esc_") || n.StartsWith("cajamadera") || n.StartsWith("barda") || n == "arbol" || n == "arbol2" || n == "arbol3" || n == "arbusto" || n.StartsWith("nube");
    }

    private static Material M(string n) { return AssetDatabase.LoadAssetAtPath<Material>(MAT + n + ".mat"); }

    public static void Aplicar(GameObject raiz)
    {
        int hechos = 0;
        foreach (MeshFilter mf in raiz.GetComponentsInChildren<MeshFilter>())
        {
            string n = mf.gameObject.name.ToLower();
            Renderer rend = mf.GetComponent<Renderer>();
            if (mf.sharedMesh == null || rend == null || !EsDeEstaTanda(n)) continue;
            Material[] mats; int tipo;
            if (n.StartsWith("nube"))
            {
                bool a = n.EndsWith("001") || n.EndsWith("003");
                mats = new[] { M(a ? "nube_a" : "nube_b") }; tipo = 1;
            }
            else
            {
                string v = n == "arbol" ? "a" : n == "arbol2" ? "b" : n == "arbol3" ? "c" : "d";
                mats = new[] { M("hojas_" + v), M("corteza_" + v) }; tipo = 2;
            }
            bool ok = true; foreach (Material m in mats) if (m == null) ok = false;
            if (!ok) { Debug.LogError("[Patio] faltan materiales de " + mf.gameObject.name + ", usa el menu Texturizar nubes, arboles y arbusto"); continue; }

            List<Pieza> piezas = Piezas(raiz, mf);
            Vector3 lo = new Vector3(1e9f, 1e9f, 1e9f), hi = new Vector3(-1e9f, -1e9f, -1e9f);
            foreach (Pieza p in piezas) { lo = Vector3.Min(lo, p.Lo); hi = Vector3.Max(hi, p.Hi); }
            var uvs = new List<Vector2[]>(); var capa = new List<int>(); var tris = new List<Tri>();
            foreach (Pieza pz in piezas)
            {
                if (tipo == 1) Nube(pz, lo, hi, uvs, capa, tris);
                else if (pz.T.Count >= 700) Copa(pz, uvs, capa, tris);
                else Tronco(pz, uvs, capa, tris);
            }
            Reconstruir(raiz, mf, tris, uvs, capa, mats.Length);
            rend.sharedMaterials = mats;
            hechos++;
        }
        Debug.Log("[Patio] nubes, arboles y arbusto texturizados: " + hechos + " objetos");
    }

    // ---------- piezas (componentes conexas) ----------
    internal static List<Pieza> Piezas(GameObject raiz, MeshFilter mf)
    {
        Mesh mesh = mf.sharedMesh;
        Vector3[] vert = mesh.vertices; Vector3[] nor = mesh.normals;
        bool hayNor = nor != null && nor.Length == vert.Length;
        var todos = new List<Tri>(); var keys = new List<Vector3Int[]>();
        for (int s = 0; s < mesh.subMeshCount; s++)
        {
            int[] t = mesh.GetTriangles(s);
            for (int i = 0; i + 2 < t.Length; i += 3)
            {
                var tr = new Tri(); var k = new Vector3Int[3];
                for (int c = 0; c < 3; c++)
                {
                    tr.P[c] = raiz.transform.InverseTransformPoint(mf.transform.TransformPoint(vert[t[i + c]]));
                    k[c] = new Vector3Int(Mathf.RoundToInt(tr.P[c].x * 500f), Mathf.RoundToInt(tr.P[c].y * 500f), Mathf.RoundToInt(tr.P[c].z * 500f));
                }
                Vector3 g = Vector3.Cross(tr.P[1] - tr.P[0], tr.P[2] - tr.P[0]);
                if (g.sqrMagnitude < 1e-14f) continue;
                g.Normalize();
                Vector3 nm = Vector3.zero;
                for (int c = 0; c < 3; c++)
                {
                    tr.N[c] = g;
                    if (hayNor)
                    {
                        Vector3 nv = raiz.transform.InverseTransformDirection(mf.transform.TransformDirection(nor[t[i + c]]));
                        if (nv.sqrMagnitude > 1e-8f) tr.N[c] = nv.normalized;
                        nm += nv;
                    }
                }
                if (hayNor && nm.sqrMagnitude > 1e-6f && Vector3.Dot(g, nm) < 0f) g = -g; // la cara mira hacia donde apuntan las normales de la malla
                tr.G = g; todos.Add(tr); keys.Add(k);
            }
        }
        var padre = new Dictionary<Vector3Int, Vector3Int>();
        System.Func<Vector3Int, Vector3Int> raizDe = null;
        raizDe = a => { while (padre[a] != a) { padre[a] = padre[padre[a]]; a = padre[a]; } return a; };
        foreach (var k in keys) foreach (var v in k) if (!padre.ContainsKey(v)) padre[v] = v;
        foreach (var k in keys)
        {
            Vector3Int a = raizDe(k[0]);
            for (int c = 1; c < 3; c++) { Vector3Int b = raizDe(k[c]); if (a != b) padre[b] = a; }
        }
        var grupos = new Dictionary<Vector3Int, Pieza>();
        for (int i = 0; i < todos.Count; i++)
        {
            Vector3Int r = raizDe(keys[i][0]);
            if (!grupos.ContainsKey(r)) grupos[r] = new Pieza();
            grupos[r].T.Add(todos[i]);
        }
        var res = new List<Pieza>(grupos.Values);
        foreach (Pieza pz in res) Marco(pz);
        return res;
    }

    private static void Marco(Pieza pz)
    {
        Vector3 c = Vector3.zero; int n = 0;
        foreach (Tri t in pz.T) for (int i = 0; i < 3; i++) { c += t.P[i]; n++; }
        c /= Mathf.Max(1, n); pz.Ctr = c;
        float[,] cov = new float[3, 3];
        pz.Lo = new Vector3(1e9f, 1e9f, 1e9f); pz.Hi = new Vector3(-1e9f, -1e9f, -1e9f);
        foreach (Tri t in pz.T) for (int i = 0; i < 3; i++)
        {
            Vector3 d = t.P[i] - c;
            for (int a = 0; a < 3; a++) for (int b = 0; b < 3; b++) cov[a, b] += d[a] * d[b];
            pz.Lo = Vector3.Min(pz.Lo, t.P[i]); pz.Hi = Vector3.Max(pz.Hi, t.P[i]);
        }
        Vector3[] e; float[] val; Jacobi(cov, out e, out val);
        int[] o = { 0, 1, 2 };
        System.Array.Sort(o, (a, b) => val[b].CompareTo(val[a]));
        pz.E1 = e[o[0]]; pz.E2 = e[o[1]]; pz.E3 = e[o[2]];
    }

    internal static void Jacobi(float[,] A, out Vector3[] vec, out float[] val)
    {
        float[,] a = (float[,])A.Clone(); float[,] v = { { 1, 0, 0 }, { 0, 1, 0 }, { 0, 0, 1 } };
        for (int it = 0; it < 30; it++)
        {
            int p = 0, q = 1; float m = Mathf.Abs(a[0, 1]);
            if (Mathf.Abs(a[0, 2]) > m) { p = 0; q = 2; m = Mathf.Abs(a[0, 2]); }
            if (Mathf.Abs(a[1, 2]) > m) { p = 1; q = 2; m = Mathf.Abs(a[1, 2]); }
            if (m < 1e-12f) break;
            float th = 0.5f * Mathf.Atan2(2f * a[p, q], a[q, q] - a[p, p]);
            float cs = Mathf.Cos(th), sn = Mathf.Sin(th);
            for (int k = 0; k < 3; k++) { float akp = a[k, p], akq = a[k, q]; a[k, p] = cs * akp - sn * akq; a[k, q] = sn * akp + cs * akq; }
            for (int k = 0; k < 3; k++) { float apk = a[p, k], aqk = a[q, k]; a[p, k] = cs * apk - sn * aqk; a[q, k] = sn * apk + cs * aqk; }
            for (int k = 0; k < 3; k++) { float vkp = v[k, p], vkq = v[k, q]; v[k, p] = cs * vkp - sn * vkq; v[k, q] = sn * vkp + cs * vkq; }
        }
        vec = new Vector3[3]; val = new float[3];
        for (int i = 0; i < 3; i++) { vec[i] = new Vector3(v[0, i], v[1, i], v[2, i]).normalized; val[i] = a[i, i]; }
    }

    internal static float Azar(Vector3 c, float s)
    {
        return Mathf.Abs(Mathf.Sin((c.x * 12.9898f + c.y * 78.233f + c.z * 37.719f) * s) * 43758.5453f) % 1f;
    }

    // ---------- nube: proyeccion de frente (x, y); el degradado vertical mezcla la altura de toda la nube con la de cada bola ----------
    private static void Nube(Pieza pz, Vector3 lo, Vector3 hi, List<Vector2[]> uvs, List<int> capas, List<Tri> tris)
    {
        float off = Azar(pz.Ctr, 1f), off2 = Azar(pz.Ctr, 3.3f);
        float hGlobal = Mathf.Max(1e-3f, hi.y - lo.y), hLocal = Mathf.Max(1e-3f, pz.Hi.y - pz.Lo.y);
        foreach (Tri t in pz.T)
        {
            var uv = new Vector2[3];
            for (int c = 0; c < 3; c++)
            {
                Vector3 p = t.P[c];
                float vg = (p.y - lo.y) / hGlobal, vl = (p.y - pz.Lo.y) / hLocal;
                float v = Mathf.Clamp(0.55f * vg + 0.45f * vl, 0.02f, 0.98f);
                uv[c] = new Vector2(-p.x / 1.5f + off + 0.35f * (p.z - pz.Ctr.z) * (1f + off2), v);
            }
            tris.Add(t); uvs.Add(uv); capas.Add(0);
        }
    }

    // ---------- copa del arbol: proyeccion por caras (eje dominante), 1.2 m por repeticion ----------
    private static void Copa(Pieza pz, List<Vector2[]> uvs, List<int> capas, List<Tri> tris)
    {
        float oU = Azar(pz.Ctr, 1f), oV = Azar(pz.Ctr, 2.7f);
        foreach (Tri t in pz.T)
        {
            Vector3 g = (t.N[0] + t.N[1] + t.N[2]).normalized;
            float ax = Mathf.Abs(g.x), ay = Mathf.Abs(g.y), az = Mathf.Abs(g.z);
            var uv = new Vector2[3];
            for (int c = 0; c < 3; c++)
            {
                Vector3 p = t.P[c] - pz.Ctr;
                if (ay >= ax && ay >= az) uv[c] = new Vector2(p.x, p.z);
                else if (ax >= az) uv[c] = new Vector2(p.z, p.y);
                else uv[c] = new Vector2(p.x, p.y);
                uv[c] = uv[c] / 1.2f + new Vector2(oU, oV);
            }
            tris.Add(t); uvs.Add(uv); capas.Add(0);
        }
    }

    // ---------- tronco / rama: el eje es la direccion mas larga; u alrededor, v a lo largo ----------
    private static void Tronco(Pieza pz, List<Vector2[]> uvs, List<int> capas, List<Tri> tris)
    {
        Vector3 ax = pz.E1;
        Vector3 p = Vector3.Cross(ax, Mathf.Abs(ax.y) < 0.9f ? Vector3.up : Vector3.right).normalized; Vector3 q = Vector3.Cross(ax, p);
        float lo = 1e9f, rad = 0f;
        foreach (Tri t in pz.T) for (int i = 0; i < 3; i++)
        {
            Vector3 d = t.P[i] - pz.Ctr; float h = Vector3.Dot(d, ax); lo = Mathf.Min(lo, h);
            rad = Mathf.Max(rad, (d - ax * h).magnitude);
        }
        rad = Mathf.Max(rad, 0.02f);
        int rep = Mathf.Max(1, Mathf.RoundToInt(2f * Mathf.PI * rad / 0.45f));
        float off = Azar(pz.Ctr, 1f);
        foreach (Tri t in pz.T)
        {
            bool tapa = Mathf.Abs(Vector3.Dot(t.G, ax)) > 0.9f;
            var uv = new Vector2[3]; var ang = new float[3];
            for (int c = 0; c < 3; c++)
            {
                Vector3 d = t.P[c] - pz.Ctr;
                if (tapa) uv[c] = new Vector2(Vector3.Dot(d, p) / 0.45f + off, Vector3.Dot(d, q) / 0.45f + off);
                else
                {
                    ang[c] = Mathf.Atan2(Vector3.Dot(d, q), Vector3.Dot(d, p)) / (2f * Mathf.PI);
                    uv[c].y = (Vector3.Dot(d, ax) - lo) / 1.0f + off;
                }
            }
            if (!tapa)
            {
                float mn = Mathf.Min(ang[0], Mathf.Min(ang[1], ang[2])), mx = Mathf.Max(ang[0], Mathf.Max(ang[1], ang[2]));
                for (int c = 0; c < 3; c++) { float a = ang[c]; if (mx - mn > 0.5f && a < 0f) a += 1f; uv[c].x = a * rep + off; }
            }
            tris.Add(t); uvs.Add(uv); capas.Add(1);
        }
    }

    // ---------- malla nueva: un vertice por esquina de triangulo, conservando las normales suaves ----------
    internal static void Reconstruir(GameObject raiz, MeshFilter mf, List<Tri> tris, List<Vector2[]> uvs, List<int> capas, int nCapas)
    {
        Mesh mesh = mf.sharedMesh;
        var pos = new List<Vector3>(); var nrm = new List<Vector3>(); var uv = new List<Vector2>();
        var sub = new List<int>[nCapas];
        for (int s = 0; s < nCapas; s++) sub[s] = new List<int>();
        for (int k = 0; k < tris.Count; k++)
        {
            for (int c = 0; c < 3; c++)
            {
                sub[capas[k]].Add(pos.Count);
                pos.Add(mf.transform.InverseTransformPoint(raiz.transform.TransformPoint(tris[k].P[c])));
                nrm.Add(mf.transform.InverseTransformDirection(raiz.transform.TransformDirection(tris[k].N[c])).normalized);
                Vector2 u = uvs[k][c];
                if (float.IsNaN(u.x) || float.IsNaN(u.y) || float.IsInfinity(u.x) || float.IsInfinity(u.y)) u = Vector2.zero;
                uv.Add(u);
            }
        }
        mesh.Clear();
        mesh.indexFormat = pos.Count > 65000 ? UnityEngine.Rendering.IndexFormat.UInt32 : UnityEngine.Rendering.IndexFormat.UInt16;
        mesh.SetVertices(pos); mesh.SetNormals(nrm); mesh.SetUVs(0, uv);
        mesh.subMeshCount = nCapas;
        for (int s = 0; s < nCapas; s++) mesh.SetTriangles(sub[s], s);
        mesh.RecalculateBounds();
        mesh.RecalculateTangents();
    }

    // ---------- texturas y materiales ----------
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
            m.SetFloat("_Metallic", 0f);
            m.SetFloat("_Smoothness", n.StartsWith("nube") ? 0.05f : n.StartsWith("hojas") ? 0.15f : 0.08f);
            EditorUtility.SetDirty(m);
        }
    }
}
