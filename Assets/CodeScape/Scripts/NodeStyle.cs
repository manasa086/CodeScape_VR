using UnityEngine;

namespace CodeScape
{
    // Colors, shapes and display names for each component category.
    public static class NodeStyle
    {
        public static readonly Color Frontend = Hex("#4A90E2");
        public static readonly Color Api      = Hex("#A66BE0");
        public static readonly Color Service  = Hex("#2ECC71");
        public static readonly Color Database = Hex("#F5A623");
        public static readonly Color External = Hex("#E8505B");
        public static readonly Color Pulse    = Hex("#34E4EA");
        public static readonly Color Failure  = Hex("#FF3B30");
        public static readonly Color Success  = Hex("#7CFC8A");
        public static readonly Color Faded    = new Color(0.22f, 0.23f, 0.26f);

        public static Color ColorFor(string type)
        {
            switch (type)
            {
                case "frontend": return Frontend;
                case "api":      return Api;
                case "database": return Database;
                case "external": return External;
                default:         return Service;
            }
        }

        public static string DisplayType(string type)
        {
            switch (type)
            {
                case "frontend": return "Frontend";
                case "api":      return "API Gateway";
                case "database": return "Database";
                case "external": return "External Service";
                default:         return "Service";
            }
        }

        public static string RichHex(Color c) => "#" + ColorUtility.ToHtmlStringRGB(c);

        static Color Hex(string hex)
        {
            ColorUtility.TryParseHtmlString(hex, out var c);
            return c;
        }
    }
}
