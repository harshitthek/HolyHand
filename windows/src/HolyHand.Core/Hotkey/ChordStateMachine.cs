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

    public const int VkEscape = 0x1B;

    public bool IsRunActive { get; set; }

    public event Action? OnTrigger;
    public event Action? OnCancel;

    public void ProcessKeyEvent(RawKeyEvent e)
    {
        if (e.IsInjected)
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
            _ctrlDown = true;
            if (_winDown && !_interrupted)
            {
                _chordArmed = true;
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
            _winDown = true;
            if (_ctrlDown && !_interrupted)
            {
                _chordArmed = true;
            }
            return;
        }

        // Intervening non-chord key was pressed
        _interrupted = true;
        _chordArmed = false;

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
        }
    }

    /// <summary>
    /// For testing and diagnostic inspection of current state.
    /// </summary>
    public (bool CtrlDown, bool WinDown, bool ChordArmed, bool Interrupted) CurrentState =>
        (_ctrlDown, _winDown, _chordArmed, _interrupted);
}
