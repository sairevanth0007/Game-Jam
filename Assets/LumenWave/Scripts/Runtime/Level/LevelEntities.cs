using UnityEngine;

namespace LumenWave
{
    /// <summary>Factories for the level-owned entity types (exit, checkpoints, crystals, hidden platforms, orbs, ...).</summary>
    public static class LevelEntities
    {
        static readonly Color PortalColor = new Color(0.75f, 0.9f, 1f);
        static readonly Color CrystalColor = new Color(0.55f, 0.85f, 1f);
        static readonly Color OrbColor = new Color(1f, 0.85f, 0.45f);

        static GameObject Root(string name, EntitySpec s, LevelContext ctx)
        {
            var go = new GameObject(name);
            go.transform.SetParent(ctx.entities, false);
            go.transform.position = s.Position3;
            return go;
        }

        [LevelEntity("exit")]
        static GameObject Exit(EntitySpec s, LevelContext ctx)
        {
            var go = Root("Exit", s, ctx);
            var shadow = PropVisuals.Shadow;
            // Two leaning shadow pillars and a lintel framing a glowing veil.
            PropVisuals.Box("PillarL", go.transform, new Vector3(-0.85f, 1.4f, 0f), new Vector3(0.35f, 2.8f, 0.6f), shadow);
            PropVisuals.Box("PillarR", go.transform, new Vector3(0.85f, 1.4f, 0f), new Vector3(0.35f, 2.8f, 0.6f), shadow);
            PropVisuals.Box("Lintel", go.transform, new Vector3(0f, 2.9f, 0f), new Vector3(2.3f, 0.3f, 0.7f), shadow);
            var veil = PropVisuals.Box("Veil", go.transform, new Vector3(0f, 1.35f, 0.1f), new Vector3(1.2f, 2.2f, 0.05f), PropVisuals.Emissive(PortalColor, 4f));
            var l = PropVisuals.PointLight(go.transform, new Vector3(0f, 1.4f, -1f), PortalColor, 8f, 3f);
            var p = go.AddComponent<ExitPortal>();
            p.Init(l, veil.transform);
            return go;
        }

        [LevelEntity("checkpoint")]
        static GameObject Checkpoint(EntitySpec s, LevelContext ctx)
        {
            var go = Root("Checkpoint", s, ctx);
            var shadow = PropVisuals.Shadow;
            PropVisuals.Box("Post", go.transform, new Vector3(0f, 0.8f, 0.3f), new Vector3(0.12f, 1.6f, 0.12f), shadow);
            PropVisuals.Box("Arm", go.transform, new Vector3(0.18f, 1.6f, 0.3f), new Vector3(0.45f, 0.08f, 0.08f), shadow);
            var core = PropVisuals.MeshObject("Lantern", go.transform, PropVisuals.Octahedron(0.16f), PropVisuals.Emissive(new Color(0.3f, 0.35f, 0.45f), 0.5f));
            core.transform.localPosition = new Vector3(0.38f, 1.32f, 0.3f);
            var l = PropVisuals.PointLight(go.transform, new Vector3(0.38f, 1.4f, -0.5f), new Color(0.4f, 0.45f, 0.6f), 6f, 0.15f);
            var c = go.AddComponent<CheckpointLantern>();
            c.index = s.Has("index") ? s.Int("index") : Mathf.RoundToInt(s.pos.x * 100f + s.pos.y);
            c.Init(l, core.GetComponent<Renderer>());
            return go;
        }

        [LevelEntity("thorns")]
        static GameObject ThornsFactory(EntitySpec s, LevelContext ctx)
        {
            var go = Root("Thorns", s, ctx);
            float w = s.Float("w", 1f);
            var mesh = PropVisuals.MeshObject("Spikes", go.transform, PropVisuals.Spikes(w, 0.7f, 1.2f, Mathf.Max(2, Mathf.RoundToInt(w * 3))), PropVisuals.Shadow);
            mesh.transform.localPosition = Vector3.zero;
            go.layer = Layers.Hazard;
            var t = go.AddComponent<Thorns>();
            t.size = new Vector2(w * 0.85f, 0.5f);
            return go;
        }

        [LevelEntity("crystal")]
        static GameObject Crystal(EntitySpec s, LevelContext ctx)
        {
            // Grid crystals occupy one full tile (pos is bottom-centre of the cell).
            var go = Root("Crystal", s, ctx);
            go.layer = Layers.Crystal;
            float h = s.Float("h", 1f);
            go.transform.position = s.Position3 + Vector3.up * h * 0.5f;
            var col = go.AddComponent<BoxCollider>();
            col.size = new Vector3(1f, h, 2f);
            var visual = new GameObject("Visual");
            visual.transform.SetParent(go.transform, false);
            var mat = PropVisuals.Emissive(CrystalColor * 0.6f, 2.2f);
            for (int i = 0; i < 3; i++)
            {
                var g = PropVisuals.MeshObject("Shard" + i, visual.transform, PropVisuals.Octahedron(0.22f + 0.06f * i, 2.0f * h), mat);
                g.transform.localPosition = new Vector3(-0.25f + 0.25f * i, -h * 0.5f + 0.45f * h, (i - 1) * 0.15f);
                g.transform.localRotation = Quaternion.Euler(0, 20 * i, (i - 1) * 14f);
            }
            var light = PropVisuals.PointLight(go.transform, new Vector3(0, 0, -0.8f), CrystalColor, 3.5f, 1f);
            var shards = MakeShards(go.transform, CrystalColor);
            go.AddComponent<LightCrystal>().Init(col, visual, light, shards);
            return go;
        }

        static ParticleSystem MakeShards(Transform parent, Color c)
        {
            var go = new GameObject("Shards");
            go.transform.SetParent(parent, false);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.playOnAwake = false;
            main.loop = false;
            main.duration = 0.5f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.5f, 1.1f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(3f, 8f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.06f, 0.18f);
            main.startColor = c * 2f;
            main.gravityModifier = 1.2f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            var em = ps.emission;
            em.rateOverTime = 0f;
            em.SetBursts(new[] { new ParticleSystem.Burst(0f, 40) });
            var sh = ps.shape;
            sh.shapeType = ParticleSystemShapeType.Box;
            sh.scale = new Vector3(0.8f, 0.8f, 0.3f);
            var r = go.GetComponent<ParticleSystemRenderer>();
            r.sharedMaterial = EnemyUtil.GlowMaterial(Color.white);   // soft glints (spinning glass shards come from ImpactFx)
            return ps;
        }

        [LevelEntity("hidden")]
        static GameObject Hidden(EntitySpec s, LevelContext ctx)
        {
            var go = Root("HiddenPlatform", s, ctx);
            float w = s.Float("w", 1f);
            // Grid cells: pos is bottom-centre of the cell; platform sits at the top 0.35 of the cell.
            go.transform.position = s.Position3 + new Vector3(0f, 1f - 0.175f, 0f);
            go.layer = Layers.Trigger;
            var trigger = go.AddComponent<BoxCollider>();
            trigger.isTrigger = true;
            trigger.size = new Vector3(w, 1.2f, 2f);
            var solid = PropVisuals.Box("Solid", go.transform, Vector3.zero, new Vector3(w, 0.35f, 2f), MaterialLibrary.Create("UnlitTransparent"), true);
            solid.layer = Layers.Ground;
            var col = solid.GetComponent<Collider>();
            col.enabled = false;
            var hp = go.AddComponent<HiddenPlatform>();
            hp.revealTime = s.Float("time", 6f);
            hp.Init(col, solid.GetComponent<Renderer>());
            return go;
        }

        [LevelEntity("tutorial")]
        static GameObject Tutorial(EntitySpec s, LevelContext ctx)
        {
            var go = Root("Tutorial", s, ctx);
            var t = go.AddComponent<TutorialZone>();
            t.text = s.Text("text", "");
            t.radius = s.Float("radius", 2.5f);
            return go;
        }

        static LevelContext _orbCtx;

        [LevelEntity("orb")]
        static GameObject Orb(EntitySpec s, LevelContext ctx)
        {
            if (_orbCtx != ctx) { _orbCtx = ctx; Secrets.ResetLevel(); }
            Secrets.InLevel++;
            var go = Root("SecretOrb", s, ctx);
            go.transform.position = s.Position3 + Vector3.up * 0.6f;
            var gem = PropVisuals.MeshObject("Gem", go.transform, PropVisuals.Octahedron(0.18f, 1.5f), PropVisuals.Emissive(OrbColor, 5f));
            PropVisuals.PointLight(go.transform, new Vector3(0, 0, -0.5f), OrbColor, 3f, 1.2f);
            go.AddComponent<SecretOrb>().Init(gem.transform);
            return go;
        }

        [LevelEntity("ending")]
        static GameObject Ending(EntitySpec s, LevelContext ctx)
        {
            var go = Root("Finale", s, ctx);
            var f = go.AddComponent<FinaleTrigger>();
            f.duration = s.Float("time", 11f);
            return go;
        }

        [LevelEntity("bot")]
        static GameObject Bot(EntitySpec s, LevelContext ctx)
        {
            var go = Root("BotMarker", s, ctx);
            var m = go.AddComponent<BotMarker>();
            m.action = s.Str("do", "jump").ToLowerInvariant();
            m.duration = s.Float("t", 0f);
            m.delay = s.Float("delay", 0f);
            m.anyHeight = s.Bool("any");
            ctx.byType.TryGetValue("bot", out var list);
            m.order = list != null ? list.Count : 0;
            return go;
        }
    }
}
