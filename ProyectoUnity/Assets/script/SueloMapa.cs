using UnityEngine;

/// <summary>
/// Refuerza los pisos planos de un mapa.
/// El piso del patio es UN solo plano sin grosor (un quad de 20x5), y un MeshCollider de ese tipo deja pasar
/// al jugador a gran velocidad de caida y tambien a las cascaras de platano (que usan raycast).
/// Aqui se le agrega una caja solida invisible debajo, ligeramente mas grande que el plano.
/// </summary>
public static class SueloMapa
{
    private const string NOMBRE_REFUERZO = "SueloSeguro";
    private const float GROSOR_MAXIMO_PLANO = 0.3f; // un piso mas delgado que esto se considera plano
    private const float GROSOR_REFUERZO = 2f;        // cuanto baja la caja bajo la superficie
    private const float MARGEN = 0.5f;               // la caja sobresale esto en X y Z

    public static void Reforzar(GameObject mapa)
    {
        if (mapa == null) return;

        foreach (Renderer r in mapa.GetComponentsInChildren<Renderer>(true))
        {
            if (!r.name.StartsWith("piso", System.StringComparison.OrdinalIgnoreCase)) continue;
            if (r.transform.Find(NOMBRE_REFUERZO) != null) continue; // ya reforzado

            Bounds b = r.bounds; // mundo; funciona aunque el mapa este apagado en la jerarquia
            if (b.size.y > GROSOR_MAXIMO_PLANO) continue; // tiene volumen, su collider ya sirve

            GameObject go = new GameObject(NOMBRE_REFUERZO);
            go.transform.SetParent(r.transform, false);
            go.transform.position = new Vector3(b.center.x, b.max.y - GROSOR_REFUERZO * 0.5f, b.center.z);
            go.transform.rotation = Quaternion.identity;

            Vector3 escalaPadre = r.transform.lossyScale;
            Vector3 tamanoMundo = new Vector3(b.size.x + MARGEN * 2f, GROSOR_REFUERZO, b.size.z + MARGEN * 2f);
            BoxCollider caja = go.AddComponent<BoxCollider>();
            // el collider se mide en espacio local del hijo, que hereda la escala del padre
            caja.size = new Vector3(
                tamanoMundo.x / Mathf.Max(0.0001f, Mathf.Abs(escalaPadre.x)),
                tamanoMundo.y / Mathf.Max(0.0001f, Mathf.Abs(escalaPadre.y)),
                tamanoMundo.z / Mathf.Max(0.0001f, Mathf.Abs(escalaPadre.z)));
            Debug.Log("SueloMapa: piso plano '" + r.name + "' reforzado con caja solida " + tamanoMundo);
        }
    }
}
