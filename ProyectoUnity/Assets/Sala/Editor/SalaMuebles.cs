using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

// Texturizar muebles de la sala: silla, mesa, mesita, base de la tele (madera vieja) y la tele (pantalla rota, plastico sucio).
// No toca estantes, cuadros, "mueble" del fondo, sillones, lamparas ni libros.
// Al importar Assets/sala.fbx se generan UV y se asignan materiales nuevos solo a esos objetos.
// Parte del escenario Sala: se aplica con Herramientas -> Sala -> Texturizar toda la sala.
internal static class SalaMuebles
{
    private const string FBX = "/sala.fbx";
    private const string CARPETA_TEX = "Assets/Sala/Texturas/";
    private const string CARPETA_MAT = "Assets/Sala/Materiales";
    private const float TILE_U = 1.5f; // metros por repeticion a lo largo de la veta
    private const float TILE_V = 0.6f; // metros por repeticion a lo ancho
    private const float V_FRENTE = 0.375f; // en el atlas de la tele: v >= 0.375 = frente con pantalla, v < 0.375 = plastico

    private static bool EsMadera(string n)
    {
        n = n.ToLower();
        return n == "silla" || n == "mesa" || n == "mesita" || n == "basetele" || n.StartsWith("silla.") || n.StartsWith("mesa.") || n.StartsWith("mesita.");
    }
    private static bool EsTele(string n) { return n.ToLower().StartsWith("sala_tv"); }

    public static void Aplicar(GameObject raiz)
    {
        Material matMadera = AssetDatabase.LoadAssetAtPath<Material>(CARPETA_MAT + "/Sala_mueble_madera.mat");
        Material matTele = AssetDatabase.LoadAssetAtPath<Material>(CARPETA_MAT + "/Sala_tele.mat");
        int partes = 0;

        foreach (MeshFilter mf in raiz.GetComponentsInChildren<MeshFilter>())
        {
            string objeto = mf.gameObject.name;
            bool madera = EsMadera(objeto), tele = EsTele(objeto);
            if (!madera && !tele) continue;
            Mesh mesh = mf.sharedMesh;
            Renderer rend = mf.GetComponent<Renderer>();
            if (mesh == null || rend == null) continue;

            Material[] mats = rend.sharedMaterials;
            Vector3[] vert = mesh.vertices;
            Vector2[] uv = mesh.uv.Length == vert.Length ? mesh.uv : new Vector2[vert.Length];

            for (int s = 0; s < mesh.subMeshCount && s < mats.Length; s++)
            {
                if (mats[s] == null) continue;
                string nom = mats[s].name.ToLower();
                if (madera && !nom.Contains("mueble")) continue;
                int[] tris = mesh.GetTriangles(s);
                var P = new Dictionary<int, Vector3>();
                foreach (int i in tris)
                    if (!P.ContainsKey(i)) P[i] = raiz.transform.InverseTransformPoint(mf.transform.TransformPoint(vert[i]));

                if (madera) UvMadera(tris, P, uv);
                else UvTele(tris, P, uv, nom.Contains("tele"));

                Material nuevo = madera ? matMadera : matTele;
                if (nuevo != null) mats[s] = nuevo;
                partes++;
            }
            SalaTexturizador.AsignarUV(mesh, uv);
            rend.sharedMaterials = mats;
        }
        Debug.Log("[TexturizarMuebles] " + partes + " partes texturizadas");
    }

    // Madera: la veta corre a lo largo del lado mas largo de cada pieza (en x o y, que es lo que ve la camara)
    private static void UvMadera(int[] tris, Dictionary<int, Vector3> P, Vector2[] uv)
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
            Vector3 sz = hi[g] - lo[g];
            int eje = sz.x >= sz.y ? 0 : 1; // veta en x o en y
            Vector3 ctr = (lo[g] + hi[g]) * 0.5f;
            float off = Mathf.Abs(Mathf.Sin(ctr.x * 12.9898f + ctr.y * 78.233f + ctr.z * 37.719f) * 43758.5453f) % 1f;
            Vector3 n = Vector3.Cross(P[tris[t + 1]] - P[tris[t]], P[tris[t + 2]] - P[tris[t]]).normalized;
            int nEje = (Mathf.Abs(n.x) >= Mathf.Abs(n.y) && Mathf.Abs(n.x) >= Mathf.Abs(n.z)) ? 0 : (Mathf.Abs(n.y) >= Mathf.Abs(n.z) ? 1 : 2);
            for (int c = 0; c < 3; c++)
            {
                int i = tris[t + c]; Vector3 q = P[i];
                float u, v;
                if (nEje == eje) { u = q[(eje + 1) % 3]; v = q[(eje + 2) % 3]; }      // cara de punta
                else { u = q[eje]; v = q[3 - eje - nEje]; }                              // veta a lo largo del eje
                uv[i] = new Vector2(u / TILE_U + off, v / TILE_V + off * 0.5f);
            }
        }
    }

    // Tele: la cara de frente (+z, la que ve la camara) lleva la pantalla; las demas caras, plastico
    private static void UvTele(int[] tris, Dictionary<int, Vector3> P, Vector2[] uv, bool esCuerpo)
    {
        Vector3 mn = new Vector3(1e9f, 1e9f, 1e9f), mx = -mn;
        foreach (var kv in P) { mn = Vector3.Min(mn, kv.Value); mx = Vector3.Max(mx, kv.Value); }
        Vector3 sz = Vector3.Max(mx - mn, new Vector3(1e-4f, 1e-4f, 1e-4f));
        for (int t = 0; t < tris.Length; t += 3)
        {
            Vector3 n = Vector3.Cross(P[tris[t + 1]] - P[tris[t]], P[tris[t + 2]] - P[tris[t]]).normalized;
            float ax = Mathf.Abs(n.x), ay = Mathf.Abs(n.y), az = Mathf.Abs(n.z);
            bool frente = esCuerpo && az >= ax && az >= ay && n.z > 0f;
            for (int c = 0; c < 3; c++)
            {
                int i = tris[t + c]; Vector3 q = P[i];
                float u, v;
                if (az >= ax && az >= ay) { u = (mx.x - q.x) / sz.x; v = (q.y - mn.y) / sz.y; }   // derecha de pantalla = -x
                else if (ax >= ay) { u = (q.z - mn.z) / sz.z; v = (q.y - mn.y) / sz.y; }
                else { u = (q.x - mn.x) / sz.x; v = (q.z - mn.z) / sz.z; }
                u = Mathf.Clamp01(u); v = Mathf.Clamp01(v);
                uv[i] = new Vector2(0.01f + u * 0.98f, frente ? V_FRENTE + (1f - V_FRENTE) * v : 0.01f + (V_FRENTE - 0.02f) * v);
            }
        }
    }


    public static void Configurar()
    {
        SalaTexturizador.AsegurarCarpetas();

        Preparar("Sala_mueble_madera.png", false, TextureWrapMode.Repeat);
        Preparar("Sala_mueble_madera_n.png", true, TextureWrapMode.Repeat);
        Preparar("Sala_tele.png", false, TextureWrapMode.Clamp);

        Material m = CrearMaterial("Sala_mueble_madera");
        m.SetTexture("_BaseMap", Cargar("Sala_mueble_madera.png"));
        Texture2D n = Cargar("Sala_mueble_madera_n.png");
        m.SetTexture("_BumpMap", null); m.DisableKeyword("_NORMALMAP");
        m.SetFloat("_Metallic", 0f); m.SetFloat("_Smoothness", 0.15f);
        EditorUtility.SetDirty(m);

        Material t = CrearMaterial("Sala_tele");
        t.SetTexture("_BaseMap", Cargar("Sala_tele.png"));
        t.SetFloat("_Metallic", 0f); t.SetFloat("_Smoothness", 0.35f);
        EditorUtility.SetDirty(t);
        AssetDatabase.SaveAssets();

    }

    private static Texture2D Cargar(string archivo) { return AssetDatabase.LoadAssetAtPath<Texture2D>(CARPETA_TEX + archivo); }

    private static void Preparar(string archivo, bool normal, TextureWrapMode wrap)
    {
        var imp = AssetImporter.GetAtPath(CARPETA_TEX + archivo) as TextureImporter;
        if (imp == null) { Debug.LogWarning("[TexturizarMuebles] falta " + CARPETA_TEX + archivo); return; }
        if (normal) imp.textureType = TextureImporterType.NormalMap;
        imp.wrapMode = wrap; imp.maxTextureSize = 1024;
        imp.SaveAndReimport();
    }

    private static Material CrearMaterial(string nombre)
    {
        string ruta = CARPETA_MAT + "/" + nombre + ".mat";
        Material m = AssetDatabase.LoadAssetAtPath<Material>(ruta);
        if (m == null) { m = new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(m, ruta); }
        m.SetTextureScale("_BaseMap", Vector2.one);
        m.SetColor("_BaseColor", Color.white);
        return m;
    }
}
