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
        public void SceneStartsWithRedRacksAndOneHeldGreenBall()
        {
            Assert.That(Object.FindObjectsByType<PattyRack>(FindObjectsSortMode.None).Length, Is.EqualTo(14));
            Assert.That(Object.FindObjectsByType<CookingChamber>(FindObjectsSortMode.None).Length, Is.EqualTo(2));
            Assert.That(Object.FindObjectsByType<ServingTarget>(FindObjectsSortMode.None).Length, Is.EqualTo(3));
            Assert.That(game.GreenBall.State, Is.EqualTo(FoodState.Knockdown));
            Assert.That(game.GreenBall.IsHeld, Is.True);
            Assert.That(game.ActivePattyCount, Is.Zero);
        }

        [UnityTest]
        public IEnumerator GreenCollisionKnocksRawPattyOffRack()
        {
            PattyRack rack = Object.FindObjectsByType<PattyRack>(FindObjectsSortMode.None)
                .OrderBy(item => item.transform.position.y).First();
            game.GreenBall.PlaceHeld((Vector2)rack.transform.position + Vector2.down);
            game.GreenBall.Launch(Vector2.up);
            yield return Steps(12);
            Assert.That(rack.IsStocked, Is.False);
            Assert.That(game.ActivePattyCount, Is.EqualTo(1));
            BreakoutBall raw = Object.FindObjectsByType<BreakoutBall>(FindObjectsSortMode.None)
                .First(ball => ball.State == FoodState.Raw);
            Assert.That(raw.Source, Is.EqualTo(rack));
        }

        [UnityTest]
        public IEnumerator BothChambersCookRawButNeverGreenBalls()
        {
            BreakoutBall left = game.SpawnBall(FoodState.Raw, new Vector2(-6.5f, 0.25f));
            BreakoutBall right = game.SpawnBall(FoodState.Raw, new Vector2(6.5f, 0.25f));
            left.Launch(Vector2.left);
            right.Launch(Vector2.right);
            yield return Steps(15);
            Assert.That(left.State, Is.EqualTo(FoodState.Cooked));
            Assert.That(right.State, Is.EqualTo(FoodState.Cooked));
            Assert.That(left.Body.linearVelocity.x, Is.GreaterThan(0));
            Assert.That(right.Body.linearVelocity.x, Is.LessThan(0));
            game.GreenBall.PlaceHeld(new Vector2(-6.5f, 0.25f));
            game.GreenBall.Launch(Vector2.left);
            yield return Steps(15);
            Assert.That(game.GreenBall.State, Is.EqualTo(FoodState.Knockdown));

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
            PattyRack rack = Object.FindFirstObjectByType<PattyRack>();
            Assert.That(rack.TryKnockDown(), Is.True);
            BreakoutBall patty = Object.FindObjectsByType<BreakoutBall>(FindObjectsSortMode.None)
                .First(ball => ball.State == FoodState.Raw);
            patty.Body.position = new Vector2(0, 4);
            patty.Launch(Vector2.up);
            yield return Steps(8);
            Assert.That(patty != null, Is.True, "Raw food must not be served.");
            Assert.That(patty.State, Is.EqualTo(FoodState.Raw));
            Assert.That(rack.IsStocked, Is.False);

            patty.Body.position = new Vector2(0, 4);
            patty.Cook(1);
            patty.Launch(Vector2.up);
            yield return Steps(8);
            Assert.That(patty == null, Is.True, "Cooked food should be served.");
            Assert.That(rack.IsStocked, Is.True);
            Assert.That(game.ActivePattyCount, Is.Zero);
        }

        [UnityTest]
        public IEnumerator PaddleEdgeRedirectsPattyWithoutCookingIt()
        {
            BreakoutBall patty = game.SpawnBall(FoodState.Raw,
                game.PaddlePosition + new Vector2(0.8f, 1.2f));
            patty.Launch(Vector2.down);
            yield return Steps(10);
            Assert.That(patty.Body.linearVelocity.x, Is.GreaterThan(2));
            Assert.That(patty.Body.linearVelocity.y, Is.GreaterThan(0));
            Assert.That(patty.State, Is.EqualTo(FoodState.Raw));
        }

        [UnityTest]
        public IEnumerator MissedFoodRestocksAndGreenBallCanAlwaysRelaunch()
        {
            PattyRack rack = Object.FindFirstObjectByType<PattyRack>();
            rack.TryKnockDown();
            BreakoutBall raw = Object.FindObjectsByType<BreakoutBall>(FindObjectsSortMode.None)
                .First(ball => ball.State == FoodState.Raw);
            raw.Body.position = new Vector2(0, -6);
            raw.Launch(Vector2.down);
            game.GreenBall.PlaceHeld(new Vector2(2, -6));
            game.GreenBall.Launch(Vector2.down);
            yield return Steps(3);
            Assert.That(rack.IsStocked, Is.True);
            Assert.That(game.ActivePattyCount, Is.Zero);
            Assert.That(game.GreenBall.IsHeld, Is.True);
            game.GreenBall.Launch(Vector2.up);
            Assert.That(game.GreenBall.Body.linearVelocity.y, Is.GreaterThan(0));
        }

        [UnityTest]
        public IEnumerator HoldShiftCatchesFoodAndReleaseLaunchesIt()
        {
            Keyboard keyboard = InputSystem.AddDevice<Keyboard>();
            try
            {
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.LeftShift));
                InputSystem.Update();
                yield return null;
                BreakoutBall patty = game.SpawnBall(FoodState.Raw,
                    game.PaddlePosition + Vector2.up * 1.2f);
                patty.Launch(Vector2.down);
                yield return Steps(12);
                Assert.That(game.CaughtPatty, Is.EqualTo(patty));
                Assert.That(patty.IsHeld, Is.True);
                InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                InputSystem.Update();
                yield return null;
                Assert.That(game.CaughtPatty == null, Is.True);
                Assert.That(patty.IsHeld, Is.False);
                Assert.That(patty.Body.linearVelocity.y, Is.GreaterThan(0));
                Assert.That(patty.State, Is.EqualTo(FoodState.Raw));
            }
            finally
            {
                InputSystem.RemoveDevice(keyboard);
            }
        }

        [Test]
        public void ActiveFoodCapAndResetKeepPlaygroundRepeatable()
        {
            PattyRack[] racks = Object.FindObjectsByType<PattyRack>(FindObjectsSortMode.None);
            for (int i = 0; i < game.MaxActivePatties; i++) Assert.That(racks[i].TryKnockDown(), Is.True);
            Assert.That(racks[game.MaxActivePatties].TryKnockDown(), Is.False);
            game.Restart();
            Assert.That(game.ActivePattyCount, Is.Zero);
            Assert.That(racks.All(rack => rack.IsStocked), Is.True);
            Assert.That(game.GreenBall.IsHeld, Is.True);
        }
    }
}
