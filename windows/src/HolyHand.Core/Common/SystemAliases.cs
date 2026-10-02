namespace HolyHand.Core.Common;

/// <summary>
/// Registry of common system utilities, desktop applications, and settings shortcuts.
/// </summary>
public static class SystemAliases
{
    public static readonly Dictionary<string, string> Aliases = new(StringComparer.OrdinalIgnoreCase)
    {
        // Browsers
        ["brave"] = "brave.exe",
        ["chrome"] = "chrome.exe",
        ["google chrome"] = "chrome.exe",
        ["edge"] = "msedge.exe",
        ["microsoft edge"] = "msedge.exe",
        ["opera"] = "opera.exe",
        ["opera gx"] = "opera.exe",
        ["vivaldi"] = "vivaldi.exe",
        ["zen"] = "zen.exe",
        ["zen browser"] = "zen.exe",

        // Developer & Engineering Tools
        ["antigravity"] = "Antigravity",
        ["code"] = "code",
        ["vscode"] = "code",
        ["vs code"] = "code",
        ["visual studio code"] = "code",
        ["warp"] = "warp.exe",
        ["terminal"] = "wt.exe",
        ["windows terminal"] = "wt.exe",
        ["cmd"] = "cmd.exe",
        ["command prompt"] = "cmd.exe",
        ["git bash"] = "git-bash.exe",
        ["git gui"] = "git-gui.exe",
        ["burp"] = "Burp Suite Community Edition",
        ["burp suite"] = "Burp Suite Community Edition",
        ["docker"] = "Docker Desktop",
        ["docker desktop"] = "Docker Desktop",
        ["wireshark"] = "Wireshark",
        ["everything"] = "Everything",
        ["packet tracer"] = "Cisco Packet Tracer",
        ["cisco packet tracer"] = "Cisco Packet Tracer",
        ["cmake"] = "cmake-gui.exe",
        ["mysql"] = "MySQL Workbench 8.0 CE",
        ["mysql workbench"] = "MySQL Workbench 8.0 CE",
        ["wsl"] = "wsl.exe",

        // Creative & Media
        ["blender"] = "blender.exe",
        ["capcut"] = "CapCut",
        ["spotify"] = "spotify.exe",
        ["vlc"] = "vlc.exe",
        ["vlc media player"] = "vlc.exe",
        ["fxsound"] = "FxSound",
        ["fx sound"] = "FxSound",
        ["steam"] = "steam.exe",
        ["winrar"] = "winrar.exe",

        // AI & LLM Assistants
        ["kimi"] = "Kimi",
        ["chatgpt"] = "chatgpt.exe",
        ["copilot"] = "copilot",

        // Windows Utilities & Settings
        ["calc"] = "calc.exe",
        ["calculator"] = "calc.exe",
        ["notepad"] = "notepad.exe",
        ["paint"] = "mspaint.exe",
        ["mspaint"] = "mspaint.exe",
        ["taskmgr"] = "taskmgr.exe",
        ["task manager"] = "taskmgr.exe",
        ["explorer"] = "explorer.exe",
        ["file explorer"] = "explorer.exe",
        ["my pc"] = "explorer.exe",
        ["this pc"] = "explorer.exe",
        ["control panel"] = "control.exe",
        ["settings"] = "ms-settings:",
        ["windows settings"] = "ms-settings:",
        ["bluetooth"] = "ms-settings:bluetooth",
        ["bluetooth settings"] = "ms-settings:bluetooth",
        ["wifi"] = "ms-settings:network-wifi",
        ["wi-fi"] = "ms-settings:network-wifi",
        ["network"] = "ms-settings:network",
        ["network settings"] = "ms-settings:network",
        ["battery"] = "ms-settings:powersleep",
        ["battery settings"] = "ms-settings:powersleep",
        ["power"] = "ms-settings:powersleep",
        ["power settings"] = "ms-settings:powersleep",
        ["display"] = "ms-settings:display",
        ["display settings"] = "ms-settings:display",
        ["sound"] = "ms-settings:sound",
        ["sound settings"] = "ms-settings:sound",
        ["audio settings"] = "ms-settings:sound",
        ["night light"] = "ms-settings:nightlight",
        ["windows update"] = "ms-settings:windowsupdate",
        ["update"] = "ms-settings:windowsupdate",
        ["updates"] = "ms-settings:windowsupdate",
        ["volume mixer"] = "sndvol.exe",
        ["downloads"] = "shell:Downloads",
        ["downloads folder"] = "shell:Downloads",
        ["documents"] = "shell:Personal",
        ["documents folder"] = "shell:Personal",
        ["desktop folder"] = "shell:Desktop",

        // Office
        ["word"] = "winword.exe",
        ["excel"] = "excel.exe",
        ["powerpoint"] = "powerpnt.exe",
        ["onenote"] = "onenote.exe",
        ["outlook"] = "outlook.exe"
    };

    public static bool IsKnownAlias(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return false;
        return Aliases.ContainsKey(name.Trim());
    }
}
