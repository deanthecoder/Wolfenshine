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
using Wolfenshine.Resources;

namespace Wolfenshine.Audio;

/// <summary>
/// Decodes the original digitized and AdLib sound effects into mono PCM.
/// </summary>
/// <remarks>
/// Digitized samples are preferred, with OPL emulation rendering effects absent from the VSWAP sound map.
/// </remarks>
public static class WolfensteinSoundLoader
{
    private const int DigitizedSampleRate = 7042;
    private const int AdLibSampleRate = 44100;
    private const int SoundTicksPerSecond = 140;
    private const int AdLibSoundStart = 87;

    private static readonly IReadOnlyDictionary<WolfensteinSoundEffect, int> s_digitizedSounds =
        new Dictionary<WolfensteinSoundEffect, int>
        {
            [WolfensteinSoundEffect.CloseDoor] = 2,
            [WolfensteinSoundEffect.OpenDoor] = 3,
            [WolfensteinSoundEffect.AttackMachineGun] = 4,
            [WolfensteinSoundEffect.AttackPistol] = 5,
            [WolfensteinSoundEffect.AttackGatling] = 6,
            [WolfensteinSoundEffect.GuardAlert] = 0,
            [WolfensteinSoundEffect.DogAlert] = 1,
            [WolfensteinSoundEffect.SsAlert] = 7,
            [WolfensteinSoundEffect.GuardDeath1] = 12,
            [WolfensteinSoundEffect.GuardDeath2] = 13,
            [WolfensteinSoundEffect.GuardDeath3] = 13,
            [WolfensteinSoundEffect.PushWall] = 15,
            [WolfensteinSoundEffect.DogDeath] = 16,
            [WolfensteinSoundEffect.MutantDeath] = 17,
            [WolfensteinSoundEffect.SsDeath] = 20,
            [WolfensteinSoundEffect.LevelDone] = 30,
            [WolfensteinSoundEffect.OfficerAlert] = 27,
            [WolfensteinSoundEffect.OfficerDeath] = 28,
            [WolfensteinSoundEffect.GuardDeath4] = 34,
            [WolfensteinSoundEffect.GuardDeath5] = 35,
            [WolfensteinSoundEffect.GuardDeath7] = 40,
            [WolfensteinSoundEffect.GuardDeath8] = 41,
            [WolfensteinSoundEffect.GuardDeath9] = 42,
            [WolfensteinSoundEffect.GuardFire] = 21,
            [WolfensteinSoundEffect.SsFire] = 11,
            [WolfensteinSoundEffect.DogAttack] = 29
        };

    /// <summary>
    /// Decodes gameplay sounds on a worker thread so OPL rendering does not delay the UI.
    /// </summary>
    public static Task<IReadOnlyDictionary<WolfensteinSoundEffect, WolfensteinSound>> LoadAsync(
        WolfensteinResources resources,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(resources);
        return Task.Run(() => Load(resources), cancellationToken);
    }

    public static IReadOnlyDictionary<WolfensteinSoundEffect, WolfensteinSound> Load(WolfensteinResources resources)
    {
        ArgumentNullException.ThrowIfNull(resources);
        var sounds = LoadAdLibSounds(resources);
        var digitized = LoadDigitizedSounds(resources);
        foreach (var (effect, soundNumber) in s_digitizedSounds)
        {
            if (digitized.TryGetValue(soundNumber, out var sound))
                sounds[effect] = sound;
        }
        Logger.Instance.Info($"Loaded {sounds.Count} gameplay sound effects.");
        return sounds;
    }

    private static Dictionary<WolfensteinSoundEffect, WolfensteinSound> LoadAdLibSounds(
        WolfensteinResources resources)
    {
        using var headerReader = new BinaryReader(resources.OpenRead(WolfensteinResourceKind.AudioHeader));
        var offsets = new uint[headerReader.BaseStream.Length / sizeof(uint)];
        for (var index = 0; index < offsets.Length; index++)
            offsets[index] = headerReader.ReadUInt32();
        using var dataReader = new BinaryReader(resources.OpenRead(WolfensteinResourceKind.AudioData));
        var result = new Dictionary<WolfensteinSoundEffect, WolfensteinSound>();
        foreach (var effect in Enum.GetValues<WolfensteinSoundEffect>().Distinct())
        {
            var soundIndex = AdLibSoundStart + (int)effect;
            var start = offsets[soundIndex];
            var end = offsets[soundIndex + 1];
            if (end <= start || start > dataReader.BaseStream.Length - 23)
                continue;
            dataReader.BaseStream.Position = start;
            var length = dataReader.ReadUInt32();
            dataReader.ReadUInt16(); // Priority.
            var instrument = dataReader.ReadBytes(16);
            var block = dataReader.ReadByte();
            if (length > end - start - 23 || length > int.MaxValue)
                throw new InvalidDataException($"AdLib sound {soundIndex} has an invalid length.");
            result[effect] = RenderAdLib(instrument, block, dataReader.ReadBytes((int)length));
        }
        return result;
    }

    private static WolfensteinSound RenderAdLib(byte[] instrument, byte block, byte[] tones)
    {
        const int modifier = 0;
        const int carrier = 3;
        var chip = new Opl3Chip();
        chip.Reset(AdLibSampleRate);
        chip.WriteRegister(0x01, 0x20); // Enable the OPL2 waveform-select registers, as Wolf3D does at startup.
        WriteOperator(chip, modifier, instrument, 0);
        WriteOperator(chip, carrier, instrument, 1);
        chip.WriteRegister(0xc0, 0);

        var framesPerTick = AdLibSampleRate / SoundTicksPerSecond;
        var releaseFrames = AdLibSampleRate / 5;
        var samples = new byte[(tones.Length * framesPerTick) + releaseFrames];
        var destination = 0;
        var stereo = new short[framesPerTick * 2];
        foreach (var tone in tones)
        {
            if (tone == 0)
                chip.WriteRegister(0xb0, 0);
            else
            {
                chip.WriteRegister(0xa0, tone);
                chip.WriteRegister(0xb0, (byte)(((block & 7) << 2) | 0x20));
            }
            chip.GenerateStream(stereo);
            ConvertToMono8(stereo, samples.AsSpan(destination, framesPerTick));
            destination += framesPerTick;
        }
        chip.WriteRegister(0xb0, 0);
        stereo = new short[releaseFrames * 2];
        chip.GenerateStream(stereo);
        ConvertToMono8(stereo, samples.AsSpan(destination));
        ApplyGain(samples, 4.0);
        return new WolfensteinSound(samples, AdLibSampleRate);
    }

    private static void WriteOperator(Opl3Chip chip, int registerOffset, byte[] instrument, int fieldOffset)
    {
        chip.WriteRegister((ushort)(0x20 + registerOffset), instrument[fieldOffset]);
        chip.WriteRegister((ushort)(0x40 + registerOffset), instrument[fieldOffset + 2]);
        chip.WriteRegister((ushort)(0x60 + registerOffset), instrument[fieldOffset + 4]);
        chip.WriteRegister((ushort)(0x80 + registerOffset), instrument[fieldOffset + 6]);
        chip.WriteRegister((ushort)(0xe0 + registerOffset), instrument[fieldOffset + 8]);
    }

    private static void ConvertToMono8(ReadOnlySpan<short> stereo, Span<byte> mono)
    {
        for (var frame = 0; frame < mono.Length; frame++)
        {
            var mixed = (stereo[frame * 2] + stereo[(frame * 2) + 1]) / 2;
            mono[frame] = (byte)Math.Clamp(128 + (mixed >> 8), byte.MinValue, byte.MaxValue);
        }
    }

    private static void ApplyGain(Span<byte> samples, double gain)
    {
        for (var index = 0; index < samples.Length; index++)
        {
            var amplified = 128 + ((samples[index] - 128) * gain);
            samples[index] = (byte)Math.Clamp(amplified, byte.MinValue, byte.MaxValue);
        }
    }

    private static Dictionary<int, WolfensteinSound> LoadDigitizedSounds(WolfensteinResources resources)
    {
        using var reader = new BinaryReader(resources.OpenRead(WolfensteinResourceKind.SwapData));
        var pageCount = reader.ReadUInt16();
        reader.ReadUInt16();
        var soundStart = reader.ReadUInt16();
        var offsets = Enumerable.Range(0, pageCount).Select(_ => reader.ReadUInt32()).ToArray();
        var lengths = Enumerable.Range(0, pageCount).Select(_ => reader.ReadUInt16()).ToArray();
        var infoPage = ReadPage(reader, offsets, lengths, pageCount - 1);
        var result = new Dictionary<int, WolfensteinSound>();
        for (var sound = 0; sound * 4 <= infoPage.Length - 4; sound++)
        {
            var relativePage = BitConverter.ToUInt16(infoPage, sound * 4);
            var byteLength = BitConverter.ToUInt16(infoPage, (sound * 4) + 2);
            if (byteLength == 0 || soundStart + relativePage >= pageCount - 1)
                break;
            var samples = new byte[byteLength];
            var destination = 0;
            for (var page = soundStart + relativePage; destination < samples.Length && page < pageCount - 1; page++)
            {
                var data = ReadPage(reader, offsets, lengths, page);
                var count = Math.Min(data.Length, samples.Length - destination);
                data.AsSpan(0, count).CopyTo(samples.AsSpan(destination));
                destination += count;
            }
            if (destination != samples.Length)
                throw new InvalidDataException($"Digitized sound {sound} ends before its declared length.");
            result[sound] = new WolfensteinSound(samples, DigitizedSampleRate);
        }
        return result;
    }

    private static byte[] ReadPage(BinaryReader reader, uint[] offsets, ushort[] lengths, int page)
    {
        var offset = offsets[page];
        var length = lengths[page];
        if (length == 0 || offset > reader.BaseStream.Length - length)
            throw new InvalidDataException($"VSWAP sound page {page} is empty or outside the file.");
        reader.BaseStream.Position = offset;
        return reader.ReadBytes(length);
    }
}
