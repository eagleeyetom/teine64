using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Reflection;
using System.IO;

// Minimal NativeAOT Win32 tray utility to keep system awake.
// No WinForms/WPF dependency for maximal size reduction.

internal static partial class Program
{
    private const int WM_DESTROY = 0x0002;
    private const int WM_COMMAND = 0x0111;
    private const int WM_USER = 0x0400;
    private const int NIF_MESSAGE = 0x0001;
    private const int NIF_ICON = 0x0002;
    private const int NIF_TIP = 0x0004;
    private const int NIM_ADD = 0x0000;
    private const int NIM_MODIFY = 0x0001;
    private const int NIM_DELETE = 0x0002;
    private const int ID_TOGGLE = 1001;
    private const int ID_EXIT = 1002;
    private const int ID_PAUSE5 = 1003;
    private const int ID_ABOUT = 1004;
    private const int ID_PAUSE15 = 1005;
    private const int ID_PAUSE30 = 1006;
    private const int ID_PAUSE60 = 1007;
    private const int ID_AUTOSTART = 1010;
    private const int TPM_RIGHTBUTTON = 0x0002;
    private const int WM_RBUTTONUP = 0x0205;
    private const int WM_LBUTTONDBLCLK = 0x0203;
    private const uint ES_CONTINUOUS = 0x80000000;
    private const uint ES_DISPLAY_REQUIRED = 0x00000002;
    private const uint ES_SYSTEM_REQUIRED = 0x00000001;
    private const int NIF_INFO = 0x0010;
    private static readonly nint HKEY_CURRENT_USER = unchecked((nint)0x80000001);

    private static bool _autostart;
    private static bool _simulateKeypressMode = false;
    private const int ID_SIMKEY = 1011;


    private static bool _active = true;
    private static nint _hWnd;
    private static uint _trayMsgId;
    private static System.Threading.Timer? _timer;
    private static System.Threading.Timer? _timedResume;
    private static bool _timedPause;
    private static nint _hInstance;
    private static nint _iconActive;
    private static nint _iconPaused;
    private static DateTime? _timedPauseEnd;

    public static int Main(string[] args)
    {
        // Load persisted config (if any) before parsing args
        LoadConfig();
        bool startPausedArg = HasArg(args, "--paused");
        if (startPausedArg)
            _active = false;
        // else keep whatever persisted state said (default true)
        // Detect autostart from registry if config missing
        _autostart = DetectAutostart();
        _hInstance = GetModuleHandle(null);
        _trayMsgId = WM_USER + 1;

        RegisterWindowClass();
        _hWnd = CreateWindowEx(0, "Teine64Hidden", "Teine64", 0, 0, 0, 0, 0, 0, 0, _hInstance, 0);
        if (_hWnd == 0)
            return 1;

        _iconActive = CreateTeaIcon(full:true);
        _iconPaused = CreateTeaIcon(full:false);
        AddTrayIcon();
        ApplyExecutionState();
        _timer = new System.Threading.Timer(_ =>
        {
            if (_simulateKeypressMode)
                SimulateKeypress();
            else
                ApplyExecutionState();
        }, null, 59_000, 59_000);

        MSG msg;
        while (GetMessage(out msg, 0, 0, 0) != 0)
        {
            TranslateMessage(ref msg);
            DispatchMessage(ref msg);
        }
        return 0;
    }


    private static bool HasArg(string[] args, string flag)
    {
        foreach (var a in args) if (string.Equals(a, flag, StringComparison.OrdinalIgnoreCase)) return true;
        return false;
    }

    // --- Simulate Shift+F15 mode ---
    private static void ToggleSimKeyMode()
    {
        _simulateKeypressMode = !_simulateKeypressMode;
        SaveConfig();
        ModifyTrayIcon();
    }

    private static void SimulateKeypress()
    {
        if (!_active) return;
        // Simulate Shift+F15 using SendInput
        INPUT[] inputs = new INPUT[4];
        // Shift down
        inputs[0].type = 1; // Keyboard
        inputs[0].U.ki.wVk = 0x10; // VK_SHIFT
        // F15 down
        inputs[1].type = 1;
        inputs[1].U.ki.wVk = 0x7E; // VK_F15
        // F15 up
        inputs[2].type = 1;
        inputs[2].U.ki.wVk = 0x7E;
        inputs[2].U.ki.dwFlags = 2; // KEYEVENTF_KEYUP
        // Shift up
        inputs[3].type = 1;
        inputs[3].U.ki.wVk = 0x10;
        inputs[3].U.ki.dwFlags = 2;
        SendInput((uint)inputs.Length, inputs, Marshal.SizeOf<INPUT>());
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct INPUT
    {
        public uint type;
        public InputUnion U;
    }
    [StructLayout(LayoutKind.Explicit)]
    private struct InputUnion
    {
        [FieldOffset(0)] public KEYBDINPUT ki;
    }
    [StructLayout(LayoutKind.Sequential)]
    private struct KEYBDINPUT
    {
        public ushort wVk;
        public ushort wScan;
        public uint dwFlags;
        public uint time;
        public nint dwExtraInfo;
    }
    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint SendInput(uint nInputs, INPUT[] pInputs, int cbSize);

    private static void RegisterWindowClass()
    {
    _wndProcDelegate = WndProc; // direct delegate
        WNDCLASS wc = new()
        {
            lpfnWndProc = Marshal.GetFunctionPointerForDelegate(_wndProcDelegate),
            hInstance = _hInstance,
            lpszClassName = "Teine64Hidden"
        };
        RegisterClass(ref wc);
    }

    private static void AddTrayIcon() => UpdateTrayIcon(NIM_ADD);
    private static void ModifyTrayIcon() => UpdateTrayIcon(NIM_MODIFY);

    private static void UpdateTrayIcon(int message)
    {
        NOTIFYICONDATA data = new()
        {
            cbSize = (uint)Marshal.SizeOf<NOTIFYICONDATA>(),
            hWnd = _hWnd,
            uID = 1,
            uFlags = NIF_MESSAGE | NIF_TIP | NIF_ICON,
            uCallbackMessage = _trayMsgId,
            hIcon = GetCurrentIcon(),
            szTip = BuildTooltip()
        };
        Shell_NotifyIcon(message, ref data);
    }

    private static string BuildTooltip()
    {
        if (_active)
            return _simulateKeypressMode ? "Teine64 - SimKey Mode" : "Teine64 - Running";
        if (_timedPause && _timedPauseEnd.HasValue)
        {
            var remaining = _timedPauseEnd.Value - DateTime.UtcNow;
            if (remaining <= TimeSpan.Zero)
            {
                _timedPause = false; _timedPauseEnd = null; _active = true; ApplyExecutionState();
                return "Teine64 - Running";
            }
            return $"Teine64 - Paused {Math.Max(0,(int)remaining.TotalMinutes)}m{Math.Max(0,remaining.Seconds):D2}s";
        }
    return _simulateKeypressMode ? "Teine64 - SimKey Paused" : "Teine64 - Paused";
    }

    private static void RemoveTrayIcon()
    {
        NOTIFYICONDATA data = new()
        {
            cbSize = (uint)Marshal.SizeOf<NOTIFYICONDATA>(),
            hWnd = _hWnd,
            uID = 1
        };
        Shell_NotifyIcon(NIM_DELETE, ref data);
    }

    private static void Toggle()
    {
        _active = !_active;
        if (_active && _timedPause)
        {
            // Manual resume cancels timed pause.
            _timedPause = false;
            _timedResume?.Dispose();
            _timedResume = null;
            _timedPauseEnd = null;
        }
        else if (!_active)
        {
            // Manual pause cancels any pending timed resume.
            _timedPause = false;
            _timedResume?.Dispose();
            _timedResume = null;
            _timedPauseEnd = null;
        }
        ApplyExecutionState();
        ModifyTrayIcon();
    }

    private static void ApplyExecutionState()
    {
        if (_active)
            SetThreadExecutionState(ES_CONTINUOUS | ES_DISPLAY_REQUIRED | ES_SYSTEM_REQUIRED);
        else
            SetThreadExecutionState(ES_CONTINUOUS); // clear requirements
    }

    private static nint WndProc(nint hWnd, uint msg, nint wParam, nint lParam)
    {
        if (msg == _trayMsgId)
        {
            int ev = (int)lParam;
            if (ev == WM_RBUTTONUP) ShowContextMenu();
            else if (ev == WM_LBUTTONDBLCLK) Toggle();
        }
        else if (msg == WM_COMMAND)
        {
            int id = LOWORD(wParam);
            switch (id)
            {
                case ID_TOGGLE:
                    Toggle();
                    break;
                case ID_PAUSE5:
                    PauseForMinutes(5);
                    break;
                case ID_PAUSE15:
                    PauseForMinutes(15);
                    break;
                case ID_PAUSE30:
                    PauseForMinutes(30);
                    break;
                case ID_PAUSE60:
                    PauseForMinutes(60);
                    break;
                case ID_AUTOSTART:
                    ToggleAutostart();
                    break;
                case ID_SIMKEY:
                    ToggleSimKeyMode();
                    break;
                case ID_ABOUT:
                    ShowAbout();
                    break;
                case ID_EXIT:
                    Quit();
                    break;
            }
        }
        else if (msg == WM_DESTROY)
        {
            Quit();
        }
        return DefWindowProc(hWnd, msg, wParam, lParam);
    }

    private static void ShowContextMenu()
    {
        nint hMenu = CreatePopupMenu();
        AppendMenu(hMenu, 0, ID_TOGGLE, _active ? "Pause Keeping Awake" : "Resume Keeping Awake");
        // Timed pauses
        AppendMenu(hMenu, 0, ID_PAUSE5, LabelForPause(5));
        AppendMenu(hMenu, 0, ID_PAUSE15, LabelForPause(15));
        AppendMenu(hMenu, 0, ID_PAUSE30, LabelForPause(30));
        AppendMenu(hMenu, 0, ID_PAUSE60, LabelForPause(60));
        AppendMenu(hMenu, _autostart ? 0x8u : 0u, ID_AUTOSTART, "Start with Windows"); // 0x8 = MF_CHECKED
    AppendMenu(hMenu, 0, ID_ABOUT, "About...");
    AppendMenu(hMenu, _simulateKeypressMode ? 0x8u : 0u, ID_SIMKEY, "Simulate Shift+F15");
    AppendMenu(hMenu, 0x800, 0, string.Empty); // separator (MF_SEPARATOR)
        AppendMenu(hMenu, 0, ID_EXIT, "Exit");
        POINT p;
        GetCursorPos(out p);
        SetForegroundWindow(_hWnd);
        TrackPopupMenu(hMenu, TPM_RIGHTBUTTON, p.X, p.Y, 0, _hWnd, 0);
        DestroyMenu(hMenu);
    }

    private static string LabelForPause(int minutes)
    {
        if (_timedPause && _timedPauseEnd.HasValue)
        {
            int activeMinutes = (int)Math.Round((_timedPauseEnd.Value - DateTime.UtcNow).TotalMinutes);
            if (activeMinutes < 0) activeMinutes = 0;
            // If current timer matches this duration (within 1 min tolerance) label as Restart
            var total = (int)Math.Round((_timedPauseEnd.Value - (_timedPauseEnd.Value - TimeSpan.FromMinutes(minutes))).TotalMinutes);
            // Simplify: if remaining <= minutes && we are in timed pause of that original duration
            // We'll just show Restart for the one whose original length equals minutes AND currently paused
            // Keep a simpler heuristic: show "Restart X-Min Pause" for all while paused.
            if (_timedPause)
                return $"Restart {minutes}-Min Pause";
        }
        return $"Pause {minutes} Minutes";
    }

    private static void Quit()
    {
        _timer?.Dispose();
        _timedResume?.Dispose();
        _active = false;
        ApplyExecutionState();
        RemoveTrayIcon();
    if (_iconActive != 0) { DestroyIcon(_iconActive); _iconActive = 0; }
    if (_iconPaused != 0) { DestroyIcon(_iconPaused); _iconPaused = 0; }
        SaveConfig();
        PostQuitMessage(0);
    }
    private static void PauseForMinutes(int minutes)
    {
        if (minutes <= 0) return;
        _timedResume?.Dispose();
        _timedResume = null;
        _timedPause = true;
        _active = false;
        _timedPauseEnd = DateTime.UtcNow + TimeSpan.FromMinutes(minutes);
        ApplyExecutionState();
        ModifyTrayIcon();
        _timedResume = new System.Threading.Timer(_ =>
        {
            if (_timedPause && !_active)
            {
                _timedPause = false;
                _timedPauseEnd = null;
                _active = true;
                ApplyExecutionState();
                ModifyTrayIcon();
                ShowBalloon("Teine64", "Auto-resumed after timed pause.");
            }
        }, null, TimeSpan.FromMinutes(minutes), Timeout.InfiniteTimeSpan);
    }

    private static void ShowAbout()
    {
        string version;
        try { version = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "1.0.0"; } catch { version = "1.0.0"; }
        string msg = "Teine64\r\n" +
                     "Lightweight Windows tray utility to keep the system awake.\r\n" +
                     "Double-click icon to toggle; right-click for options.\r\n" +
                     "Pause 5 Minutes auto-resumes.\r\n\r" +
                     $"Version: {version}\r" +
                     "(c) 2025";
        MessageBox(_hWnd, msg, "About Teine64", 0x40);
    }

    private static int LOWORD(nint v) => (int)((ulong)v & 0xFFFF);

    // Extended NOTIFYICONDATA to support balloon notifications
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct NOTIFYICONDATA
    {
        public uint cbSize;
        public nint hWnd;
        public uint uID;
        public uint uFlags;
        public uint uCallbackMessage;
        public nint hIcon;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] public string szTip;
        public uint dwState; // unused
        public uint dwStateMask; // unused
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)] public string szInfo;
        public uint uTimeoutOrVersion;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)] public string szInfoTitle;
        public uint dwInfoFlags;
        // We omit GUID + hBalloonIcon for brevity
    }

    private static void ShowBalloon(string title, string text)
    {
        try
        {
            NOTIFYICONDATA data = new()
            {
                cbSize = (uint)Marshal.SizeOf<NOTIFYICONDATA>(),
                hWnd = _hWnd,
                uID = 1,
                uFlags = NIF_INFO | NIF_TIP | NIF_ICON,
                uCallbackMessage = _trayMsgId,
                hIcon = GetCurrentIcon(),
                szTip = BuildTooltip(),
                szInfo = text.Length > 250 ? text.Substring(0, 250) : text,
                szInfoTitle = title.Length > 63 ? title.Substring(0, 63) : title,
                dwInfoFlags = 0 // info
            };
            Shell_NotifyIcon(NIM_MODIFY, ref data);
        }
        catch { /* ignore */ }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct WNDCLASS
    {
        public uint style;
        public nint lpfnWndProc;
        public int cbClsExtra;
        public int cbWndExtra;
        public nint hInstance;
        public nint hIcon;
        public nint hCursor;
        public nint hbrBackground;
        [MarshalAs(UnmanagedType.LPStr)] public string lpszMenuName;
        [MarshalAs(UnmanagedType.LPStr)] public string lpszClassName;
    }
    private delegate nint WndProcDelegate(nint hWnd, uint msg, nint wParam, nint lParam);
    private static WndProcDelegate? _wndProcDelegate;

    // no thunk needed; delegate directly references WndProc

    // --- Icon creation (tea mug) ---
    private static nint GetCurrentIcon() => (_active ? _iconPaused : _iconActive) != 0 ? (_active ? _iconPaused : _iconActive) : LoadIcon(0, (nint)0x7F00);

    private static nint CreateTeaIcon(bool full)
    {
        // 16x16 32-bit DIB
        BITMAPV5HEADER bi = new BITMAPV5HEADER();
        bi.bV5Size = (uint)Marshal.SizeOf<BITMAPV5HEADER>();
        bi.bV5Width = 16;
        bi.bV5Height = 16; // bottom-up
        bi.bV5Planes = 1;
        bi.bV5BitCount = 32;
        bi.bV5Compression = 0; // BI_RGB
        nint hdc = GetDC(0);
        nint ppvBits;
        nint colorBmp = CreateDIBSection(hdc, ref bi, 0, out ppvBits, 0, 0);
        ReleaseDC(0, hdc);
        if (colorBmp == 0) return 0;
        // Fill pixels (BGRA)
        unsafe
        {
            uint* pixels = (uint*)ppvBits;
            for (int y = 0; y < 16; y++)
            {
                for (int x = 0; x < 16; x++)
                {
                    uint col = 0x00000000; // transparent
                    // Mug body outer wall
                    if (x >= 3 && x <= 11 && y >= 4 && y <= 12)
                        col = 0xFFD8D0C8; // ceramic
                    // Interior fill (tea) if full
                    if (full && x >= 4 && x <= 10 && y >=6 && y <=11)
                        col = 0xFF5A3015; // tea fill
                    // Tea surface line (only when full)
                    if (full && y == 6 && x >=4 && x <=10)
                        col = 0xFF4A2A10;
                    // Handle (right side)
                    if ((x == 12 && y >= 6 && y <= 10) || (x == 13 && y >= 7 && y <= 9))
                        col = 0xFFD8D0C8;
                    pixels[y * 16 + x] = col;
                }
            }
        }
        // Simple mask (all opaque -> zeros)
        nint maskBmp = CreateBitmap(16, 16, 1, 1, IntPtr.Zero);
        ICONINFO ii = new ICONINFO { fIcon = true, hbmColor = colorBmp, hbmMask = maskBmp };
        nint hIcon = CreateIconIndirect(ref ii);
        DeleteObject(colorBmp);
        DeleteObject(maskBmp);
        return hIcon;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct ICONINFO { public bool fIcon; public int xHotspot; public int yHotspot; public nint hbmMask; public nint hbmColor; }

    [StructLayout(LayoutKind.Sequential)]
    private struct BITMAPV5HEADER
    {
        public uint bV5Size; public int bV5Width; public int bV5Height; public ushort bV5Planes; public ushort bV5BitCount; public uint bV5Compression; public uint bV5SizeImage; public int bV5XPelsPerMeter; public int bV5YPelsPerMeter; public uint bV5ClrUsed; public uint bV5ClrImportant; public uint bV5RedMask; public uint bV5GreenMask; public uint bV5BlueMask; public uint bV5AlphaMask; public uint bV5CSType; public uint bV5Endpoints_ciexyzRed; public uint bV5Endpoints_ciexyzGreen; public uint bV5Endpoints_ciexyzBlue; public uint bV5GammaRed; public uint bV5GammaGreen; public uint bV5GammaBlue; public uint bV5Intent; public uint bV5ProfileData; public uint bV5ProfileSize; public uint bV5Reserved;
    }

    [DllImport("gdi32.dll")] private static extern nint CreateDIBSection(nint hdc, ref BITMAPV5HEADER pbmi, uint iUsage, out nint ppvBits, nint hSection, uint dwOffset);
    [DllImport("gdi32.dll")] private static extern bool DeleteObject(nint hObject);
    [DllImport("gdi32.dll")] private static extern nint CreateBitmap(int nWidth, int nHeight, int cPlanes, int cBitsPerPel, nint lpvBits);
    [DllImport("user32.dll")] private static extern nint CreateIconIndirect(ref ICONINFO piconinfo);
    [DllImport("user32.dll")] private static extern nint GetDC(nint hWnd);
    [DllImport("user32.dll")] private static extern int ReleaseDC(nint hWnd, nint hDC);
    [DllImport("user32.dll")] private static extern bool DestroyIcon(nint hIcon);

    [StructLayout(LayoutKind.Sequential)]
    private struct MSG
    {
        public nint hWnd;
        public uint message;
        public nint wParam;
        public nint lParam;
        public uint time;
        public int pt_x;
        public int pt_y;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT { public int X; public int Y; }

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = false)]
    private static extern nint GetModuleHandle(string? lpModuleName);

    [DllImport("user32.dll", CharSet = CharSet.Ansi, SetLastError = false)]
    private static extern ushort RegisterClass(ref WNDCLASS lpWndClass);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, EntryPoint = "CreateWindowExW", SetLastError = true)]
    private static extern nint CreateWindowEx(uint exStyle, string lpClassName, string lpWindowName, uint dwStyle, int X, int Y, int nWidth, int nHeight, nint hWndParent, nint hMenu, nint hInstance, nint lpParam);

    [DllImport("user32.dll", SetLastError = true)] private static extern int GetMessage(out MSG lpMsg, nint hWnd, uint min, uint max);
    [DllImport("user32.dll", SetLastError = false)] private static extern bool TranslateMessage(ref MSG lpMsg);
    [DllImport("user32.dll", SetLastError = true)] private static extern nint DispatchMessage(ref MSG lpMsg);
    [DllImport("user32.dll", SetLastError = true)] private static extern nint DefWindowProc(nint hWnd, uint msg, nint wParam, nint lParam);
    [DllImport("user32.dll", SetLastError = false)] private static extern void PostQuitMessage(int code);
    [DllImport("user32.dll", SetLastError = true)] private static extern nint LoadIcon(nint hInstance, nint lpIconName);
    [DllImport("user32.dll", SetLastError = true)] private static extern nint CreatePopupMenu();
    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)] private static extern bool AppendMenu(nint hMenu, uint flags, int idNewItem, string text);
    [DllImport("user32.dll", SetLastError = true)] private static extern bool DestroyMenu(nint hMenu);
    [DllImport("user32.dll", SetLastError = true)] private static extern bool GetCursorPos(out POINT p);
    [DllImport("user32.dll", SetLastError = true)] private static extern bool SetForegroundWindow(nint hWnd);
    [DllImport("user32.dll", SetLastError = true)] private static extern int TrackPopupMenu(nint hMenu, int flags, int x, int y, int r, nint hWnd, nint rect);
    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)] private static extern int MessageBox(nint hWnd, string text, string caption, uint type);

    [DllImport("shell32.dll", CharSet = CharSet.Unicode, SetLastError = true)] private static extern bool Shell_NotifyIcon(int dwMessage, ref NOTIFYICONDATA lpdata);

    [DllImport("kernel32.dll", SetLastError = false)] private static extern uint SetThreadExecutionState(uint esFlags);

    // Registry (autostart)
    [DllImport("advapi32.dll", CharSet = CharSet.Unicode, SetLastError = true)] private static extern int RegCreateKeyEx(nint hKey, string lpSubKey, int Reserved, string? lpClass, int dwOptions, int samDesired, nint lpSecurityAttributes, out nint phkResult, out uint lpdwDisposition);
    [DllImport("advapi32.dll", CharSet = CharSet.Unicode, SetLastError = true)] private static extern int RegSetValueEx(nint hKey, string lpValueName, int Reserved, uint dwType, string lpData, int cbData);
    [DllImport("advapi32.dll", CharSet = CharSet.Unicode, SetLastError = true)] private static extern int RegDeleteValue(nint hKey, string lpValueName);
    [DllImport("advapi32.dll", CharSet = CharSet.Unicode, SetLastError = true)] private static extern int RegOpenKeyEx(nint hKey, string lpSubKey, int ulOptions, int samDesired, out nint phkResult);
    [DllImport("advapi32.dll", CharSet = CharSet.Unicode, SetLastError = true)] private static extern int RegCloseKey(nint hKey);
    [DllImport("advapi32.dll", CharSet = CharSet.Unicode, SetLastError = true)] private static extern int RegQueryValueEx(nint hKey, string lpValueName, int[]? lpReserved, out uint lpType, System.Text.StringBuilder? lpData, ref uint lpcbData);

    private static void ToggleAutostart()
    {
        SetAutostart(!_autostart);
        ModifyTrayIcon(); // refresh tooltip (unchanged) but keeps consistency
    }

    private static void SetAutostart(bool enable)
    {
        string subKey = "Software\\Microsoft\\Windows\\CurrentVersion\\Run";
        nint hKey;
        const int KEY_WRITE = 0x20006; // composite rights
        int rc = RegCreateKeyEx(HKEY_CURRENT_USER, subKey, 0, null, 0, KEY_WRITE, 0, out hKey, out _);
        if (rc != 0) return;
        try
        {
            if (enable)
            {
                string exe = ProcessPath();
                if (!string.IsNullOrWhiteSpace(exe))
                {
                    int bytes = (exe.Length + 1) * 2;
                    RegSetValueEx(hKey, "Teine64", 0, 1, exe, bytes); // REG_SZ
                    _autostart = true;
                }
            }
            else
            {
                RegDeleteValue(hKey, "Teine64");
                _autostart = false;
            }
        }
        finally { RegCloseKey(hKey); }
    }

    private static bool DetectAutostart()
    {
        string subKey = "Software\\Microsoft\\Windows\\CurrentVersion\\Run";
        const int KEY_READ = 0x20019;
        nint hKey;
        int rc = RegOpenKeyEx(HKEY_CURRENT_USER, subKey, 0, KEY_READ, out hKey);
        if (rc != 0) return false;
        try
        {
            uint type;
            uint size = 1024;
            var sb = new System.Text.StringBuilder((int)size / 2);
            int rc2 = RegQueryValueEx(hKey, "Teine64", null, out type, sb, ref size);
            if (rc2 != 0 || type != 1) return false;
            return !string.IsNullOrWhiteSpace(sb.ToString());
        }
        finally { RegCloseKey(hKey); }
    }

    private static string ProcessPath()
    {
        try { return Environment.ProcessPath ?? System.Diagnostics.Process.GetCurrentProcess().MainModule?.FileName ?? ""; } catch { return ""; }
    }

    // Config persistence
    private static string ConfigDir => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Teine64");
    private static string ConfigPath => Path.Combine(ConfigDir, "config.ini");

    private static void LoadConfig()
    {
        try
        {
            if (!File.Exists(ConfigPath)) { _active = true; return; }
            foreach (var line in File.ReadAllLines(ConfigPath))
            {
                var t = line.Trim();
                if (t.StartsWith("active=", StringComparison.OrdinalIgnoreCase)) _active = t.EndsWith("1");
                else if (t.StartsWith("autostart=", StringComparison.OrdinalIgnoreCase)) _autostart = t.EndsWith("1");
                else if (t.StartsWith("simkey=", StringComparison.OrdinalIgnoreCase)) _simulateKeypressMode = t.EndsWith("1");
            }
        }
        catch { _active = true; }
    }

    private static void SaveConfig()
    {
        try
        {
            Directory.CreateDirectory(ConfigDir);
            // Always persist active=1 so default startup state is Running unless overridden by --paused
            File.WriteAllText(ConfigPath, $"active=1\nautostart={(_autostart ? 1 : 0)}\nsimkey={(_simulateKeypressMode ? 1 : 0)}\n");
        }
        catch { }
    }
}