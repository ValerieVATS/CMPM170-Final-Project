using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
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
        }

        private static IEnumerator Steps(int count)
        {
            for (int i = 0; i < count; i++) yield return new WaitForFixedUpdate();
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
            Assert.That(Object.FindObjectsByType<CookingChamber>(FindObjectsSortMode.None).Length, Is.EqualTo(2));
            Assert.That(Object.FindObjectsByType<ServingTarget>(FindObjectsSortMode.None).Length, Is.EqualTo(3));
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
        public IEnumerator BothChambersCookRawIngredientsButNeverThePickle()
        {
            BreakoutBall left = game.SpawnBall(FoodState.Raw, new Vector2(-6.5f, 0.25f), null, IngredientKind.Tomato);
            BreakoutBall right = game.SpawnBall(FoodState.Raw, new Vector2(6.5f, 0.25f), null, IngredientKind.Cheese);
            left.Launch(Vector2.left);
            right.Launch(Vector2.right);
            yield return Steps(15);
            Assert.That(left.State, Is.EqualTo(FoodState.Cooked));
            Assert.That(right.State, Is.EqualTo(FoodState.Cooked));
            Assert.That(left.Ingredient, Is.EqualTo(IngredientKind.Tomato));
            Assert.That(right.Ingredient, Is.EqualTo(IngredientKind.Cheese));
            Assert.That(left.Body.linearVelocity.x, Is.GreaterThan(0));
            Assert.That(right.Body.linearVelocity.x, Is.LessThan(0));
            game.LauncherBall.PlaceHeld(new Vector2(-6.5f, 0.25f));
            game.LauncherBall.Launch(Vector2.left);
            yield return Steps(15);
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

        [UnityTest]
        public IEnumerator BunsRejectRawAndServeCookedThenRestock()
        {
            IngredientRack rack = Object.FindFirstObjectByType<IngredientRack>();
            Assert.That(rack.TryKnockDown(), Is.True);
            BreakoutBall ingredient = Object.FindObjectsByType<BreakoutBall>(FindObjectsSortMode.None)
                .First(ball => ball.State == FoodState.Raw);
            ingredient.Body.position = new Vector2(0, 4);
            ingredient.Launch(Vector2.up);
            yield return Steps(8);
            Assert.That(ingredient != null, Is.True, "Raw food must not be served.");
            Assert.That(ingredient.State, Is.EqualTo(FoodState.Raw));
            Assert.That(rack.IsStocked, Is.False);

            ingredient.Body.position = new Vector2(0, 4);
            ingredient.Cook(1);
            ingredient.Launch(Vector2.up);
            yield return Steps(8);
            Assert.That(ingredient == null, Is.True, "Cooked food should be served.");
            Assert.That(rack.IsStocked, Is.True);
            Assert.That(game.ActiveIngredientCount, Is.Zero);
        }

        [UnityTest]
        public IEnumerator PaddleEdgeRedirectsIngredientWithoutCookingIt()
        {
            BreakoutBall ingredient = game.SpawnBall(FoodState.Raw,
                game.PaddlePosition + new Vector2(0.8f, 1.2f), null, IngredientKind.Lettuce);
            ingredient.Launch(Vector2.down);
            yield return Steps(10);
            Assert.That(ingredient.Body.linearVelocity.x, Is.GreaterThan(2));
            Assert.That(ingredient.Body.linearVelocity.y, Is.GreaterThan(0));
            Assert.That(ingredient.State, Is.EqualTo(FoodState.Raw));
            Assert.That(ingredient.Ingredient, Is.EqualTo(IngredientKind.Lettuce));
        }

        [UnityTest]
        public IEnumerator MissedFoodRestocksAndPickleCanAlwaysRelaunch()
        {
            IngredientRack rack = Object.FindFirstObjectByType<IngredientRack>();
            rack.TryKnockDown();
            BreakoutBall raw = Object.FindObjectsByType<BreakoutBall>(FindObjectsSortMode.None)
                .First(ball => ball.State == FoodState.Raw);
            raw.Body.position = new Vector2(0, -6);
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
        public IEnumerator HoldShiftCatchesIngredientAndReleaseLaunchesIt()
        {
            Keyboard keyboard = InputSystem.AddDevice<Keyboard>();
            try
            {
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.LeftShift));
                InputSystem.Update();
                yield return null;
                BreakoutBall ingredient = game.SpawnBall(FoodState.Raw,
                    game.PaddlePosition + Vector2.up * 1.2f, null, IngredientKind.Onion);
                ingredient.Launch(Vector2.down);
                yield return Steps(12);
                Assert.That(game.CaughtIngredient, Is.EqualTo(ingredient));
                Assert.That(ingredient.IsHeld, Is.True);
                InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                InputSystem.Update();
                yield return null;
                Assert.That(game.CaughtIngredient == null, Is.True);
                Assert.That(ingredient.IsHeld, Is.False);
                Assert.That(ingredient.Body.linearVelocity.y, Is.GreaterThan(0));
                Assert.That(ingredient.State, Is.EqualTo(FoodState.Raw));
            }
            finally
            {
                InputSystem.RemoveDevice(keyboard);
            }
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
