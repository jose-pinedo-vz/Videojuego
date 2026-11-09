using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

// Corrige las piezas de los mapas que no traen material (se ven en morado/rosa en URP):
//  - Sala:   Piso, Techo, Paredizq y Paredderec  -> materiales nuevos con textura (sala_piso, sala_techo, sala_pared)
//  - Cocina: Pared_der y Pared_izqui             -> Cocina_semtermin_colorpared (el material de pared de esa misma cocina)
// Ademas, en los 4 mapas cualquier renderer sin material (o con un hueco vacio) recibe uno de respaldo,
// asi que no queda ninguna pieza sin textura. Se aplica al importar el FBX, la escena no se edita.
public class CorregirMaterialesMapas : AssetPostprocessor
{
    private const string CARPETA = "Assets/Materiales/Caricatura/";
    private const string TEX = "Assets/Texturas/Caricatura/";

    private const string FBX_SALA = "Assets/sala.fbx";
    private const string FBX_COCINA = "Assets/Cocina_semtermin.fbx";
    private const string FBX_PATIO = "Assets/Patio.fbx";
    private const string FBX_COCHERA = "Assets/Cochera.fbx";

    // ---------- creacion de los materiales nuevos ----------

    [InitializeOnLoadMethod]
    private static void CrearSiHaceFalta()
    {
        EditorApplication.delayCall += () =>
        {
            bool nuevos = AsegurarMaterial("sala_piso", TEX + "Cartoon_madera.png", new Color(0.85f, 0.62f, 0.38f), new Vector2(7f, 2f));
            nuevos |= AsegurarMaterial("sala_pared", TEX + "Cartoon_liso.png", new Color(0.93f, 0.85f, 0.70f), new Vector2(2f, 3f));
            nuevos |= AsegurarMaterial("sala_techo", TEX + "Cartoon_liso.png", new Color(0.98f, 0.94f, 0.86f), new Vector2(7f, 2f));
            // Se reimportan los mapas una vez por cada version de esta correccion
            const string clave = "CorregirMaterialesMapas_v3";
            if (nuevos || EditorPrefs.GetInt(clave, 0) == 0)
            {
                EditorPrefs.SetInt(clave, 1);
                CorregirAhora();
            }
        };
    }

    [MenuItem("Herramientas/Corregir materiales de mapas")]
    private static void CorregirAhora()
    {
        AssetDatabase.ImportAsset(FBX_SALA, ImportAssetOptions.ForceUpdate);
        AssetDatabase.ImportAsset(FBX_COCINA, ImportAssetOptions.ForceUpdate);
        AssetDatabase.ImportAsset(FBX_PATIO, ImportAssetOptions.ForceUpdate);
        AssetDatabase.ImportAsset(FBX_COCHERA, ImportAssetOptions.ForceUpdate);
        Debug.Log("CorregirMaterialesMapas: materiales de sala/cocina revisados y aplicados.");
    }

    private static bool AsegurarMaterial(string nombre, string rutaTextura, Color color, Vector2 repeticion)
    {
        string ruta = CARPETA + nombre + ".mat";
        if (AssetDatabase.LoadAssetAtPath<Material>(ruta) != null) return false;

        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null) return false;

        Material m = new Material(shader) { name = nombre };
        Texture2D textura = AssetDatabase.LoadAssetAtPath<Texture2D>(rutaTextura);
        m.SetColor("_BaseColor", color);
        if (textura != null)
        {
            m.SetTexture("_BaseMap", textura);
            m.SetTextureScale("_BaseMap", repeticion);
        }
        m.SetFloat("_Smoothness", 0.15f);
        m.SetFloat("_Metallic", 0f);
        AssetDatabase.CreateAsset(m, ruta);
        AssetDatabase.SaveAssets();
        return true;
    }

    // ---------- asignacion al importar ----------

    private void OnPostprocessModel(GameObject raiz)
    {
        bool sala = assetPath == FBX_SALA;
        bool cocina = assetPath == FBX_COCINA;
        if (!sala && !cocina && assetPath != FBX_PATIO && assetPath != FBX_COCHERA) return;

        if (sala) OnPostprocessModel_UV(raiz);

        Material piso = Cargar("sala_piso");
        Material pared = Cargar("sala_pared");
        Material techo = Cargar("sala_techo");
        Material paredCocina = Cargar("Cocina_semtermin_colorpared");

        // material de respaldo por mapa (el de pared de ese mismo mapa)
        Material respaldo = sala ? pared : (cocina ? paredCocina : Cargar(assetPath == FBX_PATIO ? "Patio_Concreto" : "T_Paredes"));
        if (respaldo == null) respaldo = Cargar("sala_borde");

        foreach (Renderer r in raiz.GetComponentsInChildren<Renderer>(true))
        {
            Material[] actuales = r.sharedMaterials;
            if (actuales == null || actuales.Length == 0) actuales = new Material[1];

            // Unity no deja el slot en null: a las piezas sin material les pone el "Default-Material" del
            // motor (que en URP sale morado/rosa). Por eso tambien se reemplaza ese material y las piezas
            // conocidas por nombre.
            bool pieza_conocida = EsPiezaSinMaterial(r.name, sala, cocina);

            bool cambio = false;
            for (int i = 0; i < actuales.Length; i++)
            {
                if (!pieza_conocida && !EsMaterialInvalido(actuales[i])) continue;

                string n = r.name.ToLowerInvariant();
                Material elegido;
                if (n.Contains("piso")) elegido = sala ? piso : Cargar("Cocina_semtermin_piso");
                else if (n.Contains("techo")) elegido = sala ? techo : Cargar("Cocina_semtermin_techo");
                else if (n.Contains("pared")) elegido = sala ? pared : paredCocina;
                else elegido = respaldo;

                if (elegido == null) elegido = respaldo;
                actuales[i] = elegido;
                cambio = true;
            }
            if (cambio) r.sharedMaterials = actuales;
        }
    }

    private static bool EsPiezaSinMaterial(string nombre, bool sala, bool cocina)
    {
        string n = nombre.ToLowerInvariant();
        if (sala) return n == "piso" || n == "techo" || n == "paredizq" || n == "paredderec";
        if (cocina) return n == "pared_der" || n == "pared_izqui";
        return false;
    }

    private static bool EsMaterialInvalido(Material m)
    {
        if (m == null) return true;
        string n = m.name;
        if (string.IsNullOrEmpty(n) || n.StartsWith("Default") || n.StartsWith("No Name")) return true;
        return m.shader == null || m.shader.name == "Hidden/InternalErrorShader";
    }

    // Las piezas pequeñas de la sala (libros, cuadros, mesas, tele...) vienen sin coordenadas UV, asi que la
    // textura no se dibuja bien. Sus vertices estan compartidos entre caras (un cubo tiene 8), asi que primero se
    // separan los vertices por triangulo y luego cada triangulo se proyecta segun la direccion de SU cara.
    private void OnPostprocessModel_UV(GameObject raiz)
    {
        foreach (MeshFilter mf in raiz.GetComponentsInChildren<MeshFilter>(true))
        {
            Mesh mesh = mf.sharedMesh;
            if (mesh == null || mesh.vertexCount == 0) continue;
            if (mesh.uv != null && mesh.uv.Length == mesh.vertexCount) continue;

            Vector3[] v = mesh.vertices;
            Vector3[] n = mesh.normals;
            bool hayNormales = n != null && n.Length == v.Length;
            int subs = mesh.subMeshCount;

            List<Vector3> nv = new List<Vector3>();
            List<Vector3> nn = new List<Vector3>();
            List<Vector2> nuv = new List<Vector2>();
            List<int>[] tris = new List<int>[subs];
            const float escala = 0.5f; // 1 repeticion de la textura cada 2 metros

            for (int sm = 0; sm < subs; sm++)
            {
                tris[sm] = new List<int>();
                int[] t = mesh.GetTriangles(sm);
                for (int i = 0; i + 2 < t.Length; i += 3)
                {
                    Vector3 p0 = v[t[i]], p1 = v[t[i + 1]], p2 = v[t[i + 2]];
                    Vector3 cara = Vector3.Cross(p1 - p0, p2 - p0).normalized;
                    Vector3 a = new Vector3(Mathf.Abs(cara.x), Mathf.Abs(cara.y), Mathf.Abs(cara.z));

                    for (int k = 0; k < 3; k++)
                    {
                        Vector3 pos = v[t[i + k]];
                        Vector2 uv;
                        if (a.x >= a.y && a.x >= a.z) uv = new Vector2(pos.z, pos.y);
                        else if (a.y >= a.z) uv = new Vector2(pos.x, pos.z);
                        else uv = new Vector2(pos.x, pos.y);

                        tris[sm].Add(nv.Count);
                        nv.Add(pos);
                        nn.Add(hayNormales ? n[t[i + k]] : cara);
                        nuv.Add(uv * escala);
                    }
                }
            }

            mesh.Clear();
            mesh.indexFormat = nv.Count > 65000 ? UnityEngine.Rendering.IndexFormat.UInt32 : UnityEngine.Rendering.IndexFormat.UInt16;
            mesh.SetVertices(nv);
            mesh.SetNormals(nn);
            mesh.SetUVs(0, nuv);
            mesh.subMeshCount = subs;
            for (int sm = 0; sm < subs; sm++) mesh.SetTriangles(tris[sm], sm);
            mesh.RecalculateBounds();
        }
    }

    private static Material Cargar(string nombre)
    {
        return AssetDatabase.LoadAssetAtPath<Material>(CARPETA + nombre + ".mat");
    }
}
