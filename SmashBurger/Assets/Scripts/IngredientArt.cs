using UnityEngine;

namespace SmashBurger
{
    /// <summary>Cheap, readable ingredient icons drawn from the generated square and circle sprites.</summary>
    public static class IngredientArt
    {
        public static GameObject Build(BurgerBreakout game, Transform parent, IngredientKind kind,
            float diameter, bool prepared, int order)
        {
            GameObject root = new GameObject(IngredientInfo.Name(kind) + (prepared ? " prepared art" : " art"));
            root.transform.SetParent(parent, false);
            Color body = IngredientInfo.ColorFor(kind, prepared);
            Color accent = IngredientInfo.AccentFor(kind, prepared);

            switch (kind)
            {
                case IngredientKind.Meat:
                    Part(game, root.transform, "Patty", Vector2.zero, Vector2.one * diameter, body, true, order);
                    for (int i = -1; i <= 1; i++)
                    {
                        SpriteRenderer mark = Part(game, root.transform, "Grill mark",
                            new Vector2(i * diameter * 0.19f, 0), new Vector2(diameter * 0.07f, diameter * 0.66f),
                            accent, false, order + 1);
                        mark.transform.localRotation = Quaternion.Euler(0, 0, -22);
                    }
                    Part(game, root.transform, "Patty shine", new Vector2(-diameter * 0.2f, diameter * 0.22f),
                        new Vector2(diameter * 0.26f, diameter * 0.1f), new Color(1, 1, 1, prepared ? 0.12f : 0.3f), true, order + 2);
                    break;

                case IngredientKind.Cheese:
                    SpriteRenderer slice = Part(game, root.transform, "Cheese slice", Vector2.zero,
                        new Vector2(diameter * 0.82f, diameter * 0.78f), body, false, order);
                    slice.transform.localRotation = Quaternion.Euler(0, 0, -8);
                    Hole(game, root.transform, new Vector2(-0.12f, 0.12f) * diameter, diameter * 0.11f, accent, order + 1);
                    Hole(game, root.transform, new Vector2(0.16f, -0.06f) * diameter, diameter * 0.08f, accent, order + 1);
                    Hole(game, root.transform, new Vector2(0.02f, -0.2f) * diameter, diameter * 0.06f, accent, order + 1);
                    break;

                case IngredientKind.Lettuce:
                    for (int i = 0; i < 8; i++)
                    {
                        float angle = i * Mathf.PI * 2f / 8f;
                        Vector2 offset = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * diameter * 0.31f;
                        Part(game, root.transform, "Leaf", offset, Vector2.one * diameter * 0.34f,
                            i % 2 == 0 ? body : accent, true, order);
                    }
                    Part(game, root.transform, "Lettuce core", Vector2.zero, Vector2.one * diameter * 0.68f, body, true, order + 1);
                    Part(game, root.transform, "Lettuce vein", new Vector2(0, diameter * 0.08f),
                        new Vector2(diameter * 0.44f, diameter * 0.08f), new Color(1, 1, 1, 0.22f), true, order + 2);
                    break;

                case IngredientKind.Tomato:
                    Part(game, root.transform, "Tomato slice", Vector2.zero, Vector2.one * diameter * 0.96f, body, true, order);
                    Part(game, root.transform, "Tomato flesh", Vector2.zero, Vector2.one * diameter * 0.68f, accent, true, order + 1);
                    Part(game, root.transform, "Tomato core", Vector2.zero, Vector2.one * diameter * 0.2f,
                        new Color(1, 0.85f, 0.62f, 0.9f), true, order + 2);
                    for (int i = 0; i < 4; i++)
                    {
                        float angle = i * Mathf.PI * 0.5f + Mathf.PI * 0.25f;
                        Vector2 seedPosition = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * diameter * 0.22f;
                        Hole(game, root.transform, seedPosition, diameter * 0.055f, new Color(1, 0.9f, 0.68f, 0.85f), order + 2);
                    }
                    break;

                case IngredientKind.Onion:
                    Part(game, root.transform, "Onion ring", Vector2.zero, Vector2.one * diameter * 0.96f, body, true, order);
                    Part(game, root.transform, "Onion layer", Vector2.zero, Vector2.one * diameter * 0.72f, accent, true, order + 1);
                    Part(game, root.transform, "Onion core", Vector2.zero, Vector2.one * diameter * 0.52f, body, true, order + 2);
                    Part(game, root.transform, "Onion center", Vector2.zero, Vector2.one * diameter * 0.22f, accent, true, order + 3);
                    break;

                default:
                    Part(game, root.transform, "Pickle", Vector2.zero, Vector2.one * diameter * 0.96f, body, true, order);
                    for (int i = 0; i < 5; i++)
                    {
                        float angle = i * Mathf.PI * 2f / 5f + 0.35f;
                        Vector2 bump = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * diameter * 0.29f;
                        Hole(game, root.transform, bump, diameter * 0.17f, accent, order + 1);
                    }
                    Part(game, root.transform, "Pickle shine", new Vector2(-diameter * 0.14f, diameter * 0.16f),
                        new Vector2(diameter * 0.22f, diameter * 0.1f), new Color(1, 1, 1, 0.3f), true, order + 2);
                    break;
            }
            return root;
        }

        private static SpriteRenderer Part(BurgerBreakout game, Transform parent, string name, Vector2 position,
            Vector2 size, Color color, bool round, int order)
        {
            return game.Shape(name, parent, position, size, color, round, order);
        }

        private static void Hole(BurgerBreakout game, Transform parent, Vector2 position, float size, Color color, int order)
        {
            Part(game, parent, "Detail", position, Vector2.one * size, color, true, order);
        }
    }
}
