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
using Wolfenshine.Resources;

namespace Wolfenshine.Tests.Resources;

/// <summary>
/// Verifies the Huffman expansion used by the original VGA graphics archive.
/// </summary>
/// <remarks>
/// A tiny synthetic root node keeps this format test independent of commercial game artwork.
/// </remarks>
public sealed class WolfensteinGraphicsLoaderTests
{
    [Test]
    public void GivenLeastSignificantBitFirstCodesCheckHuffmanDataIsExpanded()
    {
        var dictionary = new (ushort Bit0, ushort Bit1)[255];
        dictionary[^1] = ('A', 'B');

        var expanded = WolfensteinGraphicsLoader.ExpandHuffman([0b00000110], dictionary, 4);

        Assert.That(expanded, Is.EqualTo("ABBA"u8.ToArray()));
    }
}
