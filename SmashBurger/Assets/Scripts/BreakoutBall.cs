using UnityEngine;

namespace SmashBurger
{
    public enum FoodState { Knockdown, Raw, Cooked }

    [RequireComponent(typeof(Rigidbody2D), typeof(CircleCollider2D))]
    public sealed class BreakoutBall : MonoBehaviour
    {
        public FoodState State { get; private set; }
        public Rigidbody2D Body { get; private set; }
        public PattyRack Source { get; private set; }
        public bool IsHeld { get; private set; }
        public float Radius => State == FoodState.Knockdown ? 0.19f : 0.29f;

        private BurgerBreakout game;
        private SpriteRenderer face;
        private SpriteRenderer ring;
        private TrailRenderer trail;
        private float lastPaddleHit = -1f;
        private bool retiring;

        public void Initialize(BurgerBreakout owner, FoodState state, PattyRack source = null)
        {
            game = owner;
            State = state;
            Source = source;
            Body = GetComponent<Rigidbody2D>();
            Body.gravityScale = 0;
            Body.freezeRotation = true;
            Body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            Body.interpolation = RigidbodyInterpolation2D.Interpolate;
            GetComponent<CircleCollider2D>().radius = Radius;
            GetComponent<CircleCollider2D>().sharedMaterial = game.BounceMaterial;

            ring = game.Shape("Outline", transform, Vector2.zero,
                Vector2.one * (Radius * 2f + 0.07f), BurgerBreakout.Cream, true, 9);
            face = game.Shape("Ball", transform, Vector2.zero,
                Vector2.one * Radius * 2f, Color.white, true, 10);
            if (state != FoodState.Knockdown)
            {
                for (int i = -1; i <= 1; i++)
                {
                    SpriteRenderer mark = game.Shape("Grill mark", transform,
                        new Vector2(i * 0.12f, 0), new Vector2(0.035f, 0.31f),
                        new Color(1, 1, 1, 0.22f), false, 11);
                    mark.transform.localRotation = Quaternion.Euler(0, 0, -25);
                }
            }

            trail = gameObject.AddComponent<TrailRenderer>();
            trail.sharedMaterial = game.SpriteMaterial;
            trail.time = 0.18f;
            trail.startWidth = Radius * 1.1f;
            trail.endWidth = 0;
            trail.minVertexDistance = 0.08f;
            trail.sortingOrder = 8;
            Paint();
        }

        private void Paint()
        {
            Color color = State == FoodState.Knockdown ? BurgerBreakout.Green
                : State == FoodState.Raw ? BurgerBreakout.RawRed : Color.black;
            face.color = color;
            ring.color = State == FoodState.Cooked ? BurgerBreakout.Cream : color * 0.65f;
            trail.startColor = new Color(color.r, color.g, color.b, 0.45f);
            trail.endColor = new Color(color.r, color.g, color.b, 0);
            gameObject.name = State + " ball";
        }

        public void Hold()
        {
            IsHeld = true;
            Body.linearVelocity = Vector2.zero;
            Body.simulated = false;
            trail.Clear();
        }

        public void PlaceHeld(Vector2 position)
        {
            Body.position = position;
            transform.position = position;
        }

        public void Launch(Vector2 direction)
        {
            IsHeld = false;
            Body.simulated = true;
            Body.linearVelocity = direction.normalized * game.SpeedFor(State);
            trail.Clear();
        }

        public void Cook(float inwardDirection)
        {
            if (State != FoodState.Raw || retiring || IsHeld) return;
            State = FoodState.Cooked;
            Paint();
            // Eject toward the paddle so the cooked patty presents a second aiming task.
            Body.linearVelocity = new Vector2(inwardDirection * 0.8f, -0.6f) * game.SpeedFor(State);
            game.Feedback("COOKED — aim the black patty at a bun", BurgerBreakout.Gold);
        }

        private void FixedUpdate()
        {
            if (game == null || retiring || IsHeld) return;
            if (Body.position.y < BurgerBreakout.DrainY)
            {
                game.Lose(this);
                return;
            }

            Vector2 direction = Body.linearVelocity.normalized;
            if (direction.sqrMagnitude < 0.01f) direction = Vector2.up;
            // Prevent endless near-horizontal rallies; keep food and green balls predictable.
            if (Mathf.Abs(direction.y) < 0.22f)
                direction = new Vector2(direction.x, direction.y < 0 ? -0.22f : 0.22f).normalized;
            Body.linearVelocity = direction * game.SpeedFor(State);
        }

        private void OnCollisionEnter2D(Collision2D collision)
        {
            if (retiring || IsHeld || game == null) return;
            if (collision.collider == game.PaddleCollider && Time.fixedTime - lastPaddleHit > 0.06f)
            {
                // Only the paddle's upper surface should launch a ball upward.
                if (Body.position.y < game.PaddlePosition.y) return;
                lastPaddleHit = Time.fixedTime;
                game.HitPaddle(this);
            }
        }

        public void Retire()
        {
            if (retiring) return;
            retiring = true;
            Body.simulated = false;
            if (Source != null) Source.Restock();
            game.Forget(this);
            gameObject.SetActive(false);
            Destroy(gameObject);
        }
    }
}
