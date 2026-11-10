using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.InputSystem;

// Acciones del jugador que se pueden reasignar a otra tecla en "Ajustes de teclado".
public enum Accion
{
    Izquierda,
    Derecha,
    Saltar,
    Deslizar,     // deslizar (en movimiento) / agacharse (quieto)
    Recoger,      // recoger y soltar arma
    Disparar,
    Recargar,
    Secundaria,   // ataque secundario (lanzar machete); el clic derecho del mouse tambien lo hace
    Musica        // silenciar / activar la musica
}

// Configuracion del jugador: volumenes y teclas.
//  - Se lee UNA vez al abrir el juego y se guarda solo cuando el jugador cambia algo (sin hilos ni tareas en segundo plano).
//  - Archivo: <Application.persistentDataPath>/ajustes.json
//      Windows: %USERPROFILE%\AppData\LocalLow\<empresa>\<juego>\ajustes.json
//      Linux:   ~/.config/unity3d/<empresa>/<juego>/ajustes.json
//    Es texto plano (JSON), se puede abrir y editar a mano. Si falta o esta danado se usan los valores por defecto.
//  - Leer una tecla en la partida es un acceso a arreglo (sin busquedas ni reservas de memoria).
public static class AjustesJuego
{
    // ---------------------------------------------------------------- valores actuales
    public const float GANANCIA_MAXIMA_MUSICA = 3.5f; // 100% de musica = 3.5 veces el volumen base de las pistas (antes casi no se oia)
    public const float DEFECTO_GENERAL = 1f;
    public const float DEFECTO_MUSICA = 0.6f;
    public const float DEFECTO_EFECTOS = 1f;

    public static float General { get; private set; } = DEFECTO_GENERAL;  // 0..1, afecta a todo el audio
    public static float Musica { get; private set; } = DEFECTO_MUSICA;    // 0..1
    public static float Efectos { get; private set; } = DEFECTO_EFECTOS;  // 0..1, disparos, pasos, ambiente

    private static readonly int TOTAL = Enum.GetValues(typeof(Accion)).Length;
    private static readonly Key[] porDefecto =
    {
        Key.A,          // Izquierda
        Key.D,          // Derecha
        Key.W,          // Saltar
        Key.LeftShift,  // Deslizar
        Key.C,          // Recoger
        Key.V,          // Disparar
        Key.R,          // Recargar
        Key.F,          // Secundaria
        Key.M           // Musica
    };
    private static Key[] teclas = (Key[])porDefecto.Clone();
    private static string[] nombres = new string[TOTAL]; // texto de cada tecla, se calcula al pedirlo y se borra al cambiar

    private static bool sucio;      // hay cambios sin guardar
    private static bool cargado;

    // ---------------------------------------------------------------- archivo

    [Serializable]
    private class EntradaTecla
    {
        public string accion;
        public string tecla;
    }

    [Serializable]
    private class Datos
    {
        public int version = 1;
        public float volumenGeneral = DEFECTO_GENERAL;
        public float volumenMusica = DEFECTO_MUSICA;
        public float volumenEfectos = DEFECTO_EFECTOS;
        public List<EntradaTecla> teclas = new List<EntradaTecla>();
    }

    private static string Ruta => Path.Combine(Application.persistentDataPath, "ajustes.json");

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void CargarAlIniciar()
    {
        Cargar();
    }

    public static void Cargar()
    {
        if (cargado) return;
        cargado = true;

        try
        {
            if (File.Exists(Ruta))
            {
                Datos d = JsonUtility.FromJson<Datos>(File.ReadAllText(Ruta));
                if (d != null) Aplicar(d);
            }
        }
        catch (Exception e)
        {
            // archivo danado o sin permisos: se juega con los valores por defecto
            Debug.LogWarning("AjustesJuego: no se pudo leer " + Ruta + " (" + e.Message + "), se usan los valores por defecto");
        }

        AplicarVolumenGeneral();
    }

    private static void Aplicar(Datos d)
    {
        General = Limpiar(d.volumenGeneral, DEFECTO_GENERAL);
        Musica = Limpiar(d.volumenMusica, DEFECTO_MUSICA);
        Efectos = Limpiar(d.volumenEfectos, DEFECTO_EFECTOS);

        if (d.teclas == null) return;
        Key[] leidas = (Key[])porDefecto.Clone();
        foreach (EntradaTecla e in d.teclas)
        {
            if (e == null) continue;
            if (!Enum.TryParse(e.accion, true, out Accion accion) || !Enum.IsDefined(typeof(Accion), accion)) continue;
            if (!Enum.TryParse(e.tecla, true, out Key tecla) || !EsTeclaValida(tecla)) continue;
            leidas[(int)accion] = tecla;
        }

        // si el archivo trae dos acciones con la misma tecla (editado a mano), se ignoran las teclas y quedan las de por defecto
        HashSet<Key> usadas = new HashSet<Key>();
        for (int i = 0; i < TOTAL; i++)
        {
            if (!usadas.Add(leidas[i]))
            {
                Debug.LogWarning("AjustesJuego: el archivo repite la tecla " + leidas[i] + ", se usan las teclas por defecto");
                return;
            }
        }

        teclas = leidas;
        LimpiarNombres();
    }

    private static float Limpiar(float v, float respaldo)
    {
        if (float.IsNaN(v) || float.IsInfinity(v)) return respaldo;
        return Mathf.Clamp01(v);
    }

    // Guarda solo si hubo cambios. Escribe a un archivo temporal y lo reemplaza, asi un cierre a medias no deja el JSON roto.
    public static void GuardarSiHuboCambios()
    {
        if (!sucio) return;

        Datos d = new Datos
        {
            volumenGeneral = General,
            volumenMusica = Musica,
            volumenEfectos = Efectos
        };
        for (int i = 0; i < TOTAL; i++)
        {
            d.teclas.Add(new EntradaTecla { accion = ((Accion)i).ToString(), tecla = teclas[i].ToString() });
        }

        try
        {
            string ruta = Ruta;
            string temporal = ruta + ".tmp";
            Directory.CreateDirectory(Path.GetDirectoryName(ruta));
            File.WriteAllText(temporal, JsonUtility.ToJson(d, true));
            if (File.Exists(ruta)) File.Replace(temporal, ruta, null);
            else File.Move(temporal, ruta);
            sucio = false;
        }
        catch (Exception e)
        {
            Debug.LogWarning("AjustesJuego: no se pudo guardar " + Ruta + " (" + e.Message + ")");
        }
    }

    // ---------------------------------------------------------------- volumen

    public static void FijarGeneral(float v)
    {
        v = Limpiar(v, General);
        if (Mathf.Approximately(v, General)) return;
        General = v;
        sucio = true;
        AplicarVolumenGeneral();
    }

    public static void FijarMusica(float v)
    {
        v = Limpiar(v, Musica);
        if (Mathf.Approximately(v, Musica)) return;
        Musica = v;
        sucio = true;
    }

    public static void FijarEfectos(float v)
    {
        v = Limpiar(v, Efectos);
        if (Mathf.Approximately(v, Efectos)) return;
        Efectos = v;
        sucio = true;
    }

    public static void RestablecerVolumen()
    {
        FijarGeneral(DEFECTO_GENERAL);
        FijarMusica(DEFECTO_MUSICA);
        FijarEfectos(DEFECTO_EFECTOS);
    }

    // el volumen general es el del AudioListener: afecta a TODAS las fuentes de audio sin tocar cada una
    private static void AplicarVolumenGeneral()
    {
        AudioListener.volume = General;
    }

    // ---------------------------------------------------------------- teclas

    public static Key Tecla(Accion a)
    {
        return teclas[(int)a];
    }

    // Se usan cada cuadro desde la partida: un acceso al arreglo y al control de la tecla, sin reservar memoria.
    public static bool Presionada(Accion a)
    {
        Keyboard k = Keyboard.current;
        return k != null && k[teclas[(int)a]].isPressed;
    }

    public static bool RecienPresionada(Accion a)
    {
        Keyboard k = Keyboard.current;
        return k != null && k[teclas[(int)a]].wasPressedThisFrame;
    }

    public static bool RecienSoltada(Accion a)
    {
        Keyboard k = Keyboard.current;
        return k != null && k[teclas[(int)a]].wasReleasedThisFrame;
    }

    public static bool EsTeclaValida(Key tecla)
    {
        return tecla != Key.None && tecla != Key.Escape && tecla != Key.IMESelected && Enum.IsDefined(typeof(Key), tecla);
    }

    public static string NombreAccion(Accion a)
    {
        switch (a)
        {
            case Accion.Izquierda: return "Mover a la izquierda";
            case Accion.Derecha: return "Mover a la derecha";
            case Accion.Saltar: return "Saltar";
            case Accion.Deslizar: return "Deslizar / agacharse";
            case Accion.Recoger: return "Recoger / soltar arma";
            case Accion.Disparar: return "Disparar";
            case Accion.Recargar: return "Recargar";
            case Accion.Secundaria: return "Ataque secundario";
            case Accion.Musica: return "Silenciar música";
            default: return a.ToString();
        }
    }

    // Nombre de la tecla tal como aparece en el teclado del jugador (respeta su distribucion: QWERTY, AZERTY...)
    public static string NombreTecla(Accion a)
    {
        int i = (int)a;
        if (nombres[i] != null) return nombres[i];

        string texto = teclas[i].ToString();
        Keyboard k = Keyboard.current;
        if (k != null)
        {
            string visible = k[teclas[i]].displayName;
            if (!string.IsNullOrEmpty(visible)) texto = visible;
        }
        // sin teclado conectado todavia no se guarda en cache para reintentarlo luego
        if (k != null) nombres[i] = texto;
        return texto;
    }

    // Asigna la tecla. Si otra accion ya la usaba, las dos se intercambian y se devuelve cual fue.
    public static bool AsignarTecla(Accion a, Key nueva, out Accion? intercambiada)
    {
        intercambiada = null;
        if (!EsTeclaValida(nueva)) return false;

        int i = (int)a;
        if (teclas[i] == nueva) return true;

        for (int otra = 0; otra < TOTAL; otra++)
        {
            if (otra != i && teclas[otra] == nueva)
            {
                teclas[otra] = teclas[i];
                intercambiada = (Accion)otra;
                break;
            }
        }
        teclas[i] = nueva;
        sucio = true;
        LimpiarNombres();
        return true;
    }

    public static void RestablecerTeclas()
    {
        Array.Copy(porDefecto, teclas, TOTAL);
        sucio = true;
        LimpiarNombres();
    }

    private static void LimpiarNombres()
    {
        for (int i = 0; i < nombres.Length; i++) nombres[i] = null;
    }
}
