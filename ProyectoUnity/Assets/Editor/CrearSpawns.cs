using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using System.Collections.Generic;

// Menu "Herramientas" del editor: crea los spawns que falten dentro del mapa seleccionado
// y prepara las 5 armas del proyecto con GestorArmas.
public static class CrearSpawns
{
    private const int JUGADORES = 8;
    private const int ARMAS = 5;
    private const float SEPARACION = 2.5f;

    private struct DatosModeloArma
    {
        public string nombre;
        public string rutaFbx;
        public Vector3 escala;
        public Vector3 rotacionEuler;
        public float cadencia;
        public float alcance;
        public float dispersion;
        public int municionMax;
        public float tiempoRecarga;
        public ModoDisparo modo;
    }

    private static readonly DatosModeloArma[] MODELOS_ARMAS = new DatosModeloArma[]
    {
        new DatosModeloArma { nombre = "ak47",   rutaFbx = "Assets/Modelos/ak/ak47.fbx",       escala = new Vector3(0.20f, 0.20f, 0.20f),    rotacionEuler = Vector3.zero,                cadencia = 9.5f,  alcance = 7.5f, dispersion = 2.5f, municionMax = 20, tiempoRecarga = 1.8f, modo = ModoDisparo.Automatico },
        new DatosModeloArma { nombre = "barret", rutaFbx = "Assets/Modelos/barret/barret.fbx", escala = new Vector3(0.19f, 0.19f, 0.19f),    rotacionEuler = new Vector3(0f, -90f, 90f),  cadencia = 1.5f,  alcance = 15f,  dispersion = 0.1f, municionMax = 5,  tiempoRecarga = 2.5f, modo = ModoDisparo.Semiautomatico },
        new DatosModeloArma { nombre = "mac10",  rutaFbx = "Assets/Modelos/mac10/mac10.fbx",   escala = new Vector3(0.115f, 0.115f, 0.115f), rotacionEuler = new Vector3(0f, -90f, 90f),  cadencia = 16f,   alcance = 4.0f, dispersion = 0.5f, municionMax = 15, tiempoRecarga = 1.3f, modo = ModoDisparo.Automatico },
        new DatosModeloArma { nombre = "bereta", rutaFbx = "Assets/Modelos/bereta/bereta.fbx", escala = new Vector3(0.125f, 0.125f, 0.125f), rotacionEuler = new Vector3(0f, 180f, 0f),   cadencia = 5f,    alcance = 5.5f, dispersion = 0.8f, municionMax = 12, tiempoRecarga = 1.2f, modo = ModoDisparo.Semiautomatico },
        new DatosModeloArma { nombre = "raygun", rutaFbx = "Assets/Modelos/raygun/raygun.fbx", escala = new Vector3(0.046f, 0.046f, 0.046f), rotacionEuler = new Vector3(90f, 180f, 0f),  cadencia = 20f,   alcance = 80f,  dispersion = 0.0f, municionMax = 10, tiempoRecarga = 2.0f, modo = ModoDisparo.RayoContinuo },
    };

    // Coordenadas exactas de los 6 spawnArma en ecenaCochera (locales a Poster_fondo)
    private static readonly Vector3[] SPAWNS_COCHERA_POSTER = new Vector3[]
    {
        new Vector3(0.071f,   0.30077758f, -0.013f),
        new Vector3(-0.295f,  0.322f,      -0.0924f),
        new Vector3(-0.3712f, -0.158f,     -0.0846f),
        new Vector3(0.364f,   -0.152f,     -0.0528f),
        new Vector3(-0.2785f, -0.34f,      -0.048f),
        new Vector3(0.38f,    0.1063f,     -0.057f),
    };

    // Coordenadas exactas de los 5 spawnArma en ecenaCosina (locales a ecenaCosina)
    private static readonly Vector3[] SPAWNS_COSINA_LOCAL = new Vector3[]
    {
        new Vector3(-0.39f,      5.97f,  2.92f),
        new Vector3(0.45000076f, 3.04f,  3.26f),
        new Vector3(10.35f,      8.51f,  2.9f),
        new Vector3(3.5f,        1.291f, 2.969f),
        new Vector3(10.57f,      4.38f,  3.058f),
    };

    [InitializeOnLoadMethod]
    private static void InicializarAutoIntegracion()
    {
        EditorApplication.delayCall += VerificarEIntegrarEscenaJuego;
        EditorSceneManager.sceneOpened += (scene, mode) => VerificarEIntegrarEscenaJuego();
    }

    private static void VerificarEIntegrarEscenaJuego()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;

        var escena = EditorSceneManager.GetActiveScene();
        if (!escena.IsValid() || !escena.isLoaded || escena.name != "Juego") return;

        // Si la escena Juego aún no tiene GestorArmas, integrar todo y guardar
        if (Object.FindFirstObjectByType<GestorArmas>(FindObjectsInactive.Include) == null)
        {
            ConfigurarTodoParaProbarArmas();
            EditorSceneManager.SaveScene(escena);
            Debug.Log("Fusión automática de Juego.unity completada y guardada: 4 mapas de compañeros + spawns de armas + 5 armas + GestorArmas.");
            return;
        }

        // Si alguna arma en Juego.unity conserva escalas/rotaciones antiguas (ej. bereta a 25), actualizarla automáticamente
        bool requiereActualizarArmas = false;
        for (int i = 0; i < MODELOS_ARMAS.Length; i++)
        {
            GameObject go = BuscarObjetoArmaEnEscena(MODELOS_ARMAS[i].nombre);
            if (go != null && (Vector3.Distance(go.transform.localScale, MODELOS_ARMAS[i].escala) > 0.01f
                || Quaternion.Angle(go.transform.rotation, Quaternion.Euler(MODELOS_ARMAS[i].rotacionEuler)) > 1f))
            {
                requiereActualizarArmas = true;
                break;
            }
        }

        if (requiereActualizarArmas)
        {
            PrepararArmasYGestor();
            EditorSceneManager.SaveScene(escena);
            Debug.Log("CrearSpawns: Escalas y rotaciones de las 5 armas actualizadas y guardadas en Juego.unity.");
        }
    }

    [MenuItem("Herramientas/Configurar Todo para Probar Armas (Spawns + 5 Armas + GestorArmas)", false, 1)]
    private static void ConfigurarTodoParaProbarArmas()
    {
        List<GameObject> mapas = ObtenerTodosLosMapasEnEscena();
        if (mapas.Count == 0)
        {
            GameObject mapaFallback = new GameObject("Mapa3D");
            Undo.RegisterCreatedObjectUndo(mapaFallback, "Crear Mapa3D");
            mapas.Add(mapaFallback);
        }

        foreach (GameObject mapa in mapas)
        {
            CrearSpawnsEnMapa(mapa, GameManager.PREFIJO_ARMA, ARMAS);
        }

        PrepararArmasYGestor();
        var escenaActiva = EditorSceneManager.GetActiveScene();
        EditorSceneManager.MarkSceneDirty(escenaActiva);
        EditorSceneManager.SaveScene(escenaActiva);
        Debug.Log("¡Configuración completa y guardada! Ya puedes presionar Play para probar los spawns aleatorios y las cajas de madera (usa C para recoger, V para romper/disparar, T para re-spawnear cajas).");
    }

    [MenuItem("Herramientas/Crear spawns de jugador en el mapa seleccionado", false, 20)]
    private static void CrearSpawnsJugador()
    {
        Crear(GameManager.PREFIJO_JUGADOR, JUGADORES);
    }

    [MenuItem("Herramientas/Crear spawns de armas en el mapa seleccionado", false, 21)]
    private static void CrearSpawnsArmas()
    {
        Crear(GameManager.PREFIJO_ARMA, ARMAS);
    }

    [MenuItem("Herramientas/Preparar las 5 armas y GestorArmas en la escena", false, 22)]
    private static void PrepararArmasYGestor()
    {
        Personaje jugadorRef = Object.FindFirstObjectByType<Personaje>(FindObjectsInactive.Include);
        float zPlano = jugadorRef != null ? jugadorRef.transform.position.z : 6.16f;
        float yBase  = jugadorRef != null ? jugadorRef.transform.position.y - 1.5f : 3.25f;
        float xBase  = jugadorRef != null ? jugadorRef.transform.position.x - 4f : 2.0f;

        List<Arma> armasEnEscena = new List<Arma>();
        Material matTrazo = AssetDatabase.LoadAssetAtPath<Material>("Assets/trazo.mat");

        for (int i = 0; i < MODELOS_ARMAS.Length; i++)
        {
            DatosModeloArma datos = MODELOS_ARMAS[i];
            GameObject instancia = BuscarObjetoArmaEnEscena(datos.nombre);

            if (instancia == null)
            {
                GameObject prefabFbx = AssetDatabase.LoadAssetAtPath<GameObject>(datos.rutaFbx);
                if (prefabFbx == null)
                {
                    Debug.LogWarning("No se encontró el modelo en: " + datos.rutaFbx);
                    continue;
                }

                instancia = (GameObject)PrefabUtility.InstantiatePrefab(prefabFbx);
                Undo.RegisterCreatedObjectUndo(instancia, "Crear arma " + datos.nombre);
                instancia.transform.position = new Vector3(xBase + i * 2.2f, yBase, zPlano);
            }

            Undo.RecordObject(instancia.transform, "Configurar transform arma " + datos.nombre);
            instancia.name = datos.nombre;
            instancia.transform.rotation = Quaternion.Euler(datos.rotacionEuler);
            instancia.transform.localScale = datos.escala;

            BoxCollider box = instancia.GetComponent<BoxCollider>();
            if (box == null) box = Undo.AddComponent<BoxCollider>(instancia);
            box.isTrigger = true;

            LineRenderer lr = instancia.GetComponent<LineRenderer>();
            if (lr == null) lr = Undo.AddComponent<LineRenderer>(instancia);
            lr.enabled = false;
            lr.startWidth = 0.05f;
            lr.endWidth = 0.05f;
            lr.useWorldSpace = true;
            if (matTrazo != null) lr.sharedMaterial = matTrazo;

            Arma armaComp = instancia.GetComponent<Arma>();
            if (armaComp == null) armaComp = Undo.AddComponent<Arma>(instancia);

            SerializedObject soArma = new SerializedObject(armaComp);
            soArma.FindProperty("Cadencia").floatValue = datos.cadencia;
            soArma.FindProperty("Alcance").floatValue = datos.alcance;
            soArma.FindProperty("Dispersion").floatValue = datos.dispersion;
            soArma.FindProperty("municionMaxima").intValue = datos.municionMax;
            soArma.FindProperty("tiempoRecarga").floatValue = datos.tiempoRecarga;
            soArma.FindProperty("modoDisparo").enumValueIndex = (int)datos.modo;
            soArma.FindProperty("iniciarEnCaja").boolValue = true;
            soArma.FindProperty("soltarAlRomperCaja").boolValue = true;
            soArma.FindProperty("offsetAreaRecogida").vector3Value = Vector3.zero;
            soArma.FindProperty("tamanoAreaRecogida").vector3Value = Vector3.zero;
            soArma.ApplyModifiedProperties();

            armasEnEscena.Add(armaComp);
        }

        // Buscar o crear GestorArmas
        GestorArmas gestor = Object.FindFirstObjectByType<GestorArmas>(FindObjectsInactive.Include);
        if (gestor == null)
        {
            GameManager gm = Object.FindFirstObjectByType<GameManager>(FindObjectsInactive.Include);
            GameObject contenedor = gm != null ? gm.gameObject : new GameObject("GestorArmas");
            if (gm == null) Undo.RegisterCreatedObjectUndo(contenedor, "Crear GestorArmas");
            gestor = Undo.AddComponent<GestorArmas>(contenedor);
        }

        SerializedObject soGestor = new SerializedObject(gestor);
        SerializedProperty propLista = soGestor.FindProperty("todasLasArmas");
        propLista.arraySize = armasEnEscena.Count;
        for (int i = 0; i < armasEnEscena.Count; i++)
        {
            propLista.GetArrayElementAtIndex(i).objectReferenceValue = armasEnEscena[i];
        }
        soGestor.FindProperty("maxArmasActivas").intValue = armasEnEscena.Count;
        soGestor.ApplyModifiedProperties();

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        Selection.activeGameObject = gestor.gameObject;
        Debug.Log($"Listo: {armasEnEscena.Count} armas configuradas en GestorArmas ({string.Join(", ", armasEnEscena.ConvertAll(a => a.name))}).");
    }

    private static GameObject BuscarObjetoArmaEnEscena(string nombre)
    {
        foreach (Arma a in Object.FindObjectsByType<Arma>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (a != null && a.gameObject.name.Equals(nombre, System.StringComparison.OrdinalIgnoreCase))
                return a.gameObject;
        }

        foreach (Transform t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (t != null && t.parent == null && t.name.Equals(nombre, System.StringComparison.OrdinalIgnoreCase))
                return t.gameObject;
        }

        return null;
    }

    private static List<GameObject> ObtenerTodosLosMapasEnEscena()
    {
        List<GameObject> mapas = new List<GameObject>();

        GameObject mapa3D = GameObject.Find("Mapa3D");
        if (mapa3D != null) mapas.Add(mapa3D);

        foreach (GameObject go in Object.FindObjectsByType<GameObject>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (go != null && go.transform.parent == null && go.name.StartsWith("ecena", System.StringComparison.OrdinalIgnoreCase))
            {
                if (!mapas.Contains(go))
                    mapas.Add(go);
            }
        }

        if (mapas.Count == 0 && Selection.activeGameObject != null)
            mapas.Add(Selection.activeGameObject);

        return mapas;
    }

    private static GameObject ObtenerMapaObjetivo()
    {
        if (Selection.activeGameObject != null)
        {
            Transform raiz = Selection.activeGameObject.transform.root;
            if (raiz.name == "Mapa3D" || raiz.name.StartsWith("ecena", System.StringComparison.OrdinalIgnoreCase))
                return raiz.gameObject;
        }

        List<GameObject> mapas = ObtenerTodosLosMapasEnEscena();
        return mapas.Count > 0 ? mapas[0] : Selection.activeGameObject;
    }

    private static void Crear(string prefijo, int total)
    {
        GameObject mapa = ObtenerMapaObjetivo();
        if (mapa == null)
        {
            Debug.LogWarning("Primero selecciona el mapa (ej. Mapa3D) en la Hierarchy");
            return;
        }

        List<Object> creados = CrearSpawnsEnMapa(mapa, prefijo, total);
        if (creados.Count > 0) Selection.objects = creados.ToArray();
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
    }

    private static List<Object> CrearSpawnsEnMapa(GameObject mapa, string prefijo, int total)
    {
        List<Object> creados = new List<Object>();
        if (mapa == null) return creados;

        List<Transform> existentes = GameManager.BuscarPuntos(mapa, prefijo);

        // Si estamos creando spawns de armas en ecenaCochera o ecenaCosina, usar las coordenadas exactas definidas
        if (prefijo == GameManager.PREFIJO_ARMA && existentes.Count == 0)
        {
            if (mapa.name.Equals("ecenaCochera", System.StringComparison.OrdinalIgnoreCase))
            {
                Transform posterFondo = null;
                foreach (Transform t in mapa.GetComponentsInChildren<Transform>(true))
                {
                    if (t.name == "Poster_fondo")
                    {
                        posterFondo = t;
                        break;
                    }
                }

                Transform padre = posterFondo != null ? posterFondo : mapa.transform;
                for (int i = 0; i < SPAWNS_COCHERA_POSTER.Length; i++)
                {
                    GameObject punto = new GameObject(prefijo + (i + 1));
                    Undo.RegisterCreatedObjectUndo(punto, "Crear spawn " + prefijo + (i + 1));
                    punto.transform.SetParent(padre, false);
                    if (posterFondo != null)
                    {
                        punto.transform.localPosition = SPAWNS_COCHERA_POSTER[i];
                    }
                    else
                    {
                        Vector3 p = SPAWNS_COCHERA_POSTER[i];
                        punto.transform.localPosition = new Vector3(-p.x * 19.527935f, 4.829999f + p.y * 9.974148f, -3.87f - p.z * 11.585816f);
                    }
                    creados.Add(punto);
                }

                Debug.Log(mapa.name + ": " + SPAWNS_COCHERA_POSTER.Length + " spawns exactos con prefijo " + prefijo);
                return creados;
            }

            if (mapa.name.Equals("ecenaCosina", System.StringComparison.OrdinalIgnoreCase))
            {
                for (int i = 0; i < SPAWNS_COSINA_LOCAL.Length; i++)
                {
                    GameObject punto = new GameObject(prefijo + (i + 1));
                    Undo.RegisterCreatedObjectUndo(punto, "Crear spawn " + prefijo + (i + 1));
                    punto.transform.SetParent(mapa.transform, false);
                    punto.transform.localPosition = SPAWNS_COSINA_LOCAL[i];
                    creados.Add(punto);
                }

                Debug.Log(mapa.name + ": " + SPAWNS_COSINA_LOCAL.Length + " spawns exactos con prefijo " + prefijo);
                return creados;
            }
        }

        List<Transform> spawnsJugador = GameManager.BuscarPuntos(mapa, GameManager.PREFIJO_JUGADOR);

        // Si el mapa ya tiene spawns de jugador (ej. ecenaSala, ecenaPatio), colocar los spawnArma sobre esas plataformas
        if (prefijo == GameManager.PREFIJO_ARMA && spawnsJugador.Count > 0)
        {
            for (int i = existentes.Count; i < total; i++)
            {
                Transform refSpawn = spawnsJugador[i % spawnsJugador.Count];
                GameObject punto = new GameObject(prefijo + (i + 1));
                Undo.RegisterCreatedObjectUndo(punto, "Crear spawn " + prefijo + (i + 1));
                punto.transform.SetParent(mapa.transform);
                punto.transform.position = refSpawn.position + new Vector3(0.6f, 0f, 0f);
                creados.Add(punto);
            }

            Debug.Log(mapa.name + ": " + Mathf.Max(total, existentes.Count) + " spawns en plataformas con prefijo " + prefijo);
            return creados;
        }

        Personaje jugadorRef = Object.FindFirstObjectByType<Personaje>(FindObjectsInactive.Include);

        float zPlano = jugadorRef != null ? jugadorRef.transform.position.z
            : (spawnsJugador.Count > 0 ? spawnsJugador[0].position.z : 6.16f);
        float yBase = spawnsJugador.Count > 0 ? spawnsJugador[0].position.y
            : (jugadorRef != null ? jugadorRef.transform.position.y - 1.5f : 3.25f);

        float xInicio = mapa.transform.position.x + 2.0f;
        if (spawnsJugador.Count > 0)
        {
            float minX = float.MaxValue;
            foreach (Transform sp in spawnsJugador)
                if (sp.position.x < minX) minX = sp.position.x;
            xInicio = minX + 1.5f;
        }
        else if (jugadorRef != null)
        {
            xInicio = jugadorRef.transform.position.x - 5.0f;
        }

        Vector3 posicion = existentes.Count > 0
            ? existentes[existentes.Count - 1].position
            : new Vector3(xInicio - SEPARACION, yBase, zPlano);

        for (int i = existentes.Count + 1; i <= total; i++)
        {
            posicion += Vector3.right * SEPARACION;

            GameObject punto = new GameObject(prefijo + i);
            Undo.RegisterCreatedObjectUndo(punto, "Crear spawn " + prefijo + i);
            punto.transform.SetParent(mapa.transform);
            punto.transform.position = posicion;
            creados.Add(punto);
        }

        Debug.Log(mapa.name + ": " + Mathf.Max(total, existentes.Count) + " spawns con prefijo " + prefijo);
        return creados;
    }
}
