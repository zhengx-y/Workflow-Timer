using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows.Input;

namespace Workflow_Timer.Helpers
{
    // Canonical names are stored in JSON, while Windows receives virtual-key codes.
    // Existing A-Z names continue to work without a migration.
    public static class HotkeyKeyHelper
    {
        private static readonly Dictionary<string, uint> Codes = CreateCodes();
        private static readonly Dictionary<uint, string> Names =
            Codes.ToDictionary(pair => pair.Value, pair => pair.Key);

        private static Dictionary<string, uint> CreateCodes()
        {
            var codes = new Dictionary<string, uint>(StringComparer.Ordinal);
            for (char letter = 'A'; letter <= 'Z'; letter++)
                codes.Add(letter.ToString(), letter);

            for (int i = 0; i <= 9; i++)
            {
                string digit = i.ToString(CultureInfo.InvariantCulture);
                codes.Add(digit, (uint)(0x30 + i));
                codes.Add("Num" + digit, (uint)(0x60 + i));
            }

            for (int i = 1; i <= 11; i++)
                codes.Add("F" + i.ToString(CultureInfo.InvariantCulture), (uint)(0x6F + i));

            codes.Add("Left", 0x25);
            codes.Add("Up", 0x26);
            codes.Add("Right", 0x27);
            codes.Add("Down", 0x28);
            codes.Add("Page Up", 0x21);
            codes.Add("Page Down", 0x22);
            codes.Add("End", 0x23);
            codes.Add("Home", 0x24);
            codes.Add("Insert", 0x2D);
            codes.Add("Delete", 0x2E);
            codes.Add("Num *", 0x6A);
            codes.Add("Num +", 0x6B);
            codes.Add("Num -", 0x6D);
            codes.Add("Num Decimal", 0x6E);
            codes.Add("Num /", 0x6F);
            codes.Add("Space", 0x20);

            // OEM virtual keys depend on the active keyboard layout.
            // Store stable identifiers, never the translated character.
            codes.Add("OEM_1", 0xBA);
            codes.Add("OEM_PLUS", 0xBB);
            codes.Add("OEM_COMMA", 0xBC);
            codes.Add("OEM_MINUS", 0xBD);
            codes.Add("OEM_PERIOD", 0xBE);
            codes.Add("OEM_2", 0xBF);
            codes.Add("OEM_3", 0xC0);
            codes.Add("OEM_4", 0xDB);
            codes.Add("OEM_5", 0xDC);
            codes.Add("OEM_6", 0xDD);
            codes.Add("OEM_7", 0xDE);
            codes.Add("OEM_8", 0xDF);
            codes.Add("OEM_102", 0xE2);
            return codes;
        }

        public static string GetDisplayName(string? name)
        {
            if (string.IsNullOrEmpty(name))
                return string.Empty;
            if (!name.StartsWith("OEM_", StringComparison.Ordinal) ||
                !TryGetVirtualKey(name, out uint virtualKey))
                return name;

            // MAPVK_VK_TO_CHAR returns the unshifted symbol for this layout.
            // A dead key has its high bit set; the character is in the low word.
            uint mapped = MapVirtualKeyEx(virtualKey, 2, GetKeyboardLayout(0));
            char symbol = (char)(mapped & 0xFFFF);
            return symbol != '\0' && !char.IsControl(symbol) && !char.IsWhiteSpace(symbol)
                ? symbol.ToString() : name;
        }

        [DllImport("user32.dll", EntryPoint = "MapVirtualKeyExW", ExactSpelling = true)]
        private static extern uint MapVirtualKeyEx(uint code, uint mapType, IntPtr keyboardLayout);

        [DllImport("user32.dll", ExactSpelling = true)]
        private static extern IntPtr GetKeyboardLayout(uint threadId);

        public static bool TryGetVirtualKey(string? name, out uint virtualKey)
        {
            virtualKey = 0;
            return name != null && Codes.TryGetValue(name, out virtualKey);
        }

        public static uint GetVirtualKey(string name)
        {
            if (!TryGetVirtualKey(name, out uint virtualKey))
                throw new ArgumentException("Unsupported hotkey: " + name, nameof(name));
            return virtualKey;
        }

        public static bool TryGetName(Key key, out string name)
        {
            uint virtualKey = (uint)KeyInterop.VirtualKeyFromKey(key);
            if (Names.TryGetValue(virtualKey, out string? found))
            {
                name = found;
                return true;
            }
            name = string.Empty;
            return false;
        }
    }
}
