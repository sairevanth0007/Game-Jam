using UnityEngine;

namespace LumenWave
{
    /// <summary>
    /// Styles level blocks (installed as <see cref="LevelBuilder.TileStyler"/>). The block keeps its BoxCollider
    /// (gameplay is unchanged); its cube renderer is hidden and replaced by a bevelled rock body plus a
    /// themed top cap (moss / grass / snow / paving) with an overhanging lip on every exposed top run
    /// (see <see cref="PlatformVisual"/>), so walkable surfaces read clearly against the dark backdrop.
    /// </summary>
    public static class TileStyle
    {
        /// <summary>
        /// Distance (world units, capped at 6) from a point inside solid ground to the nearest exposed side or
        /// underside (an empty cell left/right in the same row, or below in the same column). Level borders and
        /// the level bottom don't count as exposed.
        /// </summary>
        public static System.Func<float, float, float> EdgeDistance(LevelData d)
        {
            return (x, y) =>
            {
                int col = Mathf.Clamp(Mathf.FloorToInt(x), 0, d.Width - 1);
                int row = Mathf.FloorToInt(y - 0.02f);
                float best = 6f;
                int e = col;
                while (col - e < 6 && e - 1 >= 0 && LevelData.IsSolid(d.Tile(e - 1, row))) e--;
                if (col - e < 6 && e - 1 >= 0) best = Mathf.Min(best, x - e);
                e = col;
                while (e - col < 6 && e + 1 < d.Width && LevelData.IsSolid(d.Tile(e + 1, row))) e++;
                if (e - col < 6 && e + 1 < d.Width) best = Mathf.Min(best, e + 1 - x);
                int r = row;
                while (row - r < 6 && r - 1 >= 0 && LevelData.IsSolid(d.Tile(col, r - 1))) r--;
                if (row - r < 6 && r - 1 >= 0) best = Mathf.Min(best, y - r);
                return Mathf.Max(0f, best);
            };
        }

        public static void Style(GameObject block, char tile, LevelContext ctx)
        {
            var mood = SceneRig.Instance != null ? SceneRig.Instance.Mood : Mood.Get(ctx?.data?.Get("mood", "blue"));
            var theme = Theme.For(ctx?.data);
            bool ledge = tile == '=';
            var p = block.transform.localPosition;
            var s = block.transform.localScale;
            float top = p.y + s.y * 0.5f;
            int x0 = Mathf.RoundToInt(p.x - s.x * 0.5f);
            int cols = Mathf.Max(1, Mathf.RoundToInt(s.x));
            int topRow = Mathf.RoundToInt(top);

            // Walkable surface above each column (for the "sinks into darkness" gradient).
            var surf = new float[cols];
            for (int i = 0; i < cols; i++)
            {
                int y = topRow;
                if (ctx != null) while (y < ctx.data.Height + 1 && LevelData.IsSolid(ctx.data.Tile(x0 + i, y))) y++;
                surf[i] = y;
            }

            var r = block.GetComponent<MeshRenderer>();
            if (r != null) r.enabled = false;
            var parent = block.transform.parent;
            PlatformVisual.Body(parent, p, s, ledge, surf, x0, PlatformVisual.BodyMaterial(theme, mood), ctx != null ? EdgeDistance(ctx.data) : null);

            // Exposed runs along the top: columns whose cell above is not solid.
            var mb = new MeshBuilderN();
            var rng = new System.Random(x0 * 73856093 ^ topRow * 19349663);
            int runStart = -1;
            for (int i = 0; i <= cols; i++)
            {
                bool exposed = i < cols && (ctx == null || !LevelData.IsSolid(ctx.data.Tile(x0 + i, topRow)));
                if (exposed && runStart < 0) runStart = i;
                if (!exposed && runStart >= 0)
                {
                    int xa = x0 + runStart, xb = x0 + i;
                    bool openL = ctx == null || !LevelData.IsSolid(ctx.data.Tile(xa - 1, topRow - 1)) && !LevelData.IsSolid(ctx.data.Tile(xa - 1, topRow));
                    bool openR = ctx == null || !LevelData.IsSolid(ctx.data.Tile(xb, topRow - 1)) && !LevelData.IsSolid(ctx.data.Tile(xb, topRow));
                    PlatformVisual.Cap(mb, xa, xb, top, s.z, openL, openR, theme.capKind, ledge, rng);
                    runStart = -1;
                }
            }
            // Exposed ceiling runs (cell below empty): sparse hanging roots / moss / icicles.
            float bottom = p.y - s.y * 0.5f;
            int belowRow = Mathf.RoundToInt(bottom) - 1;
            if (ctx != null && bottom > 0.5f)
            {
                runStart = -1;
                for (int i = 0; i <= cols; i++)
                {
                    bool open = i < cols && ctx.data.Tile(x0 + i, belowRow) == ' ';
                    if (open && runStart < 0) runStart = i;
                    if (!open && runStart >= 0)
                    {
                        PlatformVisual.Underside(mb, x0 + runStart, x0 + i, bottom, s.z, theme.capKind, rng);
                        runStart = -1;
                    }
                }
            }
            if (mb.Count > 0)
            {
                var go = new GameObject("TopCap");
                go.transform.SetParent(parent, false);
                go.AddComponent<MeshFilter>().sharedMesh = mb.ToMesh("TopCap");
                var mr = go.AddComponent<MeshRenderer>();
                mr.sharedMaterial = PlatformVisual.CapMaterial(theme, mood);
                mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
                mr.receiveShadows = true;
            }
        }
    }
}
