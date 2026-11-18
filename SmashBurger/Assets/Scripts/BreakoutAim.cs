using UnityEngine;

namespace SmashBurger
{
    public static class BreakoutAim
    {
        // The contact point, not the incoming angle, determines the outgoing shot.
        public static Vector2 FromPaddle(float offset, float halfWidth, float maxAngle)
        {
            float angle = Mathf.Clamp(offset / Mathf.Max(0.01f, halfWidth), -1f, 1f)
                * maxAngle * Mathf.Deg2Rad;
            return new Vector2(Mathf.Sin(angle), Mathf.Cos(angle));
        }

        public static Vector2 Toward(Vector2 origin, Vector2 target, float maxAngle)
        {
            Vector2 delta = target - origin;
            float angle = Mathf.Atan2(delta.x, Mathf.Max(0.05f, delta.y)) * Mathf.Rad2Deg;
            return FromPaddle(Mathf.Clamp(angle, -maxAngle, maxAngle), maxAngle, maxAngle);
        }
    }
}
