using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace SmashBurger
{
    /// <summary>A self-contained, endless aiming playground. All artwork is generated at runtime.</summary>
    public sealed class BurgerBreakout : MonoBehaviour
    {
        public const float DrainY = -5.65f;
        public static readonly Color Cream = new Color32(244, 233, 206, 255);
        public static readonly Color Green = new Color32(91, 230, 123, 255);
        public static readonly Color RawRed = new Color32(239, 64, 67, 255);
        public static readonly Color Gold = new Color32(246, 182, 76, 255);
        private static readonly Color Steel = new Color32(47, 65, 71, 255);

        [Header("Aiming playtest settings")]
        [SerializeField, Min(1)] private int maxActivePatties = 3;
        [SerializeField, Range(35, 75)] private float maxBounceAngle = 68;
        [SerializeField, Range(1, 4)] private float paddleWidth = 2.2f;
        [SerializeField, Min(1)] private float paddleSpeed = 16;
        [SerializeField, Min(1)] private float greenBallSpeed = 8;
        [SerializeField, Min(1)] private float pattySpeed = 6;
        [SerializeField] private bool showAimGuide = true;

        public int MaxActivePatties => maxActivePatties;
        public int ActivePattyCount { get; private set; }
        public BoxCollider2D PaddleCollider { get; private set; }
        public Vector2 PaddlePosition => paddle.position;
        public PhysicsMaterial2D BounceMaterial { get; private set; }
        public Material SpriteMaterial { get; private set; }
        public BreakoutBall GreenBall { get; private set; }
        public BreakoutBall CaughtPatty { get; private set; }

        private readonly List<BreakoutBall> balls = new List<BreakoutBall>();
        private readonly List<PattyRack> racks = new List<PattyRack>();
        private readonly List<Object> generatedAssets = new List<Object>();
        private Rigidbody2D paddle;
        private Transform world;
        private Camera view;
        private Sprite square;
        private Sprite circle;
        private Font font;
        private LineRenderer guide;
        private float targetX;
        private bool catchHeld;
        private Vector2 aimDirection = Vector2.up;
        private Vector2 lastMousePosition;
        private string message = "Launch the green ball to knock a red patty off the rack.";
        private Color messageColor;
        private float messageUntil;
        private GUIStyle titleStyle, textStyle, smallStyle;

        private void Awake()
        {
            view = Camera.main;
            if (view == null)
            {
                view = new GameObject("Main Camera", typeof(Camera)).GetComponent<Camera>();
                view.tag = "MainCamera";
            }
            view.orthographic = true;
            view.transform.position = new Vector3(0, 0, -10);
            view.clearFlags = CameraClearFlags.SolidColor;
            view.backgroundColor = new Color32(16, 24, 29, 255);
            FitCamera();

            BounceMaterial = new PhysicsMaterial2D("Frictionless breakout") { friction = 0, bounciness = 1 };
            generatedAssets.Add(BounceMaterial);
            Shader shader = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default");
            if (shader == null) shader = Shader.Find("Sprites/Default");
            SpriteMaterial = new Material(shader);
            generatedAssets.Add(SpriteMaterial);
            square = MakeSprite(false);
            circle = MakeSprite(true);
            font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            BuildCourt();
            Restart();
        }

        private void FitCamera()
        {
            view.orthographicSize = Mathf.Max(6.8f, 10f / Mathf.Max(0.25f, view.aspect));
        }

        private Sprite MakeSprite(bool round)
        {
            int size = round ? 64 : 2;
            Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            texture.name = round ? "Generated circle" : "Generated square";
            texture.filterMode = FilterMode.Bilinear;
            texture.wrapMode = TextureWrapMode.Clamp;
            Color[] pixels = new Color[size * size];
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float distance = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), Vector2.one * size * 0.5f);
                pixels[y * size + x] = new Color(1, 1, 1, round ? Mathf.Clamp01(size * 0.5f - distance) : 1);
            }
            texture.SetPixels(pixels);
            texture.Apply();
            Sprite sprite = Sprite.Create(texture, new Rect(0, 0, size, size), Vector2.one * 0.5f, size);
            generatedAssets.Add(texture);
            generatedAssets.Add(sprite);
            return sprite;
        }

        public SpriteRenderer Shape(string label, Transform parent, Vector2 position, Vector2 size,
            Color color, bool round = false, int order = 0)
        {
            GameObject item = new GameObject(label);
            item.transform.SetParent(parent, false);
            item.transform.localPosition = position;
            item.transform.localScale = size;
            SpriteRenderer image = item.AddComponent<SpriteRenderer>();
            image.sprite = round ? circle : square;
            image.sharedMaterial = SpriteMaterial;
            image.color = color;
            image.sortingOrder = order;
            return image;
        }

        private void Label(string text, Transform parent, Vector2 position, float size, Color color)
        {
            GameObject item = new GameObject(text);
            item.transform.SetParent(parent, false);
            item.transform.localPosition = position;
            TextMesh label = item.AddComponent<TextMesh>();
            label.text = text;
            label.font = font;
            label.fontSize = 48;
            label.characterSize = size;
            label.anchor = TextAnchor.MiddleCenter;
            label.alignment = TextAlignment.Center;
            label.color = color;
            MeshRenderer renderer = item.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = font.material;
            renderer.sortingOrder = 6;
        }

        private void Wall(string name, Vector2 position, Vector2 size)
        {
            SpriteRenderer image = Shape(name, world, position, size, Steel, false, 1);
            BoxCollider2D collider = image.gameObject.AddComponent<BoxCollider2D>();
            collider.sharedMaterial = BounceMaterial;
        }

        private void BuildCourt()
        {
            world = new GameObject("Kitchen court (generated)").transform;
            world.SetParent(transform, false);
            Shape("Court", world, Vector2.zero, new Vector2(17.2f, 10.8f), new Color32(24, 36, 41, 255), false, -10);
            for (int x = -8; x <= 8; x++)
                Shape("Tile seam", world, new Vector2(x, 0), new Vector2(0.015f, 10.8f), new Color32(31, 45, 49, 255), false, -9);
            for (int y = -5; y <= 5; y++)
                Shape("Tile seam", world, new Vector2(0, y), new Vector2(17.2f, 0.015f), new Color32(31, 45, 49, 255), false, -9);
            Wall("Left wall", new Vector2(-8.6f, 0), new Vector2(0.2f, 11));
            Wall("Right wall", new Vector2(8.6f, 0), new Vector2(0.2f, 11));
            Wall("Ceiling", new Vector2(0, 5.5f), new Vector2(17.4f, 0.2f));
            Shape("Drain", world, new Vector2(0, -5.25f), new Vector2(16.8f, 0.035f), Steel);
            Label("MISSED FOOD RETURNS TO THE RACK", world, new Vector2(0, -5.02f), 0.065f, new Color32(121, 148, 153, 255));

            for (int side = -1; side <= 1; side += 2)
            {
                GameObject chamber = new GameObject(side < 0 ? "Left cooking chamber" : "Right cooking chamber");
                chamber.transform.SetParent(world, false);
                chamber.transform.localPosition = new Vector2(side * 7.8f, 0.25f);
                chamber.AddComponent<CookingChamber>().Initialize(-side);
                Shape("Cooker rim", chamber.transform, Vector2.zero, new Vector2(1.38f, 2.5f), Gold);
                Shape("Cooking zone", chamber.transform, Vector2.zero, new Vector2(1.22f, 2.32f), new Color32(85, 48, 31, 255), false, 1);
                for (int i = -3; i <= 3; i++)
                    Shape("Grill", chamber.transform, new Vector2(0, i * 0.27f), new Vector2(1.05f, 0.055f), new Color32(190, 100, 43, 255), false, 2);
                Label("COOK", chamber.transform, new Vector2(0, 1.55f), 0.13f, Gold);
                Label("RED\nTO BLACK", chamber.transform, new Vector2(0, -1.65f), 0.095f, Cream);
            }

            for (int row = 0; row < 2; row++)
            {
                float y = 2.65f + row * 1.05f;
                Shape("Ingredient shelf", world, new Vector2(0, y - 0.42f), new Vector2(12.4f, 0.08f), Steel);
                for (int col = 0; col < 7; col++)
                {
                    GameObject slot = new GameObject("Raw patty rack " + row + "/" + col);
                    slot.transform.SetParent(world, false);
                    slot.transform.localPosition = new Vector2(-5.4f + col * 1.8f, y);
                    PattyRack rack = slot.AddComponent<PattyRack>();
                    rack.Initialize(this);
                    racks.Add(rack);
                }
            }

            for (int i = -1; i <= 1; i++)
            {
                GameObject target = new GameObject("Bun serving target");
                target.transform.SetParent(world, false);
                target.transform.localPosition = new Vector2(i * 4.6f, 4.8f);
                SpriteRenderer bun = Shape("Bun", target.transform, Vector2.zero, new Vector2(1.6f, 0.55f), Gold, true, 3);
                Shape("Lettuce", target.transform, new Vector2(0, -0.14f), new Vector2(1.5f, 0.065f), Green, false, 4);
                for (int seed = -1; seed <= 1; seed++)
                    Shape("Sesame", target.transform, new Vector2(seed * 0.3f, 0.10f), new Vector2(0.09f, 0.035f), Cream, true, 4);
                Label("SERVE BLACK", target.transform, new Vector2(0, 0.43f), 0.075f, Cream);
                BoxCollider2D hitbox = target.AddComponent<BoxCollider2D>();
                hitbox.size = new Vector2(1.65f, 0.55f);
                hitbox.sharedMaterial = BounceMaterial;
                target.AddComponent<ServingTarget>().Initialize(this, bun);
            }

            SpriteRenderer paddleImage = Shape("Spatula paddle", world, new Vector2(0, -4.35f),
                new Vector2(paddleWidth, 0.22f), Cream, false, 5);
            // Keep the physics root unscaled so the box and held-ball math use world units.
            GameObject paddleRoot = new GameObject("Player paddle");
            paddleRoot.transform.SetParent(world, false);
            paddleRoot.transform.localPosition = paddleImage.transform.localPosition;
            paddleImage.transform.SetParent(paddleRoot.transform, true);
            paddle = paddleRoot.AddComponent<Rigidbody2D>();
            paddle.bodyType = RigidbodyType2D.Kinematic;
            paddle.interpolation = RigidbodyInterpolation2D.Interpolate;
            PaddleCollider = paddleRoot.AddComponent<BoxCollider2D>();
            PaddleCollider.size = new Vector2(paddleWidth, 0.22f);
            PaddleCollider.sharedMaterial = BounceMaterial;
            Shape("Spatula handle", paddleRoot.transform, new Vector2(0, -0.31f), new Vector2(0.15f, 0.48f), Gold, false, 4);
            for (int i = -2; i <= 2; i++)
                Shape("Paddle slots", paddleRoot.transform, new Vector2(i * paddleWidth * 0.15f, 0), new Vector2(0.045f, 0.12f), Steel, false, 6);

            GameObject guideObject = new GameObject("Aim guide");
            guideObject.transform.SetParent(world, false);
            guide = guideObject.AddComponent<LineRenderer>();
            guide.sharedMaterial = SpriteMaterial;
            guide.startWidth = 0.035f;
            guide.endWidth = 0.012f;
            guide.sortingOrder = 12;
            guide.useWorldSpace = true;
            guide.positionCount = 3;
        }

        public float SpeedFor(FoodState state) => state == FoodState.Knockdown ? greenBallSpeed : pattySpeed;

        public BreakoutBall SpawnBall(FoodState state, Vector2 position, PattyRack source = null)
        {
            GameObject item = new GameObject(state + " ball");
            item.transform.SetParent(world, false);
            item.transform.position = position;
            BreakoutBall ball = item.AddComponent<BreakoutBall>();
            ball.Initialize(this, state, source);
            // Independent balls should not randomly spoil each other's carefully aimed shots.
            foreach (BreakoutBall other in balls)
                if (other != null) Physics2D.IgnoreCollision(ball.GetComponent<Collider2D>(), other.GetComponent<Collider2D>());
            balls.Add(ball);
            if (state == FoodState.Knockdown) GreenBall = ball;
            else ActivePattyCount++;
            return ball;
        }

        public void Restart()
        {
            foreach (BreakoutBall ball in balls.ToArray())
                if (ball != null) ball.Retire();
            balls.Clear();
            ActivePattyCount = 0;
            CaughtPatty = null;
            foreach (PattyRack rack in racks) rack.Restock();
            targetX = 0;
            paddle.position = new Vector2(0, -4.35f);
            GreenBall = SpawnBall(FoodState.Knockdown, HoldPosition(0.19f));
            GreenBall.Hold();
            Feedback("Launch the green ball. Paddle edges give steeper bank shots.", Cream);
        }

        public void Forget(BreakoutBall ball)
        {
            balls.Remove(ball);
            if (ball.State != FoodState.Knockdown) ActivePattyCount = Mathf.Max(0, ActivePattyCount - 1);
            if (CaughtPatty == ball) CaughtPatty = null;
            if (GreenBall == ball) GreenBall = null;
        }

        public void Lose(BreakoutBall ball)
        {
            if (ball.State == FoodState.Knockdown)
            {
                ball.Hold();
                ball.PlaceHeld(HoldPosition(ball.Radius));
                Feedback("Green ball ready — click or Space to relaunch.", Green);
            }
            else
            {
                ball.Retire();
                Feedback("Patty returned to its rack. Try another bank shot.", Cream);
            }
        }

        private Vector2 HoldPosition(float radius) => PaddlePosition + Vector2.up * (0.14f + radius);

        public void HitPaddle(BreakoutBall ball)
        {
            if (ball.State != FoodState.Knockdown && catchHeld && CaughtPatty == null)
            {
                CaughtPatty = ball;
                ball.Hold();
                ball.PlaceHeld(HoldPosition(ball.Radius));
                Feedback("CAUGHT — point above the paddle; release to aim", Cream);
                return;
            }
            ball.Launch(BreakoutAim.FromPaddle(ball.Body.position.x - paddle.position.x, paddleWidth * 0.5f, maxBounceAngle));
        }

        private void Update()
        {
            FitCamera();
            Keyboard keyboard = Keyboard.current;
            Mouse mouse = Mouse.current;
            Vector2 pointer = mouse != null ? mouse.position.ReadValue() : lastMousePosition;
            bool mouseMoved = (pointer - lastMousePosition).sqrMagnitude > 0.1f;
            Vector2 mouseWorld = view.ScreenToWorldPoint(pointer);
            catchHeld = (mouse != null && mouse.rightButton.isPressed)
                || (keyboard != null && (keyboard.leftShiftKey.isPressed || keyboard.rightShiftKey.isPressed));

            float move = 0;
            if (keyboard != null)
            {
                if (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed) move -= 1;
                if (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed) move += 1;
                if (keyboard.rKey.wasPressedThisFrame) Restart();
                if (keyboard.gKey.wasPressedThisFrame) showAimGuide = !showAimGuide;
            }
            if (move != 0) targetX += move * paddleSpeed * Time.deltaTime;
            else if (mouseMoved && CaughtPatty == null) targetX = mouseWorld.x;
            targetX = Mathf.Clamp(targetX, -8.4f + paddleWidth * 0.5f, 8.4f - paddleWidth * 0.5f);
            lastMousePosition = pointer;

            aimDirection = mouse != null ? BreakoutAim.Toward(HoldPosition(0.29f), mouseWorld, maxBounceAngle) : Vector2.up;
            if (CaughtPatty != null && !catchHeld)
            {
                CaughtPatty.Launch(aimDirection);
                CaughtPatty = null;
            }
            bool launch = (mouse != null && mouse.leftButton.wasPressedThisFrame)
                || (keyboard != null && keyboard.spaceKey.wasPressedThisFrame);
            if (launch && GreenBall != null && GreenBall.IsHeld)
                GreenBall.Launch(BreakoutAim.FromPaddle(0.25f, 1, maxBounceAngle));

            UpdateGuide();
        }

        private void FixedUpdate()
        {
            paddle.MovePosition(new Vector2(Mathf.MoveTowards(paddle.position.x, targetX,
                paddleSpeed * Time.fixedDeltaTime), -4.35f));
        }

        private void LateUpdate()
        {
            if (GreenBall != null && GreenBall.IsHeld) GreenBall.PlaceHeld(HoldPosition(GreenBall.Radius));
            if (CaughtPatty != null) CaughtPatty.PlaceHeld(HoldPosition(CaughtPatty.Radius));
        }

        private void UpdateGuide()
        {
            guide.enabled = false;
            if (!showAimGuide) return;
            Vector2 origin;
            Vector2 direction;
            Color color;
            if (CaughtPatty != null)
            {
                origin = HoldPosition(CaughtPatty.Radius);
                direction = aimDirection;
                color = CaughtPatty.State == FoodState.Raw ? RawRed : Cream;
            }
            else
            {
                BreakoutBall next = null;
                float soonest = float.PositiveInfinity;
                float top = PaddlePosition.y + 0.11f;
                foreach (BreakoutBall ball in balls)
                {
                    if (ball == null || ball.IsHeld || ball.Body.linearVelocity.y >= -0.1f) continue;
                    float time = (top + ball.Radius - ball.Body.position.y) / ball.Body.linearVelocity.y;
                    if (time < 0 || time > 1.5f || time >= soonest) continue;
                    float x = ball.Body.position.x + ball.Body.linearVelocity.x * time;
                    if (Mathf.Abs(x - PaddlePosition.x) > paddleWidth * 0.5f + ball.Radius) continue;
                    soonest = time;
                    next = ball;
                }
                if (next == null) return;
                origin = next.Body.position + next.Body.linearVelocity * soonest;
                direction = BreakoutAim.FromPaddle(origin.x - PaddlePosition.x, paddleWidth * 0.5f, maxBounceAngle);
                color = next.State == FoodState.Knockdown ? Green : next.State == FoodState.Raw ? RawRed : Cream;
            }
            // One wall-bank preview; ignore targets so the guide stays readable rather than noisy.
            float wall = direction.x > 0 ? 8.28f : -8.28f;
            float distance = Mathf.Abs(direction.x) > 0.001f ? (wall - origin.x) / direction.x : float.PositiveInfinity;
            float length = 5;
            Vector2 end = origin + direction * Mathf.Min(length, distance);
            Vector2 after = end + new Vector2(-direction.x, direction.y) * Mathf.Max(0, length - distance);
            guide.SetPosition(0, origin);
            guide.SetPosition(1, end);
            guide.SetPosition(2, after);
            guide.startColor = new Color(color.r, color.g, color.b, 0.65f);
            guide.endColor = new Color(color.r, color.g, color.b, 0.1f);
            guide.enabled = true;
        }

        public void Feedback(string text, Color color)
        {
            message = text;
            messageColor = color;
            messageUntil = Time.time + 4;
        }

        private void OnGUI()
        {
            if (titleStyle == null)
            {
                titleStyle = new GUIStyle(GUI.skin.label) { fontSize = 27, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleLeft };
                textStyle = new GUIStyle(GUI.skin.label) { fontSize = 16, alignment = TextAnchor.MiddleCenter };
                smallStyle = new GUIStyle(GUI.skin.label) { fontSize = 13, alignment = TextAnchor.MiddleCenter };
            }
            float scale = Mathf.Min(Screen.width / 1280f, Screen.height / 820f);
            Matrix4x4 previous = GUI.matrix;
            GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1));
            float width = Screen.width / scale;
            float height = Screen.height / scale;
            titleStyle.normal.textColor = Cream;
            GUI.Label(new Rect(32, 13, 420, 42), "SMASH / BURGER", titleStyle);
            smallStyle.alignment = TextAnchor.MiddleRight;
            smallStyle.normal.textColor = Cream;
            GUI.Label(new Rect(width - 450, 16, 418, 32), "BREAKOUT × BURGERTIME   /   AIMING PLAYGROUND", smallStyle);
            smallStyle.alignment = TextAnchor.MiddleCenter;
            textStyle.normal.textColor = Time.time < messageUntil ? messageColor : Cream;
            GUI.Label(new Rect(20, height - 92, width - 40, 30), Time.time < messageUntil ? message :
                "GREEN knocks down  /  RED goes to a side cooker  /  BLACK goes to a bun", textStyle);
            smallStyle.normal.textColor = new Color32(168, 188, 190, 255);
            GUI.Label(new Rect(20, height - 61, width - 40, 24),
                "Mouse or A/D / ← →: move     Click / Space: launch     Hold RMB / Shift: catch food, aim & release", smallStyle);
            GUI.Label(new Rect(20, height - 38, width - 40, 24),
                "G: aim guide " + (showAimGuide ? "ON" : "OFF") + "     R: reset     •     Paddle center = straight / edges = bank shots", smallStyle);
            GUI.matrix = previous;
        }

        private void OnDestroy()
        {
            foreach (Object asset in generatedAssets)
                if (asset != null) Destroy(asset);
        }
    }
}
