using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace SmashBurger
{
    /// <summary>A self-contained, timed serving game. All artwork is generated at runtime.</summary>
    public sealed class BurgerBreakout : MonoBehaviour
    {
        public const float DrainY = -5.65f;
        public static readonly Color Cream = new Color32(244, 233, 206, 255);
        public static readonly Color Green = new Color32(91, 230, 123, 255);
        public static readonly Color RawRed = new Color32(239, 64, 67, 255);
        public static readonly Color Gold = new Color32(246, 182, 76, 255);
        private static readonly Color Steel = new Color32(47, 65, 71, 255);

        [Header("Aiming playtest settings")]
        [SerializeField, Min(1)] private int maxActiveIngredients = 3;
        [SerializeField, Range(35, 75)] private float maxBounceAngle = 68;
        [SerializeField, Range(1, 4)] private float paddleWidth = 2.2f;
        [SerializeField, Min(1)] private float paddleSpeed = 16;
        [SerializeField, Min(1)] private float launcherSpeed = 8;
        [SerializeField, Min(1)] private float ingredientSpeed = 6;
        [SerializeField] private bool showAimGuide = true;
        [Header("Match timer")]
        [SerializeField, Min(1f)] private float gameDurationSeconds = 600f;
        [Header("Dining table orders")]
        [SerializeField, Min(0.1f)] private float tableOrderDuration = 60f;

        public int finalScore => Scoring != null ? Scoring.finalScore : 0;
        public int failedOrders { get; private set; }
        public int ordersDelivered => Scoring != null ? Scoring.DeliveredCount : 0;
        public int wrongDeliveries => Scoring != null ? Scoring.MistakeCount : 0;
        public float matchTimeRemaining => Mathf.Max(0, gameDurationSeconds - elapsed);
        public int MaxActiveIngredients => maxActiveIngredients;
        public int ActiveIngredientCount { get; private set; }
        public BoxCollider2D PaddleCollider { get; private set; }
        public Vector2 PaddlePosition => paddle.position;
        public PhysicsMaterial2D BounceMaterial { get; private set; }
        public Material SpriteMaterial { get; private set; }
        public BreakoutBall LauncherBall { get; private set; }
        public BreakoutBall CaughtIngredient { get; private set; }
        public ScoringSystem Scoring { get; private set; }

        public enum GamePhase { Intro, Playing, GameOver }

        public GamePhase Phase { get; private set; } = GamePhase.Intro;
        public bool isIntro => Phase == GamePhase.Intro;
        public bool isPlaying => Phase == GamePhase.Playing;
        public bool isGameOver => Phase == GamePhase.GameOver;

        private readonly List<BreakoutBall> balls = new List<BreakoutBall>();
        private readonly List<IngredientRack> racks = new List<IngredientRack>();
        private readonly List<DiningTable> tables = new List<DiningTable>();
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
        private string message = "Launch the pickle to knock an ingredient off the rack.";
        private Color messageColor;
        private float messageUntil;
        private float elapsed;
        private float pausedTimeScale = 1f;
        private bool timePaused;
        private GUIStyle titleStyle, textStyle, smallStyle, scoreStyle;
        private GUIStyle screenTitleStyle, screenSubtitleStyle, cardTitleStyle, bodyStyle, stepStyle;
        private GUIStyle controlKeyStyle, controlValueStyle, promptStyle, statLabelStyle, statValueStyle;

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
            Scoring = gameObject.AddComponent<ScoringSystem>();
            BuildCourt();
            ShowIntro();
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

        public TextMesh Label(string text, Transform parent, Vector2 position, float size, Color color)
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
            return label;
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

            for (int row = 0; row < 2; row++)
            {
                float y = 2.65f + row * 1.05f;
                Shape("Ingredient shelf", world, new Vector2(0, y - 0.42f), new Vector2(12.4f, 0.08f), Steel);
                for (int col = 0; col < 7; col++)
                {
                    int slot = row * 7 + col;
                    IngredientKind ingredient = IngredientInfo.ForRackSlot(slot);
                    GameObject rackObject = new GameObject("Ingredient rack " + row + "/" + col);
                    rackObject.transform.SetParent(world, false);
                    rackObject.transform.localPosition = new Vector2(-5.4f + col * 1.8f, y);
                    IngredientRack rack = rackObject.AddComponent<IngredientRack>();
                    rack.Initialize(this, ingredient);
                    racks.Add(rack);
                }
            }

            // With no side cookers, the tables can be spaced evenly up the side lanes.
            float[] tableHeights = { 4.25f, 2.05f, -0.15f, -2.35f };
            foreach (int side in new[] { -1, 1 })
            {
                for (int index = 0; index < tableHeights.Length; index++)
                {
                    GameObject tableObject = new GameObject((side < 0 ? "Left" : "Right") + " dining table " + (index + 1));
                    tableObject.transform.SetParent(world, false);
                    tableObject.transform.localPosition = new Vector2(side * 7.75f, tableHeights[index]);
                    DiningTable table = tableObject.AddComponent<DiningTable>();
                    table.Initialize(this, tableOrderDuration);
                    tables.Add(table);
                }
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

        public float SpeedFor(FoodState state) => state == FoodState.Knockdown ? launcherSpeed : ingredientSpeed;

        public BreakoutBall SpawnBall(FoodState state, Vector2 position, IngredientRack source = null,
            IngredientKind ingredient = IngredientKind.Meat)
        {
            GameObject item = new GameObject(state + " ball");
            item.transform.SetParent(world, false);
            item.transform.position = position;
            BreakoutBall ball = item.AddComponent<BreakoutBall>();
            ball.Initialize(this, state, source, ingredient);
            // Independent balls should not randomly spoil each other's carefully aimed shots.
            foreach (BreakoutBall other in balls)
                if (other != null) Physics2D.IgnoreCollision(ball.GetComponent<Collider2D>(), other.GetComponent<Collider2D>());
            balls.Add(ball);
            if (state == FoodState.Knockdown) LauncherBall = ball;
            else ActiveIngredientCount++;
            return ball;
        }

        /// <summary>Resets to a fresh round and shows the title screen with the goal and controls.
        /// The match clock and table orders stay paused until <see cref="StartGame"/>.</summary>
        public void ShowIntro()
        {
            ResumeTime();
            PrepareRound(false);
            Phase = GamePhase.Intro;
            if (guide != null) guide.enabled = false;
        }

        /// <summary>Starts a fresh match from the title screen.</summary>
        public void StartGame()
        {
            if (Phase == GamePhase.Playing) return;
            Restart();
        }

        /// <summary>Resets every score, order and ingredient, then resumes play.</summary>
        public void Restart()
        {
            ResumeTime();
            // Ignore the pointer jump that would otherwise teleport the paddle on the first frame.
            Mouse mouse = Mouse.current;
            if (mouse != null) lastMousePosition = mouse.position.ReadValue();
            PrepareRound(true);
            Phase = GamePhase.Playing;
            Feedback("Launch the pickle. Paddle edges give steeper bank shots.", Cream);
        }

        private void PrepareRound(bool openTables)
        {
            elapsed = 0;
            if (Scoring != null) Scoring.ResetScore();
            foreach (BreakoutBall ball in balls.ToArray())
                if (ball != null) ball.Retire();
            balls.Clear();
            ActiveIngredientCount = 0;
            CaughtIngredient = null;
            catchHeld = false;
            aimDirection = Vector2.up;
            failedOrders = 0;
            messageUntil = 0;
            foreach (IngredientRack rack in racks) rack.Restock();
            foreach (DiningTable table in tables)
            {
                if (openTables) table.ResetOrder();
                else table.ClearOrder();
            }
            targetX = 0;
            paddle.position = new Vector2(0, -4.35f);
            LauncherBall = SpawnBall(FoodState.Knockdown, HoldPosition(0.19f));
            LauncherBall.Hold();
        }

        private void SuspendTime()
        {
            if (timePaused) return;
            pausedTimeScale = Time.timeScale;
            Time.timeScale = 0;
            timePaused = true;
        }

        private void ResumeTime()
        {
            if (!timePaused) return;
            Time.timeScale = pausedTimeScale;
            timePaused = false;
        }

        private void EndGame()
        {
            if (Phase == GamePhase.GameOver) return;
            elapsed = gameDurationSeconds;
            Phase = GamePhase.GameOver;
            if (guide != null) guide.enabled = false;
            SuspendTime();
        }

        public void Forget(BreakoutBall ball)
        {
            balls.Remove(ball);
            if (ball.State != FoodState.Knockdown) ActiveIngredientCount = Mathf.Max(0, ActiveIngredientCount - 1);
            if (CaughtIngredient == ball) CaughtIngredient = null;
            if (LauncherBall == ball) LauncherBall = null;
        }

        public void Lose(BreakoutBall ball)
        {
            if (ball.State == FoodState.Knockdown)
            {
                ball.Hold();
                ball.PlaceHeld(HoldPosition(ball.Radius));
                Feedback("Pickle ready.", Green);
            }
            else
            {
                ball.Retire();
                Feedback("Ingredient returned to its rack. Try another bank shot.", Cream);
            }
        }

        private Vector2 HoldPosition(float radius) => PaddlePosition + Vector2.up * (0.14f + radius);

        public void HitPaddle(BreakoutBall ball)
        {
            if (ball.State == FoodState.Raw) ball.Cook();
            if (ball.State == FoodState.Cooked) ball.MarkPaddleBounce();
            if (ball.State != FoodState.Knockdown && catchHeld && CaughtIngredient == null)
            {
                CaughtIngredient = ball;
                ball.Hold();
                ball.PlaceHeld(HoldPosition(ball.Radius));
                Feedback("CAUGHT — line up your shot", Cream);
                return;
            }
            ball.Launch(BreakoutAim.FromPaddle(ball.Body.position.x - paddle.position.x, paddleWidth * 0.5f, maxBounceAngle));
        }

        public void IgnoreTableCollisions(Collider2D launcherCollider)
        {
            foreach (DiningTable table in tables)
                Physics2D.IgnoreCollision(launcherCollider, table.GetComponent<BoxCollider2D>());
        }

        public void FailOrder(IngredientKind ingredient)
        {
            failedOrders++;
            Feedback(IngredientInfo.Name(ingredient).ToUpperInvariant() + " order expired — new order!", RawRed);
        }

        private void Update()
        {
            FitCamera();
            Keyboard keyboard = Keyboard.current;
            Mouse mouse = Mouse.current;

            if (Phase == GamePhase.Intro)
            {
                if (ConfirmPressed(keyboard, mouse)
                    || (keyboard != null && keyboard.rKey.wasPressedThisFrame))
                    StartGame();
                return;
            }
            if (Phase == GamePhase.GameOver)
            {
                if (ConfirmPressed(keyboard, mouse)
                    || (keyboard != null && keyboard.rKey.wasPressedThisFrame))
                    Restart();
                return;
            }

            elapsed += Time.deltaTime;
            if (elapsed >= gameDurationSeconds)
            {
                EndGame();
                return;
            }
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
            else if (mouseMoved && CaughtIngredient == null) targetX = mouseWorld.x;
            targetX = Mathf.Clamp(targetX, -8.4f + paddleWidth * 0.5f, 8.4f - paddleWidth * 0.5f);
            lastMousePosition = pointer;

            aimDirection = mouse != null ? BreakoutAim.Toward(HoldPosition(0.29f), mouseWorld, maxBounceAngle) : Vector2.up;
            if (CaughtIngredient != null && !catchHeld)
            {
                CaughtIngredient.Launch(aimDirection);
                CaughtIngredient = null;
            }
            bool launch = (mouse != null && mouse.leftButton.wasPressedThisFrame)
                || (keyboard != null && keyboard.spaceKey.wasPressedThisFrame);
            if (launch && LauncherBall != null && LauncherBall.IsHeld)
                LauncherBall.Launch(BreakoutAim.FromPaddle(0.25f, 1, maxBounceAngle));

            UpdateGuide();
        }

        private static bool ConfirmPressed(Keyboard keyboard, Mouse mouse)
        {
            if (mouse != null && mouse.leftButton.wasPressedThisFrame) return true;
            if (keyboard == null) return false;
            return keyboard.spaceKey.wasPressedThisFrame
                || keyboard.enterKey.wasPressedThisFrame
                || keyboard.numpadEnterKey.wasPressedThisFrame;
        }

        private void FixedUpdate()
        {
            if (!isPlaying) return;
            paddle.MovePosition(new Vector2(Mathf.MoveTowards(paddle.position.x, targetX,
                paddleSpeed * Time.fixedDeltaTime), -4.35f));
        }

        private void LateUpdate()
        {
            if (!isPlaying) return;
            if (LauncherBall != null && LauncherBall.IsHeld) LauncherBall.PlaceHeld(HoldPosition(LauncherBall.Radius));
            if (CaughtIngredient != null) CaughtIngredient.PlaceHeld(HoldPosition(CaughtIngredient.Radius));
        }

        private static Color GuideColor(BreakoutBall ball)
        {
            return ball.State == FoodState.Cooked
                ? Cream
                : IngredientInfo.ColorFor(ball.Ingredient, ball.State);
        }

        private void UpdateGuide()
        {
            guide.enabled = false;
            if (!showAimGuide || !isPlaying) return;
            Vector2 origin;
            Vector2 direction;
            Color color;
            if (CaughtIngredient != null)
            {
                origin = HoldPosition(CaughtIngredient.Radius);
                direction = aimDirection;
                color = GuideColor(CaughtIngredient);
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
                color = GuideColor(next);
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

        public static string FormatCountdown(float secondsRemaining)
        {
            int seconds = Mathf.CeilToInt(Mathf.Max(0, secondsRemaining));
            return $"{seconds / 60}:{seconds % 60:00}";
        }

        private void EnsureStyles()
        {
            if (titleStyle != null) return;
            titleStyle = new GUIStyle(GUI.skin.label) { fontSize = 27, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleLeft };
            textStyle = new GUIStyle(GUI.skin.label) { fontSize = 17, alignment = TextAnchor.MiddleCenter };
            smallStyle = new GUIStyle(GUI.skin.label) { fontSize = 13, alignment = TextAnchor.MiddleCenter };
            scoreStyle = new GUIStyle(GUI.skin.label) { fontSize = 24, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleRight };
            screenTitleStyle = new GUIStyle(GUI.skin.label) { fontSize = 56, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            screenSubtitleStyle = new GUIStyle(GUI.skin.label) { fontSize = 18, alignment = TextAnchor.MiddleCenter };
            cardTitleStyle = new GUIStyle(GUI.skin.label) { fontSize = 15, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleLeft };
            bodyStyle = new GUIStyle(GUI.skin.label) { fontSize = 19, alignment = TextAnchor.UpperLeft, wordWrap = true };
            stepStyle = new GUIStyle(GUI.skin.label) { fontSize = 16, alignment = TextAnchor.UpperLeft, wordWrap = true };
            controlKeyStyle = new GUIStyle(GUI.skin.label) { fontSize = 17, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleLeft };
            controlValueStyle = new GUIStyle(GUI.skin.label) { fontSize = 17, alignment = TextAnchor.MiddleLeft };
            promptStyle = new GUIStyle(GUI.skin.label) { fontSize = 22, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            statLabelStyle = new GUIStyle(GUI.skin.label) { fontSize = 13, alignment = TextAnchor.MiddleCenter };
            statValueStyle = new GUIStyle(GUI.skin.label) { fontSize = 30, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
        }

        private void OnGUI()
        {
            EnsureStyles();
            float scale = Mathf.Min(Screen.width / 1280f, Screen.height / 820f);
            if (scale <= 0.0001f) return;
            Matrix4x4 previous = GUI.matrix;
            GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1));
            float width = Screen.width / scale;
            float height = Screen.height / scale;

            if (Phase == GamePhase.Intro) DrawIntroScreen(width, height);
            else if (Phase == GamePhase.GameOver) DrawGameOverScreen(width, height);
            else DrawHud(width, height);

            GUI.matrix = previous;
        }

        // In-match HUD: score and countdown only; the controls live on the intro screen.
        private void DrawHud(float width, float height)
        {
            titleStyle.normal.textColor = Cream;
            GUI.Label(new Rect(32, 13, 420, 42), "SMASH / BURGER", titleStyle);
            scoreStyle.normal.textColor = Gold;
            GUI.Label(new Rect(width - 450, 16, 418, 34), "SCORE  " + finalScore, scoreStyle);

            bool flashRed = matchTimeRemaining < 15 && (int)(Time.unscaledTime * 4) % 2 == 0;
            titleStyle.normal.textColor = flashRed ? RawRed : Cream;
            titleStyle.fontSize = 48;
            titleStyle.alignment = TextAnchor.MiddleCenter;
            GUI.Label(new Rect(0, 5, width, 60), FormatCountdown(matchTimeRemaining), titleStyle);
            titleStyle.fontSize = 27;
            titleStyle.alignment = TextAnchor.MiddleLeft;

            textStyle.normal.textColor = Time.time < messageUntil ? messageColor : Cream;
            GUI.Label(new Rect(20, height - 58, width - 40, 30), message, textStyle);
        }

        private void DrawIntroScreen(float width, float height)
        {
            DrawBackdrop(width, height);
            float top = Mathf.Max(0f, (height - 820f) * 0.5f);
            GUI.BeginGroup(new Rect(0, top, width, 820));

            screenTitleStyle.normal.textColor = Gold;
            GUI.Label(new Rect(0, 54, width, 70), "SMASH / BURGER", screenTitleStyle);
            screenSubtitleStyle.normal.textColor = Cream;
            GUI.Label(new Rect(0, 124, width, 28), "A break-the-kitchen serving game", screenSubtitleStyle);

            float panelWidth = Mathf.Min(1000, width - 80);
            float panelX = width * 0.5f - panelWidth * 0.5f;

            DrawPanel(new Rect(panelX, 172, panelWidth, 232));
            cardTitleStyle.normal.textColor = Gold;
            GUI.Label(new Rect(panelX + 32, 186, 400, 24), "YOUR JOB", cardTitleStyle);
            bodyStyle.normal.textColor = Cream;
            GUI.Label(new Rect(panelX + 32, 214, panelWidth - 64, 58),
                "Deliver each food order to the proper table before its order timer runs out. "
                + "Fast, correct deliveries score more; expired orders are counted as misses.", bodyStyle);

            float columnWidth = (panelWidth - 80) * 0.5f;
            stepStyle.normal.textColor = new Color32(203, 219, 220, 255);
            GUI.Label(new Rect(panelX + 32, 280, columnWidth, 46),
                "1   Launch the pickle to knock an ingredient off its rack.", stepStyle);
            GUI.Label(new Rect(panelX + 48 + columnWidth, 280, columnWidth, 46),
                "2   Bounce the fallen food off your paddle to cook it.", stepStyle);
            GUI.Label(new Rect(panelX + 32, 330, columnWidth, 46),
                "3   Bank the cooked food into the table showing that item.", stepStyle);
            GUI.Label(new Rect(panelX + 48 + columnWidth, 330, columnWidth, 46),
                "4   Wrong tables bounce it back; missed orders expire.", stepStyle);

            DrawPanel(new Rect(panelX, 420, panelWidth, 244));
            cardTitleStyle.normal.textColor = Gold;
            GUI.Label(new Rect(panelX + 32, 434, 400, 24), "CONTROLS", cardTitleStyle);

            float rowY = 470;
            ControlRow(panelX + 32, rowY, "Move paddle", "Mouse  •  A / D  •  Left / Right arrows", panelWidth - 64);
            ControlRow(panelX + 32, rowY + 30, "Launch pickle", "Left click  •  Space", panelWidth - 64);
            ControlRow(panelX + 32, rowY + 60, "Catch food", "Hold right click or Shift, then release to throw", panelWidth - 64);
            ControlRow(panelX + 32, rowY + 90, "Aim guide", "G toggles the first-bounce preview", panelWidth - 64);
            ControlRow(panelX + 32, rowY + 120, "Restart match", "R", panelWidth - 64);

            smallStyle.alignment = TextAnchor.MiddleLeft;
            smallStyle.normal.textColor = new Color32(168, 188, 190, 255);
            GUI.Label(new Rect(panelX + 32, 624, panelWidth - 64, 24),
                "Cooked food must bounce off the paddle before it can be served.", smallStyle);
            smallStyle.alignment = TextAnchor.MiddleCenter;

            promptStyle.normal.textColor = Green;
            GUI.Label(new Rect(0, 704, width, 36), "PRESS SPACE, ENTER, OR CLICK TO START", promptStyle);
            smallStyle.normal.textColor = new Color32(168, 188, 190, 255);
            GUI.Label(new Rect(0, 750, width, 24),
                Mathf.RoundToInt(gameDurationSeconds / 60f) + "-minute match  •  " + tables.Count
                + " tables  •  orders expire after " + Mathf.RoundToInt(tableOrderDuration) + " seconds", smallStyle);
            GUI.EndGroup();
        }

        private void DrawGameOverScreen(float width, float height)
        {
            DrawBackdrop(width, height);
            float top = Mathf.Max(0f, (height - 820f) * 0.5f);
            GUI.BeginGroup(new Rect(0, top, width, 820));

            screenTitleStyle.normal.textColor = RawRed;
            GUI.Label(new Rect(0, 96, width, 74), "GAME OVER", screenTitleStyle);
            screenSubtitleStyle.normal.textColor = new Color32(203, 219, 220, 255);
            GUI.Label(new Rect(0, 170, width, 28), "Time's up — the dinner rush is over.", screenSubtitleStyle);

            float panelWidth = Mathf.Min(620, width - 80);
            float panelX = width * 0.5f - panelWidth * 0.5f;
            DrawPanel(new Rect(panelX, 222, panelWidth, 178));
            statLabelStyle.normal.textColor = new Color32(168, 188, 190, 255);
            GUI.Label(new Rect(panelX, 240, panelWidth, 24), "FINAL SCORE", statLabelStyle);
            statValueStyle.normal.textColor = Gold;
            GUI.Label(new Rect(panelX, 268, panelWidth, 74), finalScore.ToString("N0"), statValueStyle);
            smallStyle.normal.textColor = Cream;
            GUI.Label(new Rect(panelX, 350, panelWidth, 24),
                ordersDelivered + " correct  •  " + failedOrders + " expired  •  " + wrongDeliveries + " wrong table", smallStyle);

            const float statWidth = 190;
            const float statGap = 28;
            const float statY = 444;
            float startX = width * 0.5f - (statWidth * 3 + statGap * 2) * 0.5f;
            DrawStat(startX, statY, "ORDERS DELIVERED", ordersDelivered);
            DrawStat(startX + statWidth + statGap, statY, "ORDERS EXPIRED", failedOrders);
            DrawStat(startX + (statWidth + statGap) * 2, statY, "WRONG TABLE", wrongDeliveries);

            promptStyle.normal.textColor = Green;
            GUI.Label(new Rect(0, 566, width, 36), "PRESS R, SPACE, ENTER, OR CLICK TO PLAY AGAIN", promptStyle);
            smallStyle.normal.textColor = new Color32(168, 188, 190, 255);
            GUI.Label(new Rect(0, 614, width, 24), "Fast, correct deliveries earn the biggest tips.", smallStyle);
            GUI.EndGroup();
        }

        private void DrawStat(float x, float y, string label, int value)
        {
            statLabelStyle.normal.textColor = new Color32(168, 188, 190, 255);
            GUI.Label(new Rect(x, y, 190, 22), label, statLabelStyle);
            statValueStyle.normal.textColor = Cream;
            GUI.Label(new Rect(x, y + 22, 190, 42), value.ToString("N0"), statValueStyle);
        }

        private void ControlRow(float x, float y, string key, string value, float width)
        {
            controlKeyStyle.normal.textColor = Cream;
            GUI.Label(new Rect(x, y, 210, 26), key, controlKeyStyle);
            controlValueStyle.normal.textColor = new Color32(246, 222, 178, 255);
            GUI.Label(new Rect(x + 210, y, width - 210, 26), value, controlValueStyle);
        }

        private static void DrawBackdrop(float width, float height)
        {
            Fill(new Rect(0, 0, width, height), new Color(0.035f, 0.055f, 0.065f, 0.97f));
            Fill(new Rect(0, 0, width, 4), Gold);
            Fill(new Rect(0, height - 4, width, 4), Gold);
        }

        private static void DrawPanel(Rect rect)
        {
            Fill(rect, new Color(0.10f, 0.145f, 0.165f, 0.98f));
            Fill(new Rect(rect.x, rect.y, rect.width, 2), new Color(0.96f, 0.71f, 0.30f, 0.55f));
        }

        private static void Fill(Rect rect, Color color)
        {
            Color previous = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = previous;
        }

        private void OnDestroy()
        {
            if (timePaused) Time.timeScale = pausedTimeScale;
            foreach (Object asset in generatedAssets)
                if (asset != null) Destroy(asset);
        }
    }
}
