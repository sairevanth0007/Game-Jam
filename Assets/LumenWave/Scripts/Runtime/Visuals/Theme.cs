using UnityEngine;

namespace LumenWave
{
    public enum CapKind { Moss, Grass, Snow, Dust, Paving }
    public enum MoteKind { Dust, Fireflies, Embers, Snow, Sparkles }

    /// <summary>
    /// Environment theme of a level (header line <c>theme: forest|canyon|ruins|ice|garden|clockwork|prism|fortress|dawn</c>;
    /// defaults from the mood). Picks rock/cap textures for the playfield, scenery models for the depth layers,
    /// the sky panorama, foreground silhouettes and ambient particles. Purely visual.
    /// </summary>
    public class Theme
    {
        public string name;
        // Playfield
        public string rock = "Rock035", cap = "Moss002";
        public Color rockTint = new Color(0.264f, 0.288f, 0.336f), capTint = new Color(0.173f, 0.230f, 0.173f);
        public CapKind capKind = CapKind.Moss;
        public float rockScale = 6f, wet = 0.55f;
        // Dressing on platform tops (small, no colliders)
        public string[] topProps = { "KenneyNature/grass", "KenneyNature/grass_large", "KenneyNature/rock_smallA", "KenneyNature/rock_smallB", "KenneyNature/mushroom_tanGroup", "KenneyNature/plant_bush" };
        public Color topPropTint = new Color(0.12f, 0.16f, 0.12f);
        // Depth layers: near-mid (z ~ 6-10), mid (z ~ 16-24), far (z ~ 34-50)
        public string[] nearProps = { "KenneyNature/tree_default_dark", "KenneyNature/tree_oak_dark", "KenneyNature/tree_detailed_dark", "KenneyNature/rock_tallA", "KenneyNature/stump_oldTall", "KenneyNature/tree_fat_darkh" };
        public string[] midProps = { "KenneyNature/tree_pineRoundA", "KenneyNature/tree_pineDefaultA", "KenneyNature/tree_cone_dark", "KenneyNature/tree_default_dark", "KenneyNature/rock_tallB", "KenneyNature/statue_columnDamaged" };
        public string[] farProps = { "KenneyNature/rock_largeA", "KenneyNature/tree_pineDefaultA", "KenneyNature/tree_pineRoundA", "KenneyNature/rock_tallC" };
        public float nearHeight = 9f, midHeight = 13f, farHeight = 24f;
        public float nearSpacing = 5.5f, midSpacing = 6.5f, farSpacing = 11f;
        // Sky
        public string sky = "kloppenheim_02_puresky";
        public float skyExposure = 0.9f;
        public float stars = 1f;
        // Atmosphere
        public MoteKind motes = MoteKind.Dust;
        public float godRays = 1f;
        public bool gears, crystals;
        public Color crystalColor = new Color(0.3f, 1f, 0.6f);
        public float iridescence = 0.3f;
        public bool foregroundChains, foregroundVines = true, foregroundGrass = true, foregroundRuins;

        public static string Default(string mood)
        {
            switch ((mood ?? "").ToLowerInvariant())
            {
                case "violet": return "canyon";
                case "red": return "ruins";
                case "green": return "garden";
                case "gold": return "clockwork";
                case "dawn": return "dawn";
                case "ice": return "ice";
                case "prism": return "prism";
                default: return "forest";
            }
        }

        public static Theme Get(string name)
        {
            var t = new Theme { name = (name ?? "forest").ToLowerInvariant() };
            switch (t.name)
            {
                case "canyon":
                    t.rock = "Rock051"; t.cap = "Ground037"; t.capKind = CapKind.Dust;
                    t.rockTint = new Color(0.298f, 0.240f, 0.316f); t.capTint = new Color(0.192f, 0.162f, 0.192f);
                    t.rockScale = 5f; t.wet = 0.25f;
                    t.topProps = new[] { "KenneyNature/rock_smallA", "KenneyNature/rock_smallC", "KenneyNature/rock_smallFlatA", "KenneyNature/grass", "KenneyNature/stone_smallA", "KenneyNature/stone_smallB" };
                    t.topPropTint = new Color(0.16f, 0.12f, 0.17f);
                    t.nearProps = new[] { "KenneyNature/rock_tallA", "KenneyNature/rock_tallB", "KenneyNature/rock_tallC", "KenneyNature/stone_tallA", "KenneyNature/tree_blocks_dark" };
                    t.midProps = new[] { "KenneyNature/rock_tallC", "KenneyNature/rock_tallD", "KenneyNature/stone_tallB", "KenneyNature/rock_largeD", "KenneyNature/statue_obelisk" };
                    t.farProps = new[] { "KenneyNature/rock_largeA", "KenneyNature/rock_largeC", "KenneyNature/rock_tallC" };
                    t.nearHeight = 10f; t.midHeight = 16f; t.farHeight = 30f; t.farSpacing = 9f;
                    t.sky = "kloppenheim_02_puresky"; t.motes = MoteKind.Sparkles;
                    t.foregroundVines = false; t.foregroundRuins = true;
                    break;
                case "ruins":
                    t.rock = "Bricks066"; t.cap = "PavingStones131"; t.capKind = CapKind.Paving;
                    t.rockTint = new Color(0.298f, 0.230f, 0.226f); t.capTint = new Color(0.173f, 0.138f, 0.131f);
                    t.rockScale = 3f; t.wet = 0.3f;
                    t.topProps = new[] { "KenneyGraveyard/debris", "KenneyGraveyard/rocks", "KenneyNature/rock_smallB", "KenneyGraveyard/gravestone-broken", "KenneyNature/grass", "KenneyGraveyard/urn-round" };
                    t.topPropTint = new Color(0.14f, 0.09f, 0.09f);
                    t.nearProps = new[] { "KenneyGraveyard/pillar-large", "KenneyGraveyard/pine-crooked", "KenneyNature/statue_columnDamaged", "KenneyGraveyard/pillar-obelisk", "KenneyGraveyard/column-large" };
                    t.midProps = new[] { "KenneyCastle/wall-half", "KenneyGraveyard/crypt-large", "KenneyNature/statue_column", "KenneyGraveyard/pine-fall-crooked", "KenneyCastle/tower-base", "KenneyNature/statue_ring" };
                    t.farProps = new[] { "KenneyCastle/tower-square", "KenneyCastle/tower-hexagon-base", "KenneyNature/rock_largeC", "KenneyCastle/wall" };
                    t.nearHeight = 8f; t.midHeight = 12f; t.farHeight = 26f;
                    t.sky = "kloppenheim_07_puresky"; t.stars = 0.3f; t.motes = MoteKind.Embers;
                    t.foregroundRuins = true; t.foregroundChains = true; t.foregroundVines = false;
                    break;
                case "fortress":
                    t.rock = "Bricks066"; t.cap = "Rock035"; t.capKind = CapKind.Paving;
                    t.rockTint = new Color(0.264f, 0.216f, 0.216f); t.capTint = new Color(0.162f, 0.138f, 0.138f);
                    t.rockScale = 2.6f; t.wet = 0.35f;
                    t.topProps = new[] { "KenneyGraveyard/debris", "KenneyGraveyard/rocks", "KenneyNature/rock_smallA", "KenneyGraveyard/iron-fence-damaged", "KenneyGraveyard/lantern-candle" };
                    t.topPropTint = new Color(0.12f, 0.08f, 0.08f);
                    t.nearProps = new[] { "KenneyCastle/wall-pillar", "KenneyGraveyard/pillar-square", "KenneyGraveyard/lightpost-single", "KenneyCastle/flag-banner-long" };
                    t.midProps = new[] { "KenneyCastle/tower-square", "KenneyCastle/tower-hexagon-base", "KenneyCastle/wall", "KenneyCastle/gate", "KenneyCastle/tower-base" };
                    t.farProps = new[] { "KenneyCastle/tower-square", "KenneyCastle/tower-hexagon-base" };
                    t.nearHeight = 9f; t.midHeight = 16f; t.farHeight = 32f; t.midSpacing = 12f; t.farSpacing = 14f;
                    t.sky = "kloppenheim_07_puresky"; t.stars = 0.2f; t.motes = MoteKind.Embers;
                    t.foregroundRuins = true; t.foregroundChains = true; t.foregroundVines = false;
                    break;
                case "ice":
                    t.rock = "Rock035"; t.cap = "Ice002"; t.capKind = CapKind.Snow;
                    t.rockTint = new Color(0.240f, 0.288f, 0.360f); t.capTint = new Color(0.288f, 0.326f, 0.384f);
                    t.rockScale = 6f; t.wet = 0.8f;
                    t.topProps = new[] { "KenneyNature/rock_smallA", "KenneyNature/rock_smallFlatA", "KenneyNature/stone_smallA", "KenneyNature/rock_smallC" };
                    t.topPropTint = new Color(0.16f, 0.2f, 0.26f);
                    t.nearProps = new[] { "KenneyNature/rock_tallA", "KenneyNature/rock_tallC", "KenneyNature/tree_pineGroundA", "KenneyNature/stone_tallA" };
                    t.midProps = new[] { "KenneyNature/tree_pineDefaultA", "KenneyNature/rock_tallD", "KenneyNature/rock_tallC", "KenneyNature/tree_pineRoundA" };
                    t.farProps = new[] { "KenneyNature/tree_pineDefaultA", "KenneyNature/rock_tallD", "KenneyNature/tree_pineRoundA", "KenneyNature/rock_tallB" };
                    t.farHeight = 30f;
                    t.sky = "kloppenheim_02_puresky"; t.motes = MoteKind.Snow; t.crystals = true;
                    t.crystalColor = new Color(0.45f, 0.75f, 1.2f); t.iridescence = 0.25f;
                    t.foregroundVines = false;
                    break;
                case "garden":
                    t.rock = "Rock030"; t.cap = "Moss002"; t.capKind = CapKind.Grass;
                    t.rockTint = new Color(0.240f, 0.264f, 0.240f); t.capTint = new Color(0.173f, 0.288f, 0.173f);
                    t.wet = 0.6f;
                    t.topProps = new[] { "KenneyNature/grass_large", "KenneyNature/grass_leafs", "KenneyNature/plant_bushDetailed", "KenneyNature/mushroom_tanGroup", "KenneyNature/plant_flatTall", "KenneyNature/grass_leafsLarge" };
                    t.topPropTint = new Color(0.1f, 0.2f, 0.12f);
                    t.nearProps = new[] { "KenneyNature/tree_oak_dark", "KenneyNature/tree_detailed_dark", "KenneyNature/plant_bushLarge", "KenneyNature/tree_fat_darkh", "KenneyNature/statue_column" };
                    t.midProps = new[] { "KenneyNature/tree_default_dark", "KenneyNature/tree_oak_dark", "KenneyNature/statue_columnDamaged", "KenneyNature/statue_head", "KenneyNature/tree_fat_darkh" };
                    t.farProps = new[] { "KenneyNature/tree_oak_dark", "KenneyNature/rock_largeA", "KenneyNature/tree_detailed_dark" };
                    t.sky = "kloppenheim_02_puresky"; t.motes = MoteKind.Fireflies; t.crystals = true;
                    t.crystalColor = new Color(0.25f, 1.1f, 0.65f); t.iridescence = 0.35f;
                    break;
                case "clockwork":
                    t.rock = "Bricks066"; t.cap = "PavingStones131"; t.capKind = CapKind.Paving;
                    t.rockTint = new Color(0.298f, 0.250f, 0.182f); t.capTint = new Color(0.192f, 0.162f, 0.115f);
                    t.rockScale = 2.8f; t.wet = 0.45f;
                    t.topProps = new[] { "KenneyGraveyard/debris", "KenneyNature/rock_smallA", "KenneyGraveyard/urn-round", "KenneyGraveyard/lantern-candle" };
                    t.topPropTint = new Color(0.16f, 0.12f, 0.07f);
                    t.nearProps = new[] { "KenneyGraveyard/pillar-large", "KenneyCastle/wall-pillar", "KenneyNature/statue_column", "KenneyGraveyard/lightpost-double" };
                    t.midProps = new[] { "KenneyCastle/tower-hexagon-base", "KenneyCastle/tower-square-mid-windows", "KenneyNature/statue_obelisk", "KenneyCastle/wall-doorway" };
                    t.farProps = new[] { "KenneyCastle/tower-hexagon-base", "KenneyCastle/tower-square", "KenneyNature/statue_obelisk" };
                    t.nearSpacing = 9f; t.midHeight = 11f; t.farHeight = 26f;
                    t.sky = "kloppenheim_07_puresky"; t.stars = 0.4f; t.motes = MoteKind.Dust; t.gears = true;
                    t.foregroundChains = true; t.foregroundVines = false; t.foregroundGrass = false; t.foregroundRuins = true;
                    break;
                case "prism":
                    t.rock = "Rock035"; t.cap = "Rock035"; t.capKind = CapKind.Dust;
                    t.rockTint = new Color(0.202f, 0.192f, 0.264f); t.capTint = new Color(0.192f, 0.184f, 0.238f);
                    t.rockScale = 6f; t.wet = 0.9f;
                    t.topProps = new[] { "KenneyNature/rock_smallA", "KenneyNature/stone_smallB", "KenneyNature/rock_smallFlatA" };
                    t.topPropTint = new Color(0.1f, 0.09f, 0.14f);
                    t.nearProps = new[] { "KenneyNature/statue_column", "KenneyNature/rock_tallB", "KenneyNature/statue_ring", "KenneyNature/stone_tallA" };
                    t.midProps = new[] { "KenneyNature/statue_columnDamaged", "KenneyNature/statue_obelisk", "KenneyNature/rock_tallD", "KenneyNature/statue_column" };
                    t.farProps = new[] { "KenneyNature/rock_largeC", "KenneyNature/statue_obelisk", "KenneyNature/rock_largeD" };
                    t.nearSpacing = 8f;
                    t.sky = "kloppenheim_02_puresky"; t.motes = MoteKind.Sparkles; t.crystals = true;
                    t.crystalColor = new Color(0.7f, 0.6f, 1.2f); t.iridescence = 1.2f;
                    t.foregroundVines = false; t.foregroundGrass = false; t.foregroundRuins = true;
                    break;
                case "dawn":
                    t.rock = "Rock030"; t.cap = "Ground037"; t.capKind = CapKind.Grass;
                    t.rockTint = new Color(0.316f, 0.268f, 0.250f); t.capTint = new Color(0.238f, 0.238f, 0.154f);
                    t.wet = 0.35f;
                    t.topProps = new[] { "KenneyNature/grass_large", "KenneyNature/grass", "KenneyNature/plant_flatTall", "KenneyNature/rock_smallA", "KenneyNature/grass_leafs" };
                    t.topPropTint = new Color(0.2f, 0.18f, 0.12f);
                    t.nearProps = new[] { "KenneyNature/tree_pineRoundA", "KenneyNature/statue_column", "KenneyNature/tree_default_dark", "KenneyNature/statue_ring" };
                    t.midProps = new[] { "KenneyNature/tree_pineDefaultA", "KenneyNature/statue_obelisk", "KenneyNature/tree_cone_dark", "KenneyNature/statue_columnDamaged" };
                    t.farProps = new[] { "KenneyNature/rock_largeA", "KenneyNature/tree_pineRoundA", "KenneyNature/rock_largeC" };
                    t.sky = "kloppenheim_07_puresky"; t.skyExposure = 1.6f; t.stars = 0.15f; t.motes = MoteKind.Dust; t.godRays = 1.6f;
                    break;
                default:
                    t.name = "forest";
                    t.motes = MoteKind.Fireflies;
                    break;
            }
            return t;
        }

        public static Theme For(LevelData data)
        {
            if (data == null) return Get("forest");
            return Get(data.Get("theme", Default(data.Get("mood", "blue"))));
        }
    }
}
