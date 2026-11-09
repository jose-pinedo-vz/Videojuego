using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

// Sonidos del juego. Los clips estan en Assets/Resources/Audio y los genera Docs/generar_sonidos.py.
// Uso desde cualquier script:  Sonidos.Reproducir("ui_click");  o con posicion:  Sonidos.Reproducir("impacto", punto);
public static class Sonidos
{
    public static void Reproducir(string nombre, float volumen = 1f, float tono = 1f)
    {
        GestorSonido g = GestorSonido.Obtener();
        if (g != null) g.Tocar(nombre, volumen, tono);
    }

    // el volumen baja con la distancia a la camara (jugadores lejanos suenan mas bajito)
    public static void Reproducir(string nombre, Vector3 posicion, float volumen = 1f, float tono = 1f)
    {
        Camera cam = Camera.main;
        if (cam != null)
        {
            float d = Vector2.Distance(new Vector2(cam.transform.position.x, cam.transform.position.y), new Vector2(posicion.x, posicion.y));
            volumen *= Mathf.Clamp01(1f - d / GestorSonido.DistanciaMaxima);
        }
        Reproducir(nombre, volumen, tono);
    }

    public static bool Existe(string nombre)
    {
        GestorSonido g = GestorSonido.Obtener();
        return g != null && g.Tiene(nombre);
    }
}

// Reproduce los sonidos sueltos con varias fuentes de audio y cambia el ambiente segun el mapa activo.
public class GestorSonido : MonoBehaviour
{
    public const float DistanciaMaxima = 24f;

    private const int Fuentes = 18;
    private const float VolumenAmbiente = 0.25f;
    private const float VolumenMusicaMenu = 0.28f;
    private const string PistaMenuLobby = "BLACK OPS 2 ZOMBIES OFFICIAL Theme Song - spllitz";
    private const string PistaPartidaPrincipal = "Throttle_Locked_Open";
    private static readonly string[] PistasPartida = new string[]
    {
        "Throttle_Locked_Open",          // Phonk
        "Pisteando_hasta_que_aguante",   // Electro cumbia
        "Gilded_Knuckles"                // Instrumental trap
    };
    private const float VolumenMusicaPartidaInicial = 0.04f;
    private const float VolumenMusicaPartidaObjetivo = 0.24f;
    private const float TiempoSubidaMusicaPartida = 8.0f;
    private const float TiempoCruce = 1.2f;

    private static GestorSonido instancia;

    private readonly Dictionary<string, AudioClip> clips = new Dictionary<string, AudioClip>();
    private AudioSource[] pool;
    private int siguiente;

    // ambiente: dos fuentes para pasar de un mapa a otro con fundido cruzado
    private AudioSource ambienteA, ambienteB, sueltos;
    private AudioSource ambienteActual;
    private AudioSource musicaA, musicaB, musicaActual;
    private string pistaActual;
    private bool musicaActiva = true; // la tecla M la apaga y la prende
    private string bucleActual;
    private float proximoSuelto;

    // Cola aleatoria de canciones de partida
    private readonly List<string> colaCancionesPartida = new List<string>();
    private int indiceCancionPartida = 0;
    private bool estabaEnPartida = false;

    private inicio menu;
    private List<GameObject> mapas = new List<GameObject>();
    private float proximaRevision;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Crear()
    {
        Obtener();
    }

    public static GestorSonido Obtener()
    {
        if (instancia != null) return instancia;

        GameObject go = new GameObject("GestorSonido");
        DontDestroyOnLoad(go);
        instancia = go.AddComponent<GestorSonido>();
        return instancia;
    }

    private void Awake()
    {
        foreach (AudioClip c in Resources.LoadAll<AudioClip>("Audio")) clips[c.name] = c;

#if UNITY_EDITOR
        CargarClipEditorSiFalta(PistaMenuLobby, "Assets/Audio/BLACK OPS 2 ZOMBIES OFFICIAL Theme Song - spllitz.mp3");
        CargarClipEditorSiFalta("Throttle_Locked_Open", "Assets/Audio/Throttle_Locked_Open.mp3");
        CargarClipEditorSiFalta("Pisteando_hasta_que_aguante", "Assets/Audio/Pisteando_hasta_que_aguante.mp3");
        CargarClipEditorSiFalta("Gilded_Knuckles", "Assets/Audio/Gilded_Knuckles.mp3");
        CargarClipEditorSiFalta("ak47 shoot sound", "Assets/Audio/ak47 shoot sound.mp3");
        CargarClipEditorSiFalta("barret soot sound", "Assets/Audio/barret soot sound.mp3");
        CargarClipEditorSiFalta("beretta shoot efect", "Assets/Audio/beretta shoot efect.mp3");
        CargarClipEditorSiFalta("mac10 shoot efect", "Assets/Audio/mac10 shoot efect.mp3");
#endif

        // Mapear los sonidos reales de disparo a sus armas correspondientes
        VincularAliasDisparo("disparo_ak47", "ak47 shoot sound");
        VincularAliasDisparo("disparo_barret", "barret soot sound");
        VincularAliasDisparo("disparo_antitanques", "barret soot sound");
        VincularAliasDisparo("disparo_bereta", "beretta shoot efect");
        VincularAliasDisparo("disparo_mac10", "mac10 shoot efect");
        VincularAliasDisparo("disparo_escopeta", "barret soot sound");

        pool = new AudioSource[Fuentes];
        for (int i = 0; i < Fuentes; i++) pool[i] = NuevaFuente(false);

        ambienteA = NuevaFuente(true);
        ambienteB = NuevaFuente(true);
        sueltos = NuevaFuente(false);
        musicaA = NuevaFuente(false);
        musicaB = NuevaFuente(false);
    }

#if UNITY_EDITOR
    private void CargarClipEditorSiFalta(string clave, string rutaAsset)
    {
        if (clips.ContainsKey(clave)) return;
        AudioClip clipEditor = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>(rutaAsset);
        if (clipEditor != null) clips[clave] = clipEditor;
    }
#endif

    private void VincularAliasDisparo(string claveDestino, string claveClipReal)
    {
        if (clips.TryGetValue(claveClipReal, out AudioClip clipReal) && clipReal != null)
        {
            clips[claveDestino] = clipReal;
        }
    }

    private void BarajarColaPartida()
    {
        string ultimaPista = colaCancionesPartida.Count > 0
            ? colaCancionesPartida[Mathf.Clamp(indiceCancionPartida, 0, colaCancionesPartida.Count - 1)]
            : null;

        colaCancionesPartida.Clear();
        for (int i = 0; i < PistasPartida.Length; i++)
        {
            if (clips.ContainsKey(PistasPartida[i]))
                colaCancionesPartida.Add(PistasPartida[i]);
        }

        // Fisher-Yates shuffle para orden aleatorio en cada partida
        for (int i = colaCancionesPartida.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            (colaCancionesPartida[i], colaCancionesPartida[j]) = (colaCancionesPartida[j], colaCancionesPartida[i]);
        }

        // Evitar repetir inmediatamente la última canción si acabamos de re-barajar
        if (colaCancionesPartida.Count > 1 && colaCancionesPartida[0] == ultimaPista)
        {
            int ultimoIdx = colaCancionesPartida.Count - 1;
            (colaCancionesPartida[0], colaCancionesPartida[ultimoIdx]) = (colaCancionesPartida[ultimoIdx], colaCancionesPartida[0]);
        }

        indiceCancionPartida = 0;
    }

    private bool EsPistaPartida(string nombre)
    {
        if (string.IsNullOrEmpty(nombre)) return false;
        for (int i = 0; i < PistasPartida.Length; i++)
        {
            if (PistasPartida[i] == nombre) return true;
        }
        return false;
    }

    private AudioSource NuevaFuente(bool bucle)
    {
        AudioSource a = gameObject.AddComponent<AudioSource>();
        a.playOnAwake = false;
        a.loop = bucle;
        a.spatialBlend = 0f; // el juego es lateral: todo suena "en pantalla", la distancia se calcula a mano
        return a;
    }

    public bool Tiene(string nombre)
    {
        return clips.ContainsKey(nombre);
    }

    // tono con un poco de variacion al azar para que no suene siempre igual
    public void Tocar(string nombre, float volumen, float tono)
    {
        if (volumen <= 0.001f || !clips.TryGetValue(nombre, out AudioClip clip)) return;

        AudioSource a = pool[siguiente];
        siguiente = (siguiente + 1) % pool.Length;
        a.pitch = tono * Random.Range(0.96f, 1.04f);
        a.panStereo = 0f;
        a.volume = Mathf.Clamp01(volumen);
        a.clip = clip;
        a.Play();
    }

    // ---------------------------------------------------------------- ambiente

    private void Update()
    {
        if (Time.unscaledTime >= proximaRevision)
        {
            proximaRevision = Time.unscaledTime + 0.3f;
            CambiarAmbienteSegunMapa();
        }

        if (Keyboard.current != null && Keyboard.current.mKey.wasPressedThisFrame) musicaActiva = !musicaActiva;

        Cruzar(ambienteA, ambienteB, ambienteActual, VolumenAmbiente);
        CruzarMusica();
        SonidosSueltos();
    }

    private string MapaActivo()
    {
        if (menu == null)
        {
            menu = FindFirstObjectByType<inicio>(FindObjectsInactive.Include);
            mapas.Clear();
        }
        if (menu == null) return null;
        if (mapas.Count == 0) mapas = menu.Mapas;

        // solo hay mapa "activo" durante la partida
        if (menu.txt_tiempo == null || !menu.txt_tiempo.gameObject.activeInHierarchy) return null;
        foreach (GameObject m in mapas)
        {
            if (m != null && m.activeSelf) return m.name.ToLowerInvariant();
        }
        return null;
    }

    private static string BucleDe(string mapa)
    {
        if (mapa == null) return "amb_menu";
        if (mapa.Contains("cochera")) return "amb_cochera";
        if (mapa.Contains("cocina") || mapa.Contains("cosina")) return "amb_cocina";
        if (mapa.Contains("patio")) return "amb_patio";
        if (mapa.Contains("sala")) return "amb_sala";
        return "amb_menu";
    }

    // En menú/carga suena la canción de Black Ops 2 Zombies; en partida suena la cola aleatoria (Phonk, Electro cumbia, Trap)
    private string PistaDe(string mapa)
    {
        if (mapa == null)
        {
            estabaEnPartida = false;
            return clips.ContainsKey(PistaMenuLobby) ? PistaMenuLobby : "musica_menu";
        }

        if (!estabaEnPartida || colaCancionesPartida.Count == 0)
        {
            estabaEnPartida = true;
            BarajarColaPartida();
        }
        else if (musicaActual != null && EsPistaPartida(pistaActual))
        {
            // Si la canción actual de la partida terminó o está a punto de terminar, pasar a la siguiente de la cola
            bool termino = !musicaActual.isPlaying
                || (musicaActual.clip != null && musicaActual.time >= musicaActual.clip.length - 0.6f);

            if (termino && colaCancionesPartida.Count > 0)
            {
                indiceCancionPartida++;
                if (indiceCancionPartida >= colaCancionesPartida.Count)
                {
                    BarajarColaPartida();
                }
            }
        }

        if (colaCancionesPartida.Count > 0)
        {
            int idx = Mathf.Clamp(indiceCancionPartida, 0, colaCancionesPartida.Count - 1);
            return colaCancionesPartida[idx];
        }

        if (clips.ContainsKey(PistaPartidaPrincipal)) return PistaPartidaPrincipal;

        if (mapa.Contains("cochera")) return "musica_juego1";
        if (mapa.Contains("cocina") || mapa.Contains("cosina")) return "musica_juego2";
        if (mapa.Contains("patio")) return "musica_juego3";
        if (mapa.Contains("sala")) return "musica_juego4";
        return "musica_juego" + Random.Range(1, 5);
    }

    // la fuente libre toma el nuevo clip y sube mientras la otra baja
    private AudioSource EmpezarEn(AudioSource a, AudioSource b, AudioSource actual, string nombre)
    {
        if (!clips.TryGetValue(nombre, out AudioClip clip)) return actual;
        AudioSource nueva = actual == a ? b : a;
        nueva.clip = clip;
        // Las pistas de la cola de partida no hacen loop individual para encadenar hacia la siguiente pista aleatoria
        nueva.loop = !EsPistaPartida(nombre);
        nueva.time = 0f;
        nueva.volume = (EsPistaPartida(nombre) && musicaActiva) ? VolumenMusicaPartidaInicial : 0f;
        nueva.Play();
        return nueva;
    }

    private void CruzarMusica()
    {
        float pasoSalida = Time.unscaledDeltaTime / TiempoCruce * 0.35f;

        if (!musicaActiva || musicaActual == null)
        {
            Mover(musicaA, null, 0f, pasoSalida);
            Mover(musicaB, null, 0f, pasoSalida);
            return;
        }

        bool esCancionPartida = EsPistaPartida(pistaActual);
        float objetivo = esCancionPartida ? VolumenMusicaPartidaObjetivo : VolumenMusicaMenu;
        float pasoSubida = esCancionPartida
            ? ((VolumenMusicaPartidaObjetivo - VolumenMusicaPartidaInicial) / TiempoSubidaMusicaPartida) * Time.unscaledDeltaTime
            : (VolumenMusicaMenu / TiempoCruce) * Time.unscaledDeltaTime;

        AudioSource anterior = musicaActual == musicaA ? musicaB : musicaA;
        Mover(anterior, musicaActual, 0f, pasoSalida);
        Mover(musicaActual, musicaActual, objetivo, pasoSubida);
    }

    private void CambiarAmbienteSegunMapa()
    {
        string mapa = MapaActivo();

        string pista = PistaDe(mapa);
        if (pista != pistaActual)
        {
            pistaActual = pista;
            musicaActual = EmpezarEn(musicaA, musicaB, musicaActual, pista);
        }

        string bucle = BucleDe(mapa);
        if (bucle == bucleActual) return;
        bucleActual = bucle;
        // el menu ya tiene musica, su fondo suave solo estorbaria: se deja sin ambiente
        if (bucle == "amb_menu") { ambienteActual = null; }
        else ambienteActual = EmpezarEn(ambienteA, ambienteB, ambienteActual, bucle);
        proximoSuelto = Time.unscaledTime + Random.Range(2f, 5f);
    }

    // la fuente activa sube hasta `objetivo` y la anterior baja hasta apagarse
    private void Cruzar(AudioSource a, AudioSource b, AudioSource actual, float objetivo)
    {
        float paso = Time.unscaledDeltaTime / TiempoCruce * Mathf.Max(objetivo, 0.3f);
        Mover(a, actual, objetivo, paso);
        Mover(b, actual, objetivo, paso);
    }

    private static void Mover(AudioSource fuente, AudioSource actual, float objetivo, float paso)
    {
        if (fuente == null || !fuente.isPlaying) return;
        if (fuente == actual) fuente.volume = Mathf.MoveTowards(fuente.volume, objetivo, paso);
        else
        {
            fuente.volume = Mathf.MoveTowards(fuente.volume, 0f, paso);
            if (fuente.volume <= 0.001f) fuente.Stop();
        }
    }

    // sonidos que aparecen de vez en cuando segun el lugar: pajaros, gotas, reloj...
    private void SonidosSueltos()
    {
        if (Time.unscaledTime < proximoSuelto) return;
        proximoSuelto = Time.unscaledTime + Random.Range(4f, 9f);

        string[] opciones;
        switch (bucleActual)
        {
            case "amb_cochera": opciones = new[] { "gota", "clank", "gota" }; break;
            case "amb_cocina": opciones = new[] { "nevera", "reloj", "gota" }; break;
            case "amb_patio": opciones = new[] { "pajaro1", "pajaro2", "pajaro1" }; break;
            case "amb_sala": opciones = new[] { "reloj", "crujido" }; break;
            default: return; // el menu solo tiene su ambiente de fondo
        }

        if (!clips.TryGetValue(opciones[Random.Range(0, opciones.Length)], out AudioClip clip)) return;
        sueltos.pitch = Random.Range(0.9f, 1.1f);
        sueltos.panStereo = Random.Range(-0.6f, 0.6f);
        sueltos.PlayOneShot(clip, Random.Range(0.35f, 0.6f));
    }
}
