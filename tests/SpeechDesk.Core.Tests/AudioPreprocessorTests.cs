using SpeechDesk.Core.Audio;
using Xunit;

namespace SpeechDesk.Core.Tests;

public class AudioPreprocessorTests
{
    private static byte[] Pcm(params short[] samples)
    {
        var bytes = new byte[samples.Length * 2];
        Buffer.BlockCopy(samples, 0, bytes, 0, bytes.Length);
        return bytes;
    }

    [Fact]
    public void Adds_silence_before_and_after()
    {
        var result = AudioPreprocessor.Prepare(Pcm(16000, -16000), sampleRate: 1000, leadMs: 3, tailMs: 5);

        Assert.Equal(3 + 2 + 5, result.Length);
        Assert.All(result[..3], s => Assert.Equal(0f, s));
        Assert.All(result[5..], s => Assert.Equal(0f, s));
    }

    [Fact]
    public void Boosts_quiet_microphone()
    {
        var result = AudioPreprocessor.Prepare(Pcm(5000, -2500),sampleRate: 1000, leadMs: 0, tailMs: 0);

        Assert.Equal(0.7f, result.Max(Math.Abs), precision: 3);
    }

    [Fact]
    public void Gain_is_capped_so_noise_is_not_blown_up()
    {
        var result = AudioPreprocessor.Prepare(Pcm(500), sampleRate: 1000, leadMs: 0, tailMs: 0);

        Assert.Equal(500 / 32768f * 10, result[0], precision: 4);
    }

    [Fact]
    public void Leaves_loud_audio_and_silence_untouched()
    {
        Assert.Equal(30000 / 32768f, AudioPreprocessor.Prepare(Pcm(30000), 1000, 0, 0)[0], precision: 4);
        Assert.Equal(50 / 32768f, AudioPreprocessor.Prepare(Pcm(50), 1000, 0, 0)[0], precision: 6);
    }
}
