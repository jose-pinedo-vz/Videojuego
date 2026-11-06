using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Herramientas -> Arreglar fondos (patio y cocina) y cajas del patio
// 1) El "Poster_fondo" de cada mapa se coloca justo sobre su pared trasera ("Pared", 20x10, igual proporcion 2:1 que la imagen)
//    y la pared 3D se oculta (se conserva su collider).
// 2) Las cajas de madera del patio (cajamadera*) se adelantan al plano donde camina el jugador (z de los spawns).
public static class ArreglarPatio
{
    [MenuItem("Herramientas/Arreglar fondos (patio y cocina) y cajas del patio")]
    public static void Ejecutar()
    {
        Arreglar("ecenaPatio", true);
        Arreglar("ecenaCosina", false);
        EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
    }

    // Para modo batch: abre la escena, arregla y guarda.
    public static void EjecutarYGuardar()
    {
        var esc = EditorSceneManager.OpenScene("Assets/Scenes/Juego.unity");
        Arreglar("ecenaPatio", true);
        Arreglar("ecenaCosina", false);
        foreach (Transform t in GameObject.Find("ecenaPatio").GetComponentsInChildren<Transform>(true)) if (t.name == "Techo") t.gameObject.SetActive(true);
        EditorSceneManager.MarkSceneDirty(esc);
        EditorSceneManager.SaveScene(esc);
    }

    private static void Arreglar(string nombreMapa, bool cajas)
    {
        GameObject mapa = GameObject.Find(nombreMapa);
        if (mapa == null) { Debug.LogError("No encontre " + nombreMapa); return; }

        Renderer pared = null;
        foreach (Renderer r in mapa.GetComponentsInChildren<Renderer>(true))
            if (r.name == "Pared") { pared = r; break; }
        Transform poster = null;
        foreach (Transform t in mapa.GetComponentsInChildren<Transform>(true))
            if (t.name == "Poster_fondo") { poster = t; break; }

        if (pared != null && poster != null)
        {
            Bounds b = pared.bounds; // ya trae el tamano real 20 x 10 (z ~ 0)
            poster.rotation = Quaternion.Euler(0, 180, 0);
            poster.position = new Vector3(b.center.x, b.center.y, b.center.z + 0.02f); // un pelin hacia la camara
            Vector3 ls = poster.parent != null ? poster.parent.lossyScale : Vector3.one;
            poster.localScale = new Vector3(b.size.x / ls.x, b.size.y / ls.y, 1);
            pared.enabled = false;
            var mr = poster.GetComponent<MeshRenderer>();
            if (mr != null) { mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; mr.receiveShadows = false; }
            Debug.Log("FIX " + nombreMapa + " poster en " + poster.position + " escala " + b.size);
        }
        else Debug.LogWarning("FIX " + nombreMapa + ": falta Pared o Poster_fondo");

        if (!cajas) return;
        Transform spawn = mapa.transform.Find("spawn1");
        float planoZ = spawn != null ? spawn.position.z : mapa.transform.position.z + 17.5f;
        int n = 0;
        foreach (Renderer r in mapa.GetComponentsInChildren<Renderer>(true))
        {
            if (!r.name.StartsWith("cajamadera")) continue;
            r.transform.position += new Vector3(0, 0, planoZ - r.bounds.center.z);
            if (r.GetComponent<Collider>() == null) r.gameObject.AddComponent<BoxCollider>();
            n++;
        }
        Debug.Log("FIX cajas adelantadas al plano z=" + planoZ + ": " + n);
    }
}
