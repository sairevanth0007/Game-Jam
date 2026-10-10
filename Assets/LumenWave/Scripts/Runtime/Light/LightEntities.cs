using UnityEngine;

namespace LumenWave
{
    /// <summary>Level-file factories for the light system: beam, lamp, mirror, prism.</summary>
    public static class LightEntities
    {
        static void ApplyLevelFog(LevelContext ctx)
        {
            if (ctx?.data != null) LightField.FogSigma = ctx.data.GetFloat("fog", 0.04f);
        }

        /// <summary>beam x y color=red dir=-90 range=16 intensity=1 width=0.6 [beat=1] [sweep=deg speed=cps] [fixture=0]</summary>
        [LevelEntity("beam")]
        static GameObject Beam(EntitySpec s, LevelContext ctx)
        {
            ApplyLevelFog(ctx);
            var go = new GameObject("Beam_" + s.Color());
            go.transform.position = s.Position3;
            var b = go.AddComponent<LightBeam>();
            b.color = s.Color("color", LightColor.White);
            b.SetBaseDirection(s.Dir("dir", -90f));
            b.range = s.Float("range", 16f);
            b.intensity = s.Float("intensity", 1f);
            b.width = s.Float("width", 0.6f);
            b.beatGated = s.Bool("beat", b.color == LightColor.Yellow);
            b.sweepDegrees = s.Float("sweep", 0f);
            b.sweepSpeed = s.Float("speed", 0.25f);
            b.showFixture = s.Bool("fixture", true);
            return go;
        }

        /// <summary>lamp x y color=green radius=5 intensity=1 [beat=1] [hanging=0]</summary>
        [LevelEntity("lamp")]
        static GameObject Lamp(EntitySpec s, LevelContext ctx)
        {
            ApplyLevelFog(ctx);
            var go = new GameObject("Lamp_" + s.Color("color", LightColor.Green));
            go.transform.position = s.Position3;
            var l = go.AddComponent<AreaLamp>();
            l.color = s.Color("color", LightColor.Green);
            l.radius = s.Float("radius", 5f);
            l.intensity = s.Float("intensity", 1f);
            l.beatGated = s.Bool("beat", false);
            l.BuildVisuals(s.Bool("hanging", true));
            return go;
        }

        /// <summary>mirror x y angle=45 [rotatable=1]</summary>
        [LevelEntity("mirror")]
        static GameObject MirrorEntity(EntitySpec s, LevelContext ctx)
        {
            var go = new GameObject("Mirror");
            go.transform.position = s.Position3;
            var m = go.AddComponent<Mirror>();
            m.angle = s.Float("angle", 45f);
            m.rotatable = s.Bool("rotatable", false);
            m.step = s.Float("step", 45f);
            m.Build();
            return go;
        }

        /// <summary>prism x y angle=0 [spread=18] [rotatable=1]</summary>
        [LevelEntity("prism")]
        static GameObject PrismEntity(EntitySpec s, LevelContext ctx)
        {
            var go = new GameObject("Prism");
            go.transform.position = s.Position3;
            var p = go.AddComponent<Prism>();
            p.angle = s.Float("angle", 0f);
            p.spread = s.Float("spread", 18f);
            p.rotatable = s.Bool("rotatable", false);
            p.step = s.Float("step", 45f);
            p.Build();
            return go;
        }
    }
}
