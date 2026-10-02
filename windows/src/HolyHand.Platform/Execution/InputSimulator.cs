using System.Drawing;
using Windows.Win32;
using Windows.Win32.UI.Input.KeyboardAndMouse;

namespace HolyHand.Platform.Execution;

public static class InputSimulator
{
    public static unsafe void Click(int x, int y)
    {
        // 1. Move cursor
        PInvoke.SetCursorPos(x, y);

        // 2. Mouse down and up
        var inputs = new INPUT[2];

        inputs[0].type = INPUT_TYPE.INPUT_MOUSE;
        inputs[0].Anonymous.mi.dwFlags = MOUSE_EVENT_FLAGS.MOUSEEVENTF_LEFTDOWN;

        inputs[1].type = INPUT_TYPE.INPUT_MOUSE;
        inputs[1].Anonymous.mi.dwFlags = MOUSE_EVENT_FLAGS.MOUSEEVENTF_LEFTUP;

        fixed (INPUT* pInputs = inputs)
        {
            PInvoke.SendInput((uint)inputs.Length, pInputs, sizeof(INPUT));
        }
    }

    public static unsafe void Scroll(bool down)
    {
        var inputs = new INPUT[1];
        inputs[0].type = INPUT_TYPE.INPUT_MOUSE;
        inputs[0].Anonymous.mi.dwFlags = MOUSE_EVENT_FLAGS.MOUSEEVENTF_WHEEL;
        inputs[0].Anonymous.mi.mouseData = unchecked((uint)(down ? -120 : 120));

        fixed (INPUT* pInputs = inputs)
        {
            PInvoke.SendInput(1, pInputs, sizeof(INPUT));
        }
    }

    public static unsafe void TypeText(string text)
    {
        if (string.IsNullOrEmpty(text)) return;

        var inputs = new INPUT[text.Length * 2];
        int idx = 0;

        foreach (var ch in text)
        {
            // Key down
            inputs[idx].type = INPUT_TYPE.INPUT_KEYBOARD;
            inputs[idx].Anonymous.ki.wScan = ch;
            inputs[idx].Anonymous.ki.dwFlags = KEYBD_EVENT_FLAGS.KEYEVENTF_UNICODE;
            idx++;

            // Key up
            inputs[idx].type = INPUT_TYPE.INPUT_KEYBOARD;
            inputs[idx].Anonymous.ki.wScan = ch;
            inputs[idx].Anonymous.ki.dwFlags = KEYBD_EVENT_FLAGS.KEYEVENTF_UNICODE | KEYBD_EVENT_FLAGS.KEYEVENTF_KEYUP;
            idx++;
        }

        fixed (INPUT* pInputs = inputs)
        {
            PInvoke.SendInput((uint)inputs.Length, pInputs, sizeof(INPUT));
        }
    }

    public static unsafe void SendKey(VIRTUAL_KEY vk)
    {
        var inputs = new INPUT[2];

        // Key down
        inputs[0].type = INPUT_TYPE.INPUT_KEYBOARD;
        inputs[0].Anonymous.ki.wVk = vk;

        // Key up
        inputs[1].type = INPUT_TYPE.INPUT_KEYBOARD;
        inputs[1].Anonymous.ki.wVk = vk;
        inputs[1].Anonymous.ki.dwFlags = KEYBD_EVENT_FLAGS.KEYEVENTF_KEYUP;

        fixed (INPUT* pInputs = inputs)
        {
            PInvoke.SendInput(2, pInputs, sizeof(INPUT));
        }
    }

    public static unsafe void SendChord(VIRTUAL_KEY mod, VIRTUAL_KEY key)
    {
        var inputs = new INPUT[4];

        // Mod down
        inputs[0].type = INPUT_TYPE.INPUT_KEYBOARD;
        inputs[0].Anonymous.ki.wVk = mod;

        // Key down
        inputs[1].type = INPUT_TYPE.INPUT_KEYBOARD;
        inputs[1].Anonymous.ki.wVk = key;

        // Key up
        inputs[2].type = INPUT_TYPE.INPUT_KEYBOARD;
        inputs[2].Anonymous.ki.wVk = key;
        inputs[2].Anonymous.ki.dwFlags = KEYBD_EVENT_FLAGS.KEYEVENTF_KEYUP;

        // Mod up
        inputs[3].type = INPUT_TYPE.INPUT_KEYBOARD;
        inputs[3].Anonymous.ki.wVk = mod;
        inputs[3].Anonymous.ki.dwFlags = KEYBD_EVENT_FLAGS.KEYEVENTF_KEYUP;

        fixed (INPUT* pInputs = inputs)
        {
            PInvoke.SendInput(4, pInputs, sizeof(INPUT));
        }
    }

    public static unsafe void SendKeyChord3(VIRTUAL_KEY mod1, VIRTUAL_KEY mod2, VIRTUAL_KEY key)
    {
        var inputs = new INPUT[6];

        // Mod1 down
        inputs[0].type = INPUT_TYPE.INPUT_KEYBOARD;
        inputs[0].Anonymous.ki.wVk = mod1;

        // Mod2 down
        inputs[1].type = INPUT_TYPE.INPUT_KEYBOARD;
        inputs[1].Anonymous.ki.wVk = mod2;

        // Key down
        inputs[2].type = INPUT_TYPE.INPUT_KEYBOARD;
        inputs[2].Anonymous.ki.wVk = key;

        // Key up
        inputs[3].type = INPUT_TYPE.INPUT_KEYBOARD;
        inputs[3].Anonymous.ki.wVk = key;
        inputs[3].Anonymous.ki.dwFlags = KEYBD_EVENT_FLAGS.KEYEVENTF_KEYUP;

        // Mod2 up
        inputs[4].type = INPUT_TYPE.INPUT_KEYBOARD;
        inputs[4].Anonymous.ki.wVk = mod2;
        inputs[4].Anonymous.ki.dwFlags = KEYBD_EVENT_FLAGS.KEYEVENTF_KEYUP;

        // Mod1 up
        inputs[5].type = INPUT_TYPE.INPUT_KEYBOARD;
        inputs[5].Anonymous.ki.wVk = mod1;
        inputs[5].Anonymous.ki.dwFlags = KEYBD_EVENT_FLAGS.KEYEVENTF_KEYUP;

        fixed (INPUT* pInputs = inputs)
        {
            PInvoke.SendInput(6, pInputs, sizeof(INPUT));
        }
    }

    public static void SelectAllAndClear()
    {
        // Ctrl + A
        SendChord((VIRTUAL_KEY)0x11, (VIRTUAL_KEY)0x41);
        Thread.Sleep(30);
        // Backspace
        SendKey((VIRTUAL_KEY)0x08);
        Thread.Sleep(30);
    }
}
