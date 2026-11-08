using System.Collections.Generic;
using UnityEngine;
using TMPro;

[System.Serializable]
public class EntradaPersonaje
{
    public string id;
    public string nombreAnimal;
    public string subnombre;
    public GameObject prefabVisual;
    public Avatar avatar;
    public Vector3 escalaLocal = new Vector3(0.3f, 0.3f, 0.3f);
    public Vector3 posicionLocal = Vector3.zero;
    public Vector3 rotacionLocalEuler = Vector3.zero;
}

public class CatalogoPersonajes : ScriptableObject
{
    public const string RUTA_RECURSO = "Personajes/CatalogoPersonajes";
    public const int TOTAL_PERSONAJES = 4;

    public static readonly string[] SUBNOMBRES_DEFAULT = new string[]
    {
        "Cheto",  // 0: Gato
        "Kaiser", // 1: Perro
        "Dorito", // 2: Ratón
        "CR7"     // 3: Cabra
    };

    public static readonly string[] ANIMALES_DEFAULT = new string[]
    {
        "Gato",
        "Perro",
        "Ratón",
        "Cabra"
    };

    public EntradaPersonaje[] personajes = new EntradaPersonaje[TOTAL_PERSONAJES];

    private static CatalogoPersonajes instanciaCache;

    public static CatalogoPersonajes Obtener()
    {
        if (instanciaCache == null)
        {
            instanciaCache = Resources.Load<CatalogoPersonajes>(RUTA_RECURSO);
        }
        return instanciaCache;
    }

    public static string ObtenerSubnombre(int indice)
    {
        if (indice < 0) return "";
        CatalogoPersonajes cat = Obtener();
        if (cat != null && cat.personajes != null && indice < cat.personajes.Length)
        {
            EntradaPersonaje ep = cat.personajes[indice];
            if (ep != null && !string.IsNullOrEmpty(ep.subnombre))
                return ep.subnombre;
        }
        int idxSeguro = ((indice % TOTAL_PERSONAJES) + TOTAL_PERSONAJES) % TOTAL_PERSONAJES;
        return SUBNOMBRES_DEFAULT[idxSeguro];
    }

    public static string FormatearNombreConSubnombre(string nombreBase, int indicePersonaje)
    {
        string limpio = string.IsNullOrWhiteSpace(nombreBase) ? "Jugador" : nombreBase.Trim();

        // Si el nombre ya traía un sufijo previo " (Subnombre)", extraer solo el nombre base
        for (int i = 0; i < SUBNOMBRES_DEFAULT.Length; i++)
        {
            string sufijo = " (" + SUBNOMBRES_DEFAULT[i] + ")";
            if (limpio.EndsWith(sufijo, System.StringComparison.OrdinalIgnoreCase))
            {
                limpio = limpio.Substring(0, limpio.Length - sufijo.Length).Trim();
                break;
            }
        }

        string sub = indicePersonaje >= 0 ? ObtenerSubnombre(indicePersonaje) : "";
        string sufijoFinal = string.IsNullOrEmpty(sub) ? "" : (" (" + sub + ")");

        // FixedString32Bytes admite hasta 29 bytes UTF-8; recortamos el nombre base para que nunca desborde
        int maxBytesNombre = Mathf.Max(6, 29 - System.Text.Encoding.UTF8.GetByteCount(sufijoFinal));
        while (limpio.Length > 1 && System.Text.Encoding.UTF8.GetByteCount(limpio) > maxBytesNombre)
        {
            limpio = limpio.Substring(0, limpio.Length - 1).TrimEnd();
        }

        return limpio + sufijoFinal;
    }

    public EntradaPersonaje ObtenerEntrada(int indice)
    {
        if (personajes == null || personajes.Length == 0) return null;
        int idx = Mathf.Clamp(indice, 0, personajes.Length - 1);
        return personajes[idx];
    }

    public static bool EsObjetoAuxiliarJugador(Transform hijo)
    {
        if (hijo == null) return true;
        string n = hijo.name;
        if (n == "Mano" || n == "PolvoDeslizar" || n == "EtiquetaNombre3D")
            return true;
        if (hijo.GetComponent<TMP_Text>() != null)
            return true;
        if (hijo.GetComponent<Arma>() != null)
            return true;
        return false;
    }

    public Transform AplicarPersonajeA(GameObject objetoCuerpo, int indice, bool aplicarVisibilidad, bool esVisible)
    {
        if (objetoCuerpo == null) return null;

        int idx = ((indice % TOTAL_PERSONAJES) + TOTAL_PERSONAJES) % TOTAL_PERSONAJES;
        EntradaPersonaje entrada = ObtenerEntrada(idx);
        Animator animatorCuerpo = objetoCuerpo.GetComponent<Animator>();

        string nombreEsperado = entrada != null && !string.IsNullOrEmpty(entrada.id)
            ? ("ModeloPersonaje_" + entrada.id)
            : ("ModeloPersonaje_" + idx);

        // Buscar modelos visuales actuales hijos directos de cuerpo
        List<Transform> modelosActuales = new List<Transform>();
        for (int i = 0; i < objetoCuerpo.transform.childCount; i++)
        {
            Transform h = objetoCuerpo.transform.GetChild(i);
            if (!EsObjetoAuxiliarJugador(h))
            {
                modelosActuales.Add(h);
            }
        }

        // Si ya tiene exactamente el modelo deseado instanciado, solo asegurar referencias y visibilidad
        if (modelosActuales.Count == 1 && modelosActuales[0].name == nombreEsperado)
        {
            Transform yaPuesto = modelosActuales[0];
            SincronizarComponentesAnimacion(objetoCuerpo, animatorCuerpo, yaPuesto, entrada != null ? entrada.avatar : null);
            if (aplicarVisibilidad) AplicarVisibilidadRenderers(yaPuesto, esVisible);
            return yaPuesto;
        }

        // Si no hay prefab en catálogo para Cheto (índice 0) y sigue el "Personaje gato" original del prefab, conservarlo
        if ((entrada == null || entrada.prefabVisual == null) && idx == 0 && modelosActuales.Count == 1)
        {
            Transform gatoExistente = modelosActuales[0];
            SincronizarComponentesAnimacion(objetoCuerpo, animatorCuerpo, gatoExistente, animatorCuerpo != null ? animatorCuerpo.avatar : null);
            if (aplicarVisibilidad) AplicarVisibilidadRenderers(gatoExistente, esVisible);
            return gatoExistente;
        }

        if (entrada == null || entrada.prefabVisual == null)
        {
            Debug.LogWarning($"CatalogoPersonajes: No se encontró prefabVisual para el índice {idx} ({ObtenerSubnombre(idx)}).");
            return modelosActuales.Count > 0 ? modelosActuales[0] : null;
        }

        // Desacoplar de inmediato los modelos anteriores antes de Rebind() para que el Animator solo vea el esqueleto nuevo
        for (int i = 0; i < modelosActuales.Count; i++)
        {
            Transform viejo = modelosActuales[i];
            if (viejo == null) continue;
            viejo.SetParent(null, false);
            viejo.gameObject.SetActive(false);
            if (Application.isPlaying)
                Destroy(viejo.gameObject);
            else
                DestroyImmediate(viejo.gameObject);
        }

        GameObject instancia = Instantiate(entrada.prefabVisual, objetoCuerpo.transform);
        instancia.name = nombreEsperado;
        instancia.transform.localPosition = entrada.posicionLocal;
        instancia.transform.localRotation = Quaternion.Euler(entrada.rotacionLocalEuler);
        instancia.transform.localScale = entrada.escalaLocal;

        // Evitar que un Animator secundario en el modelo hijo compita con el Animator principal de cuerpo
        Animator animHijo = instancia.GetComponent<Animator>();
        if (animHijo != null)
        {
            animHijo.enabled = false;
            if (Application.isPlaying)
                Destroy(animHijo);
            else
                DestroyImmediate(animHijo);
        }

        SincronizarComponentesAnimacion(objetoCuerpo, animatorCuerpo, instancia.transform, entrada.avatar);

        if (aplicarVisibilidad)
        {
            AplicarVisibilidadRenderers(instancia.transform, esVisible);
        }

        return instancia.transform;
    }

    private static void AplicarVisibilidadRenderers(Transform raizModelo, bool visible)
    {
        if (raizModelo == null) return;
        foreach (Renderer r in raizModelo.GetComponentsInChildren<Renderer>(true))
        {
            if (r != null) r.enabled = visible;
        }
    }

    private static void SincronizarComponentesAnimacion(GameObject objetoCuerpo, Animator animatorCuerpo, Transform nuevoModelo, Avatar nuevoAvatar)
    {
        if (animatorCuerpo != null)
        {
            if (nuevoAvatar != null)
            {
                animatorCuerpo.avatar = nuevoAvatar;
            }
            animatorCuerpo.Rebind();
            animatorCuerpo.Update(0f);
        }

        AnimadorArma animArma = objetoCuerpo.GetComponent<AnimadorArma>();
        if (animArma != null)
        {
            animArma.ActualizarModeloPersonaje(nuevoModelo);
        }

        AnimadorDeslizar animDeslizar = objetoCuerpo.GetComponent<AnimadorDeslizar>();
        if (animDeslizar != null)
        {
            animDeslizar.RefrescarHuesos();
        }

        Personaje personaje = objetoCuerpo.GetComponent<Personaje>();
        if (personaje != null)
        {
            personaje.RefrescarAnimator();
        }
    }
}
