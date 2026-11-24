namespace Iron.Agent.Core;

using System.Runtime.InteropServices;

public class DesktopAutomation : IDesktopAutomation
{
    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
    private static extern IntPtr FindWindow(string lpClassName, string lpWindowName);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
    private static extern int GetWindowText(IntPtr hWnd, System.Text.StringBuilder lpString, int nMaxCount);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool IsWindowVisible(IntPtr hWnd);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);

    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
    private static extern int GetClassName(IntPtr hWnd, System.Text.StringBuilder lpClassName, int nMaxCount);

    [DllImport("user32.dll")]
    private static extern uint SendInput(uint nInputs, INPUT[] pInputs, int cbSize);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool TerminateProcess(IntPtr hProcess, uint uExitCode);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr OpenProcess(uint dwDesiredAccess, bool bInheritHandle, int dwProcessId);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool CloseHandle(IntPtr hObject);

    private const int SW_SHOW = 5;
    private const int SW_RESTORE = 9;
    private const uint PROCESS_TERMINATE = 0x0001;

    private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct INPUT
    {
        public uint Type;
        public InputUnion Union;
    }

    [StructLayout(LayoutKind.Explicit)]
    private struct InputUnion
    {
        [FieldOffset(0)]
        public MOUSEINPUT Mouse;
        [FieldOffset(0)]
        public KEYBDINPUT Keyboard;
        [FieldOffset(0)]
        public HARDWAREINPUT Hardware;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MOUSEINPUT
    {
        public int X;
        public int Y;
        public uint MouseData;
        public uint Flags;
        public uint Time;
        public IntPtr ExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct KEYBDINPUT
    {
        public ushort Vk;
        public ushort Scan;
        public uint Flags;
        public uint Time;
        public IntPtr ExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct HARDWAREINPUT
    {
        public uint Msg;
        public ushort ParamL;
        public ushort ParamH;
    }

    private const uint INPUT_MOUSE = 0;
    private const uint INPUT_KEYBOARD = 1;
    private const uint KEYEVENTF_KEYUP = 0x0002;
    private const uint KEYEVENTF_SCANCODE = 0x0008;
    private const uint MOUSEEVENTF_LEFTDOWN = 0x0002;
    private const uint MOUSEEVENTF_LEFTUP = 0x0004;

    public async Task<UIElement[]> FindElementsAsync(string windowTitle, string automationId = null, string className = null, CancellationToken cancellationToken = default)
    {
        var results = new List<UIElement>();
        var targetWindow = FindWindowByTitle(windowTitle);

        if (targetWindow == IntPtr.Zero)
            return Array.Empty<UIElement>();

        GetWindowRect(targetWindow, out var rect);
        results.Add(new UIElement(
            AutomationId: windowTitle,
            Name: windowTitle,
            ClassName: GetClassNameString(targetWindow),
            Value: null,
            IsEnabled: IsWindowVisible(targetWindow),
            IsKeyboardFocusable: true,
            BoundingRect: new Rect(rect.Left, rect.Top, rect.Right - rect.Left, rect.Bottom - rect.Top)
        ));

        return results.ToArray();
    }

    public async Task<bool> ClickElementAsync(UIElement element, CancellationToken cancellationToken = default)
    {
        try
        {
            var centerX = element.BoundingRect.Left + (element.BoundingRect.Right - element.BoundingRect.Left) / 2;
            var centerY = element.BoundingRect.Top + (element.BoundingRect.Bottom - element.BoundingRect.Top) / 2;
            return SendMouseClick(centerX, centerY);
        }
        catch
        {
            return false;
        }
    }

    public async Task<bool> TypeTextAsync(UIElement element, string text, CancellationToken cancellationToken = default)
    {
        try
        {
            foreach (var c in text)
            {
                if (cancellationToken.IsCancellationRequested) return false;
                SendKeyPress((ushort)c);
                await Task.Delay(10, cancellationToken);
            }
            return true;
        }
        catch
        {
            return false;
        }
    }

    public async Task<bool> PressKeyAsync(string keyName, CancellationToken cancellationToken = default)
    {
        try
        {
            SendKeyPress(ParseVk(keyName));
            return true;
        }
        catch
        {
            return false;
        }
    }

    public async Task<bool> ActivateWindowAsync(string windowTitle, CancellationToken cancellationToken = default)
    {
        var hWnd = FindWindowByTitle(windowTitle);
        if (hWnd == IntPtr.Zero) return false;

        ShowWindow(hWnd, SW_RESTORE);
        return SetForegroundWindow(hWnd);
    }

    public async Task<string[]> ListRunningProcessesAsync(CancellationToken cancellationToken = default)
    {
        return System.Diagnostics.Process.GetProcesses()
            .Where(p => !string.IsNullOrEmpty(p.MainWindowTitle))
            .Select(p => p.MainWindowTitle)
            .ToArray();
    }

    public async Task<bool> StartProcessAsync(string fileName, string arguments, CancellationToken cancellationToken = default)
    {
        try
        {
            var psi = new System.Diagnostics.ProcessStartInfo
            {
                FileName = fileName,
                Arguments = arguments,
                UseShellExecute = true
            };
            System.Diagnostics.Process.Start(psi);
            return true;
        }
        catch
        {
            return false;
        }
    }

    public async Task<bool> StopProcessAsync(string processName, CancellationToken cancellationToken = default)
    {
        try
        {
            var processes = System.Diagnostics.Process.GetProcessesByName(processName);
            foreach (var p in processes)
            {
                p.Kill();
            }
            return true;
        }
        catch
        {
            return false;
        }
    }

    public async Task<AutomationResult> ExecuteCommandAsync(string command, CancellationToken cancellationToken = default)
    {
        try
        {
            var psi = new System.Diagnostics.ProcessStartInfo
            {
                FileName = "cmd.exe",
                Arguments = $"/c {command}",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = System.Diagnostics.Process.Start(psi);
            var output = await process.StandardOutput.ReadToEndAsync(cancellationToken);
            var error = await process.StandardError.ReadToEndAsync(cancellationToken);
            await process.WaitForExitAsync(cancellationToken);

            return new AutomationResult(process.ExitCode == 0, string.IsNullOrEmpty(error) ? null : error, output);
        }
        catch (Exception ex)
        {
            return new AutomationResult(false, ex.Message, null);
        }
    }

    private IntPtr FindWindowByTitle(string title)
    {
        IntPtr result = IntPtr.Zero;
        EnumWindows((hWnd, lParam) =>
        {
            var sb = new System.Text.StringBuilder(512);
            GetWindowText(hWnd, sb, sb.Capacity);
            if (sb.ToString().Contains(title, StringComparison.OrdinalIgnoreCase))
            {
                result = hWnd;
                return false;
            }
            return true;
        }, IntPtr.Zero);
        return result;
    }

    private string GetClassNameString(IntPtr hWnd)
    {
        var sb = new System.Text.StringBuilder(256);
        GetClassName(hWnd, sb, sb.Capacity);
        return sb.ToString();
    }

    private bool SendMouseClick(int x, int y)
    {
        var inputs = new INPUT[2];
        inputs[0].Type = INPUT_MOUSE;
        inputs[0].Union.Mouse = new MOUSEINPUT
        {
            X = x,
            Y = y,
            Flags = MOUSEEVENTF_LEFTDOWN
        };
        inputs[1].Type = INPUT_MOUSE;
        inputs[1].Union.Mouse = new MOUSEINPUT
        {
            X = x,
            Y = y,
            Flags = MOUSEEVENTF_LEFTUP
        };
        return SendInput(2, inputs, Marshal.SizeOf(typeof(INPUT))) > 0;
    }

    private void SendKeyPress(ushort vk)
    {
        var inputs = new INPUT[2];
        inputs[0].Type = INPUT_KEYBOARD;
        inputs[0].Union.Keyboard = new KEYBDINPUT
        {
            Vk = vk,
            Flags = 0
        };
        inputs[1].Type = INPUT_KEYBOARD;
        inputs[1].Union.Keyboard = new KEYBDINPUT
        {
            Vk = vk,
            Flags = KEYEVENTF_KEYUP
        };
        SendInput(2, inputs, Marshal.SizeOf(typeof(INPUT)));
    }

    private static ushort ParseVk(string keyName)
    {
        return keyName.ToUpperInvariant() switch
        {
            "ENTER" => 0x0D,
            "TAB" => 0x09,
            "ESC" => 0x1B,
            "SPACE" => 0x20,
            "BACK" => 0x08,
            "DELETE" => 0x2E,
            "LEFT" => 0x25,
            "UP" => 0x26,
            "RIGHT" => 0x27,
            "DOWN" => 0x28,
            "CTRL" => 0x11,
            "ALT" => 0x12,
            "SHIFT" => 0x10,
            "WIN" => 0x5B,
            _ => 0x0D
        };
    }
}
