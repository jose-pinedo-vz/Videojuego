using System;
using System.Threading;
using UnityEngine;

namespace Grabacion
{
    /// <summary>
    /// Captura el audio mezclado del AudioListener principal en tiempo real sin silenciar la salida.
    /// </summary>
    public class CapturaAudioGrabacion : MonoBehaviour
    {
        public volatile bool Grabando;
        public volatile bool Pausado;

        private Action<byte[]> receptorAudio;
        private long muestrasTotales;

        public int FrecuenciaMuestreo { get; private set; } = 48000;
        public long MuestrasRecibidas => Interlocked.Read(ref muestrasTotales);

        private void Awake()
        {
            FrecuenciaMuestreo = AudioSettings.outputSampleRate;
            if (FrecuenciaMuestreo <= 0) FrecuenciaMuestreo = 48000;
        }

        public void IniciarCaptura(Action<byte[]> alRecibirPcm)
        {
            FrecuenciaMuestreo = AudioSettings.outputSampleRate;
            if (FrecuenciaMuestreo <= 0) FrecuenciaMuestreo = 48000;
            Interlocked.Exchange(ref muestrasTotales, 0);
            receptorAudio = alRecibirPcm;
            Pausado = false;
            Grabando = true;
        }

        public void DetenerCaptura()
        {
            Grabando = false;
            Pausado = false;
            receptorAudio = null;
        }

        private void OnAudioFilterRead(float[] data, int channels)
        {
            if (!Grabando || Pausado || data == null || channels <= 0) return;

            Action<byte[]> destino = receptorAudio;
            if (destino == null) return;

            int cuadros = data.Length / channels;
            if (cuadros <= 0) return;

            byte[] pcm = new byte[cuadros * 4];
            for (int i = 0; i < cuadros; i++)
            {
                int baseIdx = i * channels;
                float izq = data[baseIdx];
                float der = channels > 1 ? data[baseIdx + 1] : izq;

                short sIzq = ConvertirAInt16(izq);
                short sDer = ConvertirAInt16(der);

                int p = i * 4;
                pcm[p] = (byte)(sIzq & 0xFF);
                pcm[p + 1] = (byte)((sIzq >> 8) & 0xFF);
                pcm[p + 2] = (byte)(sDer & 0xFF);
                pcm[p + 3] = (byte)((sDer >> 8) & 0xFF);
            }

            Interlocked.Add(ref muestrasTotales, cuadros);
            destino(pcm);
        }

        private static short ConvertirAInt16(float muestra)
        {
            if (muestra >= 1f) return short.MaxValue;
            if (muestra <= -1f) return short.MinValue;
            return (short)Mathf.RoundToInt(muestra * short.MaxValue);
        }
    }
}
