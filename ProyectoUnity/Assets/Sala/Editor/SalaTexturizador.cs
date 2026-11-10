using System.IO;
using UnityEditor;
using UnityEngine;

// Escenario SALA: todas las texturas, materiales y scripts de texturizado de sala.fbx en una sola carpeta (Assets/Sala).
//  Assets/Sala/Texturas   -> imagenes (PNG)
//  Assets/Sala/Materiales -> materiales (se crean solos)
//  Assets/Sala/Editor     -> scripts (uno por grupo de objetos) + este archivo
// Al importar Assets/sala.fbx este AssetPostprocessor genera UV y asigna los materiales de: sillones, lamparas (pie y candelabro),
// estantes, muebles y tele, marcos y cuadros, libros y cajas, piso y paredes laterales.
// NO toca: la pared de fondo ("Pared"), el techo, el "mueble" del fondo izquierdo, ni Juego.unity.
// Uso: Herramientas -> Sala -> Texturizar toda la sala.
// Para quitarlo todo: borrar la carpeta Assets/Sala y reimportar sala.fbx.
public class SalaTexturizador : AssetPostprocessor
{
    private const string FBX = "/sala.fbx";

    // Se ejecuta cada vez que Unity importa sala.fbx
    private void OnPostprocessModel(GameObject raiz)
    {
        if (!assetPath.ToLower().EndsWith(FBX)) return;
        SalaPiso.Aplicar(raiz);
        SalaParedes.Aplicar(raiz);
        SalaEstantes.Aplicar(raiz);
        SalaMuebles.Aplicar(raiz);
        SalaSillones.Aplicar(raiz);
        SalaLibros.Aplicar(raiz);
        SalaMarcos.Aplicar(raiz);
        SalaLampara.Aplicar(raiz);
        SalaCandelabro.Aplicar(raiz);
    }

    // Subir este numero fuerza a Unity a reimportar sala.fbx cuando se edita un script
    public override uint GetVersion() { return 22; }

    // Corre DESPUES de CorregirMaterialesMapas (del equipo), que tambien asigna materiales al piso, techo y paredes de la sala
    public override int GetPostprocessOrder() { return 1000; }

    // Asigna UV seguras (sin NaN/infinito) y recalcula tangentes para que el mapa de normales no genere pixeles invalidos
    public static void AsignarUV(Mesh mesh, Vector2[] uv)
    {
        for (int i = 0; i < uv.Length; i++)
            if (float.IsNaN(uv[i].x) || float.IsNaN(uv[i].y) || float.IsInfinity(uv[i].x) || float.IsInfinity(uv[i].y)) uv[i] = Vector2.zero;
        mesh.uv = uv;
        mesh.RecalculateTangents();
    }

    public static void AsegurarCarpetas()
    {
        if (!AssetDatabase.IsValidFolder("Assets/Sala")) AssetDatabase.CreateFolder("Assets", "Sala");
        if (!AssetDatabase.IsValidFolder("Assets/Sala/Texturas")) AssetDatabase.CreateFolder("Assets/Sala", "Texturas");
        if (!AssetDatabase.IsValidFolder("Assets/Sala/Materiales")) AssetDatabase.CreateFolder("Assets/Sala", "Materiales");
    }

    [MenuItem("Herramientas/Sala/Texturizar toda la sala")]
    public static void TexturizarTodo()
    {
        AssetDatabase.Refresh();
        AsegurarCarpetas();
        SalaPiso.Configurar();
        SalaParedes.Configurar();
        SalaEstantes.Configurar();
        SalaMuebles.Configurar();
        SalaSillones.Configurar();
        SalaLibros.Configurar();
        SalaMarcos.Configurar();
        SalaLampara.Configurar();
        SalaCandelabro.Configurar();
        AssetDatabase.SaveAssets();

        string fbx = "Assets/sala.fbx";
        if (AssetDatabase.LoadAssetAtPath<GameObject>(fbx) == null) { Debug.LogError("[Sala] no encuentro " + fbx); return; }
        AssetDatabase.ImportAsset(fbx, ImportAssetOptions.ForceUpdate);
        Debug.Log("[Sala] sala texturizada");
    }

    // Opcional: borra los scripts, texturas y materiales de las versiones sueltas anteriores (antes de tener la carpeta Assets/Sala)
    [MenuItem("Herramientas/Sala/Borrar versiones sueltas anteriores")]
    public static void BorrarAnteriores()
    {
        if (!EditorUtility.DisplayDialog("Sala",
            "Se borraran los scripts Texturizar*.cs sueltos y las carpetas Assets/Texturas/Sala y Assets/Materiales/Sala (versiones anteriores). La carpeta Assets/Sala no se toca. ¿Continuar?",
            "Borrar", "Cancelar")) return;
        string[] viejos = { "TexturizarSala", "TexturizarLibros", "TexturizarSillones", "TexturizarLampara", "TexturizarCandelabro",
                            "TexturizarEstantes", "TexturizarMuebles", "TexturizarMarcos", "TexturizarPiso", "TexturizarParedes" };
        int borrados = 0;
        foreach (string guid in AssetDatabase.FindAssets("t:MonoScript"))
        {
            string p = AssetDatabase.GUIDToAssetPath(guid);
            if (p.StartsWith("Assets/Sala/")) continue;
            if (System.Array.IndexOf(viejos, Path.GetFileNameWithoutExtension(p)) >= 0 && AssetDatabase.DeleteAsset(p)) borrados++;
        }
        foreach (string carpeta in new[] { "Assets/Texturas/Sala", "Assets/Materiales/Sala" })
            if (AssetDatabase.IsValidFolder(carpeta) && AssetDatabase.DeleteAsset(carpeta)) borrados++;
        Debug.Log("[Sala] versiones anteriores borradas: " + borrados + ". Ahora usa Herramientas -> Sala -> Texturizar toda la sala");
    }
}
