using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Media;

namespace Pomidoro.Audio;

// Звук конца фазы. Волна синтезируется в памяти, чтобы не таскать с приложением
// wav-файл и не зависеть от того, какие системные звуки настроены у пользователя.
internal static class Chime
{
    private const int SampleRate = 44100;
    private const double DurationSeconds = 1.1;
    private const double Amplitude = 0.55;
    private const double DecayPerSecond = 3.2;
    private const double AttackSeconds = 0.005;
    private const int WaveHeaderBytes = 44;

    // Основной тон и его обертоны — вместе дают колокольчик, а не писк.
    private static readonly (double Frequency, double Weight)[] Partials =
    [
        (880.0, 0.60),
        (1320.0, 0.28),
        (2640.0, 0.12)
    ];

    // Один проигрыватель на всё время работы: пересоздавать его на каждый сигнал
    // значит оставлять за собой неосвобождённые потоки.
    private static readonly Lazy<SoundPlayer> Player = new(CreatePlayer);

    [SuppressMessage(
        "Design",
        "CA1031:Do not catch general exception types",
        Justification = "Нет звуковой карты или устройство занято — таймер из-за этого падать не должен")]
    public static void Play()
    {
        try
        {
            Player.Value.Play();
        }
        catch (Exception)
        {
            SystemSounds.Asterisk.Play();
        }
    }

    private static SoundPlayer CreatePlayer()
    {
        SoundPlayer player = new(new MemoryStream(BuildWaveFile()));
        player.Load();

        return player;
    }

    private static byte[] BuildWaveFile()
    {
        int sampleCount = (int)(SampleRate * DurationSeconds);
        short[] samples = new short[sampleCount];

        for (int i = 0; i < sampleCount; i++)
        {
            double time = (double)i / SampleRate;

            // Экспоненциальное затухание — звук отзвенивает, а не обрывается щелчком.
            double envelope = Math.Exp(-DecayPerSecond * time);

            // Короткая атака убирает щелчок в самом начале.
            envelope *= Math.Min(1.0, time / AttackSeconds);

            double value = 0.0;
            foreach ((double frequency, double weight) in Partials)
            {
                value += weight * Math.Sin(2.0 * Math.PI * frequency * time);
            }

            samples[i] = (short)(value * envelope * short.MaxValue * Amplitude);
        }

        return WrapInWaveContainer(samples);
    }

    private static byte[] WrapInWaveContainer(short[] samples)
    {
        const short channels = 1;
        const short bitsPerSample = 16;

        int dataBytes = samples.Length * sizeof(short);

        using MemoryStream stream = new(WaveHeaderBytes + dataBytes);
        using BinaryWriter writer = new(stream);

        writer.Write("RIFF"u8);
        writer.Write(WaveHeaderBytes - 8 + dataBytes);
        writer.Write("WAVE"u8);

        writer.Write("fmt "u8);
        writer.Write(16);                                            // размер fmt-блока
        writer.Write((short)1);                                      // PCM
        writer.Write(channels);
        writer.Write(SampleRate);
        writer.Write(SampleRate * channels * bitsPerSample / 8);     // байт в секунду
        writer.Write((short)(channels * bitsPerSample / 8));         // выравнивание блока
        writer.Write(bitsPerSample);

        writer.Write("data"u8);
        writer.Write(dataBytes);

        foreach (short sample in samples)
        {
            writer.Write(sample);
        }

        writer.Flush();

        return stream.ToArray();
    }
}
