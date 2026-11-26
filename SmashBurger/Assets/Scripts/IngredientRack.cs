using UnityEngine;

namespace SmashBurger
{
    public sealed class IngredientRack : MonoBehaviour
    {
        public IngredientKind Ingredient { get; private set; }

        private BurgerBreakout game;
        private GameObject art;
        private Collider2D hitbox;
        public bool IsStocked => hitbox != null && hitbox.enabled;

        public void Initialize(BurgerBreakout owner, IngredientKind ingredient = IngredientKind.Meat)
        {
            game = owner;
            Ingredient = ingredient;
            art = IngredientArt.Build(game, transform, ingredient, 0.62f, false, 3);
            gameObject.name = "Ingredient rack (" + IngredientInfo.Name(ingredient) + ")";
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
            if (!game.isPlaying || !IsStocked || game.ActiveIngredientCount >= game.MaxActiveIngredients) return false;
            hitbox.enabled = false;
            art.SetActive(false);
            BreakoutBall ball = game.SpawnBall(FoodState.Raw,
                (Vector2)transform.position + Vector2.down * 0.48f, this, Ingredient);
            // A gentle, deterministic drop makes the first redirect readable.
            ball.Launch(new Vector2(-Mathf.Sign(transform.position.x) * 0.15f, -1));
            game.Feedback(IngredientInfo.Label(Ingredient, false) + " — hit the paddle to cook it",
                IngredientInfo.ColorFor(Ingredient, FoodState.Raw));
            return true;
        }

        public void Restock()
        {
            hitbox.enabled = true;
            art.SetActive(true);
        }
    }
}
