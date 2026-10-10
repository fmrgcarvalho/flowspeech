using System.Text;
using FlowSpeech.Core.Models;

namespace FlowSpeech.Core.Audio;

public static class WavEncoder
{
    /// <summary>Envolve PCM16 mono num contentor WAV (RIFF) — formato que o Whisper e as APIs aceitam.</summary>
    public static byte[] ToWav(AudioClip clip)
    {
        const short channels = 1, bitsPerSample = 16;
        var byteRate = clip.SampleRate * channels * bitsPerSample / 8;
        var blockAlign = (short)(channels * bitsPerSample / 8);

        using var ms = new MemoryStream(44 + clip.Pcm16.Length);
        using var w = new BinaryWriter(ms, Encoding.ASCII);
        w.Write("RIFF"u8);
        w.Write(36 + clip.Pcm16.Length);
        w.Write("WAVE"u8);
        w.Write("fmt "u8);
        w.Write(16);
        w.Write((short)1); // PCM
        w.Write(channels);
        w.Write(clip.SampleRate);
        w.Write(byteRate);
        w.Write(blockAlign);
        w.Write(bitsPerSample);
        w.Write("data"u8);
        w.Write(clip.Pcm16.Length);
        w.Write(clip.Pcm16);
        w.Flush();
        return ms.ToArray();
    }

    /// <summary>RMS normalizado (0..1) de um buffer PCM16 — para o medidor de nível.</summary>
    public static float Rms(ReadOnlySpan<byte> pcm16)
    {
        if (pcm16.Length < 2) return 0;
        double sum = 0;
        var samples = pcm16.Length / 2;
        for (var i = 0; i < samples; i++)
        {
            var sample = (short)(pcm16[2 * i] | (pcm16[2 * i + 1] << 8)) / 32768.0;
            sum += sample * sample;
        }
        return (float)Math.Min(1.0, Math.Sqrt(sum / samples) * 4); // ganho visual
    }
}
