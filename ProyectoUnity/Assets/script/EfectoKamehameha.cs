using UnityEngine;

// Efecto visual tipo "Kamehameha" para la raygun:
// carga = esfera de energía que crece y pulsa con partículas que se absorben;
// disparo = haz azul con núcleo blanco que pulsa, esfera en la boca y destello en el impacto.
// Es solo visual (el daño sigue en Arma.cs).
public class EfectoKamehameha : MonoBehaviour
{
    private static Sprite spriteBrillo;

    private SpriteRenderer esferaExterior, esferaNucleo, impacto;
    private LineRenderer nucleoHaz;
    private Transform[] chispas;
    private float[] faseChispa;
    private float tiempoCarga;
    private float tiempoDisparo;

    private static readonly Color azul = new Color(1f, 0.12f, 0.08f, 0.6f);        // rojo (antes azul/morado)
    private static readonly Color azulClaro = new Color(1f, 0.55f, 0.45f, 0.9f);
    private bool materialListo;

    private static Sprite ObtenerSprite()
    {
        if (spriteBrillo != null) return spriteBrillo;
        const int n = 64;
        Texture2D tex = new Texture2D(n, n, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float d = Vector2.Distance(new Vector2(x, y), new Vector2(n / 2f, n / 2f)) / (n / 2f);
                float a = Mathf.Clamp01(1f - d);
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, a * a));
            }
        tex.Apply();
        spriteBrillo = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), n);
        return spriteBrillo;
    }

    private SpriteRenderer NuevoSprite(string nombre, int orden)
    {
        GameObject go = new GameObject(nombre);
        go.transform.SetParent(transform, false);
        SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = ObtenerSprite();
        sr.sortingOrder = orden;
        sr.enabled = false;
        return sr;
    }

    private void Awake()
    {
        esferaExterior = NuevoSprite("KameEsferaExt", 50);
        esferaNucleo = NuevoSprite("KameEsferaNucleo", 51);
        impacto = NuevoSprite("KameImpacto", 50);

        GameObject nh = new GameObject("KameNucleoHaz");
        nh.transform.SetParent(transform, false);
        nucleoHaz = nh.AddComponent<LineRenderer>();
        nucleoHaz.useWorldSpace = true;
        nucleoHaz.material = new Material(Shader.Find("Sprites/Default"));
        nucleoHaz.sortingOrder = 49;
        nucleoHaz.numCapVertices = 8;
        nucleoHaz.enabled = false;

        chispas = new Transform[10];
        faseChispa = new float[chispas.Length];
        for (int i = 0; i < chispas.Length; i++)
        {
            SpriteRenderer s = NuevoSprite("KameChispa" + i, 52);
            s.color = azulClaro;
            chispas[i] = s.transform;
            faseChispa[i] = Random.value;
        }
    }

    // Se llama cada frame desde Arma (LateUpdate).
    // trazo: LineRenderer del rayo ya posicionado por Arma (local o remoto).
    public void Actualizar(bool cargando, bool disparando, Vector3 boca, LineRenderer trazo, float anchoBase)
    {
        if (disparando && trazo != null && trazo.enabled && trazo.positionCount >= 2)
        {
            tiempoCarga = 0f;
            tiempoDisparo += Time.deltaTime;
            Vector3 ini = trazo.GetPosition(0), fin = trazo.GetPosition(1);
            DibujarHaz(trazo, ini, fin, anchoBase);
        }
        else
        {
            tiempoDisparo = 0f;
            nucleoHaz.enabled = false;
            impacto.enabled = false;
            if (cargando)
            {
                tiempoCarga += Time.deltaTime;
                DibujarCarga(boca);
            }
            else
            {
                tiempoCarga = 0f;
                esferaExterior.enabled = false;
                esferaNucleo.enabled = false;
                foreach (Transform c in chispas) c.gameObject.SetActive(false);
            }
        }
    }

    private void DibujarCarga(Vector3 boca)
    {
        float t = Mathf.Clamp01(tiempoCarga / 3f);          // 3 s = tiempoCargaRequerido
        float pulso = 1f + 0.12f * Mathf.Sin(tiempoCarga * (10f + 20f * t));
        float radio = Mathf.Lerp(0.12f, 0.9f, t * t) * pulso;

        esferaExterior.enabled = true;
        esferaExterior.transform.position = boca;
        float parpadeo = 0.75f + 0.25f * Mathf.Sin(tiempoCarga * 45f) * Mathf.Sin(tiempoCarga * 17f);
        esferaExterior.transform.localScale = Vector3.one * radio * 1.9f * parpadeo;
        esferaExterior.color = new Color(azul.r, azul.g, azul.b, Mathf.Lerp(0.3f, 0.7f, t) * parpadeo);

        esferaNucleo.enabled = true;
        esferaNucleo.transform.position = boca;
        esferaNucleo.transform.localScale = Vector3.one * radio * 1.1f;
        esferaNucleo.color = Color.white;

        // Chispas que son absorbidas hacia la esfera
        for (int i = 0; i < chispas.Length; i++)
        {
            faseChispa[i] = (faseChispa[i] + Time.deltaTime * (0.8f + t)) % 1f;
            float ang = (i / (float)chispas.Length) * Mathf.PI * 2f + i;
            float dist = Mathf.Lerp(2.2f, 0.1f, faseChispa[i]);
            chispas[i].gameObject.SetActive(true);
            chispas[i].position = boca + new Vector3(Mathf.Cos(ang), Mathf.Sin(ang), 0f) * dist;
            chispas[i].localScale = Vector3.one * Mathf.Lerp(0.25f, 0.05f, faseChispa[i]);
        }
    }

    private void DibujarHaz(LineRenderer trazo, Vector3 ini, Vector3 fin, float anchoBase)
    {
        foreach (Transform c in chispas) c.gameObject.SetActive(false);

        // Pulso rápido del grosor + ensanchamiento inicial al disparar
        float arranque = Mathf.Lerp(1.6f, 1f, Mathf.Clamp01(tiempoDisparo / 0.25f));
        float pulso = (1f + 0.18f * Mathf.Sin(tiempoDisparo * 40f) + 0.08f * Mathf.Sin(tiempoDisparo * 97f)) * arranque;
        float ancho = anchoBase * pulso;

        // Haz exterior (azul, con degradado más tenue en la punta)
        if (!materialListo)
        {
            trazo.material = new Material(Shader.Find("Sprites/Default"));   // evita el tinte morado del material original
            materialListo = true;
        }
        trazo.widthCurve = AnimationCurve.Constant(0f, 1f, 1f);
        trazo.widthMultiplier = ancho * 0.8f;
        Gradient g = new Gradient();
        g.SetKeys(
            new[] { new GradientColorKey(new Color(1f, 0.2f, 0.1f), 0f), new GradientColorKey(new Color(1f, 0.1f, 0.05f), 1f) },
            new[] { new GradientAlphaKey(0.6f, 0f), new GradientAlphaKey(0.45f, 1f) });
        trazo.colorGradient = g;

        // Núcleo blanco más delgado
        nucleoHaz.enabled = true;
        nucleoHaz.positionCount = 2;
        nucleoHaz.SetPosition(0, ini);
        nucleoHaz.SetPosition(1, fin);
        nucleoHaz.widthMultiplier = ancho * 0.45f;
        nucleoHaz.startColor = Color.white;
        nucleoHaz.endColor = new Color(1f, 0.9f, 0.9f, 0.9f);

        // Esfera de energía en la boca del arma
        esferaExterior.enabled = true;
        esferaExterior.transform.position = ini;
        float destello = 0.7f + 0.3f * Mathf.Sin(tiempoDisparo * 60f);
        esferaExterior.transform.localScale = Vector3.one * ancho * 2.2f * destello;
        esferaExterior.color = new Color(azul.r, azul.g, azul.b, 0.55f * destello);
        esferaNucleo.enabled = true;
        esferaNucleo.transform.position = ini;
        esferaNucleo.transform.localScale = Vector3.one * ancho * 1.3f;
        esferaNucleo.color = Color.white;

        // Destello en el punto de impacto/final
        impacto.enabled = true;
        impacto.transform.position = fin;
        impacto.transform.localScale = Vector3.one * ancho * (2.2f + 0.5f * Mathf.Sin(tiempoDisparo * 55f));
        impacto.color = new Color(1f, 0.25f, 0.15f, 0.7f * destello);
    }

    private void OnDisable()
    {
        if (esferaExterior != null) esferaExterior.enabled = false;
        if (esferaNucleo != null) esferaNucleo.enabled = false;
        if (impacto != null) impacto.enabled = false;
        if (nucleoHaz != null) nucleoHaz.enabled = false;
    }
}
