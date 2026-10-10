using UnityEngine;

namespace LumenWave.Demo
{
    [DefaultExecutionOrder(50)]
    public sealed class DemoSession : MonoBehaviour
    {
        [SerializeField] ChordMotor player;
        [SerializeField] DemoLightField[] lights;
        [SerializeField] Vector2 spawn = new Vector2(-4.8f, .5f);
        [SerializeField] float blueDamage = 32;
        [SerializeField] float greenHealing = 24;
        [SerializeField] float deathY = -4;
        ChordInput input;
        Collider2D playerCollider;
        Vector2 checkpoint;
        float respawnAt;
        public float Health { get; private set; } = 100;
        public bool Completed { get; private set; }
        public bool Recovering { get; private set; }
        public int Deaths { get; private set; }
        public string Message { get; private set; } = "Climb the watchtower. Play a chord to move the light.";
        public DemoLightField[] Lights => lights;
        public ChordMotor Player => player;
        public void Configure(ChordMotor motor, DemoLightField[] fields, Vector2 start)
        { player = motor; lights = fields; spawn = start; }
        void Awake()
        {
            checkpoint = spawn;
            input = player.GetComponent<ChordInput>();
            playerCollider = player.GetComponent<Collider2D>();
        }
        void Update()
        {
            if (input.RestartPressed) { if (Completed) checkpoint = spawn; Respawn(); return; }
            if (Completed) return;
            if (Recovering) { if (Time.time >= respawnAt) Respawn(); return; }
            if (player.transform.position.y < deathY) { Fail("Fell into the dark. Returning to safety..."); return; }
            bool blue = false, green = false;
            foreach (var light in lights)
            {
                if (light == null || !light.Overlaps(playerCollider.bounds)) continue;
                if (light.Kind == LightColorType.Red) { Fail("Red light consumes shadow instantly."); return; }
                blue |= light.Kind == LightColorType.Blue;
                green |= light.Kind == LightColorType.Green;
            }
            if (blue) { Health = Mathf.Max(0, Health - blueDamage * Time.deltaTime); Message = "Blue light burns! Complete a movement pair to bend it."; }
            else if (green) { Health = Mathf.Min(100, Health + greenHealing * Time.deltaTime); Message = "Green light restores your shadow."; }
            if (Health <= 0) Fail("Your shadow faded. Returning to safety...");
        }
        void Fail(string reason)
        { Deaths++; Recovering = true; player.Frozen = true; respawnAt = Time.time + .65f; Health = 0; Message = reason; }
        public void Respawn()
        {
            Health = 100; Completed = Recovering = false; player.Frozen = false;
            player.Teleport(checkpoint); Message = "Back in safety. Release and press a chord to renew its effect.";
        }
        public void Checkpoint(Vector2 position)
        { checkpoint = position; Message = "Checkpoint reached. Your shadow can rest here."; }
        public void Complete()
        { Completed = true; player.Frozen = true; Message = "TOWER CLEARED  /  Press R to play again"; }
    }
}
