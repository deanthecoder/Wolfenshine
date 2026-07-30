// Code authored by Dean Edis (DeanTheCoder).
// Anyone is free to copy, modify, use, compile, or distribute this software,
// either in source code form or as a compiled binary, for any purpose.
//
// If you modify the code, please retain this copyright header,
// and consider contributing back to the repository or letting us know
// about your modifications. Your contributions are valued!
//
// THE SOFTWARE IS PROVIDED AS IS, WITHOUT WARRANTY OF ANY KIND.

namespace Wolfenshine.Rendering;

/// <summary>
/// Adds restrained partial pixel coverage to stair-stepped RGBA sprite silhouettes.
/// </summary>
public static class SpriteOutlineSmoother
{
    private const int BytesPerPixel = 4;
    private const byte PartialCoverageAlpha = 230;

    /// <summary>
    /// Softens convex corners without blurring opaque artwork or extending straight edges.
    /// </summary>
    /// <remarks>
    /// The source must be an unmodified copy of the destination. Reading from that copy prevents
    /// newly generated edge pixels from cascading farther outside the original silhouette.
    /// </remarks>
    public static void AddCornerCoverage(
        ReadOnlySpan<byte> source,
        Span<byte> destination,
        int width,
        int height)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(width);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(height);
        var expectedLength = checked(width * height * BytesPerPixel);
        if (source.Length != expectedLength)
            throw new ArgumentException($"The source buffer must contain exactly {expectedLength} bytes.", nameof(source));
        if (destination.Length != expectedLength)
        {
            throw new ArgumentException(
                $"The destination buffer must contain exactly {expectedLength} bytes.",
                nameof(destination));
        }

        // A transparent pixel completing an opaque 2 x 2 corner receives partial coverage.
        // This targets diagonal stair steps while leaving horizontal and vertical runs crisp.
        for (var y = 1; y < height - 1; y++)
        {
            for (var x = 1; x < width - 1; x++)
            {
                var target = GetOffset(x, y, width);
                if (source[target + 3] != 0)
                    continue;

                var left = GetOffset(x - 1, y, width);
                var right = GetOffset(x + 1, y, width);
                var above = GetOffset(x, y - 1, width);
                var below = GetOffset(x, y + 1, width);
                var hasLeft = IsOpaque(source, left);
                var hasRight = IsOpaque(source, right);
                var hasAbove = IsOpaque(source, above);
                var hasBelow = IsOpaque(source, below);
                if (CountTrue(hasLeft, hasRight, hasAbove, hasBelow) != 2)
                    continue;

                var diagonal = 0;
                if (hasLeft && hasAbove)
                {
                    diagonal = GetOffset(x - 1, y - 1, width);
                }
                else if (hasAbove && hasRight)
                {
                    diagonal = GetOffset(x + 1, y - 1, width);
                }
                else if (hasRight && hasBelow)
                {
                    diagonal = GetOffset(x + 1, y + 1, width);
                }
                else if (hasBelow && hasLeft)
                {
                    diagonal = GetOffset(x - 1, y + 1, width);
                }
                else
                {
                    continue;
                }

                if (!IsOpaque(source, diagonal))
                    continue;

                WriteAverageOpaqueColor(source, destination, target, x, y, width, height);
                destination[target + 3] = PartialCoverageAlpha;
            }
        }
    }

    private static void WriteAverageOpaqueColor(
        ReadOnlySpan<byte> source,
        Span<byte> destination,
        int target,
        int centerX,
        int centerY,
        int width,
        int height)
    {
        // Looking two pixels into the artwork reaches past Wolf3D's commonly grey silhouette
        // pixels, allowing the generated coverage to retain nearby weapon and hand colors.
        var totalRed = 0;
        var totalGreen = 0;
        var totalBlue = 0;
        var sampleCount = 0;
        for (var y = Math.Max(0, centerY - 2); y <= Math.Min(height - 1, centerY + 2); y++)
        {
            for (var x = Math.Max(0, centerX - 2); x <= Math.Min(width - 1, centerX + 2); x++)
            {
                var sample = GetOffset(x, y, width);
                if (!IsOpaque(source, sample))
                    continue;

                totalRed += source[sample];
                totalGreen += source[sample + 1];
                totalBlue += source[sample + 2];
                sampleCount++;
            }
        }

        destination[target] = (byte)(totalRed / sampleCount);
        destination[target + 1] = (byte)(totalGreen / sampleCount);
        destination[target + 2] = (byte)(totalBlue / sampleCount);
    }

    private static int GetOffset(int x, int y, int width) => ((y * width) + x) * BytesPerPixel;

    private static bool IsOpaque(ReadOnlySpan<byte> pixels, int offset) => pixels[offset + 3] == byte.MaxValue;

    private static int CountTrue(bool first, bool second, bool third, bool fourth)
        => (first ? 1 : 0) + (second ? 1 : 0) + (third ? 1 : 0) + (fourth ? 1 : 0);
}
