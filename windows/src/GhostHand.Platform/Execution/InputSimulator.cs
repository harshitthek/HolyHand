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
}
