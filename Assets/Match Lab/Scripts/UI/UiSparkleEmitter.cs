using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Random = UnityEngine.Random;

/// <summary>
/// Sparkles made of UI images, for Screen Space Overlay canvases where
/// ParticleSystems don't render. Emits continuously from the outline of a
/// RectTransform (Play / Detach / Stop), or all at once from a point (Burst).
/// Sparkles live in a container that doesn't move with the source, so they
/// stay where they were emitted. Images are pooled and never block raycasts.
/// </summary>
public class UiSparkleEmitter : MonoBehaviour
{
    // How one kind of sparkle looks and moves. Curves take the normalised age (0..1).
    public class Profile
    {
        public Vector2 lifetime;
        public Vector2 speed;
        public Vector2 size;
        public Color colorA, colorB;
        public float spreadDegrees;
        public float maxRotation;
        public float twinkle;
        public Func<float, float> speedOverLife;
        public Func<float, float> sizeOverLife;
        public Func<float, float> alphaOverLife;
    }

    // Shed from a completing goal card's outline.
    public static readonly Profile Outline = new Profile
    {
        lifetime = new Vector2(.45f, .75f),
        speed = new Vector2(110, 220),
        size = new Vector2(33, 44),
        colorA = new Color(1f, 1f, .79f),
        colorB = new Color(1f, .93f, .33f),
        spreadDegrees = 35,
        maxRotation = 20,
        twinkle = .15f,
        speedOverLife = t => Mathf.Lerp(1f, .04f, t),
        sizeOverLife = t => t < .3f ? 1f : 1f - (t - .3f) / .7f,
        alphaOverLife = t => t < .55f ? 1f : 1f - (t - .55f) / .45f,
    };

    // Burst under a goal card when one of its items is collected.
    public static readonly Profile Hit = new Profile
    {
        lifetime = new Vector2(.2f, .5f),
        speed = new Vector2(220, 440),
        size = new Vector2(22, 66),
        colorA = new Color(.99f, 1f, .68f),
        colorB = new Color(1f, .93f, 0f),
        spreadDegrees = 180,
        maxRotation = 20,
        twinkle = 0,
        speedOverLife = t => 1f,
        sizeOverLife = t => t < .52f ? Mathf.Lerp(.81f, .68f, t / .52f)
            : t < .88f ? Mathf.Lerp(.68f, 0f, (t - .52f) / .36f) : 0f,
        alphaOverLife = t => t < .05f ? t / .05f : t < .4f ? 1f : 1f - (t - .4f) / .6f,
    };

    private class Sparkle
    {
        public Image image;
        public RectTransform rect;
        public Profile profile;
        public Vector2 velocity;
        public Color color;
        public float age, life, size, phase;
    }

    // Shared by every emitter, so two cards completing together stay under the cap.
    private const int MaxLiveSparkles = 300;
    private static int liveCount;
    private static readonly Stack<Image> pool = new Stack<Image>();

    private readonly List<Sparkle> sparkles = new List<Sparkle>();
    private readonly Vector3[] worldCorners = new Vector3[4];
    private readonly Vector2[] corners = new Vector2[4];
    private RectTransform container;
    private Sprite sprite;
    private Profile profile;
    private RectTransform shapeSource;
    private bool emitting;
    private bool detached;
    private float rate;
    private float pending;

    /// <summary>A new emitter living in <paramref name="container"/>. It destroys itself once stopped and empty.</summary>
    public static UiSparkleEmitter Create(RectTransform container, Sprite sprite, Profile profile)
    {
        GameObject go = new GameObject("Sparkle Emitter", typeof(RectTransform));
        RectTransform rect = (RectTransform)go.transform;
        rect.SetParent(container, false);
        rect.SetAsLastSibling();

        UiSparkleEmitter emitter = go.AddComponent<UiSparkleEmitter>();
        emitter.container = container;
        emitter.sprite = sprite;
        emitter.profile = profile;
        return emitter;
    }

    /// <summary>One-shot: <paramref name="count"/> sparkles from a point in container space.</summary>
    public static void Burst(RectTransform container, Vector2 localPosition, Sprite sprite, Profile profile, int count)
    {
        if (container == null)
            return;

        UiSparkleEmitter emitter = Create(container, sprite, profile);
        for (int i = 0; i < count; i++)
        {
            float angle = Random.Range(0f, 360f) * Mathf.Deg2Rad;
            emitter.Spawn(localPosition, new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)));
        }
    }

    /// <summary>Emits <paramref name="perSecond"/> sparkles from the outline of <paramref name="source"/>, following it.</summary>
    public void Play(RectTransform source, float perSecond)
    {
        shapeSource = source;
        rate = perSecond;
        emitting = true;
        detached = false;
        pending = 0;
    }

    /// <summary>Stops following the source but keeps emitting from its outline at full size (scale 1, no rotation).</summary>
    public void Detach()
    {
        if (shapeSource != null)
        {
            Rect r = shapeSource.rect;
            Transform parent = shapeSource.parent;
            Vector3 pos = shapeSource.localPosition;
            Vector3[] local =
            {
                new Vector3(r.xMin, r.yMin), new Vector3(r.xMin, r.yMax),
                new Vector3(r.xMax, r.yMax), new Vector3(r.xMax, r.yMin),
            };

            for (int i = 0; i < 4; i++)
            {
                Vector3 world = parent != null ? parent.TransformPoint(pos + local[i]) : pos + local[i];
                corners[i] = container.InverseTransformPoint(world);
            }
        }

        detached = true;
        shapeSource = null;
    }

    /// <summary>Stops emitting; live sparkles finish, then the emitter removes itself.</summary>
    public void Stop() => emitting = false;

    private void Update()
    {
        float dt = Time.deltaTime;

        if (emitting)
            Emit(dt);

        for (int i = sparkles.Count - 1; i >= 0; i--)
        {
            Sparkle s = sparkles[i];
            s.age += dt;
            float t = s.age / s.life;

            if (t >= 1)
            {
                Release(s);
                sparkles.RemoveAt(i);
                continue;
            }

            Profile p = s.profile;
            s.rect.anchoredPosition += s.velocity * (p.speedOverLife(t) * dt);

            float twinkle = 1 + p.twinkle * Mathf.Sin(s.phase + s.age * 28f);
            s.rect.localScale = Vector3.one * Mathf.Max(0, p.sizeOverLife(t) * twinkle);

            Color c = s.color;
            c.a = Mathf.Clamp01(p.alphaOverLife(t));
            s.image.color = c;
        }

        if (!emitting && sparkles.Count == 0)
            Destroy(gameObject);
    }

    private void Emit(float dt)
    {
        if (!detached)
        {
            // The source is gone without a Detach (e.g. destroyed): nothing to emit from.
            if (shapeSource == null)
            {
                emitting = false;
                return;
            }

            // World corners follow the card's pop, wobble and spin.
            shapeSource.GetWorldCorners(worldCorners);
            for (int i = 0; i < 4; i++)
                corners[i] = container.InverseTransformPoint(worldCorners[i]);
        }

        pending += rate * dt;
        int count = (int)pending;
        pending -= count;

        float perimeter = 0;
        for (int i = 0; i < 4; i++)
            perimeter += Vector2.Distance(corners[i], corners[(i + 1) % 4]);

        // Scaled to 0 or edge-on: no outline to emit from this frame.
        if (perimeter < 1f)
            return;

        Vector2 centre = (corners[0] + corners[1] + corners[2] + corners[3]) * .25f;

        for (int n = 0; n < count; n++)
        {
            // A random point on the outline, each edge weighted by its length.
            float d = Random.Range(0, perimeter);
            for (int i = 0; i < 4; i++)
            {
                Vector2 a = corners[i], b = corners[(i + 1) % 4];
                float length = Vector2.Distance(a, b);
                if (d > length && i < 3)
                {
                    d -= length;
                    continue;
                }

                Vector2 point = Vector2.Lerp(a, b, length > 0 ? d / length : 0);
                Vector2 edge = (b - a).normalized;
                Vector2 normal = new Vector2(edge.y, -edge.x);
                if (Vector2.Dot(normal, point - centre) < 0)
                    normal = -normal;

                Spawn(point, normal);
                break;
            }
        }
    }

    private void Spawn(Vector2 localPosition, Vector2 direction)
    {
        if (liveCount >= MaxLiveSparkles)
            return;

        Image image = Rent();
        RectTransform rect = image.rectTransform;
        rect.SetParent(container, false);
        rect.SetAsLastSibling();
        rect.anchorMin = rect.anchorMax = container.pivot;

        // anchoredPosition from the pivot anchor is the container's local position.
        rect.anchoredPosition = localPosition;

        Profile p = profile;
        float size = Random.Range(p.size.x, p.size.y);
        rect.sizeDelta = Vector2.one * size;
        rect.localScale = Vector3.one * p.sizeOverLife(0);
        rect.localRotation = Quaternion.Euler(0, 0, Random.Range(-p.maxRotation, p.maxRotation));

        float spread = Random.Range(-p.spreadDegrees, p.spreadDegrees);
        Vector2 velocity = (Vector2)(Quaternion.Euler(0, 0, spread) * direction) * Random.Range(p.speed.x, p.speed.y);

        Color color = Color.Lerp(p.colorA, p.colorB, Random.value);
        color.a = Mathf.Clamp01(p.alphaOverLife(0));
        image.color = color;
        image.sprite = sprite;

        sparkles.Add(new Sparkle
        {
            image = image,
            rect = rect,
            profile = p,
            velocity = velocity,
            color = color,
            life = Random.Range(p.lifetime.x, p.lifetime.y),
            phase = Random.Range(0, Mathf.PI * 2),
        });
        liveCount++;
    }

    private static Image Rent()
    {
        // Pooled images die with their container (scene reload): skip those.
        while (pool.Count > 0)
        {
            Image pooled = pool.Pop();
            if (pooled != null)
            {
                pooled.gameObject.SetActive(true);
                return pooled;
            }
        }

        GameObject go = new GameObject("Sparkle", typeof(RectTransform), typeof(Image));
        Image image = go.GetComponent<Image>();
        image.raycastTarget = false;
        return image;
    }

    private void Release(Sparkle s)
    {
        liveCount--;
        if (s.image == null)
            return;

        s.image.gameObject.SetActive(false);
        pool.Push(s.image);
    }

    // The game panel was hidden (level end) or the scene is unloading: clear up now.
    private void OnDisable()
    {
        for (int i = 0; i < sparkles.Count; i++)
            Release(sparkles[i]);

        sparkles.Clear();
        emitting = false;
        Destroy(gameObject);
    }

    // Static state survives play sessions when domain reload is off.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        liveCount = 0;
        pool.Clear();
    }
}
