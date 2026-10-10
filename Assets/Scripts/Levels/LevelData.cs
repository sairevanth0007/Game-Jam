using System;
using System.Collections.Generic;
using UnityEngine;

namespace LumenWave
{
    /// <summary>
    /// Characters used in level maps. See Assets/Levels/LEGEND.md for the full description.
    /// </summary>
    public static class Legend
    {
        public const char Empty = '.';
        public const char Solid = '#';
        public const char OneWay = '-';
        public const char PlayerStart = 'P';
        public const char Checkpoint = 'C';
        public const char Echo = 'E';
        public const char Door = 'D';
        // '1'..'9' = hint stones (text from "hint1:" .. "hint9:")

        // Reserved for later phases (ignored by the P1 loader without a warning).
        public const string Reserved = "^WSbryBRY~";
    }

    /// <summary>A parsed level file: settings plus the character grid. y = 0 is the bottom row.</summary>
    public class LevelData
    {
        public string FileName;
        public string Name;
        public string Subtitle;
        public int Width;
        public int Height;
        public string[] Melody = Array.Empty<string>();
        public int DroneLayers = 1;

        readonly Dictionary<string, string> settings = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        char[,] cells;

        public IReadOnlyDictionary<string, string> Settings => settings;

        public char Get(int x, int y)
        {
            if (x < 0 || y < 0 || x >= Width || y >= Height) return Legend.Empty;
            return cells[x, y];
        }

        public bool IsSolid(int x, int y) => Get(x, y) == Legend.Solid;

        public string Setting(string key, string fallback = null)
        {
            return settings.TryGetValue(key, out string value) ? value : fallback;
        }

        public int SettingInt(string key, int fallback)
        {
            return settings.TryGetValue(key, out string value) && int.TryParse(value, out int result) ? result : fallback;
        }

        /// <summary>Cell centre in world space.</summary>
        public static Vector2 CellCenter(int x, int y) => new Vector2(x + 0.5f, y + 0.5f);

        /// <summary>Bottom middle of a cell (where feet stand).</summary>
        public static Vector2 CellBottom(int x, int y) => new Vector2(x + 0.5f, y);

        // ---------- Parsing ----------

        /// <summary>
        /// Format: "key: value" lines, then a line "map:" followed by the map rows (top row first).
        /// Lines starting with "//" are comments. Rows shorter than the longest are padded with '.'.
        /// Spaces in the map count as empty.
        /// </summary>
        public static LevelData Parse(string text, string fileName)
        {
            var level = new LevelData { FileName = fileName };
            var rows = new List<string>();
            bool inMap = false;

            string[] lines = text.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
            foreach (string rawLine in lines)
            {
                if (inMap)
                {
                    string row = rawLine.TrimEnd();
                    if (row.StartsWith("//")) continue;
                    rows.Add(row);
                    continue;
                }

                string line = rawLine.Trim();
                if (line.Length == 0 || line.StartsWith("//")) continue;
                if (line.Equals("map:", StringComparison.OrdinalIgnoreCase))
                {
                    inMap = true;
                    continue;
                }

                int colon = line.IndexOf(':');
                if (colon <= 0)
                {
                    Debug.LogWarning($"[LevelData] {fileName}: ignoring line without 'key:' before the map: \"{line}\"");
                    continue;
                }
                level.settings[line.Substring(0, colon).Trim()] = line.Substring(colon + 1).Trim();
            }

            // Drop trailing empty rows.
            while (rows.Count > 0 && rows[rows.Count - 1].Length == 0)
                rows.RemoveAt(rows.Count - 1);

            if (rows.Count == 0)
                Debug.LogError($"[LevelData] {fileName}: no map found. Add a line 'map:' followed by the rows.");

            level.Height = rows.Count;
            level.Width = 0;
            foreach (string row in rows) level.Width = Mathf.Max(level.Width, row.Length);

            level.cells = new char[Mathf.Max(1, level.Width), Mathf.Max(1, level.Height)];
            for (int r = 0; r < rows.Count; r++)
            {
                int y = level.Height - 1 - r;
                string row = rows[r];
                for (int x = 0; x < level.Width; x++)
                {
                    char c = x < row.Length ? row[x] : Legend.Empty;
                    level.cells[x, y] = c == ' ' ? Legend.Empty : c;
                }
            }

            level.Name = level.Setting("name", fileName);
            level.Subtitle = level.Setting("subtitle", "");
            level.DroneLayers = level.SettingInt("drone", 1);
            string melody = level.Setting("melody", "C4 E4 G4 C5");
            level.Melody = melody.Split(new[] { ' ', '\t', ',' }, StringSplitOptions.RemoveEmptyEntries);
            return level;
        }
    }
}
