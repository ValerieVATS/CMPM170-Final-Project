using System.Collections.Generic;
using UnityEngine;

namespace SmashBurger
{
    public readonly struct Order
    {
        public readonly IngredientKind Item;
        public readonly float PlacedAt;
        public readonly float Patience;
        public readonly int id;

        public Order(IngredientKind item, float placedAt, float patience)
            : this(item, placedAt, patience, 0)
        {
        }

        internal Order(IngredientKind item, float placedAt, float patience, int orderId)
        {
            Item = item;
            PlacedAt = placedAt;
            Patience = Mathf.Max(0.01f, patience);
            id = orderId;
        }

        public float Deadline => PlacedAt + Patience;

        public float Freshness(float now)
        {
            return Mathf.Clamp01((Deadline - now) / Patience);
        }

        public bool IsExpired(float now) => now >= Deadline;
    }

    public readonly struct DeliveryResult
    {
        public readonly bool Correct;
        public readonly bool Scored;
        public readonly int Points;
        public readonly float Freshness;
        public readonly IngredientKind Delivered;
        public readonly IngredientKind Requested;

        public DeliveryResult(bool correct, bool scored, int points, float freshness,
            IngredientKind delivered, IngredientKind requested)
        {
            Correct = correct;
            Scored = scored;
            Points = points;
            Freshness = freshness;
            Delivered = delivered;
            Requested = requested;
        }
    }

    public sealed class ScoringSystem : MonoBehaviour
    {
        public const int BaseOrderPoints = 100;
        public const int MaxTimelinessBonus = 100;
        // Keep the public scoring setting, but wrong orders must not lower the score.
        public const int WrongItemPenalty = 0;
        public const float DefaultPatience = 60f;

        private readonly List<Order> activeOrders = new List<Order>();
        private int nextOrderId;

        public int finalScore { get; private set; }
        public int DeliveredCount { get; private set; }
        public int MistakeCount { get; private set; }
        public IReadOnlyList<Order> ActiveOrders => activeOrders;
        public int LastDeliveryPoints { get; private set; }

        public Order PlaceOrder(IngredientKind item, float patience = DefaultPatience)
        {
            return PlaceOrder(item, Time.time, patience);
        }

        public Order PlaceOrder(IngredientKind item, float now, float patience)
        {
            Order order = new Order(item, now, patience, ++nextOrderId);
            activeOrders.Add(order);
            return order;
        }

        public bool CancelOrder(Order order)
        {
            int index = FindOrderIndex(order);
            if (index < 0) return false;
            activeOrders.RemoveAt(index);
            return true;
        }

        public DeliveryResult Deliver(IngredientKind item)
        {
            return Deliver(item, Time.time);
        }

        public DeliveryResult Deliver(IngredientKind item, float now)
        {
            int index = FindMatchingOrder(item);
            if (activeOrders.Count == 0)
            {
                LastDeliveryPoints = 0;
                return new DeliveryResult(false, false, 0, 0f, item, item);
            }

            return DeliverOrder(index < 0 ? MostUrgentOrder() : activeOrders[index], item, now);
        }

        // Deliver to a particular table, not just any table asking for the same ingredient.
        public DeliveryResult DeliverOrder(Order order, IngredientKind item, float now)
        {
            int index = FindOrderIndex(order);
            if (index < 0 || activeOrders[index].IsExpired(now))
            {
                LastDeliveryPoints = 0;
                return new DeliveryResult(false, false, 0, 0f, item, order.Item);
            }

            Order activeOrder = activeOrders[index];
            if (activeOrder.Item != item)
            {
                int wrongPoints = ScoreDelivery(false, 0f);
                MistakeCount++;
                LastDeliveryPoints = wrongPoints;
                return new DeliveryResult(false, true, wrongPoints, 0f, item, activeOrder.Item);
            }

            activeOrders.RemoveAt(index);
            float freshness = activeOrder.Freshness(now);
            int points = ScoreDelivery(true, freshness);
            finalScore += points;
            DeliveredCount++;
            LastDeliveryPoints = points;
            return new DeliveryResult(true, true, points, freshness, item, activeOrder.Item);
        }

        public DeliveryResult DeliverOrder(Order order, IngredientKind item)
        {
            return DeliverOrder(order, item, Time.time);
        }

        public static int ScoreDelivery(bool correct, float freshness)
        {
            if (!correct) return -WrongItemPenalty;
            int bonus = Mathf.RoundToInt(MaxTimelinessBonus * Mathf.Clamp01(freshness));
            return BaseOrderPoints + bonus;
        }

        public void ResetScore()
        {
            activeOrders.Clear();
            finalScore = 0;
            DeliveredCount = 0;
            MistakeCount = 0;
            LastDeliveryPoints = 0;
        }

        private int FindMatchingOrder(IngredientKind item)
        {
            int best = -1;
            for (int i = 0; i < activeOrders.Count; i++)
            {
                if (activeOrders[i].Item != item) continue;
                if (best < 0 || activeOrders[i].Deadline < activeOrders[best].Deadline) best = i;
            }
            return best;
        }

        private int FindOrderIndex(Order order)
        {
            for (int i = 0; i < activeOrders.Count; i++)
                if (activeOrders[i].id == order.id) return i;
            return -1;
        }

        private Order MostUrgentOrder()
        {
            Order urgent = activeOrders[0];
            for (int i = 1; i < activeOrders.Count; i++)
                if (activeOrders[i].Deadline < urgent.Deadline) urgent = activeOrders[i];
            return urgent;
        }
    }
}
