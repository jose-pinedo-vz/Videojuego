using System.IO;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

public static class ConfigurarPersonajesYAudio
{
    private const string CLAVE_SESION = "ConfigurarPersonajesYAudio_Ejecutado_v1";
    private const string CARPETA_RESOURCES = "Assets/Resources";
    private const string CARPETA_PERSONAJES = "Assets/Resources/Personajes";
    private const string CARPETA_AUDIO_RES = "Assets/Resources/Audio";
    private const string CARPETA_MAT_PERSONAJES = "Assets/Materiales/Personajes";
    private const string RUTA_CATALOGO = "Assets/Resources/Personajes/CatalogoPersonajes.asset";

    private const float ALTURA_OBJETIVO = 0.60f;

    private struct DefinicionAnimal
    {
        public string id;
        public string nombreAnimal;
        public string subnombre;
        public string rutaFbx;
        public string rutaTextura;
        public string nombreMaterial;
        public string nombrePrefab;
        public float escalaFallback;
        public float offsetYFallback;
    }

    private static readonly DefinicionAnimal[] DEFINICIONES = new DefinicionAnimal[]
    {
        new DefinicionAnimal
        {
            id = "gato",
            nombreAnimal = "Gato",
            subnombre = "Cheto",
            rutaFbx = "Assets/Personaje gato.fbx",
            rutaTextura = "",
            nombreMaterial = "",
            nombrePrefab = "Cheto_Gato",
            escalaFallback = 0.30f,
            offsetYFallback = 0.0f
        },
        new DefinicionAnimal
        {
            id = "perro",
            nombreAnimal = "Perro",
            subnombre = "Kaiser",
            rutaFbx = "Assets/Modelos/perro/perro.fbx",
            rutaTextura = "Assets/Modelos/perro/color perro.png",
            nombreMaterial = "Mat_Kaiser_Perro",
            nombrePrefab = "Kaiser_Perro",
            escalaFallback = 0.19f,
            offsetYFallback = 0.004f
        },
        new DefinicionAnimal
        {
            id = "raton",
            nombreAnimal = "Ratón",
            subnombre = "Dorito",
            rutaFbx = "Assets/Modelos/Diego/raton.fbx",
            rutaTextura = "Assets/Modelos/Diego/color raton.jpg",
            nombreMaterial = "Mat_Dorito_Raton",
            nombrePrefab = "Dorito_Raton",
            escalaFallback = 0.11f,
            offsetYFallback = 0.0f
        },
        new DefinicionAnimal
        {
            id = "cabra",
            nombreAnimal = "Cabra",
            subnombre = "CR7",
            rutaFbx = "Assets/Modelos/CR7 GOAT/GOAT.fbx",
            rutaTextura = "Assets/Modelos/CR7 GOAT/Color cabra.jpg",
            nombreMaterial = "Mat_CR7_Cabra",
            nombrePrefab = "CR7_Cabra",
            escalaFallback = 0.056f,
            offsetYFallback = 0.02f
        }
    };

    [InitializeOnLoadMethod]
    private static void InicializarAutomatico()
    {
        EditorApplication.delayCall += VerificarYConfigurarSiHaceFalta;
    }

    private static void VerificarYConfigurarSiHaceFalta()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating)
            return;

        CatalogoPersonajes cat = AssetDatabase.LoadAssetAtPath<CatalogoPersonajes>(RUTA_CATALOGO);
        bool faltaAudioRaygun = AssetDatabase.LoadAssetAtPath<AudioClip>(CARPETA_AUDIO_RES + "/raygun_disparo.mp3") == null;
        bool faltaMusica = AssetDatabase.LoadAssetAtPath<AudioClip>(CARPETA_AUDIO_RES + "/Throttle_Locked_Open.mp3") == null;

        bool faltaAlgunPersonaje = cat == null || cat.personajes == null || cat.personajes.Length < CatalogoPersonajes.TOTAL_PERSONAJES;
        if (!faltaAlgunPersonaje)
        {
            for (int i = 0; i < CatalogoPersonajes.TOTAL_PERSONAJES; i++)
            {
                if (cat.personajes[i] == null || cat.personajes[i].prefabVisual == null || cat.personajes[i].avatar == null)
                {
                    faltaAlgunPersonaje = true;
                    break;
                }
            }
        }

        if (!faltaAlgunPersonaje && !faltaAudioRaygun && !faltaMusica && SessionState.GetBool(CLAVE_SESION, false))
            return;

        SessionState.SetBool(CLAVE_SESION, true);
        ConfigurarTodo();
    }

    [MenuItem("Herramientas/Configurar Personajes (4 Animales) y Audios", false, 2)]
    public static void ConfigurarTodo()
    {
        AsegurarCarpetas();
        SincronizarAudiosEnResources();
        ConstruirCatalogoYPrefabsPersonajes();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[ConfigurarPersonajesYAudio] 4 personajes (Cheto, Kaiser, Dorito, CR7) en Humanoid y audios listos en Resources.");
    }

    private static void AsegurarCarpetas()
    {
        CrearCarpetaSiNoExiste("Assets", "Resources");
        CrearCarpetaSiNoExiste(CARPETA_RESOURCES, "Personajes");
        CrearCarpetaSiNoExiste(CARPETA_RESOURCES, "Audio");
        CrearCarpetaSiNoExiste("Assets", "Materiales");
        CrearCarpetaSiNoExiste("Assets/Materiales", "Personajes");
    }

    private static void CrearCarpetaSiNoExiste(string padre, string nombre)
    {
        string ruta = padre + "/" + nombre;
        if (!AssetDatabase.IsValidFolder(ruta))
        {
            AssetDatabase.CreateFolder(padre, nombre);
        }
    }

    private static void SincronizarAudiosEnResources()
    {
        CopiarYConfigurarAudio(
            "Assets/Audio/raygun audio disparo.mp3",
            CARPETA_AUDIO_RES + "/raygun_disparo.mp3",
            AudioClipLoadType.DecompressOnLoad,
            false);

        CopiarYConfigurarAudio(
            "Assets/Audio/Throttle_Locked_Open.mp3",
            CARPETA_AUDIO_RES + "/Throttle_Locked_Open.mp3",
            AudioClipLoadType.CompressedInMemory,
            true);
    }

    private static void CopiarYConfigurarAudio(string rutaOrigen, string rutaDestino, AudioClipLoadType loadType, bool enSegundoPlano)
    {
        string absOrigen = Path.Combine(Directory.GetCurrentDirectory(), rutaOrigen.Replace('/', Path.DirectorySeparatorChar));
        string absDestino = Path.Combine(Directory.GetCurrentDirectory(), rutaDestino.Replace('/', Path.DirectorySeparatorChar));

        if (!File.Exists(absOrigen))
        {
            Debug.LogWarning("[ConfigurarPersonajesYAudio] No se encontró el audio origen: " + rutaOrigen);
            return;
        }

        bool necesitaCopiar = !File.Exists(absDestino);
        if (!necesitaCopiar)
        {
            FileInfo fiOrig = new FileInfo(absOrigen);
            FileInfo fiDest = new FileInfo(absDestino);
            if (fiOrig.Length != fiDest.Length) necesitaCopiar = true;
        }

        if (necesitaCopiar)
        {
            string dirDest = Path.GetDirectoryName(absDestino);
            if (!string.IsNullOrEmpty(dirDest) && !Directory.Exists(dirDest))
                Directory.CreateDirectory(dirDest);

            File.Copy(absOrigen, absDestino, true);
            AssetDatabase.ImportAsset(rutaDestino, ImportAssetOptions.ForceSynchronousImport);
        }

        AudioImporter audioImp = AssetImporter.GetAtPath(rutaDestino) as AudioImporter;
        if (audioImp != null)
        {
            bool modificado = false;
            if (audioImp.loadInBackground != enSegundoPlano)
            {
                audioImp.loadInBackground = enSegundoPlano;
                modificado = true;
            }

            AudioImporterSampleSettings settings = audioImp.defaultSampleSettings;
            if (settings.loadType != loadType)
            {
                settings.loadType = loadType;
                settings.compressionFormat = loadType == AudioClipLoadType.DecompressOnLoad
                    ? AudioCompressionFormat.PCM
                    : AudioCompressionFormat.Vorbis;
                settings.quality = 0.75f;
                audioImp.defaultSampleSettings = settings;
                modificado = true;
            }

            if (modificado)
            {
                audioImp.SaveAndReimport();
            }
        }
    }

    private static Material CrearOActualizarMaterialPersonaje(string nombreMat, string rutaTextura)
    {
        if (string.IsNullOrEmpty(nombreMat) || string.IsNullOrEmpty(rutaTextura))
            return null;

        Texture2D tex = AssetDatabase.LoadAssetAtPath<Texture2D>(rutaTextura);
        if (tex == null)
        {
            AssetDatabase.ImportAsset(rutaTextura, ImportAssetOptions.ForceSynchronousImport);
            tex = AssetDatabase.LoadAssetAtPath<Texture2D>(rutaTextura);
        }

        string rutaMat = CARPETA_MAT_PERSONAJES + "/" + nombreMat + ".mat";
        Material mat = AssetDatabase.LoadAssetAtPath<Material>(rutaMat);

        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null) shader = Shader.Find("Standard");

        if (mat == null)
        {
            mat = new Material(shader);
            AssetDatabase.CreateAsset(mat, rutaMat);
        }
        else if (shader != null && mat.shader != shader)
        {
            mat.shader = shader;
        }

        if (tex != null)
        {
            if (mat.HasProperty("_BaseMap")) mat.SetTexture("_BaseMap", tex);
            if (mat.HasProperty("_MainTex")) mat.SetTexture("_MainTex", tex);
            mat.mainTexture = tex;
        }

        if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", Color.white);
        mat.color = Color.white;
        if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", 0.15f);
        if (mat.HasProperty("_Metallic")) mat.SetFloat("_Metallic", 0f);

        EditorUtility.SetDirty(mat);
        return mat;
    }

    private static Avatar ConfigurarImportadorHumanoid(DefinicionAnimal def, Material matAsignado)
    {
        ModelImporter imp = AssetImporter.GetAtPath(def.rutaFbx) as ModelImporter;
        if (imp == null)
        {
            Debug.LogWarning("[ConfigurarPersonajesYAudio] No se encontró FBX en: " + def.rutaFbx);
            return null;
        }

        bool reimportar = false;

        if (imp.animationType != ModelImporterAnimationType.Human)
        {
            imp.animationType = ModelImporterAnimationType.Human;
            imp.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            imp.autoGenerateAvatarMappingIfUnspecified = true;
            reimportar = true;
        }
        else if (imp.avatarSetup == ModelImporterAvatarSetup.NoAvatar)
        {
            imp.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            reimportar = true;
        }

        if (imp.importCameras)
        {
            imp.importCameras = false;
            reimportar = true;
        }

        if (imp.importLights)
        {
            imp.importLights = false;
            reimportar = true;
        }

        if (matAsignado != null)
        {
            foreach (Object sub in AssetDatabase.LoadAllAssetsAtPath(def.rutaFbx))
            {
                if (sub is Material matOrig)
                {
                    imp.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), matOrig.name), matAsignado);
                    reimportar = true;
                }
            }
        }

        if (reimportar)
        {
            imp.SaveAndReimport();
        }

        foreach (Object sub in AssetDatabase.LoadAllAssetsAtPath(def.rutaFbx))
        {
            if (sub is Avatar av && av.isValid && av.isHuman)
            {
                return av;
            }
        }

        // Segundo intento por si el Avatar existe aunque no reporte isHuman inmediatamente
        foreach (Object sub in AssetDatabase.LoadAllAssetsAtPath(def.rutaFbx))
        {
            if (sub is Avatar av) return av;
        }

        return null;
    }

    private static void ConstruirCatalogoYPrefabsPersonajes()
    {
        CatalogoPersonajes catalogo = AssetDatabase.LoadAssetAtPath<CatalogoPersonajes>(RUTA_CATALOGO);
        if (catalogo == null)
        {
            catalogo = ScriptableObject.CreateInstance<CatalogoPersonajes>();
            AssetDatabase.CreateAsset(catalogo, RUTA_CATALOGO);
        }

        if (catalogo.personajes == null || catalogo.personajes.Length != CatalogoPersonajes.TOTAL_PERSONAJES)
        {
            catalogo.personajes = new EntradaPersonaje[CatalogoPersonajes.TOTAL_PERSONAJES];
        }

        GameObject cuerpoPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/cuerpo.prefab");

        for (int i = 0; i < DEFINICIONES.Length; i++)
        {
            DefinicionAnimal def = DEFINICIONES[i];
            Material mat = CrearOActualizarMaterialPersonaje(def.nombreMaterial, def.rutaTextura);
            Avatar avatar = ConfigurarImportadorHumanoid(def, mat);

            string rutaPrefabDestino = CARPETA_PERSONAJES + "/" + def.nombrePrefab + ".prefab";
            Vector3 escalaCalculada = new Vector3(def.escalaFallback, def.escalaFallback, def.escalaFallback);
            Vector3 offsetCalculado = new Vector3(0f, def.offsetYFallback, 0f);

            GameObject tempInstancia = null;

            // Para Cheto (Gato), tomamos el hijo "Personaje gato" de cuerpo.prefab que ya conserva sus 7 materiales y ojos ajustados
            if (def.id == "gato" && cuerpoPrefab != null)
            {
                Transform hijoGato = cuerpoPrefab.transform.Find("Personaje gato");
                if (hijoGato != null)
                {
                    tempInstancia = Object.Instantiate(hijoGato.gameObject);
                    escalaCalculada = new Vector3(0.30f, 0.30f, 0.30f);
                    offsetCalculado = Vector3.zero;
                }
            }

            if (tempInstancia == null)
            {
                GameObject modeloFbx = AssetDatabase.LoadAssetAtPath<GameObject>(def.rutaFbx);
                if (modeloFbx == null)
                {
                    Debug.LogWarning("[ConfigurarPersonajesYAudio] No se pudo cargar el modelo: " + def.rutaFbx);
                    continue;
                }
                tempInstancia = Object.Instantiate(modeloFbx);
            }

            tempInstancia.name = def.nombrePrefab;
            tempInstancia.transform.position = Vector3.zero;
            tempInstancia.transform.rotation = Quaternion.identity;
            tempInstancia.transform.localScale = Vector3.one;

            LimpiarComponentesExtrasEnModelo(tempInstancia, mat);

            if (def.id != "gato")
            {
                if (CalcularBoundsModelo(tempInstancia, out Bounds bUnitario) && bUnitario.size.y > 0.01f)
                {
                    float s = ALTURA_OBJETIVO / bUnitario.size.y;
                    escalaCalculada = new Vector3(s, s, s);
                    tempInstancia.transform.localScale = escalaCalculada;

                    if (CalcularBoundsModelo(tempInstancia, out Bounds bEscalado))
                    {
                        offsetCalculado = new Vector3(0f, -bEscalado.min.y, 0f);
                    }
                }
            }

            // El prefab se guarda con escala (1,1,1) o base limpia; CatalogoPersonajes aplica escalaLocal y posicionLocal exactas al instanciar
            tempInstancia.transform.position = Vector3.zero;
            tempInstancia.transform.rotation = Quaternion.identity;
            tempInstancia.transform.localScale = escalaCalculada;

            GameObject prefabGuardado = PrefabUtility.SaveAsPrefabAsset(tempInstancia, rutaPrefabDestino);
            Object.DestroyImmediate(tempInstancia);

            catalogo.personajes[i] = new EntradaPersonaje
            {
                id = def.id,
                nombreAnimal = def.nombreAnimal,
                subnombre = def.subnombre,
                prefabVisual = prefabGuardado,
                avatar = avatar,
                escalaLocal = escalaCalculada,
                posicionLocal = offsetCalculado,
                rotacionLocalEuler = Vector3.zero
            };
        }

        EditorUtility.SetDirty(catalogo);
    }

    private static void LimpiarComponentesExtrasEnModelo(GameObject raiz, Material matForzado)
    {
        if (raiz == null) return;

        // Quitar cámaras o luces que pudieran venir exportadas de Blender
        foreach (Camera c in raiz.GetComponentsInChildren<Camera>(true))
        {
            if (c != null) Object.DestroyImmediate(c.gameObject);
        }
        foreach (Light l in raiz.GetComponentsInChildren<Light>(true))
        {
            if (l != null) Object.DestroyImmediate(l.gameObject);
        }

        // Quitar Animator del hijo para que únicamente el Animator en "cuerpo" controle los huesos
        foreach (Animator a in raiz.GetComponentsInChildren<Animator>(true))
        {
            if (a != null) Object.DestroyImmediate(a);
        }

        if (matForzado != null)
        {
            foreach (Renderer r in raiz.GetComponentsInChildren<Renderer>(true))
            {
                if (r == null) continue;
                int slots = Mathf.Max(1, r.sharedMaterials.Length);
                Material[] mats = new Material[slots];
                for (int m = 0; m < slots; m++) mats[m] = matForzado;
                r.sharedMaterials = mats;
            }
        }
    }

    private static bool CalcularBoundsModelo(GameObject raiz, out Bounds total)
    {
        total = new Bounds(Vector3.zero, Vector3.zero);
        bool iniciado = false;

        foreach (Renderer r in raiz.GetComponentsInChildren<Renderer>(true))
        {
            if (r == null) continue;
            if (r is SkinnedMeshRenderer smr && smr.sharedMesh != null)
            {
                // Forzar actualización de bounds en pose actual
                smr.updateWhenOffscreen = true;
            }
            Bounds rb = r.bounds;
            if (rb.size.sqrMagnitude < 0.000001f) continue;

            if (!iniciado)
            {
                total = rb;
                iniciado = true;
            }
            else
            {
                total.Encapsulate(rb);
            }
        }

        return iniciado;
    }
}
