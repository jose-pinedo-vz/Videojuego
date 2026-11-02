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
        public int municionMax;
        public float tiempoRecarga;
    }

    private static readonly DatosModeloArma[] MODELOS_ARMAS = new DatosModeloArma[]
    {
        new DatosModeloArma { nombre = "ak47",   rutaFbx = "Assets/Modelos/ak/ak47.fbx",       escala = new Vector3(0.2f, 0.2f, 0.2f),       rotacionEuler = Vector3.zero,                 cadencia = 10f,  alcance = 6f,   municionMax = 30, tiempoRecarga = 1.8f },
        new DatosModeloArma { nombre = "barret", rutaFbx = "Assets/Modelos/barret/barret.fbx", escala = new Vector3(0.208f, 0.208f, 0.208f), rotacionEuler = new Vector3(0f, 90f, -90f),   cadencia = 1.5f, alcance = 15f,  municionMax = 5,  tiempoRecarga = 2.5f },
        new DatosModeloArma { nombre = "mac10",  rutaFbx = "Assets/Modelos/mac10/mac10.fbx",   escala = new Vector3(0.25f, 0.25f, 0.25f),    rotacionEuler = new Vector3(0f, 0f, 180f),    cadencia = 14f,  alcance = 4.5f, municionMax = 32, tiempoRecarga = 1.4f },
        new DatosModeloArma { nombre = "bereta", rutaFbx = "Assets/Modelos/bereta/bereta.fbx", escala = new Vector3(25f, 25f, 25f),          rotacionEuler = new Vector3(0f, 180f, 0f),    cadencia = 5f,   alcance = 5.5f, municionMax = 12, tiempoRecarga = 1.2f },
        new DatosModeloArma { nombre = "raygun", rutaFbx = "Assets/Modelos/raygun/raygun.fbx", escala = new Vector3(0.15f, 0.15f, 0.15f),    rotacionEuler = new Vector3(0f, 180f, 0f),    cadencia = 6f,   alcance = 8f,   municionMax = 15, tiempoRecarga = 2.0f },
    };

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
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        Debug.Log("¡Configuración completa! Ya puedes presionar Play para probar los spawns aleatorios y las cajas de madera (usa C para recoger, V para romper/disparar, T para re-spawnear cajas).");
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
            soArma.FindProperty("municionMaxima").intValue = datos.municionMax;
            soArma.FindProperty("tiempoRecarga").floatValue = datos.tiempoRecarga;
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
        List<Transform> spawnsJugador = GameManager.BuscarPuntos(mapa, GameManager.PREFIJO_JUGADOR);
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
