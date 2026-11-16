using System;
using UnityEngine;

[Serializable]
public class EntradaModeloArma
{
    public string nombre;
    public string rutaAsset;
    public GameObject prefabModelo;
}

/// <summary>
/// Catálogo en Resources con los prefabs FBX de todas las armas y de la caja de munición,
/// permitiendo instanciarlos tanto en el Editor como en compilaciones (.exe).
/// </summary>
public class CatalogoArmas : ScriptableObject
{
    public const string RUTA_RECURSO = "Armas/CatalogoArmas";

    public EntradaModeloArma[] modelos = new EntradaModeloArma[0];
    public GameObject prefabCajaMunicion;
    public GameObject prefabBarrenoProyectil;

    private static CatalogoArmas instanciaCache;

    public static CatalogoArmas Obtener()
    {
        if (instanciaCache == null)
        {
            instanciaCache = Resources.Load<CatalogoArmas>(RUTA_RECURSO);
        }
        return instanciaCache;
    }

    public GameObject ObtenerModelo(string nombre)
    {
        if (string.IsNullOrEmpty(nombre) || modelos == null) return null;
        for (int i = 0; i < modelos.Length; i++)
        {
            EntradaModeloArma e = modelos[i];
            if (e != null && string.Equals(e.nombre, nombre, StringComparison.OrdinalIgnoreCase))
            {
                return e.prefabModelo;
            }
        }
        return null;
    }
}
