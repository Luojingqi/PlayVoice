using System;
using System.Runtime.InteropServices;
using System.Text;

namespace PlayVoice.Hotkey;

[Flags]
public enum Win32Modifiers
{
    None = 0,
    LControl = 1,
    RControl = 2,
    LAlt = 4,
    RAlt = 8,
    LShift = 16,
    RShift = 32,
    LWin = 64,
    RWin = 128
}

public class HotkeyData
{
    public Win32Modifiers Modifiers { get; set; } // 修饰键 (完全剥离 WPF)
    public int VkCode { get; set; }             // Win32 虚拟键码 (Virtual-Key Code)
    public bool IsMouse { get; set; }           // 是否为鼠标按键
    public Action Callback;

    // 引入 Win32 API 用于解析键名
    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    private static extern uint MapVirtualKey(uint uCode, uint uMapType);

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    private static extern int GetKeyNameText(int lParam, StringBuilder lpString, int cchSize);

    public override string ToString()
    {
        StringBuilder sb = new StringBuilder();

        // 解析左右修饰键
        if (Modifiers.HasFlag(Win32Modifiers.LControl)) sb.Append("LCtrl + ");
        if (Modifiers.HasFlag(Win32Modifiers.RControl)) sb.Append("RCtrl + ");
        if (Modifiers.HasFlag(Win32Modifiers.LAlt)) sb.Append("LAlt + ");
        if (Modifiers.HasFlag(Win32Modifiers.RAlt)) sb.Append("RAlt + ");
        if (Modifiers.HasFlag(Win32Modifiers.LShift)) sb.Append("LShift + ");
        if (Modifiers.HasFlag(Win32Modifiers.RShift)) sb.Append("RShift + ");
        if (Modifiers.HasFlag(Win32Modifiers.LWin)) sb.Append("LWin + ");
        if (Modifiers.HasFlag(Win32Modifiers.RWin)) sb.Append("RWin + ");

        if (IsMouse)
        {
            string mouseBtn = VkCode switch
            {
                0x04 => "MButton",      // VK_MBUTTON
                0x05 => "XButton1",     // VK_XBUTTON1
                0x06 => "XButton2",     // VK_XBUTTON2
                _ => "UnMouse"
            };
            sb.Append(mouseBtn);
        }
        else if (VkCode > 0)
        {
            // 使用 Win32 原生 API 将 VK 码转换为当前系统语言的键名
            uint scanCode = MapVirtualKey((uint)VkCode, 0);
            int lParam = (int)(scanCode << 16);

            // 处理扩展键（方向键、Insert、Delete、Home、End、PageUp、PageDown 等）
            if (VkCode is >= 33 and <= 46 or >= 91 and <= 93)
            {
                lParam |= 0x01000000;
            }

            StringBuilder keyName = new StringBuilder(256);
            if (GetKeyNameText(lParam, keyName, 256) > 0)
            {
                sb.Append(keyName.ToString());
            }
            else
            {
                sb.Append($"VK_{VkCode:X2}");
            }
        }

        return sb.Length > 0 ? sb.ToString() : "None";
    }

    public void Clear()
    {
        Modifiers = Win32Modifiers.None;
        VkCode = 0;
        IsMouse = false;
    }
}