using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Herramienta de editor: revisa que un mapa sea "plataformeable".
/// Busca las superficies donde se puede parar el jugador (raycast hacia abajo en el plano de juego),
/// calcula cuales son alcanzables saltando desde el piso (salto ~2 m, ver Personaje.salto/gravedad)
/// y dice si cada punto spawnArma se puede agarrar. Menu: Herramientas/Auditar mapas (resultado en la consola).
/// </summary>
public static class AuditoriaMapas
{
    // fisica del jugador (Personaje: salto 10, gravedad 25, velocidad 5)
    private const float SALTO = 10f, GRAVEDAD = 25f, VELOCIDAD = 5f;
    private const float MARGEN_SUBIDA = 0.25f; // el salto real se queda un poco corto del maximo teorico

    private class Segmento
    {
        public float x0, x1, y;
        public bool alcanzable;
    }

    [MenuItem("Herramientas/Auditar mapas")]
    public static void AuditarTodos()
    {
        StringBuilder sb = new StringBuilder();
        foreach (GameObject go in Object.FindObjectsByType<GameObject>(FindObjectsInactive.Include))
        {
            if (go.transform.parent == null && go.name.StartsWith("ecena", System.StringComparison.OrdinalIgnoreCase))
                sb.AppendLine(Auditar(go.name));
        }
        Debug.Log(sb.ToString());
    }

    public static string Auditar(string nombreMapa, bool detalle = true)
    {
        GameObject mapa = GameObject.Find(nombreMapa);
        if (mapa == null) return nombreMapa + ": no existe";
        Physics.SyncTransforms();

        // plano de juego = mediana de la Z de los spawns de jugador (igual que GameManager.TodosLosSpawns)
        List<Transform> spawns = GameManager.BuscarPuntos(mapa, GameManager.PREFIJO_JUGADOR);
        List<Transform> armas = GameManager.BuscarPuntos(mapa, GameManager.PREFIJO_ARMA);
        List<float> zs = new List<float>();
        foreach (Transform s in spawns) zs.Add(s.position.z);
        zs.Sort();
        float planoZ = zs.Count > 0 ? zs[zs.Count / 2] : mapa.transform.position.z;

        Bounds caja = new Bounds(); bool hay = false;
        foreach (Collider c in mapa.GetComponentsInChildren<Collider>(true))
        {
            if (!hay) { caja = c.bounds; hay = true; } else caja.Encapsulate(c.bounds);
        }
        StringBuilder sb = new StringBuilder();
        sb.AppendLine("=== " + nombreMapa + " planoZ=" + planoZ.ToString("F2") + " x[" + caja.min.x.ToString("F1") + "," + caja.max.x.ToString("F1") + "] y[" + caja.min.y.ToString("F1") + "," + caja.max.y.ToString("F1") + "]");

        Segmento piso;
        List<Segmento> segs = Analizar(mapa, caja, planoZ, out piso);

        int total = 0, ok = 0;
        foreach (Segmento s in segs) { if (s.x1 - s.x0 < 0.5f) continue; total++; if (s.alcanzable) ok++; }
        sb.AppendLine("superficies: " + ok + "/" + total + " alcanzables (piso y=" + (piso != null ? piso.y.ToString("F2") : "?") + ")");
        if (detalle)
        {
            foreach (Segmento s in segs)
            {
                if (s.x1 - s.x0 < 0.5f) continue;
                sb.AppendLine("  " + (s.alcanzable ? "ok " : "NO ") + "y=" + s.y.ToString("F2") + " x[" + s.x0.ToString("F1") + "," + s.x1.ToString("F1") + "]");
            }
        }

        // 3) armas: se agarran tocando la caja, asi que el spawn tiene que quedar sobre una superficie alcanzable
        foreach (Transform t in armas)
        {
            string estado = "NO ALCANZABLE";
            float mejor = float.MaxValue;
            foreach (Segmento s in segs)
            {
                if (!s.alcanzable) continue;
                if (t.position.x < s.x0 - 0.4f || t.position.x > s.x1 + 0.4f) continue;
                float dy = t.position.y - s.y; // cuanto esta el spawn sobre la superficie
                if (dy >= -0.3f && dy <= 1.6f && Mathf.Abs(dy) < mejor) { mejor = Mathf.Abs(dy); estado = "ok (" + dy.ToString("F2") + " sobre la superficie)"; }
            }
            float dz = Mathf.Abs(t.position.z - planoZ);
            if (dz > 0.8f) estado += " | FUERA DEL PLANO dz=" + dz.ToString("F1");
            sb.AppendLine("  " + t.name + " " + t.position.ToString("F1") + " -> " + estado);
        }
        // 4) spawns de jugador
        foreach (Transform t in spawns)
        {
            if (t.name.StartsWith(GameManager.PREFIJO_ARMA, System.StringComparison.OrdinalIgnoreCase)) continue;
            bool bien = false;
            foreach (Segmento s in segs)
                if (s.alcanzable && t.position.x >= s.x0 - 0.3f && t.position.x <= s.x1 + 0.3f && t.position.y - s.y > -0.3f && t.position.y - s.y < 1.5f) bien = true;
            if (!bien) sb.AppendLine("  JUGADOR " + t.name + " " + t.position.ToString("F1") + " no esta sobre una superficie alcanzable");
        }
        return sb.ToString();
    }

    // superficies donde se puede parar el jugador y cuales son alcanzables desde el piso
    private static List<Segmento> Analizar(GameObject mapa, Bounds caja, float planoZ, out Segmento piso)
    {
        // 1) superficies: rayos hacia abajo cada 0.15 m
        List<Segmento> segs = new List<Segmento>();
        Segmento actual = null;
        float paso = 0.15f;
        for (float x = caja.min.x + 0.2f; x < caja.max.x - 0.2f; x += paso)
        {
            RaycastHit[] hits = Physics.RaycastAll(new Vector3(x, caja.max.y + 1f, planoZ), Vector3.down, caja.size.y + 3f, ~0, QueryTriggerInteraction.Ignore);
            System.Array.Sort(hits, (a, b) => b.point.y.CompareTo(a.point.y));
            List<float> alturas = new List<float>();
            foreach (RaycastHit h in hits)
            {
                if (h.normal.y < 0.7f) continue;
                if (h.collider.transform.IsChildOf(mapa.transform) == false) continue;
                // debe haber hueco para pararse (el techo/estantes de arriba no deben estar pegados)
                if (Physics.Raycast(h.point + Vector3.up * 0.03f, Vector3.up, 0.7f, ~0, QueryTriggerInteraction.Ignore)) continue;
                alturas.Add(h.point.y);
            }
            // cada altura nueva continua un segmento abierto de altura parecida, o abre uno nuevo
            foreach (float y in alturas)
            {
                Segmento cont = null;
                for (int i = segs.Count - 1; i >= 0 && i >= segs.Count - 40; i--)
                {
                    Segmento s = segs[i];
                    if (Mathf.Abs(s.y - y) < 0.2f && x - s.x1 <= paso * 1.6f) { cont = s; break; }
                }
                if (cont != null) cont.x1 = x;
                else segs.Add(new Segmento { x0 = x, x1 = x, y = y });
            }
        }
        segs.RemoveAll(s => s.x1 - s.x0 < 0.25f); // astillas
        segs.RemoveAll(s => s.y > caja.max.y - 0.6f);  // la cara de arriba del techo no cuenta: no se puede subir ahi

        // 2) alcanzabilidad: desde el segmento mas bajo y ancho (el piso)
        piso = null;
        foreach (Segmento s in segs) if ((s.x1 - s.x0) > 2.5f && (piso == null || s.y < piso.y)) piso = s; // el piso: la superficie ancha mas baja
        Queue<Segmento> cola = new Queue<Segmento>();
        if (piso != null) { piso.alcanzable = true; cola.Enqueue(piso); }
        while (cola.Count > 0)
        {
            Segmento a = cola.Dequeue();
            foreach (Segmento b in segs)
            {
                if (b.alcanzable) continue;
                if (PuedeIr(a, b)) { b.alcanzable = true; cola.Enqueue(b); }
            }
        }

        return segs;
    }

    // ---- utilidades para reacomodar props del mapa (todo con Undo) ----

    // hijo del modelo con ese nombre, el mas cercano en X a xAprox (hay nombres repetidos)
    public static Transform Buscar(string mapa, string nombre, float xAprox)
    {
        GameObject raiz = GameObject.Find(mapa);
        Transform mejor = null; float md = float.MaxValue;
        foreach (Transform t in raiz.GetComponentsInChildren<Transform>(true))
        {
            if (t.name != nombre) continue;
            Collider c = t.GetComponent<Collider>();
            if (c == null) continue;
            float d = Mathf.Abs(c.bounds.center.x - xAprox);
            if (d < md) { md = d; mejor = t; }
        }
        return mejor;
    }

    // deja el borde superior del objeto en "top" y lo corre dx en X
    public static string Mover(string mapa, string nombre, float xAprox, float top, float dx)
    {
        Transform t = Buscar(mapa, nombre, xAprox);
        if (t == null) return "NO ENCONTRADO " + nombre;
        Collider c = t.GetComponent<Collider>();
        Undo.RecordObject(t, "Reacomodar mapa");
        t.position += new Vector3(dx, top - c.bounds.max.y, 0f);
        Physics.SyncTransforms();
        return nombre + " -> top " + top + " dx " + dx;
    }

    // copia "modelo" (misma textura/collider) y la deja con centro en x y borde superior en top, en el plano de juego
    public static Transform Copiar(string mapa, string nombreModelo, float xModelo, string nombreNuevo, float x, float top, float planoZ)
    {
        Transform orig = Buscar(mapa, nombreModelo, xModelo);
        GameObject g = Object.Instantiate(orig.gameObject, orig.parent);
        g.name = nombreNuevo;
        Undo.RegisterCreatedObjectUndo(g, "Copiar prop");
        Physics.SyncTransforms();
        Collider c = g.GetComponent<Collider>();
        g.transform.position += new Vector3(x - c.bounds.center.x, top - c.bounds.max.y, planoZ - c.bounds.center.z);
        Physics.SyncTransforms();
        return g.transform;
    }

    // copia una tabla/repisa larga y la acorta a "ancho" metros: sirve de escalon flotante pegado a la pared
    public static Transform Escalon(string mapa, string nombreModelo, float xModelo, string nombreNuevo, float xIzq, float xDer, float top, float planoZ)
    {
        Transform t = Copiar(mapa, nombreModelo, xModelo, nombreNuevo, (xIzq + xDer) * 0.5f, top, planoZ);
        Collider c = t.GetComponent<Collider>();
        float factor = (xDer - xIzq) / c.bounds.size.x;
        t.localScale = new Vector3(t.localScale.x * factor, t.localScale.y, t.localScale.z);
        Physics.SyncTransforms();
        c = t.GetComponent<Collider>();
        t.position += new Vector3((xIzq + xDer) * 0.5f - c.bounds.center.x, top - c.bounds.max.y, 0f);
        Physics.SyncTransforms();
        return t;
    }

    // Pone los spawns (jugador y arma) sobre la superficie alcanzable mas cercana a donde estaban.
    // sobreSuelo: altura del punto sobre la superficie (jugador 0.2, arma 0.4)
    public static string AjustarSpawns(string nombreMapa)
    {
        GameObject mapa = GameObject.Find(nombreMapa);
        Physics.SyncTransforms();
        List<Transform> spawns = GameManager.BuscarPuntos(mapa, GameManager.PREFIJO_JUGADOR);
        List<Transform> armas = GameManager.BuscarPuntos(mapa, GameManager.PREFIJO_ARMA);
        List<float> zs = new List<float>();
        foreach (Transform s in spawns) zs.Add(s.position.z);
        zs.Sort();
        float planoZ = zs[zs.Count / 2];
        Bounds caja = new Bounds(); bool hay = false;
        foreach (Collider c in mapa.GetComponentsInChildren<Collider>(true))
        {
            if (!hay) { caja = c.bounds; hay = true; } else caja.Encapsulate(c.bounds);
        }
        Segmento piso;
        List<Segmento> segs = Analizar(mapa, caja, planoZ, out piso);
        StringBuilder sb = new StringBuilder();
        List<Transform> todos = new List<Transform>(spawns); todos.AddRange(armas);
        foreach (Transform t in todos)
        {
            bool esArma = t.name.StartsWith(GameManager.PREFIJO_ARMA, System.StringComparison.OrdinalIgnoreCase);
            float suelo = esArma ? 0.4f : 0.2f;
            Vector3 p = t.position;
            float mejor = float.MaxValue; Vector3 destino = p; bool hubo = false;
            foreach (Segmento s in segs)
            {
                if (!s.alcanzable || s.x1 - s.x0 < 0.6f) continue;
                if (s.y > p.y + 1.5f) continue; // no subirlo mucho mas de lo que estaba
                float x = Mathf.Clamp(p.x, s.x0 + 0.25f, s.x1 - 0.25f);
                float costo = Mathf.Abs(x - p.x) * 1.5f + Mathf.Abs((s.y + suelo) - p.y);
                if (costo < mejor) { mejor = costo; destino = new Vector3(x, s.y + suelo, planoZ); hubo = true; }
            }
            if (hubo)
            {
                Undo.RecordObject(t, "Ajustar spawn");
                if ((destino - p).sqrMagnitude > 0.01f) sb.AppendLine(t.name + " " + p.ToString("F1") + " -> " + destino.ToString("F1"));
                t.position = destino;
            }
        }
        return sb.ToString();
    }

    // ---- trampas: lugares donde el jugador cae y ya no puede volver a un spawn ----

    [MenuItem("Herramientas/Buscar trampas en mapas")]
    public static void BuscarTrampasTodos()
    {
        StringBuilder sb = new StringBuilder();
        foreach (GameObject go in Object.FindObjectsByType<GameObject>(FindObjectsInactive.Exclude))
        {
            if (go.transform.parent == null && go.name.StartsWith("ecena", System.StringComparison.OrdinalIgnoreCase))
                sb.AppendLine(BuscarTrampas(go.name));
        }
        Debug.Log(sb.ToString());
    }

    /// <summary>
    /// Simula con un CharacterController igual al del prefab (cuerpo.prefab) lo mismo que hace Personaje:
    /// mover en X, gravedad, saltar solo si isGrounded, el techo corta el salto y control total en el aire.
    /// Desde los spawns prueba saltos y pasos a cada lado; luego revisa desde que posiciones se puede regresar a un spawn.
    /// "sin salida" = posiciones a las que se llega pero de las que no se sale. "atorado" = queda en el aire sin isGrounded.
    /// </summary>
    public static string BuscarTrampas(string nombreMapa)
    {
        const float DT = 1f / 30f;
        GameObject mapa = GameObject.Find(nombreMapa);
        if (mapa == null) return nombreMapa + ": no existe";
        CharacterController modelo = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/cuerpo.prefab").GetComponent<CharacterController>();
        GameObject prueba = new GameObject("__ccPrueba");
        prueba.hideFlags = HideFlags.HideAndDontSave;
        CharacterController cc = prueba.AddComponent<CharacterController>();
        cc.height = modelo.height; cc.radius = modelo.radius; cc.center = modelo.center; cc.stepOffset = modelo.stepOffset;
        cc.slopeLimit = modelo.slopeLimit; cc.skinWidth = modelo.skinWidth; cc.minMoveDistance = 0f;
        try
        {
            List<Transform> spawns = GameManager.BuscarPuntos(mapa, GameManager.PREFIJO_JUGADOR);
            if (spawns.Count == 0) return nombreMapa + ": sin spawns";
            float z = spawns[0].position.z;
            float yMin = float.MaxValue;
            foreach (Collider c in mapa.GetComponentsInChildren<Collider>()) yMin = Mathf.Min(yMin, c.bounds.min.y);
            yMin -= 3f;

            // estado: 0 aterrizo, 1 cayo al vacio, 2 atorado en el aire, 3 se acabo el tiempo
            int estado = 0;
            System.Func<Vector3, bool, float, float, float, Vector3> simular = (p, saltar, vx0, t1, vx1) =>
            {
                prueba.transform.position = p; Physics.SyncTransforms();
                float vy = saltar ? SALTO : -2f, t = 0f; int quieto = 0; Vector3 antes = p; bool despego = !saltar;
                while (t < 2.5f)
                {
                    cc.Move(new Vector3((t < t1 ? vx0 : vx1) * DT, 0f, 0f));
                    if (cc.isGrounded && vy < 0f) vy = -2f;
                    vy = Mathf.Max(vy - GRAVEDAD * DT, -30f);
                    cc.Move(new Vector3(0f, vy * DT, z - prueba.transform.position.z));
                    if ((cc.collisionFlags & CollisionFlags.Above) != 0 && vy > 0f) vy = 0f;
                    t += DT;
                    Vector3 q = prueba.transform.position;
                    if (Mathf.Abs(q.y - p.y) > 0.05f || Mathf.Abs(q.x - p.x) > 0.05f) despego = true;
                    if (cc.isGrounded && vy <= 0f && (despego || t > 0.3f)) { estado = 0; return q; }
                    if (q.y < yMin) { estado = 1; return q; }
                    if ((q - antes).sqrMagnitude < 0.0001f && !cc.isGrounded) { if (++quieto > 15) { estado = 2; return q; } }
                    else quieto = 0;
                    antes = q;
                }
                estado = 3; return prueba.transform.position;
            };
            System.Func<Vector3, string> clave = p => Mathf.RoundToInt(p.x * 10f) + "," + Mathf.RoundToInt(p.y * 10f);

            // {velocidad X hasta t1, t1, velocidad X despues}: saltos rectos, verticales y con cambio de direccion en el aire
            float V = VELOCIDAD;
            float[][] acciones = {
                new[]{-V,9f,-V}, new[]{V,9f,V}, new[]{0f,9f,0f},
                new[]{0f,0.25f,V}, new[]{0f,0.25f,-V}, new[]{0f,0.5f,V}, new[]{0f,0.5f,-V},
                new[]{V,0.15f,0f}, new[]{-V,0.15f,0f}, new[]{V,0.3f,0f}, new[]{-V,0.3f,0f} };

            Dictionary<string, Vector3> pos = new Dictionary<string, Vector3>();
            Dictionary<string, List<string>> ady = new Dictionary<string, List<string>>();
            Queue<string> cola = new Queue<string>();
            Dictionary<string, Vector3> atorados = new Dictionary<string, Vector3>();
            System.Func<Vector3, string> agregar = p =>
            {
                string k = clave(p);
                if (!pos.ContainsKey(k)) { pos[k] = p; ady[k] = new List<string>(); cola.Enqueue(k); }
                return k;
            };
            List<string> inicio = new List<string>();
            foreach (Transform s in spawns)
            {
                Vector3 q = simular(new Vector3(s.position.x, s.position.y + 0.3f, z), false, 0f, 9f, 0f);
                if (estado == 0) inicio.Add(agregar(q));
            }
            while (cola.Count > 0 && pos.Count < 5000)
            {
                string k = cola.Dequeue(); Vector3 p = pos[k];
                List<Vector3> destinos = new List<Vector3>();
                foreach (float[] a in acciones)
                {
                    Vector3 q = simular(p, true, a[0], a[1], a[2]);
                    if (estado == 0) destinos.Add(q); else if (estado == 2) atorados[clave(q)] = q;
                }
                foreach (float d in new[] { -V, V })
                {
                    Vector3 q = simular(p, false, d, 0.02f, 0f); // un paso de ~0.17 m
                    if (estado == 0) destinos.Add(q); else if (estado == 2) atorados[clave(q)] = q;
                }
                foreach (Vector3 q in destinos) { string kq = agregar(q); if (kq != k && !ady[k].Contains(kq)) ady[k].Add(kq); }
            }

            // de que posiciones se puede regresar a un spawn (busqueda hacia atras)
            Dictionary<string, List<string>> inversa = new Dictionary<string, List<string>>();
            foreach (var kv in ady) foreach (string d in kv.Value)
            {
                if (!inversa.ContainsKey(d)) inversa[d] = new List<string>();
                inversa[d].Add(kv.Key);
            }
            HashSet<string> sale = new HashSet<string>(); Queue<string> c2 = new Queue<string>();
            foreach (string k in inicio) if (sale.Add(k)) c2.Enqueue(k);
            while (c2.Count > 0)
            {
                string k = c2.Dequeue();
                if (!inversa.ContainsKey(k)) continue;
                foreach (string a in inversa[k]) if (sale.Add(a)) c2.Enqueue(a);
            }

            List<Vector3> sinSalida = new List<Vector3>();
            foreach (var kv in pos) if (!sale.Contains(kv.Key)) sinSalida.Add(kv.Value);
            sinSalida.Sort((a, b) => a.x.CompareTo(b.x));
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("=== " + nombreMapa + " posiciones=" + pos.Count + " sin salida=" + sinSalida.Count + " atorado=" + atorados.Count);
            foreach (Vector3 p in sinSalida) sb.AppendLine("  SIN SALIDA " + p.ToString("F1"));
            foreach (Vector3 p in atorados.Values) sb.AppendLine("  ATORADO " + p.ToString("F1"));
            return sb.ToString();
        }
        finally { Object.DestroyImmediate(prueba); }
    }

    private static bool PuedeIr(Segmento a, Segmento b)
    {
        float dy = b.y - a.y;
        if (dy > 2f * 0f + (SALTO * SALTO) / (2f * GRAVEDAD) - MARGEN_SUBIDA) return false; // demasiado alto
        float hueco = Mathf.Max(0f, Mathf.Max(b.x0 - a.x1, a.x0 - b.x1));
        float tiempo;
        if (dy > 0f)
        {
            float disc = SALTO * SALTO - 2f * GRAVEDAD * dy;
            if (disc < 0f) return false;
            tiempo = (SALTO + Mathf.Sqrt(disc)) / GRAVEDAD; // llega bajando, tiempo maximo en el aire sobre ese nivel
        }
        else
        {
            float disc = SALTO * SALTO - 2f * GRAVEDAD * dy; // dy negativo: cae mas
            tiempo = (SALTO + Mathf.Sqrt(disc)) / GRAVEDAD;
        }
        return hueco <= tiempo * VELOCIDAD - 0.6f;
    }
}
