using UnityEngine;

namespace SmashBurger
{
    [RequireComponent(typeof(BoxCollider2D))]
    public sealed class DiningTable : MonoBehaviour
    {
        private const float barWidth = 1.35f;

        // Set the default on BurgerBreakout before Play, or override an individual table during Play.
        [SerializeField, Min(0.1f)] private float orderDurationSeconds = 60f;

        public float orderDuration
        {
            get => Mathf.Max(0.1f, orderDurationSeconds);
            set => orderDurationSeconds = Mathf.Max(0.1f, value);
        }

        public IngredientKind requestedIngredient { get; private set; }
        public Order currentOrder { get; private set; }
        public float remainingTime => Mathf.Max(0, orderDuration - (Time.time - orderStartedAt));

        private BurgerBreakout game;
        private SpriteRenderer timerFill;
        private TextMesh requestLabel;
        private GameObject requestArt;
        private float orderStartedAt;
        private bool hasOrder;

        public void Initialize(BurgerBreakout owner, float duration)
        {
            game = owner;
            orderDuration = duration;

            Color tableEdge = new Color32(174, 183, 178, 255);
            game.Shape("Left table leg", transform, new Vector2(-0.51f, -0.30f), new Vector2(0.11f, 0.26f), tableEdge, false, 2);
            game.Shape("Right table leg", transform, new Vector2(0.51f, -0.30f), new Vector2(0.11f, 0.26f), tableEdge, false, 2);
            game.Shape("White dining table", transform, Vector2.zero, new Vector2(1.48f, 0.43f), Color.white, false, 3);
            game.Shape("Table edge", transform, new Vector2(0, -0.22f), new Vector2(1.48f, 0.06f), tableEdge, false, 4);
            game.Shape("Plate rim", transform, new Vector2(-0.42f, 0.02f), new Vector2(0.38f, 0.36f), tableEdge, true, 4);
            game.Shape("Plate", transform, new Vector2(-0.42f, 0.02f), new Vector2(0.32f, 0.30f), Color.white, true, 5);
            requestLabel = game.Label("", transform, new Vector2(0.19f, 0.02f), 0.09f, new Color32(47, 65, 71, 255));
            game.Shape("Timer track", transform, new Vector2(0, 0.48f), new Vector2(barWidth, 0.09f),
                new Color32(47, 65, 71, 255), false, 3);
            timerFill = game.Shape("Green order timer", transform, new Vector2(0, 0.48f),
                new Vector2(barWidth, 0.09f), BurgerBreakout.Green, false, 4);

            BoxCollider2D hitbox = GetComponent<BoxCollider2D>();
            hitbox.size = new Vector2(1.48f, 0.43f);
            hitbox.sharedMaterial = game.BounceMaterial;
            ResetOrder();
        }

        public void ResetOrder()
        {
            if (hasOrder) game.Scoring.CancelOrder(currentOrder);
            IngredientKind next;
            do
            {
                next = IngredientInfo.ForRackSlot(Random.Range(0, IngredientInfo.orderableCount));
            } while (hasOrder && next == requestedIngredient);

            requestedIngredient = next;
            hasOrder = true;
            orderStartedAt = Time.time;
            currentOrder = game.Scoring.PlaceOrder(next, orderStartedAt, orderDuration);
            requestLabel.text = IngredientInfo.Name(next).ToUpperInvariant();
            if (requestArt != null)
            {
                requestArt.SetActive(false);
                Destroy(requestArt);
            }
            requestArt = IngredientArt.Build(game, transform, next, 0.23f, true, 7);
            requestArt.transform.localPosition = new Vector2(-0.42f, 0.02f);
            UpdateTimerBar();
        }

        private void Update()
        {
            if (!hasOrder || game.isGameOver) return;
            SyncOrderDuration();
            if (remainingTime <= 0) ExpireOrder();
            UpdateTimerBar();
        }

        private void SyncOrderDuration()
        {
            // Inspector edits during Play should change both the visible bar and scoring deadline.
            if (Mathf.Approximately(currentOrder.Patience, orderDuration)) return;
            game.Scoring.CancelOrder(currentOrder);
            currentOrder = game.Scoring.PlaceOrder(requestedIngredient, orderStartedAt, orderDuration);
        }

        private void UpdateTimerBar()
        {
            float width = barWidth * Mathf.Clamp01(remainingTime / orderDuration);
            timerFill.transform.localScale = new Vector3(width, 0.09f, 1);
            timerFill.transform.localPosition = new Vector2((width - barWidth) * 0.5f, 0.48f);
        }

        private void ExpireOrder()
        {
            game.FailOrder(requestedIngredient);
            ResetOrder();
        }

        public bool TryDeliver(BreakoutBall ball)
        {
            if (game.isGameOver || ball == null || !ball.gameObject.activeInHierarchy || ball.IsHeld
                || ball.State == FoodState.Knockdown)
                return false;
            SyncOrderDuration();
            if (remainingTime <= 0)
            {
                ExpireOrder();
                return false;
            }
            if (ball.State != FoodState.Cooked)
            {
                game.Feedback("STILL RAW — bounce it off the paddle to cook", BurgerBreakout.RawRed);
                return false;
            }
            if (!ball.hasPaddleBounceSinceCooking)
            {
                game.Feedback("Bounce cooked food off the paddle before serving", BurgerBreakout.Cream);
                return false;
            }
            if (ball.Ingredient != requestedIngredient)
            {
                DeliveryResult wrong = game.Scoring.DeliverOrder(currentOrder, ball.Ingredient);
                if (wrong.Scored)
                    game.Feedback("WRONG ORDER — this table wants " + IngredientInfo.Name(requestedIngredient).ToUpperInvariant(),
                        BurgerBreakout.RawRed);
                return false;
            }

            DeliveryResult result = game.Scoring.DeliverOrder(currentOrder, ball.Ingredient);
            if (!result.Scored || !result.Correct) return false;
            game.Feedback("SERVED " + IngredientInfo.Name(ball.Ingredient).ToUpperInvariant()
                + "  +" + result.Points, BurgerBreakout.Green);
            ball.Retire();
            ResetOrder();
            return true;
        }

        private void OnCollisionEnter2D(Collision2D collision)
        {
            BreakoutBall ball = collision.gameObject.GetComponent<BreakoutBall>();
            if (ball != null) TryDeliver(ball);
        }
    }
}
