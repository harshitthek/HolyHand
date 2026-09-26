========================================================================
HolyHand (Windows) - AI Desktop Assistant
========================================================================

HolyHand is a Windows-native AI assistant inspired by macOS HolyHand.
It allows you to focus any application, press a global hotkey, and command
it using plain text or voice.

------------------------------------------------------------------------
QUICK START:
------------------------------------------------------------------------
1. CONFIGURATION:
   - Make sure you have a .env file in this directory with your Vercel AI Gateway key:
     AI_GATEWAY_API_KEY=vck_your_api_key_here
     AI_GATEWAY_ZERO_DATA_RETENTION=false
   - Alternatively, set the system environment variable AI_GATEWAY_API_KEY.

2. TEST CONNECTION:
   - Double-click CHECK_CONNECTION.bat (or run HolyHand.Cli.exe check).
   - If configured properly, it will show:
     [SUCCESS] Connected to Jev via Vercel AI Gateway.

3. START HOLYHAND:
   - Double-click START_HOLYHAND.bat (or run HolyHand.App.exe).
   - HolyHand starts silently in your Windows System Tray.

4. HOW TO USE:
   - Open and focus ANY target window on your PC (e.g., Notepad, Calculator, Edge).
   - Press the global hotkey:
       Ctrl + Win
   - A modern popup will appear immediately above your target app.
   - Type your instruction (e.g. "write Hello World", "calculate 25 * 4").
   - Press Enter to submit!
   - Watch the live progress status on screen as HolyHand interacts with the app.

5. KILL SWITCH:
   - Press Ctrl + Win again, press Esc, or click Close at any time to immediately
     abort automation.
========================================================================
