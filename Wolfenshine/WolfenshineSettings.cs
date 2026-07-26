// Code authored by Dean Edis (DeanTheCoder).
// Anyone is free to copy, modify, use, compile, or distribute this software,
// either in source code form or as a compiled binary, for any purpose.
//
// If you modify the code, please retain this copyright header,
// and consider contributing back to the repository or letting us know
// about your modifications. Your contributions are valued!
//
// THE SOFTWARE IS PROVIDED AS IS, WITHOUT WARRANTY OF ANY KIND.

using DTC.Core.Settings;

namespace Wolfenshine;

/// <summary>
/// Persists local Wolfenshine development settings between runs.
/// </summary>
public sealed class WolfenshineSettings : UserSettingsBase
{
    protected override string SettingsFileName => "wolfenshine-settings.json";

    public int SavedMapSlot { get => Get<int>(); set => Set(value); }
    public double SavedX { get => Get<double>(); set => Set(value); }
    public double SavedY { get => Get<double>(); set => Set(value); }
    public double SavedDirectionX { get => Get<double>(); set => Set(value); }
    public double SavedDirectionY { get => Get<double>(); set => Set(value); }

    protected override void ApplyDefaults() => SavedMapSlot = -1;
}
