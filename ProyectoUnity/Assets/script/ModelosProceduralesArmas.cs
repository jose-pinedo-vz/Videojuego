using UnityEngine;

// Genera mallas 3D de respaldo cuando un FBX fue exportado vacío desde Blender (0 vértices)
// y provee carga segura de modelos para proyectiles/trampas (cáscara, barreno, caja de munición).
public static class ModelosProceduralesArmas
{
    private static GameObject prefabBarrenoCache;

    public static bool TieneMallaValida(GameObject go)
    {
        if (go == null) return false;
        foreach (MeshFilter mf in go.GetComponentsInChildren<MeshFilter>(true))
        {
            if (mf == null || mf.sharedMesh == null) continue;
            if (mf.gameObject.name == "CajaMaderaVisual") continue;
            if (mf.sharedMesh.vertexCount > 0) return true;
        }
        return false;
    }

    public static Material CrearMaterialColor(Color color, float metallic = 0.1f, float smoothness = 0.45f)
    {
        Shader sh = Shader.Find("Universal Render Pipeline/Lit");
        if (sh == null) sh = Shader.Find("Standard");
        if (sh == null) sh = Shader.Find("Sprites/Default");
        Material mat = new Material(sh);
        mat.color = color;
        if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
        if (mat.HasProperty("_Metallic")) mat.SetFloat("_Metallic", metallic);
        if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", smoothness);
        return mat;
    }

    private static GameObject CrearPiezaPrimitiva(PrimitiveType tipo, string nombre, Transform padre, Vector3 posLocal, Vector3 rotEuler, Vector3 escalaLocal, Material mat)
    {
        GameObject pieza = GameObject.CreatePrimitive(tipo);
        pieza.name = nombre;
        Collider col = pieza.GetComponent<Collider>();
        if (col != null) Object.Destroy(col);
        pieza.transform.SetParent(padre, false);
        pieza.transform.localPosition = posLocal;
        pieza.transform.localRotation = Quaternion.Euler(rotEuler);
        pieza.transform.localScale = escalaLocal;
        MeshRenderer mr = pieza.GetComponent<MeshRenderer>();
        if (mr != null && mat != null) mr.sharedMaterial = mat;
        return pieza;
    }

    // Construye un machete 3D orientado en +X (mango en -X, hoja afilada hacia +X)
    public static void AsegurarMallaMachete(GameObject raiz)
    {
        if (raiz == null || TieneMallaValida(raiz)) return;

        Transform contenedor = raiz.transform.Find("ModeloMacheteProc");
        if (contenedor != null) return;

        GameObject grupo = new GameObject("ModeloMacheteProc");
        grupo.transform.SetParent(raiz.transform, false);
        grupo.transform.localPosition = Vector3.zero;
        grupo.transform.localRotation = Quaternion.identity;
        grupo.transform.localScale = Vector3.one;

        Material matMango = CrearMaterialColor(new Color(0.38f, 0.20f, 0.08f), 0.05f, 0.35f);
        Material matGuarda = CrearMaterialColor(new Color(0.22f, 0.22f, 0.25f), 0.7f, 0.6f);
        Material matHoja = CrearMaterialColor(new Color(0.82f, 0.85f, 0.90f), 0.85f, 0.8f);
        Material matFilo = CrearMaterialColor(new Color(0.95f, 0.97f, 1.0f), 0.9f, 0.9f);

        // Mango ergonómico
        CrearPiezaPrimitiva(PrimitiveType.Cube, "Mango", grupo.transform,
            new Vector3(-0.18f, -0.01f, 0f), new Vector3(0f, 0f, 8f), new Vector3(0.16f, 0.045f, 0.03f), matMango);
        CrearPiezaPrimitiva(PrimitiveType.Sphere, "Pomo", grupo.transform,
            new Vector3(-0.26f, -0.02f, 0f), Vector3.zero, new Vector3(0.05f, 0.06f, 0.035f), matMango);

        // Guarda
        CrearPiezaPrimitiva(PrimitiveType.Cube, "Guarda", grupo.transform,
            new Vector3(-0.095f, 0.0f, 0f), Vector3.zero, new Vector3(0.02f, 0.09f, 0.04f), matGuarda);

        // Hoja principal y punta ancha tipo machete
        CrearPiezaPrimitiva(PrimitiveType.Cube, "HojaBase", grupo.transform,
            new Vector3(0.10f, 0.01f, 0f), Vector3.zero, new Vector3(0.38f, 0.058f, 0.012f), matHoja);
        CrearPiezaPrimitiva(PrimitiveType.Cube, "HojaPunta", grupo.transform,
            new Vector3(0.28f, 0.022f, 0f), new Vector3(0f, 0f, 12f), new Vector3(0.12f, 0.065f, 0.011f), matHoja);
        CrearPiezaPrimitiva(PrimitiveType.Cube, "FiloBrillante", grupo.transform,
            new Vector3(0.12f, -0.018f, 0f), Vector3.zero, new Vector3(0.42f, 0.012f, 0.014f), matFilo);
    }

    // Construye una penca de 5 plátanos amarillos si penca.fbx está vacío
    public static void AsegurarMallaPencaPlatanos(GameObject raiz)
    {
        if (raiz == null || TieneMallaValida(raiz)) return;

        Transform contenedor = raiz.transform.Find("ModeloPencaProc");
        if (contenedor != null) return;

        GameObject grupo = new GameObject("ModeloPencaProc");
        grupo.transform.SetParent(raiz.transform, false);
        grupo.transform.localPosition = Vector3.zero;
        grupo.transform.localRotation = Quaternion.identity;
        grupo.transform.localScale = Vector3.one;

        Material matAmarillo = CrearMaterialColor(new Color(1.0f, 0.88f, 0.12f), 0.05f, 0.55f);
        Material matTallo = CrearMaterialColor(new Color(0.35f, 0.55f, 0.15f), 0.05f, 0.3f);
        Material matPunta = CrearMaterialColor(new Color(0.28f, 0.18f, 0.07f), 0.05f, 0.2f);

        // Tallo superior que une la penca
        CrearPiezaPrimitiva(PrimitiveType.Cylinder, "TalloPenca", grupo.transform,
            new Vector3(-0.08f, 0.09f, 0f), new Vector3(0f, 0f, 35f), new Vector3(0.045f, 0.045f, 0.06f), matTallo);

        // 5 plátanos individuales en abanico
        float[] offsetsZ = { -0.06f, -0.03f, 0f, 0.03f, 0.06f };
        float[] angulosZ = { -18f, -9f, 0f, 9f, 18f };
        for (int i = 0; i < 5; i++)
        {
            GameObject platano = new GameObject("Platano_" + (i + 1));
            platano.transform.SetParent(grupo.transform, false);
            platano.transform.localPosition = new Vector3(0.02f * Mathf.Abs(i - 2), 0f, offsetsZ[i]);
            platano.transform.localRotation = Quaternion.Euler(angulosZ[i] * 0.6f, 0f, -15f + i * 6f);

            CrearPiezaPrimitiva(PrimitiveType.Capsule, "CuerpoA", platano.transform,
                new Vector3(0.0f, 0.03f, 0f), new Vector3(0f, 0f, -25f), new Vector3(0.042f, 0.09f, 0.042f), matAmarillo);
            CrearPiezaPrimitiva(PrimitiveType.Capsule, "CuerpoB", platano.transform,
                new Vector3(0.05f, -0.04f, 0f), new Vector3(0f, 0f, -65f), new Vector3(0.038f, 0.085f, 0.038f), matAmarillo);
            CrearPiezaPrimitiva(PrimitiveType.Sphere, "PuntaNegra", platano.transform,
                new Vector3(0.11f, -0.065f, 0f), Vector3.zero, new Vector3(0.022f, 0.022f, 0.022f), matPunta);
        }
    }

    // Asegura que el barreno muestre el paquete de 3 barrenos unidos
    public static void AsegurarPaqueteTresBarrenos(GameObject raiz)
    {
        if (raiz == null) return;
        if (raiz.transform.Find("BarrenoExtra_1") != null) return;

        Material matRojo = CrearMaterialColor(new Color(0.85f, 0.14f, 0.10f), 0.1f, 0.4f);
        Material matCinta = CrearMaterialColor(new Color(0.15f, 0.15f, 0.16f), 0.2f, 0.5f);
        Material matMecha = CrearMaterialColor(new Color(0.92f, 0.85f, 0.65f), 0.0f, 0.2f);

        // Colorear la malla base del barreno si tiene material por defecto
        foreach (MeshRenderer mr in raiz.GetComponentsInChildren<MeshRenderer>(true))
        {
            if (mr != null && mr.gameObject.name != "CajaMaderaVisual")
                mr.sharedMaterial = matRojo;
        }

        // Agregar 2 cilindros hermanos y cintas aislantes para formar el paquete de 3 barrenos
        CrearPiezaPrimitiva(PrimitiveType.Cylinder, "BarrenoExtra_1", raiz.transform,
            new Vector3(2.2f, 1.2f, 0f), new Vector3(90f, 0f, 0f), new Vector3(2.8f, 7.2f, 2.8f), matRojo);
        CrearPiezaPrimitiva(PrimitiveType.Cylinder, "BarrenoExtra_2", raiz.transform,
            new Vector3(-2.2f, 1.2f, 0f), new Vector3(90f, 0f, 0f), new Vector3(2.8f, 7.2f, 2.8f), matRojo);

        // Cintas negras que amarran el paquete y mecha
        CrearPiezaPrimitiva(PrimitiveType.Cube, "CintaPaquete_1", raiz.transform,
            new Vector3(0f, 0.6f, 3.5f), Vector3.zero, new Vector3(7.6f, 4.2f, 1.2f), matCinta);
        CrearPiezaPrimitiva(PrimitiveType.Cube, "CintaPaquete_2", raiz.transform,
            new Vector3(0f, 0.6f, -3.5f), Vector3.zero, new Vector3(7.6f, 4.2f, 1.2f), matCinta);
        CrearPiezaPrimitiva(PrimitiveType.Cylinder, "MechaCentral", raiz.transform,
            new Vector3(0f, 0.8f, 8.8f), new Vector3(90f, 0f, 0f), new Vector3(0.55f, 2.0f, 0.55f), matMecha);
    }

    // Construye una caja de munición militar 3D si caja municion.fbx está vacío
    public static void AsegurarMallaCajaMunicion(GameObject raiz)
    {
        if (raiz == null || TieneMallaValida(raiz)) return;

        Transform contenedor = raiz.transform.Find("ModeloCajaMunicionProc");
        if (contenedor != null) return;

        GameObject grupo = new GameObject("ModeloCajaMunicionProc");
        grupo.transform.SetParent(raiz.transform, false);
        grupo.transform.localPosition = Vector3.zero;
        grupo.transform.localRotation = Quaternion.identity;
        grupo.transform.localScale = Vector3.one;

        Material matVerdeMilitar = CrearMaterialColor(new Color(0.22f, 0.33f, 0.16f), 0.35f, 0.5f);
        Material matTapa = CrearMaterialColor(new Color(0.18f, 0.27f, 0.13f), 0.4f, 0.55f);
        Material matFranjaAmarilla = CrearMaterialColor(new Color(0.95f, 0.78f, 0.15f), 0.2f, 0.6f);
        Material matBronce = CrearMaterialColor(new Color(0.88f, 0.65f, 0.20f), 0.8f, 0.75f);

        // Cuerpo de la caja
        CrearPiezaPrimitiva(PrimitiveType.Cube, "CuerpoCaja", grupo.transform,
            new Vector3(0f, 0f, 0f), Vector3.zero, new Vector3(0.42f, 0.28f, 0.24f), matVerdeMilitar);

        // Tapa superior reforzada
        CrearPiezaPrimitiva(PrimitiveType.Cube, "TapaCaja", grupo.transform,
            new Vector3(0f, 0.145f, 0f), Vector3.zero, new Vector3(0.44f, 0.045f, 0.26f), matTapa);

        // Franja amarilla distintiva de munición
        CrearPiezaPrimitiva(PrimitiveType.Cube, "FranjaMunicion", grupo.transform,
            new Vector3(0f, 0.01f, 0f), Vector3.zero, new Vector3(0.43f, 0.065f, 0.25f), matFranjaAmarilla);

        // 3 balas doradas decorativas arriba
        for (int i = -1; i <= 1; i++)
        {
            CrearPiezaPrimitiva(PrimitiveType.Cylinder, "BalaIcono_" + i, grupo.transform,
                new Vector3(i * 0.08f, 0.20f, 0f), Vector3.zero, new Vector3(0.035f, 0.045f, 0.035f), matBronce);
        }
    }

    // Solo la cascara de platano (no el platano entero): una base chica con 4 petalos abiertos sobre el piso,
    // amarillos con la punta cafe. Mide ~0.3 de ancho, el personaje mide ~0.55.
    public static GameObject InstanciarModeloCascara(Vector3 posicion)
    {
        GameObject go = new GameObject("CascaraPlatano");
        go.transform.position = posicion;

        Material matAmarillo = CrearMaterialColor(new Color(1.0f, 0.86f, 0.10f), 0.0f, 0.55f);
        Material matInterior = CrearMaterialColor(new Color(0.96f, 0.90f, 0.62f), 0.0f, 0.35f);
        Material matPunta = CrearMaterialColor(new Color(0.36f, 0.22f, 0.07f), 0.0f, 0.2f);

        // base (donde estaba el tallo)
        CrearPiezaPrimitiva(PrimitiveType.Sphere, "Base", go.transform,
            new Vector3(0f, 0.025f, 0f), Vector3.zero, new Vector3(0.09f, 0.045f, 0.09f), matInterior);
        CrearPiezaPrimitiva(PrimitiveType.Cylinder, "Tallo", go.transform,
            new Vector3(0f, 0.06f, 0f), new Vector3(0f, 0f, 12f), new Vector3(0.025f, 0.04f, 0.025f), matPunta);

        // petalos: salen hacia afuera, aplastados, con la punta levantada
        for (int i = 0; i < 4; i++)
        {
            float ang = i * 90f + 20f;
            GameObject eje = new GameObject("Petalo_" + i);
            eje.transform.SetParent(go.transform, false);
            eje.transform.localRotation = Quaternion.Euler(0f, ang, 0f);

            CrearPiezaPrimitiva(PrimitiveType.Capsule, "Cuerpo", eje.transform,
                new Vector3(0f, 0.03f, 0.11f), new Vector3(80f, 0f, 0f), new Vector3(0.085f, 0.11f, 0.02f), matAmarillo);
            CrearPiezaPrimitiva(PrimitiveType.Sphere, "Punta", eje.transform,
                new Vector3(0f, 0.045f, 0.23f), Vector3.zero, new Vector3(0.05f, 0.025f, 0.05f), matPunta);
        }
        return go;
    }

    public static GameObject InstanciarModeloBarreno(Vector3 posicion)
    {
#if UNITY_EDITOR
        if (prefabBarrenoCache == null)
        {
            prefabBarrenoCache = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Modelos/barreno/barreno.fbx");
        }
#endif
        GameObject go;
        if (prefabBarrenoCache != null)
        {
            go = Object.Instantiate(prefabBarrenoCache, posicion, Quaternion.identity);
            go.transform.localScale = new Vector3(0.08f, 0.08f, 0.08f); // igual que el barreno de la mano (GestorArmas)
        }
        else
        {
            go = new GameObject("BarrenoProyectil");
            go.transform.position = posicion;
            Material matRojo = CrearMaterialColor(new Color(0.85f, 0.14f, 0.10f), 0.1f, 0.4f);
            CrearPiezaPrimitiva(PrimitiveType.Cylinder, "Cuerpo", go.transform,
                Vector3.zero, new Vector3(90f, 0f, 0f), new Vector3(0.08f, 0.18f, 0.08f), matRojo);
        }

        Material matRojoVivo = CrearMaterialColor(new Color(0.88f, 0.15f, 0.10f), 0.15f, 0.45f);
        foreach (MeshRenderer mr in go.GetComponentsInChildren<MeshRenderer>(true))
        {
            if (mr != null) mr.sharedMaterial = matRojoVivo;
        }
        return go;
    }
}
