using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// Herramientas -> Aplicar fondos y texturas caricatura.
// 1) Crea un Poster_fondo (copia del de la cochera) en ecenaPatio y ecenaCosina con su imagen.
// 2) Da materiales con textura caricaturesca a los modelos que no tenian (patio, cocina, sala, gato, armas).
public static class AplicarFondosYTexturas
{
    private const string CARPETA_MAT = "Assets/Materiales/Caricatura";
    private const string CARPETA_TEX = "Assets/Texturas/Caricatura/";

    private static readonly string[] MODELOS =
    {
        "Assets/Patio.fbx", "Assets/Cocina_semtermin.fbx", "Assets/sala.fbx", "Assets/Personaje gato.fbx",
        "Assets/Modelos/gato/Personaje gato.fbx", "Assets/Modelos/mac10/mac10.fbx",
        "Assets/Modelos/bereta/bereta.fbx", "Assets/Modelos/raygun/raygun.fbx"
    };

    // palabra del nombre del material -> (textura, color). Primera coincidencia gana.
    private static readonly (string clave, string tex, string hex)[] REGLAS =
    {
        ("nube", "nubes", "FFFFFF"), ("azul", "liso", "5DB7F0"), ("hoja", "hojas", "4CC04A"), ("arbusto", "hojas", "3FAE4F"),
        ("madera", "madera", "C98A4B"), ("caja", "madera", "D9A05B"), ("mueble", "madera", "B9783F"), ("repisa", "madera", "C08A4E"),
        ("escalera", "madera", "C69558"), ("sombrilla", "tela", "F2564B"), ("tela", "tela", "E5605A"), ("sillon", "tela", "6FA8DC"),
        ("piedra", "piedra", "A9A9B5"), ("concreto", "piedra", "B5B5BF"), ("cocreto", "piedra", "B5B5BF"), ("barda", "ladrillo", "E08A63"),
        ("pared", "ladrillo", "F0C987"), ("paredes", "ladrillo", "F0C987"), ("techo", "liso", "D8C3A5"), ("piso", "baldosa", "C9B79C"),
        ("metal", "metal", "9AA5B1"), ("fierro", "metal", "8E99A6"), ("plata", "metal", "C8D0D8"), ("tubos", "metal", "9AA5B1"),
        ("oxidado", "metal", "C0794A"), ("refri", "metal", "F2E6D8"), ("negro", "metal", "4A4A55"), ("polimero", "liso", "3C3F58"),
        ("libros", "tela", "E2735B"), ("tele", "metal", "3B3F4A"), ("borde", "liso", "E9D8B8"), ("ventana", "liso", "9AD6F0"),
        ("escombro", "piedra", "9C8F85"), ("muebles", "madera", "B9783F"),
        ("aguila", "pelaje", "A66A3A"), ("plumas", "pelaje", "4A90D9"), ("pecho", "pelaje", "FFFFFF"), ("cabeza", "pelaje", "FFB347"),
        ("pico", "liso", "FFA726"), ("ave", "pelaje", "4A90D9"), ("ojo", "liso", "222222"),
        ("garra", "liso", "FFEBD1"), ("bigote", "liso", "FFF5E6"), ("boca", "liso", "8A1F2B"), ("diente", "liso", "FFFFFF"),
        ("color", "liso", "E4572E"), ("imagen", "liso", "F4D35E"), ("colorpared", "ladrillo", "F0C987"),
    };

    [MenuItem("Herramientas/Aplicar fondos y texturas caricatura")]
    public static void Ejecutar()
    {
        AplicarTexturas();
        AplicarFondos();
        AssetDatabase.SaveAssets();
        Debug.Log("[AplicarFondosYTexturas] listo");
    }

    private static void ImportarTexturas()
    {
        foreach (string g in AssetDatabase.FindAssets("t:Texture2D", new[] { "Assets/Texturas/Caricatura", "Assets/fondos" }))
            AssetDatabase.ImportAsset(AssetDatabase.GUIDToAssetPath(g), ImportAssetOptions.ForceSynchronousImport);
    }

    private static void AplicarTexturas()
    {
        ImportarTexturas();
        if (!AssetDatabase.IsValidFolder(CARPETA_MAT)) AssetDatabase.CreateFolder("Assets/Materiales", "Caricatura");
        Shader urp = Shader.Find("Universal Render Pipeline/Lit");
        var cache = new Dictionary<string, Material>();

        foreach (string ruta in MODELOS)
        {
            var imp = AssetImporter.GetAtPath(ruta) as ModelImporter;
            if (imp == null) { Debug.LogWarning("No existe " + ruta); continue; }
            string modelo = System.IO.Path.GetFileNameWithoutExtension(ruta);
            foreach (Object o in AssetDatabase.LoadAllAssetsAtPath(ruta))
            {
                if (!(o is Material orig)) continue;
                string nombre = orig.name;
                string baja = nombre.ToLower();
                string tex = "liso"; Color color = Color.white; bool regla = false;
                foreach (var r in REGLAS)
                    if (baja.Contains(r.clave)) { tex = r.tex; ColorUtility.TryParseHtmlString("#" + r.hex, out color); regla = true; break; }
                if (!regla)
                {   // sin regla: se conserva el color original, pero con textura
                    Color c = orig.HasProperty("_BaseColor") ? orig.GetColor("_BaseColor") : orig.color;
                    color = c; tex = "liso";
                    if (baja.Contains("vidrio")) { color = new Color(0.6f, 0.9f, 0.95f); }
                }
                string clave = modelo + "_" + nombre;
                string rutaMat = CARPETA_MAT + "/" + SanearNombre(clave) + ".mat";
                Material m = AssetDatabase.LoadAssetAtPath<Material>(rutaMat);
                if (m == null) { m = new Material(urp); AssetDatabase.CreateAsset(m, rutaMat); }
                Texture2D t = AssetDatabase.LoadAssetAtPath<Texture2D>(CARPETA_TEX + "Cartoon_" + tex + ".png");
                m.SetTexture("_BaseMap", t);
                m.SetColor("_BaseColor", color);
                m.SetFloat("_Smoothness", 0.15f); // mate, estilo caricatura
                m.SetFloat("_Metallic", 0f);
                EditorUtility.SetDirty(m);
                imp.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), nombre), m);
            }
            imp.SaveAndReimport();
            Debug.Log("[Texturas] " + ruta);
        }
    }

    private static string SanearNombre(string s)
    {
        foreach (char c in System.IO.Path.GetInvalidFileNameChars()) s = s.Replace(c, '_');
        return s.Replace(' ', '_');
    }

    private static void AplicarFondos()
    {
        Scene escena = EditorSceneManager.OpenScene("Assets/Scenes/Juego.unity", OpenSceneMode.Single);
        GameObject cochera = GameObject.Find("ecenaCochera");
        Transform modelo = cochera != null ? BuscarPoster(cochera.transform) : null;
        if (modelo == null) { Debug.LogError("No encontre Poster_fondo en ecenaCochera"); return; }
        Material matCochera = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materiales/Fondo cochera.mat");

        foreach (var (mapa, img, nombreMat) in new[] { ("ecenaCosina", "cocina", "Fondo cocina"), ("ecenaPatio", "patio", "Fondo patio") })
        {
            GameObject go = GameObject.Find(mapa);
            if (go == null) { Debug.LogWarning("No existe " + mapa); continue; }
            Transform ya = BuscarPoster(go.transform);
            string rutaMat = "Assets/Materiales/" + nombreMat + ".mat";
            Material m = AssetDatabase.LoadAssetAtPath<Material>(rutaMat);
            if (m == null) { m = new Material(matCochera); AssetDatabase.CreateAsset(m, rutaMat); }
            m.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/fondos/" + img + ".PNG"));
            EditorUtility.SetDirty(m);

            if (ya != null) { ya.GetComponent<MeshRenderer>().sharedMaterial = m; Debug.Log("Poster ya existia en " + mapa); continue; }
            // Cuadro de fondo detras del plano de juego, con el mismo tamano que el de la cochera
            GameObject p = GameObject.CreatePrimitive(PrimitiveType.Quad);
            p.name = "Poster_fondo";
            p.transform.SetParent(go.transform, false);
            Bounds b = CalcularLimites(go);
            float alto = Mathf.Max(b.size.y, 6f), ancho = alto * 2f;
            p.transform.position = new Vector3(b.center.x, b.center.y, 3.5f); // detras del plano Z=0..3 donde se camina
            p.transform.rotation = Quaternion.Euler(0, 180, 0);
            p.transform.localScale = new Vector3(ancho, alto, 1);
            p.GetComponent<MeshRenderer>().sharedMaterial = m;
            Object.DestroyImmediate(p.GetComponent<MeshCollider>());
            Debug.Log("Poster creado en " + mapa + " pos " + p.transform.position);
        }
        EditorSceneManager.MarkSceneDirty(escena);
        EditorSceneManager.SaveScene(escena);
    }

    private static Transform BuscarPoster(Transform raiz)
    {
        foreach (Transform t in raiz.GetComponentsInChildren<Transform>(true)) if (t.name == "Poster_fondo") return t;
        return null;
    }

    private static Bounds CalcularLimites(GameObject go)
    {
        var rs = go.GetComponentsInChildren<Renderer>();
        if (rs.Length == 0) return new Bounds(go.transform.position, new Vector3(20, 10, 1));
        Bounds b = rs[0].bounds; foreach (var r in rs) b.Encapsulate(r.bounds); return b;
    }
}
