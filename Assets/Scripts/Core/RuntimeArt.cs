using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace LumenWave
{
    /// <summary>
    /// All art that isn't an asset: sprites drawn into Texture2Ds at runtime, plus helpers to spawn
    /// sprites, Light2Ds and particle systems with consistent sorting. Everything is cached per play session.
    /// </summary>
    public static class RuntimeArt
    {
        // Sorting orders (one sorting layer, ordered by number).
        public const int SortSky = -300;
        public const int SortFar = -200;
        public const int SortMid = -150;
        public const int SortBackDecor = -10;
        public const int SortTiles = 0;
        public const int SortDecor = 2;
        public const int SortObjects = 5;
        public const int SortPlayer = 10;
        public const int SortFx = 20;

        public const int TilePixels = 32;

        static Material unlitMaterial;
        static Material litMaterial;
        static Material particleMaterial;
        static readonly Dictionary<string, Sprite> sprites = new Dictionary<string, Sprite>();
        static Texture2D softCircleTexture;

        /// <summary>Called once by GameBootstrap with the URP sprite materials (may be null: fallbacks are used).</summary>
        public static void Init(Material unlit, Material lit)
        {
            if (unlit != null) unlitMaterial = unlit;
            if (lit != null) litMaterial = lit;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetCache()
        {
            sprites.Clear();
            unlitMaterial = null;
            litMaterial = null;
            particleMaterial = null;
            softCircleTexture = null;
        }

        public static Material UnlitMaterial
        {
            get
            {
                if (unlitMaterial == null)
                {
                    Shader shader = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default");
                    if (shader == null) shader = Shader.Find("Sprites/Default");
                    if (shader == null)
                    {
                        Debug.LogError("[RuntimeArt] No unlit sprite shader found. Assign Sprite-Unlit-Default on GameBootstrap.");
                        return null;
                    }
                    unlitMaterial = new Material(shader) { name = "LW Unlit (runtime)" };
                }
                return unlitMaterial;
            }
        }

        /// <summary>The lit sprite material, or null to keep the renderer's default (which is lit under URP 2D).</summary>
        public static Material LitMaterial
        {
            get
            {
                if (litMaterial == null)
                {
                    Shader shader = Shader.Find("Universal Render Pipeline/2D/Sprite-Lit-Default");
                    if (shader != null) litMaterial = new Material(shader) { name = "LW Lit (runtime)" };
                }
                return litMaterial;
            }
        }

        public static Material ParticleMaterial
        {
            get
            {
                if (particleMaterial == null && UnlitMaterial != null)
                    particleMaterial = new Material(UnlitMaterial) { name = "LW Particles (runtime)", mainTexture = SoftCircleTexture };
                return particleMaterial;
            }
        }

        // =====================================================================
        // Spawning helpers
        // =====================================================================

        public static SpriteRenderer CreateSprite(string name, Transform parent, Sprite sprite, Color color, int sortingOrder, bool unlit, Vector3 localPosition = default)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition;
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.color = color;
            sr.sortingOrder = sortingOrder;
            ApplyMaterial(sr, unlit);
            return sr;
        }

        public static void ApplyMaterial(Renderer renderer, bool unlit)
        {
            if (unlit) { if (UnlitMaterial != null) renderer.sharedMaterial = UnlitMaterial; }
            else if (LitMaterial != null) renderer.sharedMaterial = LitMaterial;
        }

        /// <summary>A point Light2D. Built inactive and switched on at the end so URP registers it fully configured.</summary>
        public static Light2D CreatePointLight(string name, Transform parent, Vector3 localPosition, Color color, float intensity, float outerRadius, float innerRadius = 0f, float falloff = 0.5f)
        {
            var go = new GameObject(name);
            go.SetActive(false);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition;
            var light = go.AddComponent<Light2D>();
            light.lightType = Light2D.LightType.Point;
            light.color = color;
            light.intensity = intensity;
            light.pointLightOuterRadius = outerRadius;
            light.pointLightInnerRadius = innerRadius;
            light.falloffIntensity = falloff;
            light.shadowsEnabled = false;
            AllSortingLayers(light);
            go.SetActive(true);
            return light;
        }

        public static void AllSortingLayers(Light2D light)
        {
            SortingLayer[] layers = SortingLayer.layers;
            var ids = new int[layers.Length];
            for (int i = 0; i < layers.Length; i++) ids[i] = layers[i].id;
            light.targetSortingLayers = ids;
        }

        /// <summary>A particle system that only emits when told to (Emit), with fade-out and shrink over life.</summary>
        public static ParticleSystem CreateParticles(string name, Transform parent, int maxParticles, float gravity, int sortingOrder, bool worldSpace = true)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            var main = ps.main;
            main.playOnAwake = false;
            main.loop = true;
            main.maxParticles = maxParticles;
            main.simulationSpace = worldSpace ? ParticleSystemSimulationSpace.World : ParticleSystemSimulationSpace.Local;
            main.gravityModifier = gravity;
            main.startLifetime = 0.6f;
            main.startSpeed = 2f;
            main.startSize = 0.15f;
            main.scalingMode = ParticleSystemScalingMode.Hierarchy;

            var emission = ps.emission;
            emission.rateOverTime = 0f;

            var shape = ps.shape;
            shape.enabled = false;

            var colorOverLifetime = ps.colorOverLifetime;
            colorOverLifetime.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0.8f, 0.5f), new GradientAlphaKey(0f, 1f) });
            colorOverLifetime.color = gradient;

            var sizeOverLifetime = ps.sizeOverLifetime;
            sizeOverLifetime.enabled = true;
            sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0.2f));

            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = ParticleMaterial;
            renderer.sortingOrder = sortingOrder;
            renderer.renderMode = ParticleSystemRenderMode.Billboard;

            ps.Play();
            return ps;
        }

        // =====================================================================
        // Textures and sprites
        // =====================================================================

        public static Texture2D SoftCircleTexture
        {
            get
            {
                if (softCircleTexture == null)
                    softCircleTexture = MakeTexture(32, 32, FilterMode.Bilinear, (x, y) =>
                    {
                        float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(16f, 16f)) / 16f;
                        float a = Mathf.Clamp01(1f - d);
                        return new Color(1f, 1f, 1f, a * a);
                    });
                return softCircleTexture;
            }
        }

        public static Sprite White => Cached("white", () =>
            MakeSprite(MakeTexture(4, 4, FilterMode.Point, (x, y) => Color.white), 4f, new Vector2(0.5f, 0.5f)));

        public static Sprite SoftCircle => Cached("softcircle", () =>
            MakeSprite(SoftCircleTexture, 32f, new Vector2(0.5f, 0.5f)));

        public static Sprite Circle => Cached("circle", () =>
            MakeSprite(MakeTexture(64, 64, FilterMode.Bilinear, (x, y) =>
            {
                float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(32f, 32f));
                return new Color(1f, 1f, 1f, Mathf.Clamp01(31f - d));
            }), 64f, new Vector2(0.5f, 0.5f)));

        /// <summary>Echo crystal: a bright diamond with a soft halo. 0.5 units across.</summary>
        public static Sprite Diamond => Cached("diamond", () =>
            MakeSprite(MakeTexture(32, 32, FilterMode.Bilinear, (x, y) =>
            {
                float dx = Mathf.Abs(x + 0.5f - 16f), dy = Mathf.Abs(y + 0.5f - 16f);
                float d = dx / 9f + dy / 13f;
                if (d <= 1f)
                {
                    float core = Mathf.Clamp01((1f - d) * 3f);
                    return Color.Lerp(new Color(0.75f, 0.85f, 1f, 1f), Color.white, core);
                }
                float glow = Mathf.Clamp01(1f - (d - 1f) / 0.7f);
                return new Color(0.7f, 0.85f, 1f, glow * glow * 0.6f);
            }), 64f, new Vector2(0.5f, 0.5f)));

        /// <summary>Umbra's body: black blob, purple rim, faint glow. Pivot at the feet.</summary>
        public static Sprite UmbraBody => Cached("umbra", () =>
        {
            const int size = 64;
            const float cx = 32f, cy = 28f, rx = 22f, ryTop = 23f, ryBottom = 17f;
            Color core = new Color(0.01f, 0.005f, 0.02f, 1f);
            Color rim = new Color(0.62f, 0.36f, 1f, 1f);
            Texture2D tex = MakeTexture(size, size, FilterMode.Bilinear, (x, y) =>
            {
                float dx = (x + 0.5f - cx) / rx;
                float dy = (y + 0.5f - cy) / (y + 0.5f >= cy ? ryTop : ryBottom);
                float d = Mathf.Pow(Mathf.Pow(Mathf.Abs(dx), 2.4f) + Mathf.Pow(Mathf.Abs(dy), 2.4f), 1f / 2.4f);
                if (d <= 1f)
                {
                    float rimT = Mathf.InverseLerp(0.8f, 1f, d);
                    Color c = Color.Lerp(core, rim, rimT * rimT);
                    c.a = Mathf.Clamp01((1f - d) * rx); // anti-aliased edge
                    c.a = Mathf.Max(c.a, 0.5f);
                    return c;
                }
                float glow = Mathf.Clamp01(1f - (d - 1f) / 0.35f);
                return new Color(rim.r, rim.g, rim.b, glow * glow * 0.45f);
            });
            return MakeSprite(tex, 56f, new Vector2(0.5f, (cy - ryBottom) / size));
        });

        /// <summary>Two white oval eyes, centred.</summary>
        public static Sprite UmbraEyes => Cached("eyes", () =>
            MakeSprite(MakeTexture(32, 16, FilterMode.Bilinear, (x, y) =>
            {
                float a = Ellipse(x, y, 10.5f, 8f, 3.4f, 5.2f);
                float b = Ellipse(x, y, 21.5f, 8f, 3.4f, 5.2f);
                return new Color(1f, 1f, 1f, Mathf.Max(a, b));
            }), 56f, new Vector2(0.5f, 0.5f)));

        /// <summary>Tombstone-like rune stone, 1 x 1.5 units, pivot at the bottom.</summary>
        public static Sprite RuneStone => Cached("runestone", () =>
            MakeSprite(MakeTexture(32, 48, FilterMode.Point, (x, y) =>
            {
                if (!InArch(x, y, 5, 27, 34)) return Color.clear;
                bool edge = !InArch(x - 1, y, 5, 27, 34) || !InArch(x + 1, y, 5, 27, 34) || !InArch(x, y + 1, 5, 27, 34);
                return edge ? new Color(0.3f, 0.26f, 0.42f) : new Color(0.06f, 0.05f, 0.09f);
            }), 32f, new Vector2(0.5f, 0f)));

        /// <summary>The rune carved into the stone. Tint it to show inactive/active.</summary>
        public static Sprite RuneGlyph => Cached("runeglyph", () =>
            MakeSprite(MakeTexture(32, 48, FilterMode.Bilinear, (x, y) =>
            {
                Vector2 p = new Vector2(x + 0.5f, y + 0.5f);
                float d = SegmentDistance(p, new Vector2(16f, 8f), new Vector2(16f, 32f));
                d = Mathf.Min(d, SegmentDistance(p, new Vector2(16f, 22f), new Vector2(10f, 30f)));
                d = Mathf.Min(d, SegmentDistance(p, new Vector2(16f, 22f), new Vector2(22f, 30f)));
                d = Mathf.Min(d, SegmentDistance(p, new Vector2(16f, 14f), new Vector2(11f, 9f)));
                d = Mathf.Min(d, SegmentDistance(p, new Vector2(16f, 14f), new Vector2(21f, 9f)));
                float a = Mathf.Clamp01(2.2f - d);
                return new Color(1f, 1f, 1f, a);
            }), 32f, new Vector2(0.5f, 0f)));

        /// <summary>Hint stone glyph: a "?".</summary>
        public static Sprite HintGlyph => Cached("hintglyph", () => PixelArt(new[]
        {
            ".###.",
            "#...#",
            "....#",
            "...#.",
            "..#..",
            ".....",
            "..#..",
        }, 3, 32, 48, 8, 12));

        /// <summary>Door arch frame (1.5 x 2.5 units, pivot bottom). The opening is transparent.</summary>
        public static Sprite DoorFrame => Cached("doorframe", () =>
            MakeSprite(MakeTexture(48, 80, FilterMode.Point, (x, y) =>
            {
                bool outer = InArch(x, y, 0, 47, 56);
                bool inner = InArch(x, y, 6, 41, 52) && y < 79;
                if (!outer || inner) return Color.clear;
                bool rimPx = !InArch(x - 1, y, 0, 47, 56) || !InArch(x + 1, y, 0, 47, 56) || !InArch(x, y + 1, 0, 47, 56)
                             || InArch(x - 1, y, 6, 41, 52) || InArch(x + 1, y, 6, 41, 52) || InArch(x, y - 1, 6, 41, 52);
                return rimPx ? new Color(0.5f, 0.42f, 0.7f) : new Color(0.05f, 0.04f, 0.08f);
            }), 32f, new Vector2(0.5f, 0f)));

        /// <summary>The glowing inside of the door. Tinted at runtime.</summary>
        public static Sprite DoorGlow => Cached("doorglow", () =>
            MakeSprite(MakeTexture(48, 80, FilterMode.Bilinear, (x, y) =>
            {
                if (!InArch(x, y, 6, 41, 52)) return Color.clear;
                float t = y / 80f;
                float side = 1f - Mathf.Abs(x + 0.5f - 24f) / 18f;
                float a = Mathf.Lerp(1f, 0.35f, t) * Mathf.Lerp(0.6f, 1f, side);
                return new Color(1f, 1f, 1f, a);
            }), 32f, new Vector2(0.5f, 0f)));

        /// <summary>Spiky grass tufts on top of tiles. Three variants. 1 x 0.5 units, pivot bottom.</summary>
        public static Sprite Grass(int variant) => Cached("grass" + variant, () =>
        {
            var rng = new System.Random(1234 + variant * 77);
            int[] heights = new int[32];
            for (int i = 0; i < 32; i++) heights[i] = 0;
            int blades = 5 + rng.Next(5);
            for (int b = 0; b < blades; b++)
            {
                int x0 = rng.Next(2, 30);
                int h = 3 + rng.Next(11);
                for (int dx = -2; dx <= 2; dx++)
                {
                    int x = x0 + dx;
                    if (x < 0 || x >= 32) continue;
                    int hh = h - Mathf.Abs(dx) * (h / 3 + 1);
                    if (hh > heights[x]) heights[x] = hh;
                }
            }
            Texture2D tex = MakeTexture(32, 16, FilterMode.Point, (x, y) =>
            {
                if (y >= heights[x]) return Color.clear;
                bool tip = y >= heights[x] - 2;
                return tip ? new Color(0.36f, 0.3f, 0.52f) : new Color(0.04f, 0.035f, 0.07f);
            });
            return MakeSprite(tex, 32f, new Vector2(0.5f, 0f));
        });

        /// <summary>Solid tile with a rim on the edges that face empty space. mask bits: 1 = up, 2 = right, 4 = down, 8 = left are solid.</summary>
        public static Sprite Tile(int mask) => Cached("tile" + mask, () =>
        {
            const int n = TilePixels;
            bool openUp = (mask & 1) == 0, openRight = (mask & 2) == 0, openDown = (mask & 4) == 0, openLeft = (mask & 8) == 0;
            Color baseColor = new Color(0.025f, 0.02f, 0.04f);
            Color rimColor = new Color(0.42f, 0.36f, 0.62f);
            Color topColor = new Color(0.58f, 0.5f, 0.82f);
            Texture2D tex = MakeTexture(n, n, FilterMode.Point, (x, y) =>
            {
                float d = 99f;
                bool top = false;
                if (openUp && n - 1 - y < d) { d = n - 1 - y; top = true; }
                if (openRight && n - 1 - x < d) { d = n - 1 - x; top = false; }
                if (openDown && y < d) { d = y; top = false; }
                if (openLeft && x < d) { d = x; top = false; }

                // Faint stone texture so big black areas aren't flat.
                float noise = Hash(x, y, mask) * 0.012f;
                Color c = baseColor + new Color(noise, noise, noise * 1.5f, 0f);
                if (d < 1f) return top ? topColor : rimColor;
                if (d < 4f) return Color.Lerp(top ? topColor : rimColor, c, 0.55f + d * 0.13f);
                return c;
            });
            return MakeSprite(tex, n, new Vector2(0.5f, 0.5f));
        });

        /// <summary>One-way ledge: a thin plank in the top quarter of the cell.</summary>
        public static Sprite Plank => Cached("plank", () =>
            MakeSprite(MakeTexture(TilePixels, TilePixels, FilterMode.Point, (x, y) =>
            {
                if (y < 23) return Color.clear;
                if (y >= 30) return new Color(0.58f, 0.5f, 0.82f);
                if (y == 23) return new Color(0.3f, 0.26f, 0.45f);
                return new Color(0.04f, 0.035f, 0.07f);
            }), TilePixels, new Vector2(0.5f, 0.5f)));

        /// <summary>Vertical sky gradient, stretched behind everything.</summary>
        public static Sprite SkyGradient => Cached("sky", () =>
            MakeSprite(MakeTexture(4, 128, FilterMode.Bilinear, (x, y) =>
            {
                float t = y / 127f;
                return Color.Lerp(new Color(0.07f, 0.05f, 0.14f), new Color(0.01f, 0.01f, 0.03f), Mathf.Pow(t, 0.7f));
            }), 4f, new Vector2(0.5f, 0.5f)));

        /// <summary>Fill colour of a silhouette layer (to extend it downward with a plain block).</summary>
        public static Color SilhouetteColor(int layer) =>
            layer == 0 ? new Color(0.085f, 0.07f, 0.15f) : new Color(0.045f, 0.035f, 0.085f);

        /// <summary>Seamlessly tiling background silhouette. layer 0 = far lamp-towers, 1 = near hills.</summary>
        public static Sprite Silhouette(int layer) => Cached("silhouette" + layer, () =>
        {
            const int w = 512, h = 128;
            var heights = new float[w];
            var windows = new HashSet<int>();
            var rng = new System.Random(99 + layer);

            if (layer == 0)
            {
                for (int x = 0; x < w; x++)
                    heights[x] = 18f + 6f * Mathf.Sin(x * Mathf.PI * 2f * 3f / w) + 4f * Mathf.Sin(x * Mathf.PI * 2f * 7f / w);
                int towers = 14;
                for (int t = 0; t < towers; t++)
                {
                    int cx = rng.Next(w);
                    int half = 3 + rng.Next(6);
                    float top = 45f + rng.Next(70);
                    for (int dx = -half - 4; dx <= half + 4; dx++)
                    {
                        int x = ((cx + dx) % w + w) % w;
                        float hh = Mathf.Abs(dx) <= half ? top : top - (Mathf.Abs(dx) - half) * 6f;
                        if (Mathf.Abs(dx) <= 1) hh = top + 10f; // spire
                        heights[x] = Mathf.Max(heights[x], hh);
                    }
                    // A few lit windows: the light that rules this world, far away.
                    for (int k = 0; k < 3; k++)
                        windows.Add(((cx + rng.Next(-half + 1, half)) % w + w) % w * 1000 + (int)(top * (0.3f + 0.15f * k)));
                }
            }
            else
            {
                for (int x = 0; x < w; x++)
                    heights[x] = 34f + 12f * Mathf.Sin(x * Mathf.PI * 2f * 2f / w + 1f) + 7f * Mathf.Sin(x * Mathf.PI * 2f * 5f / w + 2f)
                                 + 3f * Mathf.Sin(x * Mathf.PI * 2f * 13f / w);
                for (int t = 0; t < 9; t++)
                {
                    int cx = rng.Next(w);
                    float top = heights[cx] + 20f + rng.Next(25);
                    for (int dx = -1; dx <= 1; dx++)
                    {
                        int x = ((cx + dx) % w + w) % w;
                        heights[x] = Mathf.Max(heights[x], top - Mathf.Abs(dx) * 4f);
                    }
                    for (int b = 0; b < 3; b++)
                    {
                        int dir = rng.Next(2) == 0 ? -1 : 1;
                        int len = 4 + rng.Next(8);
                        float y0 = top - 6f - b * 5f;
                        for (int s = 1; s <= len; s++)
                        {
                            int x = ((cx + dir * s) % w + w) % w;
                            heights[x] = Mathf.Max(heights[x], y0 + s * 0.6f);
                        }
                    }
                }
            }

            Color body = SilhouetteColor(layer);
            Color edge = layer == 0 ? new Color(0.13f, 0.1f, 0.22f) : new Color(0.1f, 0.08f, 0.18f);
            Texture2D tex = MakeTexture(w, h, FilterMode.Bilinear, (x, y) =>
            {
                if (y > heights[x]) return Color.clear;
                if (layer == 0 && windows.Contains(x * 1000 + y)) return new Color(0.9f, 0.75f, 0.45f);
                return heights[x] - y < 1.5f ? edge : body;
            });
            tex.wrapMode = TextureWrapMode.Repeat;
            return MakeSprite(tex, 16f, new Vector2(0f, 0f));
        });

        // =====================================================================
        // Low-level helpers
        // =====================================================================

        static Sprite Cached(string key, Func<Sprite> make)
        {
            if (!sprites.TryGetValue(key, out Sprite sprite) || sprite == null)
            {
                sprite = make();
                sprite.name = "LW " + key;
                sprites[key] = sprite;
            }
            return sprite;
        }

        public static Texture2D MakeTexture(int width, int height, FilterMode filter, Func<int, int, Color> pixel)
        {
            var tex = new Texture2D(width, height, TextureFormat.RGBA32, false)
            {
                filterMode = filter,
                wrapMode = TextureWrapMode.Clamp,
            };
            var colors = new Color[width * height];
            for (int y = 0; y < height; y++)
                for (int x = 0; x < width; x++)
                    colors[y * width + x] = pixel(x, y);
            tex.SetPixels(colors);
            tex.Apply(false, false);
            return tex;
        }

        public static Sprite MakeSprite(Texture2D tex, float pixelsPerUnit, Vector2 pivot)
        {
            return Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), pivot, pixelsPerUnit, 0, SpriteMeshType.FullRect);
        }

        static Sprite PixelArt(string[] rows, int scale, int width, int height, int offsetX, int offsetY)
        {
            Texture2D tex = MakeTexture(width, height, FilterMode.Point, (x, y) =>
            {
                int px = (x - offsetX) / scale;
                int rowFromTop = rows.Length - 1 - (y - offsetY) / scale;
                if (x < offsetX || y < offsetY || rowFromTop < 0 || rowFromTop >= rows.Length) return Color.clear;
                string row = rows[rowFromTop];
                return px < row.Length && row[px] == '#' ? Color.white : Color.clear;
            });
            return MakeSprite(tex, 32f, new Vector2(0.5f, 0f));
        }

        static float Ellipse(int x, int y, float cx, float cy, float rx, float ry)
        {
            float dx = (x + 0.5f - cx) / rx, dy = (y + 0.5f - cy) / ry;
            float d = Mathf.Sqrt(dx * dx + dy * dy);
            return Mathf.Clamp01((1f - d) * Mathf.Min(rx, ry));
        }

        // Rectangle from x0..x1 with a half-circle on top; the straight part goes up to yStraight.
        static bool InArch(int x, int y, int x0, int x1, int yStraight)
        {
            if (x < x0 || x > x1 || y < 0) return false;
            if (y <= yStraight) return true;
            float cx = (x0 + x1) * 0.5f, r = (x1 - x0) * 0.5f + 0.5f;
            float dx = x - cx, dy = y - yStraight;
            return dx * dx + dy * dy <= r * r;
        }

        static float SegmentDistance(Vector2 p, Vector2 a, Vector2 b)
        {
            Vector2 ab = b - a;
            float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / ab.sqrMagnitude);
            return Vector2.Distance(p, a + ab * t);
        }

        static float Hash(int x, int y, int seed)
        {
            unchecked
            {
                int h = x * 374761393 + y * 668265263 + seed * 2147483647;
                h = (h ^ (h >> 13)) * 1274126177;
                return ((h ^ (h >> 16)) & 0xffff) / 65535f;
            }
        }
    }
}
