using System.Collections.Generic;
using TMPro;
using UnityEngine;

// Efectos de disparo estilo caricatura: destello en forma de estrella, chispas, humo, casquillos,
// palabra tipo comic ("¡PAF!") y sacudida de camara. Lo llama AnimadorArma cuando sale un disparo,
// asi funciona igual para el jugador local y para los demas. No cambia nada de Arma.
[DefaultExecutionOrder(-1000)] // quita la sacudida del cuadro anterior antes de que CamaraSeguir mueva la camara
public class EfectosDisparo : MonoBehaviour
{
    // como se ve y se siente cada arma (por nombre del objeto)
    public struct Perfil
    {
        public Color destello, chispa;
        public float tamano;     // tamano del destello (metros)
        public string texto;     // palabra del comic
        public float sacudida;   // fuerza de la camara
        public float retroceso;  // cuanto se echa atras el arma (metros)
        public bool casquillo;
    }

    public static Perfil PerfilDe(string nombre)
    {
        string n = (nombre ?? "").ToLowerInvariant();
        Perfil p = new Perfil
        {
            destello = new Color(1f, 0.85f, 0.25f), chispa = new Color(1f, 0.7f, 0.15f),
            tamano = 0.34f, texto = "¡BAP!", sacudida = 0.03f, retroceso = 0.07f, casquillo = true
        };
        if (n.Contains("antitanq"))
        {
            p.tamano = 0.72f; p.texto = "¡KABOOM!"; p.sacudida = 0.18f; p.retroceso = 0.22f;
            p.destello = new Color(1f, 0.5f, 0.1f); p.chispa = new Color(1f, 0.35f, 0.05f);
        }
        else if (n.Contains("barret"))
        {
            p.tamano = 0.6f; p.texto = "¡BOOM!"; p.sacudida = 0.13f; p.retroceso = 0.16f;
            p.destello = new Color(1f, 0.6f, 0.15f); p.chispa = new Color(1f, 0.45f, 0.1f);
        }
        else if (n.Contains("escopeta"))
        {
            p.tamano = 0.52f; p.texto = "¡BLAM!"; p.sacudida = 0.11f; p.retroceso = 0.14f;
            p.destello = new Color(1f, 0.72f, 0.2f); p.chispa = new Color(1f, 0.55f, 0.15f);
        }
        else if (n.Contains("machete"))
        {
            p.tamano = 0.38f; p.texto = "¡SLASH!"; p.sacudida = 0.04f; p.retroceso = 0.05f; p.casquillo = false;
            p.destello = new Color(0.85f, 0.95f, 1f); p.chispa = new Color(0.7f, 0.9f, 1f);
        }
        else if (n.Contains("escudo"))
        {
            p.tamano = 0.4f; p.texto = "¡CLANG!"; p.sacudida = 0.08f; p.retroceso = 0.06f; p.casquillo = false;
            p.destello = new Color(0.95f, 0.85f, 0.45f); p.chispa = new Color(1f, 0.9f, 0.5f);
        }
        else if (n.Contains("mac"))
        {
            p.tamano = 0.26f; p.texto = "¡TAT!"; p.sacudida = 0.02f; p.retroceso = 0.04f;
        }
        else if (n.Contains("ber"))
        {
            p.tamano = 0.34f; p.texto = "¡PAF!"; p.sacudida = 0.045f; p.retroceso = 0.1f;
        }
        else if (n.Contains("ray"))
        {
            p.tamano = 0.42f; p.texto = "¡ZAP!"; p.sacudida = 0.05f; p.retroceso = 0.06f; p.casquillo = false;
            p.destello = new Color(0.3f, 1f, 0.85f); p.chispa = new Color(0.4f, 1f, 0.6f);
        }
        return p;
    }

    private static EfectosDisparo instancia;
    private static Vector3 sacudidaAplicada;
    private static float sacudida;

    private class Destello { public Transform raiz; public SpriteRenderer anillo, nucleo; public float inicio, duracion, tamano; public bool activo; }
    private class Palabra { public TextMeshPro texto; public float inicio, duracion, giro; public Vector3 origen; public bool activo; }

    private readonly List<Destello> destellos = new List<Destello>();
    private readonly List<Palabra> palabras = new List<Palabra>();
    private ParticleSystem chispas, humo, casquillos;
    private Sprite estrella, circulo;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Crear()
    {
        Obtener();
    }

    private static EfectosDisparo Obtener()
    {
        if (instancia != null) return instancia;
        GameObject go = new GameObject("EfectosDisparo");
        DontDestroyOnLoad(go);
        instancia = go.AddComponent<EfectosDisparo>();
        go.AddComponent<AplicarSacudida>();
        return instancia;
    }

    // ---------------------------------------------------------------- llamadas publicas

    // boca y destino son los dos puntos del trazo del disparo; local indica si el tirador es el jugador de esta PC
    public static void Disparo(Arma arma, Vector3 boca, Vector3 destino, bool local)
    {
        Obtener().Reproducir(arma, boca, destino, local);
    }

    public static void Sacudir(float fuerza)
    {
        sacudida = Mathf.Max(sacudida, fuerza);
    }

    // ---------------------------------------------------------------- creacion

    private void Awake()
    {
        circulo = CrearCirculo();
        estrella = CrearEstrella();
        chispas = CrearSistema("Chispas", 0.8f);
        humo = CrearSistema("Humo", -0.05f);
        casquillos = CrearSistema("Casquillos", 3f);
    }

    private ParticleSystem CrearSistema(string nombre, float gravedad)
    {
        GameObject go = new GameObject(nombre);
        go.transform.SetParent(transform, false);
        ParticleSystem ps = go.AddComponent<ParticleSystem>();
        ps.Stop();

        ParticleSystem.MainModule main = ps.main;
        main.loop = true;
        main.playOnAwake = false;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.gravityModifier = gravedad;
        main.maxParticles = 400;

        ParticleSystem.EmissionModule emision = ps.emission;
        emision.rateOverTime = 0f; // solo se emite a mano con Emit()

        ParticleSystem.ShapeModule forma = ps.shape;
        forma.enabled = false;

        ParticleSystem.ColorOverLifetimeModule color = ps.colorOverLifetime;
        color.enabled = true;
        Gradient g = new Gradient();
        g.SetKeys(
            new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 0.6f), new GradientAlphaKey(0f, 1f) });
        color.color = g;

        ParticleSystem.SizeOverLifetimeModule tamano = ps.sizeOverLifetime;
        tamano.enabled = true;
        // el humo crece, lo demas se encoge
        tamano.size = new ParticleSystem.MinMaxCurve(1f, nombre == "Humo" ? AnimationCurve.Linear(0f, 0.5f, 1f, 1.6f) : AnimationCurve.Linear(0f, 1f, 1f, 0.2f));

        ParticleSystemRenderer render = go.GetComponent<ParticleSystemRenderer>();
        Shader shader = Shader.Find("Sprites/Default");
        if (shader != null)
        {
            Material m = new Material(shader);
            m.mainTexture = circulo.texture;
            render.material = m;
        }

        ps.Play();
        return ps;
    }

    private static Sprite CrearCirculo()
    {
        const int tam = 32;
        Texture2D tex = new Texture2D(tam, tam, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;
        for (int y = 0; y < tam; y++)
        {
            for (int x = 0; x < tam; x++)
            {
                float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(tam / 2f, tam / 2f)) / (tam / 2f);
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, Mathf.Clamp01((1f - d) * 3f)));
            }
        }
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, tam, tam), new Vector2(0.5f, 0.5f), tam);
    }

    // estrella de 4 puntas largas y 4 cortas en diagonal
    private static Sprite CrearEstrella()
    {
        const int tam = 96;
        Texture2D tex = new Texture2D(tam, tam, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;
        for (int y = 0; y < tam; y++)
        {
            for (int x = 0; x < tam; x++)
            {
                Vector2 p = new Vector2(x + 0.5f - tam / 2f, y + 0.5f - tam / 2f) / (tam / 2f);
                float r = p.magnitude;
                float a = Mathf.Atan2(p.y, p.x);
                float largas = Mathf.Pow(Mathf.Abs(Mathf.Cos(2f * a)), 6f);
                float cortas = 0.45f * Mathf.Pow(Mathf.Abs(Mathf.Sin(2f * a)), 6f);
                float limite = 0.2f + 0.8f * Mathf.Max(largas, cortas);
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, Mathf.Clamp01((limite - r) * 10f)));
            }
        }
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, tam, tam), new Vector2(0.5f, 0.5f), tam);
    }

    // ---------------------------------------------------------------- disparo

    private void Reproducir(Arma arma, Vector3 boca, Vector3 destino, bool local)
    {
        Perfil perfil = PerfilDe(arma != null ? arma.name : "");
        Vector3 dir = destino - boca;
        dir.z = 0f;
        dir = dir.sqrMagnitude > 0.0001f ? dir.normalized : Vector3.right;

        Sonido(arma, boca, perfil);
        NuevoDestello(boca, perfil.destello, perfil.tamano * 1.4f, 0.09f);

        // chispas hacia adelante
        int cantidad = perfil.tamano > 0.5f ? 14 : 8;
        for (int i = 0; i < cantidad; i++)
        {
            Vector3 v = dir * Random.Range(2f, 5f) + new Vector3(0f, Random.Range(-1.4f, 1.4f), Random.Range(-0.3f, 0.3f));
            Emitir(chispas, boca, v, perfil.chispa, Random.Range(0.04f, 0.08f), Random.Range(0.18f, 0.35f));
        }

        // nubecitas de humo
        for (int i = 0; i < 3; i++)
        {
            Vector3 v = dir * Random.Range(0.2f, 0.8f) + Vector3.up * Random.Range(0.3f, 0.8f);
            Emitir(humo, boca, v, new Color(0.9f, 0.9f, 0.92f, 0.55f), Random.Range(0.12f, 0.2f), Random.Range(0.45f, 0.7f));
        }

        // casquillo que sale hacia atras y arriba
        if (perfil.casquillo && arma != null)
        {
            Vector3 v = -dir * Random.Range(0.4f, 1.0f) + Vector3.up * Random.Range(1.2f, 2.0f);
            Emitir(casquillos, arma.transform.position + Vector3.up * 0.03f, v, new Color(1f, 0.8f, 0.25f), 0.04f, 0.7f);
        }

        PalabraComic(boca + Vector3.up * 0.25f, perfil.texto, perfil.destello);

        // el impacto en el otro extremo del trazo
        NuevoDestello(destino, perfil.destello, perfil.tamano * 0.6f, 0.12f);
        for (int i = 0; i < 6; i++)
        {
            Vector3 v = new Vector3(Random.Range(-2f, 2f), Random.Range(0.5f, 3f), Random.Range(-0.4f, 0.4f)) - dir * Random.Range(0.5f, 1.5f);
            Emitir(chispas, destino, v, perfil.chispa, Random.Range(0.04f, 0.07f), Random.Range(0.2f, 0.4f));
        }
        Sonidos.Reproducir("impacto", destino, 0.4f);

        if (local) Sacudir(perfil.sacudida);
    }

    private static void Sonido(Arma arma, Vector3 posicion, Perfil perfil)
    {
        // La Raygun reproduce su propio audio sincronizado de carga + disparo continuo
        if (arma != null && arma.Modo == ModoDisparo.RayoContinuo && arma.TieneAudioPersonalizadoRayo)
            return;

        string n = arma != null ? arma.name.ToLowerInvariant() : "ak47";
        float tono = 1f;
        string clave = "disparo_" + n;

        if (n.Contains("ak47") || n == "ak")
        {
            clave = "disparo_ak47";
        }
        else if (n.Contains("antitanq"))
        {
            clave = "disparo_antitanques";
            tono = 0.85f;
        }
        else if (n.Contains("barret"))
        {
            clave = "disparo_barret";
        }
        else if (n.Contains("escopeta"))
        {
            clave = "disparo_escopeta";
            tono = 0.80f;
            Sonidos.Reproducir("disparo_bereta", posicion, 0.65f, 0.72f);
        }
        else if (n.Contains("mac"))
        {
            clave = "disparo_mac10";
        }
        else if (n.Contains("ber"))
        {
            clave = "disparo_bereta";
        }
        else if (n.Contains("machete"))
        {
            Sonidos.Reproducir("deslizar", posicion, 0.9f, 1.35f);
            return;
        }
        else if (n.Contains("escudo"))
        {
            Sonidos.Reproducir("clank", posicion, 0.95f, 0.85f);
            return;
        }

        if (!Sonidos.Existe(clave)) clave = "disparo_ak47";
        Sonidos.Reproducir(clave, posicion, 0.92f, tono);
    }

    private static void Emitir(ParticleSystem ps, Vector3 pos, Vector3 vel, Color color, float tamano, float vida)
    {
        ParticleSystem.EmitParams e = new ParticleSystem.EmitParams();
        e.position = pos;
        e.velocity = vel;
        e.startColor = color;
        e.startSize = tamano;
        e.startLifetime = vida;
        ps.Emit(e, 1);
    }

    // ---------------------------------------------------------------- destellos y palabras

    private void NuevoDestello(Vector3 pos, Color color, float tamano, float duracion)
    {
        Destello d = null;
        foreach (Destello x in destellos) if (!x.activo) { d = x; break; }
        if (d == null)
        {
            d = new Destello();
            GameObject go = new GameObject("Destello");
            go.transform.SetParent(transform, false);
            d.raiz = go.transform;
            d.anillo = go.AddComponent<SpriteRenderer>();
            d.anillo.sprite = estrella;
            d.anillo.sortingOrder = 100;

            GameObject nucleo = new GameObject("Nucleo");
            nucleo.transform.SetParent(go.transform, false);
            nucleo.transform.localScale = Vector3.one * 0.45f;
            d.nucleo = nucleo.AddComponent<SpriteRenderer>();
            d.nucleo.sprite = estrella;
            d.nucleo.color = Color.white;
            d.nucleo.sortingOrder = 101;
            destellos.Add(d);
        }

        d.activo = true;
        d.inicio = Time.time;
        d.duracion = duracion;
        d.tamano = tamano;
        d.anillo.color = color;
        d.raiz.position = pos;
        d.raiz.rotation = Quaternion.identity;
        d.raiz.gameObject.SetActive(true);
    }

    private void PalabraComic(Vector3 pos, string texto, Color color)
    {
        Palabra p = null;
        foreach (Palabra x in palabras) if (!x.activo) { p = x; break; }
        if (p == null)
        {
            p = new Palabra();
            GameObject go = new GameObject("PalabraComic");
            go.transform.SetParent(transform, false);
            p.texto = go.AddComponent<TextMeshPro>();
            p.texto.alignment = TextAlignmentOptions.Center;
            p.texto.fontSize = 2.4f;
            p.texto.fontStyle = FontStyles.Bold | FontStyles.Italic;
            p.texto.textWrappingMode = TextWrappingModes.NoWrap;
            p.texto.outlineWidth = 0.3f;
            p.texto.outlineColor = new Color32(40, 18, 6, 255);
            p.texto.sortingOrder = 120;
            palabras.Add(p);
        }

        p.activo = true;
        p.inicio = Time.time;
        p.duracion = 0.55f;
        p.giro = Random.Range(-14f, 14f);
        p.origen = pos;
        p.texto.text = texto;
        p.texto.color = Color.Lerp(color, Color.white, 0.35f);
        p.texto.gameObject.SetActive(true);
    }

    private void Update()
    {
        Camera cam = Camera.main;
        Quaternion haciaCamara = cam != null ? cam.transform.rotation : Quaternion.identity;

        foreach (Destello d in destellos)
        {
            if (!d.activo) continue;
            float t = (Time.time - d.inicio) / d.duracion;
            if (t >= 1f) { d.activo = false; d.raiz.gameObject.SetActive(false); continue; }
            // aparece de golpe, crece un poco y se apaga
            float escala = d.tamano * (t < 0.3f ? Mathf.Lerp(0.5f, 1.25f, t / 0.3f) : Mathf.Lerp(1.25f, 0f, (t - 0.3f) / 0.7f));
            d.raiz.localScale = Vector3.one * escala;
            d.raiz.rotation = haciaCamara * Quaternion.Euler(0f, 0f, t * 50f);
        }

        foreach (Palabra p in palabras)
        {
            if (!p.activo) continue;
            float t = (Time.time - p.inicio) / p.duracion;
            if (t >= 1f) { p.activo = false; p.texto.gameObject.SetActive(false); continue; }
            // rebote de entrada, sube un poco y se desvanece
            float escala = t < 0.2f ? Mathf.LerpUnclamped(0.2f, 1f, EaseOutBack(t / 0.2f)) : 1f;
            Transform tr = p.texto.transform;
            tr.position = p.origen + Vector3.up * (0.35f * t);
            tr.localScale = Vector3.one * escala * 0.95f;
            tr.rotation = haciaCamara * Quaternion.Euler(0f, 0f, p.giro);
            Color c = p.texto.color;
            c.a = t < 0.65f ? 1f : 1f - (t - 0.65f) / 0.35f;
            p.texto.color = c;
        }
    }

    private static float EaseOutBack(float t)
    {
        const float c1 = 1.70158f;
        const float c3 = c1 + 1f;
        return 1f + c3 * Mathf.Pow(t - 1f, 3f) + c1 * Mathf.Pow(t - 1f, 2f);
    }

    // ---------------------------------------------------------------- sacudida de camara

    private void LateUpdate()
    {
        // se deshace lo del cuadro anterior para que la camara no se desvie de donde la deja CamaraSeguir
        Camera cam = Camera.main;
        if (cam != null) cam.transform.position -= sacudidaAplicada;
        sacudidaAplicada = Vector3.zero;
    }

    // corre despues de CamaraSeguir y mueve la camara un instante
    [DefaultExecutionOrder(1000)]
    private class AplicarSacudida : MonoBehaviour
    {
        private void LateUpdate()
        {
            sacudida = Mathf.MoveTowards(sacudida, 0f, sacudida * 14f * Time.deltaTime + 0.0005f);
            Camera cam = Camera.main;
            if (cam == null || sacudida <= 0.0005f) return;

            sacudidaAplicada = new Vector3(Random.Range(-1f, 1f), Random.Range(-1f, 1f), 0f) * sacudida;
            cam.transform.position += sacudidaAplicada;
        }
    }
}
