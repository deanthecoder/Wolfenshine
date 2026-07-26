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
    private readonly ALDevice m_device;
    private readonly ALContext m_context;
    private readonly IReadOnlyDictionary<WolfensteinSoundEffect, int> m_buffers;
    private readonly int[] m_sources;
    private int m_nextSource;
    private bool m_isDisposed;

    public WolfensteinAudioPlayer(IReadOnlyDictionary<WolfensteinSoundEffect, WolfensteinSound> sounds)
    {
        ArgumentNullException.ThrowIfNull(sounds);
        m_device = ALC.OpenDevice(null);
        if (m_device == ALDevice.Null)
            throw new InvalidOperationException("OpenAL could not open an audio device.");
        m_context = ALC.CreateContext(m_device, (int[])null);
        if (m_context == ALContext.Null || !ALC.MakeContextCurrent(m_context))
            throw new InvalidOperationException("OpenAL could not create an audio context.");

        m_buffers = sounds.ToDictionary(pair => pair.Key, pair => CreateBuffer(pair.Value));
        m_sources = AL.GenSources(SourceCount);
        foreach (var source in m_sources)
        {
            AL.Source(source, ALSourceb.SourceRelative, true);
            AL.Source(source, ALSourcef.ReferenceDistance, 1.5f);
            AL.Source(source, ALSourcef.MaxDistance, 16.0f);
            AL.Source(source, ALSourcef.RolloffFactor, 0.35f);
        }
        Logger.Instance.Info($"Initialized OpenAL with {m_buffers.Count} sound buffers and {SourceCount} sources.");
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

    public void Dispose()
    {
        if (m_isDisposed)
            return;
        m_isDisposed = true;
        foreach (var source in m_sources)
            AL.SourceStop(source);
        AL.DeleteSources(m_sources);
        AL.DeleteBuffers(m_buffers.Values.ToArray());
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
}
