using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

// Las 11 cajas de la cochera (Caja, Caja2, Caja2.001-.003, Caja3, Caja3.001-.002, Caja5, Caja6, Caja6.001).
// Cada una tiene su propio diseno y su propia textura (Assets/Cochera/Texturas/Cochera_caja_N.png):
//   1 Caja       casillero de acero azul "HERRAMIENTAS"      7 Caja3.001  carton "LIBROS" mojado y aplastado
//   2 Caja2      cofre de acero oxidado "REPUESTOS"           8 Caja3.002  carton con letras a mano "NAVIDAD"
//   3 Caja2.001  caja de acero verde oliva "CABLES 230V"      9 Caja5      cajon de madera pintado de rojo "PINTURA"
//   4 Caja2.002  caja de herramientas roja "TOOLS"           10 Caja6      huacal grande de madera con travesano y letreros
//   5 Caja2.003  caja galvanizada "TORNILLERIA"              11 Caja6.001  contenedor de plastico gris azulado "INVIERNO"
//   6 Caja3      carton de envio con cinta y etiqueta
// Cada textura es un atlas con 5 zonas: F frente, S lados, T parte superior del cuerpo, L TAPA (la base para saltar) y E canto de la tapa.
// El cuerpo y la tapa son dos submallas distintas; la tapa es la mas delgada.
internal static class CocheraCajas
{
    private const string TEX = "Assets/Cochera/Texturas/";
    private const string MAT = "Assets/Cochera/Materiales/";

    private class Caja
    {
        public string nombre; public int id; public Vector4 F, S, T, L, E; public float brillo, metal;
        public Caja(string n, int i, Vector4 f, Vector4 s, Vector4 t, Vector4 l, Vector4 e, float b, float m) { nombre = n; id = i; F = f; S = s; T = t; L = l; E = e; brillo = b; metal = m; }
    }

    private static readonly Caja[] CAJAS =
    {
        new Caja("caja", 1, new Vector4(0.00000f, 0.69336f, 0.24805f, 1.00000f), new Vector4(0.25195f, 0.69336f, 0.59505f, 1.00000f), new Vector4(0.00000f, 0.01432f, 0.24805f, 0.35742f), new Vector4(0.59896f, 0.36133f, 0.84505f, 1.00000f), new Vector4(0.25195f, 0.33529f, 0.89062f, 0.35742f), 0.35f, 0.30f),
        new Caja("caja2", 2, new Vector4(0.00000f, 0.88737f, 0.48177f, 1.00000f), new Vector4(0.48568f, 0.88737f, 0.88932f, 1.00000f), new Vector4(0.47656f, 0.47982f, 0.95833f, 0.88346f), new Vector4(0.00000f, 0.46224f, 0.47266f, 0.88346f), new Vector4(0.00000f, 0.42708f, 0.47266f, 0.45833f), 0.25f, 0.35f),
        new Caja("caja2.001", 3, new Vector4(0.00000f, 0.88737f, 0.48177f, 1.00000f), new Vector4(0.48568f, 0.88737f, 0.88932f, 1.00000f), new Vector4(0.47656f, 0.47982f, 0.95833f, 0.88346f), new Vector4(0.00000f, 0.46224f, 0.47266f, 0.88346f), new Vector4(0.00000f, 0.42708f, 0.47266f, 0.45833f), 0.30f, 0.30f),
        new Caja("caja2.002", 4, new Vector4(0.00000f, 0.88737f, 0.48177f, 1.00000f), new Vector4(0.48568f, 0.88737f, 0.88932f, 1.00000f), new Vector4(0.47656f, 0.47982f, 0.95833f, 0.88346f), new Vector4(0.00000f, 0.46224f, 0.47266f, 0.88346f), new Vector4(0.00000f, 0.42708f, 0.47266f, 0.45833f), 0.45f, 0.30f),
        new Caja("caja2.003", 5, new Vector4(0.00000f, 0.68490f, 0.35221f, 1.00000f), new Vector4(0.35612f, 0.68490f, 0.88737f, 1.00000f), new Vector4(0.34701f, 0.14974f, 0.69922f, 0.68099f), new Vector4(0.00000f, 0.12695f, 0.34310f, 0.68099f), new Vector4(0.00000f, 0.02930f, 0.55404f, 0.12305f), 0.50f, 0.55f),
        new Caja("caja3", 6, new Vector4(0.00000f, 0.61589f, 0.38607f, 1.00000f), new Vector4(0.38997f, 0.61589f, 0.91081f, 1.00000f), new Vector4(0.38346f, 0.09115f, 0.76953f, 0.61198f), new Vector4(0.00000f, 0.05339f, 0.37956f, 0.61198f), new Vector4(0.00000f, 0.00195f, 0.55859f, 0.04948f), 0.03f, 0.00f),
        new Caja("caja3.001", 7, new Vector4(0.00000f, 0.68229f, 0.31966f, 1.00000f), new Vector4(0.32357f, 0.68229f, 0.90885f, 1.00000f), new Vector4(0.31706f, 0.09310f, 0.63672f, 0.67839f), new Vector4(0.00000f, 0.05013f, 0.31315f, 0.67839f), new Vector4(0.00000f, 0.00651f, 0.62826f, 0.04622f), 0.03f, 0.00f),
        new Caja("caja3.002", 8, new Vector4(0.00000f, 0.72786f, 0.28125f, 1.00000f), new Vector4(0.28516f, 0.72786f, 0.85482f, 1.00000f), new Vector4(0.28906f, 0.15430f, 0.57031f, 0.72396f), new Vector4(0.00000f, 0.11263f, 0.28516f, 0.72396f), new Vector4(0.00000f, 0.05664f, 0.61133f, 0.10872f), 0.03f, 0.00f),
        new Caja("caja5", 9, new Vector4(0.00000f, 0.74089f, 0.36003f, 1.00000f), new Vector4(0.36393f, 0.74089f, 0.83594f, 1.00000f), new Vector4(0.39648f, 0.26497f, 0.75651f, 0.73698f), new Vector4(0.00000f, 0.07487f, 0.39258f, 0.73698f), new Vector4(0.00000f, 0.04622f, 0.66211f, 0.07096f), 0.20f, 0.00f),
        new Caja("caja6", 10, new Vector4(0.00000f, 0.77799f, 0.53385f, 1.00000f), new Vector4(0.53776f, 0.77799f, 0.87240f, 1.00000f), new Vector4(0.00000f, 0.08594f, 0.53385f, 0.42057f), new Vector4(0.00000f, 0.42448f, 0.52930f, 0.77409f), new Vector4(0.00000f, 0.02474f, 0.52930f, 0.08203f), 0.06f, 0.00f),
        new Caja("caja6.001", 11, new Vector4(0.00000f, 0.75456f, 0.35677f, 1.00000f), new Vector4(0.36068f, 0.75456f, 0.94206f, 1.00000f), new Vector4(0.36133f, 0.16927f, 0.71810f, 0.75065f), new Vector4(0.00000f, 0.17513f, 0.35742f, 0.75065f), new Vector4(0.00000f, 0.14648f, 0.57552f, 0.16536f), 0.50f, 0.00f),
    };

    private static Caja Buscar(string nombre)
    {
        string n = nombre.ToLower();
        foreach (Caja c in CAJAS) if (c.nombre == n) return c;
        return null;
    }

    private static Vector2 EnRect(Vector4 r, float u, float v)
    {
        const float m = 0.0016f; // margen para no sangrar a la zona vecina
        u = Mathf.Clamp01(u); v = Mathf.Clamp01(v);
        return new Vector2(Mathf.Lerp(r.x + m, r.z - m, u), Mathf.Lerp(r.y + m, r.w - m, v));
    }

    public static void Aplicar(GameObject raiz)
    {
        int hechas = 0;
        foreach (MeshFilter mf in raiz.GetComponentsInChildren<MeshFilter>())
        {
            Caja caja = Buscar(mf.gameObject.name);
            if (caja == null) continue;
            Mesh mesh = mf.sharedMesh;
            Renderer rend = mf.GetComponent<Renderer>();
            if (mesh == null || rend == null) continue;
            Material mat = AssetDatabase.LoadAssetAtPath<Material>(MAT + "Cochera_caja_" + caja.id + ".mat");

            // 1) vertices en el espacio del modelo (1 unidad = 1 m), triangulos separados por cara
            Vector3[] vert = mesh.vertices;
            Vector3[] nor = mesh.normals;
            bool hayNor = nor != null && nor.Length == vert.Length;
            int subs = mesh.subMeshCount;
            var pos = new List<Vector3>(); var uv = new List<Vector2>();
            var tris = new List<int>[subs];
            var P = new Vector3[subs][]; var N = new Vector3[subs][]; // por submalla: posiciones y normal de cara (3 por triangulo)
            var lo = new Vector3[subs]; var hi = new Vector3[subs];
            for (int s = 0; s < subs; s++)
            {
                int[] t = mesh.GetTriangles(s);
                tris[s] = new List<int>();
                P[s] = new Vector3[t.Length]; N[s] = new Vector3[t.Length / 3];
                lo[s] = new Vector3(1e9f, 1e9f, 1e9f); hi[s] = new Vector3(-1e9f, -1e9f, -1e9f);
                for (int i = 0; i + 2 < t.Length; i += 3)
                {
                    for (int c = 0; c < 3; c++)
                    {
                        Vector3 p = raiz.transform.InverseTransformPoint(mf.transform.TransformPoint(vert[t[i + c]]));
                        P[s][i + c] = p; lo[s] = Vector3.Min(lo[s], p); hi[s] = Vector3.Max(hi[s], p);
                    }
                    Vector3 g = Vector3.Cross(P[s][i + 1] - P[s][i], P[s][i + 2] - P[s][i]).normalized;
                    if (hayNor)
                    {
                        Vector3 nm = nor[t[i]] + nor[t[i + 1]] + nor[t[i + 2]];
                        nm = raiz.transform.InverseTransformDirection(mf.transform.TransformDirection(nm));
                        if (nm.sqrMagnitude > 1e-6f && Vector3.Dot(g, nm) < 0f) g = -g; // la cara mira hacia donde apunta la normal de la malla
                    }
                    N[s][i / 3] = g;
                }
            }
            // la tapa es la submalla mas delgada (en y)
            int lid = -1;
            if (subs >= 2) { float menor = 1e9f; for (int s = 0; s < subs; s++) { float h = hi[s].y - lo[s].y; if (h < menor) { menor = h; lid = s; } } }
            float lenTapa = lid >= 0 ? Mathf.Max(hi[lid].x - lo[lid].x, hi[lid].z - lo[lid].z) : 1f;

            for (int s = 0; s < subs; s++)
            {
                bool esTapa = s == lid;
                Vector3 mn = lo[s], mx = hi[s];
                float w = Mathf.Max(1e-4f, mx.x - mn.x), h = Mathf.Max(1e-4f, mx.y - mn.y), d = Mathf.Max(1e-4f, mx.z - mn.z);
                for (int i = 0; i + 2 < P[s].Length; i += 3)
                {
                    Vector3 g = N[s][i / 3];
                    float ax = Mathf.Abs(g.x), ay = Mathf.Abs(g.y), az = Mathf.Abs(g.z);
                    tris[s].Add(pos.Count); tris[s].Add(pos.Count + 1); tris[s].Add(pos.Count + 2);
                    for (int c = 0; c < 3; c++)
                    {
                        Vector3 p = P[s][i + c];
                        Vector2 r;
                        if (ay >= ax && ay >= az)
                        {
                            // cara de arriba / abajo
                            float u = g.y > 0f ? (mx.x - p.x) / w : (p.x - mn.x) / w;
                            float v = (mx.z - p.z) / d;
                            r = EnRect(esTapa ? caja.L : caja.T, u, v);
                        }
                        else if (esTapa)
                        {
                            // canto de la tapa: tira E (la longitud va a lo largo de la tira)
                            float u = az >= ax ? (g.z > 0f ? (mx.x - p.x) : (p.x - mn.x)) / lenTapa : (g.x > 0f ? (mx.z - p.z) : (p.z - mn.z)) / lenTapa;
                            r = EnRect(caja.E, u, (p.y - mn.y) / h);
                        }
                        else if (az >= ax)
                        {
                            // frente (+z) o atras (-z)
                            float u = g.z > 0f ? (mx.x - p.x) / w : (p.x - mn.x) / w;
                            r = EnRect(caja.F, u, (p.y - mn.y) / h);
                        }
                        else
                        {
                            // lados (+x / -x)
                            float u = g.x > 0f ? (mx.z - p.z) / d : (p.z - mn.z) / d;
                            r = EnRect(caja.S, u, (p.y - mn.y) / h);
                        }
                        pos.Add(p); uv.Add(r);
                    }
                }
            }

            // 2) reconstruir la malla con los triangulos separados (las posiciones vuelven al espacio local del objeto)
            var local = new List<Vector3>(pos.Count); var localN = new List<Vector3>(pos.Count);
            for (int k = 0; k < pos.Count; k++)
            {
                local.Add(mf.transform.InverseTransformPoint(raiz.transform.TransformPoint(pos[k])));
            }
            // normales: la de cada cara, llevada al espacio local
            for (int s = 0; s < subs; s++)
                for (int i = 0; i + 2 < P[s].Length; i += 3)
                {
                    Vector3 gl = mf.transform.InverseTransformDirection(raiz.transform.TransformDirection(N[s][i / 3])).normalized;
                    localN.Add(gl); localN.Add(gl); localN.Add(gl);
                }
            mesh.Clear();
            mesh.indexFormat = local.Count > 65000 ? UnityEngine.Rendering.IndexFormat.UInt32 : UnityEngine.Rendering.IndexFormat.UInt16;
            mesh.SetVertices(local);
            mesh.SetNormals(localN);
            mesh.SetUVs(0, uv);
            mesh.subMeshCount = subs;
            for (int s = 0; s < subs; s++) mesh.SetTriangles(tris[s], s);
            mesh.RecalculateBounds();
            mesh.RecalculateTangents();

            if (mat != null)
            {
                var mats = new Material[Mathf.Max(1, subs)];
                for (int s = 0; s < mats.Length; s++) mats[s] = mat;
                rend.sharedMaterials = mats;
            }
            CocheraTexturizador.Tratados.Add(rend);
            hechas++;
        }
        Debug.Log("[Cochera] cajas texturizadas: " + hechas + " de " + CAJAS.Length);
    }

    public static void Configurar()
    {
        foreach (Caja c in CAJAS)
        {
            string tex = TEX + "Cochera_caja_" + c.id + ".png";
            var imp = AssetImporter.GetAtPath(tex) as TextureImporter;
            if (imp == null) { Debug.LogError("[Cochera] falta " + tex); continue; }
            imp.wrapMode = TextureWrapMode.Clamp; imp.maxTextureSize = 2048; imp.anisoLevel = 4; imp.SaveAndReimport();
            string ruta = MAT + "Cochera_caja_" + c.id + ".mat";
            Material m = AssetDatabase.LoadAssetAtPath<Material>(ruta);
            if (m == null) { m = new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(m, ruta); }
            m.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(tex));
            m.SetTextureScale("_BaseMap", Vector2.one);
            m.SetColor("_BaseColor", Color.white);
            m.SetTexture("_BumpMap", null); m.DisableKeyword("_NORMALMAP");
            m.SetFloat("_Metallic", c.metal);
            m.SetFloat("_Smoothness", c.brillo);
            EditorUtility.SetDirty(m);
        }
    }
}
