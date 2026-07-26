// Code authored by Dean Edis (DeanTheCoder).
// Anyone is free to copy, modify, use, compile, or distribute this software,
// either in source code form or as a compiled binary, for any purpose.
//
// If you modify the code, please retain this copyright header,
// and consider contributing back to the repository or letting us know
// about your modifications. Your contributions are valued!
//
// THE SOFTWARE IS PROVIDED AS IS, WITHOUT WARRANTY OF ANY KIND.

using DTC.Core;
using NukedOPL3Sharp;
using OpenTK.Audio.OpenAL;
using Wolfenshine.Rendering;

namespace Wolfenshine.Audio;

/// <summary>
/// Plays Wolfenstein sound effects through a reusable pool of OpenAL sources.
/// </summary>
/// <remarks>
/// Map positions are transformed into listener-relative coordinates to provide stereo placement and distance attenuation.
/// </remarks>
public sealed class WolfensteinAudioPlayer : IDisposable
{
    private const int SourceCount = 16;
    private const int MusicSampleRate = 44100;
    private const int MusicTicksPerSecond = 700;
    private const float MusicSampleGain = 3.0f;
    private const float MusicGain = 0.75f;
    private static readonly int[] s_mapMusic =
    [
        3, 11, 9, 12, 3, 11, 9, 12, 2, 0,
        8, 18, 17, 4, 8, 18, 4, 17, 2, 1,
        6, 20, 22, 21, 6, 20, 22, 21, 19, 26,
        3, 11, 9, 12, 3, 11, 9, 12, 2, 0,
        8, 18, 17, 4, 8, 18, 4, 17, 2, 1,
        6, 20, 22, 21, 6, 20, 22, 21, 19, 15
    ];
    private readonly ALDevice m_device;
    private readonly ALContext m_context;
    private readonly IReadOnlyDictionary<WolfensteinSoundEffect, int> m_buffers;
    private readonly IReadOnlyList<WolfensteinMusicTrack> m_musicTracks;
    private readonly Dictionary<int, int> m_musicBuffers = [];
    private readonly int[] m_sources;
    private readonly int m_musicSource;
    private int m_nextSource;
    private bool m_isDisposed;

    public WolfensteinAudioPlayer(
        IReadOnlyDictionary<WolfensteinSoundEffect, WolfensteinSound> sounds,
        IReadOnlyList<WolfensteinMusicTrack> musicTracks)
    {
        ArgumentNullException.ThrowIfNull(sounds);
        ArgumentNullException.ThrowIfNull(musicTracks);
        m_device = ALC.OpenDevice(null);
        if (m_device == ALDevice.Null)
            throw new InvalidOperationException("OpenAL could not open an audio device.");
        m_context = ALC.CreateContext(m_device, (int[])null);
        if (m_context == ALContext.Null || !ALC.MakeContextCurrent(m_context))
            throw new InvalidOperationException("OpenAL could not create an audio context.");

        m_buffers = sounds.ToDictionary(pair => pair.Key, pair => CreateBuffer(pair.Value));
        m_musicTracks = musicTracks;
        m_sources = AL.GenSources(SourceCount);
        m_musicSource = AL.GenSource();
        AL.Source(m_musicSource, ALSourceb.SourceRelative, true);
        AL.Source(m_musicSource, ALSourceb.Looping, true);
        foreach (var source in m_sources)
        {
            AL.Source(source, ALSourceb.SourceRelative, true);
            AL.Source(source, ALSourcef.ReferenceDistance, 1.5f);
            AL.Source(source, ALSourcef.MaxDistance, 16.0f);
            AL.Source(source, ALSourcef.RolloffFactor, 0.35f);
        }
        Logger.Instance.Info(
            $"Initialized OpenAL with {m_buffers.Count} sound buffers, {m_musicTracks.Count} music tracks, " +
            $"and {SourceCount} effect sources.");
    }

    public void Play(WolfensteinSoundEvent soundEvent, RaycastCamera listener)
    {
        ArgumentNullException.ThrowIfNull(soundEvent);
        ArgumentNullException.ThrowIfNull(listener);
        if (m_isDisposed || !m_buffers.TryGetValue(soundEvent.Effect, out var buffer))
            return;

        var source = m_sources[m_nextSource++ % m_sources.Length];
        AL.SourceStop(source);
        AL.Source(source, ALSourcei.Buffer, buffer);
        var right = 0.0;
        var forward = 0.0;
        if (soundEvent.X is { } worldX && soundEvent.Y is { } worldY)
        {
            var offsetX = worldX - listener.X;
            var offsetY = worldY - listener.Y;
            right = (-listener.DirectionY * offsetX) + (listener.DirectionX * offsetY);
            forward = (listener.DirectionX * offsetX) + (listener.DirectionY * offsetY);
        }
        AL.Source(source, ALSource3f.Position, (float)right, 0.0f, (float)-forward);
        AL.SourcePlay(source);
    }

    /// <summary>
    /// Starts the original looping music selected for the given map slot.
    /// </summary>
    public void PlayMusic(int mapSlot)
    {
        if (m_isDisposed || mapSlot < 0 || mapSlot >= s_mapMusic.Length)
            return;
        PlayMusicTrack(s_mapMusic[mapSlot]);
    }

    /// <summary>
    /// Starts a looping music sequence by its original music number.
    /// </summary>
    public void PlayMusicTrack(int trackNumber)
    {
        if (m_isDisposed)
            return;
        if (trackNumber >= m_musicTracks.Count)
            return;
        AL.SourceStop(m_musicSource);
        if (!m_musicBuffers.TryGetValue(trackNumber, out var buffer))
        {
            buffer = CreateMusicBuffer(m_musicTracks[trackNumber]);
            m_musicBuffers.Add(trackNumber, buffer);
        }
        AL.Source(m_musicSource, ALSourcei.Buffer, buffer);
        AL.Source(m_musicSource, ALSourcef.Gain, MusicGain);
        AL.SourcePlay(m_musicSource);
        Logger.Instance.Info($"Playing music track {trackNumber}.");
    }

    /// <summary>
    /// Fades the music from full volume at zero to silence at one.
    /// </summary>
    public void SetMusicFade(double fade) =>
        AL.Source(m_musicSource, ALSourcef.Gain, MusicGain * (float)(1.0 - Math.Clamp(fade, 0.0, 1.0)));

    /// <summary>
    /// Pauses or resumes the current music without restarting its sequence.
    /// </summary>
    public void SetPaused(bool isPaused)
    {
        if (m_isDisposed)
            return;
        if (isPaused)
            AL.SourcePause(m_musicSource);
        else
            AL.SourcePlay(m_musicSource);
    }

    public void Dispose()
    {
        if (m_isDisposed)
            return;
        m_isDisposed = true;
        AL.SourceStop(m_musicSource);
        foreach (var source in m_sources)
            AL.SourceStop(source);
        AL.DeleteSource(m_musicSource);
        AL.DeleteSources(m_sources);
        AL.DeleteBuffers(m_buffers.Values.ToArray());
        AL.DeleteBuffers(m_musicBuffers.Values.ToArray());
        ALC.MakeContextCurrent(ALContext.Null);
        ALC.DestroyContext(m_context);
        ALC.CloseDevice(m_device);
    }

    private static int CreateBuffer(WolfensteinSound sound)
    {
        var buffer = AL.GenBuffer();
        AL.BufferData(buffer, ALFormat.Mono8, sound.Samples, sound.SampleRate);
        return buffer;
    }

    private static int CreateMusicBuffer(WolfensteinMusicTrack track)
    {
        var framesPerTick = MusicSampleRate / MusicTicksPerSecond;
        var frameCount = checked(track.Commands.Sum(command => command.Delay) * framesPerTick);
        if (frameCount == 0)
            throw new InvalidDataException("The IMF music sequence contains no timed samples.");
        var samples = new short[checked(frameCount * 2)];
        var chip = new Opl3Chip();
        chip.Reset(MusicSampleRate);
        chip.WriteRegister(0x01, 0x20);
        var destination = 0;
        foreach (var command in track.Commands)
        {
            chip.WriteRegister(command.Register, command.Value);
            var sampleCount = command.Delay * framesPerTick * 2;
            if (sampleCount == 0)
                continue;
            chip.GenerateStream(samples.AsSpan(destination, sampleCount));
            destination += sampleCount;
        }
        ApplyMusicGain(samples);
        var buffer = AL.GenBuffer();
        AL.BufferData(buffer, ALFormat.Stereo16, samples, MusicSampleRate);
        return buffer;
    }

    private static void ApplyMusicGain(Span<short> samples)
    {
        for (var index = 0; index < samples.Length; index++)
        {
            var amplified = samples[index] * MusicSampleGain;
            samples[index] = (short)Math.Clamp(amplified, short.MinValue, short.MaxValue);
        }
    }
}
