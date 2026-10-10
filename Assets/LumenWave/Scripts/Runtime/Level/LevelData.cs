using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using UnityEngine;

namespace LumenWave
{
    /// <summary>
    /// A level parsed from a text file in Resources/Levels. Format:
    /// <code>
    /// name: First Light          (header "key: value" lines)
    /// ---
    /// ######....#####            (ASCII grid; top line = highest row; bottom line = y 0)
    /// ---
    /// beam 12 6 color=red dir=-90 range=14   (entity lines: type x y key=value...)
    /// </code>
    /// Tile (col,row) occupies world [x, x+1] × [y, y+1]. Entity coordinates are world units.
    /// Lines starting with // are comments.
    /// </summary>
    public class LevelData
    {
        public string id;
        public readonly Dictionary<string, string> header = new Dictionary<string, string>();
        public string[] grid = Array.Empty<string>();
        public readonly List<EntitySpec> entities = new List<EntitySpec>();

        public int Width => grid.Length == 0 ? 0 : grid.Max(r => r.Length);
        public int Height => grid.Length;
        public string Name => Get("name", id);

        public string Get(string key, string fallback = "") => header.TryGetValue(key, out var v) ? v : fallback;
        public float GetFloat(string key, float fallback) =>
            header.TryGetValue(key, out var v) && float.TryParse(v, NumberStyles.Float, CultureInfo.InvariantCulture, out var f) ? f : fallback;

        /// <summary>Tile char at world cell (x,y) where y=0 is the bottom row. Returns ' ' outside.</summary>
        public char Tile(int x, int y)
        {
            int row = Height - 1 - y;
            if (row < 0 || row >= Height || x < 0) return ' ';
            var line = grid[row];
            return x < line.Length ? line[x] : ' ';
        }

        /// <summary>Default grid character legend: single chars that spawn parameterless entities.</summary>
        public static readonly Dictionary<char, string> DefaultLegend = new Dictionary<char, string>
        {
            { 'P', "player" }, { 'E', "exit" }, { 'C', "checkpoint" }, { '^', "thorns" },
            { 'S', "sentinel" }, { 'W', "wisp" }, { 'x', "crystal" }, { 'h', "hidden" },
            { 'T', "tutorial" }, { 'o', "orb" },
        };

        public static bool IsSolid(char c) => c == '#' || c == '=';

        public static LevelData Parse(string text, string id = "level")
        {
            var data = new LevelData { id = id };
            var lines = text.Replace("\r", "").Split('\n');
            int section = 0;
            var gridLines = new List<string>();
            foreach (var raw in lines)
            {
                if (raw.TrimStart().StartsWith("//")) continue;
                if (raw.Trim() == "---") { section++; continue; }
                if (section == 0)
                {
                    var line = raw.Trim();
                    if (line.Length == 0) continue;
                    int c = line.IndexOf(':');
                    if (c > 0) data.header[line.Substring(0, c).Trim().ToLowerInvariant()] = line.Substring(c + 1).Trim();
                }
                else if (section == 1)
                {
                    gridLines.Add(raw.TrimEnd());
                }
                else
                {
                    var line = raw.Trim();
                    if (line.Length == 0) continue;
                    var e = EntitySpec.Parse(line);
                    if (e != null) data.entities.Add(e);
                }
            }
            // Trim empty lines at the start/end of the grid.
            while (gridLines.Count > 0 && gridLines[0].Length == 0) gridLines.RemoveAt(0);
            while (gridLines.Count > 0 && gridLines[gridLines.Count - 1].Length == 0) gridLines.RemoveAt(gridLines.Count - 1);
            data.grid = gridLines.ToArray();

            // Grid legend characters become entities at their cell centre (bottom-centre for standing things).
            for (int y = 0; y < data.Height; y++)
                for (int x = 0; x < data.Width; x++)
                {
                    char ch = data.Tile(x, y);
                    if (DefaultLegend.TryGetValue(ch, out var type))
                        data.entities.Add(new EntitySpec { type = type, pos = new Vector2(x + 0.5f, y), fromGrid = true });
                }
            return data;
        }

        public static LevelData Load(string resourceName)
        {
            var ta = Resources.Load<TextAsset>("Levels/" + resourceName);
            if (ta == null) throw new Exception("[LumenWave] Level not found: " + resourceName);
            return Parse(ta.text, resourceName);
        }

        public Vector3 PlayerStart
        {
            get
            {
                var p = entities.FirstOrDefault(e => e.type == "player");
                return p != null ? new Vector3(p.pos.x, p.pos.y + 0.05f, 0f) : new Vector3(2f, 2f, 0f);
            }
        }
    }

    public class EntitySpec
    {
        public string type;
        public Vector2 pos;
        public bool fromGrid;
        public readonly Dictionary<string, string> args = new Dictionary<string, string>();

        public static EntitySpec Parse(string line)
        {
            var parts = line.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 3) return null;
            var e = new EntitySpec { type = parts[0].ToLowerInvariant() };
            e.pos = new Vector2(ParseF(parts[1]), ParseF(parts[2]));
            for (int i = 3; i < parts.Length; i++)
            {
                int eq = parts[i].IndexOf('=');
                if (eq > 0) e.args[parts[i].Substring(0, eq).ToLowerInvariant()] = parts[i].Substring(eq + 1);
                else e.args[parts[i].ToLowerInvariant()] = "true";
            }
            return e;
        }

        static float ParseF(string s) => float.Parse(s, NumberStyles.Float, CultureInfo.InvariantCulture);

        public bool Has(string key) => args.ContainsKey(key);
        public string Str(string key, string fallback = "") => args.TryGetValue(key, out var v) ? v : fallback;
        public float Float(string key, float fallback = 0f) =>
            args.TryGetValue(key, out var v) && float.TryParse(v, NumberStyles.Float, CultureInfo.InvariantCulture, out var f) ? f : fallback;
        public int Int(string key, int fallback = 0) => Mathf.RoundToInt(Float(key, fallback));
        public bool Bool(string key, bool fallback = false) => args.TryGetValue(key, out var v) ? v == "true" || v == "1" || v == "yes" : fallback;

        /// <summary>Text args use '_' for spaces (e.g. text=Press_{Jump}_to_jump).</summary>
        public string Text(string key, string fallback = "") => Str(key, fallback).Replace('_', ' ');

        public LightColor Color(string key = "color", LightColor fallback = LightColor.White)
        {
            switch (Str(key, "").ToLowerInvariant())
            {
                case "red": case "r": return LightColor.Red;
                case "green": case "g": return LightColor.Green;
                case "blue": case "b": return LightColor.Blue;
                case "yellow": case "y": return LightColor.Yellow;
                case "white": case "w": return LightColor.White;
                case "cyan": return LightColor.Cyan;
                case "magenta": return LightColor.Magenta;
                default: return fallback;
            }
        }

        /// <summary>Direction from an angle in degrees (0 = right, 90 = up, -90 = down).</summary>
        public Vector2 Dir(string key = "dir", float fallbackDeg = 0f)
        {
            float a = Float(key, fallbackDeg) * Mathf.Deg2Rad;
            return new Vector2(Mathf.Cos(a), Mathf.Sin(a));
        }

        public Vector3 Position3 => new Vector3(pos.x, pos.y, 0f);
    }

    /// <summary>Ordered list of levels in Resources/Levels (file names start with a 2-digit number).</summary>
    public static class LevelDatabase
    {
        static string[] _names;

        public static string[] Names
        {
            get
            {
                if (_names == null)
                {
                    var index = Resources.Load<TextAsset>("Levels/index");
                    if (index != null)
                        _names = index.text.Replace("\r", "").Split('\n').Select(s => s.Trim()).Where(s => s.Length > 0 && !s.StartsWith("//")).ToArray();
                    else
                        _names = Resources.LoadAll<TextAsset>("Levels").Select(t => t.name).Where(n => n != "index").OrderBy(n => n).ToArray();
                }
                return _names;
            }
        }

        public static int Count => Names.Length;

        /// <summary>1-based level number to resource name.</summary>
        public static string NameOf(int levelNumber) => Names[Mathf.Clamp(levelNumber - 1, 0, Names.Length - 1)];

        public static LevelData Load(int levelNumber) => LevelData.Load(NameOf(levelNumber));

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { _names = null; }
    }
}
