using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

// Texturizar libros y cajas de la sala (version 2, con titulos).
// - 27 libros sueltos (objetos "Libro", "Libro.001" ... "Libro.026"): cada uno con su titulo, color, tipografia, dibujo y desgaste.
//   Cada libro usa su propio rectangulo del atlas: lomo (cara de frente, +z), portada (costados) y hojas (arriba, abajo y atras).
// - Los 3 bultos del librero de la derecha (Librero.002, material libros) son CAJAS: carton deteriorado.
// Titulos (numero de objeto = indice):
    //   0 = La noche boca arriba
    //   1 = El príncipe feliz
    //   2 = El fantasma de Canterville
    //   3 = La sirenita
    //   4 = La casa de Asterión
    //   5 = El diario de Ana Frank
    //   6 = La metamorfosis
    //   7 = El mago de Oz
    //   8 = El ruiseñor
    //   9 = El corazón delator
    //  10 = El viejo y el mar
    //  11 = Caperucita Roja
    //  12 = Juan Salvador Gaviota
    //  13 = Alicia en el país de las maravillas
    //  14 = La isla del tesoro
    //  15 = El soldadito de plomo
    //  16 = El Principito
    //  17 = El alquimista
    //  18 = Barba Azul
    //  19 = El gigante egoísta
    //  20 = El libro de la selva
    //  21 = El gato negro
    //  22 = Platero y yo
    //  23 = El árbol generoso
    //  24 = El patito feo
    //  25 = La bella y la bestia
    //  26 = El traje nuevo del emperador
// Parte del escenario Sala: se aplica con Herramientas -> Sala -> Texturizar toda la sala.
internal static class SalaLibros
{
    private const string FBX = "/sala.fbx";
    private const string RUTA_TEX = "Assets/Sala/Texturas/Sala_libros.png";
    private const string RUTA_CAJAS = "Assets/Sala/Texturas/Sala_cajas.png";
    private const string CARPETA_MAT = "Assets/Sala/Materiales";
    private const string RUTA_MAT = "Assets/Sala/Materiales/Sala_libros.mat";
    private const string RUTA_MAT_CAJAS = "Assets/Sala/Materiales/Sala_cajas.mat";
    private const float TILE_CAJA = 1.2f; // metros por repeticion del carton

    // rectangulos del atlas (u0, v0, u1, v1)
    private static readonly float[][] LOMO = new float[][] {
        new float[] { 0.44320f, 0.69026f, 0.49886f, 0.99908f },
        new float[] { 0.93213f, 0.72457f, 0.98714f, 0.99908f },
        new float[] { 0.20101f, 0.65656f, 0.23910f, 0.99908f },
        new float[] { 0.40511f, 0.47641f, 0.50212f, 0.64982f },
        new float[] { 0.50114f, 0.69026f, 0.55680f, 0.99908f },
        new float[] { 0.24137f, 0.65656f, 0.27946f, 0.99908f },
        new float[] { 0.00049f, 0.37531f, 0.05550f, 0.64982f },
        new float[] { 0.28695f, 0.41023f, 0.32406f, 0.64982f },
        new float[] { 0.50439f, 0.47641f, 0.60140f, 0.64982f },
        new float[] { 0.55908f, 0.69026f, 0.61475f, 0.99908f },
        new float[] { 0.73291f, 0.72273f, 0.83024f, 0.99908f },
        new float[] { 0.05778f, 0.37531f, 0.11279f, 0.64982f },
        new float[] { 0.28174f, 0.65656f, 0.31982f, 0.99908f },
        new float[] { 0.00049f, 0.65411f, 0.09847f, 0.99908f },
        new float[] { 0.61702f, 0.69026f, 0.67269f, 0.99908f },
        new float[] { 0.32210f, 0.65656f, 0.36019f, 0.99908f },
        new float[] { 0.11507f, 0.37531f, 0.17008f, 0.64982f },
        new float[] { 0.32633f, 0.41023f, 0.36344f, 0.64982f },
        new float[] { 0.60368f, 0.47641f, 0.70068f, 0.64982f },
        new float[] { 0.67497f, 0.69026f, 0.73063f, 0.99908f },
        new float[] { 0.36247f, 0.65656f, 0.40055f, 0.99908f },
        new float[] { 0.17236f, 0.37531f, 0.22738f, 0.64982f },
        new float[] { 0.36572f, 0.41023f, 0.40283f, 0.64982f },
        new float[] { 0.83252f, 0.72273f, 0.92985f, 0.99908f },
        new float[] { 0.22965f, 0.37531f, 0.28467f, 0.64982f },
        new float[] { 0.40283f, 0.65656f, 0.44092f, 0.99908f },
        new float[] { 0.10075f, 0.65411f, 0.19873f, 0.99908f }
    };
    private static readonly float[][] PORTADA = new float[][] {
        new float[] { 0.42367f, 0.23070f, 0.50602f, 0.37102f },
        new float[] { 0.08512f, 0.08670f, 0.16748f, 0.21109f },
        new float[] { 0.87223f, 0.49418f, 0.95459f, 0.64982f },
        new float[] { 0.84684f, 0.13266f, 0.92920f, 0.21109f },
        new float[] { 0.50830f, 0.23070f, 0.59066f, 0.37102f },
        new float[] { 0.00049f, 0.21538f, 0.08285f, 0.37102f },
        new float[] { 0.16976f, 0.08670f, 0.25212f, 0.21109f },
        new float[] { 0.59294f, 0.10263f, 0.67529f, 0.21109f },
        new float[] { 0.00049f, 0.00337f, 0.08285f, 0.08180f },
        new float[] { 0.59294f, 0.23070f, 0.67529f, 0.37102f },
        new float[] { 0.84684f, 0.24602f, 0.92920f, 0.37102f },
        new float[] { 0.25439f, 0.08670f, 0.33675f, 0.21109f },
        new float[] { 0.08512f, 0.21538f, 0.16748f, 0.37102f },
        new float[] { 0.70296f, 0.49295f, 0.78532f, 0.64982f },
        new float[] { 0.67757f, 0.23070f, 0.75993f, 0.37102f },
        new float[] { 0.16976f, 0.21538f, 0.25212f, 0.37102f },
        new float[] { 0.33903f, 0.08670f, 0.42139f, 0.21109f },
        new float[] { 0.67757f, 0.10263f, 0.75993f, 0.21109f },
        new float[] { 0.08512f, 0.00337f, 0.16748f, 0.08180f },
        new float[] { 0.76221f, 0.23070f, 0.84456f, 0.37102f },
        new float[] { 0.25439f, 0.21538f, 0.33675f, 0.37102f },
        new float[] { 0.42367f, 0.08670f, 0.50602f, 0.21109f },
        new float[] { 0.76221f, 0.10263f, 0.84456f, 0.21109f },
        new float[] { 0.00049f, 0.08609f, 0.08285f, 0.21109f },
        new float[] { 0.50830f, 0.08670f, 0.59066f, 0.21109f },
        new float[] { 0.33903f, 0.21538f, 0.42139f, 0.37102f },
        new float[] { 0.78760f, 0.49295f, 0.86995f, 0.64982f }
    };
    private static readonly float[] HOJAS = new float[] { 0.16976f, 0.06403f, 0.17920f, 0.08180f };

    private static int IndiceLibro(string nombre)
    {
        string n = nombre.ToLower();
        if (n == "libro") return 0;
        if (n.StartsWith("libro.")) { int k; if (int.TryParse(n.Substring(6), out k)) return k; }
        return -1;
    }

    public static void Aplicar(GameObject raiz)
    {
        Material matLibros = AssetDatabase.LoadAssetAtPath<Material>(RUTA_MAT);
        Material matCajas = AssetDatabase.LoadAssetAtPath<Material>(RUTA_MAT_CAJAS);
        int libros = 0, cajas = 0;

        foreach (MeshFilter mf in raiz.GetComponentsInChildren<MeshFilter>())
        {
            string objeto = mf.gameObject.name.ToLower();
            int idx = IndiceLibro(mf.gameObject.name);
            bool librero = objeto.StartsWith("librero");
            if (idx < 0 && !librero) continue; // el marco circular tambien usa el material libros: no se toca aqui
            Mesh mesh = mf.sharedMesh;
            Renderer rend = mf.GetComponent<Renderer>();
            if (mesh == null || rend == null) continue;

            Material[] mats = rend.sharedMaterials;
            Vector3[] vert = mesh.vertices;
            Vector2[] uv = mesh.uv.Length == vert.Length ? mesh.uv : new Vector2[vert.Length];
            bool cambio = false;

            for (int s = 0; s < mesh.subMeshCount && s < mats.Length; s++)
            {
                // sala.fbx tiene el material "libros" reasignado a "sala_libros", por eso se busca por "contiene"
                if (mats[s] == null || !mats[s].name.ToLower().Contains("libros")) continue;
                int[] tris = mesh.GetTriangles(s);
                var P = new Dictionary<int, Vector3>();
                foreach (int i in tris)
                    if (!P.ContainsKey(i)) P[i] = raiz.transform.InverseTransformPoint(mf.transform.TransformPoint(vert[i]));

                if (idx >= 0 && idx < LOMO.Length) { UvLibro(idx, tris, P, uv); if (matLibros != null) mats[s] = matLibros; libros++; cambio = true; }
                else if (librero) { UvCajas(tris, P, uv); if (matCajas != null) mats[s] = matCajas; cajas++; cambio = true; }
            }
            if (cambio) { SalaTexturizador.AsignarUV(mesh, uv); rend.sharedMaterials = mats; }
        }
        Debug.Log("[TexturizarLibros] " + libros + " libros y " + cajas + " cajas texturizados");
    }

    private static void UvLibro(int idx, int[] tris, Dictionary<int, Vector3> P, Vector2[] uv)
    {
        Vector3 mn = new Vector3(1e9f, 1e9f, 1e9f), mx = -mn;
        foreach (var kv in P) { mn = Vector3.Min(mn, kv.Value); mx = Vector3.Max(mx, kv.Value); }
        Vector3 sz = Vector3.Max(mx - mn, new Vector3(1e-4f, 1e-4f, 1e-4f));
        float[] lomo = LOMO[idx], portada = PORTADA[idx];
        for (int t = 0; t < tris.Length; t += 3)
        {
            Vector3 n = Vector3.Cross(P[tris[t + 1]] - P[tris[t]], P[tris[t + 2]] - P[tris[t]]).normalized;
            float ax = Mathf.Abs(n.x), ay = Mathf.Abs(n.y), az = Mathf.Abs(n.z);
            for (int c = 0; c < 3; c++)
            {
                int i = tris[t + c]; Vector3 q = P[i];
                float vy = Mathf.Clamp01((q.y - mn.y) / sz.y);
                if (az >= ax && az >= ay && n.z > 0f)
                {
                    // lomo: la camara esta en +z mirando hacia -z y la derecha de la pantalla es -x
                    float u = Mathf.Clamp01((mx.x - q.x) / sz.x);
                    uv[i] = new Vector2(Mathf.Lerp(lomo[0], lomo[2], u), Mathf.Lerp(lomo[1], lomo[3], vy));
                }
                else if (ax >= ay && ax >= az)
                {
                    float u = n.x > 0f ? (q.z - mn.z) / sz.z : (mx.z - q.z) / sz.z; // portada
                    uv[i] = new Vector2(Mathf.Lerp(portada[0], portada[2], Mathf.Clamp01(u)), Mathf.Lerp(portada[1], portada[3], vy));
                }
                else
                {
                    uv[i] = new Vector2((HOJAS[0] + HOJAS[2]) * 0.5f, (HOJAS[1] + HOJAS[3]) * 0.5f); // hojas
                }
            }
        }
    }

    // Cajas: cada bulto del librero = grupo de triangulos conectados; carton en metros con desplazamiento propio
    private static void UvCajas(int[] tris, Dictionary<int, Vector3> P, Vector2[] uv)
    {
        var padre = new Dictionary<Vector3Int, Vector3Int>();
        System.Func<Vector3Int, Vector3Int> buscar = null;
        buscar = k => { while (padre.TryGetValue(k, out Vector3Int p) && p != k) { padre[k] = padre.ContainsKey(p) ? padre[p] : p; k = p; } return k; };
        System.Func<int, Vector3Int> clave = i => new Vector3Int(Mathf.RoundToInt(P[i].x * 1000f), Mathf.RoundToInt(P[i].y * 1000f), Mathf.RoundToInt(P[i].z * 1000f));
        for (int t = 0; t < tris.Length; t += 3)
            for (int c = 0; c < 3; c++) { Vector3Int k = clave(tris[t + c]); if (!padre.ContainsKey(k)) padre[k] = k; }
        for (int t = 0; t < tris.Length; t += 3)
        {
            Vector3Int a = buscar(clave(tris[t]));
            for (int c = 1; c < 3; c++) { Vector3Int b = buscar(clave(tris[t + c])); if (a != b) padre[b] = a; }
        }
        var lo = new Dictionary<Vector3Int, Vector3>(); var hi = new Dictionary<Vector3Int, Vector3>();
        for (int t = 0; t < tris.Length; t += 3)
        {
            Vector3Int g = buscar(clave(tris[t]));
            for (int c = 0; c < 3; c++)
            {
                Vector3 p = P[tris[t + c]];
                lo[g] = lo.ContainsKey(g) ? Vector3.Min(lo[g], p) : p;
                hi[g] = hi.ContainsKey(g) ? Vector3.Max(hi[g], p) : p;
            }
        }
        for (int t = 0; t < tris.Length; t += 3)
        {
            Vector3Int g = buscar(clave(tris[t]));
            Vector3 ctr = (lo[g] + hi[g]) * 0.5f;
            float off = Mathf.Abs(Mathf.Sin(ctr.x * 12.9898f + ctr.y * 78.233f + ctr.z * 37.719f) * 43758.5453f) % 1f;
            Vector3 n = Vector3.Cross(P[tris[t + 1]] - P[tris[t]], P[tris[t + 2]] - P[tris[t]]).normalized;
            float ax = Mathf.Abs(n.x), ay = Mathf.Abs(n.y), az = Mathf.Abs(n.z);
            for (int c = 0; c < 3; c++)
            {
                int i = tris[t + c]; Vector3 q = P[i];
                Vector2 r;
                if (az >= ax && az >= ay) r = new Vector2(q.x, q.y);
                else if (ax >= ay) r = new Vector2(q.z, q.y);
                else r = new Vector2(q.x, q.z);
                uv[i] = r / TILE_CAJA + new Vector2(off, off * 0.7f);
            }
        }
    }


    public static void Configurar()
    {
        SalaTexturizador.AsegurarCarpetas();

        var imp = AssetImporter.GetAtPath(RUTA_TEX) as TextureImporter;
        if (imp == null) { Debug.LogError("[TexturizarLibros] falta " + RUTA_TEX); return; }
        imp.wrapMode = TextureWrapMode.Clamp; imp.mipmapEnabled = false; imp.maxTextureSize = 4096; imp.filterMode = FilterMode.Bilinear;
        imp.SaveAndReimport();
        var impC = AssetImporter.GetAtPath(RUTA_CAJAS) as TextureImporter;
        if (impC != null) { impC.wrapMode = TextureWrapMode.Repeat; impC.maxTextureSize = 1024; impC.SaveAndReimport(); }

        Material m = CrearMaterial(RUTA_MAT);
        m.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(RUTA_TEX));
        m.SetFloat("_Smoothness", 0.1f);
        EditorUtility.SetDirty(m);
        Material c = CrearMaterial(RUTA_MAT_CAJAS);
        c.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(RUTA_CAJAS));
        c.SetFloat("_Smoothness", 0.05f);
        EditorUtility.SetDirty(c);
        AssetDatabase.SaveAssets();

    }

    private static Material CrearMaterial(string ruta)
    {
        Material m = AssetDatabase.LoadAssetAtPath<Material>(ruta);
        if (m == null) { m = new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(m, ruta); }
        m.SetTextureScale("_BaseMap", Vector2.one);
        m.SetColor("_BaseColor", Color.white);
        m.SetFloat("_Metallic", 0f);
        return m;
    }
}
