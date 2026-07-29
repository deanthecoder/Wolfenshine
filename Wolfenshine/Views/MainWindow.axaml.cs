// Code authored by Dean Edis (DeanTheCoder).
// Anyone is free to copy, modify, use, compile, or distribute this software,
// either in source code form or as a compiled binary, for any purpose.
//
// If you modify the code, please retain this copyright header,
// and consider contributing back to the repository or letting us know
// about your modifications. Your contributions are valued!
//
// THE SOFTWARE IS PROVIDED AS IS, WITHOUT WARRANTY OF ANY KIND.

using System.Diagnostics;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Threading;
using Wolfenshine.Game;
using Wolfenshine.ViewModels;
#if DEBUG
using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.VisualTree;
using DTC.Core;
using SkiaSharp;
#endif

namespace Wolfenshine.Views;

/// <summary>
/// Hosts Wolfenshine's game viewport and desktop chrome.
/// </summary>
/// <remarks>
/// View-specific behavior belongs here; game and rendering behavior will live outside the window.
/// </remarks>
public sealed partial class MainWindow : Window
{
    private static readonly TimeSpan s_gameInterval = TimeSpan.FromSeconds(1.0 / 60.0);
    private readonly DispatcherTimer m_gameTimer;
    private readonly Stopwatch m_gameClock = new();
    private bool m_moveForward;
    private bool m_moveBackward;
    private bool m_turnLeft;
    private bool m_turnRight;
    private bool m_use;
    private bool m_run;
    private bool m_attack;
    private bool m_strafe;
    private bool m_pauseKeyDown;
    private bool m_rendererKeyDown;
    private double m_viewBobOffset;
    private double m_viewBobPhase;
    private double m_weaponSwayOffset;
    private PlayerWeapon? m_weaponSelection;
#if DEBUG
    private MapWindow m_mapWindow;
    private bool m_isCapturingRendererComparison;
    private readonly HashSet<Key> m_debugKeysDown = [];
#endif

    public MainWindow()
    {
        InitializeComponent();
        m_gameTimer = new DispatcherTimer { Interval = s_gameInterval };
        m_gameTimer.Tick += OnGameTick;
        Opened += OnOpened;
        Closed += OnClosed;
        Deactivated += OnDeactivated;
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Key == Key.P)
        {
            if (!m_pauseKeyDown && DataContext is MainWindowViewModel viewModel)
                viewModel.TogglePause();
            m_pauseKeyDown = true;
            e.Handled = true;
            return;
        }
        if (e.Key == Key.F2)
        {
            if (!m_rendererKeyDown && DataContext is MainWindowViewModel viewModel)
                viewModel.ToggleRenderer();
            m_rendererKeyDown = true;
            e.Handled = true;
            return;
        }
#if DEBUG
        if (!m_debugKeysDown.Add(e.Key))
        {
            e.Handled = true;
            return;
        }
        if (e.Key == Key.M)
        {
            ToggleMapWindow();
            e.Handled = true;
            return;
        }
        if (e.Key == Key.C)
        {
            CaptureRendererComparison();
            e.Handled = true;
            return;
        }
        if (e.Key == Key.R)
        {
            if (DataContext is MainWindowViewModel viewModel)
                viewModel.ReloadDebugState();
            e.Handled = true;
            return;
        }
        if (e.Key == Key.I)
        {
            if (DataContext is MainWindowViewModel viewModel)
                viewModel.DumpDebugInfo();
            e.Handled = true;
            return;
        }
        if (e.Key == Key.S)
        {
            if (DataContext is MainWindowViewModel viewModel)
                viewModel.SaveDebugPosition();
            e.Handled = true;
            return;
        }
        if (e.Key == Key.L)
        {
            if (DataContext is MainWindowViewModel viewModel)
                viewModel.LoadDebugPosition();
            e.Handled = true;
            return;
        }
#endif
        e.Handled = SetKeyState(e.Key, true) || e.Handled;
    }

    protected override void OnKeyUp(KeyEventArgs e)
    {
        base.OnKeyUp(e);
        if (e.Key == Key.P)
        {
            m_pauseKeyDown = false;
            e.Handled = true;
            return;
        }
        if (e.Key == Key.F2)
        {
            m_rendererKeyDown = false;
            e.Handled = true;
            return;
        }
#if DEBUG
        m_debugKeysDown.Remove(e.Key);
#endif
        e.Handled = SetKeyState(e.Key, false) || e.Handled;
    }

    private void OnOpened(object sender, EventArgs e)
    {
        m_gameClock.Restart();
        m_gameTimer.Start();
        Focus();
    }

    private void OnClosed(object sender, EventArgs e)
    {
#if DEBUG
        m_mapWindow?.Close();
        m_mapWindow = null;
        m_debugKeysDown.Clear();
#endif
        m_gameTimer.Stop();
        m_gameClock.Stop();
        (DataContext as IDisposable)?.Dispose();
    }

#if DEBUG
    private async void CaptureRendererComparison()
    {
        if (m_isCapturingRendererComparison)
            return;
        if (DataContext is not MainWindowViewModel
            {
                HasGameData: true,
                IsSelectingDifficulty: false,
                IsShowingLevelStats: false,
                IsPaused: false
            } viewModel)
        {
            Logger.Instance.Warn("Renderer comparison capture requires active, unpaused gameplay.");
            return;
        }

        m_isCapturingRendererComparison = true;
        var timerWasEnabled = m_gameTimer.IsEnabled;
        var originalEnhancedMode = viewModel.IsEnhancedRendering;
        m_gameTimer.Stop();
        try
        {
            Logger.Instance.Info("Capturing authentic and enhanced renderer frames.");
            var repositoryDirectory = FindRepositoryDirectory();
            var screenshotDirectory = Directory.CreateDirectory(
                Path.Combine(repositoryDirectory.FullName, "local", "screenshots"));
            var authenticPath = Path.Combine(screenshotDirectory.FullName, "renderer-authentic.png");
            var enhancedPath = Path.Combine(screenshotDirectory.FullName, "renderer-enhanced.png");

            if (viewModel.IsEnhancedRendering)
                viewModel.ToggleRenderer();
            await WaitForRendererFrame();
            await CaptureMacOsWindow(authenticPath);

            viewModel.ToggleRenderer();
            await WaitForRendererFrame();
            await CaptureMacOsWindow(enhancedPath);

            using var authentic = SKBitmap.Decode(authenticPath) ??
                                  throw new InvalidDataException("The authentic screenshot is invalid.");
            using var enhanced = SKBitmap.Decode(enhancedPath) ??
                                 throw new InvalidDataException("The enhanced screenshot is invalid.");
            using var comparison = CreateRendererComparison(authentic, enhanced);
            var comparisonPath = Path.Combine(repositoryDirectory.FullName, "img", "renderer-comparison.png");
            SavePng(comparison, comparisonPath);
            Logger.Instance.Info(
                $"Captured authentic and enhanced renders, then updated {comparisonPath}.");
        }
        catch (Exception exception)
        {
            Logger.Instance.Error($"Renderer comparison capture failed: {exception}");
        }
        finally
        {
            if (viewModel.IsEnhancedRendering != originalEnhancedMode)
                viewModel.ToggleRenderer();
            if (timerWasEnabled)
            {
                m_gameClock.Restart();
                m_gameTimer.Start();
            }
            m_isCapturingRendererComparison = false;
        }
    }

    private SKBitmap CreateRendererComparison(SKBitmap authentic, SKBitmap enhanced)
    {
        if (authentic.Width != enhanced.Width || authentic.Height != enhanced.Height)
            throw new InvalidDataException("Authentic and enhanced screenshots have different dimensions.");
        var comparison = new SKBitmap(authentic.Info);
        using var canvas = new SKCanvas(comparison);
        canvas.DrawBitmap(authentic, 0.0f, 0.0f);
        var topLeft = GameViewport.TranslatePoint(default, this) ?? default;
        var bottomRight = GameViewport.TranslatePoint(
            new Point(GameViewport.Bounds.Width, GameViewport.Bounds.Height),
            this) ?? default;
        var scale = RenderScaling;
        var windowContentOffsetX = (authentic.Width - (Bounds.Width * scale)) * 0.5;
        var windowContentOffsetY = authentic.Height - (Bounds.Height * scale);
        using var enhancedTriangle = new SKPath();
        enhancedTriangle.MoveTo(
            (float)(windowContentOffsetX + (topLeft.X * scale)),
            (float)(windowContentOffsetY + (topLeft.Y * scale)));
        enhancedTriangle.LineTo(
            (float)(windowContentOffsetX + (bottomRight.X * scale)),
            (float)(windowContentOffsetY + (topLeft.Y * scale)));
        enhancedTriangle.LineTo(
            (float)(windowContentOffsetX + (topLeft.X * scale)),
            (float)(windowContentOffsetY + (bottomRight.Y * scale)));
        enhancedTriangle.Close();
        canvas.Save();
        canvas.ClipPath(enhancedTriangle, SKClipOperation.Intersect, antialias: true);
        canvas.DrawBitmap(enhanced, 0.0f, 0.0f);
        canvas.Restore();
        return comparison;
    }

    private async Task WaitForRendererFrame()
    {
        UpdateLayout();
        await Task.Delay(150);
    }

    private async Task CaptureMacOsWindow(string path)
    {
        if (!OperatingSystem.IsMacOS())
            throw new PlatformNotSupportedException("Renderer comparison capture currently requires macOS.");
        var platformHandle = TryGetPlatformHandle();
        if (platformHandle == null || platformHandle.HandleDescriptor != "NSWindow")
            throw new InvalidOperationException("Could not obtain Wolfenshine's native macOS window.");
        var selector = SelRegisterName("windowNumber");
        var windowNumber = ObjcMsgSend(platformHandle.Handle, selector).ToInt64();
        using var process = Process.Start(new ProcessStartInfo
        {
            FileName = "/usr/sbin/screencapture",
            UseShellExecute = false,
            CreateNoWindow = true,
            ArgumentList = { "-x", "-o", "-l", windowNumber.ToString(), path }
        }) ?? throw new InvalidOperationException("Could not start the macOS screenshot utility.");
        await process.WaitForExitAsync();
        if (process.ExitCode != 0 || !File.Exists(path))
            throw new IOException($"macOS could not capture Wolfenshine's window to {path}.");
    }

    [DllImport("/usr/lib/libobjc.A.dylib", EntryPoint = "sel_registerName")]
    private static extern IntPtr SelRegisterName(string name);

    [DllImport("/usr/lib/libobjc.A.dylib", EntryPoint = "objc_msgSend")]
    private static extern IntPtr ObjcMsgSend(IntPtr receiver, IntPtr selector);

    private static DirectoryInfo FindRepositoryDirectory()
    {
        foreach (var startingPath in new[] { Environment.CurrentDirectory, AppContext.BaseDirectory })
        {
            for (var directory = new DirectoryInfo(startingPath); directory != null; directory = directory.Parent)
            {
                if (File.Exists(Path.Combine(directory.FullName, "README.md")) &&
                    Directory.Exists(Path.Combine(directory.FullName, "img")))
                {
                    return directory;
                }
            }
        }
        throw new DirectoryNotFoundException("Could not locate the Wolfenshine repository directory.");
    }

    private static void SavePng(SKBitmap bitmap, string path)
    {
        using var stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None);
        if (!bitmap.Encode(stream, SKEncodedImageFormat.Png, 100))
            throw new IOException($"Could not encode {path}.");
    }

    private void ToggleMapWindow()
    {
        if (m_mapWindow != null)
        {
            m_mapWindow.Close();
            m_mapWindow = null;
            return;
        }

        if (DataContext is not MainWindowViewModel { HasGameData: true } viewModel)
            return;
        m_mapWindow = new MapWindow { DataContext = viewModel };
        m_mapWindow.Closed += (_, _) => m_mapWindow = null;
        m_mapWindow.Show(this);
    }
#endif

    private void OnDeactivated(object sender, EventArgs e)
    {
#if DEBUG
        m_debugKeysDown.Clear();
#endif
        ClearInput();
        m_gameClock.Restart();
    }

    private void OnGameTick(object sender, EventArgs e)
    {
        // Clamp long UI stalls so a delayed tick cannot jump the player through the level.
        var elapsedSeconds = Math.Min(m_gameClock.Elapsed.TotalSeconds, 0.05);
        m_gameClock.Restart();
        if (DataContext is not MainWindowViewModel viewModel)
            return;
        viewModel.UpdateGame(
            elapsedSeconds,
            new PlayerInput(
                m_moveForward,
                m_moveBackward,
                m_turnLeft,
                m_turnRight,
                m_use,
                m_run,
                m_attack,
                m_weaponSelection,
                m_strafe));
        UpdateViewBob(elapsedSeconds, viewModel);
        m_weaponSelection = null;
        if (viewModel.IsGameOver)
            Close();
    }

    private void UpdateViewBob(double elapsedSeconds, MainWindowViewModel viewModel)
    {
        var speedRatio = Math.Clamp(viewModel.PlayerSpeed / 11.25, 0.0, 1.0);
        var isMoving = speedRatio > 0.01;
        if (viewModel.IsEnhancedRendering && !viewModel.IsPaused && isMoving)
        {
            m_viewBobPhase += elapsedSeconds * (7.0 + (speedRatio * 7.0));
            m_viewBobOffset = Math.Sin(m_viewBobPhase) * (0.5 + (speedRatio * 3.0));
            m_weaponSwayOffset = Math.Sin(m_viewBobPhase * 0.5) * (0.25 + (speedRatio * 2.25));
            EnhancedViewport.ViewBob = m_viewBobOffset;
            EnhancedViewport.WeaponSway = m_weaponSwayOffset;
            return;
        }
        m_viewBobOffset *= Math.Pow(0.02, elapsedSeconds);
        m_weaponSwayOffset *= Math.Pow(0.02, elapsedSeconds);
        if (Math.Abs(m_viewBobOffset) < 0.01)
            m_viewBobOffset = 0.0;
        if (Math.Abs(m_weaponSwayOffset) < 0.01)
            m_weaponSwayOffset = 0.0;
        EnhancedViewport.ViewBob = m_viewBobOffset;
        EnhancedViewport.WeaponSway = m_weaponSwayOffset;
    }

    private bool SetKeyState(Key key, bool isDown)
    {
        var isMacOs = OperatingSystem.IsMacOS();
        
        switch (key)
        {
            case Key.Up:
                m_moveForward = isDown;
                return true;
            case Key.Down:
                m_moveBackward = isDown;
                return true;
            case Key.Left:
                m_turnLeft = isDown;
                return true;
            case Key.Right:
                m_turnRight = isDown;
                return true;
            case Key.Space:
            case Key.Enter:
                m_use = isDown;
                return true;
            case Key.LeftShift:
            case Key.RightShift:
                m_run = isDown;
                return true;
            case Key.LeftCtrl when !isMacOs:
            case Key.RightCtrl when !isMacOs:
            case Key.LWin when isMacOs:
            case Key.RWin when isMacOs:
                m_attack = isDown;
                return true;
            case Key.LeftAlt:
            case Key.RightAlt:
                m_strafe = isDown;
                return true;
            case Key.D1 when isDown:
                m_weaponSelection = PlayerWeapon.Knife;
                return true;
            case Key.D2 when isDown:
                m_weaponSelection = PlayerWeapon.Pistol;
                return true;
            case Key.D3 when isDown:
                m_weaponSelection = PlayerWeapon.MachineGun;
                return true;
            case Key.D4 when isDown:
                m_weaponSelection = PlayerWeapon.Chaingun;
                return true;
            default:
                return false;
        }
    }

    private void ClearInput()
    {
        m_moveForward = false;
        m_moveBackward = false;
        m_turnLeft = false;
        m_turnRight = false;
        m_use = false;
        m_run = false;
        m_attack = false;
        m_strafe = false;
        m_pauseKeyDown = false;
        m_rendererKeyDown = false;
        m_weaponSelection = null;
    }
}
