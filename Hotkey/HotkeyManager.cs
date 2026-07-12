using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace PlayVoice.Hotkey;

public class HotkeyManager
{
    public enum KeyAction
    {
        Down,
        Up
    }

    public static HotkeyManager Inst { get; private set; }

    private readonly HashSet<HotkeyData> _registeredHotkeys = new();

    public bool IsRecording { get; private set; }
    private Action<HotkeyData> _recordCallback;

    // Win32 API 常量
    private const int WH_KEYBOARD_LL = 13;
    private const int WH_MOUSE_LL = 14;
    private const int WM_KEYDOWN = 0x0100;
    private const int WM_SYSKEYDOWN = 0x0104;
    private const int WM_MBUTTONDOWN = 0x0207;
    private const int WM_XBUTTONDOWN = 0x020B;

    // 虚拟键码常量 (Virtual-Key Codes)
    private const int VK_MBUTTON = 0x04;
    private const int VK_XBUTTON1 = 0x05;
    private const int VK_XBUTTON2 = 0x06;
    private const int VK_ESCAPE = 0x1B;
    private const int VK_BACK = 0x08;
    private const int VK_DELETE = 0x2E;

    // keybd_event 标志常量
    private const uint KEYEVENTF_EXTENDEDKEY = 0x0001;
    private const uint KEYEVENTF_KEYUP = 0x0002;

    // mouse_event 标志常量
    private const uint MOUSEEVENTF_MIDDLEDOWN = 0x0020;
    private const uint MOUSEEVENTF_MIDDLEUP = 0x0040;
    private const uint MOUSEEVENTF_XDOWN = 0x0080;
    private const uint MOUSEEVENTF_XUP = 0x0100;
    private const uint XBUTTON1 = 0x0001;
    private const uint XBUTTON2 = 0x0002;

    private IntPtr _keyboardHookId = IntPtr.Zero;
    private IntPtr _mouseHookId = IntPtr.Zero;

    private readonly LowLevelProc _keyboardProc;
    private readonly LowLevelProc _mouseProc;

    public HotkeyManager()
    {
        Inst = this;
        _keyboardProc = KeyboardHookCallback;
        _mouseProc = MouseHookCallback;
    }

    #region 热键管理与 Hook 生命周期

    public void AddHotkey(HotkeyData hotkeyData)
    {
        _registeredHotkeys.Remove(hotkeyData);
        _registeredHotkeys.Add(hotkeyData);
    }

    public void RemoveHotkey(HotkeyData hotkeyData) => _registeredHotkeys.Remove(hotkeyData);

    public void ClearHotkeys() => _registeredHotkeys.Clear();

    public void Start()
    {
        if (_keyboardHookId == IntPtr.Zero)
            _keyboardHookId = SetHook(_keyboardProc, WH_KEYBOARD_LL);

        if (_mouseHookId == IntPtr.Zero)
            _mouseHookId = SetHook(_mouseProc, WH_MOUSE_LL);
    }

    public void Stop()
    {
        if (_keyboardHookId != IntPtr.Zero)
        {
            UnhookWindowsHookEx(_keyboardHookId);
            _keyboardHookId = IntPtr.Zero;
        }
        if (_mouseHookId != IntPtr.Zero)
        {
            UnhookWindowsHookEx(_mouseHookId);
            _mouseHookId = IntPtr.Zero;
        }
    }

    #endregion

    #region 录制模式

    public void StartRecording(Action<HotkeyData> callback)
    {
        _recordCallback = callback;
        IsRecording = true;
    }

    public void StopRecording()
    {
        IsRecording = false;
        _recordCallback = null;
    }

    #endregion

    #region Hook 回调逻辑

    private IntPtr KeyboardHookCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0 && (wParam == (IntPtr)WM_KEYDOWN || wParam == (IntPtr)WM_SYSKEYDOWN))
        {
            int vkCode = Marshal.ReadInt32(lParam);

            if (IsModifierKey(vkCode))
            {
                return CallNextHookEx(_keyboardHookId, nCode, wParam, lParam);
            }

            Win32Modifiers currentModifiers = GetCurrentModifiers();

            if (IsRecording && MainWindow.Inst.IsActive)
            {
                if (currentModifiers == Win32Modifiers.None &&
                   (vkCode == VK_ESCAPE || vkCode == VK_BACK || vkCode == VK_DELETE))
                {
                    _recordCallback?.Invoke(null);
                }
                else
                {
                    _recordCallback?.Invoke(new HotkeyData
                    {
                        Modifiers = currentModifiers,
                        VkCode = vkCode,
                        IsMouse = false
                    });
                }
                return (IntPtr)1;
            }
            else
            {
                foreach (var hotkey in _registeredHotkeys)
                {
                    if (!hotkey.IsMouse && hotkey.Modifiers == currentModifiers && hotkey.VkCode == vkCode)
                    {
                        hotkey.Callback?.Invoke();
                        return (IntPtr)1;
                    }
                }
            }
        }
        return CallNextHookEx(_keyboardHookId, nCode, wParam, lParam);
    }

    private IntPtr MouseHookCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0)
        {
            int? vkCode = null;

            if (wParam == (IntPtr)WM_MBUTTONDOWN)
            {
                vkCode = VK_MBUTTON;
            }
            else if (wParam == (IntPtr)WM_XBUTTONDOWN)
            {
                int mouseData = Marshal.ReadInt32(lParam + 8) >> 16;
                vkCode = mouseData == 1 ? VK_XBUTTON1 : VK_XBUTTON2;
            }

            if (vkCode.HasValue)
            {
                Win32Modifiers currentModifiers = GetCurrentModifiers();

                if (IsRecording)
                {
                    _recordCallback?.Invoke(new HotkeyData
                    {
                        Modifiers = currentModifiers,
                        VkCode = vkCode.Value,
                        IsMouse = true
                    });
                    return (IntPtr)1;
                }
                else
                {
                    foreach (var hotkey in _registeredHotkeys)
                    {
                        if (hotkey.IsMouse && hotkey.Modifiers == currentModifiers && hotkey.VkCode == vkCode.Value)
                        {
                            hotkey.Callback?.Invoke();
                            return (IntPtr)1;
                        }
                    }
                }
            }
        }
        return CallNextHookEx(_mouseHookId, nCode, wParam, lParam);
    }

    #endregion

    #region 模拟按键

    public void SimulateHotkey(HotkeyData hotkey, KeyAction action)
    {
        if (hotkey == null) return;

        bool isDown = action == KeyAction.Down;
        if (isDown)
        {
            SimulateModifiers(hotkey.Modifiers, KeyAction.Down);
            SimulateMainKey(hotkey, KeyAction.Down);
        }
        else
        {
            SimulateMainKey(hotkey, KeyAction.Up);
            SimulateModifiers(hotkey.Modifiers, KeyAction.Up);
        }
    }

    private void SimulateModifiers(Win32Modifiers modifiers, KeyAction action)
    {
        uint flag = action == KeyAction.Up ? KEYEVENTF_KEYUP : 0;

        // 辅助方法：发送带硬件扫描码的按键
        void SendKey(byte vk, uint extraFlag)
        {
            byte scanCode = (byte)MapVirtualKey(vk, 0);
            keybd_event(vk, scanCode, extraFlag | flag, UIntPtr.Zero);
        }

        if (modifiers.HasFlag(Win32Modifiers.LControl)) SendKey(0xA2, 0);
        if (modifiers.HasFlag(Win32Modifiers.RControl)) SendKey(0xA3, KEYEVENTF_EXTENDEDKEY);

        if (modifiers.HasFlag(Win32Modifiers.LAlt)) SendKey(0xA4, 0);
        if (modifiers.HasFlag(Win32Modifiers.RAlt)) SendKey(0xA5, KEYEVENTF_EXTENDEDKEY);

        if (modifiers.HasFlag(Win32Modifiers.LShift)) SendKey(0xA0, 0);
        if (modifiers.HasFlag(Win32Modifiers.RShift)) SendKey(0xA1, 0);

        if (modifiers.HasFlag(Win32Modifiers.LWin)) SendKey(0x5B, KEYEVENTF_EXTENDEDKEY);
        if (modifiers.HasFlag(Win32Modifiers.RWin)) SendKey(0x5C, KEYEVENTF_EXTENDEDKEY);
    }

    private void SimulateMainKey(HotkeyData hotkey, KeyAction action)
    {
        if (hotkey.VkCode == 0) return;

        if (hotkey.IsMouse)
        {
            uint mouseFlag = 0;
            uint mouseData = 0;

            if (hotkey.VkCode == VK_MBUTTON)
            {
                mouseFlag = action == KeyAction.Down ? MOUSEEVENTF_MIDDLEDOWN : MOUSEEVENTF_MIDDLEUP;
            }
            else if (hotkey.VkCode == VK_XBUTTON1)
            {
                mouseFlag = action == KeyAction.Down ? MOUSEEVENTF_XDOWN : MOUSEEVENTF_XUP;
                mouseData = XBUTTON1;
            }
            else if (hotkey.VkCode == VK_XBUTTON2)
            {
                mouseFlag = action == KeyAction.Down ? MOUSEEVENTF_XDOWN : MOUSEEVENTF_XUP;
                mouseData = XBUTTON2;
            }

            if (mouseFlag != 0)
            {
                mouse_event(mouseFlag, 0, 0, mouseData, UIntPtr.Zero);
            }
        }
        else
        {
            uint flag = action == KeyAction.Up ? KEYEVENTF_KEYUP : 0;

            if (hotkey.VkCode is >= 33 and <= 46 or >= 91 and <= 93)
            {
                flag |= KEYEVENTF_EXTENDEDKEY;
            }

            // 将虚拟键码转换为真实的硬件扫描码
            byte scanCode = (byte)MapVirtualKey((uint)hotkey.VkCode, 0);

            // 使用转换后的 scanCode 替代原有的 0
            keybd_event((byte)hotkey.VkCode, scanCode, flag, UIntPtr.Zero);
        }
    }

    #endregion

    #region Win32 API

    private bool IsModifierKey(int vkCode)
    {
        return vkCode == 0x10 || vkCode == 0xA0 || vkCode == 0xA1 || // Shift, LShift, RShift
               vkCode == 0x11 || vkCode == 0xA2 || vkCode == 0xA3 || // Ctrl, LCtrl, RCtrl
               vkCode == 0x12 || vkCode == 0xA4 || vkCode == 0xA5 || // Alt, LAlt, RAlt
               vkCode == 0x5B || vkCode == 0x5C;                     // LWin, RWin
    }

    private Win32Modifiers GetCurrentModifiers()
    {
        Win32Modifiers modifiers = Win32Modifiers.None;
        if ((GetAsyncKeyState(0xA2) & 0x8000) != 0) modifiers |= Win32Modifiers.LControl;
        if ((GetAsyncKeyState(0xA3) & 0x8000) != 0) modifiers |= Win32Modifiers.RControl;
        if ((GetAsyncKeyState(0xA4) & 0x8000) != 0) modifiers |= Win32Modifiers.LAlt;
        if ((GetAsyncKeyState(0xA5) & 0x8000) != 0) modifiers |= Win32Modifiers.RAlt;
        if ((GetAsyncKeyState(0xA0) & 0x8000) != 0) modifiers |= Win32Modifiers.LShift;
        if ((GetAsyncKeyState(0xA1) & 0x8000) != 0) modifiers |= Win32Modifiers.RShift;
        if ((GetAsyncKeyState(0x5B) & 0x8000) != 0) modifiers |= Win32Modifiers.LWin;
        if ((GetAsyncKeyState(0x5C) & 0x8000) != 0) modifiers |= Win32Modifiers.RWin;
        return modifiers;
    }

    private delegate IntPtr LowLevelProc(int nCode, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelProc lpfn, IntPtr hMod, uint dwThreadId);

    [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UnhookWindowsHookEx(IntPtr hhk);

    [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

    [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern IntPtr GetModuleHandle(string lpModuleName);

    [DllImport("user32.dll")]
    private static extern short GetAsyncKeyState(int vKey);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, UIntPtr dwExtraInfo);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern void mouse_event(uint dwFlags, uint dx, uint dy, uint dwData, UIntPtr dwExtraInfo);

    // 新增引入 MapVirtualKey
    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    private static extern uint MapVirtualKey(uint uCode, uint uMapType);

    private IntPtr SetHook(LowLevelProc proc, int hookType)
    {
        using (Process curProcess = Process.GetCurrentProcess())
        using (ProcessModule curModule = curProcess.MainModule)
        {
            return SetWindowsHookEx(hookType, proc, GetModuleHandle(curModule.ModuleName), 0);
        }
    }
    #endregion
}