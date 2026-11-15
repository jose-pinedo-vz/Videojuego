using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;

namespace Grabacion
{
    /// <summary>
    /// Escritor nativo en C# para archivos RIFF .avi (Video MJPEG + Audio PCM estereo 16-bit).
    /// Opera en un hilo dedicado para no bloquear el hilo principal del juego y mantiene sincronia A/V.
    /// </summary>
    public sealed class EscritorAVI : IDisposable
    {
        private const long LIMITE_TAMANO_RIFF = 1_850_000_000L; // margen seguro debajo del limite 2GB RIFF
        private const int MAX_COPIAS_SINCRONIA = 8;

        private struct EntradaCola
        {
            public bool EsAudio;
            public byte[] Datos;
        }

        private struct IndiceChunk
        {
            public uint FourCC;
            public uint Flags;
            public uint Offset;
            public uint Tamano;
        }

        private readonly string rutaArchivo;
        private readonly int ancho;
        private readonly int alto;
        private readonly int fps;
        private readonly int sampleRate;

        private readonly FileStream fs;
        private readonly BinaryWriter bw;
        private readonly BlockingCollection<EntradaCola> cola = new BlockingCollection<EntradaCola>(512);
        private readonly Thread hiloEscritura;
        private readonly List<IndiceChunk> indices = new List<IndiceChunk>(4096);
        private readonly Stopwatch relojSinPausa = new Stopwatch();

        private long offsetMoviTipo;
        private long offsetMoviSize;
        private long offsetRiffSize;
        private long offsetAvihTotalFrames;
        private long offsetStrhVideoLength;
        private long offsetStrhAudioLength;

        private long framesEscritos;
        private long muestrasAudioEscritas;
        private byte[] ultimoFrameJpg;
        private volatile bool pausado;
        private volatile bool limiteAlcanzado;
        private volatile bool cerrado;
        private readonly object lockFinal = new object();

        public string RutaArchivo => rutaArchivo;
        public long FramesEscritos => Interlocked.Read(ref framesEscritos);
        public bool LimiteTamanoAlcanzado => limiteAlcanzado;
        public double DuracionSegundos => fps > 0 ? Interlocked.Read(ref framesEscritos) / (double)fps : 0.0;

        public EscritorAVI(string rutaDestino, int anchoVideo, int altoVideo, int framesPorSegundo, int frecuenciaAudio)
        {
            rutaArchivo = rutaDestino;
            ancho = Math.Max(16, anchoVideo);
            alto = Math.Max(16, altoVideo);
            fps = Math.Max(1, framesPorSegundo);
            sampleRate = Math.Max(8000, frecuenciaAudio);

            string dir = Path.GetDirectoryName(rutaDestino);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);

            fs = new FileStream(rutaDestino, FileMode.Create, FileAccess.Write, FileShare.Read, 1 << 16);
            bw = new BinaryWriter(fs, Encoding.ASCII, leaveOpen: true);

            EscribirEncabezadoInicial();
            relojSinPausa.Start();

            hiloEscritura = new Thread(BucleEscritura)
            {
                IsBackground = true,
                Name = "Grabacion_EscritorAVI"
            };
            hiloEscritura.Start();
        }

        public void EstablecerPausa(bool enPausa)
        {
            pausado = enPausa;
            if (enPausa) relojSinPausa.Stop();
            else if (!cerrado) relojSinPausa.Start();
        }

        public void EncolarVideo(byte[] datosJpg)
        {
            if (cerrado || limiteAlcanzado || datosJpg == null || datosJpg.Length == 0) return;
            cola.TryAdd(new EntradaCola { EsAudio = false, Datos = datosJpg });
        }

        public void EncolarAudio(byte[] datosPcm)
        {
            if (cerrado || limiteAlcanzado || datosPcm == null || datosPcm.Length == 0) return;
            cola.TryAdd(new EntradaCola { EsAudio = true, Datos = datosPcm });
        }

        private void BucleEscritura()
        {
            try
            {
                foreach (EntradaCola item in cola.GetConsumingEnumerable())
                {
                    if (fs.Position >= LIMITE_TAMANO_RIFF)
                    {
                        limiteAlcanzado = true;
                        continue;
                    }

                    if (item.EsAudio)
                    {
                        EscribirChunkMovi(0x62773130, false, item.Datos); // "01wb"
                        muestrasAudioEscritas += item.Datos.Length / 4;
                        SincronizarVideoConRelojAudio();
                    }
                    else
                    {
                        ultimoFrameJpg = item.Datos;
                        double tObjetivo = CalcularTiempoObjetivo();
                        long frameObjetivo = Math.Max(1L, (long)Math.Round(tObjetivo * fps));

                        if (framesEscritos < frameObjetivo)
                        {
                            EscribirFrameVideo(ultimoFrameJpg);
                            SincronizarVideoConRelojAudio();
                        }
                        else if (framesEscritos == 0)
                        {
                            EscribirFrameVideo(ultimoFrameJpg);
                        }
                    }
                }

                // Rellenar ultimo ajuste al cerrar si hay un cuadro pendiente respecto al audio
                if (ultimoFrameJpg != null && muestrasAudioEscritas > 0)
                {
                    double tAudio = muestrasAudioEscritas / (double)sampleRate;
                    long frameObjetivo = (long)Math.Round(tAudio * fps);
                    int extra = 0;
                    while (framesEscritos < frameObjetivo && extra < MAX_COPIAS_SINCRONIA && fs.Position < LIMITE_TAMANO_RIFF)
                    {
                        EscribirFrameVideo(ultimoFrameJpg);
                        extra++;
                    }
                }
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogError("[EscritorAVI] Error en hilo de escritura: " + ex.Message);
            }
        }

        private double CalcularTiempoObjetivo()
        {
            if (muestrasAudioEscritas > sampleRate / 10)
            {
                return muestrasAudioEscritas / (double)sampleRate;
            }
            return relojSinPausa.Elapsed.TotalSeconds;
        }

        private void SincronizarVideoConRelojAudio()
        {
            if (ultimoFrameJpg == null) return;
            double tObjetivo = CalcularTiempoObjetivo();
            long frameObjetivo = (long)Math.Floor(tObjetivo * fps);
            int copias = 0;
            while (framesEscritos < frameObjetivo && copias < MAX_COPIAS_SINCRONIA && fs.Position < LIMITE_TAMANO_RIFF)
            {
                EscribirFrameVideo(ultimoFrameJpg);
                copias++;
            }
        }

        private void EscribirFrameVideo(byte[] jpg)
        {
            EscribirChunkMovi(0x63643030, true, jpg); // "00dc"
            Interlocked.Increment(ref framesEscritos);
        }

        private void EscribirChunkMovi(uint fourCC, bool esKeyframe, byte[] datos)
        {
            long posInicioChunk = fs.Position;
            uint offsetRelativoMovi = (uint)(posInicioChunk - offsetMoviTipo);

            bw.Write(fourCC);
            bw.Write((uint)datos.Length);
            bw.Write(datos);
            if ((datos.Length & 1) != 0) bw.Write((byte)0);

            indices.Add(new IndiceChunk
            {
                FourCC = fourCC,
                Flags = esKeyframe ? 0x10u : 0x0u, // AVIIF_KEYFRAME
                Offset = offsetRelativoMovi,
                Tamano = (uint)datos.Length
            });
        }

        private void EscribirEncabezadoInicial()
        {
            EscribirFourCC("RIFF");
            offsetRiffSize = fs.Position;
            bw.Write(0u);
            EscribirFourCC("AVI ");

            // LIST hdrl
            EscribirFourCC("LIST");
            long offsetHdrlSize = fs.Position;
            bw.Write(0u);
            long inicioHdrl = fs.Position;
            EscribirFourCC("hdrl");

            // avih (56 bytes)
            EscribirFourCC("avih");
            bw.Write(56u);
            bw.Write((uint)(1_000_000 / fps)); // dwMicroSecPerFrame
            bw.Write(20_000_000u);            // dwMaxBytesPerSec
            bw.Write(0u);                     // dwPaddingGranularity
            bw.Write(0x00000110u);            // AVIF_HASINDEX | AVIF_ISINTERLEAVED
            offsetAvihTotalFrames = fs.Position;
            bw.Write(0u);                     // dwTotalFrames
            bw.Write(0u);                     // dwInitialFrames
            bw.Write(2u);                     // dwStreams (video + audio)
            bw.Write((uint)(ancho * alto * 3));
            bw.Write((uint)ancho);
            bw.Write((uint)alto);
            bw.Write(0u); bw.Write(0u); bw.Write(0u); bw.Write(0u);

            // LIST strl Video
            EscribirFourCC("LIST");
            long offsetStrlVidSize = fs.Position;
            bw.Write(0u);
            long inicioStrlVid = fs.Position;
            EscribirFourCC("strl");

            // strh Video (56 bytes)
            EscribirFourCC("strh");
            bw.Write(56u);
            EscribirFourCC("vids");
            EscribirFourCC("MJPG");
            bw.Write(0u);                     // dwFlags
            bw.Write((ushort)0);              // wPriority
            bw.Write((ushort)0);              // wLanguage
            bw.Write(0u);                     // dwInitialFrames
            bw.Write(1u);                     // dwScale
            bw.Write((uint)fps);              // dwRate
            bw.Write(0u);                     // dwStart
            offsetStrhVideoLength = fs.Position;
            bw.Write(0u);                     // dwLength
            bw.Write((uint)(ancho * alto * 3));
            bw.Write(0xFFFFFFFFu);            // dwQuality
            bw.Write(0u);                     // dwSampleSize
            bw.Write((short)0); bw.Write((short)0);
            bw.Write((short)ancho); bw.Write((short)alto);

            // strf Video BITMAPINFOHEADER (40 bytes)
            EscribirFourCC("strf");
            bw.Write(40u);
            bw.Write(40u);
            bw.Write(ancho);
            bw.Write(alto);
            bw.Write((ushort)1);
            bw.Write((ushort)24);
            EscribirFourCC("MJPG");
            bw.Write((uint)(ancho * alto * 3));
            bw.Write(0); bw.Write(0);
            bw.Write(0u); bw.Write(0u);
            ActualizarTamanoEn(offsetStrlVidSize, fs.Position - inicioStrlVid);

            // LIST strl Audio
            EscribirFourCC("LIST");
            long offsetStrlAudSize = fs.Position;
            bw.Write(0u);
            long inicioStrlAud = fs.Position;
            EscribirFourCC("strl");

            // strh Audio (56 bytes)
            EscribirFourCC("strh");
            bw.Write(56u);
            EscribirFourCC("auds");
            bw.Write(0u);                     // PCM sin fccHandler
            bw.Write(0u);                     // dwFlags
            bw.Write((ushort)0);              // wPriority
            bw.Write((ushort)0);              // wLanguage
            bw.Write(0u);                     // dwInitialFrames
            bw.Write(4u);                     // dwScale = nBlockAlign (2 ch * 2 bytes)
            bw.Write((uint)(sampleRate * 4)); // dwRate = nAvgBytesPerSec
            bw.Write(0u);                     // dwStart
            offsetStrhAudioLength = fs.Position;
            bw.Write(0u);                     // dwLength = muestras estereo
            bw.Write((uint)(sampleRate * 4));
            bw.Write(0u);
            bw.Write(4u);                     // dwSampleSize
            bw.Write((short)0); bw.Write((short)0);
            bw.Write((short)0); bw.Write((short)0);

            // strf Audio WAVEFORMATEX (18 bytes)
            EscribirFourCC("strf");
            bw.Write(18u);
            bw.Write((ushort)1);              // WAVE_FORMAT_PCM
            bw.Write((ushort)2);              // nChannels
            bw.Write((uint)sampleRate);       // nSamplesPerSec
            bw.Write((uint)(sampleRate * 4)); // nAvgBytesPerSec
            bw.Write((ushort)4);              // nBlockAlign
            bw.Write((ushort)16);             // wBitsPerSample
            bw.Write((ushort)0);              // cbSize
            ActualizarTamanoEn(offsetStrlAudSize, fs.Position - inicioStrlAud);

            ActualizarTamanoEn(offsetHdrlSize, fs.Position - inicioHdrl);

            // LIST movi
            EscribirFourCC("LIST");
            offsetMoviSize = fs.Position;
            bw.Write(0u);
            offsetMoviTipo = fs.Position;
            EscribirFourCC("movi");
        }

        public void Finalizar()
        {
            lock (lockFinal)
            {
                if (cerrado) return;
                cerrado = true;
                relojSinPausa.Stop();
            }

            cola.CompleteAdding();
            if (hiloEscritura != null && hiloEscritura.IsAlive)
            {
                hiloEscritura.Join(8000);
            }

            try
            {
                long finMovi = fs.Position;
                ActualizarTamanoEn(offsetMoviSize, finMovi - offsetMoviTipo);

                // idx1
                EscribirFourCC("idx1");
                bw.Write((uint)(indices.Count * 16));
                for (int i = 0; i < indices.Count; i++)
                {
                    IndiceChunk c = indices[i];
                    bw.Write(c.FourCC);
                    bw.Write(c.Flags);
                    bw.Write(c.Offset);
                    bw.Write(c.Tamano);
                }

                long tamanoTotal = fs.Position;
                ActualizarTamanoEn(offsetAvihTotalFrames, Interlocked.Read(ref framesEscritos));
                ActualizarTamanoEn(offsetStrhVideoLength, Interlocked.Read(ref framesEscritos));
                ActualizarTamanoEn(offsetStrhAudioLength, muestrasAudioEscritas);
                ActualizarTamanoEn(offsetRiffSize, tamanoTotal - 8);
                bw.Flush();
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogError("[EscritorAVI] Error al finalizar encabezado AVI: " + ex.Message);
            }
            finally
            {
                bw.Dispose();
                fs.Dispose();
                cola.Dispose();
            }
        }

        private void EscribirFourCC(string s)
        {
            bw.Write((byte)s[0]);
            bw.Write((byte)s[1]);
            bw.Write((byte)s[2]);
            bw.Write((byte)s[3]);
        }

        private void ActualizarTamanoEn(long offset, long tamano)
        {
            long actual = fs.Position;
            fs.Position = offset;
            bw.Write((uint)Math.Max(0, tamano));
            fs.Position = actual;
        }

        public void Dispose()
        {
            Finalizar();
        }
    }
}
