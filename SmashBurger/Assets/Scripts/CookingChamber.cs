using UnityEngine;

namespace SmashBurger
{
    [RequireComponent(typeof(BoxCollider2D))]
    public sealed class CookingChamber : MonoBehaviour
    {
        private float inwardDirection;

        public void Initialize(float inward)
        {
            inwardDirection = inward;
            BoxCollider2D zone = GetComponent<BoxCollider2D>();
            zone.isTrigger = true;
            zone.size = new Vector2(1.35f, 2.2f);
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            BreakoutBall ball = other.GetComponent<BreakoutBall>();
            if (ball != null) ball.Cook(inwardDirection);
        }
    }
}
