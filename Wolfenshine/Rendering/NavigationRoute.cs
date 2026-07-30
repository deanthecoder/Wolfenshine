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
/// Describes one tile-center route to a navigation-guide objective.
/// </summary>
/// <remarks>
/// Keeping the route independent from its visual treatment lets tests verify guidance without compiling the shader.
/// </remarks>
public sealed record NavigationRoute(
    IReadOnlyList<NavigationRoutePoint> Points,
    NavigationTargetType TargetType)
{
    public static NavigationRoute Empty { get; } = new([], NavigationTargetType.None);
}

/// <summary>
/// Identifies one map tile along a navigation route.
/// </summary>
public readonly record struct NavigationRoutePoint(int X, int Y);

/// <summary>
/// Identifies the objective selected by the navigation guide.
/// </summary>
public enum NavigationTargetType
{
    None,
    Exit,
    GoldKey,
    SilverKey
}
