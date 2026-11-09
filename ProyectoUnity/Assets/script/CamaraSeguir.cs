using UnityEngine;
using Unity.Netcode;
using System.Collections.Generic;

// Camara dinamica: sigue al jugador local pero nunca muestra nada fuera del fondo (Poster_fondo) del mapa donde esta.
//  - El zoom (campo de vision) se reduce solo si el encuadre no cabe dentro del fondo.
//  - La posicion se limita al rectangulo del fondo; si el mapa es chico para el encuadre, la camara queda quieta y centrada.
//  - Al cambiar de mapa la camara salta directo al nuevo (sin recorrer el espacio entre mapas).
public class CamaraSeguir : NetworkBehaviour
{
    [SerializeField] private float alturaExtra = 1f;
    [SerializeField] private float suavizado = 8f;
    [SerializeField] private float suavizadoZoom = 4f;
    [SerializeField] private float margen = 0.05f;        // sobrante en metros respecto al borde del fondo
    [SerializeField] private float pisoVisible = 1.5f;    // metros de piso que siempre se ven bajo los pies
    [SerializeField] private float cabezaVisible = 2.5f;  // metros sobre los pies que siempre se ven
    [SerializeField, Range(0.2f, 1f)] private float zoomMinimo = 0.5f; // fraccion del FOV base al acercarse a una esquina (menor = mas zoom)

    private Camera camara;
    private Transform cam;
    private float zOriginal;
    private float fovBase;
    private object fondoActual;                            // identidad del mapa actual (para detectar cambio de mapa)
    private readonly List<ZonaMapa> zonas = new List<ZonaMapa>();
    private float proximaBusqueda;

    // area util de un mapa: su Poster_fondo, o (si no tiene, como la sala) la geometria completa del mapa
    private class ZonaMapa { public object id; public Bounds b; }

    public override void OnNetworkSpawn()
    {
        // Si este personaje no es mio, no toco la cámara
        if (!IsOwner) { enabled = false; return; }

        camara = Camera.main;
        if (camara != null)
        {
            cam = camara.transform;
            zOriginal = cam.position.z;       // la camara conserva su distancia actual
            fovBase = camara.fieldOfView;     // zoom "ideal"; solo se reduce cuando no cabe
        }
    }

    private void BuscarFondos()
    {
        zonas.Clear();
        var posteres = new List<MeshRenderer>();
        foreach (MeshRenderer mr in FindObjectsByType<MeshRenderer>(FindObjectsInactive.Exclude))
            if (mr.name == "Poster_fondo") posteres.Add(mr);

        // un area por cada mapa (objeto raiz "ecena*")
        foreach (GameObject raiz in gameObject.scene.GetRootGameObjects())
        {
            if (!raiz.name.StartsWith("ecena", System.StringComparison.OrdinalIgnoreCase) || !raiz.activeInHierarchy) continue;

            MeshRenderer poster = null;
            foreach (MeshRenderer p in posteres)
                if (p.transform.IsChildOf(raiz.transform)) { poster = p; break; }

            if (poster != null) { zonas.Add(new ZonaMapa { id = raiz, b = poster.bounds }); posteres.Remove(poster); continue; }

            // sin Poster_fondo: se usa el volumen de todo el mapa
            bool hay = false; Bounds total = new Bounds();
            foreach (Renderer r in raiz.GetComponentsInChildren<Renderer>(false))
            {
                if (r is LineRenderer || r is ParticleSystemRenderer || r is TrailRenderer) continue;
                if (!hay) { total = r.bounds; hay = true; } else total.Encapsulate(r.bounds);
            }
            if (hay) zonas.Add(new ZonaMapa { id = raiz, b = total });
        }

        // posters que no cuelgan de ningun "ecena*"
        foreach (MeshRenderer p in posteres) zonas.Add(new ZonaMapa { id = p, b = p.bounds });
        proximaBusqueda = Time.time + 2f;
    }

    // la zona del mapa donde esta el jugador: la que contiene su X, o la mas cercana
    private ZonaMapa ElegirFondo(float x)
    {
        ZonaMapa mejor = null; float mejorDist = float.MaxValue;
        foreach (ZonaMapa z in zonas)
        {
            Bounds b = z.b;
            float d = x < b.min.x ? b.min.x - x : (x > b.max.x ? x - b.max.x : 0f);
            d += Mathf.Abs(x - b.center.x) * 0.001f; // desempate
            if (d < mejorDist) { mejorDist = d; mejor = z; }
        }
        return mejor;
    }

    private void LateUpdate()
    {
        if (cam == null) return;

        if (zonas.Count == 0 || Time.time > proximaBusqueda) BuscarFondos();
        ZonaMapa fondo = ElegirFondo(transform.position.x);
        object idFondo = fondo != null ? fondo.id : null;
        bool cambioMapa = idFondo != fondoActual;
        fondoActual = idFondo;

        Vector3 destino = new Vector3(transform.position.x, transform.position.y + alturaExtra, zOriginal);
        float fovDestino = fovBase;

        if (fondo != null)
        {
            Bounds r = fondo.b;
            float dist = Mathf.Max(0.5f, zOriginal - r.center.z); // distancia de la camara al plano del fondo

            // zoom: el campo de vision en el plano del fondo debe caber en el fondo
            float mitadAltoMax = Mathf.Max(0.1f, r.extents.y - margen);
            float mitadAnchoMax = Mathf.Max(0.1f, r.extents.x - margen);
            float mitadAltoFit = Mathf.Min(mitadAltoMax, mitadAnchoMax / Mathf.Max(0.1f, camara.aspect));
            float fovFit = 2f * Mathf.Atan(mitadAltoFit / dist) * Mathf.Rad2Deg;
            fovDestino = Mathf.Min(fovBase, fovFit);

            // zoom dinamico: cerca de una esquina/borde se acerca (zoom in) para que el jugador quede centrado;
            // en el centro del mapa se abre la vista. Nunca baja del zoom minimo.
            float distBorde = Mathf.Min(transform.position.x - r.min.x, r.max.x - transform.position.x) - margen;
            float mitadAltoBorde = Mathf.Max(0.1f, distBorde) / Mathf.Max(0.1f, camara.aspect);
            float fovBorde = 2f * Mathf.Atan(mitadAltoBorde / dist) * Mathf.Rad2Deg;
            float fovMinimo = fovBase * zoomMinimo;
            fovDestino = Mathf.Min(fovDestino, Mathf.Max(fovMinimo, fovBorde));

            // posicion: se usa el zoom destino para que el limite sea el correcto durante la transicion
            // (el mayor entre el zoom actual y el destino, asi nunca se muestra de mas durante la transicion)
            float fovUsar = cambioMapa ? fovDestino : Mathf.Max(camara.fieldOfView, fovDestino);
            float mitadAlto = dist * Mathf.Tan(fovUsar * 0.5f * Mathf.Deg2Rad);
            float mitadAncho = mitadAlto * camara.aspect;

            Vector3 jugador = new Vector3(transform.position.x, transform.position.y + alturaExtra, 0f);
            destino.x = LimitarEje(destino.x, r.min.x + mitadAncho + margen, r.max.x - mitadAncho - margen, r.center.x);
            destino.y = LimitarEje(destino.y, r.min.y + mitadAlto + margen, r.max.y - mitadAlto - margen, r.center.y);

            // el jugador siempre debe verse. El jugador esta mas cerca de la camara que el fondo,
            // asi que su encuadre real es mas chico: se calcula a SU distancia.
            float distJug = Mathf.Max(0.5f, zOriginal - transform.position.z);
            float mitadAltoJ = distJug * Mathf.Tan(fovUsar * 0.5f * Mathf.Deg2Rad);
            float mitadAnchoJ = mitadAltoJ * camara.aspect;
            destino.x = Mathf.Clamp(destino.x, jugador.x - mitadAnchoJ * 0.6f, jugador.x + mitadAnchoJ * 0.6f);
            // el piso tiene prioridad: el borde inferior de la vista queda siempre por debajo de los pies
            float pies = transform.position.y;
            destino.y = Mathf.Min(destino.y, pies - pisoVisible + mitadAltoJ);
            destino.y = Mathf.Max(destino.y, pies + cabezaVisible - mitadAltoJ);
        }

        if (cambioMapa)
        {
            // al entrar a otro mapa (reaparecer) la camara salta directo, sin barrer los espacios vacios entre mapas
            cam.position = destino;
            camara.fieldOfView = fovDestino;
            return;
        }

        camara.fieldOfView = Mathf.Lerp(camara.fieldOfView, fovDestino, suavizadoZoom * Time.deltaTime);
        // seguimiento suave; el destino ya esta limitado al fondo, asi que la camara tampoco se pasa al suavizar
        cam.position = Vector3.Lerp(cam.position, destino, suavizado * Time.deltaTime);
    }

    // limita v a [min, max]; si el rango no existe (el encuadre es mayor que el fondo) se centra
    private static float LimitarEje(float v, float min, float max, float centro)
    {
        if (min > max) return centro;
        return Mathf.Clamp(v, min, max);
    }
}
