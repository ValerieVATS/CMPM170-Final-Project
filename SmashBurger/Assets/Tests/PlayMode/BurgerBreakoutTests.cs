using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace SmashBurger.Tests
{
    public sealed class BurgerBreakoutTests
    {
        private BurgerBreakout game;

        [UnitySetUp]
        public IEnumerator LoadPrototype()
        {
            yield return SceneManager.LoadSceneAsync("SampleScene");
            yield return new WaitForFixedUpdate();
            game = Object.FindFirstObjectByType<BurgerBreakout>();
            Assert.That(game, Is.Not.Null);
            Assert.That(game.isIntro, Is.True, "The scene should open on the title screen.");
            game.StartGame();
            yield return new WaitForFixedUpdate();
        }

        private static IEnumerator Steps(int count)
        {
            for (int i = 0; i < count; i++) yield return new WaitForFixedUpdate();
        }

        [TestCase(600f, "10:00")]
        [TestCase(599f, "9:59")]
        [TestCase(59.1f, "1:00")]
        [TestCase(0.1f, "0:01")]
        [TestCase(0f, "0:00")]
        [TestCase(-1f, "0:00")]
        public void CountdownDisplaysMinutesAndSeconds(float remaining, string expected)
        {
            Assert.That(BurgerBreakout.FormatCountdown(remaining), Is.EqualTo(expected));
        }

        [UnityTest]
        public IEnumerator TitleScreenShowsNoOrdersAndHoldsTheMatchClock()
        {
            game.ShowIntro();
            Assert.That(game.isIntro, Is.True);
            Assert.That(game.isPlaying, Is.False);
            Assert.That(game.isGameOver, Is.False);
            Assert.That(game.matchTimeRemaining, Is.EqualTo(600f));
            Assert.That(game.finalScore, Is.Zero);
            Assert.That(game.failedOrders, Is.Zero);
            Assert.That(game.Scoring.ActiveOrders, Is.Empty);
            Assert.That(game.ActiveIngredientCount, Is.Zero);
            Assert.That(game.LauncherBall, Is.Not.Null);
            Assert.That(game.LauncherBall.IsHeld, Is.True);
            foreach (DiningTable table in Object.FindObjectsByType<DiningTable>(FindObjectsSortMode.None))
                Assert.That(table.GetComponentInChildren<TextMesh>().text, Is.EqualTo("WAITING"));

            yield return new WaitForSeconds(0.12f);
            Assert.That(game.isIntro, Is.True);
            Assert.That(game.matchTimeRemaining, Is.EqualTo(600f), "The match clock must stay frozen on the title screen.");
            Assert.That(game.Scoring.ActiveOrders, Is.Empty);

            game.StartGame();
            Assert.That(game.isPlaying, Is.True, "Space, Enter or a click should start the match.");
            Assert.That(game.matchTimeRemaining, Is.EqualTo(600f));
            Assert.That(game.Scoring.ActiveOrders.Count, Is.EqualTo(8));
            yield return new WaitForSeconds(0.12f);
            Assert.That(game.matchTimeRemaining, Is.LessThan(600f));
            Assert.That(game.matchTimeRemaining, Is.GreaterThan(599f));
        }

        [Test]
        public void TitleScreenResetsAPartiallyPlayedRound()
        {
            DiningTable table = Object.FindFirstObjectByType<DiningTable>();
            BreakoutBall served = game.SpawnBall(FoodState.Cooked, new Vector2(0, -2), null, table.requestedIngredient);
            game.HitPaddle(served);
            Assert.That(table.TryDeliver(served), Is.True);
            Assert.That(game.finalScore, Is.GreaterThan(0));

            game.ShowIntro();
            Assert.That(game.isIntro, Is.True);
            Assert.That(game.finalScore, Is.Zero);
            Assert.That(game.Scoring.ActiveOrders, Is.Empty);
            Assert.That(game.ActiveIngredientCount, Is.Zero);
            Assert.That(Object.FindObjectsByType<IngredientRack>(FindObjectsSortMode.None).All(rack => rack.IsStocked), Is.True);
            Assert.That(game.LauncherBall.IsHeld, Is.True);
        }

        [UnityTest]
        public IEnumerator MatchTimerStopsPlayAtZeroAndRestartsAtTenMinutes()
        {
            float originalTimeScale = Time.timeScale;
            try
            {
                DiningTable table = Object.FindFirstObjectByType<DiningTable>();
                BreakoutBall served = game.SpawnBall(FoodState.Cooked, new Vector2(0, -2), null, table.requestedIngredient);
                game.HitPaddle(served);
                Assert.That(table.TryDeliver(served), Is.True);
                int finalScore = game.finalScore;
                Assert.That(finalScore, Is.GreaterThan(0));

                float start = game.matchTimeRemaining;
                yield return new WaitForSeconds(0.12f);
                Assert.That(game.matchTimeRemaining, Is.LessThan(start - 0.08f));

                BreakoutBall pending = game.SpawnBall(FoodState.Cooked, new Vector2(0, -2), null, table.requestedIngredient);
                game.HitPaddle(pending);
                int orderId = table.currentOrder.id;
                LineRenderer guide = GameObject.Find("Aim guide").GetComponent<LineRenderer>();
                guide.enabled = true;
                FieldInfo elapsed = typeof(BurgerBreakout).GetField("elapsed", BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.That(elapsed, Is.Not.Null);
                elapsed.SetValue(game, 600f);
                Time.timeScale = 0.5f;
                yield return null;

                Assert.That(game.isGameOver, Is.True);
                Assert.That(game.Phase, Is.EqualTo(BurgerBreakout.GamePhase.GameOver));
                Assert.That(game.matchTimeRemaining, Is.Zero);
                Assert.That(BurgerBreakout.FormatCountdown(game.matchTimeRemaining), Is.EqualTo("0:00"));
                Assert.That(Time.timeScale, Is.Zero);
                Assert.That(guide.enabled, Is.False);
                float tableTime = table.remainingTime;
                float stoppedTime = Time.time;
                yield return new WaitForSecondsRealtime(0.06f);
                Assert.That(Time.time, Is.EqualTo(stoppedTime));
                Assert.That(table.remainingTime, Is.EqualTo(tableTime));
                Assert.That(table.currentOrder.id, Is.EqualTo(orderId));
                Assert.That(table.TryDeliver(pending), Is.False);
                Assert.That(game.finalScore, Is.EqualTo(finalScore));

                game.Restart();
                Assert.That(game.isGameOver, Is.False);
                Assert.That(game.isPlaying, Is.True);
                Assert.That(game.Phase, Is.EqualTo(BurgerBreakout.GamePhase.Playing));
                Assert.That(Time.timeScale, Is.EqualTo(0.5f));
                Assert.That(game.matchTimeRemaining, Is.EqualTo(600f));
                Assert.That(game.finalScore, Is.Zero);
                Assert.That(game.Scoring.ActiveOrders.Count, Is.EqualTo(8));
                Assert.That(table.currentOrder.id, Is.Not.EqualTo(orderId));
                Assert.That(game.LauncherBall.IsHeld, Is.True);
            }
            finally
            {
                if (game != null && game.isGameOver) game.Restart();
                Time.timeScale = originalTimeScale;
            }
        }

        [Test]
        public void ContactOffsetProducesPredictableUpwardAim()
        {
            Vector2 center = BreakoutAim.FromPaddle(0, 1, 68);
            Vector2 left = BreakoutAim.FromPaddle(-1, 1, 68);
            Vector2 right = BreakoutAim.FromPaddle(1, 1, 68);
            Assert.That(center, Is.EqualTo(Vector2.up));
            Assert.That(left.x, Is.EqualTo(-right.x).Within(0.0001f));
            Assert.That(left.y, Is.GreaterThan(0.35f));
            Assert.That(right.magnitude, Is.EqualTo(1).Within(0.0001f));
            Assert.That(BreakoutAim.FromPaddle(9, 1, 68), Is.EqualTo(right));
            Assert.That(BreakoutAim.Toward(Vector2.zero, Vector2.down, 68).y, Is.GreaterThan(0));
        }

        [Test]
        public void SceneStartsWithIngredientRacksAndOneHeldPickle()
        {
            Assert.That(Object.FindObjectsByType<IngredientRack>(FindObjectsSortMode.None).Length, Is.EqualTo(14));
            Assert.That(GameObject.Find("Left cooking chamber"), Is.Null);
            Assert.That(GameObject.Find("Right cooking chamber"), Is.Null);
            DiningTable[] tables = Object.FindObjectsByType<DiningTable>(FindObjectsSortMode.None);
            Assert.That(tables.Length, Is.EqualTo(8));
            Assert.That(tables.Count(table => table.transform.position.x < 0), Is.EqualTo(4));
            Assert.That(tables.Count(table => table.transform.position.x > 0), Is.EqualTo(4));
            Assert.That(game.Scoring, Is.Not.Null);
            Assert.That(game.Scoring.ActiveOrders.Count, Is.EqualTo(8));
            Assert.That(tables.Select(table => table.currentOrder.id).Distinct().Count(), Is.EqualTo(8));
            foreach (int side in new[] { -1, 1 })
            {
                float[] heights = tables.Where(table => Mathf.Sign(table.transform.position.x) == side)
                    .Select(table => table.transform.position.y).OrderBy(height => height).ToArray();
                Assert.That(heights[0], Is.GreaterThan(-2.5f), "No tables should sit beside the paddle.");
                for (int i = 1; i < heights.Length; i++)
                    Assert.That(heights[i] - heights[i - 1], Is.EqualTo(2.2f).Within(0.01f));
            }
            foreach (DiningTable table in tables)
            {
                Assert.That(table.orderDuration, Is.EqualTo(60f));
                Assert.That(table.remainingTime, Is.GreaterThan(0));
                Assert.That(table.requestedIngredient, Is.Not.EqualTo(IngredientKind.Pickle));
                Assert.That(table.currentOrder.Item, Is.EqualTo(table.requestedIngredient));
                Assert.That(table.currentOrder.Patience, Is.EqualTo(table.orderDuration));
                Assert.That(table.transform.Find("White dining table").GetComponent<SpriteRenderer>().color, Is.EqualTo(Color.white));
                Assert.That(table.transform.Find("Green order timer").GetComponent<SpriteRenderer>().color,
                    Is.EqualTo(BurgerBreakout.Green));
                Assert.That(table.GetComponentInChildren<TextMesh>().text,
                    Is.EqualTo(IngredientInfo.Name(table.requestedIngredient).ToUpperInvariant()));
            }
            Assert.That(game.finalScore, Is.Zero);
            Assert.That(game.failedOrders, Is.Zero);
            Assert.That(game.matchTimeRemaining, Is.InRange(599f, 600f));
            Assert.That(BurgerBreakout.FormatCountdown(game.matchTimeRemaining), Is.EqualTo("10:00"));
            Assert.That(game.isGameOver, Is.False);
            Assert.That(game.LauncherBall.State, Is.EqualTo(FoodState.Knockdown));
            Assert.That(game.LauncherBall.Ingredient, Is.EqualTo(IngredientKind.Pickle));
            Assert.That(game.LauncherBall.IsHeld, Is.True);
            Assert.That(game.ActiveIngredientCount, Is.Zero);
        }

        [Test]
        public void RacksStockAssortedIngredientsAndDropsMatch()
        {
            IngredientRack[] racks = Object.FindObjectsByType<IngredientRack>(FindObjectsSortMode.None);
            IngredientKind[] kinds = racks.Select(rack => rack.Ingredient).Distinct().ToArray();
            Assert.That(kinds.Length, Is.EqualTo(5), "The shelf should show every burger ingredient.");
            Assert.That(kinds, Does.Contain(IngredientKind.Meat));
            Assert.That(kinds, Does.Contain(IngredientKind.Cheese));
            Assert.That(kinds, Does.Contain(IngredientKind.Lettuce));
            Assert.That(kinds, Does.Contain(IngredientKind.Tomato));
            Assert.That(kinds, Does.Contain(IngredientKind.Onion));

            IngredientRack cheese = racks.First(rack => rack.Ingredient == IngredientKind.Cheese);
            Assert.That(cheese.TryKnockDown(), Is.True);
            BreakoutBall dropped = Object.FindObjectsByType<BreakoutBall>(FindObjectsSortMode.None)
                .First(ball => ball.State == FoodState.Raw);
            Assert.That(dropped.Ingredient, Is.EqualTo(IngredientKind.Cheese));
            Assert.That(dropped.Source, Is.EqualTo(cheese));
        }

        [UnityTest]
        public IEnumerator PickleCollisionKnocksIngredientOffRack()
        {
            IngredientRack rack = Object.FindObjectsByType<IngredientRack>(FindObjectsSortMode.None)
                .OrderBy(item => item.transform.position.y).First();
            game.LauncherBall.PlaceHeld((Vector2)rack.transform.position + Vector2.down);
            game.LauncherBall.Launch(Vector2.up);
            yield return Steps(12);
            Assert.That(rack.IsStocked, Is.False);
            Assert.That(game.ActiveIngredientCount, Is.EqualTo(1));
            BreakoutBall raw = Object.FindObjectsByType<BreakoutBall>(FindObjectsSortMode.None)
                .First(ball => ball.State == FoodState.Raw);
            Assert.That(raw.Source, Is.EqualTo(rack));
            Assert.That(raw.Ingredient, Is.EqualTo(rack.Ingredient));
        }

        [UnityTest]
        public IEnumerator EmptySideLanesDoNotCookButPaddleDoes()
        {
            BreakoutBall left = game.SpawnBall(FoodState.Raw, new Vector2(-6.5f, 0.25f), null, IngredientKind.Tomato);
            BreakoutBall right = game.SpawnBall(FoodState.Raw, new Vector2(6.5f, 0.25f), null, IngredientKind.Cheese);
            left.Launch(Vector2.left);
            right.Launch(Vector2.right);
            yield return Steps(15);
            Assert.That(left.State, Is.EqualTo(FoodState.Raw));
            Assert.That(right.State, Is.EqualTo(FoodState.Raw));
            Assert.That(left.Ingredient, Is.EqualTo(IngredientKind.Tomato));
            Assert.That(right.Ingredient, Is.EqualTo(IngredientKind.Cheese));
            game.HitPaddle(left);
            Assert.That(left.State, Is.EqualTo(FoodState.Cooked));
            Assert.That(left.hasPaddleBounceSinceCooking, Is.True);
            Assert.That(left.Body.linearVelocity.y, Is.GreaterThan(0));
            game.HitPaddle(game.LauncherBall);
            Assert.That(game.LauncherBall.State, Is.EqualTo(FoodState.Knockdown));
            Assert.That(game.LauncherBall.Ingredient, Is.EqualTo(IngredientKind.Pickle));

            string screenshot = System.Environment.GetEnvironmentVariable("SMASHBURGER_SCREENSHOT");
            if (!string.IsNullOrEmpty(screenshot))
            {
                yield return new WaitForEndOfFrame();
                ScreenCapture.CaptureScreenshot(screenshot);
                yield return null;
            }
        }

        [Test]
        public void TablesRequireMatchingCookedFoodBouncedFromPaddleAndRestockOnDelivery()
        {
            DiningTable table = Object.FindFirstObjectByType<DiningTable>();
            IngredientRack rack = Object.FindObjectsByType<IngredientRack>(FindObjectsSortMode.None)
                .First(item => item.Ingredient == table.requestedIngredient);
            Assert.That(rack.TryKnockDown(), Is.True);
            BreakoutBall ingredient = Object.FindObjectsByType<BreakoutBall>(FindObjectsSortMode.None)
                .First(ball => ball.State == FoodState.Raw);
            Assert.That(table.TryDeliver(ingredient), Is.False, "Raw food must not be served.");
            Assert.That(ingredient.State, Is.EqualTo(FoodState.Raw));
            Assert.That(rack.IsStocked, Is.False);

            int oldOrderId = table.currentOrder.id;
            game.HitPaddle(ingredient);
            Assert.That(ingredient.State, Is.EqualTo(FoodState.Cooked), "The paddle should cook raw food.");
            Assert.That(ingredient.hasPaddleBounceSinceCooking, Is.True);
            Assert.That(table.TryDeliver(ingredient), Is.True);
            Assert.That(ingredient.gameObject.activeSelf, Is.False);
            Assert.That(rack.IsStocked, Is.True);
            Assert.That(game.ActiveIngredientCount, Is.Zero);
            Assert.That(game.finalScore, Is.InRange(100, 200));
            Assert.That(game.Scoring.finalScore, Is.EqualTo(game.finalScore));
            Assert.That(game.Scoring.DeliveredCount, Is.EqualTo(1));
            Assert.That(game.Scoring.ActiveOrders.Count, Is.EqualTo(8));
            Assert.That(table.currentOrder.id, Is.Not.EqualTo(oldOrderId));
            Assert.That(table.requestedIngredient, Is.Not.EqualTo(rack.Ingredient));
            Assert.That(table.remainingTime, Is.GreaterThan(59));
        }

        [Test]
        public void WrongDeliveryLeavesFoodInPlayAndKeepsRequestAndScore()
        {
            DiningTable table = Object.FindFirstObjectByType<DiningTable>();
            IngredientKind requested = table.requestedIngredient;
            int orderId = table.currentOrder.id;
            IngredientKind wrong = requested == IngredientKind.Meat ? IngredientKind.Cheese : IngredientKind.Meat;
            IngredientRack rack = Object.FindObjectsByType<IngredientRack>(FindObjectsSortMode.None)
                .First(item => item.Ingredient == wrong);
            Assert.That(rack.TryKnockDown(), Is.True);
            BreakoutBall ingredient = Object.FindObjectsByType<BreakoutBall>(FindObjectsSortMode.None)
                .First(ball => ball.State == FoodState.Raw);
            game.HitPaddle(ingredient);

            Assert.That(table.TryDeliver(ingredient), Is.False);
            Assert.That(ingredient.gameObject.activeSelf, Is.True);
            Assert.That(rack.IsStocked, Is.False);
            Assert.That(table.requestedIngredient, Is.EqualTo(requested));
            Assert.That(table.currentOrder.id, Is.EqualTo(orderId));
            Assert.That(game.finalScore, Is.Zero);
            Assert.That(game.Scoring.finalScore, Is.EqualTo(game.finalScore));
            Assert.That(game.Scoring.MistakeCount, Is.EqualTo(1));
            Assert.That(game.Scoring.LastDeliveryPoints, Is.Zero);
            Assert.That(game.Scoring.ActiveOrders.Count, Is.EqualTo(8));
            Assert.That(game.ActiveIngredientCount, Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator WrongBallCollisionBouncesWithoutScoring()
        {
            DiningTable table = Object.FindObjectsByType<DiningTable>(FindObjectsSortMode.None)
                .First(item => item.transform.position.y > 4);
            IngredientKind requested = table.requestedIngredient;
            IngredientKind wrong = requested == IngredientKind.Meat ? IngredientKind.Cheese : IngredientKind.Meat;
            int orderId = table.currentOrder.id;
            float side = Mathf.Sign(table.transform.position.x);
            BreakoutBall ingredient = game.SpawnBall(FoodState.Cooked,
                new Vector2(side * 6.2f, table.transform.position.y), null, wrong);
            game.HitPaddle(ingredient);
            ingredient.Launch(Vector2.right * side);

            yield return Steps(12);
            Assert.That(ingredient != null, Is.True, "Wrong cooked food should stay in play.");
            Assert.That(ingredient.gameObject.activeSelf, Is.True);
            Assert.That(ingredient.Body.linearVelocity.x * side, Is.LessThan(0), "Wrong food should bounce off the table.");
            Assert.That(table.requestedIngredient, Is.EqualTo(requested));
            Assert.That(table.currentOrder.id, Is.EqualTo(orderId));
            Assert.That(game.Scoring.MistakeCount, Is.EqualTo(1));
            Assert.That(game.Scoring.DeliveredCount, Is.Zero);
            Assert.That(game.finalScore, Is.Zero);
            Assert.That(game.ActiveIngredientCount, Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator CookedBallCollisionServesTheTableOnTheEdge()
        {
            DiningTable table = Object.FindObjectsByType<DiningTable>(FindObjectsSortMode.None)
                .First(item => item.transform.position.y > 4);
            float side = Mathf.Sign(table.transform.position.x);
            BreakoutBall ingredient = game.SpawnBall(FoodState.Cooked,
                new Vector2(side * 6.2f, table.transform.position.y), null, table.requestedIngredient);
            game.HitPaddle(ingredient);
            ingredient.Launch(Vector2.right * side);

            yield return Steps(12);
            Assert.That(ingredient == null, Is.True, "A matching cooked ball should score on collision.");
            Assert.That(game.finalScore, Is.GreaterThan(0));
            Assert.That(table.remainingTime, Is.GreaterThan(59));
        }

        [UnityTest]
        public IEnumerator PicklePassesThroughTablesWhileIngredientsStillCollide()
        {
            DiningTable table = Object.FindObjectsByType<DiningTable>(FindObjectsSortMode.None)
                .First(item => item.transform.position.x > 0 && item.transform.position.y > 4);
            IngredientKind request = table.requestedIngredient;
            BreakoutBall pickle = game.LauncherBall;
            pickle.PlaceHeld(new Vector2(6.2f, table.transform.position.y));
            pickle.Launch(Vector2.right);
            foreach (DiningTable target in Object.FindObjectsByType<DiningTable>(FindObjectsSortMode.None))
                Assert.That(Physics2D.GetIgnoreCollision(pickle.GetComponent<Collider2D>(), target.GetComponent<Collider2D>()),
                    Is.True, "Only the pickle should ignore table collisions.");

            yield return Steps(12);
            Assert.That(pickle.Body.position.x, Is.GreaterThan(table.transform.position.x));
            Assert.That(pickle.State, Is.EqualTo(FoodState.Knockdown));
            Assert.That(table.requestedIngredient, Is.EqualTo(request));
            Assert.That(game.finalScore, Is.Zero);
        }

        [UnityTest]
        public IEnumerator FasterCorrectDeliveriesEarnMorePoints()
        {
            DiningTable[] tables = Object.FindObjectsByType<DiningTable>(FindObjectsSortMode.None);
            BreakoutBall fast = game.SpawnBall(FoodState.Cooked, new Vector2(0, -2), null, tables[0].requestedIngredient);
            game.HitPaddle(fast);
            Assert.That(tables[0].TryDeliver(fast), Is.True);
            int fastPoints = game.finalScore;

            yield return new WaitForSeconds(0.6f);
            BreakoutBall slow = game.SpawnBall(FoodState.Cooked, new Vector2(0, -2), null, tables[1].requestedIngredient);
            game.HitPaddle(slow);
            Assert.That(tables[1].TryDeliver(slow), Is.True);
            int slowPoints = game.finalScore - fastPoints;
            Assert.That(fastPoints, Is.GreaterThan(slowPoints));
            Assert.That(slowPoints, Is.GreaterThanOrEqualTo(100));
            Assert.That(game.Scoring.DeliveredCount, Is.EqualTo(2));
        }

        [Test]
        public void WrongDeliveryDoesNotReducePointsAfterAPreviousSuccess()
        {
            DiningTable table = Object.FindFirstObjectByType<DiningTable>();
            BreakoutBall correct = game.SpawnBall(FoodState.Cooked, new Vector2(0, -2), null, table.requestedIngredient);
            game.HitPaddle(correct);
            Assert.That(table.TryDeliver(correct), Is.True);
            int earned = game.finalScore;

            IngredientKind wrong = table.requestedIngredient == IngredientKind.Meat
                ? IngredientKind.Cheese : IngredientKind.Meat;
            BreakoutBall incorrect = game.SpawnBall(FoodState.Cooked, new Vector2(0, -2), null, wrong);
            game.HitPaddle(incorrect);
            Assert.That(table.TryDeliver(incorrect), Is.False);
            Assert.That(incorrect.gameObject.activeSelf, Is.True);
            Assert.That(game.finalScore, Is.EqualTo(earned));
            Assert.That(game.Scoring.DeliveredCount, Is.EqualTo(1));
            Assert.That(game.Scoring.MistakeCount, Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator AnExpiredTableFailsAndGetsANewOrderWithoutResettingOtherTables()
        {
            DiningTable[] tables = Object.FindObjectsByType<DiningTable>(FindObjectsSortMode.None);
            DiningTable expiring = tables[0];
            DiningTable other = tables[1];
            IngredientKind oldRequest = expiring.requestedIngredient;
            int oldOrderId = expiring.currentOrder.id;
            IngredientKind otherRequest = other.requestedIngredient;
            expiring.orderDuration = 0.35f;
            Transform bar = expiring.transform.Find("Green order timer");

            yield return new WaitForSeconds(0.12f);
            Assert.That(bar.localScale.x, Is.InRange(0, 1.35f), "The green bar should shrink as the order ages.");

            yield return new WaitForSeconds(0.34f);
            Assert.That(game.failedOrders, Is.EqualTo(1));
            Assert.That(game.finalScore, Is.Zero);
            Assert.That(expiring.requestedIngredient, Is.Not.EqualTo(oldRequest));
            Assert.That(expiring.currentOrder.id, Is.Not.EqualTo(oldOrderId));
            Assert.That(game.Scoring.ActiveOrders.Count, Is.EqualTo(8));
            Assert.That(game.Scoring.ActiveOrders.Any(order => order.id == oldOrderId), Is.False);
            Assert.That(expiring.remainingTime, Is.GreaterThan(0));
            Assert.That(other.requestedIngredient, Is.EqualTo(otherRequest));
            Assert.That(other.remainingTime, Is.GreaterThan(58));

            game.Restart();
            Assert.That(game.failedOrders, Is.Zero);
            Assert.That(game.finalScore, Is.Zero);
            Assert.That(game.Scoring.ActiveOrders.Count, Is.EqualTo(8));
            Assert.That(expiring.remainingTime, Is.GreaterThan(0));
        }

        [UnityTest]
        public IEnumerator PaddleEdgeCooksAndRedirectsIngredient()
        {
            BreakoutBall ingredient = game.SpawnBall(FoodState.Raw,
                game.PaddlePosition + new Vector2(0.8f, 1.2f), null, IngredientKind.Lettuce);
            ingredient.Launch(Vector2.down);
            yield return Steps(10);
            Assert.That(ingredient.Body.linearVelocity.x, Is.GreaterThan(2));
            Assert.That(ingredient.Body.linearVelocity.y, Is.GreaterThan(0));
            Assert.That(ingredient.State, Is.EqualTo(FoodState.Cooked));
            Assert.That(ingredient.hasPaddleBounceSinceCooking, Is.True);
            Assert.That(ingredient.Ingredient, Is.EqualTo(IngredientKind.Lettuce));
        }

        [UnityTest]
        public IEnumerator MissedFoodRestocksAndPickleCanAlwaysRelaunch()
        {
            IngredientRack rack = Object.FindFirstObjectByType<IngredientRack>();
            rack.TryKnockDown();
            BreakoutBall raw = Object.FindObjectsByType<BreakoutBall>(FindObjectsSortMode.None)
                .First(ball => ball.State == FoodState.Raw);
            raw.PlaceHeld(new Vector2(0, -6));
            raw.Launch(Vector2.down);
            game.LauncherBall.PlaceHeld(new Vector2(2, -6));
            game.LauncherBall.Launch(Vector2.down);
            yield return Steps(3);
            Assert.That(rack.IsStocked, Is.True);
            Assert.That(game.ActiveIngredientCount, Is.Zero);
            Assert.That(game.LauncherBall.IsHeld, Is.True);
            game.LauncherBall.Launch(Vector2.up);
            Assert.That(game.LauncherBall.Body.linearVelocity.y, Is.GreaterThan(0));
        }

        [UnityTest]
        public IEnumerator CatchModeCooksAndHoldsIngredientUntilRelease()
        {
            // The batch runner replaces virtual keyboard state between frames; set the catch input
            // directly to exercise the paddle catch and subsequent release deterministically.
            FieldInfo catchInput = typeof(BurgerBreakout).GetField("catchHeld", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(catchInput, Is.Not.Null);
            catchInput.SetValue(game, true);
            BreakoutBall ingredient = game.SpawnBall(FoodState.Raw,
                game.PaddlePosition + Vector2.up * 0.5f, null, IngredientKind.Onion);
            game.HitPaddle(ingredient);
            Assert.That(game.CaughtIngredient, Is.EqualTo(ingredient));
            Assert.That(ingredient.IsHeld, Is.True);
            Assert.That(ingredient.State, Is.EqualTo(FoodState.Cooked));

            catchInput.SetValue(game, false);
            yield return null;
            Assert.That(game.CaughtIngredient == null, Is.True);
            Assert.That(ingredient.IsHeld, Is.False);
            Assert.That(ingredient.Body.linearVelocity.y, Is.GreaterThan(0));
            Assert.That(ingredient.State, Is.EqualTo(FoodState.Cooked));
        }

        [Test]
        public void ActiveFoodCapAndResetKeepPlaygroundRepeatable()
        {
            IngredientRack[] racks = Object.FindObjectsByType<IngredientRack>(FindObjectsSortMode.None);
            for (int i = 0; i < game.MaxActiveIngredients; i++) Assert.That(racks[i].TryKnockDown(), Is.True);
            Assert.That(racks[game.MaxActiveIngredients].TryKnockDown(), Is.False);
            game.Restart();
            Assert.That(game.ActiveIngredientCount, Is.Zero);
            Assert.That(racks.All(rack => rack.IsStocked), Is.True);
            Assert.That(game.LauncherBall.IsHeld, Is.True);
        }
    }
}
