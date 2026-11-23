using NUnit.Framework;
using UnityEngine;

namespace SmashBurger.Tests
{
    public sealed class ScoringSystemTests
    {
        [Test]
        public void ScoreDeliveryRewardsCorrectAndTimelyItems()
        {
            Assert.That(ScoringSystem.ScoreDelivery(true, 1f),
                Is.EqualTo(ScoringSystem.BaseOrderPoints + ScoringSystem.MaxTimelinessBonus));
            Assert.That(ScoringSystem.ScoreDelivery(true, 0.5f),
                Is.EqualTo(ScoringSystem.BaseOrderPoints + ScoringSystem.MaxTimelinessBonus / 2));
            Assert.That(ScoringSystem.ScoreDelivery(true, 0f), Is.EqualTo(ScoringSystem.BaseOrderPoints));
            Assert.That(ScoringSystem.ScoreDelivery(false, 1f), Is.EqualTo(-ScoringSystem.WrongItemPenalty));
        }

        [Test]
        public void DeliverScoresMatchingOrderAndAddsToFinalScore()
        {
            GameObject host = new GameObject("Scoring");
            try
            {
                ScoringSystem scoring = host.AddComponent<ScoringSystem>();
                scoring.PlaceOrder(IngredientKind.Meat, 0f, 10f);
                DeliveryResult result = scoring.Deliver(IngredientKind.Meat, 0f);
                Assert.That(result.Scored, Is.True);
                Assert.That(result.Correct, Is.True);
                Assert.That(result.Freshness, Is.EqualTo(1f).Within(0.0001f));
                Assert.That(scoring.finalScore,
                    Is.EqualTo(ScoringSystem.BaseOrderPoints + ScoringSystem.MaxTimelinessBonus));
                Assert.That(scoring.ActiveOrders, Is.Empty);
                Assert.That(scoring.DeliveredCount, Is.EqualTo(1));
            }
            finally
            {
                Object.DestroyImmediate(host);
            }
        }

        [Test]
        public void LateDeliveryLosesTimelinessBonus()
        {
            GameObject host = new GameObject("Scoring");
            try
            {
                ScoringSystem scoring = host.AddComponent<ScoringSystem>();
                scoring.PlaceOrder(IngredientKind.Cheese, 0f, 10f);
                DeliveryResult result = scoring.Deliver(IngredientKind.Cheese, 5f);
                Assert.That(result.Correct, Is.True);
                Assert.That(result.Points, Is.EqualTo(ScoringSystem.BaseOrderPoints + ScoringSystem.MaxTimelinessBonus / 2));
                Assert.That(scoring.finalScore, Is.EqualTo(result.Points));
            }
            finally
            {
                Object.DestroyImmediate(host);
            }
        }

        [Test]
        public void WrongItemPenalisesButKeepsOrderActive()
        {
            GameObject host = new GameObject("Scoring");
            try
            {
                ScoringSystem scoring = host.AddComponent<ScoringSystem>();
                scoring.PlaceOrder(IngredientKind.Meat, 0f, 10f);
                DeliveryResult result = scoring.Deliver(IngredientKind.Cheese, 1f);
                Assert.That(result.Scored, Is.True);
                Assert.That(result.Correct, Is.False);
                Assert.That(result.Requested, Is.EqualTo(IngredientKind.Meat));
                Assert.That(result.Points, Is.EqualTo(-ScoringSystem.WrongItemPenalty));
                Assert.That(scoring.finalScore, Is.Zero);
                Assert.That(scoring.ActiveOrders.Count, Is.EqualTo(1));
                Assert.That(scoring.MistakeCount, Is.EqualTo(1));
            }
            finally
            {
                Object.DestroyImmediate(host);
            }
        }

        [Test]
        public void DeliveryWithNoOrdersIsIgnored()
        {
            GameObject host = new GameObject("Scoring");
            try
            {
                ScoringSystem scoring = host.AddComponent<ScoringSystem>();
                DeliveryResult result = scoring.Deliver(IngredientKind.Tomato, 1f);
                Assert.That(result.Scored, Is.False);
                Assert.That(scoring.finalScore, Is.Zero);
            }
            finally
            {
                Object.DestroyImmediate(host);
            }
        }
    }
}
