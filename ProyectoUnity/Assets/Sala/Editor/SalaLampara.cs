using UnityEditor;
using UnityEngine;

// Texturizar la lampara de la sala (solo el objeto "Lampara"; el resto de sala.fbx no se toca).
// Pie y poste = laton viejo con oxido y pintura descascarada. Pantalla = tela amarillenta con manchas, quemadura y roto.
// Al importar Assets/sala.fbx se generan UV y se asignan dos materiales nuevos solo a ese objeto.
// Parte del escenario Sala: se aplica con Herramientas -> Sala -> Texturizar toda la sala.
internal static class SalaLampara
{
    private const string FBX = "/sala.fbx";
    private const string CARPETA_TEX = "Assets/Sala/Texturas/";
    private const string CARPETA_MAT = "Assets/Sala/Materiales";
    private const float TILE_METAL = 0.5f; // metros por repeticion de la textura de metal

    public static void Aplicar(GameObject raiz)
    {
        Material metal = AssetDatabase.LoadAssetAtPath<Material>(CARPETA_MAT + "/Sala_lampara_metal.mat");
        Material pantalla = AssetDatabase.LoadAssetAtPath<Material>(CARPETA_MAT + "/Sala_lampara_pantalla.mat");
        int hechos = 0;

        foreach (MeshFilter mf in raiz.GetComponentsInChildren<MeshFilter>())
        {
            if (!mf.gameObject.name.ToLower().StartsWith("lampara")) continue; // no toca el candelabro ni otros objetos
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
                // sala.fbx tiene los materiales reasignados a "sala_cocreto" (metal) y "sala_tela" (pantalla)
                bool esPantalla = nom.Contains("tela");
                bool esMetal = nom.Contains("cocreto");
                if (!esPantalla && !esMetal) continue;

                int[] tris = mesh.GetTriangles(s);
                Vector3 mn = new Vector3(1e9f, 1e9f, 1e9f), mx = -mn;
                var P = new System.Collections.Generic.Dictionary<int, Vector3>();
                foreach (int i in tris)
                {
                    if (P.ContainsKey(i)) continue;
                    Vector3 p = raiz.transform.InverseTransformPoint(mf.transform.TransformPoint(vert[i]));
                    P[i] = p; mn = Vector3.Min(mn, p); mx = Vector3.Max(mx, p);
                }
                Vector3 sz = Vector3.Max(mx - mn, new Vector3(1e-4f, 1e-4f, 1e-4f));

                for (int t = 0; t < tris.Length; t += 3)
                {
                    // normal geometrica del triangulo (no depende de las normales suavizadas)
                    Vector3 n = Vector3.Cross(P[tris[t + 1]] - P[tris[t]], P[tris[t + 2]] - P[tris[t]]).normalized;
                    float ax = Mathf.Abs(n.x), ay = Mathf.Abs(n.y), az = Mathf.Abs(n.z);
                    for (int c = 0; c < 3; c++)
                    {
                        int i = tris[t + c];
                        Vector3 q = P[i];
                        Vector2 r;
                        if (esPantalla)
                        {
                            // cada cara de la pantalla muestra la textura completa
                            if (az >= ax && az >= ay) r = new Vector2((q.x - mn.x) / sz.x, (q.y - mn.y) / sz.y);
                            else if (ax >= ay) r = new Vector2((q.z - mn.z) / sz.z, (q.y - mn.y) / sz.y);
                            else r = new Vector2((q.x - mn.x) / sz.x, (q.z - mn.z) / sz.z);
                        }
                        else
                        {
                            // metal: proyeccion de caja en metros
                            if (az >= ax && az >= ay) r = new Vector2(q.x, q.y) / TILE_METAL;
                            else if (ax >= ay) r = new Vector2(q.z, q.y) / TILE_METAL;
                            else r = new Vector2(q.x, q.z) / TILE_METAL;
                        }
                        uv[i] = r;
                    }
                }
                Material nuevo = esPantalla ? pantalla : metal;
                if (nuevo != null) mats[s] = nuevo;
                hechos++;
            }
            SalaTexturizador.AsignarUV(mesh, uv);
            rend.sharedMaterials = mats;
        }
        Debug.Log("[TexturizarLampara] " + hechos + " partes de la lampara texturizadas");
    }


    public static void Configurar()
    {
        SalaTexturizador.AsegurarCarpetas();
        Crear("metal", TextureWrapMode.Repeat, 0.25f, 0.3f);
        Crear("pantalla", TextureWrapMode.Clamp, 0.0f, 0.05f);
        AssetDatabase.SaveAssets();

    }

    private static void Crear(string clave, TextureWrapMode wrap, float metalico, float brillo)
    {
        string rutaTex = CARPETA_TEX + "Sala_lampara_" + clave + ".png";
        var imp = AssetImporter.GetAtPath(rutaTex) as TextureImporter;
        if (imp == null) { Debug.LogError("[TexturizarLampara] falta " + rutaTex); return; }
        imp.wrapMode = wrap;
        imp.maxTextureSize = 1024;
        imp.SaveAndReimport();

        string rutaMat = CARPETA_MAT + "/Sala_lampara_" + clave + ".mat";
        Material m = AssetDatabase.LoadAssetAtPath<Material>(rutaMat);
        if (m == null) { m = new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(m, rutaMat); }
        m.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(rutaTex));
        m.SetTextureScale("_BaseMap", Vector2.one);
        m.SetColor("_BaseColor", Color.white);
        m.SetFloat("_Metallic", metalico);
        m.SetFloat("_Smoothness", brillo);
        EditorUtility.SetDirty(m);
    }
}
