using UnityEngine;

namespace LumenWave
{
    /// <summary>
    /// Styles level blocks (installed as <see cref="LevelBuilder.TileStyler"/>): dark blue-grey stone with slight
    /// per-block variation, plus a thin faintly glowing "moss" strip on every exposed top edge so walkable
    /// surfaces always read clearly against the dark backdrop.
    /// </summary>
    public static class TileStyle
    {
        static readonly Color[] Stone =
        {
            new Color(0.045f, 0.05f, 0.065f),
            new Color(0.052f, 0.055f, 0.07f),
            new Color(0.04f, 0.046f, 0.06f),
        };

        public static void Style(GameObject block, char tile, LevelContext ctx)
        {
            var r = block.GetComponent<MeshRenderer>();
            var p = block.transform.localPosition;
            var s = block.transform.localScale;
            int variant = Mathf.Abs(Mathf.RoundToInt(p.x * 7f + p.y * 13f)) % Stone.Length;
            if (r != null)
            {
                r.sharedMaterial = MaterialLibrary.Tinted("Stone", tile == '=' ? Stone[0] * 1.25f : Stone[variant]);
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
                r.receiveShadows = true;
            }

            var mood = SceneRig.Instance != null ? SceneRig.Instance.Mood : Mood.Get(ctx?.data?.Get("mood", "blue"));
            float x0 = p.x - s.x * 0.5f, top = p.y + s.y * 0.5f;
            int cols = Mathf.RoundToInt(s.x);
            int topRow = Mathf.RoundToInt(top);
            // Exposed runs along the top: columns whose cell above is not solid.
            int runStart = -1;
            for (int i = 0; i <= cols; i++)
            {
                bool exposed = i < cols && (ctx == null || !LevelData.IsSolid(ctx.data.Tile(Mathf.RoundToInt(x0) + i, topRow)));
                if (exposed && runStart < 0) runStart = i;
                if (!exposed && runStart >= 0)
                {
                    Strip(block.transform.parent, x0 + runStart, x0 + i, top, s.z, mood);
                    runStart = -1;
                }
            }
        }

        static void Strip(Transform parent, float xa, float xb, float top, float depth, Mood mood)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = "EdgeGlow";
            Backdrop.RemoveCollider(go);
            go.transform.SetParent(parent, false);
            // A thin glowing line along the front top edge (facing the camera) – reads as moss / moonlit rim.
            go.transform.localPosition = new Vector3((xa + xb) * 0.5f, top - 0.02f, -depth * 0.5f - 0.01f);
            go.transform.localScale = new Vector3(xb - xa, 0.07f, 0.06f);
            var emission = mood.edge * 0.9f;
            go.GetComponent<MeshRenderer>().sharedMaterial = MaterialLibrary.Tinted("LitEmissive", mood.edge * 0.2f, emission);
            Backdrop.Shadowless(go);
        }
    }
}
