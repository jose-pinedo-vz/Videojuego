using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

// Texturizar los marcos de la sala: Cuadro1, Cuadro2, Cuadro3 (marco + imagen) y Marcocirc (marco circular + imagen).
//  Cuadro1 = paisaje con marco dorado | Cuadro2 = girasoles con marco de nogal | Cuadro3 = velero con marco blanco descascarado
//  Marcocirc = luna sobre el bosque con marco dorado.
// Las imagenes son pinturas originales (no obras reales), envejecidas: craquelado, manchas, moho y pintura desprendida.
// Los marcos en el FBX son cajas: la cara de frente del "lienzo" (material tela) lleva la imagen y el resto el marco.
// Marcocirc en el FBX es un aro vacio: este script le agrega, dentro del mismo objeto, un disco para la imagen.
// Parte del escenario Sala: se aplica con Herramientas -> Sala -> Texturizar toda la sala.
internal static class SalaMarcos
{
    private const string FBX = "/sala.fbx";
    private const string CARPETA_TEX = "Assets/Sala/Texturas/";
    private const string CARPETA_MAT = "Assets/Sala/Materiales";
    private const float TILE = 0.5f; // metros por repeticion de la textura del marco

    // objeto -> (imagen, marco)
    private static bool Config(string objeto, out string imagen, out string marco)
    {
        string n = objeto.ToLower();
        imagen = marco = null;
        if (n == "cuadro1") { imagen = "cuadro1"; marco = "dorado"; }
        else if (n == "cuadro2") { imagen = "cuadro2"; marco = "nogal"; }
        else if (n == "cuadro3") { imagen = "cuadro3"; marco = "blanco"; }
        else if (n.StartsWith("marcocirc")) { imagen = "cuadro_circular"; marco = "dorado"; }
        return imagen != null;
    }

    public static void Aplicar(GameObject raiz)
    {
        int hechos = 0;
        foreach (MeshFilter mf in raiz.GetComponentsInChildren<MeshFilter>())
        {
            string imagen, marco;
            if (!Config(mf.gameObject.name, out imagen, out marco)) continue;
            Mesh mesh = mf.sharedMesh;
            Renderer rend = mf.GetComponent<Renderer>();
            if (mesh == null || rend == null) continue;

            Material matMarco = AssetDatabase.LoadAssetAtPath<Material>(CARPETA_MAT + "/Sala_marco_" + marco + ".mat");
            Material matImg = AssetDatabase.LoadAssetAtPath<Material>(CARPETA_MAT + "/Sala_" + imagen + ".mat");
            bool circular = mf.gameObject.name.ToLower().StartsWith("marcocirc");
            if (circular) { AjustarCircular(raiz, mf, mesh, rend, matMarco, matImg); hechos++; continue; }

            Material[] mats = rend.sharedMaterials;
            Vector3[] vert = mesh.vertices;
            Vector2[] uv = mesh.uv.Length == vert.Length ? mesh.uv : new Vector2[vert.Length];
            for (int s = 0; s < mesh.subMeshCount && s < mats.Length; s++)
            {
                if (mats[s] == null) continue;
                string nom = mats[s].name.ToLower();
                bool lienzo = nom.Contains("tela"), madera = nom.Contains("mueble"); // materiales reasignados: sala_tela, sala_mueble
                if (!lienzo && !madera) continue;
                int[] tris = mesh.GetTriangles(s);
                var P = new Dictionary<int, Vector3>();
                Vector3 mn = new Vector3(1e9f, 1e9f, 1e9f), mx = -mn;
                foreach (int i in tris)
                {
                    if (P.ContainsKey(i)) continue;
                    Vector3 p = raiz.transform.InverseTransformPoint(mf.transform.TransformPoint(vert[i]));
                    P[i] = p; mn = Vector3.Min(mn, p); mx = Vector3.Max(mx, p);
                }
                Vector3 sz = Vector3.Max(mx - mn, new Vector3(1e-4f, 1e-4f, 1e-4f));
                for (int t = 0; t < tris.Length; t += 3)
                {
                    Vector3 n = Vector3.Cross(P[tris[t + 1]] - P[tris[t]], P[tris[t + 2]] - P[tris[t]]).normalized;
                    float ax = Mathf.Abs(n.x), ay = Mathf.Abs(n.y), az = Mathf.Abs(n.z);
                    bool frente = az >= ax && az >= ay && n.z > 0f; // la camara esta en +z
                    for (int c = 0; c < 3; c++)
                    {
                        int i = tris[t + c]; Vector3 q = P[i];
                        if (lienzo)
                        {
                            // frente: imagen completa (derecha de la pantalla = -x); demas caras: borde de la imagen
                            if (frente) uv[i] = new Vector2(Mathf.Clamp01((mx.x - q.x) / sz.x), Mathf.Clamp01((q.y - mn.y) / sz.y));
                            else uv[i] = new Vector2(0.01f, 0.5f);
                        }
                        else
                        {
                            if (az >= ax && az >= ay) uv[i] = new Vector2(q.x, q.y) / TILE;
                            else if (ax >= ay) uv[i] = new Vector2(q.z, q.y) / TILE;
                            else uv[i] = new Vector2(q.x, q.z) / TILE;
                        }
                    }
                }
                Material nuevo = lienzo ? matImg : matMarco;
                if (nuevo != null) mats[s] = nuevo;
                hechos++;
            }
            SalaTexturizador.AsignarUV(mesh, uv);
            rend.sharedMaterials = mats;
        }
        Debug.Log("[TexturizarMarcos] " + hechos + " marcos texturizados");
    }

    // Marco circular: UV polares para el aro y un disco nuevo (submesh 2) con la imagen, dentro del mismo objeto
    private static void AjustarCircular(GameObject raiz, MeshFilter mf, Mesh mesh, Renderer rend, Material matMarco, Material matImg)
    {
        Vector3[] vert = mesh.vertices;
        Vector3[] nor = mesh.normals;
        int nv = vert.Length;
        var Pm = new Vector3[nv]; // posiciones en el espacio del modelo
        Vector3 mn = new Vector3(1e9f, 1e9f, 1e9f), mx = -mn;
        for (int i = 0; i < nv; i++)
        {
            Pm[i] = raiz.transform.InverseTransformPoint(mf.transform.TransformPoint(vert[i]));
            mn = Vector3.Min(mn, Pm[i]); mx = Vector3.Max(mx, Pm[i]);
        }
        Vector3 ctr = (mn + mx) * 0.5f;
        float rMin = 1e9f;
        for (int i = 0; i < nv; i++) rMin = Mathf.Min(rMin, Mathf.Sqrt((Pm[i].x - ctr.x) * (Pm[i].x - ctr.x) + (Pm[i].y - ctr.y) * (Pm[i].y - ctr.y)));
        float R = rMin + 0.012f; // el disco se mete un poco en la pared interior del aro

        // UV del aro: u = angulo (7 repeticiones), v = radio + profundidad
        var uv = new List<Vector2>(nv);
        for (int i = 0; i < nv; i++)
        {
            float dx = Pm[i].x - ctr.x, dy = Pm[i].y - ctr.y;
            float ang = Mathf.Atan2(dy, dx) / (2f * Mathf.PI); // -0.5 .. 0.5
            float r = Mathf.Sqrt(dx * dx + dy * dy);
            uv.Add(new Vector2(ang * 7f, (r + (Pm[i].z - ctr.z)) / TILE));
        }

        // disco: centro + 48 vertices, mirando a +z del modelo, con las dos orientaciones de triangulos
        const int SEG = 48;
        var verts = new List<Vector3>(vert);
        var norms = new List<Vector3>(nor.Length == nv ? nor : new Vector3[nv]);
        int baseIdx = verts.Count;
        Vector3 localCtr = mf.transform.InverseTransformPoint(raiz.transform.TransformPoint(ctr));
        Vector3 localN = mf.transform.InverseTransformDirection(raiz.transform.TransformDirection(Vector3.forward)).normalized;
        verts.Add(localCtr); norms.Add(localN); uv.Add(new Vector2(0.5f, 0.5f));
        for (int k = 0; k < SEG; k++)
        {
            float a = 2f * Mathf.PI * k / SEG;
            Vector3 pm = ctr + new Vector3(Mathf.Cos(a) * R, Mathf.Sin(a) * R, 0f);
            verts.Add(mf.transform.InverseTransformPoint(raiz.transform.TransformPoint(pm)));
            norms.Add(localN);
            // derecha de la pantalla = -x del modelo
            uv.Add(new Vector2(0.5f - Mathf.Cos(a) * 0.5f, 0.5f + Mathf.Sin(a) * 0.5f));
        }
        var tris = new List<int>();
        for (int k = 0; k < SEG; k++)
        {
            int a = baseIdx + 1 + k, b = baseIdx + 1 + (k + 1) % SEG;
            tris.Add(baseIdx); tris.Add(a); tris.Add(b);
            tris.Add(baseIdx); tris.Add(b); tris.Add(a);
        }
        int[] triAro = mesh.GetTriangles(0);
        mesh.SetVertices(verts);
        mesh.SetNormals(norms);
        mesh.SetUVs(0, uv);
        mesh.subMeshCount = 2;
        mesh.SetTriangles(triAro, 0);
        mesh.SetTriangles(tris, 1);
        mesh.RecalculateBounds();
        rend.sharedMaterials = new[] { matMarco != null ? matMarco : rend.sharedMaterials[0], matImg != null ? matImg : rend.sharedMaterials[0] };
    }


    public static void Configurar()
    {
        SalaTexturizador.AsegurarCarpetas();
        foreach (string m in new[] { "dorado", "nogal", "blanco" }) Crear("Sala_marco_" + m, TextureWrapMode.Repeat, 0.2f, m == "dorado" ? 0.25f : 0.12f);
        foreach (string i in new[] { "cuadro1", "cuadro2", "cuadro3", "cuadro_circular" }) Crear("Sala_" + i, TextureWrapMode.Clamp, 0f, 0.3f);
        AssetDatabase.SaveAssets();

    }

    private static void Crear(string nombre, TextureWrapMode wrap, float metalico, float brillo)
    {
        string rutaTex = CARPETA_TEX + nombre + ".png";
        var imp = AssetImporter.GetAtPath(rutaTex) as TextureImporter;
        if (imp == null) { Debug.LogWarning("[TexturizarMarcos] falta " + rutaTex); return; }
        imp.wrapMode = wrap; imp.maxTextureSize = 1024; imp.SaveAndReimport();

        string rutaMat = CARPETA_MAT + "/" + nombre + ".mat";
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
