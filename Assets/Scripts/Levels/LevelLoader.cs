using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.Tilemaps;

namespace LumenWave
{
    /// <summary>
    /// Builds a level from its text file at runtime: tilemaps with merged colliders, one-way ledges,
    /// spawn, checkpoints, echoes, door, hint stones, decoration, level walls and the parallax background.
    /// Everything goes under one root object, so unloading is a single Destroy.
    /// </summary>
    public class LevelLoader : MonoBehaviour
    {
        [Tooltip("Level files (Assets/Levels/*.txt) in play order.")]
        [SerializeField] TextAsset[] levelFiles;
        [Tooltip("Camera the parallax background follows. Empty = Camera.main.")]
        [SerializeField] Camera viewCamera;
        [Tooltip("Chance (0..1) of a grass tuft on top of a ground tile.")]
        [SerializeField, Range(0f, 1f)] float grassChance = 0.45f;

        public int LevelCount => levelFiles != null ? levelFiles.Length : 0;
        public LevelData Current { get; private set; }
        public int CurrentIndex { get; private set; } = -1;
        /// <summary>Feet position of 'P'.</summary>
        public Vector2 SpawnPosition { get; private set; }
        /// <summary>The playable area: (0,0) to (width,height).</summary>
        public Rect Bounds { get; private set; }
        public int EchoTotal { get; private set; }
        public Transform Root => root;

        Transform root;
        Tile[] solidTiles;
        Tile plankTile;
        readonly HashSet<char> warnedChars = new HashSet<char>();

        public void Configure(TextAsset[] files, Camera cam)
        {
            levelFiles = files;
            viewCamera = cam;
        }

        public string LevelFileName(int index) =>
            levelFiles != null && index >= 0 && index < levelFiles.Length && levelFiles[index] != null ? levelFiles[index].name : "?";

        /// <summary>Destroys the current level (if any) and builds level [index].</summary>
        public LevelData Load(int index)
        {
            Unload();
            if (LevelCount == 0)
            {
                Debug.LogError("[LevelLoader] No level files. Put .txt files in Assets/Levels and use LumenWave > Create Game Scene.", this);
                return null;
            }

            index = Mathf.Clamp(index, 0, LevelCount - 1);
            TextAsset file = levelFiles[index];
            if (file == null)
            {
                Debug.LogError($"[LevelLoader] Level slot {index} is empty.", this);
                return null;
            }

            CurrentIndex = index;
            Current = LevelData.Parse(file.text, file.name);
            Build(Current);
            return Current;
        }

        public void Unload()
        {
            if (root == null) return;
            root.gameObject.SetActive(false); // stop triggers right now; Destroy happens at the end of the frame
            Destroy(root.gameObject);
            root = null;
        }

        // =====================================================================

        void Build(LevelData level)
        {
            EnsureTiles();
            warnedChars.Clear();
            EchoTotal = 0;

            root = new GameObject("Level " + level.Name).transform;
            var grid = new GameObject("Grid").AddComponent<Grid>();
            grid.transform.SetParent(root, false);

            Tilemap solid = CreateTilemap("Solid", grid.transform, RuntimeArt.SortTiles, false);
            Tilemap ledges = CreateTilemap("OneWay", grid.transform, RuntimeArt.SortTiles, true);
            var solidCells = new List<Vector3Int>();
            var solidTileList = new List<TileBase>();
            var ledgeCells = new List<Vector3Int>();

            bool foundSpawn = false;
            var objects = new GameObject("Objects").transform;
            objects.SetParent(root, false);

            for (int y = 0; y < level.Height; y++)
            {
                for (int x = 0; x < level.Width; x++)
                {
                    char c = level.Get(x, y);
                    switch (c)
                    {
                        case Legend.Empty:
                            break;
                        case Legend.Solid:
                            solidCells.Add(new Vector3Int(x, y, 0));
                            solidTileList.Add(solidTiles[NeighbourMask(level, x, y)]);
                            break;
                        case Legend.OneWay:
                            ledgeCells.Add(new Vector3Int(x, y, 0));
                            break;
                        case Legend.PlayerStart:
                            if (foundSpawn) Debug.LogWarning($"[LevelLoader] {level.FileName}: more than one 'P'; using the last one.");
                            SpawnPosition = LevelData.CellBottom(x, y);
                            foundSpawn = true;
                            break;
                        case Legend.Checkpoint:
                            BuildCheckpoint(objects, LevelData.CellBottom(x, y));
                            break;
                        case Legend.Echo:
                            BuildEcho(objects, LevelData.CellCenter(x, y));
                            EchoTotal++;
                            break;
                        case Legend.Door:
                            BuildDoor(objects, LevelData.CellBottom(x, y));
                            break;
                        default:
                            if (c >= '1' && c <= '9')
                                BuildHint(objects, LevelData.CellBottom(x, y), level.Setting("hint" + c, "..."));
                            else
                                WarnUnknown(level, c, x, y);
                            break;
                    }
                }
            }

            solid.SetTiles(solidCells.ToArray(), solidTileList.ToArray());
            var plankArray = new TileBase[ledgeCells.Count];
            for (int i = 0; i < plankArray.Length; i++) plankArray[i] = plankTile;
            ledges.SetTiles(ledgeCells.ToArray(), plankArray);
            FinishColliders(solid);
            FinishColliders(ledges);

            BuildGrass(level, root);
            BuildLevelWalls(level, root);

            Bounds = new Rect(0f, 0f, level.Width, level.Height);
            if (!foundSpawn)
            {
                Debug.LogWarning($"[LevelLoader] {level.FileName}: no 'P' in the map; starting at the left edge.");
                SpawnPosition = new Vector2(1.5f, level.Height * 0.5f);
            }

            BuildBackground(level, root);
        }

        // Bits: 1 = solid above, 2 = right, 4 = below, 8 = left. Outside the map counts as solid,
        // except above the top row, so the level edges don't get a rim.
        static int NeighbourMask(LevelData level, int x, int y)
        {
            int mask = 0;
            if (SolidOrEdge(level, x, y + 1)) mask |= 1;
            if (SolidOrEdge(level, x + 1, y)) mask |= 2;
            if (SolidOrEdge(level, x, y - 1)) mask |= 4;
            if (SolidOrEdge(level, x - 1, y)) mask |= 8;
            return mask;
        }

        static bool SolidOrEdge(LevelData level, int x, int y)
        {
            if (y >= level.Height) return false;
            if (x < 0 || x >= level.Width || y < 0) return true;
            return level.IsSolid(x, y);
        }

        void EnsureTiles()
        {
            if (solidTiles != null) return;
            solidTiles = new Tile[16];
            for (int mask = 0; mask < 16; mask++)
            {
                solidTiles[mask] = ScriptableObject.CreateInstance<Tile>();
                solidTiles[mask].sprite = RuntimeArt.Tile(mask);
                solidTiles[mask].colliderType = Tile.ColliderType.Grid;
                solidTiles[mask].name = "Solid " + mask;
            }
            plankTile = ScriptableObject.CreateInstance<Tile>();
            plankTile.sprite = RuntimeArt.Plank;
            plankTile.colliderType = Tile.ColliderType.Grid;
            plankTile.name = "Plank";
        }

        static Tilemap CreateTilemap(string name, Transform parent, int sortingOrder, bool oneWay)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var tilemap = go.AddComponent<Tilemap>();
            var renderer = go.AddComponent<TilemapRenderer>();
            renderer.sortingOrder = sortingOrder;
            RuntimeArt.ApplyMaterial(renderer, false);

            var tileCollider = go.AddComponent<TilemapCollider2D>();
            var composite = go.AddComponent<CompositeCollider2D>(); // adds a Rigidbody2D
            go.GetComponent<Rigidbody2D>().bodyType = RigidbodyType2D.Static;
            composite.geometryType = CompositeCollider2D.GeometryType.Polygons;
            tileCollider.compositeOperation = Collider2D.CompositeOperation.Merge;

            if (oneWay)
            {
                var effector = go.AddComponent<PlatformEffector2D>();
                effector.useOneWay = true;
                effector.surfaceArc = 170f;
                effector.useSideFriction = false;
                effector.useSideBounce = false;
                composite.usedByEffector = true;
            }
            return tilemap;
        }

        static void FinishColliders(Tilemap tilemap)
        {
            var tileCollider = tilemap.GetComponent<TilemapCollider2D>();
            if (tileCollider != null) tileCollider.ProcessTilemapChanges();
            var composite = tilemap.GetComponent<CompositeCollider2D>();
            if (composite != null) composite.GenerateGeometry();
        }

        void WarnUnknown(LevelData level, char c, int x, int y)
        {
            if (Legend.Reserved.IndexOf(c) >= 0 || warnedChars.Contains(c)) return;
            warnedChars.Add(c);
            Debug.LogWarning($"[LevelLoader] {level.FileName}: unknown map character '{c}' (first at column {x}, row {level.Height - 1 - y} from top). Treated as empty.");
        }

        // =====================================================================
        // Objects
        // =====================================================================

        static readonly Color RuneIdle = new Color(0.32f, 0.27f, 0.48f, 1f);
        static readonly Color RuneLit = new Color(0.85f, 0.65f, 1f, 1f);
        static readonly Color ShadowGlow = new Color(0.6f, 0.4f, 1f);

        static void BuildCheckpoint(Transform parent, Vector2 feet)
        {
            var go = new GameObject("Checkpoint");
            go.transform.SetParent(parent, false);
            go.transform.position = feet;

            var box = go.AddComponent<BoxCollider2D>();
            box.isTrigger = true;
            box.size = new Vector2(1.2f, 2.4f);
            box.offset = new Vector2(0f, 1.2f);

            RuntimeArt.CreateSprite("Stone", go.transform, RuntimeArt.RuneStone, Color.white, RuntimeArt.SortObjects, true);
            SpriteRenderer idleGlyph = RuntimeArt.CreateSprite("Glyph (idle)", go.transform, RuntimeArt.RuneGlyph, RuneIdle, RuntimeArt.SortObjects + 1, true);

            var active = new GameObject("Active");
            active.transform.SetParent(go.transform, false);
            SpriteRenderer litGlyph = RuntimeArt.CreateSprite("Glyph (lit)", active.transform, RuntimeArt.RuneGlyph, RuneLit, RuntimeArt.SortObjects + 1, true);
            Light2D light = RuntimeArt.CreatePointLight("Glow", active.transform, new Vector3(0f, 0.8f, 0f), ShadowGlow, 0.9f, 4f, 0.3f);
            active.AddComponent<Pulse>().Setup(litGlyph, light, 1.8f, 0.3f);

            var checkpoint = go.AddComponent<Checkpoint>();
            checkpoint.SetVisuals(idleGlyph.gameObject, active);
        }

        static void BuildEcho(Transform parent, Vector2 center)
        {
            var go = new GameObject("Echo");
            go.transform.SetParent(parent, false);
            go.transform.position = center;

            var circle = go.AddComponent<CircleCollider2D>();
            circle.isTrigger = true;
            circle.radius = 0.45f;

            SpriteRenderer crystal = RuntimeArt.CreateSprite("Crystal", go.transform, RuntimeArt.Diamond, new Color(0.88f, 0.94f, 1f, 1f), RuntimeArt.SortObjects + 2, true);
            Light2D light = RuntimeArt.CreatePointLight("Glow", go.transform, Vector3.zero, new Color(0.6f, 0.8f, 1f), 0.6f, 1.8f, 0.1f);

            go.AddComponent<Echo>().Setup(crystal, light);
        }

        static void BuildDoor(Transform parent, Vector2 feet)
        {
            var go = new GameObject("Door");
            go.transform.SetParent(parent, false);
            go.transform.position = feet;

            var box = go.AddComponent<BoxCollider2D>();
            box.isTrigger = true;
            box.size = new Vector2(0.9f, 2f);
            box.offset = new Vector2(0f, 1f);

            RuntimeArt.CreateSprite("Frame", go.transform, RuntimeArt.DoorFrame, Color.white, RuntimeArt.SortObjects, true);
            SpriteRenderer glow = RuntimeArt.CreateSprite("Glow", go.transform, RuntimeArt.DoorGlow, new Color(0.78f, 0.66f, 1f, 0.9f), RuntimeArt.SortObjects - 1, true);
            Light2D light = RuntimeArt.CreatePointLight("Light", go.transform, new Vector3(0f, 1.2f, 0f), new Color(0.75f, 0.6f, 1f), 1f, 4.5f, 0.6f);
            go.AddComponent<Pulse>().Setup(glow, light, 1.2f, 0.25f);

            go.AddComponent<WinZone>();
        }

        static void BuildHint(Transform parent, Vector2 feet, string text)
        {
            var go = new GameObject("Hint");
            go.transform.SetParent(parent, false);
            go.transform.position = feet;

            var circle = go.AddComponent<CircleCollider2D>();
            circle.isTrigger = true;
            circle.radius = 2.2f;
            circle.offset = new Vector2(0f, 1f);

            var art = new GameObject("Art").transform;
            art.SetParent(go.transform, false);
            art.localScale = new Vector3(0.7f, 0.7f, 1f);
            RuntimeArt.CreateSprite("Stone", art, RuntimeArt.RuneStone, Color.white, RuntimeArt.SortObjects, true);
            SpriteRenderer glyph = RuntimeArt.CreateSprite("Glyph", art, RuntimeArt.HintGlyph, Color.white, RuntimeArt.SortObjects + 1, true);

            go.AddComponent<HintSign>().Setup(text, glyph);
        }

        // =====================================================================
        // Decoration, walls, background
        // =====================================================================

        void BuildGrass(LevelData level, Transform parent)
        {
            var decor = new GameObject("Decor").transform;
            decor.SetParent(parent, false);
            for (int y = 0; y < level.Height; y++)
            {
                for (int x = 0; x < level.Width; x++)
                {
                    if (!level.IsSolid(x, y) || level.Get(x, y + 1) != Legend.Empty) continue;
                    float r = Hash01(x, y);
                    if (r > grassChance) continue;
                    int variant = (int)(Hash01(y, x) * 3f) % 3;
                    SpriteRenderer tuft = RuntimeArt.CreateSprite("Grass", decor, RuntimeArt.Grass(variant), Color.white, RuntimeArt.SortDecor, false,
                        new Vector3(x + 0.5f + (r - 0.5f) * 0.3f, y + 1f, 0f));
                    tuft.flipX = Hash01(x + 7, y + 3) > 0.5f;
                }
            }
        }

        static void BuildLevelWalls(LevelData level, Transform parent)
        {
            var walls = new GameObject("Level Walls");
            walls.transform.SetParent(parent, false);
            float height = level.Height * 3f;
            var left = walls.AddComponent<BoxCollider2D>();
            left.size = new Vector2(1f, height);
            left.offset = new Vector2(-0.5f, level.Height * 0.5f);
            var right = walls.AddComponent<BoxCollider2D>();
            right.size = new Vector2(1f, height);
            right.offset = new Vector2(level.Width + 0.5f, level.Height * 0.5f);
        }

        void BuildBackground(LevelData level, Transform parent)
        {
            Camera cam = viewCamera != null ? viewCamera : Camera.main;
            if (cam == null) return;

            float halfHeight = cam.orthographicSize;
            float halfWidth = halfHeight * Mathf.Max(1.3f, cam.aspect) + 4f;
            float referenceCameraY = Mathf.Max(SpawnPosition.y + 1.6f, halfHeight);

            var background = new GameObject("Background").transform;
            background.SetParent(parent, false);

            // layer, horizontal factor, vertical factor, height offset
            BuildParallax(background, cam, level, 0, 0.85f, 0.9f, 3f, halfWidth, halfHeight, referenceCameraY, RuntimeArt.SortFar);
            BuildParallax(background, cam, level, 1, 0.6f, 0.75f, 0.5f, halfWidth, halfHeight, referenceCameraY, RuntimeArt.SortMid);
        }

        static void BuildParallax(Transform parent, Camera cam, LevelData level, int layer, float factorX, float factorY, float offsetY,
            float halfWidth, float halfHeight, float referenceCameraY, int sortingOrder)
        {
            var go = new GameObject("Parallax " + layer);
            go.transform.SetParent(parent, false);

            Sprite sprite = RuntimeArt.Silhouette(layer);
            float segment = sprite.bounds.size.x;
            float start = -halfWidth - segment;
            float end = level.Width * (1f - factorX) + halfWidth + segment;
            for (float x = start; x < end; x += segment)
                RuntimeArt.CreateSprite("Segment", go.transform, sprite, Color.white, sortingOrder, true, new Vector3(x, 0f, 0f));

            // Solid block under the silhouettes so nothing shows through when the camera goes low.
            SpriteRenderer fill = RuntimeArt.CreateSprite("Fill", go.transform, RuntimeArt.White, RuntimeArt.SilhouetteColor(layer), sortingOrder, true,
                new Vector3((start + end) * 0.5f, -15f + 0.05f, 0f));
            fill.transform.localScale = new Vector3(end - start, 30f, 1f);

            go.AddComponent<ParallaxLayer>().Setup(cam.transform, factorX, factorY, offsetY, referenceCameraY, halfHeight);
        }

        static float Hash01(int x, int y)
        {
            unchecked
            {
                int h = x * 73856093 ^ y * 19349663;
                h = (h ^ (h >> 13)) * 1274126177;
                return ((h ^ (h >> 16)) & 0xffff) / 65535f;
            }
        }
    }
}
