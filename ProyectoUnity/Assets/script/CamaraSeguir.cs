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

    private Camera camara;
    private Transform cam;
    private float zOriginal;
    private float fovBase;
    private MeshRenderer fondoActual;
    private readonly List<MeshRenderer> fondos = new List<MeshRenderer>();
    private float proximaBusqueda;

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
        fondos.Clear();
        foreach (MeshRenderer mr in FindObjectsByType<MeshRenderer>(FindObjectsInactive.Exclude))
            if (mr.name == "Poster_fondo") fondos.Add(mr);
        proximaBusqueda = Time.time + 2f;
    }

    // el fondo del mapa donde esta el jugador: el que contiene su X, o el mas cercano
    private MeshRenderer ElegirFondo(float x)
    {
        MeshRenderer mejor = null; float mejorDist = float.MaxValue;
        foreach (MeshRenderer mr in fondos)
        {
            if (mr == null) continue;
            Bounds b = mr.bounds;
            float d = x < b.min.x ? b.min.x - x : (x > b.max.x ? x - b.max.x : 0f);
            d += Mathf.Abs(x - b.center.x) * 0.001f; // desempate
            if (d < mejorDist) { mejorDist = d; mejor = mr; }
        }
        return mejor;
    }

    private void LateUpdate()
    {
        if (cam == null) return;

        if (fondos.Count == 0 || Time.time > proximaBusqueda) BuscarFondos();
        MeshRenderer fondo = ElegirFondo(transform.position.x);
        bool cambioMapa = fondo != fondoActual;
        fondoActual = fondo;

        Vector3 destino = new Vector3(transform.position.x, transform.position.y + alturaExtra, zOriginal);
        float fovDestino = fovBase;

        if (fondo != null)
        {
            Bounds r = fondo.bounds;
            float dist = Mathf.Max(0.5f, zOriginal - r.center.z); // distancia de la camara al plano del fondo

            // zoom: el campo de vision en el plano del fondo debe caber en el fondo
            float mitadAltoMax = Mathf.Max(0.1f, r.extents.y - margen);
            float mitadAnchoMax = Mathf.Max(0.1f, r.extents.x - margen);
            float mitadAltoFit = Mathf.Min(mitadAltoMax, mitadAnchoMax / Mathf.Max(0.1f, camara.aspect));
            float fovFit = 2f * Mathf.Atan(mitadAltoFit / dist) * Mathf.Rad2Deg;
            fovDestino = Mathf.Min(fovBase, fovFit);

            // posicion: se usa el zoom destino para que el limite sea el correcto durante la transicion
            // (el mayor entre el zoom actual y el destino, asi nunca se muestra de mas durante la transicion)
            float fovUsar = cambioMapa ? fovDestino : Mathf.Max(camara.fieldOfView, fovDestino);
            float mitadAlto = dist * Mathf.Tan(fovUsar * 0.5f * Mathf.Deg2Rad);
            float mitadAncho = mitadAlto * camara.aspect;

            destino.x = LimitarEje(destino.x, r.min.x + mitadAncho + margen, r.max.x - mitadAncho - margen, r.center.x);
            destino.y = LimitarEje(destino.y, r.min.y + mitadAlto + margen, r.max.y - mitadAlto - margen, r.center.y);
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
