// Code authored by Dean Edis (DeanTheCoder).
// Anyone is free to copy, modify, use, compile, or distribute this software,
// either in source code form or as a compiled binary, for any purpose.
//
// If you modify the code, please retain this copyright header,
// and consider contributing back to the repository or letting us know
// about your modifications. Your contributions are valued!
//
// THE SOFTWARE IS PROVIDED AS IS, WITHOUT WARRANTY OF ANY KIND.

using NUnit.Framework;
using Wolfenshine.Game;
using Wolfenshine.Graphics;

namespace Wolfenshine.Tests.Graphics;

/// <summary>
/// Verifies dynamic weapon and ammo composition in the indexed status bar.
/// </summary>
/// <remarks>
/// Solid synthetic pictures make HUD placement testable without commercial artwork.
/// </remarks>
public sealed class WolfensteinHudGraphicsTests
{
    [Test]
    public void GivenWeaponAndAmmoCheckMatchingPicturesAreComposited()
    {
        var background = CreateGraphic(320, 40, 0);
        var weaponIcons = Enumerable.Range(0, 4).Select(index => CreateGraphic(1, 1, (byte)(10 + index))).ToArray();
        var digits = Enumerable.Range(0, 10).Select(index => CreateGraphic(8, 16, (byte)(20 + index))).ToArray();
        var hud = new WolfensteinHudGraphics(
            background,
            weaponIcons,
            CreateGraphic(1, 1, 1),
            CreateGraphic(8, 16, 2),
            digits,
            CreateGraphic(1, 1, 3));

        var rendered = hud.Render(PlayerWeapon.Chaingun, 42, 1234);

        Assert.Multiple(() =>
        {
            Assert.That(rendered.GetIndex(32 * 8, 8), Is.EqualTo(13));
            Assert.That(rendered.GetIndex(27 * 8, 16), Is.EqualTo(24));
            Assert.That(rendered.GetIndex(28 * 8, 16), Is.EqualTo(22));
            Assert.That(rendered.GetIndex(8 * 8, 16), Is.EqualTo(21));
            Assert.That(rendered.GetIndex(11 * 8, 16), Is.EqualTo(24));
        });
    }

    private static WolfensteinGraphic CreateGraphic(int width, int height, byte index) =>
        new(width, height, Enumerable.Repeat(index, width * height).ToArray());
}
