using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace SmashBurger
{
    /// <summary>
    /// Adds game feel to SmashBurger without editing any existing script or scene.
    /// It attaches itself whenever a scene with <see cref="BurgerBreakout"/> loads, watches public game
    /// state (balls, racks, the paddle), and turns what it sees into effects:
    ///   bounce, paddle hit, catch, launch, knock-down, cook, serve, reject, miss, restock, countdown.
    /// J toggles all juice, M mutes. Nothing here changes rules, physics or colliders.
    /// </summary>
    public sealed class JuiceWatcher : MonoBehaviour
    {
        // ---------------- Self-installation (no scene or script edits needed) ----------------

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Register()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AttachToFirstScene() => TryAttach();

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode) => TryAttach();

        private static void TryAttach()
        {
            BurgerBreakout game = FindFirstObjectByType<BurgerBreakout>();
            if (game == null) return;
            // Match on the game instance: during a scene reload the old scene's watcher can still exist briefly.
            foreach (JuiceWatcher existing in FindObjectsByType<JuiceWatcher>(FindObjectsSortMode.None))
                if (existing.game == game) return;
            GameObject host = new GameObject("Juice (added at runtime)");
            SceneManager.MoveGameObjectToScene(host, game.gameObject.scene);
            host.AddComponent<JuiceWatcher>().Attach(game);
        }

        // ---------------- State ----------------

        public Juice Effects { get; private set; }

        private static readonly Color HotOrange = new Color32(255, 140, 50, 255);
        private static readonly FieldInfo ElapsedField =
            typeof(BurgerBreakout).GetField("elapsed", BindingFlags.NonPublic | BindingFlags.Instance);

        private BurgerBreakout game;
        private BreakoutBall knownLauncher;
        private float fallbackElapsed;
        private int lastTickSecond;
        private bool timeUpPlayed;
        private GUIStyle hintStyle;

        private sealed class BallTrack
        {
            public BreakoutBall Ball;
            public Transform Stretch, Spin;
            public FoodState State;
            public Vector2 Position, Velocity;
            public bool Held, Sampled;
            public float SquashAmount, SquashAge = 1, Facing, Roll, SteamTimer;
            public void Squash(float amount) { SquashAmount = amount; SquashAge = 0; }
        }
        private readonly List<BallTrack> balls = new List<BallTrack>();

        private sealed class RackTrack { public IngredientRack Rack; public bool Stocked; }
        private readonly List<RackTrack> racks = new List<RackTrack>();

        private sealed class Cooker { public CookingChamber Chamber; public SpriteRenderer Zone; public Color Cool; public float Glow; }
        private readonly List<Cooker> cookers = new List<Cooker>();

        private readonly List<ServingTarget> buns = new List<ServingTarget>();

        public void Attach(BurgerBreakout owner)
        {
            game = owner;
            Camera view = Camera.main;
            Effects = gameObject.AddComponent<Juice>();
            Effects.Initialize(view, game.SpriteMaterial);
            knownLauncher = game.LauncherBall;

            foreach (IngredientRack rack in FindObjectsByType<IngredientRack>(FindObjectsSortMode.None))
                racks.Add(new RackTrack { Rack = rack, Stocked = rack.IsStocked });
            foreach (ServingTarget bun in FindObjectsByType<ServingTarget>(FindObjectsSortMode.None))
                buns.Add(bun);
            foreach (CookingChamber chamber in FindObjectsByType<CookingChamber>(FindObjectsSortMode.None))
            {
                Transform zone = chamber.transform.Find("Cooking zone");
                SpriteRenderer image = zone != null ? zone.GetComponent<SpriteRenderer>() : null;
                if (image != null) cookers.Add(new Cooker { Chamber = chamber, Zone = image, Cool = image.color });
            }
        }

        // ---------------- Physics-rate sampling: bounces, cooking, catching, launching ----------------

        private void FixedUpdate()
        {
            if (game == null || CheckRestart()) return;
            DiscoverBalls();
            foreach (BallTrack track in balls)
            {
                BreakoutBall ball = track.Ball;
                if (!Alive(ball)) continue;
                bool held = ball.IsHeld;
                Vector2 velocity = held ? Vector2.zero : ball.Body.linearVelocity;
                Vector2 position = ball.Body.position;

                if (track.Sampled)
                {
                    if (track.State == FoodState.Raw && ball.State == FoodState.Cooked) Cooked(track, position);
                    else if (!held && !track.Held) DetectBounce(track, position, velocity);

                    if (held && !track.Held)
                    {
                        if (ball.State == FoodState.Knockdown) { if (track.Position.y < -4.6f) Missed(track.Position, true); }
                        else Caught(track);
                    }
                    else if (!held && track.Held) Launched(position);
                }

                track.State = ball.State;
                track.Position = position;
                track.Velocity = velocity;
                track.Held = held;
                track.Sampled = true;
            }
        }

        private void DetectBounce(BallTrack track, Vector2 position, Vector2 velocity)
        {
            if (track.Velocity.sqrMagnitude < 0.01f || velocity.sqrMagnitude < 0.01f) return;
            Vector2 before = track.Velocity.normalized;
            Vector2 after = velocity.normalized;
            if (Vector2.Dot(before, after) > 0.95f) return;

            Vector2 paddle = game.PaddlePosition;
            float halfWidth = game.PaddleCollider.size.x * 0.5f;
            if (before.y <= 0 && after.y > 0 && position.y < paddle.y + 1f && Mathf.Abs(position.x - paddle.x) < halfWidth + 0.6f)
            {
                PaddleHit(track, position);
                return;
            }
            if (track.Ball.State == FoodState.Raw && before.y > 0 && after.y < 0 && position.y > 3.8f && position.y < 4.7f)
            {
                ServingTarget bun = NearestBun(position, 1.15f);
                if (bun != null) { Rejected(track, position, bun); return; }
            }
            Vector2 normal = (after - before).normalized;
            Bounced(track, position - normal * track.Ball.Radius, normal);
        }

        // ---------------- Frame-rate checks: racks, served/missed food, timer, input ----------------

        private void Update()
        {
            if (game == null) return;
            Keyboard keyboard = Keyboard.current;
            if (keyboard != null)
            {
                if (keyboard.jKey.wasPressedThisFrame) Effects.Toggle();
                if (keyboard.mKey.wasPressedThisFrame) Effects.ToggleMute();
            }
            if (CheckRestart()) return;

            foreach (RackTrack track in racks)
            {
                if (track.Rack == null) continue;
                bool stocked = track.Rack.IsStocked;
                if (track.Stocked && !stocked) KnockedDown(track.Rack);
                else if (!track.Stocked && stocked && track.Rack.transform.childCount > 0)
                    Effects.Punch(track.Rack.transform.GetChild(0), 1, 0.55f, Juice.PunchKind.PopIn);
                track.Stocked = stocked;
            }

            for (int i = balls.Count - 1; i >= 0; i--)
            {
                BallTrack track = balls[i];
                if (Alive(track.Ball)) continue;
                if (track.State == FoodState.Cooked && track.Position.y > 3.5f)
                {
                    ServingTarget bun = NearestBun(track.Position, 3f);
                    if (bun != null) Served(track.Position, bun);
                }
                else if (track.State != FoodState.Knockdown && track.Position.y < -4.6f) Missed(track.Position, false);
                balls.RemoveAt(i);
            }

            UpdateCountdown();
        }

        private void UpdateCountdown()
        {
            fallbackElapsed += Time.deltaTime;
            float elapsed = ElapsedField != null ? (float)ElapsedField.GetValue(game) : fallbackElapsed;
            int secondsLeft = Mathf.CeilToInt(Mathf.Max(0, 60 - elapsed));
            if (secondsLeft <= 10 && secondsLeft > 0 && secondsLeft != lastTickSecond)
            {
                lastTickSecond = secondsLeft;
                Effects.Play(Juice.Sound.Tick, 0.5f, 0, secondsLeft <= 3 ? 1.35f : 1f);
                Effects.Shake(secondsLeft <= 3 ? 0.08f : 0.03f);
            }
            if (secondsLeft == 0 && !timeUpPlayed)
            {
                timeUpPlayed = true;
                Effects.Text("TIME!", new Vector2(0, 0.6f), BurgerBreakout.RawRed, 0.24f, 1.6f);
                Effects.Play(Juice.Sound.TimeUp, 0.6f, 0);
                Effects.Shake(0.4f);
            }
        }

        /// <summary>The game's Restart() swaps in a new launcher pickle; reset our bookkeeping silently.</summary>
        private bool CheckRestart()
        {
            if (game.LauncherBall == knownLauncher) return false;
            bool restarted = knownLauncher != null && game.LauncherBall != null;
            knownLauncher = game.LauncherBall;
            if (!restarted) return false;
            balls.Clear();
            foreach (RackTrack track in racks) if (track.Rack != null) track.Stocked = track.Rack.IsStocked;
            fallbackElapsed = 0;
            lastTickSecond = 0;
            timeUpPlayed = false;
            return true;
        }

        // ---------------- Per-frame visuals: squash/stretch, steam, cooker heat ----------------

        private void LateUpdate()
        {
            if (game == null) return;
            float dt = Time.deltaTime;
            foreach (BallTrack track in balls)
            {
                if (!Alive(track.Ball)) continue;
                Adopt(track);
                AnimateBall(track, dt);
            }
            foreach (Cooker cooker in cookers)
            {
                if (cooker.Zone == null) continue;
                cooker.Glow = Effects.Enabled ? Mathf.Max(0, cooker.Glow - dt * 2.2f) : 0;
                float flicker = Effects.Enabled ? Mathf.PerlinNoise(Time.time * 3, cooker.Chamber.transform.position.x) * 0.12f : 0;
                cooker.Zone.color = Color.Lerp(cooker.Cool, HotOrange, Mathf.Clamp01(cooker.Glow + flicker));
            }
        }

        private void AnimateBall(BallTrack track, float dt)
        {
            BreakoutBall ball = track.Ball;
            if (!Effects.Enabled)
            {
                track.Stretch.localRotation = Quaternion.identity;
                track.Stretch.localScale = Vector3.one;
                track.Spin.localRotation = Quaternion.identity;
                return;
            }

            Vector2 velocity = ball.IsHeld ? Vector2.zero : ball.Body.linearVelocity;
            float speed = velocity.magnitude;
            if (speed > 0.1f) track.Facing = Mathf.Atan2(velocity.y, velocity.x) * Mathf.Rad2Deg;

            // Stretch along the direction of travel, minus a damped spring after each impact.
            float stretch = Mathf.Clamp01(speed / game.SpeedFor(ball.State)) * 0.14f;
            float squash = 0;
            if (track.SquashAge < 0.45f)
            {
                track.SquashAge += dt;
                squash = track.SquashAmount * Mathf.Exp(-track.SquashAge * 11) * Mathf.Cos(track.SquashAge * 38);
            }
            float s = Mathf.Clamp(stretch - squash, -0.5f, 0.4f);
            track.Stretch.localRotation = Quaternion.Euler(0, 0, track.Facing);
            track.Stretch.localScale = new Vector3(1 + s, 1 - s * 0.85f, 1);

            // Counter-rotate so the art rolls with sideways motion instead of snapping on bounces.
            track.Roll -= velocity.x * 75 * dt;
            track.Spin.localRotation = Quaternion.Euler(0, 0, track.Roll - track.Facing);

            if (ball.State == FoodState.Cooked && !ball.IsHeld)
            {
                track.SteamTimer -= dt;
                if (track.SteamTimer <= 0)
                {
                    track.SteamTimer = 0.08f;
                    Effects.Steam(ball.Body.position);
                }
            }
        }

        // ---------------- Ball bookkeeping ----------------

        private void DiscoverBalls()
        {
            foreach (BreakoutBall ball in FindObjectsByType<BreakoutBall>(FindObjectsSortMode.None))
            {
                if (!Alive(ball) || Tracking(ball)) continue;
                // Two pivots between the ball and its art: one stretches, one rolls.
                // The ball's own transform and collider are never scaled.
                Transform stretch = new GameObject("Juice stretch").transform;
                stretch.SetParent(ball.transform, false);
                Transform spin = new GameObject("Juice spin").transform;
                spin.SetParent(stretch, false);
                BallTrack track = new BallTrack { Ball = ball, Stretch = stretch, Spin = spin };
                Adopt(track);
                balls.Add(track);
            }
        }

        /// <summary>Moves the ball's art under our pivots, including art the ball rebuilds when it cooks.</summary>
        private static void Adopt(BallTrack track)
        {
            Transform root = track.Ball.transform;
            for (int i = root.childCount - 1; i >= 0; i--)
            {
                Transform child = root.GetChild(i);
                if (child != track.Stretch) child.SetParent(track.Spin, false);
            }
        }

        private bool Tracking(BreakoutBall ball)
        {
            foreach (BallTrack track in balls) if (track.Ball == ball) return true;
            return false;
        }

        private static bool Alive(BreakoutBall ball) => ball != null && ball.gameObject.activeInHierarchy;

        private ServingTarget NearestBun(Vector2 position, float maxDistanceX)
        {
            ServingTarget best = null;
            float bestDistance = maxDistanceX;
            foreach (ServingTarget bun in buns)
            {
                if (bun == null) continue;
                float distance = Mathf.Abs(bun.transform.position.x - position.x);
                if (distance < bestDistance) { bestDistance = distance; best = bun; }
            }
            return best;
        }

        // ---------------- The effects for each moment ----------------

        private void Launched(Vector2 position)
        {
            Effects.Play(Juice.Sound.Launch, 0.55f);
            DipPaddle(0.25f);
            Effects.Burst(position, BurgerBreakout.Cream, 6, 1.5f, 3.5f, 0.05f, 0.09f, 0.25f, 0, 4, Vector2.down);
        }

        private void PaddleHit(BallTrack track, Vector2 position)
        {
            track.Squash(0.4f);
            DipPaddle(0.3f);
            Effects.Burst(position + Vector2.down * track.Ball.Radius, BurgerBreakout.Cream, 8, 2, 5, 0.04f, 0.08f, 0.22f, 6, 3, Vector2.up);
            Effects.Play(Juice.Sound.Paddle, 0.7f, 0.1f);
            Effects.Shake(0.1f);
        }

        private void Caught(BallTrack track)
        {
            track.Squash(0.25f);
            DipPaddle(0.5f);
            Effects.Play(Juice.Sound.Catch, 0.6f, 0.05f);
            Effects.Shake(0.05f);
        }

        private void Bounced(BallTrack track, Vector2 point, Vector2 normal)
        {
            track.Squash(0.3f);
            Color color = IngredientInfo.ColorFor(track.Ball.Ingredient, track.Ball.State);
            Effects.Burst(point, Color.Lerp(color, Color.white, 0.4f), 4, 1.5f, 3.5f, 0.03f, 0.07f, 0.2f, 0, 4, normal);
            Effects.Play(Juice.Sound.Bounce, 0.3f, 0.15f, track.Ball.State == FoodState.Knockdown ? 1.25f : 1f);
        }

        private void KnockedDown(IngredientRack rack)
        {
            Vector2 position = rack.transform.position;
            Color body = IngredientInfo.ColorFor(rack.Ingredient, false);
            Color accent = IngredientInfo.AccentFor(rack.Ingredient, false);
            Effects.Burst(position, body, 14, 2, 6, 0.06f, 0.14f, 0.5f, 9, 2, Vector2.zero);
            Effects.Burst(position, accent, 8, 1, 4, 0.04f, 0.1f, 0.45f, 9, 2, Vector2.zero);
            Effects.Burst(position, BurgerBreakout.Cream, 6, 4, 7, 0.02f, 0.05f, 0.18f, 0, 6, Vector2.zero);
            Effects.Text(IngredientInfo.Name(rack.Ingredient).ToUpperInvariant() + "!", position + Vector2.up * 0.35f, body, 0.09f);
            Effects.Play(Juice.Sound.Knock, 0.7f, 0.12f);
            Effects.Shake(0.18f);
        }

        private void Cooked(BallTrack track, Vector2 position)
        {
            track.Squash(0.55f);
            Cooker nearest = null;
            float best = float.MaxValue;
            foreach (Cooker cooker in cookers)
            {
                if (cooker.Chamber == null) continue;
                float distance = Mathf.Abs(cooker.Chamber.transform.position.x - position.x);
                if (distance < best) { best = distance; nearest = cooker; }
            }
            if (nearest != null && Effects.Enabled) nearest.Glow = 1;
            Effects.Burst(position, new Color32(255, 196, 64, 255), 16, 2, 6, 0.04f, 0.1f, 0.45f, -2, 3, Vector2.zero);
            Effects.Burst(position, new Color32(255, 108, 40, 255), 12, 1.5f, 5, 0.05f, 0.12f, 0.5f, -3, 3, Vector2.up);
            Effects.Burst(position, new Color(1, 1, 1, 0.5f), 8, 0.5f, 1.8f, 0.12f, 0.24f, 0.9f, -1.5f, 2, Vector2.up);
            Effects.Text("SIZZLE!", position + Vector2.up * 0.5f, BurgerBreakout.Gold, 0.1f);
            Effects.Play(Juice.Sound.Sizzle, 0.65f, 0.1f);
            Effects.Shake(0.14f);
        }

        private void Served(Vector2 position, ServingTarget bun)
        {
            Transform bunImage = bun.transform.Find("Bun");
            Effects.Punch(bunImage, 0.45f, 0.55f, Juice.PunchKind.Squash);
            foreach (Transform part in bun.transform) Effects.Punch(part, 0.6f, 0.45f, Juice.PunchKind.Dip);
            Color[] confetti =
            {
                BurgerBreakout.Green, BurgerBreakout.Gold, BurgerBreakout.Cream, BurgerBreakout.RawRed,
                new Color32(110, 190, 255, 255),
            };
            Vector2 burst = new Vector2(bun.transform.position.x, position.y);
            foreach (Color color in confetti)
                Effects.Burst(burst, color, 9, 3, 8, 0.05f, 0.11f, 0.9f, 9, 1.5f, Vector2.down, false);
            Effects.Text("SERVED!", burst + Vector2.down * 0.6f, BurgerBreakout.Green, 0.13f);
            Effects.Play(Juice.Sound.Serve, 0.75f, 0.03f);
            Effects.Shake(0.28f);
        }

        private void Rejected(BallTrack track, Vector2 position, ServingTarget bun)
        {
            track.Squash(0.35f);
            foreach (Transform part in bun.transform) Effects.Punch(part, 1, 0.4f, Juice.PunchKind.ShakeX);
            Effects.Burst(position, BurgerBreakout.RawRed, 6, 1.5f, 3.5f, 0.04f, 0.08f, 0.3f, 4, 3, Vector2.down);
            Effects.Text("RAW!", position + Vector2.down * 0.45f, BurgerBreakout.RawRed, 0.12f);
            Effects.Play(Juice.Sound.Reject, 0.55f, 0.04f);
            Effects.Shake(0.1f);
        }

        private void Missed(Vector2 position, bool pickle)
        {
            Vector2 drain = new Vector2(position.x, -5.2f);
            Effects.Burst(drain, new Color(0.75f, 0.82f, 0.84f, 0.8f), 10, 1, 3, 0.06f, 0.14f, 0.5f, -1, 3, Vector2.up);
            if (!pickle) Effects.Text("MISS", drain + Vector2.up * 0.45f, new Color32(168, 188, 190, 255), 0.085f);
            Effects.Play(Juice.Sound.Miss, 0.55f, 0.08f, pickle ? 1.2f : 1f);
        }

        /// <summary>Dips every paddle part; the paddle's own transform (with the collider) never moves.</summary>
        private void DipPaddle(float strength)
        {
            Transform root = game.PaddleCollider.transform;
            foreach (Transform part in root) Effects.Punch(part, strength, 0.4f, Juice.PunchKind.Dip);
            Effects.Punch(root.Find("Spatula paddle"), strength * 0.6f, 0.35f, Juice.PunchKind.Squash);
        }

        // ---------------- A small hint under the title ----------------

        private void OnGUI()
        {
            if (Effects == null) return;
            if (hintStyle == null) hintStyle = new GUIStyle(GUI.skin.label) { fontSize = 13 };
            float scale = Mathf.Min(Screen.width / 1280f, Screen.height / 820f);
            Matrix4x4 previous = GUI.matrix;
            GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1));
            hintStyle.normal.textColor = new Color32(168, 188, 190, 255);
            GUI.Label(new Rect(34, 52, 420, 22),
                "J: juice " + (Effects.Enabled ? "ON" : "OFF") + "     M: sound " + (Effects.Muted ? "OFF" : "ON"), hintStyle);
            GUI.matrix = previous;
        }
    }
}
