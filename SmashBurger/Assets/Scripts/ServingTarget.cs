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
                game.Feedback("STILL RAW — use a side cooker first", BurgerBreakout.RawRed);
                return;
            }
            flash = 0.5f;
            game.Feedback("SERVED — the rack is restocked. Keep experimenting!", BurgerBreakout.Green);
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
