using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Herramientas de Editor para colocar, visualizar y ajustar en escena los ObjetoDestruible
/// (paredes de cobertura y cajas/plataformas) directamente dentro de cada mapa de Juego.unity.
/// </summary>
public static class ConfigurarObstaculosDestruibles
{
    private const string NOMBRE_CONTENEDOR = "ObstaculosDestruibles";

    [InitializeOnLoadMethod]
    private static void InicializarAutoConfiguracion()
    {
        EditorApplication.delayCall += VerificarEscenaJuego;
        EditorSceneManager.sceneOpened += (scene, mode) => VerificarEscenaJuego();
    }

    private static void VerificarEscenaJuego()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;

        var escena = EditorSceneManager.GetActiveScene();
        if (!escena.IsValid() || !escena.isLoaded || escena.name != "Juego") return;

        List<GameObject> mapas = ObtenerMapasEscena();
        if (mapas.Count == 0) return;

        // Solo auto-poblar una vez los mapas que aun no tienen ni carpeta "ObstaculosDestruibles" ni ObjetoDestruible manuales
        bool faltaAlguno = false;
        foreach (GameObject mapa in mapas)
        {
            if (mapa == null) continue;
            if (mapa.transform.Find(NOMBRE_CONTENEDOR) == null
                && mapa.GetComponentsInChildren<ObjetoDestruible>(true).Length == 0)
            {
                faltaAlguno = true;
                break;
            }
        }

        if (!faltaAlguno) return;

        int agregados = PoblarMapasVacios(mapas, false);
        if (agregados > 0)
        {
            EditorSceneManager.MarkSceneDirty(escena);
            EditorSceneManager.SaveScene(escena);
            Debug.Log($"[Coberturas Destruibles] Se crearon {agregados} obstaculos editables dentro de los mapas de Juego.unity. Puedes moverlos o ajustarlos libremente desde la escena.");
        }
    }

    [MenuItem("Herramientas/Coberturas Destruibles/Agregar en mapas sin coberturas", false, 30)]
    private static void MenuPoblarVacios()
    {
        List<GameObject> mapas = ObtenerMapasEscena();
        if (mapas.Count == 0)
        {
            Debug.LogWarning("No se encontraron mapas en la escena actual.");
            return;
        }

        int agregados = PoblarMapasVacios(mapas, true);
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        Debug.Log($"Se agregaron {agregados} obstaculos destruibles en mapas vacios.");
    }

    [MenuItem("Herramientas/Coberturas Destruibles/Re-generar coberturas en mapa seleccionado", false, 31)]
    private static void MenuRegenerarMapaSeleccionado()
    {
        GameObject mapa = ObtenerMapaSeleccionadoOPrimero();
        if (mapa == null)
        {
            Debug.LogWarning("Selecciona primero un mapa (ej. ecenaCochera, ecenaCosina, ecenaPatio, ecenaSala).");
            return;
        }

        Transform existente = mapa.transform.Find(NOMBRE_CONTENEDOR);
        if (existente != null)
        {
            Undo.DestroyObjectImmediate(existente.gameObject);
        }

        int creados = GenerarEnUnMapa(mapa);
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        Debug.Log($"{mapa.name}: regenerados {creados} obstaculos destruibles dentro de '{NOMBRE_CONTENEDOR}'.");
    }

    [MenuItem("Herramientas/Coberturas Destruibles/Agregar Pared de Cobertura aqui", false, 45)]
    private static void MenuAgregarPared()
    {
        CrearObstaculoIndividualEnEscena(TipoObstaculoDestruible.ParedCobertura);
    }

    [MenuItem("Herramientas/Coberturas Destruibles/Agregar Plataforma Caja aqui", false, 46)]
    private static void MenuAgregarPlataforma()
    {
        CrearObstaculoIndividualEnEscena(TipoObstaculoDestruible.PlataformaCaja);
    }

    [MenuItem("Herramientas/Coberturas Destruibles/Ajustar seleccionados al piso", false, 60)]
    private static void MenuAjustarAlPiso()
    {
        int ajustados = 0;
        foreach (GameObject go in Selection.gameObjects)
        {
            if (go == null) continue;
            ObjetoDestruible obs = go.GetComponent<ObjetoDestruible>();
            if (obs == null) continue;

            if (IntentarBuscarPisoDebajo(go.transform.position, go.transform.root.gameObject, out float yPiso))
            {
                Undo.RecordObject(go.transform, "Ajustar cobertura al piso");
                Vector3 p = go.transform.position;
                go.transform.position = new Vector3(p.x, yPiso, p.z);
                ajustados++;
            }
        }
        if (ajustados > 0) EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        Debug.Log($"Ajustados al piso: {ajustados} obstaculo(s).");
    }

    private static int PoblarMapasVacios(List<GameObject> mapas, bool forzarSiCarpetaVacia)
    {
        int totalCreados = 0;
        Dictionary<GameObject, bool> estadosOriginales = new Dictionary<GameObject, bool>();
        foreach (GameObject m in mapas)
        {
            if (m != null) estadosOriginales[m] = m.activeSelf;
        }

        try
        {
            foreach (GameObject mapa in mapas)
            {
                if (mapa == null) continue;

                Transform carpetaExistente = mapa.transform.Find(NOMBRE_CONTENEDOR);
                bool yaTieneObstaculos = mapa.GetComponentsInChildren<ObjetoDestruible>(true).Length > 0;
                if (yaTieneObstaculos || (!forzarSiCarpetaVacia && carpetaExistente != null))
                {
                    continue;
                }

                // Activar solo el mapa actual para hacer raycasts precisos contra sus pisos
                foreach (GameObject otro in mapas)
                {
                    if (otro != null) otro.SetActive(otro == mapa);
                }

                SueloMapa.Reforzar(mapa);
                Physics.SyncTransforms();
                totalCreados += GenerarEnUnMapa(mapa);
            }
        }
        finally
        {
            foreach (var kv in estadosOriginales)
            {
                if (kv.Key != null) kv.Key.SetActive(kv.Value);
            }
            Physics.SyncTransforms();
        }

        return totalCreados;
    }

    private static int GenerarEnUnMapa(GameObject mapa)
    {
        if (mapa == null) return 0;

        bool estabaActivo = mapa.activeSelf;
        if (!estabaActivo)
        {
            mapa.SetActive(true);
            SueloMapa.Reforzar(mapa);
            Physics.SyncTransforms();
        }

        try
        {
            List<Vector3> spawnsJugador = new List<Vector3>();
            foreach (Transform t in GameManager.BuscarPuntos(mapa, GameManager.PREFIJO_JUGADOR))
                if (t != null) spawnsJugador.Add(t.position);

            List<Vector3> spawnsArmas = new List<Vector3>();
            foreach (Transform t in GameManager.BuscarPuntos(mapa, GameManager.PREFIJO_ARMA))
                if (t != null) spawnsArmas.Add(t.position);

            float planoZ = CalcularPlanoZ(spawnsJugador, spawnsArmas, mapa);
            List<Vector3> candidatos = GestorEntornoDestruible.ExplorarSuperficiesPiso(mapa, spawnsJugador, spawnsArmas, planoZ);
            var elegidos = GestorEntornoDestruible.SeleccionarUbicaciones(candidatos, spawnsJugador, spawnsArmas);
            if (elegidos.Count == 0 && candidatos.Count > 0)
            {
                // Respaldo para mapas compactos: elegir puntos interiores mas alejados de spawns sin salirse del mapa
                int objetivoCompacto = Mathf.Min(GestorEntornoDestruible.MIN_OBSTACULOS, candidatos.Count);
                for (int i = 0; i < candidatos.Count && elegidos.Count < objetivoCompacto; i++)
                {
                    bool muyCerca = false;
                    foreach (var ya in elegidos)
                    {
                        if (Vector2.Distance(candidatos[i], ya.pos) < 1.6f) { muyCerca = true; break; }
                    }
                    if (!muyCerca)
                    {
                        var tipo = (elegidos.Count % 2 == 0)
                            ? TipoObstaculoDestruible.ParedCobertura
                            : TipoObstaculoDestruible.PlataformaCaja;
                        elegidos.Add((tipo, candidatos[i]));
                    }
                }
            }

            if (elegidos.Count == 0) return 0;

            Transform carpeta = mapa.transform.Find(NOMBRE_CONTENEDOR);
            if (carpeta == null)
            {
                GameObject goCarpeta = new GameObject(NOMBRE_CONTENEDOR);
                Undo.RegisterCreatedObjectUndo(goCarpeta, "Crear carpeta " + NOMBRE_CONTENEDOR);
                goCarpeta.transform.SetParent(mapa.transform, false);
                carpeta = goCarpeta.transform;
            }

            int creados = 0;
            int idxPared = 1;
            int idxCaja = 1;
            foreach (var item in elegidos)
            {
                string nombre = item.tipo == TipoObstaculoDestruible.ParedCobertura
                    ? $"CoberturaPared_{idxPared++}"
                    : $"PlataformaCaja_{idxCaja++}";

                GameObject go = new GameObject(nombre);
                Undo.RegisterCreatedObjectUndo(go, "Crear " + nombre);
                go.transform.SetParent(carpeta, false);
                go.transform.position = item.pos;
                go.transform.rotation = Quaternion.identity;

                ObjetoDestruible comp = go.AddComponent<ObjetoDestruible>();
                comp.EstablecerTipo(item.tipo);
                creados++;
            }

            return creados;
        }
        finally
        {
            if (!estabaActivo) mapa.SetActive(false);
        }
    }

    private static void CrearObstaculoIndividualEnEscena(TipoObstaculoDestruible tipo)
    {
        GameObject mapa = ObtenerMapaSeleccionadoOPrimero();
        if (mapa == null)
        {
            Debug.LogWarning("Selecciona un mapa en la Hierarchy antes de agregar un obstaculo.");
            return;
        }

        Transform carpeta = mapa.transform.Find(NOMBRE_CONTENEDOR);
        if (carpeta == null)
        {
            GameObject goCarpeta = new GameObject(NOMBRE_CONTENEDOR);
            Undo.RegisterCreatedObjectUndo(goCarpeta, "Crear carpeta " + NOMBRE_CONTENEDOR);
            goCarpeta.transform.SetParent(mapa.transform, false);
            carpeta = goCarpeta.transform;
        }

        List<Transform> spawns = GameManager.BuscarPuntos(mapa, GameManager.PREFIJO_JUGADOR);
        float planoZ = spawns.Count > 0 ? spawns[0].position.z : mapa.transform.position.z;
        Vector3 pos = mapa.transform.position;

        if (SceneView.lastActiveSceneView != null)
        {
            Vector3 pivote = SceneView.lastActiveSceneView.pivot;
            pos = new Vector3(pivote.x, pivote.y, planoZ);
        }
        else if (spawns.Count > 0)
        {
            Vector3 suma = Vector3.zero;
            foreach (Transform sp in spawns) suma += sp.position;
            pos = suma / spawns.Count;
            pos.z = planoZ;
        }

        if (IntentarBuscarPisoDebajo(pos + Vector3.up * 1.5f, mapa, out float yPiso))
        {
            pos.y = yPiso;
        }

        int numero = carpeta.childCount + 1;
        string prefijo = tipo == TipoObstaculoDestruible.ParedCobertura ? "CoberturaPared_" : "PlataformaCaja_";
        GameObject go = new GameObject(prefijo + numero);
        Undo.RegisterCreatedObjectUndo(go, "Agregar " + prefijo + numero);
        go.transform.SetParent(carpeta, false);
        go.transform.position = pos;

        ObjetoDestruible obs = go.AddComponent<ObjetoDestruible>();
        obs.EstablecerTipo(tipo);

        Selection.activeGameObject = go;
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
    }

    private static bool IntentarBuscarPisoDebajo(Vector3 origen, GameObject mapa, out float yPiso)
    {
        yPiso = origen.y;
        RaycastHit[] hits = Physics.RaycastAll(origen + Vector3.up * 1.2f, Vector3.down, 25f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
        float mejor = float.MaxValue;
        bool encontrado = false;

        foreach (RaycastHit h in hits)
        {
            if (h.distance <= 0.05f || h.collider == null) continue;
            if (mapa != null && !h.collider.transform.IsChildOf(mapa.transform)) continue;
            if (h.collider.GetComponentInParent<ObjetoDestruible>() != null) continue;
            if (h.normal.y < 0.65f) continue;

            if (h.distance < mejor)
            {
                mejor = h.distance;
                yPiso = h.point.y;
                encontrado = true;
            }
        }
        return encontrado;
    }

    private static float CalcularPlanoZ(List<Vector3> spawnsJugador, List<Vector3> spawnsArmas, GameObject mapa)
    {
        List<float> zs = new List<float>();
        foreach (Vector3 v in spawnsJugador) zs.Add(v.z);
        if (zs.Count == 0)
        {
            foreach (Vector3 v in spawnsArmas) zs.Add(v.z);
        }
        if (zs.Count == 0) return mapa != null ? mapa.transform.position.z : 0f;
        zs.Sort();
        return zs[zs.Count / 2];
    }

    private static List<GameObject> ObtenerMapasEscena()
    {
        List<GameObject> mapas = new List<GameObject>();
        inicio menu = Object.FindFirstObjectByType<inicio>(FindObjectsInactive.Include);
        if (menu != null && menu.Mapas != null)
        {
            foreach (GameObject m in menu.Mapas)
            {
                if (m != null && !mapas.Contains(m)) mapas.Add(m);
            }
        }

        foreach (GameObject go in Object.FindObjectsByType<GameObject>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (go != null && go.transform.parent == null && go.name.StartsWith("ecena", System.StringComparison.OrdinalIgnoreCase))
            {
                if (!mapas.Contains(go)) mapas.Add(go);
            }
        }

        GameObject mapa3d = GameObject.Find("Mapa3D");
        if (mapa3d != null && !mapas.Contains(mapa3d)) mapas.Add(mapa3d);

        return mapas;
    }

    private static GameObject ObtenerMapaSeleccionadoOPrimero()
    {
        if (Selection.activeGameObject != null)
        {
            Transform raiz = Selection.activeGameObject.transform.root;
            if (raiz.name == "Mapa3D" || raiz.name.StartsWith("ecena", System.StringComparison.OrdinalIgnoreCase))
                return raiz.gameObject;
        }

        List<GameObject> mapas = ObtenerMapasEscena();
        foreach (GameObject m in mapas)
        {
            if (m != null && m.activeInHierarchy) return m;
        }
        return mapas.Count > 0 ? mapas[0] : null;
    }
}
