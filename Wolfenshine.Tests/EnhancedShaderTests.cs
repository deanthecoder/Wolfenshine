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
using SkiaSharp;

namespace Wolfenshine.Tests;

/// <summary>
/// Verifies that the enhanced renderer's SKSL source remains valid.
/// </summary>
[TestFixture]
public sealed class EnhancedShaderTests
{
    [Test]
    public void GivenEnhancedShaderThenItCompiles()
    {
        var shaderPath = Path.Combine(TestContext.CurrentContext.TestDirectory, "Assets", "enhanced.sksl");
        var source = File.ReadAllText(shaderPath);

        using var effect = SKRuntimeEffect.Create(source, out var error);

        Assert.That(effect, Is.Not.Null, error);
    }
}
