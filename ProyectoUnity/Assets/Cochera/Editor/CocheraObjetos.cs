using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

// Objetos sueltos de la cochera: madera (banca de trabajo, mesa pequena, escalera, rack pequeno), bote de basura y bicicleta.
//   Banca     -> "Cochera_banca"      roble oscuro aceitado, cortes de sierra, quemaduras, salpicaduras de pintura, huellas de tornillo de banco
//   Mesapequ  -> "Cochera_mesa"       madera pintada verde menta, descascarada y oxidada
//   Estante   -> "Cochera_escalera1"  (la escalera) pino claro con pintura salpicada y peldanos gastados
//   estantepqu-> "Cochera_escalera2"  (el rack pequeno) madera tratada oscura con moho y clavos oxidados
//   botebas   -> "Cochera_bote_*"     lamina galvanizada abollada, rotulo BASURA desgastado, oxido en chorreos
//   bici.002  -> "Cochera_bici_*"     marco rojo descascarado, llantas de hule con tacos y letras, manillar cromado con puños de goma
// Cada pieza (cada tabla, pata, peldano...) se mapea con su propio eje: la veta corre a lo largo de la pieza y las puntas usan una textura de corte (testa).
// Cada pieza recibe un desplazamiento distinto para que no se repita el mismo dibujo.
internal static class CocheraObjetos
{
    private const string TEX = "Assets/Cochera/Texturas/Cochera_";
    private const string MAT = "Assets/Cochera/Materiales/Cochera_";

    // nombre, repetir, brillo, metalico
    private static readonly object[][] DEF =
    {
        new object[]{"banca", true, 0.14f, 0f}, new object[]{"banca_testa", true, 0.05f, 0f},
        new object[]{"mesa", true, 0.10f, 0f}, new object[]{"mesa_testa", true, 0.05f, 0f},
        new object[]{"escalera1", true, 0.06f, 0f}, new object[]{"escalera1_testa", true, 0.04f, 0f},
        new object[]{"escalera2", true, 0.08f, 0f}, new object[]{"escalera2_testa", true, 0.04f, 0f},
        new object[]{"bote_lado", true, 0.45f, 0.55f}, new object[]{"bote_tapa", false, 0.45f, 0.55f},
        new object[]{"bici_marco", true, 0.45f, 0.25f}, new object[]{"bici_metal", true, 0.55f, 0.65f},
        new object[]{"bici_llanta", true, 0.12f, 0f}, new object[]{"bici_manillar", false, 0.5f, 0.4f},
        new object[]{"panel1_perfil", true, 0.35f, 0.5f}, new object[]{"panel1_placa", false, 0.4f, 0.5f},
        new object[]{"panel2_perfil", true, 0.35f, 0.5f}, new object[]{"panel2_placa", false, 0.4f, 0.5f},
        new object[]{"panel3_perfil", true, 0.35f, 0.5f}, new object[]{"panel3_placa", false, 0.4f, 0.5f},
        new object[]{"tubo", true, 0.45f, 0.6f}, new object[]{"tubo_tapa", false, 0.3f, 0.4f},
    };

    private class Tri { public Vector3[] P = new Vector3[3]; public Vector3 G; public int Comp; }
    private class Pieza { public Vector3 Ctr, E1, E2, E3, Lo, Hi; public float Radio; public List<Tri> T = new List<Tri>(); }

    private static Material M(string n) { return AssetDatabase.LoadAssetAtPath<Material>(MAT + n + ".mat"); }

    public static void Aplicar(GameObject raiz)
    {
        int hechos = 0;
        foreach (MeshFilter mf in raiz.GetComponentsInChildren<MeshFilter>())
        {
            string n = mf.gameObject.name.ToLower();
            Renderer rend = mf.GetComponent<Renderer>();
            if (mf.sharedMesh == null || rend == null) continue;
            string[] pares = null; int tipo = 0;
            if (n == "banca") { pares = new[] { "banca", "banca_testa" }; tipo = 1; }
            else if (n == "mesapequ") { pares = new[] { "mesa", "mesa_testa" }; tipo = 1; }
            else if (n == "estante") { pares = new[] { "escalera1", "escalera1_testa" }; tipo = 1; }
            else if (n == "estantepqu") { pares = new[] { "escalera2", "escalera2_testa" }; tipo = 1; }
            else if (n == "botebas") { pares = new[] { "bote_lado", "bote_tapa" }; tipo = 2; }
            else if (n.StartsWith("panel") && n.Length > 5 && char.IsDigit(n[5]) && n[5] >= '1' && n[5] <= '3') { pares = new[] { "panel" + n[5] + "_perfil", "panel" + n[5] + "_placa" }; tipo = 4; }
            else if (n.StartsWith("tuberia")) { pares = new[] { "tubo", "tubo_tapa" }; tipo = 5; }
            else if (n.StartsWith("bici")) { pares = new[] { "bici_marco", "bici_metal", "bici_llanta", "bici_manillar" }; tipo = 3; }
            if (tipo == 0) continue;
            var mats = new Material[pares.Length];
            bool ok = true;
            for (int i = 0; i < pares.Length; i++) { mats[i] = M(pares[i]); if (mats[i] == null) ok = false; }
            if (!ok) { Debug.LogError("[Cochera] faltan materiales de " + mf.gameObject.name + ", usa el menu Texturizar toda la cochera"); continue; }

            List<Pieza> piezas = Piezas(raiz, mf);
            var uvs = new List<Vector2[]>(); var capa = new List<int>(); var tris = new List<Tri>();
            foreach (Pieza pz in piezas)
            {
                if (tipo == 1) Madera(pz, uvs, capa, tris);
                else if (tipo == 2) Cilindro(pz, uvs, capa, tris, 0, 1, 0.94f, 0.5f);
                else if (tipo == 4) Panel(pz, piezas, uvs, capa, tris);
                else if (tipo == 5) Cilindro(pz, uvs, capa, tris, 0, 1, 0.8f, 2.0f);
                else Bici(pz, piezas, uvs, capa, tris);
            }
            Reconstruir(raiz, mf, tris, uvs, capa, pares.Length);
            rend.sharedMaterials = mats;
            CocheraTexturizador.Tratados.Add(rend);
            hechos++;
        }
        Debug.Log("[Cochera] madera, bote y bici texturizados: " + hechos + " objetos");
    }

    // ---------- geometria ----------
    private static List<Pieza> Piezas(GameObject raiz, MeshFilter mf)
    {
        Mesh mesh = mf.sharedMesh;
        Vector3[] vert = mesh.vertices; Vector3[] nor = mesh.normals;
        bool hayNor = nor != null && nor.Length == vert.Length;
        var todos = new List<Tri>();
        var keys = new List<Vector3Int[]>();
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
                if (hayNor)
                {
                    Vector3 nm = nor[t[i]] + nor[t[i + 1]] + nor[t[i + 2]];
                    nm = raiz.transform.InverseTransformDirection(mf.transform.TransformDirection(nm));
                    if (nm.sqrMagnitude > 1e-6f && Vector3.Dot(g, nm) < 0f) g = -g;
                }
                tr.G = g; todos.Add(tr); keys.Add(k);
            }
        }
        // componentes conexas (union-find por posicion soldada)
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
        res.Sort((a, b) => a.Ctr.y.CompareTo(b.Ctr.y));
        return res;
    }

    // ejes principales de la pieza: E1 = mas larga, E3 = mas fina (covarianza de los vertices)
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
        Vector3[] e; float[] val;
        Jacobi(cov, out e, out val);
        int[] o = { 0, 1, 2 };
        System.Array.Sort(o, (a, b) => val[b].CompareTo(val[a]));
        pz.E1 = e[o[0]]; pz.E2 = e[o[1]]; pz.E3 = e[o[2]];
    }

    // autovalores y autovectores de una matriz simetrica 3x3 (Jacobi)
    private static void Jacobi(float[,] A, out Vector3[] vec, out float[] val)
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

    private static float Azar(Vector3 c, float s)
    {
        return Mathf.Abs(Mathf.Sin((c.x * 12.9898f + c.y * 78.233f + c.z * 37.719f) * s) * 43758.5453f) % 1f;
    }

    private static void Poner(Pieza pz, Tri t, Vector2[] uv, int capa, List<Vector2[]> uvs, List<int> capas, List<Tri> tris)
    {
        tris.Add(t); uvs.Add(uv); capas.Add(capa);
    }

    // ---------- madera: veta a lo largo de la pieza ----------
    private static void Madera(Pieza pz, List<Vector2[]> uvs, List<int> capas, List<Tri> tris)
    {
        float offU = Azar(pz.Ctr, 1f), offV = Azar(pz.Ctr, 2.7f);
        foreach (Tri t in pz.T)
        {
            float a1 = Mathf.Abs(Vector3.Dot(t.G, pz.E1)), a2 = Mathf.Abs(Vector3.Dot(t.G, pz.E2)), a3 = Mathf.Abs(Vector3.Dot(t.G, pz.E3));
            var uv = new Vector2[3];
            int capa;
            for (int c = 0; c < 3; c++)
            {
                Vector3 d = t.P[c] - pz.Ctr;
                float x1 = Vector3.Dot(d, pz.E1), x2 = Vector3.Dot(d, pz.E2), x3 = Vector3.Dot(d, pz.E3);
                if (a1 >= a2 && a1 >= a3) uv[c] = new Vector2(x2 * 2f + offU, x3 * 2f + offV);          // punta: testa
                else if (a2 >= a3) uv[c] = new Vector2(x1 + offU, x3 + offV);                           // cara lateral
                else uv[c] = new Vector2(x1 + offU, x2 + offV);                                         // cara ancha
            }
            capa = (a1 >= a2 && a1 >= a3) ? 1 : 0;
            Poner(pz, t, uv, capa, uvs, capas, tris);
        }
    }

    // ---------- panel: placa grande (la de arriba) con su textura de una sola vez, tubos y base con perfil de acero ----------
    private static void Panel(Pieza pz, List<Pieza> todas, List<Vector2[]> uvs, List<int> capas, List<Tri> tris)
    {
        Pieza placa = null; float mejor = -1f;
        foreach (Pieza o in todas) { float a = (o.Hi.x - o.Lo.x) * (o.Hi.z - o.Lo.z); if (a > mejor) { mejor = a; placa = o; } }
        if (pz != placa) { Madera(pz, uvs, capas, tris); return; }
        float ax = Mathf.Max(1e-3f, pz.Hi.x - pz.Lo.x), az = Mathf.Max(1e-3f, pz.Hi.z - pz.Lo.z);
        foreach (Tri t in pz.T)
        {
            var uv = new Vector2[3];
            bool cara = Mathf.Abs(t.G.y) > 0.7f;
            for (int c = 0; c < 3; c++)
            {
                Vector3 d = t.P[c] - pz.Ctr;
                if (cara) uv[c] = new Vector2(-d.x / ax + 0.5f, -d.z / az + 0.5f); // derecha de la pantalla = -x; arriba del texto = hacia el fondo (-z)
                else uv[c] = new Vector2(-d.x + 0.3f, d.y * 4f + 0.3f);
            }
            Poner(pz, t, uv, cara ? 1 : 0, uvs, capas, tris);
        }
    }

    // ---------- cilindro (bote): lados por angulo, tapas planas ----------
    // eje = direccion donde las normales de las tapas se acumulan
    private static void Cilindro(Pieza pz, List<Vector2[]> uvs, List<int> capas, List<Tri> tris, int capaLado, int capaTapa, float anchoLado, float altoLado)
    {
        float[,] T3 = new float[3, 3];
        foreach (Tri t in pz.T)
        {
            float ar = Vector3.Cross(t.P[1] - t.P[0], t.P[2] - t.P[0]).magnitude;
            for (int a = 0; a < 3; a++) for (int b = 0; b < 3; b++) T3[a, b] += ar * t.G[a] * t.G[b];
        }
        Vector3[] e; float[] val; Jacobi(T3, out e, out val);
        int im = 0; for (int i = 1; i < 3; i++) if (val[i] > val[im]) im = i;
        Vector3 ax = e[im];
        Vector3 p = Vector3.Cross(ax, Mathf.Abs(ax.y) < 0.9f ? Vector3.up : Vector3.right).normalized; Vector3 q = Vector3.Cross(ax, p);
        float lo = 1e9f, hi = -1e9f, rad = 0f;
        foreach (Tri t in pz.T) for (int i = 0; i < 3; i++)
        {
            Vector3 d = t.P[i] - pz.Ctr; float h = Vector3.Dot(d, ax); lo = Mathf.Min(lo, h); hi = Mathf.Max(hi, h);
            rad = Mathf.Max(rad, (d - ax * h).magnitude);
        }
        int rep = Mathf.Max(1, Mathf.RoundToInt(2f * Mathf.PI * rad / anchoLado));
        float off = Azar(pz.Ctr, 1f);
        foreach (Tri t in pz.T)
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
                    uv[c].y = (Vector3.Dot(d, ax) - lo) / altoLado + off;
                }
            }
            if (!tapa)
            {
                float mn = Mathf.Min(ang[0], Mathf.Min(ang[1], ang[2])), mx = Mathf.Max(ang[0], Mathf.Max(ang[1], ang[2]));
                for (int c = 0; c < 3; c++) { float a = ang[c]; if (mx - mn > 0.5f && a < 0f) a += 1f; uv[c].x = a * rep + off; }
            }
            Poner(pz, t, uv, tapa ? capaTapa : capaLado, uvs, capas, tris);
        }
    }

    // ---------- bici ----------
    // piezas ordenadas de abajo hacia arriba. Las de 288 triangulos son las llantas; la mas alta es el manillar;
    // la mas baja de las demas es el tubo del marco; las otras dos (tubos verticales) son metal cromado oxidado.
    private static void Bici(Pieza pz, List<Pieza> todas, List<Vector2[]> uvs, List<int> capas, List<Tri> tris)
    {
        bool llanta = pz.T.Count > 150;
        if (llanta)
        {
            // eje de la rueda = direccion de menor dispersion de los vertices (E3)
            Vector3 ax = pz.E3; Vector3 p = pz.E1; Vector3 q = Vector3.Cross(ax, p);
            float lo = 1e9f, hi = -1e9f;
            foreach (Tri t in pz.T) for (int i = 0; i < 3; i++) { float h = Vector3.Dot(t.P[i] - pz.Ctr, ax); lo = Mathf.Min(lo, h); hi = Mathf.Max(hi, h); }
            float w = Mathf.Max(1e-3f, hi - lo); float off = Azar(pz.Ctr, 1f);
            foreach (Tri t in pz.T)
            {
                var uv = new Vector2[3]; var ang = new float[3];
                for (int c = 0; c < 3; c++)
                {
                    Vector3 d = t.P[c] - pz.Ctr;
                    ang[c] = Mathf.Atan2(Vector3.Dot(d, q), Vector3.Dot(d, p)) / (2f * Mathf.PI);
                    uv[c].y = (Vector3.Dot(d, ax) - lo) / w;
                }
                float mn = Mathf.Min(ang[0], Mathf.Min(ang[1], ang[2])), mx = Mathf.Max(ang[0], Mathf.Max(ang[1], ang[2]));
                for (int c = 0; c < 3; c++) { float a = ang[c]; if (mx - mn > 0.5f && a < 0f) a += 1f; uv[c].x = a * 1f + off; }
                Poner(pz, t, uv, 2, uvs, capas, tris);
            }
            return;
        }
        // piezas no-llanta: ordenadas por altura
        var resto = new List<Pieza>();
        foreach (Pieza o in todas) if (o.T.Count <= 150) resto.Add(o);
        resto.Sort((a, b) => a.Ctr.y.CompareTo(b.Ctr.y));
        int pos = resto.IndexOf(pz);
        int capa = pos == 0 ? 0 : (pos == resto.Count - 1 ? 3 : 1);
        float offU = Azar(pz.Ctr, 1f), offV = Azar(pz.Ctr, 2.7f);
        float largo = Mathf.Max(1e-3f, pz.Hi.x - pz.Lo.x);
        foreach (Tri t in pz.T)
        {
            float a1 = Mathf.Abs(Vector3.Dot(t.G, pz.E1)), a2 = Mathf.Abs(Vector3.Dot(t.G, pz.E2)), a3 = Mathf.Abs(Vector3.Dot(t.G, pz.E3));
            var uv = new Vector2[3];
            for (int c = 0; c < 3; c++)
            {
                Vector3 d = t.P[c] - pz.Ctr;
                float x1 = Vector3.Dot(d, pz.E1), x2 = Vector3.Dot(d, pz.E2), x3 = Vector3.Dot(d, pz.E3);
                float L = 0f;
                foreach (Tri tt in pz.T) for (int i = 0; i < 3; i++) L = Mathf.Max(L, Mathf.Abs(Vector3.Dot(tt.P[i] - pz.Ctr, pz.E1)));
                if (capa == 3)
                {   // manillar: u de 0 a 1 a lo largo (punos en los extremos), v alrededor
                    float u = Mathf.Clamp01(x1 / (2f * Mathf.Max(L, 1e-3f)) + 0.5f);
                    float v = (a1 >= a2 && a1 >= a3) ? 0.5f : Mathf.Clamp01((a2 >= a3 ? x3 : x2) / 0.2f + 0.5f);
                    uv[c] = new Vector2(u, v);
                }
                else if (a1 >= a2 && a1 >= a3) uv[c] = new Vector2(x2 + offU, x3 + offV);
                else if (a2 >= a3) uv[c] = new Vector2(x1 + offU, x3 + offV);
                else uv[c] = new Vector2(x1 + offU, x2 + offV);
            }
            Poner(pz, t, uv, capa, uvs, capas, tris);
        }
    }

    // ---------- reconstruir la malla: un vertice por esquina de triangulo ----------
    private static void Reconstruir(GameObject raiz, MeshFilter mf, List<Tri> tris, List<Vector2[]> uvs, List<int> capas, int nCapas)
    {
        Mesh mesh = mf.sharedMesh;
        var pos = new List<Vector3>(); var nrm = new List<Vector3>(); var uv = new List<Vector2>();
        var sub = new List<int>[nCapas];
        for (int s = 0; s < nCapas; s++) sub[s] = new List<int>();
        for (int k = 0; k < tris.Count; k++)
        {
            Vector3 gl = mf.transform.InverseTransformDirection(raiz.transform.TransformDirection(tris[k].G)).normalized;
            for (int c = 0; c < 3; c++)
            {
                sub[capas[k]].Add(pos.Count);
                pos.Add(mf.transform.InverseTransformPoint(raiz.transform.TransformPoint(tris[k].P[c])));
                nrm.Add(gl);
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
        foreach (object[] d in DEF)
        {
            string n = (string)d[0]; string tex = TEX + n + ".png";
            var imp = AssetImporter.GetAtPath(tex) as TextureImporter;
            if (imp == null) { Debug.LogError("[Cochera] falta " + tex); continue; }
            imp.wrapMode = (bool)d[1] ? TextureWrapMode.Repeat : TextureWrapMode.Clamp;
            imp.maxTextureSize = 2048; imp.anisoLevel = 8; imp.SaveAndReimport();
            string ruta = MAT + n + ".mat";
            Material m = AssetDatabase.LoadAssetAtPath<Material>(ruta);
            if (m == null) { m = new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(m, ruta); }
            m.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(tex));
            m.SetTextureScale("_BaseMap", Vector2.one);
            m.SetColor("_BaseColor", Color.white);
            m.SetTexture("_BumpMap", null); m.DisableKeyword("_NORMALMAP");
            m.SetFloat("_Metallic", (float)d[3]);
            m.SetFloat("_Smoothness", (float)d[2]);
            EditorUtility.SetDirty(m);
        }
    }
}
