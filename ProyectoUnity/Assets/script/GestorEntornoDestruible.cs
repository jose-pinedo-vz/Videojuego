using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

/// <summary>
/// Administra los obstaculos destruibles de cada ronda:
/// 1) Prioriza los ObjetoDestruible colocados directamente dentro del mapa en la escena
///    (permitiendo moverlos, duplicarlos o cambiarles el tipo desde Unity Scene View).
/// 2) Solo si un mapa no tiene obstaculos propios colocados, genera 4-5 sobre suelo interior firme.
/// </summary>
public class GestorEntornoDestruible : MonoBehaviour
{
    public const int MIN_OBSTACULOS = 4;
    public const int MAX_OBSTACULOS = 5;
    private const float DIST_MIN_SPAWN_JUGADOR = 2.25f;
    private const float DIST_MIN_SPAWN_ARMA = 2.0f;
    private const float DIST_MIN_ENTRE_OBSTACULOS = 2.4f;

    public static GestorEntornoDestruible Instancia { get; private set; }

    private readonly List<ObjetoDestruible> obstaculosActivos = new List<ObjetoDestruible>();
    private readonly Dictionary<int, ObjetoDestruible> obstaculosPorId = new Dictionary<int, ObjetoDestruible>();
    private GameObject contenedorObstaculosDinamicos;
    private bool rondaGenerada = false;

    public static GestorEntornoDestruible ObtenerOCrear()
    {
        if (Instancia != null) return Instancia;

        GestorEntornoDestruible existente = FindFirstObjectByType<GestorEntornoDestruible>(FindObjectsInactive.Include);
        if (existente != null)
        {
            Instancia = existente;
            return Instancia;
        }

        GameObject go = new GameObject("GestorEntornoDestruible");
        Instancia = go.AddComponent<GestorEntornoDestruible>();
        return Instancia;
    }

    private void Awake()
    {
        if (Instancia != null && Instancia != this)
        {
            Destroy(gameObject);
            return;
        }
        Instancia = this;
    }

    private IEnumerator Start()
    {
        // Respaldo automatico para escenas de prueba locales (sin GameManager en red)
        yield return new WaitForSeconds(0.35f);
        bool sinRed = NetworkManager.Singleton == null || !NetworkManager.Singleton.IsListening;
        if (sinRed && !rondaGenerada)
        {
            GameObject mapa = BuscarMapaActivoEnEscena();
            if (mapa != null || FindFirstObjectByType<Personaje>() != null)
            {
                IniciarRondaObstaculosServidor(mapa);
            }
        }
    }

    public void LimpiarObstaculos()
    {
        for (int i = 0; i < obstaculosActivos.Count; i++)
        {
            ObjetoDestruible obs = obstaculosActivos[i];
            if (obs != null && obs.GeneradoDinamicamente)
            {
                Destroy(obs.gameObject);
            }
        }
        obstaculosActivos.Clear();
        obstaculosPorId.Clear();

        if (contenedorObstaculosDinamicos != null)
        {
            Destroy(contenedorObstaculosDinamicos);
            contenedorObstaculosDinamicos = null;
        }
        rondaGenerada = false;
    }

    /// <summary>
    /// Prepara los obstaculos del mapa elegido: si el mapa ya tiene ObjetoDestruible en escena los usa
    /// exactamente en sus ubicaciones configuradas; de lo contrario, genera respaldo sobre piso interior.
    /// </summary>
    public void IniciarRondaObstaculosServidor(GameObject mapa)
    {
        Physics.SyncTransforms();

        if (mapa == null) mapa = BuscarMapaActivoEnEscena();

        // 1) Prioridad: usar los obstaculos colocados directamente en el mapa de la escena
        List<ObjetoDestruible> enEscena = RecopilarObstaculosEnMapa(mapa);
        if (enEscena.Count > 0)
        {
            if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
            {
                GameManager gm = FindFirstObjectByType<GameManager>();
                if (gm != null && gm.IsSpawned)
                {
                    gm.SincronizarObstaculosDeEscenaClientRpc();
                    return;
                }
            }

            AplicarObstaculosDeEscenaLocal(mapa);
            return;
        }

        // 2) Respaldo automatico si el mapa aun no tiene ObjetoDestruible colocados manualmente
        List<Vector3> spawnsJugador = new List<Vector3>();
        List<Vector3> spawnsArmas = new List<Vector3>();
        RecopilarSpawns(mapa, spawnsJugador, spawnsArmas);

        float planoZ = CalcularPlanoZ(spawnsJugador, spawnsArmas);
        List<Vector3> candidatos = ExplorarSuperficiesPiso(mapa, spawnsJugador, spawnsArmas, planoZ);
        List<(TipoObstaculoDestruible tipo, Vector3 pos)> elegidos = SeleccionarUbicaciones(candidatos, spawnsJugador, spawnsArmas);

        int[] ids = new int[elegidos.Count];
        int[] tipos = new int[elegidos.Count];
        Vector3[] posiciones = new Vector3[elegidos.Count];
        for (int i = 0; i < elegidos.Count; i++)
        {
            ids[i] = i;
            tipos[i] = (int)elegidos[i].tipo;
            posiciones[i] = elegidos[i].pos;
        }

        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
        {
            GameManager gm = FindFirstObjectByType<GameManager>();
            if (gm != null && gm.IsSpawned)
            {
                gm.SincronizarObstaculosRondaClientRpc(ids, tipos, posiciones);
                return;
            }
        }

        AplicarObstaculosRondaLocal(ids, tipos, posiciones);
    }

    public void AplicarObstaculosDeEscenaLocal(GameObject mapa)
    {
        LimpiarObstaculos();
        if (mapa == null) mapa = BuscarMapaActivoEnEscena();

        List<ObjetoDestruible> enEscena = RecopilarObstaculosEnMapa(mapa);
        for (int i = 0; i < enEscena.Count; i++)
        {
            ObjetoDestruible obs = enEscena[i];
            if (obs == null) continue;
            obs.ConfigurarComoObstaculoEnEscena(i);
            obstaculosActivos.Add(obs);
            obstaculosPorId[i] = obs;
        }

        Physics.SyncTransforms();
        rondaGenerada = true;
    }

    public void AplicarObstaculosRondaLocal(int[] ids, int[] tipos, Vector3[] posiciones)
    {
        LimpiarObstaculos();
        if (ids == null || tipos == null || posiciones == null) return;

        int total = Mathf.Min(ids.Length, Mathf.Min(tipos.Length, posiciones.Length));
        if (total == 0) return;

        contenedorObstaculosDinamicos = new GameObject("ObstaculosDestruiblesRonda");

        for (int i = 0; i < total; i++)
        {
            TipoObstaculoDestruible tipo = tipos[i] == (int)TipoObstaculoDestruible.PlataformaCaja
                ? TipoObstaculoDestruible.PlataformaCaja
                : TipoObstaculoDestruible.ParedCobertura;

            GameObject go = new GameObject($"Obstaculo_{ids[i]}_{tipo}");
            go.transform.SetParent(contenedorObstaculosDinamicos.transform, false);
            go.transform.position = posiciones[i];

            ObjetoDestruible comp = go.AddComponent<ObjetoDestruible>();
            comp.ConfigurarComoObstaculoGenerado(ids[i], tipo);

            obstaculosActivos.Add(comp);
            obstaculosPorId[ids[i]] = comp;
        }

        Physics.SyncTransforms();
        rondaGenerada = true;
    }

    private static List<ObjetoDestruible> RecopilarObstaculosEnMapa(GameObject mapa)
    {
        List<ObjetoDestruible> lista = new List<ObjetoDestruible>();
        if (mapa != null)
        {
            foreach (ObjetoDestruible obs in mapa.GetComponentsInChildren<ObjetoDestruible>(true))
            {
                if (obs != null && !obs.GeneradoDinamicamente)
                {
                    lista.Add(obs);
                }
            }
        }
        else
        {
            foreach (ObjetoDestruible obs in FindObjectsByType<ObjetoDestruible>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            {
                if (obs != null && !obs.GeneradoDinamicamente)
                {
                    lista.Add(obs);
                }
            }
        }
        return lista;
    }

    public static void SolicitarDestruirObstaculo(ObjetoDestruible obstaculo, Vector3 origenImpacto, Vector3 direccionImpacto, bool porExplosion)
    {
        if (obstaculo == null || obstaculo.Destruido) return;

        obstaculo.DestruirLocal(origenImpacto, direccionImpacto, porExplosion);

        if (obstaculo.IdObstaculo < 0) return;
        if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsListening) return;

        GameManager gm = FindFirstObjectByType<GameManager>();
        if (gm == null || !gm.IsSpawned) return;

        if (NetworkManager.Singleton.IsServer)
        {
            gm.DestruirObstaculoClientRpc(obstaculo.IdObstaculo, origenImpacto, direccionImpacto, porExplosion);
        }
        else
        {
            gm.SolicitarDestruirObstaculoServerRpc(obstaculo.IdObstaculo, origenImpacto, direccionImpacto, porExplosion);
        }
    }

    public void AplicarDestruccionRemota(int idObstaculo, Vector3 origenImpacto, Vector3 direccionImpacto, bool porExplosion)
    {
        if (obstaculosPorId.TryGetValue(idObstaculo, out ObjetoDestruible obs) && obs != null)
        {
            obs.DestruirLocal(origenImpacto, direccionImpacto, porExplosion);
        }
    }

    private GameObject BuscarMapaActivoEnEscena()
    {
        inicio menu = FindFirstObjectByType<inicio>();
        if (menu != null && menu.Mapas != null)
        {
            foreach (GameObject m in menu.Mapas)
            {
                if (m != null && m.activeInHierarchy) return m;
            }
        }

        GameObject mapa3d = GameObject.Find("Mapa3D");
        if (mapa3d != null && mapa3d.activeInHierarchy) return mapa3d;

        foreach (GameObject go in FindObjectsByType<GameObject>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
        {
            if (go != null && go.transform.parent == null && go.name.StartsWith("ecena", System.StringComparison.OrdinalIgnoreCase))
                return go;
        }

        return null;
    }

    private void RecopilarSpawns(GameObject mapa, List<Vector3> spawnsJugador, List<Vector3> spawnsArmas)
    {
        if (mapa != null)
        {
            foreach (Transform t in GameManager.BuscarPuntos(mapa, GameManager.PREFIJO_JUGADOR))
            {
                if (t != null) spawnsJugador.Add(t.position);
            }
            foreach (Transform t in GameManager.BuscarPuntos(mapa, GameManager.PREFIJO_ARMA))
            {
                if (t != null) spawnsArmas.Add(t.position);
            }
        }
        else
        {
            foreach (Transform t in FindObjectsByType<Transform>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            {
                if (t == null) continue;
                if (t.name.StartsWith(GameManager.PREFIJO_ARMA, System.StringComparison.OrdinalIgnoreCase))
                    spawnsArmas.Add(t.position);
                else if (t.name.StartsWith(GameManager.PREFIJO_JUGADOR, System.StringComparison.OrdinalIgnoreCase))
                    spawnsJugador.Add(t.position);
            }
        }

        if (spawnsJugador.Count == 0)
        {
            foreach (Personaje p in FindObjectsByType<Personaje>(FindObjectsSortMode.None))
            {
                if (p != null) spawnsJugador.Add(p.transform.position);
            }
        }
    }

    private static float CalcularPlanoZ(List<Vector3> spawnsJugador, List<Vector3> spawnsArmas)
    {
        List<float> zs = new List<float>();
        foreach (Vector3 v in spawnsJugador) zs.Add(v.z);
        if (zs.Count == 0)
        {
            foreach (Vector3 v in spawnsArmas) zs.Add(v.z);
        }
        if (zs.Count == 0)
        {
            Personaje p = FindFirstObjectByType<Personaje>();
            return p != null ? p.transform.position.z : 0f;
        }
        zs.Sort();
        return zs[zs.Count / 2];
    }

    public static List<Vector3> ExplorarSuperficiesPiso(GameObject mapa, List<Vector3> spawnsJugador, List<Vector3> spawnsArmas, float planoZ)
    {
        float minX = float.MaxValue;
        float maxX = float.MinValue;
        float minY = float.MaxValue;
        float maxY = float.MinValue;

        void Expandir(Vector3 v)
        {
            if (Mathf.Abs(v.z - planoZ) > 3.0f) return;
            if (v.x < minX) minX = v.x;
            if (v.x > maxX) maxX = v.x;
            if (v.y < minY) minY = v.y;
            if (v.y > maxY) maxY = v.y;
        }

        foreach (Vector3 s in spawnsJugador) Expandir(s);
        foreach (Vector3 a in spawnsArmas) Expandir(a);

        // Mantener el muestreo estrictamente dentro del rango interior de spawns
        if (minX <= maxX && (maxX - minX) > 3.2f)
        {
            minX += 0.7f;
            maxX -= 0.7f;
        }
        else if (minX > maxX)
        {
            float cx = spawnsJugador.Count > 0 ? spawnsJugador[0].x : 0f;
            float cy = spawnsJugador.Count > 0 ? spawnsJugador[0].y : 1f;
            minX = cx - 5f;
            maxX = cx + 5f;
            minY = cy - 1f;
            maxY = cy + 4f;
        }

        List<float> columnasX = new List<float>();
        const int columnasMuestreo = 26;
        for (int i = 0; i < columnasMuestreo; i++)
        {
            float t = (float)i / (columnasMuestreo - 1);
            columnasX.Add(Mathf.Lerp(minX, maxX, t));
        }

        List<Vector3> todosSpawns = new List<Vector3>(spawnsJugador);
        todosSpawns.AddRange(spawnsArmas);
        for (int i = 0; i < todosSpawns.Count; i++)
        {
            for (int j = i + 1; j < todosSpawns.Count; j++)
            {
                float mx = Mathf.Clamp((todosSpawns[i].x + todosSpawns[j].x) * 0.5f, minX, maxX);
                columnasX.Add(mx);
            }
        }

        List<Vector3> candidatos = new List<Vector3>();
        float yOrigenRaycast = maxY + 1.8f;
        float distRaycast = Mathf.Max(12f, (maxY - minY) + 5.0f);

        foreach (float x in columnasX)
        {
            Vector3 origen = new Vector3(x, yOrigenRaycast, planoZ);
            RaycastHit[] hits = Physics.RaycastAll(origen, Vector3.down, distRaycast, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            if (hits == null || hits.Length == 0) continue;

            System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

            foreach (RaycastHit h in hits)
            {
                if (h.distance <= 0.05f || h.collider == null) continue;
                if (mapa != null && !h.collider.transform.IsChildOf(mapa.transform)) continue;
                string nombreCol = h.collider.name.ToLowerInvariant();
                if (nombreCol.Contains("techo") || nombreCol.Contains("roof") || nombreCol.Contains("poster")) continue;
                if (h.collider.GetComponentInParent<Personaje>() != null) continue;
                if (h.collider.GetComponentInParent<Arma>() != null) continue;
                if (h.collider.GetComponentInParent<ObjetoDestruible>() != null) continue;
                if (h.normal.y < 0.72f) continue;

                float yPiso = h.point.y;
                if (yPiso < minY - 1.2f || yPiso > maxY + 0.5f) continue;

                Vector3 punto = new Vector3(x, yPiso, planoZ);
                if (!ValidarEspacioObstaculo(punto)) continue;

                bool duplicado = false;
                for (int k = 0; k < candidatos.Count; k++)
                {
                    if (DistanciaXY(candidatos[k], punto) < 0.85f)
                    {
                        duplicado = true;
                        break;
                    }
                }
                if (!duplicado) candidatos.Add(punto);
            }
        }

        return candidatos;
    }

    private static bool ValidarEspacioObstaculo(Vector3 puntoPiso)
    {
        RaycastHit[] arriba = Physics.RaycastAll(puntoPiso + Vector3.up * 0.25f, Vector3.up, 2.0f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
        foreach (RaycastHit h in arriba)
        {
            if (h.distance <= 0.05f || h.collider == null) continue;
            if (h.collider.GetComponentInParent<Personaje>() != null) continue;
            if (h.collider.GetComponentInParent<Arma>() != null) continue;
            return false;
        }

        float[] alturasLado = { 0.45f, 1.25f };
        Vector3[] dirs = { Vector3.left, Vector3.right };
        foreach (float h in alturasLado)
        {
            Vector3 centro = puntoPiso + Vector3.up * h;
            foreach (Vector3 d in dirs)
            {
                RaycastHit[] lados = Physics.RaycastAll(centro, d, 0.85f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
                foreach (RaycastHit hit in lados)
                {
                    if (hit.distance <= 0.05f || hit.collider == null) continue;
                    if (hit.collider.GetComponentInParent<Personaje>() != null) continue;
                    if (hit.collider.GetComponentInParent<Arma>() != null) continue;
                    if (hit.normal.y > 0.6f) continue;
                    return false;
                }
            }
        }

        float[] offsetsBase = { -0.55f, 0.55f };
        foreach (float ox in offsetsBase)
        {
            Vector3 probe = puntoPiso + new Vector3(ox, 0.35f, 0f);
            RaycastHit[] baseHits = Physics.RaycastAll(probe, Vector3.down, 0.65f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            bool tieneSoporte = false;
            foreach (RaycastHit bh in baseHits)
            {
                if (bh.distance <= 0.05f || bh.collider == null) continue;
                if (bh.collider.GetComponentInParent<Personaje>() != null) continue;
                if (bh.collider.GetComponentInParent<Arma>() != null) continue;
                if (bh.normal.y >= 0.7f)
                {
                    tieneSoporte = true;
                    break;
                }
            }
            if (!tieneSoporte) return false;
        }

        return true;
    }

    public static List<(TipoObstaculoDestruible tipo, Vector3 pos)> SeleccionarUbicaciones(
        List<Vector3> candidatos,
        List<Vector3> spawnsJugador,
        List<Vector3> spawnsArmas)
    {
        var elegidos = new List<(TipoObstaculoDestruible tipo, Vector3 pos)>();
        if (candidatos.Count == 0) return elegidos;

        List<Vector3> barajados = new List<Vector3>(candidatos);
        for (int i = barajados.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            (barajados[i], barajados[j]) = (barajados[j], barajados[i]);
        }

        int objetivo = Random.Range(MIN_OBSTACULOS, MAX_OBSTACULOS + 1);
        float[] distanciasEntreSi = { DIST_MIN_ENTRE_OBSTACULOS, 1.85f };
        bool empezarConPared = Random.value > 0.5f;

        foreach (float distEntre in distanciasEntreSi)
        {
            foreach (Vector3 cand in barajados)
            {
                if (elegidos.Count >= objetivo) break;

                if (EstaCercaDeAlguno(cand, spawnsJugador, DIST_MIN_SPAWN_JUGADOR)) continue;
                if (EstaCercaDeAlguno(cand, spawnsArmas, DIST_MIN_SPAWN_ARMA)) continue;

                bool cercaDeOtro = false;
                for (int i = 0; i < elegidos.Count; i++)
                {
                    if (DistanciaXY(cand, elegidos[i].pos) < distEntre)
                    {
                        cercaDeOtro = true;
                        break;
                    }
                }
                if (cercaDeOtro) continue;

                bool esPared = empezarConPared ? (elegidos.Count % 2 == 0) : (elegidos.Count % 2 == 1);
                TipoObstaculoDestruible tipo = esPared
                    ? TipoObstaculoDestruible.ParedCobertura
                    : TipoObstaculoDestruible.PlataformaCaja;

                elegidos.Add((tipo, cand));
            }

            if (elegidos.Count >= MIN_OBSTACULOS) break;
        }

        return elegidos;
    }

    private static bool EstaCercaDeAlguno(Vector3 punto, List<Vector3> lista, float distMin)
    {
        float distMinSq = distMin * distMin;
        for (int i = 0; i < lista.Count; i++)
        {
            float dx = punto.x - lista[i].x;
            float dy = punto.y - lista[i].y;
            if ((dx * dx + dy * dy) < distMinSq) return true;
        }
        return false;
    }

    private static float DistanciaXY(Vector3 a, Vector3 b)
    {
        float dx = a.x - b.x;
        float dy = a.y - b.y;
        return Mathf.Sqrt(dx * dx + dy * dy);
    }
}
