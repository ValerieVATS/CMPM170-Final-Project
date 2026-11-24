using UnityEngine;

namespace SmashBurger
{
    /// <summary>Identifies the food a rack holds and a breakout ball carries.</summary>
    public enum IngredientKind { Meat, Cheese, Lettuce, Tomato, Onion, Pickle }

    public static class IngredientInfo
    {
        // One rack of each burger layer, repeated across the shelf. Pickle is reserved for the launcher.
        private static readonly IngredientKind[] RackMenu =
            { IngredientKind.Meat, IngredientKind.Cheese, IngredientKind.Lettuce, IngredientKind.Tomato, IngredientKind.Onion };

        public static int orderableCount => RackMenu.Length;

        public static IngredientKind ForRackSlot(int slot)
        {
            return RackMenu[Mathf.Abs(slot) % RackMenu.Length];
        }

        public static string Name(IngredientKind kind)
        {
            switch (kind)
            {
                case IngredientKind.Meat: return "Meat";
                case IngredientKind.Cheese: return "Cheese";
                case IngredientKind.Lettuce: return "Lettuce";
                case IngredientKind.Tomato: return "Tomato";
                case IngredientKind.Onion: return "Onion";
                default: return "Pickle";
            }
        }

        /// <summary>All-caps wording for the playtest HUD, with the ingredient's cooked name.</summary>
        public static string Label(IngredientKind kind, bool prepared)
        {
            if (!prepared)
            {
                switch (kind)
                {
                    case IngredientKind.Meat: return "RAW PATTY";
                    case IngredientKind.Cheese: return "CHEESE";
                    case IngredientKind.Lettuce: return "LETTUCE";
                    case IngredientKind.Tomato: return "TOMATO";
                    case IngredientKind.Onion: return "ONION";
                    default: return "PICKLE";
                }
            }
            switch (kind)
            {
                case IngredientKind.Meat: return "COOKED PATTY";
                case IngredientKind.Cheese: return "MELTED CHEESE";
                case IngredientKind.Lettuce: return "WILTED LETTUCE";
                case IngredientKind.Tomato: return "ROASTED TOMATO";
                case IngredientKind.Onion: return "CARAMELIZED ONION";
                default: return "PICKLE";
            }
        }

        public static Color ColorFor(IngredientKind kind, FoodState state)
        {
            return ColorFor(kind, state == FoodState.Cooked);
        }

        public static Color ColorFor(IngredientKind kind, bool prepared)
        {
            switch (kind)
            {
                case IngredientKind.Meat:
                    return prepared ? new Color32(110, 58, 38, 255) : new Color32(226, 83, 75, 255);
                case IngredientKind.Cheese:
                    return prepared ? new Color32(240, 150, 40, 255) : new Color32(244, 197, 66, 255);
                case IngredientKind.Lettuce:
                    return prepared ? new Color32(71, 140, 48, 255) : new Color32(116, 201, 71, 255);
                case IngredientKind.Tomato:
                    return prepared ? new Color32(168, 42, 34, 255) : new Color32(227, 59, 50, 255);
                case IngredientKind.Onion:
                    return prepared ? new Color32(217, 167, 92, 255) : new Color32(242, 231, 247, 255);
                default:
                    return prepared ? new Color32(80, 145, 50, 255) : new Color32(109, 190, 69, 255);
            }
        }

        public static Color AccentFor(IngredientKind kind, bool prepared)
        {
            switch (kind)
            {
                case IngredientKind.Meat:
                    return prepared ? new Color32(48, 28, 20, 255) : new Color32(244, 130, 120, 255);
                case IngredientKind.Cheese:
                    return prepared ? new Color32(184, 99, 18, 255) : new Color32(198, 140, 32, 255);
                case IngredientKind.Lettuce:
                    return prepared ? new Color32(120, 174, 95, 255) : new Color32(163, 224, 110, 255);
                case IngredientKind.Tomato:
                    return prepared ? new Color32(205, 95, 80, 255) : new Color32(250, 130, 112, 255);
                case IngredientKind.Onion:
                    return prepared ? new Color32(160, 109, 52, 255) : new Color32(196, 160, 214, 255);
                default:
                    return prepared ? new Color32(130, 190, 90, 255) : new Color32(160, 220, 110, 255);
            }
        }
    }
}
