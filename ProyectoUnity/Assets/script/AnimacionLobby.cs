using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

// Animacion del personaje en la vista previa del lobby (solo visual, no se sincroniza).
// 1) "Stand Up" una vez  2) saluda  3) queda en "Standing Idle" en ciclo con detalles de personalidad:
//    respira, mira el panorama, saluda de vez en cuando, inclina la cabeza y da saltitos.
// Ademas mantiene al modelo centrado y con los pies en el piso en todo momento
// (evita que algun modelo se hunda, flote o se salga del recuadro).
public class AnimacionLobby : MonoBehaviour
{
    private enum Accion { Ninguna, Saludar, Mirar, LadearCabeza, Saltito }

    private Animator anim;
    private PlayableGraph grafo;
    private AnimationMixerPlayable mezclador;
    private AnimationClipPlayable pLevantarse, pReposo, pSaltito;
    private bool hayLevantarse, hayReposo, haySaltito;

    private Vector3 origen;          // punto del piso donde va parado el modelo
    private Renderer[] renderers;
    private bool levantandose;
    private bool saludoInicialHecho;

    // accion actual
    private Accion accion = Accion.Ninguna;
    private float inicioAccion, duracionAccion, siguienteAccion;
    private float ladoMirada;        // -1 izquierda, 1 derecha

    // pesos suavizados de cada capa procedural
    private float pesoSaludo, pesoMirar, pesoLadear;

    // huesos (solo si el avatar es humanoide)
    private Transform hCabeza, hCuello, hPecho, hBrazoDer, hAntebrazoDer, hManoDer, hBrazoIzq, hCadera;

    private const float Fundido = 0.3f;

    public void Iniciar(Avatar avatar, CatalogoPersonajes cat, Vector3 puntoPiso)
    {
        origen = puntoPiso;
        renderers = GetComponentsInChildren<Renderer>(true);
        foreach (Renderer r in renderers)
        {
            SkinnedMeshRenderer smr = r as SkinnedMeshRenderer;
            if (smr != null) smr.updateWhenOffscreen = true; // bounds reales cada cuadro para apoyar los pies
        }

        anim = GetComponent<Animator>();
        if (anim == null) anim = gameObject.AddComponent<Animator>();
        anim.enabled = true;
        anim.runtimeAnimatorController = null;
        if (avatar != null) anim.avatar = avatar;
        anim.applyRootMotion = false;
        anim.cullingMode = AnimatorCullingMode.AlwaysAnimate;

        if (anim.avatar == null || !anim.avatar.isValid || !anim.avatar.isHuman)
        {
            Debug.LogWarning($"AnimacionLobby: el avatar de '{name}' no es humanoide válido; se muestra sin animación.");
            return;
        }

        hCabeza = anim.GetBoneTransform(HumanBodyBones.Head);
        hCuello = anim.GetBoneTransform(HumanBodyBones.Neck);
        hPecho = anim.GetBoneTransform(HumanBodyBones.Chest);
        if (hPecho == null) hPecho = anim.GetBoneTransform(HumanBodyBones.Spine);
        hBrazoDer = anim.GetBoneTransform(HumanBodyBones.RightUpperArm);
        hAntebrazoDer = anim.GetBoneTransform(HumanBodyBones.RightLowerArm);
        hManoDer = anim.GetBoneTransform(HumanBodyBones.RightHand);
        hBrazoIzq = anim.GetBoneTransform(HumanBodyBones.LeftUpperArm);
        hCadera = anim.GetBoneTransform(HumanBodyBones.Hips);

        grafo = PlayableGraph.Create("LobbyPersonaje");
        grafo.SetTimeUpdateMode(DirectorUpdateMode.GameTime);
        AnimationPlayableOutput salida = AnimationPlayableOutput.Create(grafo, "Salida", anim);
        mezclador = AnimationMixerPlayable.Create(grafo, 3);
        salida.SetSourcePlayable(mezclador);

        hayLevantarse = Conectar(cat.clipLevantarse, 0, true, out pLevantarse);
        hayReposo = Conectar(cat.clipReposo, 1, false, out pReposo);
        haySaltito = Conectar(cat.clipSaltito, 2, true, out pSaltito);

        levantandose = hayLevantarse;
        mezclador.SetInputWeight(0, hayLevantarse ? 1f : 0f);
        mezclador.SetInputWeight(1, hayLevantarse ? 0f : 1f);
        mezclador.SetInputWeight(2, 0f);
        if (hayReposo && hayLevantarse) pReposo.Pause();
        if (haySaltito) pSaltito.Pause();

        if (!hayLevantarse && !hayReposo)
            Debug.LogWarning("AnimacionLobby: el catálogo no tiene clipLevantarse/clipReposo asignados (menú Herramientas > Asignar animaciones del lobby).");

        siguienteAccion = Time.time + 999f; // las acciones empiezan al terminar de levantarse
        if (!hayLevantarse) TerminarDeLevantarse();

        grafo.Play();
    }

    private bool Conectar(AnimationClip clip, int entrada, bool unaVez, out AnimationClipPlayable p)
    {
        p = default;
        if (clip == null) return false;
        p = AnimationClipPlayable.Create(grafo, clip);
        if (unaVez) p.SetDuration(clip.length); // se reproduce una sola vez aunque el clip tenga ciclo
        grafo.Connect(p, 0, mezclador, entrada);
        return true;
    }

    private void TerminarDeLevantarse()
    {
        levantandose = false;
        // primero saluda a la camara, despues acciones al azar
        siguienteAccion = Time.time + 0.2f;
    }

    private void Update()
    {
        if (!grafo.IsValid()) return;

        // ---- Stand Up -> Standing Idle ----
        if (levantandose)
        {
            float largo = (float)pLevantarse.GetDuration();
            float t = (float)pLevantarse.GetTime();
            if (hayReposo && t >= largo - Fundido)
            {
                if (pReposo.GetPlayState() != PlayState.Playing) { pReposo.SetTime(0); pReposo.Play(); }
                float w = Mathf.Clamp01((t - (largo - Fundido)) / Fundido);
                mezclador.SetInputWeight(0, 1f - w);
                mezclador.SetInputWeight(1, w);
            }
            if (t >= largo)
            {
                pLevantarse.Pause();
                mezclador.SetInputWeight(0, 0f);
                mezclador.SetInputWeight(1, hayReposo ? 1f : 0f);
                TerminarDeLevantarse();
            }
            return;
        }

        // ---- elegir siguiente accion ----
        if (accion == Accion.Ninguna && Time.time >= siguienteAccion)
        {
            if (!saludoInicialHecho) { EmpezarAccion(Accion.Saludar); saludoInicialHecho = true; }
            else
            {
                float r = Random.value;
                if (r < 0.30f) EmpezarAccion(Accion.Mirar);
                else if (r < 0.55f) EmpezarAccion(Accion.Saludar);
                else if (r < 0.75f) EmpezarAccion(Accion.LadearCabeza);
                else EmpezarAccion(haySaltito ? Accion.Saltito : Accion.Mirar);
            }
        }

        // ---- saltito: mezcla el clip de salto y luego regresa al reposo ----
        if (accion == Accion.Saltito)
        {
            float largo = (float)pSaltito.GetDuration();
            float t = (float)pSaltito.GetTime();
            float w = Mathf.Min(Mathf.Clamp01(t / 0.15f), Mathf.Clamp01((largo - t) / 0.25f));
            mezclador.SetInputWeight(2, w);
            mezclador.SetInputWeight(1, hayReposo ? 1f - w : 0f);
            if (t >= largo) TerminarAccion();
        }
        else if (Time.time - inicioAccion >= duracionAccion && accion != Accion.Ninguna)
        {
            TerminarAccion();
        }

        // ---- pesos suaves de las capas procedurales ----
        float vel = Time.deltaTime * 5f;
        pesoSaludo = Mathf.MoveTowards(pesoSaludo, accion == Accion.Saludar ? 1f : 0f, vel);
        pesoMirar = Mathf.MoveTowards(pesoMirar, accion == Accion.Mirar ? 1f : 0f, vel * 0.6f);
        pesoLadear = Mathf.MoveTowards(pesoLadear, accion == Accion.LadearCabeza ? 1f : 0f, vel);
    }

    private void EmpezarAccion(Accion a)
    {
        accion = a;
        inicioAccion = Time.time;
        switch (a)
        {
            case Accion.Saludar: duracionAccion = 2.4f; break;
            case Accion.Mirar: duracionAccion = 2.8f; ladoMirada = Random.value < 0.5f ? -1f : 1f; break;
            case Accion.LadearCabeza: duracionAccion = 1.6f; ladoMirada = Random.value < 0.5f ? -1f : 1f; break;
            case Accion.Saltito:
                pSaltito.SetTime(0);
                pSaltito.Play();
                duracionAccion = (float)pSaltito.GetDuration();
                break;
        }
    }

    private void TerminarAccion()
    {
        if (accion == Accion.Saltito)
        {
            pSaltito.Pause();
            mezclador.SetInputWeight(2, 0f);
            mezclador.SetInputWeight(1, hayReposo ? 1f : 0f);
        }
        accion = Accion.Ninguna;
        siguienteAccion = Time.time + Random.Range(1.5f, 3.5f);
    }

    // Despues de que el Animator aplica la pose: capas procedurales y ajuste al piso
    private void LateUpdate()
    {
        if (grafo.IsValid() && !levantandose) AplicarPersonalidad();
        AjustarAlPiso();
    }

    private void AplicarPersonalidad()
    {
        if (hBrazoDer == null || hBrazoIzq == null) return;

        // ejes del personaje (no del objeto, porque algun modelo viene girado)
        Vector3 derecha = (hBrazoDer.position - hBrazoIzq.position);
        derecha.y = 0f;
        if (derecha.sqrMagnitude < 1e-6f) return;
        derecha.Normalize();
        Vector3 arriba = Vector3.up;
        Vector3 frente = Vector3.Cross(derecha, arriba);
        float t = Time.time;

        // respiracion: el pecho sube y baja un poquito
        if (hPecho != null)
            hPecho.rotation = Quaternion.AngleAxis(Mathf.Sin(t * 1.8f) * 2.5f, derecha) * hPecho.rotation;

        // cabeza: siempre mira un poco el panorama (ruido suave) + mirar a un lado + ladear
        if (hCabeza != null)
        {
            float yaw = (Mathf.PerlinNoise(t * 0.35f, 0.1f) - 0.5f) * 30f + pesoMirar * ladoMirada * 45f;
            float pitch = (Mathf.PerlinNoise(0.7f, t * 0.3f) - 0.5f) * 12f - pesoMirar * 5f;
            float roll = pesoLadear * ladoMirada * 18f * (1f + 0.15f * Mathf.Sin(t * 6f));
            Quaternion giro = Quaternion.AngleAxis(yaw, arriba) * Quaternion.AngleAxis(pitch, derecha) * Quaternion.AngleAxis(roll, frente);
            Transform h = hCuello != null ? hCuello : hCabeza;
            h.rotation = Quaternion.Slerp(Quaternion.identity, giro, 0.4f) * h.rotation;
            hCabeza.rotation = Quaternion.Slerp(Quaternion.identity, giro, 0.6f) * hCabeza.rotation;
        }

        // saludo con la mano derecha: sube el brazo y mueve el antebrazo de lado a lado
        if (pesoSaludo > 0.001f && hAntebrazoDer != null)
        {
            float e = Mathf.SmoothStep(0f, 1f, pesoSaludo);
            hBrazoDer.rotation = Quaternion.AngleAxis(140f * e, frente) * Quaternion.AngleAxis(-15f * e, arriba) * hBrazoDer.rotation;
            float vaiven = Mathf.Sin(t * 11f) * 28f;
            hAntebrazoDer.rotation = Quaternion.AngleAxis((25f + vaiven) * e, frente) * hAntebrazoDer.rotation;
            if (hManoDer != null)
                hManoDer.rotation = Quaternion.AngleAxis(vaiven * 0.4f * e, frente) * hManoDer.rotation;
        }
    }

    // Mantiene la cadera centrada en el recuadro y los pies sobre el piso
    private void AjustarAlPiso()
    {
        if (renderers == null) return;

        float suave = 1f - Mathf.Exp(-Time.deltaTime * 12f);
        Vector3 pos = transform.position;

        if (hCadera != null)
        {
            Vector3 d = origen - hCadera.position;
            pos.x += d.x * suave;
            pos.z += d.z * suave;
        }

        // durante el saltito no se corrige la altura (para que se vea el salto)
        if (accion != Accion.Saltito)
        {
            bool hay = false;
            float minY = 0f;
            foreach (Renderer r in renderers)
            {
                if (r == null || !r.enabled) continue;
                float y = r.bounds.min.y;
                if (!hay || y < minY) { minY = y; hay = true; }
            }
            if (hay) pos.y += (origen.y - minY) * suave;
        }

        transform.position = pos;
    }

    private void OnDestroy()
    {
        if (grafo.IsValid()) grafo.Destroy();
    }
}
