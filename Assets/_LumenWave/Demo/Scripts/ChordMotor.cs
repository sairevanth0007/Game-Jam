using UnityEngine;

namespace LumenWave.Demo
{
    [RequireComponent(typeof(Rigidbody2D), typeof(CapsuleCollider2D), typeof(ChordInput))]
    public sealed class ChordMotor : MonoBehaviour
    {
        [SerializeField] float speed = 4.8f;
        [SerializeField] float acceleration = 35;
        [SerializeField] float jumpSpeed = 10.8f;
        [SerializeField] float graceSeconds = .12f;
        [SerializeField] Transform eyes;
        readonly ContactPoint2D[] contacts = new ContactPoint2D[12];
        Rigidbody2D body;
        ChordInput input;
        float coyote, buffer;
        bool separating;
        public bool Frozen { get; set; }
        public bool Grounded { get; private set; }
        public Rigidbody2D Body => body;
        public void Configure(Transform eyeRoot) { eyes = eyeRoot; }
        void Awake() { body = GetComponent<Rigidbody2D>(); input = GetComponent<ChordInput>(); }
        void Update()
        {
            buffer = input.JumpPressed && !Frozen ? graceSeconds : Mathf.Max(0, buffer - Time.deltaTime);
            if (eyes != null && input.Horizontal != 0)
                eyes.localPosition = new Vector3(input.Horizontal * .055f, .08f, 0);
        }
        void FixedUpdate()
        {
            if (Frozen) { body.linearVelocity = Vector2.zero; return; }
            Grounded = false;
            int count = body.GetContacts(contacts);
            for (int i = 0; i < count; i++) if (contacts[i].normal.y > .65f) Grounded = true;
            if (!Grounded) separating = false;
            coyote = Grounded && !separating ? graceSeconds : Mathf.Max(0, coyote - Time.fixedDeltaTime);
            Vector2 v = body.linearVelocity;
            v.x = Mathf.MoveTowards(v.x, input.Horizontal * speed, acceleration * (Grounded ? 1 : .7f) * Time.fixedDeltaTime);
            if (buffer > 0 && coyote > 0)
            { v.y = jumpSpeed; buffer = coyote = 0; separating = true; }
            body.linearVelocity = v;
        }
        public void Teleport(Vector2 position)
        {
            body.position = position;
            transform.position = position;
            body.linearVelocity = Vector2.zero;
            buffer = coyote = 0;
            separating = false;
        }
    }
}
