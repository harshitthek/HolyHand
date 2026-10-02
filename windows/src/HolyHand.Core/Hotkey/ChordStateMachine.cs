namespace HolyHand.Core.Hotkey;

/// <summary>
/// Pure, testable state machine for detecting the Ctrl+Win modifier chord.
/// Fires when both Ctrl and Win are held and one is released with no other key pressed in between.
/// </summary>
public class ChordStateMachine
{
    private bool _ctrlDown;
    private bool _winDown;
    private bool _chordArmed;
    private bool _interrupted;
    private bool _winSuppressionNeeded;

    public const int VkEscape = 0x1B;

    public bool IsRunActive { get; set; }

    public event Action? OnTrigger;
    public event Action? OnCancel;

    public bool IgnoreInjectedEvents { get; set; }

    public void ProcessKeyEvent(RawKeyEvent e)
    {
        if (e.IsInjected && (IgnoreInjectedEvents || e.KeyCode == RawKeyEvent.VkDummySuppression))
        {
            return;
        }

        if (!e.IsKeyUp)
        {
            HandleKeyDown(e);
        }
        else
        {
            HandleKeyUp(e);
        }
    }

    private void HandleKeyDown(RawKeyEvent e)
    {
        if (e.IsCtrl)
        {
            if (_ctrlDown)
            {
                // Ignore key auto-repeat
                return;
            }
            if (!_winDown)
            {
                // Fresh chord sequence begins from modifier-idle state
                _interrupted = false;
            }
            _ctrlDown = true;
            if (_winDown && !_interrupted)
            {
                _chordArmed = true;
                _winSuppressionNeeded = true;
            }
            return;
        }

        if (e.IsWin)
        {
            if (_winDown)
            {
                // Ignore key auto-repeat
                return;
            }
            if (!_ctrlDown)
            {
                // Fresh chord sequence begins from modifier-idle state
                _interrupted = false;
            }
            _winDown = true;
            if (_ctrlDown && !_interrupted)
            {
                _chordArmed = true;
                _winSuppressionNeeded = true;
            }
            return;
        }

        // Intervening non-chord key was pressed while any modifier was held
        if (_ctrlDown || _winDown)
        {
            _interrupted = true;
            _chordArmed = false;
            _winSuppressionNeeded = false;
        }

        // Esc while running triggers cancel (kill switch)
        if (e.KeyCode == VkEscape && IsRunActive)
        {
            OnCancel?.Invoke();
        }
    }

    private void HandleKeyUp(RawKeyEvent e)
    {
        if (e.IsCtrl)
        {
            _ctrlDown = false;
            CheckAndFireChord();
            ResetInterruptedIfAllModifiersUp();
            return;
        }

        if (e.IsWin)
        {
            _winDown = false;
            CheckAndFireChord();
            ResetInterruptedIfAllModifiersUp();
            return;
        }

        if (!_ctrlDown && !_winDown)
        {
            _interrupted = false;
            _chordArmed = false;
            _winSuppressionNeeded = false;
        }
    }

    private void CheckAndFireChord()
    {
        if (_chordArmed && !_interrupted)
        {
            _chordArmed = false;
            if (IsRunActive)
            {
                OnCancel?.Invoke();
            }
            else
            {
                OnTrigger?.Invoke();
            }
        }
    }

    private void ResetInterruptedIfAllModifiersUp()
    {
        if (!_ctrlDown && !_winDown)
        {
            _interrupted = false;
            _chordArmed = false;
            _winSuppressionNeeded = false;
        }
    }

    /// <summary>
    /// Consumes the pending Windows Start menu suppression request.
    /// Returns true if suppression should be applied for the current Win key release.
    /// </summary>
    public bool ConsumeWinSuppression()
    {
        bool needed = _winSuppressionNeeded;
        _winSuppressionNeeded = false;
        return needed;
    }

    /// <summary>
    /// For testing and diagnostic inspection of current state.
    /// </summary>
    public (bool CtrlDown, bool WinDown, bool ChordArmed, bool Interrupted) CurrentState =>
        (_ctrlDown, _winDown, _chordArmed, _interrupted);

    public bool WinSuppressionNeeded => _winSuppressionNeeded;
}
