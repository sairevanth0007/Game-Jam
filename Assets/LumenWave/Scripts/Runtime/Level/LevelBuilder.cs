using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace LumenWave
{
    /// <summary>
    /// Marks a static method <c>static GameObject Build(EntitySpec spec, LevelContext ctx)</c> as the
    /// factory for an entity type in level files. Each subsystem registers its own entities.
    /// </summary>
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
    public class LevelEntityAttribute : Attribute
    {
        public readonly string type;
        public LevelEntityAttribute(string type) { this.type = type.ToLowerInvariant(); }
    }

    /// <summary>Shared state while building a level.</summary>
    public class LevelContext
    {
        public LevelData data;
        public Transform root;
        public Transform geometry;
        public Transform entities;
        public int levelNumber;
        /// <summary>Objects created per entity type (e.g. "checkpoint") in build order.</summary>
        public readonly Dictionary<string, List<GameObject>> byType = new Dictionary<string, List<GameObject>>();
    }

    /// <summary>Builds level geometry + entities from <see cref="LevelData"/>.</summary>
    public static class LevelBuilder
    {
        public delegate GameObject EntityFactory(EntitySpec spec, LevelContext ctx);
        static Dictionary<string, EntityFactory> _factories;

        public static Dictionary<string, EntityFactory> Factories
        {
            get
            {
                if (_factories == null) Discover();
                return _factories;
            }
        }

        static void Discover()
        {
            _factories = new Dictionary<string, EntityFactory>();
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                var n = asm.GetName().Name;
                if (!n.StartsWith("LumenWave")) continue;
                Type[] types;
                try { types = asm.GetTypes(); } catch (ReflectionTypeLoadException e) { types = Array.FindAll(e.Types, t => t != null); }
                foreach (var t in types)
                    foreach (var m in t.GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic))
                        foreach (LevelEntityAttribute a in m.GetCustomAttributes(typeof(LevelEntityAttribute), false))
                        {
                            try { _factories[a.type] = (EntityFactory)Delegate.CreateDelegate(typeof(EntityFactory), m); }
                            catch (Exception ex) { Debug.LogError($"[LumenWave] Bad entity factory {t.Name}.{m.Name}: {ex.Message}"); }
                        }
            }
        }

        /// <summary>Hook so the visuals system can style tiles (material, decoration). Set by Visuals.</summary>
        public static Action<GameObject, char, LevelContext> TileStyler;
        /// <summary>Hook run after everything is built (background, parallax, ambience...).</summary>
        public static event Action<LevelContext> PostBuild;

        public static LevelContext Build(LevelData data, Transform parent, int levelNumber)
        {
            var root = new GameObject("Level_" + data.id).transform;
            root.SetParent(parent, false);
            var ctx = new LevelContext
            {
                data = data, root = root, levelNumber = levelNumber,
                geometry = new GameObject("Geometry").transform,
                entities = new GameObject("Entities").transform,
            };
            ctx.geometry.SetParent(root, false);
            ctx.entities.SetParent(root, false);

            BuildTiles(ctx, '#', 1f);
            BuildTiles(ctx, '=', 0.35f);

            foreach (var e in data.entities)
            {
                if (!Factories.TryGetValue(e.type, out var f))
                {
                    Debug.LogWarning($"[LumenWave] No factory for entity '{e.type}' in {data.id}");
                    continue;
                }
                GameObject go = null;
                try { go = f(e, ctx); }
                catch (Exception ex) { Debug.LogError($"[LumenWave] Entity '{e.type}' at {e.pos} failed: {ex}"); }
                if (go == null) continue;
                if (go.transform.parent == null) go.transform.SetParent(ctx.entities, true);
                if (!ctx.byType.TryGetValue(e.type, out var list)) ctx.byType[e.type] = list = new List<GameObject>();
                list.Add(go);
            }

            try { PostBuild?.Invoke(ctx); }
            catch (Exception ex) { Debug.LogError("[LumenWave] PostBuild failed: " + ex); }
            return ctx;
        }

        /// <summary>
        /// Greedy-merges runs of the given tile char into horizontal strips, then stacks identical strips
        /// vertically, so a level becomes a handful of seamless box colliders.
        /// </summary>
        static void BuildTiles(LevelContext ctx, char tile, float thickness)
        {
            var d = ctx.data;
            int w = d.Width, h = d.Height;
            var used = new bool[w, h];
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    if (used[x, y] || d.Tile(x, y) != tile) continue;
                    int x2 = x;
                    while (x2 + 1 < w && !used[x2 + 1, y] && d.Tile(x2 + 1, y) == tile) x2++;
                    int y2 = y;
                    if (tile == '#')
                    {
                        while (y2 + 1 < h)
                        {
                            bool ok = true;
                            for (int i = x; i <= x2 && ok; i++) ok = !used[i, y2 + 1] && d.Tile(i, y2 + 1) == tile;
                            // Only extend if the strip above is exactly the same width (keeps edges clean).
                            if (ok && x > 0 && d.Tile(x - 1, y2 + 1) == tile && !used[x - 1, y2 + 1]) ok = false;
                            if (ok && x2 + 1 < w && d.Tile(x2 + 1, y2 + 1) == tile && !used[x2 + 1, y2 + 1]) ok = false;
                            if (!ok) break;
                            y2++;
                        }
                    }
                    for (int yy = y; yy <= y2; yy++) for (int xx = x; xx <= x2; xx++) used[xx, yy] = true;

                    float bw = x2 - x + 1, bh = (y2 - y + 1) - 1 + thickness;
                    var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    go.name = tile == '#' ? "Block" : "Ledge";
                    go.layer = Layers.Ground;
                    go.isStatic = true;
                    go.transform.SetParent(ctx.geometry, false);
                    float top = y2 + 1;
                    go.transform.localPosition = new Vector3(x + bw * 0.5f, top - bh * 0.5f, 0f);
                    // Depth: solid blocks are deep so they read as 3D slabs from the side camera.
                    go.transform.localScale = new Vector3(bw, bh, tile == '#' ? 4f : 2f);
                    TileStyler?.Invoke(go, tile, ctx);
                }
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { PostBuild = null; TileStyler = null; }
    }
}
