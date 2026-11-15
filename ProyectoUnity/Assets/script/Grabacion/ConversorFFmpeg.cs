using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using UnityEngine;

namespace Grabacion
{
    /// <summary>
    /// Detecta FFmpeg en el sistema o en StreamingAssets y convierte archivos .avi a .mp4 en segundo plano.
    /// </summary>
    public static class ConversorFFmpeg
    {
        private static string rutaDetectada;
        private static bool yaBuscado;

        public static bool EstaDisponible => !string.IsNullOrEmpty(BuscarEjecutable());

        public static string BuscarEjecutable(bool forzarRebusqueda = false)
        {
            if (yaBuscado && !forzarRebusqueda) return rutaDetectada;
            yaBuscado = true;
            rutaDetectada = null;

            string streaming = Path.Combine(Application.streamingAssetsPath, "ffmpeg.exe");
            if (File.Exists(streaming)) { rutaDetectada = streaming; return rutaDetectada; }

            DirectoryInfo dirApp = Directory.GetParent(Application.dataPath);
            if (dirApp != null)
            {
                string juntoExe = Path.Combine(dirApp.FullName, "ffmpeg.exe");
                if (File.Exists(juntoExe)) { rutaDetectada = juntoExe; return rutaDetectada; }
            }

            string pathEnv = Environment.GetEnvironmentVariable("PATH");
            if (!string.IsNullOrEmpty(pathEnv))
            {
                string[] dirs = pathEnv.Split(Path.PathSeparator);
                for (int i = 0; i < dirs.Length; i++)
                {
                    string d = dirs[i].Trim().Trim('"');
                    if (string.IsNullOrEmpty(d)) continue;
                    string candidato = Path.Combine(d, "ffmpeg.exe");
                    try
                    {
                        if (File.Exists(candidato)) { rutaDetectada = candidato; return rutaDetectada; }
                    }
                    catch
                    {
                        // Ignorar rutas inaccesibles del PATH
                    }
                }
            }

            return null;
        }

        public static void ConvertirEnSegundoPlano(string rutaAvi, bool conservarAvi, Action<bool, string> alTerminar)
        {
            string exe = BuscarEjecutable();
            if (string.IsNullOrEmpty(exe) || string.IsNullOrEmpty(rutaAvi) || !File.Exists(rutaAvi))
            {
                alTerminar?.Invoke(false, rutaAvi);
                return;
            }

            string rutaMp4 = Path.ChangeExtension(rutaAvi, ".mp4");
            ThreadPool.QueueUserWorkItem(_ =>
            {
                try
                {
                    var psi = new ProcessStartInfo
                    {
                        FileName = exe,
                        Arguments = $"-y -i \"{rutaAvi}\" -c:v libx264 -preset fast -crf 18 -pix_fmt yuv420p -c:a aac -b:a 192k -movflags +faststart \"{rutaMp4}\"",
                        CreateNoWindow = true,
                        UseShellExecute = false,
                        RedirectStandardError = true,
                        RedirectStandardOutput = true
                    };

                    using (Process p = Process.Start(psi))
                    {
                        if (p == null)
                        {
                            alTerminar?.Invoke(false, "No se pudo iniciar FFmpeg.");
                            return;
                        }
                        string stderr = p.StandardError.ReadToEnd();
                        p.WaitForExit();

                        bool ok = p.ExitCode == 0 && File.Exists(rutaMp4) && new FileInfo(rutaMp4).Length > 0;
                        if (ok && !conservarAvi)
                        {
                            try { File.Delete(rutaAvi); } catch { }
                        }

                        if (!ok)
                        {
                            UnityEngine.Debug.LogWarning("[ConversorFFmpeg] Error al convertir a MP4:\n" + stderr);
                        }
                        alTerminar?.Invoke(ok, ok ? rutaMp4 : "Error FFmpeg (codigo " + p.ExitCode + ")");
                    }
                }
                catch (Exception ex)
                {
                    UnityEngine.Debug.LogWarning("[ConversorFFmpeg] Excepcion al convertir: " + ex.Message);
                    alTerminar?.Invoke(false, ex.Message);
                }
            });
        }
    }
}
