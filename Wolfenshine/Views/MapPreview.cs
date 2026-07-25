// Code authored by Dean Edis (DeanTheCoder).
// Anyone is free to copy, modify, use, compile, or distribute this software,
// either in source code form or as a compiled binary, for any purpose.
//
// If you modify the code, please retain this copyright header,
// and consider contributing back to the repository or letting us know
// about your modifications. Your contributions are valued!
//
// THE SOFTWARE IS PROVIDED AS IS, WITHOUT WARRANTY OF ANY KIND.

using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Wolfenshine.Maps;

namespace Wolfenshine.Views;

/// <summary>
/// Draws a diagnostic overhead view of a decoded Wolfenstein 3D map.
/// </summary>
/// <remarks>
/// The preview proves the map pipeline before the software raycaster is available and is not part of the final renderer.
/// </remarks>
public sealed class MapPreview : Control
{
    private static readonly IBrush s_floorBrush = new SolidColorBrush(Color.Parse("#171A1F"));
    private static readonly IBrush s_doorBrush = new SolidColorBrush(Color.Parse("#C49A5A"));
    private static readonly IBrush s_objectBrush = new SolidColorBrush(Color.Parse("#D96666"));
    private static readonly IBrush s_playerBrush = new SolidColorBrush(Color.Parse("#7BD88F"));
    private static readonly IBrush[] s_wallBrushes =
    [
        new SolidColorBrush(Color.Parse("#7A8391")),
        new SolidColorBrush(Color.Parse("#65758A")),
        new SolidColorBrush(Color.Parse("#7D6E66")),
        new SolidColorBrush(Color.Parse("#6E7F72"))
    ];

    public static readonly StyledProperty<WolfensteinMap> MapProperty =
        AvaloniaProperty.Register<MapPreview, WolfensteinMap>(nameof(Map));

    static MapPreview() => AffectsRender<MapPreview>(MapProperty);

    public WolfensteinMap Map
    {
        get => GetValue(MapProperty);
        set => SetValue(MapProperty, value);
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);
        context.FillRectangle(s_floorBrush, Bounds);
        if (Map == null)
            return;

        var tileSize = Math.Min(Bounds.Width / Map.Width, Bounds.Height / Map.Height);
        var mapWidth = tileSize * Map.Width;
        var mapHeight = tileSize * Map.Height;
        var originX = (Bounds.Width - mapWidth) * 0.5;
        var originY = (Bounds.Height - mapHeight) * 0.5;
        var objectSize = Math.Max(1.0, tileSize * 0.45);
        var objectInset = (tileSize - objectSize) * 0.5;

        for (var y = 0; y < Map.Height; y++)
        {
            for (var x = 0; x < Map.Width; x++)
            {
                var wall = Map.GetWall(x, y);
                var left = originX + (x * tileSize);
                var top = originY + (y * tileSize);
                if (wall > 0 && wall < 107)
                {
                    var brush = wall is >= 90 and <= 101
                        ? s_doorBrush
                        : s_wallBrushes[wall % s_wallBrushes.Length];
                    context.FillRectangle(brush, new Rect(left, top, tileSize, tileSize));
                }

                var mapObject = Map.GetObject(x, y);
                if (mapObject == 0)
                    continue;
                var objectBrush = mapObject is >= 19 and <= 22 ? s_playerBrush : s_objectBrush;
                context.FillRectangle(
                    objectBrush,
                    new Rect(left + objectInset, top + objectInset, objectSize, objectSize));
            }
        }
    }
}
