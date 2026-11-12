using UnityEditor;
using UnityEngine;

/// <summary>
/// Herramienta de editor: el escudo (Assets/Modelos/escudo/escudo.fbx) venia sin textura.
/// Pinta una textura que respeta el mapeo UV del modelo (se calcula por pixel a partir de la posicion 3D de cada punto):
/// cara con sectores rojo y crema, centro de metal, aro de acero con remaches, y el reverso de madera y cuero.
/// Crea escudo_color.png y escudo.mat y los asigna al material "M_Medieval_Shield" del FBX.
/// Menu: Herramientas/Texturizar escudo
/// </summary>
public static class TexturizarEscudo
{
    private const string RUTA_FBX = "Assets/Modelos/escudo/escudo.fbx";
    private const string RUTA_PNG = "Assets/Modelos/escudo/escudo_color.png";
    private const string RUTA_MAT = "Assets/Modelos/escudo/escudo.mat";
    private const int TAM = 1024;

    [MenuItem("Herramientas/Texturizar escudo")]
    public static void Ejecutar()
    {
        Debug.Log(Generar());
    }

    public static string Generar()
    {
        GameObject fbx = AssetDatabase.LoadAssetAtPath<GameObject>(RUTA_FBX);
        if (fbx == null) return "No se encontro " + RUTA_FBX;
        MeshFilter mf = fbx.GetComponentInChildren<MeshFilter>();
        if (mf == null || mf.sharedMesh == null) return "El escudo no tiene malla";
        Mesh malla = mf.sharedMesh;
        Vector3[] v = malla.vertices;
        Vector2[] uv = malla.uv;
        int[] tri = malla.triangles;
        if (uv.Length != v.Length) return "El escudo no tiene UV";

        // el eje mas delgado es el grosor; los otros dos forman la cara
        Bounds b = malla.bounds;
        Vector3 s = b.size;
        int delgado = s.x < s.y && s.x < s.z ? 0 : (s.y < s.z ? 1 : 2);
        int ea = delgado == 0 ? 1 : 0;
        int eb = delgado == 2 ? 1 : 2;
        if (delgado == 1) { ea = 0; eb = 2; }
        float radio = Mathf.Max(s[ea], s[eb]) * 0.5f;
        float mitadGrosor = s[delgado] * 0.5f;

        Color32[] px = new Color32[TAM * TAM];
        bool[] pintado = new bool[TAM * TAM];

        for (int i = 0; i < tri.Length; i += 3)
        {
            int i0 = tri[i], i1 = tri[i + 1], i2 = tri[i + 2];
            Vector2 p0 = uv[i0] * (TAM - 1), p1 = uv[i1] * (TAM - 1), p2 = uv[i2] * (TAM - 1);
            int minX = Mathf.Max(0, Mathf.FloorToInt(Mathf.Min(p0.x, Mathf.Min(p1.x, p2.x))) - 1);
            int maxX = Mathf.Min(TAM - 1, Mathf.CeilToInt(Mathf.Max(p0.x, Mathf.Max(p1.x, p2.x))) + 1);
            int minY = Mathf.Max(0, Mathf.FloorToInt(Mathf.Min(p0.y, Mathf.Min(p1.y, p2.y))) - 1);
            int maxY = Mathf.Min(TAM - 1, Mathf.CeilToInt(Mathf.Max(p0.y, Mathf.Max(p1.y, p2.y))) + 1);
            float den = (p1.y - p2.y) * (p0.x - p2.x) + (p2.x - p1.x) * (p0.y - p2.y);
            if (Mathf.Abs(den) < 1e-6f) continue;

            for (int y = minY; y <= maxY; y++)
            {
                for (int x = minX; x <= maxX; x++)
                {
                    float l0 = ((p1.y - p2.y) * (x - p2.x) + (p2.x - p1.x) * (y - p2.y)) / den;
                    float l1 = ((p2.y - p0.y) * (x - p2.x) + (p0.x - p2.x) * (y - p2.y)) / den;
                    float l2 = 1f - l0 - l1;
                    const float e = -0.02f;
                    if (l0 < e || l1 < e || l2 < e) continue;

                    Vector3 pos = v[i0] * l0 + v[i1] * l1 + v[i2] * l2;
                    float a = (pos[ea] - b.center[ea]) / radio;
                    float c = (pos[eb] - b.center[eb]) / radio;
                    float t = (pos[delgado] - b.center[delgado]) / mitadGrosor; // -1 un lado, +1 el otro
                    px[y * TAM + x] = Pintar(a, c, t);
                    pintado[y * TAM + x] = true;
                }
            }
        }

        // rellenar los bordes entre islas UV para que no se vean costuras
        for (int pasada = 0; pasada < 4; pasada++)
        {
            Color32[] copia = (Color32[])px.Clone();
            bool[] copiaP = (bool[])pintado.Clone();
            for (int y = 0; y < TAM; y++)
            {
                for (int x = 0; x < TAM; x++)
                {
                    int k = y * TAM + x;
                    if (copiaP[k]) continue;
                    for (int dy = -1; dy <= 1 && !pintado[k]; dy++)
                        for (int dx = -1; dx <= 1; dx++)
                        {
                            int xx = x + dx, yy = y + dy;
                            if (xx < 0 || yy < 0 || xx >= TAM || yy >= TAM) continue;
                            int kk = yy * TAM + xx;
                            if (copiaP[kk]) { px[k] = copia[kk]; pintado[k] = true; break; }
                        }
                }
            }
        }
        for (int k = 0; k < px.Length; k++) if (!pintado[k]) px[k] = new Color32(70, 48, 28, 255);

        Texture2D tex = new Texture2D(TAM, TAM, TextureFormat.RGBA32, false);
        tex.SetPixels32(px);
        System.IO.File.WriteAllBytes(RUTA_PNG, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);
        AssetDatabase.ImportAsset(RUTA_PNG, ImportAssetOptions.ForceUpdate);
        TextureImporter ti = (TextureImporter)AssetImporter.GetAtPath(RUTA_PNG);
        if (ti != null) { ti.sRGBTexture = true; ti.mipmapEnabled = true; ti.wrapMode = TextureWrapMode.Clamp; ti.SaveAndReimport(); }

        // material URP con la textura
        Texture2D textura = AssetDatabase.LoadAssetAtPath<Texture2D>(RUTA_PNG);
        Material mat = AssetDatabase.LoadAssetAtPath<Material>(RUTA_MAT);
        if (mat == null)
        {
            Shader sh = Shader.Find("Universal Render Pipeline/Lit");
            mat = new Material(sh);
            AssetDatabase.CreateAsset(mat, RUTA_MAT);
        }
        if (mat.HasProperty("_BaseMap")) mat.SetTexture("_BaseMap", textura);
        mat.mainTexture = textura;
        if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", Color.white);
        if (mat.HasProperty("_Metallic")) mat.SetFloat("_Metallic", 0.25f);
        if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", 0.4f);
        EditorUtility.SetDirty(mat);

        // el FBX usa este material en vez del que trae sin textura
        ModelImporter imp = (ModelImporter)AssetImporter.GetAtPath(RUTA_FBX);
        imp.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), "M_Medieval_Shield"), mat);
        imp.SaveAndReimport();
        AssetDatabase.SaveAssets();
        return "Escudo texturizado: " + RUTA_PNG + " + " + RUTA_MAT + " (grosor en eje " + delgado + ")";
    }

    // a, c: posicion en la cara normalizada (-1..1 en el borde); t: lado (-1..1)
    private static Color32 Pintar(float a, float c, float t)
    {
        float r = Mathf.Sqrt(a * a + c * c);
        float ang = Mathf.Atan2(c, a); // -pi..pi
        float ruido = Mathf.PerlinNoise(a * 7f + 11f, c * 7f + 3f);
        float grano = Mathf.PerlinNoise(a * 3f, c * 42f);

        Color acero = new Color(0.34f, 0.35f, 0.39f);
        Color aceroClaro = new Color(0.62f, 0.64f, 0.68f);
        Color oro = new Color(0.86f, 0.68f, 0.22f);

        // aro de acero y remaches (en ambos lados)
        if (r > 0.86f)
        {
            Color col = Color.Lerp(acero, aceroClaro, ruido * 0.6f);
            if (r > 0.96f) col *= 0.7f;
            float sector = Mathf.Repeat(ang / (Mathf.PI * 2f) * 20f, 1f);
            float dr = r - 0.905f;
            float da = (sector - 0.5f) * 0.31f;
            if (dr * dr + da * da < 0.0016f) col = Color.Lerp(aceroClaro, Color.white, 0.4f);
            return col;
        }

        bool frente = t > 0f;
        if (frente)
        {
            // centro de metal con aro dorado
            if (r < 0.15f)
            {
                float brillo = Mathf.Lerp(0.45f, 0.95f, 1f - r / 0.15f);
                return new Color(brillo * 0.78f, brillo * 0.80f, brillo * 0.85f);
            }
            if (r < 0.2f) return Color.Lerp(oro, oro * 0.6f, ruido);

            // 8 sectores rojo y crema, separados por lineas oscuras
            float u = Mathf.Repeat(ang / (Mathf.PI * 2f) * 8f, 1f);
            int cual = Mathf.FloorToInt(Mathf.Repeat(ang / (Mathf.PI * 2f) * 8f, 2f));
            if (u < 0.035f || u > 0.965f) return new Color(0.16f, 0.09f, 0.04f);
            Color baseCol = cual == 0 ? new Color(0.66f, 0.09f, 0.09f) : new Color(0.90f, 0.80f, 0.55f);
            baseCol *= Mathf.Lerp(0.82f, 1.05f, ruido);
            // anillo dorado a media cara
            if (Mathf.Abs(r - 0.55f) < 0.022f) return oro;
            return baseCol * Mathf.Lerp(0.95f, 1.03f, grano);
        }

        // reverso: madera con tablas y una agarradera de cuero en el centro
        if (r < 0.2f) return Color.Lerp(new Color(0.22f, 0.13f, 0.07f), new Color(0.32f, 0.2f, 0.1f), ruido);
        float tabla = Mathf.Repeat(a * 3.2f, 1f);
        Color madera = Color.Lerp(new Color(0.42f, 0.27f, 0.13f), new Color(0.56f, 0.38f, 0.19f), grano);
        if (tabla < 0.04f || tabla > 0.96f) madera = new Color(0.17f, 0.1f, 0.05f);
        if (Mathf.Abs(c) < 0.05f && r < 0.84f) madera = Color.Lerp(acero, aceroClaro, ruido); // refuerzo de metal
        return madera;
    }
}
