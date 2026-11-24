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
            Assert.That(ScoringSystem.ScoreDelivery(false, 1f), Is.Zero);
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
        public void WrongItemKeepsOrderActiveWithoutChangingScore()
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
                Assert.That(result.Points, Is.Zero);
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
        public void RepeatedMistakesNeverReduceScoreBeforeOrAfterACorrectDelivery()
        {
            GameObject host = new GameObject("Scoring");
            try
            {
                ScoringSystem scoring = host.AddComponent<ScoringSystem>();
                Order order = scoring.PlaceOrder(IngredientKind.Meat);
                Assert.That(order.Patience, Is.EqualTo(60f));
                for (int i = 0; i < 5; i++)
                {
                    DeliveryResult wrong = scoring.DeliverOrder(order, IngredientKind.Cheese, order.PlacedAt + 1f);
                    Assert.That(wrong.Points, Is.Zero);
                }
                Assert.That(scoring.finalScore, Is.Zero);

                DeliveryResult correct = scoring.DeliverOrder(order, IngredientKind.Meat, order.PlacedAt + 1f);
                Assert.That(correct.Correct, Is.True);
                Assert.That(scoring.finalScore, Is.EqualTo(correct.Points));

                Order next = scoring.PlaceOrder(IngredientKind.Cheese);
                for (int i = 0; i < 5; i++)
                    scoring.DeliverOrder(next, IngredientKind.Meat, next.PlacedAt + 1f);
                Assert.That(scoring.finalScore, Is.EqualTo(correct.Points));
                Assert.That(scoring.MistakeCount, Is.EqualTo(10));
                scoring.ResetScore();
                Assert.That(scoring.finalScore, Is.Zero);
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

        [Test]
        public void SpecificDeliveryKeepsOtherIdenticalTableOrdersActive()
        {
            GameObject host = new GameObject("Scoring");
            try
            {
                ScoringSystem scoring = host.AddComponent<ScoringSystem>();
                Order first = scoring.PlaceOrder(IngredientKind.Meat, 0f, 10f);
                Order second = scoring.PlaceOrder(IngredientKind.Meat, 0f, 10f);
                Assert.That(first.id, Is.Not.EqualTo(second.id));

                DeliveryResult result = scoring.DeliverOrder(second, IngredientKind.Meat, 5f);
                Assert.That(result.Correct, Is.True);
                Assert.That(result.Points, Is.EqualTo(150));
                Assert.That(scoring.ActiveOrders.Count, Is.EqualTo(1));
                Assert.That(scoring.ActiveOrders[0].id, Is.EqualTo(first.id));
                Assert.That(scoring.CancelOrder(first), Is.True);
                Assert.That(scoring.ActiveOrders, Is.Empty);
            }
            finally
            {
                Object.DestroyImmediate(host);
            }
        }

        [Test]
        public void SpecificWrongAndExpiredOrdersNeverScoreAnotherTable()
        {
            GameObject host = new GameObject("Scoring");
            try
            {
                ScoringSystem scoring = host.AddComponent<ScoringSystem>();
                Order meat = scoring.PlaceOrder(IngredientKind.Meat, 0f, 10f);
                Order cheese = scoring.PlaceOrder(IngredientKind.Cheese, 0f, 20f);

                DeliveryResult wrong = scoring.DeliverOrder(meat, IngredientKind.Cheese, 1f);
                Assert.That(wrong.Correct, Is.False);
                Assert.That(wrong.Points, Is.Zero);
                Assert.That(scoring.ActiveOrders.Count, Is.EqualTo(2));
                Assert.That(scoring.MistakeCount, Is.EqualTo(1));

                DeliveryResult late = scoring.DeliverOrder(meat, IngredientKind.Meat, 10f);
                Assert.That(late.Scored, Is.False);
                Assert.That(scoring.finalScore, Is.Zero);
                Assert.That(scoring.DeliveredCount, Is.Zero);
                Assert.That(scoring.ActiveOrders[1].id, Is.EqualTo(cheese.id));
            }
            finally
            {
                Object.DestroyImmediate(host);
            }
        }
    }
}
