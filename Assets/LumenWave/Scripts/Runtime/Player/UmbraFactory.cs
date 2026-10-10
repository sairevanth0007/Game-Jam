using UnityEngine;

namespace LumenWave
{
    /// <summary>Spawns Umbra for the "player" level entity (grid char 'P').</summary>
    public static class UmbraFactory
    {
        [LevelEntity("player")]
        static GameObject Build(EntitySpec spec, LevelContext ctx)
        {
            var start = new Vector3(spec.pos.x, spec.pos.y + 0.05f, 0f);
            return Spawn(start, ctx != null ? ctx.entities : null);
        }

        public static GameObject Spawn(Vector3 position, Transform parent = null)
        {
            var go = new GameObject("Umbra");
            go.layer = Layers.Player;
            go.tag = "Player";
            if (parent != null) go.transform.SetParent(parent, false);
            position.z = 0f;
            go.transform.position = position;

            var rb = go.AddComponent<Rigidbody>();
            rb.isKinematic = true;
            rb.useGravity = false;
            rb.interpolation = RigidbodyInterpolation.None;

            go.AddComponent<PlayerHealth>();
            go.AddComponent<Resonance>();
            var box = go.AddComponent<BoxCollider>(); // before the controller so its Awake finds it
            var ctl = go.AddComponent<UmbraController>();
            box.size = new Vector3(ctl.width, ctl.height, 0.6f);
            box.center = new Vector3(0f, ctl.height * 0.5f, 0f);

            var rig = go.AddComponent<UmbraRig>();
            rig.Build(ctl);

            var scarfGo = new GameObject("Scarf");
            scarfGo.layer = Layers.Player;
            scarfGo.transform.SetParent(go.transform, false);
            var scarf = scarfGo.AddComponent<UmbraScarf>();
            scarf.Init(rig.Neck, ctl);

            if (LevelRuntime.Current != null) LevelRuntime.Current.Player = go;
            GameEvents.RaisePlayerSpawned(go);
            return go;
        }
    }
}
