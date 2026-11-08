using System;
using System.Text.RegularExpressions;

namespace ProximityPickup
{
    // The text of one action in the hint panel hotkeys, from the strings that the game gives.
    public static class HintText
    {
        // The game joins all bindings of an action with its word for "or". The first binding is the part before
        // that word. A space inside a tag (a sprite name) does not count, and tags around the word go with it.
        public static string FirstBinding(string names, string orWord)
        {
            if (string.IsNullOrEmpty(names) || string.IsNullOrEmpty(orWord)) return names;
            var or = Regex.Match(names, @"\s+(?<!<[^>]*)(<[^>]*>\s*)*" + Regex.Escape(orWord) + @"(\s|<)");
            return or.Success ? names.Substring(0, or.Index) : names;
        }

        // Checks the raw has-value flag and value of a modifier key. False: the read is bad, and modifier is null.
        public static bool DecodeModifier(int flag, int value, Predicate<int> isDefinedAction, out int? modifier)
        {
            modifier = null;
            if (flag == 0) return true;
            if (flag != 1 || !isDefinedAction(value)) return false;
            modifier = value;
            return true;
        }

        // One action in the format of the game's inventory controls panel.
        public static string Line(string modifier, string key, string colorHex, string action)
        {
            string text = "<color=#" + colorHex + ">" + action + "</color>";
            if (string.IsNullOrEmpty(key)) return text;
            string keys = string.IsNullOrEmpty(modifier) ? key : modifier + " + " + key;
            return "<b>" + keys + "</b> " + text;
        }
    }
}
