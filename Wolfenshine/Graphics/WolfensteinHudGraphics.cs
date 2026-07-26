// Code authored by Dean Edis (DeanTheCoder).
// Anyone is free to copy, modify, use, compile, or distribute this software,
// either in source code form or as a compiled binary, for any purpose.
//
// If you modify the code, please retain this copyright header,
// and consider contributing back to the repository or letting us know
// about your modifications. Your contributions are valued!
//
// THE SOFTWARE IS PROVIDED AS IS, WITHOUT WARRANTY OF ANY KIND.

using Wolfenshine.Game;

namespace Wolfenshine.Graphics;

/// <summary>
/// Composes the original indexed status-bar pictures for the current player state.
/// </summary>
/// <remarks>
/// Dynamic composition is infrequent and keeps gameplay state independent of VGAGRAPH chunk numbering.
/// </remarks>
public sealed class WolfensteinHudGraphics
{
    private const int Width = 320;
    private const int Height = 40;
    private readonly WolfensteinGraphic m_background;
    private readonly IReadOnlyList<WolfensteinGraphic> m_weaponIcons;
    private readonly WolfensteinGraphic m_noKey;
    private readonly WolfensteinGraphic m_goldKey;
    private readonly WolfensteinGraphic m_silverKey;
    private readonly WolfensteinGraphic m_blankDigit;
    private readonly IReadOnlyList<WolfensteinGraphic> m_digits;
    private readonly IReadOnlyList<WolfensteinGraphic> m_faces;

    public WolfensteinHudGraphics(
        WolfensteinGraphic background,
        IReadOnlyList<WolfensteinGraphic> weaponIcons,
        WolfensteinGraphic noKey,
        WolfensteinGraphic goldKey,
        WolfensteinGraphic silverKey,
        WolfensteinGraphic blankDigit,
        IReadOnlyList<WolfensteinGraphic> digits,
        IReadOnlyList<WolfensteinGraphic> faces)
    {
        ArgumentNullException.ThrowIfNull(background);
        ArgumentNullException.ThrowIfNull(weaponIcons);
        ArgumentNullException.ThrowIfNull(noKey);
        ArgumentNullException.ThrowIfNull(goldKey);
        ArgumentNullException.ThrowIfNull(silverKey);
        ArgumentNullException.ThrowIfNull(blankDigit);
        ArgumentNullException.ThrowIfNull(digits);
        ArgumentNullException.ThrowIfNull(faces);
        if (background.Width != Width || background.Height != Height)
            throw new ArgumentException("The HUD background must be 320 x 40.", nameof(background));
        if (weaponIcons.Count != 4)
            throw new ArgumentException("The HUD requires four weapon icons.", nameof(weaponIcons));
        if (digits.Count != 10)
            throw new ArgumentException("The HUD requires ten digit pictures.", nameof(digits));
        if (faces.Count != 23)
            throw new ArgumentException("The HUD requires 21 health faces, the dead face, and the chaingun grin.", nameof(faces));
        m_background = background;
        m_weaponIcons = weaponIcons;
        m_noKey = noKey;
        m_goldKey = goldKey;
        m_silverKey = silverKey;
        m_blankDigit = blankDigit;
        m_digits = digits;
        m_faces = faces;
    }

    public WolfensteinGraphic Render(
        PlayerWeapon weapon,
        int ammo,
        int score,
        int health,
        int facePictureIndex = 0,
        int lives = 3,
        bool hasGoldKey = false,
        bool hasSilverKey = false)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(ammo);
        ArgumentOutOfRangeException.ThrowIfNegative(score);
        ArgumentOutOfRangeException.ThrowIfNegative(health);
        ArgumentOutOfRangeException.ThrowIfNegative(lives);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(facePictureIndex, m_faces.Count);
        var indices = CopyIndices(m_background);
        DrawPicture(indices, m_faces[facePictureIndex], 17 * 8, 4);
        DrawPicture(indices, m_weaponIcons[(int)weapon], 32 * 8, 8);
        DrawPicture(indices, hasGoldKey ? m_goldKey : m_noKey, 30 * 8, 4);
        DrawPicture(indices, hasSilverKey ? m_silverKey : m_noKey, 30 * 8, 20);
        DrawNumber(indices, 2, 16, 2, 1);
        DrawNumber(indices, 6, 16, 6, score);
        DrawNumber(indices, 14, 16, 1, lives);
        DrawNumber(indices, 21, 16, 3, health);
        DrawNumber(indices, 27, 16, 2, ammo);
        return new WolfensteinGraphic(Width, Height, indices);
    }

    private static byte[] CopyIndices(WolfensteinGraphic graphic)
    {
        var indices = new byte[graphic.Width * graphic.Height];
        for (var y = 0; y < graphic.Height; y++)
        {
            for (var x = 0; x < graphic.Width; x++)
                indices[(y * graphic.Width) + x] = graphic.GetIndex(x, y);
        }
        return indices;
    }

    private static void DrawPicture(Span<byte> destination, WolfensteinGraphic source, int left, int top)
    {
        if (left < 0 || top < 0 || left + source.Width > Width || top + source.Height > Height)
            throw new InvalidDataException("A status-bar picture lies outside the 320 x 40 HUD.");
        for (var y = 0; y < source.Height; y++)
        {
            for (var x = 0; x < source.Width; x++)
                destination[((top + y) * Width) + left + x] = source.GetIndex(x, y);
        }
    }

    private void DrawNumber(Span<byte> destination, int x, int y, int width, int value)
    {
        var text = value.ToString();
        var sourceIndex = Math.Max(0, text.Length - width);
        for (var position = 0; position < width; position++)
        {
            var padding = position < width - Math.Min(width, text.Length);
            var picture = padding ? m_blankDigit : m_digits[text[sourceIndex++] - '0'];
            DrawPicture(destination, picture, (x + position) * 8, y);
        }
    }
}
