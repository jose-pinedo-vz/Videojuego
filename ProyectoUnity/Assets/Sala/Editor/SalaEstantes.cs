using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

// Texturizar los estantes de madera de la sala: Repisa1, Repisa2 y Librero.002 (solo su madera; los libros no se tocan).
// La veta corre a lo largo de cada tabla (eje mas largo). Al importar Assets/sala.fbx se generan UV y se asigna
// el material nuevo solo a esos objetos. Las demas piezas de madera (mesa, silla, mueble, cuadros...) no se tocan.
// Parte del escenario Sala: se aplica con Herramientas -> Sala -> Texturizar toda la sala.
internal static class SalaEstantes
{
    private const string FBX = "/sala.fbx";
    private const string RUTA_TEX = "Assets/Sala/Texturas/Sala_estante_madera.png";
    private const string RUTA_NRM = "Assets/Sala/Texturas/Sala_estante_madera_n.png";
    private const string CARPETA_MAT = "Assets/Sala/Materiales";
    private const string RUTA_MAT = "Assets/Sala/Materiales/Sala_estante_madera.mat";
    private const float TILE_U = 1.5f; // metros por repeticion a lo largo de la veta
    private const float TILE_V = 0.6f; // metros por repeticion a lo ancho

    private static bool EsEstante(string n)
    {
        n = n.ToLower();
        return n.StartsWith("repisa") || n.StartsWith("librero");
    }

    public static void Aplicar(GameObject raiz)
    {
        Material mat = AssetDatabase.LoadAssetAtPath<Material>(RUTA_MAT);
        int tablas = 0;

        foreach (MeshFilter mf in raiz.GetComponentsInChildren<MeshFilter>())
        {
            if (!EsEstante(mf.gameObject.name)) continue;
            Mesh mesh = mf.sharedMesh;
            Renderer rend = mf.GetComponent<Renderer>();
            if (mesh == null || rend == null) continue;

            Material[] mats = rend.sharedMaterials;
            Vector3[] vert = mesh.vertices;
            Vector2[] uv = mesh.uv.Length == vert.Length ? mesh.uv : new Vector2[vert.Length];
            bool cambio = false;

            for (int s = 0; s < mesh.subMeshCount && s < mats.Length; s++)
            {
                // sala.fbx tiene el material "mueble" reasignado a "sala_mueble"
                if (mats[s] == null || !mats[s].name.ToLower().Contains("mueble")) continue;
                int[] tris = mesh.GetTriangles(s);

                var P = new Dictionary<int, Vector3>();
                foreach (int i in tris)
                    if (!P.ContainsKey(i)) P[i] = raiz.transform.InverseTransformPoint(mf.transform.TransformPoint(vert[i]));

                // una tabla = grupo de triangulos conectados por posicion
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
                var lo = new Dictionary<Vector3Int, Vector3>();
                var hi = new Dictionary<Vector3Int, Vector3>();
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
                    int eje = (sz.x >= sz.y && sz.x >= sz.z) ? 0 : (sz.y >= sz.z ? 1 : 2); // direccion de la veta
                    // desplazamiento distinto por tabla para que no se repita el mismo trozo
                    Vector3 ctr = (lo[g] + hi[g]) * 0.5f;
                    float off = Mathf.Abs(Mathf.Sin(ctr.x * 12.9898f + ctr.y * 78.233f + ctr.z * 37.719f) * 43758.5453f) % 1f;

                    Vector3 n = Vector3.Cross(P[tris[t + 1]] - P[tris[t]], P[tris[t + 2]] - P[tris[t]]).normalized;
                    int nEje = (Mathf.Abs(n.x) >= Mathf.Abs(n.y) && Mathf.Abs(n.x) >= Mathf.Abs(n.z)) ? 0 : (Mathf.Abs(n.y) >= Mathf.Abs(n.z) ? 1 : 2);

                    for (int c = 0; c < 3; c++)
                    {
                        int i = tris[t + c];
                        Vector3 q = P[i];
                        float u, v;
                        if (nEje == eje)
                        {
                            // cara de punta (corte transversal): se usan los dos ejes del plano
                            int a1 = (eje + 1) % 3, a2 = (eje + 2) % 3;
                            u = q[a1]; v = q[a2];
                        }
                        else
                        {
                            int resto = 3 - eje - nEje; // tercer eje
                            u = q[eje]; v = q[resto];
                        }
                        uv[i] = new Vector2(u / TILE_U + off, v / TILE_V + off * 0.5f);
                    }
                    cambio = true;
                }
                if (mat != null) mats[s] = mat;
                tablas++;
            }
            if (cambio) { SalaTexturizador.AsignarUV(mesh, uv); rend.sharedMaterials = mats; }
        }
        Debug.Log("[TexturizarEstantes] " + tablas + " partes de estante texturizadas");
    }


    public static void Configurar()
    {
        SalaTexturizador.AsegurarCarpetas();

        var imp = AssetImporter.GetAtPath(RUTA_TEX) as TextureImporter;
        if (imp == null) { Debug.LogError("[TexturizarEstantes] falta " + RUTA_TEX); return; }
        imp.wrapMode = TextureWrapMode.Repeat; imp.maxTextureSize = 1024; imp.SaveAndReimport();
        var impN = AssetImporter.GetAtPath(RUTA_NRM) as TextureImporter;
        if (impN != null)
        {
            impN.textureType = TextureImporterType.NormalMap;
            impN.wrapMode = TextureWrapMode.Repeat; impN.maxTextureSize = 1024; impN.SaveAndReimport();
        }

        Material m = AssetDatabase.LoadAssetAtPath<Material>(RUTA_MAT);
        if (m == null) { m = new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(m, RUTA_MAT); }
        m.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(RUTA_TEX));
        m.SetTextureScale("_BaseMap", Vector2.one);
        m.SetColor("_BaseColor", Color.white);
        Texture2D nrm = AssetDatabase.LoadAssetAtPath<Texture2D>(RUTA_NRM);
        m.SetTexture("_BumpMap", null); m.DisableKeyword("_NORMALMAP"); // sin mapa de normales: evita brillos raros en el librero
        m.SetFloat("_Metallic", 0f);
        m.SetFloat("_Smoothness", 0.12f);
        EditorUtility.SetDirty(m);
        AssetDatabase.SaveAssets();

    }
}
