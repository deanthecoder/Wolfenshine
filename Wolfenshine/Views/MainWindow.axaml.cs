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
        e.Handled = SetKeyState(e.Key, true) || e.Handled;
    }

    protected override void OnKeyUp(KeyEventArgs e)
    {
        base.OnKeyUp(e);
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
        m_gameTimer.Stop();
        m_gameClock.Stop();
    }

    private void OnDeactivated(object sender, EventArgs e)
    {
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
            new PlayerInput(m_moveForward, m_moveBackward, m_turnLeft, m_turnRight, m_use));
    }

    private bool SetKeyState(Key key, bool isDown)
    {
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
                m_use = isDown;
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
    }
}
