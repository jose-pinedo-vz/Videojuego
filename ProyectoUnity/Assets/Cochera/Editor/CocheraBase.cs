using UnityEditor;
using UnityEngine;

// Piso, paredes laterales y techo de la cochera (la pared de fondo "pared" no se toca).
//  Piso: losa de concreto con juntas, grietas, manchas de aceite y marcas de llantas (mosaico de 4 m x 4 m).
//  Paredes: bloques de cemento pintados (izquierda verde descolorido, derecha ocre) con humedad, goteras y pintura descascarada.
//  Techo: concreto con marcas de cimbra, manchas de filtracion y oxido (mosaico de 5 m x 4 m).
internal static class CocheraBase
{
    private const string TEX = "Assets/Cochera/Texturas/";
    private const string MAT = "Assets/Cochera/Materiales/";
    private const float PISO_TILE = 4f;                        // m por repeticion del piso (x y z)
    private const float PARED_FONDO = 8f, PARED_ALTO = 10f;    // la pared lateral cubre z (8 m) e y (10 m)
    private const float TECHO_X = 5f, TECHO_Z = 4f;

    private static string Clave(string n)
    {
        n = n.ToLower();
        if (n == "piso") return "piso";
        if (n == "techo") return "techo";
        if (n.StartsWith("paredizq")) return "izq";
        if (n.StartsWith("paredder")) return "der";
        return null; // "pared" (fondo) y todo lo demas: sin tocar
    }

    public static void Aplicar(GameObject raiz)
    {
        int hechos = 0;
        foreach (MeshFilter mf in raiz.GetComponentsInChildren<MeshFilter>())
        {
            string clave = Clave(mf.gameObject.name);
            if (clave == null) continue;
            Mesh mesh = mf.sharedMesh;
            Renderer rend = mf.GetComponent<Renderer>();
            if (mesh == null || rend == null) continue;
            Material mat = AssetDatabase.LoadAssetAtPath<Material>(MAT + "Cochera_" + (clave == "izq" || clave == "der" ? "pared_" : "") + clave + ".mat");

            Vector3[] vert = mesh.vertices;
            Vector2[] uv = new Vector2[vert.Length];
            for (int i = 0; i < vert.Length; i++)
            {
                Vector3 p = raiz.transform.InverseTransformPoint(mf.transform.TransformPoint(vert[i])); // modelo: 1 unidad = 1 m
                if (clave == "piso") uv[i] = new Vector2(p.x / PISO_TILE, p.z / PISO_TILE);
                else if (clave == "techo") uv[i] = new Vector2(p.x / TECHO_X, p.z / TECHO_Z);
                else uv[i] = new Vector2((p.z + PARED_FONDO * 0.5f) / PARED_FONDO, p.y / PARED_ALTO);
            }
            CocheraTexturizador.AsignarUV(mesh, uv);

            if (mat != null)
            {
                var mats = new Material[Mathf.Max(1, mesh.subMeshCount)];
                for (int s = 0; s < mats.Length; s++) mats[s] = mat;
                rend.sharedMaterials = mats;
            }
            CocheraTexturizador.Tratados.Add(rend);
            hechos++;
        }
        Debug.Log("[Cochera] piso, paredes y techo texturizados: " + hechos);
    }

    public static void Configurar()
    {
        CocheraTexturizador.CrearMaterial(MAT + "Cochera_piso.mat", TEX + "Cochera_piso.png", TEX + "Cochera_piso_n.png", 0.12f);
        CocheraTexturizador.CrearMaterial(MAT + "Cochera_pared_izq.mat", TEX + "Cochera_pared_izq.png", TEX + "Cochera_pared_izq_n.png", 0.05f);
        CocheraTexturizador.CrearMaterial(MAT + "Cochera_pared_der.mat", TEX + "Cochera_pared_der.png", TEX + "Cochera_pared_der_n.png", 0.05f);
        CocheraTexturizador.CrearMaterial(MAT + "Cochera_techo.mat", TEX + "Cochera_techo.png", TEX + "Cochera_techo_n.png", 0.05f);
    }
}
