using UnityEngine;

namespace SmashBurger
{
    public sealed class ServingTarget : MonoBehaviour
    {
        private BurgerBreakout game;
        private SpriteRenderer bun;
        private float flash;

        public void Initialize(BurgerBreakout owner, SpriteRenderer image)
        {
            game = owner;
            bun = image;
        }

        private void OnCollisionEnter2D(Collision2D collision)
        {
            BreakoutBall ball = collision.gameObject.GetComponent<BreakoutBall>();
            if (ball == null || ball.State == FoodState.Knockdown) return;
            if (ball.State == FoodState.Raw)
            {
                game.Feedback("STILL RAW — bounce it off the paddle to cook", BurgerBreakout.RawRed);
                return;
            }
            flash = 0.5f;
            DeliveryResult result = game.Scoring.Deliver(ball.Ingredient);
            if (!result.Scored)
                game.Feedback("SERVED — the rack is restocked. Keep experimenting!", BurgerBreakout.Green);
            else if (result.Correct)
                game.Feedback($"SERVED — {IngredientInfo.Name(result.Delivered)} +{result.Points} pts", BurgerBreakout.Green);
            else
                game.Feedback($"WRONG ORDER — needed {IngredientInfo.Name(result.Requested)}", BurgerBreakout.RawRed);
            ball.Retire();
        }

        private void Update()
        {
            if (bun == null) return;
            flash = Mathf.Max(0, flash - Time.deltaTime);
            bun.color = Color.Lerp(BurgerBreakout.Gold, BurgerBreakout.Green, flash * 2);
        }
    }
}
