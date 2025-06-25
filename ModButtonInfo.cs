using BepInEx.Configuration;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Patty_CardPicker_MOD
{
    internal struct ModButtonInfo
    {
        internal Action<bool> onToggle;
        internal ConfigEntry<bool> entry;
        internal string text;
        internal int fontSize;

        internal ModButtonInfo(ConfigEntry<bool> entry, int fontSize = 31, Action<bool> onToggle = null)
        {
            this.onToggle = onToggle;
            this.entry = entry;
            text = entry.Definition.Key;
            this.fontSize = fontSize;
        }
        internal ModButtonInfo(string text, ConfigEntry<bool> entry, int fontSize = 31, Action<bool> onToggle = null)
        {
            this.onToggle = onToggle;
            this.entry = entry;
            this.text = text;
            this.fontSize = fontSize;
        }
        internal ModButtonInfo(string text, ConfigEntry<bool> entry)
        {
            onToggle = null;
            this.entry = entry;
            this.text = text;
            fontSize = 31;
        }
        internal ModButtonInfo(string text)
        {
            onToggle = null;
            entry = null;
            this.text = text;
            fontSize = 31;
        }
    }
}
