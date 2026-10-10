namespace SpeechDesk.Core.Audio;

/// <summary>
/// Prepara o áudio do microfone para o reconhecimento: sobe o volume de microfones baixos
/// e acrescenta silêncio nas pontas (os modelos transducer precisam dele para "fechar" a última palavra).
/// </summary>
public static class AudioPreprocessor
{
    private const float TargetPeak = 0.7f;
    private const float MaxGain = 10f;
    /// <summary>Abaixo disto é só ruído: não vale a pena amplificar.</summary>
    private const float SilencePeak = 0.005f;

    public static float[] Prepare(byte[] pcm16, int sampleRate, int leadMs = 250, int tailMs = 500)
    {
        var count = pcm16.Length / 2;
        var lead = sampleRate * leadMs / 1000;
        var tail = sampleRate * tailMs / 1000;
        var samples = new float[lead + count + tail];

        var peak = 0f;
        for (var i = 0; i < count; i++)
        {
            var s = (short)(pcm16[2 * i] | (pcm16[2 * i + 1] << 8)) / 32768f;
            samples[lead + i] = s;
            peak = Math.Max(peak, Math.Abs(s));
        }

        // Só amplifica: um microfone com volume bom não é mexido.
        if (peak > SilencePeak && peak < TargetPeak)
        {
            var gain = Math.Min(MaxGain, TargetPeak / peak);
            for (var i = lead; i < lead + count; i++) samples[i] *= gain;
        }

        return samples;
    }
}
