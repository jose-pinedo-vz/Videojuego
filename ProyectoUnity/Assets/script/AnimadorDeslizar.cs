using UnityEngine;

// Animacion de deslizamiento del gato: se agacha, el torso se inclina hacia adelante y las piernas se estiran hacia atras.
// Al terminar el deslizamiento el cuerpo vuelve poco a poco a la postura normal. Tambien levanta polvo en los pies
// para notar el cambio de velocidad mientras corres.
// Se mueve el esqueleto encima de la animacion (despues del Animator) y se lee el parametro "estaDeslizando", asi
// funciona igual en el jugador local y en los demas jugadores. No cambia nada de Personaje.
[DefaultExecutionOrder(-60)] // despues del Animator pero antes de AnimadorArma, asi el arma sigue a la mano agachada
public class AnimadorDeslizar : MonoBehaviour
{
    [Header("Postura (grados / metros)")]
    [SerializeField] private float bajarCaderas = 0.2f;
    [SerializeField] private float inclinarColumna = 22f;   // torso hacia adelante
    [SerializeField] private float inclinarPecho = 14f;
    [SerializeField] private float levantarCabeza = -26f;   // la cabeza mira al frente aunque el torso se incline
    [SerializeField] private float piernasAtras = 58f;      // muslos hacia atras
    [SerializeField] private float rodillasAtras = 10f;     // las pantorrillas se doblan un poco mas

    [Header("Transicion")]
    [SerializeField] private float velocidadEntrar = 12f;
    [SerializeField] private float velocidadSalir = 5f;     // mas lento: el gato se levanta suave

    private static readonly int ParamDeslizando = Animator.StringToHash("estaDeslizando");

    private Animator animator;
    private bool tieneParametro;
    private Transform caderas, columna, pecho, cabeza, musloIzq, musloDer, rodillaIzq, rodillaDer;
    private float peso; // 0 normal, 1 deslizando por completo
    private ParticleSystem polvo;

    private void Awake()
    {
        RefrescarHuesos();
        CrearPolvo();
    }

    public void RefrescarHuesos()
    {
        animator = GetComponent<Animator>();
        if (animator == null) animator = GetComponentInChildren<Animator>();
        if (animator == null) { enabled = false; return; }

        tieneParametro = false;
        if (animator.runtimeAnimatorController != null)
        {
            foreach (AnimatorControllerParameter p in animator.parameters)
            {
                if (p.nameHash == ParamDeslizando) tieneParametro = true;
            }
        }

        if (animator.isHuman)
        {
            caderas = animator.GetBoneTransform(HumanBodyBones.Hips);
            columna = animator.GetBoneTransform(HumanBodyBones.Spine);
            pecho = animator.GetBoneTransform(HumanBodyBones.Chest);
            cabeza = animator.GetBoneTransform(HumanBodyBones.Head);
            musloIzq = animator.GetBoneTransform(HumanBodyBones.LeftUpperLeg);
            musloDer = animator.GetBoneTransform(HumanBodyBones.RightUpperLeg);
            rodillaIzq = animator.GetBoneTransform(HumanBodyBones.LeftLowerLeg);
            rodillaDer = animator.GetBoneTransform(HumanBodyBones.RightLowerLeg);
            enabled = caderas != null;
        }
        else
        {
            caderas = BuscarHueso("Hips");
            columna = BuscarHueso("Spine");
            pecho = BuscarHueso("Spine1");
            cabeza = BuscarHueso("Head");
            musloIzq = BuscarHueso("LeftUpLeg");
            musloDer = BuscarHueso("RightUpLeg");
            rodillaIzq = BuscarHueso("LeftLeg");
            rodillaDer = BuscarHueso("RightLeg");
            enabled = caderas != null;
        }
    }

    private Transform BuscarHueso(string sufijo)
    {
        foreach (Transform t in GetComponentsInChildren<Transform>(true))
        {
            if (t.name.EndsWith(sufijo, System.StringComparison.OrdinalIgnoreCase))
                return t;
        }
        return null;
    }

    private void LateUpdate()
    {
        if (!tieneParametro || caderas == null) return;

        bool deslizando = animator.GetBool(ParamDeslizando);
        peso = Mathf.MoveTowards(peso, deslizando ? 1f : 0f, (deslizando ? velocidadEntrar : velocidadSalir) * Time.deltaTime);

        if (polvo != null)
        {
            ParticleSystem.EmissionModule emision = polvo.emission;
            emision.rateOverTime = deslizando ? 45f : 0f;
        }

        if (peso <= 0.001f) return;
        float s = Mathf.SmoothStep(0f, 1f, peso);

        // el eje lateral gira con el personaje, asi "adelante" es siempre hacia donde mira
        Vector3 eje = transform.right;

        caderas.position -= Vector3.up * bajarCaderas * s;
        Girar(columna, inclinarColumna * s, eje);
        Girar(pecho, inclinarPecho * s, eje);
        Girar(cabeza, levantarCabeza * s, eje);
        Girar(musloIzq, piernasAtras * s, eje);
        Girar(musloDer, piernasAtras * s, eje);
        Girar(rodillaIzq, rodillasAtras * s, eje);
        Girar(rodillaDer, rodillasAtras * s, eje);
    }

    private static void Girar(Transform hueso, float grados, Vector3 eje)
    {
        if (hueso != null) hueso.rotation = Quaternion.AngleAxis(grados, eje) * hueso.rotation;
    }

    // nube de polvo detras de los pies, solo se emite mientras se desliza
    private void CrearPolvo()
    {
        Shader shader = Shader.Find("Sprites/Default");
        if (shader == null) return; // sin shader no hay polvo, la animacion sigue igual

        GameObject go = new GameObject("PolvoDeslizar");
        go.transform.SetParent(transform, false);
        go.transform.localPosition = new Vector3(0f, 0.05f, 0f);

        polvo = go.AddComponent<ParticleSystem>();
        polvo.Stop();

        ParticleSystem.MainModule main = polvo.main;
        main.loop = true;
        main.playOnAwake = false;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.startLifetime = 0.45f;
        main.startSpeed = 0.4f;
        main.startSize = new ParticleSystem.MinMaxCurve(0.08f, 0.16f);
        main.startColor = new Color(0.85f, 0.8f, 0.7f, 0.55f);
        main.gravityModifier = -0.05f;

        ParticleSystem.EmissionModule emision = polvo.emission;
        emision.rateOverTime = 0f;

        ParticleSystem.ShapeModule forma = polvo.shape;
        forma.shapeType = ParticleSystemShapeType.Box;
        forma.scale = new Vector3(0.1f, 0.02f, 0.25f);

        ParticleSystem.SizeOverLifetimeModule tamano = polvo.sizeOverLifetime;
        tamano.enabled = true;
        tamano.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0.6f, 1f, 1.8f));

        // se desvanece al final de su vida
        ParticleSystem.ColorOverLifetimeModule color = polvo.colorOverLifetime;
        color.enabled = true;
        Gradient degradado = new Gradient();
        degradado.SetKeys(
            new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new[] { new GradientAlphaKey(0.9f, 0f), new GradientAlphaKey(0f, 1f) });
        color.color = degradado;

        ParticleSystemRenderer render = go.GetComponent<ParticleSystemRenderer>();
        Material material = new Material(shader);
        material.mainTexture = CirculoSuave();
        render.material = material;

        polvo.Play();
    }

    // circulo con borde difuso, para que el polvo no se vea como cuadros
    private static Texture2D CirculoSuave()
    {
        const int tam = 32;
        Texture2D tex = new Texture2D(tam, tam, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;
        for (int y = 0; y < tam; y++)
        {
            for (int x = 0; x < tam; x++)
            {
                float d = Vector2.Distance(new Vector2(x, y), new Vector2(tam / 2f, tam / 2f)) / (tam / 2f);
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, Mathf.Clamp01(1f - d)));
            }
        }
        tex.Apply();
        return tex;
    }
}
