using System.Collections.Generic;
using UnityEngine;

namespace SmashBurger
{
    /// <summary>
    /// The effects toolbox: screen shake, particles, pop-up text, springy transforms and generated sound.
    /// It knows nothing about the game's rules; <see cref="JuiceWatcher"/> decides when to call it.
    /// Everything here is cosmetic and lives outside the game's own objects.
    /// </summary>
    public sealed class Juice : MonoBehaviour
    {
        private const int Rate = 44100;
        private const int MaxParticles = 320;

        public bool Enabled { get; private set; } = true;
        public bool Muted { get; private set; }

        private Transform cameraTransform;
        private Vector3 cameraRest;
        private Quaternion cameraRestRotation;
        private Material material;
        private Font font;
        private Sprite circle, square;
        private float trauma;
        private readonly List<Object> generated = new List<Object>();

        // ---------- Particles ----------
        private sealed class Particle
        {
            public SpriteRenderer Image;
            public Vector2 Velocity;
            public float Life, MaxLife, Size, Gravity, Drag, Spin;
            public Color Color;
            public bool Alive;
        }
        private readonly List<Particle> particles = new List<Particle>();
        private int nextParticle;

        // ---------- Pop-up text ----------
        private sealed class Popup
        {
            public TextMesh Text;
            public Vector2 Start;
            public Color Color;
            public float Age, Life, Size;
        }
        private readonly List<Popup> popups = new List<Popup>();

        // ---------- Springy transforms ----------
        public enum PunchKind { Squash, ShakeX, Dip, PopIn }
        private sealed class Spring
        {
            public Transform Target;
            public Vector3 BaseScale, BasePosition;
            public PunchKind Kind;
            public float Age, Duration, Strength;
        }
        // Scale and position springs are tracked separately so one object can dip and squash at once.
        private readonly Dictionary<Transform, Spring> scalePunches = new Dictionary<Transform, Spring>();
        private readonly Dictionary<Transform, Spring> movePunches = new Dictionary<Transform, Spring>();
        private readonly List<Transform> finished = new List<Transform>();

        // ---------- Audio ----------
        public enum Sound { Bounce, Paddle, Knock, Sizzle, Serve, Reject, Launch, Miss, Tick, TimeUp, Catch }
        private readonly Dictionary<Sound, AudioClip> sounds = new Dictionary<Sound, AudioClip>();
        private readonly List<AudioSource> voices = new List<AudioSource>();
        private int nextVoice;
        private readonly System.Random noise = new System.Random(170);

        public void Initialize(Camera view, Material spriteMaterial)
        {
            cameraTransform = view.transform;
            cameraRest = cameraTransform.position;
            cameraRestRotation = cameraTransform.rotation;
            material = spriteMaterial;
            font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            circle = MakeSprite(true);
            square = MakeSprite(false);

            if (FindFirstObjectByType<AudioListener>() == null) view.gameObject.AddComponent<AudioListener>();
            for (int i = 0; i < 10; i++)
            {
                AudioSource voice = gameObject.AddComponent<AudioSource>();
                voice.playOnAwake = false;
                voice.spatialBlend = 0;
                voices.Add(voice);
            }
            BuildSounds();
        }

        public void Toggle()
        {
            Enabled = !Enabled;
            if (Enabled) return;
            trauma = 0;
            cameraTransform.position = cameraRest;
            cameraTransform.rotation = cameraRestRotation;
            foreach (Spring punch in scalePunches.Values) Restore(punch);
            foreach (Spring punch in movePunches.Values) Restore(punch);
            scalePunches.Clear();
            movePunches.Clear();
            foreach (Particle particle in particles) Kill(particle);
            foreach (Popup popup in popups) if (popup.Text != null) Destroy(popup.Text.gameObject);
            popups.Clear();
        }

        public void ToggleMute() => Muted = !Muted;

        // =====================================================================
        // Building blocks used by JuiceWatcher
        // =====================================================================

        /// <summary>Adds screen shake. Amounts stack and are capped; shake strength is trauma squared.</summary>
        public void Shake(float amount)
        {
            if (Enabled) trauma = Mathf.Min(1, trauma + amount);
        }

        /// <summary>Springs a transform's scale or local position, then puts it back exactly as it was.</summary>
        public void Punch(Transform target, float strength, float duration, PunchKind kind)
        {
            if (!Enabled || target == null) return;
            Dictionary<Transform, Spring> punches = Moves(kind) ? movePunches : scalePunches;
            if (!punches.TryGetValue(target, out Spring punch))
            {
                punch = new Spring { Target = target, BaseScale = target.localScale, BasePosition = target.localPosition };
                punches.Add(target, punch);
            }
            punch.Kind = kind;
            punch.Age = 0;
            punch.Duration = duration;
            punch.Strength = strength;
        }

        public void Burst(Vector2 position, Color color, int count, float minSpeed, float maxSpeed,
            float minSize, float maxSize, float life, float gravity, float drag, Vector2 bias, bool round = true)
        {
            if (!Enabled) return;
            for (int i = 0; i < count; i++)
            {
                Vector2 direction = (Random.insideUnitCircle.normalized + bias * 0.9f).normalized;
                if (direction.sqrMagnitude < 0.01f) direction = Random.insideUnitCircle.normalized;
                Spawn(position, direction * Random.Range(minSpeed, maxSpeed), color,
                    Random.Range(minSize, maxSize), life * Random.Range(0.7f, 1.2f), gravity, drag, round);
            }
        }

        /// <summary>A single wisp of steam, used by cooked food while it flies.</summary>
        public void Steam(Vector2 position)
        {
            if (!Enabled) return;
            Spawn(position + Random.insideUnitCircle * 0.12f, new Vector2(Random.Range(-0.25f, 0.25f), Random.Range(0.4f, 0.9f)),
                new Color(1, 1, 1, 0.28f), Random.Range(0.1f, 0.18f), Random.Range(0.45f, 0.7f), -0.6f, 1.5f, true);
        }

        public void Text(string words, Vector2 position, Color color, float size, float life = 0.9f)
        {
            if (!Enabled) return;
            GameObject item = new GameObject("Juice text: " + words);
            item.transform.SetParent(transform, false);
            item.transform.position = position;
            TextMesh text = item.AddComponent<TextMesh>();
            text.text = words;
            text.font = font;
            text.fontSize = 64;
            text.fontStyle = FontStyle.Bold;
            text.characterSize = size;
            text.anchor = TextAnchor.MiddleCenter;
            text.alignment = TextAlignment.Center;
            text.color = color;
            MeshRenderer renderer = item.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = font.material;
            renderer.sortingOrder = 30;
            popups.Add(new Popup { Text = text, Start = position, Color = color, Life = life, Size = size });
        }

        public void Play(Sound sound, float volume, float pitchVariance = 0.08f, float pitch = 1)
        {
            if (!Enabled || Muted || voices.Count == 0 || !sounds.TryGetValue(sound, out AudioClip clip)) return;
            AudioSource voice = voices[nextVoice];
            nextVoice = (nextVoice + 1) % voices.Count;
            voice.pitch = pitch * (1 + Random.Range(-pitchVariance, pitchVariance));
            voice.PlayOneShot(clip, volume * 0.8f);
        }

        // =====================================================================
        // Internals
        // =====================================================================

        private static bool Moves(PunchKind kind) => kind == PunchKind.Dip || kind == PunchKind.ShakeX;

        private void Spawn(Vector2 position, Vector2 velocity, Color color, float size, float life,
            float gravity, float drag, bool round)
        {
            Particle particle = null;
            foreach (Particle candidate in particles)
                if (!candidate.Alive && candidate.Image != null) { particle = candidate; break; }
            if (particle == null && particles.Count < MaxParticles)
            {
                GameObject item = new GameObject("Juice particle");
                item.transform.SetParent(transform, false);
                particle = new Particle { Image = item.AddComponent<SpriteRenderer>() };
                if (material != null) particle.Image.sharedMaterial = material;
                particle.Image.sortingOrder = 20;
                particles.Add(particle);
            }
            else if (particle == null)
            {
                // Recycle the oldest slot so heavy moments never allocate more.
                particle = particles[nextParticle];
                nextParticle = (nextParticle + 1) % MaxParticles;
            }
            particle.Image.sprite = round ? circle : square;
            particle.Image.transform.position = position;
            particle.Image.transform.rotation = Quaternion.Euler(0, 0, Random.Range(0, 360f));
            particle.Image.gameObject.SetActive(true);
            particle.Velocity = velocity;
            particle.Color = color;
            particle.Size = round ? size : size * 1.4f;
            particle.Life = particle.MaxLife = life;
            particle.Gravity = gravity;
            particle.Drag = drag;
            particle.Spin = round ? 0 : Random.Range(-720f, 720f);
            particle.Alive = true;
        }

        private static void Kill(Particle particle)
        {
            particle.Alive = false;
            if (particle.Image != null) particle.Image.gameObject.SetActive(false);
        }

        private Sprite MakeSprite(bool round)
        {
            int size = round ? 32 : 2;
            Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            texture.name = round ? "Juice circle" : "Juice square";
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
            generated.Add(texture);
            generated.Add(sprite);
            return sprite;
        }

        private void Update()
        {
            float dt = Time.deltaTime;

            for (int i = 0; i < particles.Count; i++)
            {
                Particle p = particles[i];
                if (!p.Alive) continue;
                if (p.Image == null) { p.Alive = false; continue; }
                p.Life -= dt;
                if (p.Life <= 0) { Kill(p); continue; }
                p.Velocity += Vector2.down * (p.Gravity * dt);
                p.Velocity *= Mathf.Max(0, 1 - p.Drag * dt);
                Transform t = p.Image.transform;
                t.position += (Vector3)(p.Velocity * dt);
                if (p.Spin != 0) t.Rotate(0, 0, p.Spin * dt);
                float progress = p.Life / p.MaxLife;
                t.localScale = Vector3.one * (p.Size * Mathf.Lerp(0.2f, 1, progress));
                p.Image.color = new Color(p.Color.r, p.Color.g, p.Color.b, p.Color.a * Mathf.Clamp01(progress * 2.5f));
            }

            for (int i = popups.Count - 1; i >= 0; i--)
            {
                Popup popup = popups[i];
                popup.Age += dt;
                if (popup.Text == null || popup.Age >= popup.Life)
                {
                    if (popup.Text != null) Destroy(popup.Text.gameObject);
                    popups.RemoveAt(i);
                    continue;
                }
                float k = popup.Age / popup.Life;
                // Overshoot pop on arrival, then drift upward and fade.
                float scale = 1 + 0.6f * Mathf.Exp(-popup.Age * 14) * Mathf.Cos(popup.Age * 30);
                popup.Text.characterSize = popup.Size * scale;
                popup.Text.transform.position = popup.Start + Vector2.up * (0.7f * (1 - (1 - k) * (1 - k)));
                popup.Text.color = new Color(popup.Color.r, popup.Color.g, popup.Color.b, Mathf.Clamp01((1 - k) * 3.5f));
            }

            AnimatePunches(scalePunches, dt);
            AnimatePunches(movePunches, dt);
        }

        private void AnimatePunches(Dictionary<Transform, Spring> punches, float dt)
        {
            finished.Clear();
            foreach (Spring punch in punches.Values)
            {
                if (punch.Target == null) { finished.Add(punch.Target); continue; }
                punch.Age += dt;
                if (punch.Age >= punch.Duration)
                {
                    Restore(punch);
                    finished.Add(punch.Target);
                    continue;
                }
                float t = punch.Age;
                float spring = punch.Strength * Mathf.Exp(-t * 9) * Mathf.Cos(t * 32);
                switch (punch.Kind)
                {
                    case PunchKind.Squash:
                        punch.Target.localScale = Vector3.Scale(punch.BaseScale, new Vector3(1 + spring, 1 - spring, 1));
                        break;
                    case PunchKind.ShakeX:
                        punch.Target.localPosition = punch.BasePosition
                            + Vector3.right * (0.12f * punch.Strength * Mathf.Exp(-t * 7) * Mathf.Sin(t * 70));
                        break;
                    case PunchKind.Dip:
                        punch.Target.localPosition = punch.BasePosition + Vector3.down * (0.2f * spring);
                        break;
                    case PunchKind.PopIn:
                        float grow = 1 - Mathf.Exp(-t * 9) * Mathf.Cos(t * 20);
                        punch.Target.localScale = punch.BaseScale * grow;
                        break;
                }
            }
            // Unity objects keep their instance-ID hash after Destroy, so removing by the same reference works.
            foreach (Transform done in finished) punches.Remove(done);
        }

        private static void Restore(Spring punch)
        {
            if (punch.Target == null) return;
            if (Moves(punch.Kind)) punch.Target.localPosition = punch.BasePosition;
            else punch.Target.localScale = punch.BaseScale;
        }

        private void LateUpdate()
        {
            if (!Enabled || cameraTransform == null) return;
            trauma = Mathf.Max(0, trauma - Time.deltaTime * 1.8f);
            float shake = trauma * trauma;
            float time = Time.time * 28;
            Vector3 offset = new Vector3(
                (Mathf.PerlinNoise(time, 0.3f) * 2 - 1) * 0.4f * shake,
                (Mathf.PerlinNoise(0.7f, time) * 2 - 1) * 0.4f * shake, 0);
            cameraTransform.position = cameraRest + offset;
            cameraTransform.rotation = cameraRestRotation
                * Quaternion.Euler(0, 0, (Mathf.PerlinNoise(time, time) * 2 - 1) * 2.5f * shake);
        }

        // =====================================================================
        // Sound: a tiny synthesizer so the project needs no audio files
        // =====================================================================

        private enum Wave { Sine, Square, Saw }

        private void BuildSounds()
        {
            float[] d;

            d = Buffer(0.08f);
            Tone(d, 0, 0.08f, 900, 600, Wave.Sine, 0.5f, 45);
            Clip(Sound.Bounce, d);

            d = Buffer(0.16f);
            Tone(d, 0, 0.12f, 220, 90, Wave.Square, 0.3f, 28);
            Tone(d, 0, 0.16f, 120, 60, Wave.Sine, 0.6f, 18);
            Noise(d, 0, 0.05f, 0.35f, 60, 0.35f);
            Clip(Sound.Paddle, d);

            d = Buffer(0.14f);
            Tone(d, 0, 0.13f, 420, 1200, Wave.Sine, 0.6f, 22);
            Noise(d, 0, 0.03f, 0.3f, 90, 0.8f);
            Clip(Sound.Knock, d);

            d = Buffer(0.6f);
            Noise(d, 0, 0.6f, 0.35f, 4.5f, 0.9f);
            for (int i = 0; i < 28; i++)
                Noise(d, (float)noise.NextDouble() * 0.5f, 0.005f, 0.55f, 0, 1);
            Tone(d, 0, 0.2f, 180, 130, Wave.Sine, 0.3f, 14);
            Clip(Sound.Sizzle, d);

            d = Buffer(0.75f);
            float[] notes = { 1047, 1319, 1568, 2093 };
            for (int i = 0; i < notes.Length; i++)
            {
                Tone(d, i * 0.07f, 0.5f, notes[i], notes[i], Wave.Sine, i == 3 ? 0.3f : 0.35f, 7);
                Tone(d, i * 0.07f, 0.3f, notes[i] * 2, notes[i] * 2, Wave.Sine, 0.08f, 12);
            }
            Clip(Sound.Serve, d);

            d = Buffer(0.3f);
            Tone(d, 0, 0.3f, 140, 100, Wave.Saw, 0.3f, 6);
            Tone(d, 0, 0.3f, 147, 104, Wave.Saw, 0.3f, 6);
            Clip(Sound.Reject, d);

            d = Buffer(0.22f);
            Noise(d, 0, 0.22f, 0.3f, 10, 0.12f);
            Tone(d, 0, 0.16f, 300, 900, Wave.Sine, 0.25f, 14);
            Clip(Sound.Launch, d);

            d = Buffer(0.3f);
            Tone(d, 0, 0.3f, 380, 70, Wave.Sine, 0.6f, 9);
            Clip(Sound.Miss, d);

            d = Buffer(0.04f);
            Tone(d, 0, 0.04f, 1800, 1700, Wave.Sine, 0.45f, 80);
            Clip(Sound.Tick, d);

            d = Buffer(0.8f);
            Tone(d, 0, 0.8f, 220, 210, Wave.Saw, 0.22f, 2.5f);
            Tone(d, 0, 0.8f, 277, 265, Wave.Saw, 0.22f, 2.5f);
            Tone(d, 0, 0.8f, 330, 315, Wave.Saw, 0.22f, 2.5f);
            Clip(Sound.TimeUp, d);

            d = Buffer(0.1f);
            Tone(d, 0, 0.1f, 520, 780, Wave.Sine, 0.45f, 30);
            Clip(Sound.Catch, d);
        }

        private static float[] Buffer(float seconds) => new float[Mathf.CeilToInt(seconds * Rate)];

        private static void Tone(float[] data, float start, float duration, float fromHz, float toHz,
            Wave wave, float volume, float decay)
        {
            int offset = (int)(start * Rate);
            int count = (int)(duration * Rate);
            double phase = 0;
            for (int i = 0; i < count && offset + i < data.Length; i++)
            {
                float t = i / (float)Rate;
                phase += Mathf.Lerp(fromHz, toHz, t / duration) / Rate;
                float p = (float)(phase - System.Math.Floor(phase));
                float sample = wave == Wave.Sine ? Mathf.Sin(p * Mathf.PI * 2)
                    : wave == Wave.Square ? (p < 0.5f ? 1 : -1)
                    : p * 2 - 1;
                data[offset + i] += sample * volume * Envelope(t, duration, decay);
            }
        }

        private void Noise(float[] data, float start, float duration, float volume, float decay, float brightness)
        {
            int offset = (int)(start * Rate);
            int count = Mathf.Max(1, (int)(duration * Rate));
            float filtered = 0;
            for (int i = 0; i < count && offset + i < data.Length; i++)
            {
                float t = i / (float)Rate;
                float white = (float)noise.NextDouble() * 2 - 1;
                filtered += Mathf.Clamp01(brightness) * (white - filtered);
                data[offset + i] += filtered * volume * Envelope(t, duration, decay);
            }
        }

        // Short attack and release ramps prevent audible clicks.
        private static float Envelope(float t, float duration, float decay) =>
            Mathf.Exp(-t * decay) * Mathf.Clamp01(t * 400) * Mathf.Clamp01((duration - t) * 200);

        private void Clip(Sound sound, float[] data)
        {
            float peak = 0;
            foreach (float sample in data) peak = Mathf.Max(peak, Mathf.Abs(sample));
            if (peak > 0.9f)
                for (int i = 0; i < data.Length; i++) data[i] *= 0.9f / peak;
            AudioClip clip = AudioClip.Create("Juice " + sound, data.Length, 1, Rate, false);
            clip.SetData(data, 0);
            generated.Add(clip);
            sounds[sound] = clip;
        }

        private void OnDestroy()
        {
            if (cameraTransform != null)
            {
                cameraTransform.position = cameraRest;
                cameraTransform.rotation = cameraRestRotation;
            }
            foreach (Object asset in generated)
                if (asset != null) Destroy(asset);
        }
    }
}
