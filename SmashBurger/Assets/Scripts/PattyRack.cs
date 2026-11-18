using UnityEngine;

namespace SmashBurger
{
    public sealed class PattyRack : MonoBehaviour
    {
        private BurgerBreakout game;
        private SpriteRenderer patty;
        private Collider2D hitbox;
        public bool IsStocked => hitbox != null && hitbox.enabled;

        public void Initialize(BurgerBreakout owner)
        {
            game = owner;
            patty = game.Shape("Raw patty", transform, Vector2.zero,
                Vector2.one * 0.62f, BurgerBreakout.RawRed, true, 3);
            game.Shape("Patty highlight", transform, new Vector2(-0.10f, 0.11f),
                new Vector2(0.17f, 0.065f), new Color(1, 0.6f, 0.5f, 0.65f), true, 4);
            hitbox = gameObject.AddComponent<CircleCollider2D>();
            ((CircleCollider2D)hitbox).radius = 0.34f;
            hitbox.sharedMaterial = game.BounceMaterial;
        }

        private void OnCollisionEnter2D(Collision2D collision)
        {
            BreakoutBall ball = collision.gameObject.GetComponent<BreakoutBall>();
            if (ball != null && ball.State == FoodState.Knockdown) TryKnockDown();
        }

        public bool TryKnockDown()
        {
            if (!IsStocked || game.ActivePattyCount >= game.MaxActivePatties) return false;
            hitbox.enabled = false;
            foreach (SpriteRenderer image in GetComponentsInChildren<SpriteRenderer>()) image.enabled = false;
            BreakoutBall ball = game.SpawnBall(FoodState.Raw,
                (Vector2)transform.position + Vector2.down * 0.48f, this);
            // A gentle, deterministic drop makes the first redirect readable.
            ball.Launch(new Vector2(-Mathf.Sign(transform.position.x) * 0.15f, -1));
            game.Feedback("RAW PATTY — bank it into either side cooker", BurgerBreakout.RawRed);
            return true;
        }

        public void Restock()
        {
            hitbox.enabled = true;
            foreach (SpriteRenderer image in GetComponentsInChildren<SpriteRenderer>()) image.enabled = true;
        }
    }
}
