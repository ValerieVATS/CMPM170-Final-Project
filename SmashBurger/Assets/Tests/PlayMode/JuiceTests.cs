using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace SmashBurger.Tests
{
    public sealed class JuiceTests
    {
        private BurgerBreakout game;
        private JuiceWatcher watcher;

        [UnitySetUp]
        public IEnumerator LoadPrototype()
        {
            yield return SceneManager.LoadSceneAsync("SampleScene");
            yield return new WaitForFixedUpdate();
            game = Object.FindFirstObjectByType<BurgerBreakout>();
            watcher = Object.FindFirstObjectByType<JuiceWatcher>();
        }

        private static IEnumerator Steps(int count)
        {
            for (int i = 0; i < count; i++) yield return new WaitForFixedUpdate();
        }

        [Test]
        public void JuiceAttachesItselfOnceWithoutSceneChanges()
        {
            Assert.That(game, Is.Not.Null);
            Assert.That(watcher, Is.Not.Null, "JuiceWatcher should add itself when the scene loads.");
            Assert.That(watcher.Effects, Is.Not.Null);
            Assert.That(watcher.Effects.Enabled, Is.True);
            Assert.That(Object.FindObjectsByType<JuiceWatcher>(FindObjectsSortMode.None).Length, Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator TogglingJuiceOffSettlesTheCamera()
        {
            Transform view = Camera.main.transform;
            Vector3 rest = view.position;
            watcher.Effects.Shake(1);
            yield return null;
            watcher.Effects.Toggle();
            Assert.That(watcher.Effects.Enabled, Is.False);
            Assert.That(view.position, Is.EqualTo(rest));
            yield return Steps(5);
            Assert.That(view.position, Is.EqualTo(rest));
            watcher.Effects.Toggle();
            Assert.That(watcher.Effects.Enabled, Is.True);
        }

        [UnityTest]
        public IEnumerator CookAndServeStillWorkWithJuiceOn()
        {
            IngredientRack rack = Object.FindFirstObjectByType<IngredientRack>();
            Assert.That(rack.TryKnockDown(), Is.True);
            yield return Steps(2);
            BreakoutBall food = Object.FindObjectsByType<BreakoutBall>(FindObjectsSortMode.None)
                .First(ball => ball.State == FoodState.Raw);
            Assert.That(food.transform.Find("Juice stretch"), Is.Not.Null, "Food art should sit under the juice pivots.");
            Assert.That(food.transform.localScale, Is.EqualTo(Vector3.one), "The ball itself (and its collider) is never scaled.");

            food.Body.position = new Vector2(0, 4);
            food.Cook(1);
            food.Launch(Vector2.up);
            yield return Steps(8);
            Assert.That(food == null, Is.True, "Cooked food should still be served.");
            Assert.That(rack.IsStocked, Is.True);
        }
    }
}
