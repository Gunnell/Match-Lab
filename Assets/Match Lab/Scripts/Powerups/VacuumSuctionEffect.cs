using UnityEngine;
using UnityEngine.Rendering;

// Suction particles at the vacuum nozzle, built in code.
public class VacuumSuctionEffect : MonoBehaviour
{
    [Header(" Timing (from the tap) ")]
    [SerializeField] private float startDelay = .43f;
    [SerializeField] private float holdTime = 1.34f;
    [SerializeField] private float shrinkTime = .76f;

    [Header(" Look ")]
    [SerializeField] private float size = 1f;
    [Tooltip("Optional, built in code when empty.")]
    [SerializeField] private Material dotMaterial;
    [Tooltip("Optional, built in code when empty.")]
    [SerializeField] private Material ringMaterial;

    private Transform target;
    private ParticleSystem[] systems;

    public static VacuumSuctionEffect Create(Transform target)
    {
        GameObject go = new GameObject("Vacuum Suction");
        VacuumSuctionEffect effect = go.AddComponent<VacuumSuctionEffect>();
        effect.target = target;
        return effect;
    }

    public void SetTarget(Transform newTarget) => target = newTarget;

    private void Awake() => Build();

    private void LateUpdate()
    {
        if (target != null)
            transform.position = target.position;
    }

    public void Play()
    {
        Cancel();
        if (target != null)
            transform.position = target.position;

        transform.localScale = Vector3.zero;

        LeanTween.delayedCall(gameObject, startDelay, () =>
        {
            transform.localScale = Vector3.one * size;
            foreach (ParticleSystem ps in systems)
                ps.Play(false);

            LeanTween.scale(gameObject, Vector3.zero, shrinkTime)
                .setDelay(holdTime)
                .setEase(LeanTweenType.easeOutSine)
                .setOnComplete(() => StopEmitting(true));
        });
    }

    public void Stop() => Cancel();

    private void Cancel()
    {
        LeanTween.cancel(gameObject);
        StopEmitting(true);
    }

    private void StopEmitting(bool clear)
    {
        if (systems == null)
            return;

        foreach (ParticleSystem ps in systems)
            ps.Stop(false, clear ? ParticleSystemStopBehavior.StopEmittingAndClear : ParticleSystemStopBehavior.StopEmitting);
    }

    #region Build

    private void Build()
    {
        // Flat on the board, arc side toward +Z.
        transform.rotation = Quaternion.Euler(90, 0, 0);
        transform.localScale = Vector3.zero;

        Material dot = dotMaterial != null ? dotMaterial : MakeMaterial(MakeDotTexture());
        Material ring = ringMaterial != null ? ringMaterial : MakeMaterial(MakeRingTexture());

        systems = new[]
        {
            BuildRings(ring),
            BuildGlow(dot),
            BuildFineStreaks(dot),
            BuildLongStreaks(dot),
        };
    }

    private ParticleSystem BuildRings(Material material)
    {
        ParticleSystem ps = NewSystem("Rings", material, 8, .5f);

        var main = ps.main;
        main.startLifetime = .8f;
        main.startSpeed = 0f;
        main.startSize = .6f;
        main.startColor = new Color(1, 1, 1, .3f);

        var emission = ps.emission;
        emission.rateOverTime = 6f;

        var shape = ps.shape;
        shape.enabled = false;

        var sizeOverLifetime = ps.sizeOverLifetime;
        sizeOverLifetime.enabled = true;
        sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0, .1f, 1, 1));

        var colorOverLifetime = ps.colorOverLifetime;
        colorOverLifetime.enabled = true;
        colorOverLifetime.color = Fade(Color.white, new Color(.83f, .55f, 1f), 1f, .05f, 1f);

        return ps;
    }

    private ParticleSystem BuildGlow(Material material)
    {
        ParticleSystem ps = NewSystem("Glow", material, 6, .25f);

        var main = ps.main;
        main.startLifetime = new ParticleSystem.MinMaxCurve(.25f, .3f);
        main.startSpeed = 0f;
        main.startSize = 4.5f;
        main.startColor = new Color(.85f, .95f, 1f, .12f);

        var emission = ps.emission;
        emission.rateOverTime = 6f;

        var shape = ps.shape;
        shape.enabled = false;

        var colorOverLifetime = ps.colorOverLifetime;
        colorOverLifetime.enabled = true;
        colorOverLifetime.color = Fade(Color.white, Color.white, 0f, .5f, 1f);

        return ps;
    }

    private ParticleSystem BuildFineStreaks(Material material)
    {
        ParticleSystem ps = NewSystem("Fine Streaks", material, 60, .05f);
        Stretch(ps, .5f, -1f);

        var main = ps.main;
        main.startLifetime = .1f;
        main.startSpeed = -1f;
        main.startSize = new ParticleSystem.MinMaxCurve(0f, .18f);

        var emission = ps.emission;
        emission.rateOverTime = 100f;

        HalfCircle(ps, 1f, .65f, new Vector3(.8f, 1f, 1f));

        var sizeOverLifetime = ps.sizeOverLifetime;
        sizeOverLifetime.enabled = true;
        sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0, 0, 1, 1));

        var colorOverLifetime = ps.colorOverLifetime;
        colorOverLifetime.enabled = true;
        colorOverLifetime.color = Fade(new Color(.3f, .85f, 1f), Color.white, 0f, .2f, 1f);

        return ps;
    }

    private ParticleSystem BuildLongStreaks(Material material)
    {
        ParticleSystem ps = NewSystem("Long Streaks", material, 120, .05f);
        Stretch(ps, .8f, .1f);

        var main = ps.main;
        main.startLifetime = .11f;
        main.startSpeed = -7.5f;
        main.startSize = new ParticleSystem.MinMaxCurve(.15f, .5f);

        var emission = ps.emission;
        emission.rateOverTime = 500f;

        HalfCircle(ps, 1.5f, 1f, new Vector3(1.35f, 1.8f, 1f));

        var sizeOverLifetime = ps.sizeOverLifetime;
        sizeOverLifetime.enabled = true;
        sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0, 1, 1, 0));

        var colorOverLifetime = ps.colorOverLifetime;
        colorOverLifetime.enabled = true;
        colorOverLifetime.color = Fade(Color.white, new Color(.6f, .9f, 1f), .25f, .25f, .7f);

        return ps;
    }

    private ParticleSystem NewSystem(string name, Material material, int maxParticles, float duration)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(transform, false);

        ParticleSystem ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var main = ps.main;
        main.duration = duration;
        main.loop = true;
        main.playOnAwake = false;
        main.maxParticles = maxParticles;
        // So the shrink at the end scales live particles too.
        main.simulationSpace = ParticleSystemSimulationSpace.Local;
        main.scalingMode = ParticleSystemScalingMode.Hierarchy;

        ParticleSystemRenderer renderer = go.GetComponent<ParticleSystemRenderer>();
        renderer.sharedMaterial = material;
        renderer.renderMode = ParticleSystemRenderMode.Billboard;
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        renderer.receiveShadows = false;

        return ps;
    }

    private static void Stretch(ParticleSystem ps, float lengthScale, float velocityScale)
    {
        ParticleSystemRenderer renderer = ps.GetComponent<ParticleSystemRenderer>();
        renderer.renderMode = ParticleSystemRenderMode.Stretch;
        renderer.lengthScale = lengthScale;
        renderer.velocityScale = velocityScale;
    }

    // Use a negative start speed to pull particles inward.
    private static void HalfCircle(ParticleSystem ps, float radius, float thickness, Vector3 scale)
    {
        var shape = ps.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Circle;
        shape.radius = radius;
        shape.radiusThickness = thickness;
        shape.arc = 180f;
        shape.scale = scale;
    }

    private static Gradient Fade(Color a, Color b, float startAlpha, float peak, float peakAlpha)
    {
        Gradient g = new Gradient();
        g.SetKeys(
            new[] { new GradientColorKey(a, 0f), new GradientColorKey(b, 1f) },
            new[] { new GradientAlphaKey(startAlpha, 0f), new GradientAlphaKey(peakAlpha, Mathf.Max(peak, .001f)), new GradientAlphaKey(0f, 1f) });
        return g;
    }

    private static Material MakeMaterial(Texture2D texture)
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
        if (shader == null)
            shader = Shader.Find("Sprites/Default");

        Material m = new Material(shader) { name = "Vacuum Suction (runtime)" };
        m.mainTexture = texture;
        if (m.HasProperty("_BaseMap"))
            m.SetTexture("_BaseMap", texture);

        // Additive
        m.SetFloat("_Surface", 1f);
        m.SetFloat("_Blend", 2f);
        m.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
        m.SetFloat("_DstBlend", (float)BlendMode.One);
        m.SetFloat("_SrcBlendAlpha", (float)BlendMode.One);
        m.SetFloat("_DstBlendAlpha", (float)BlendMode.One);
        m.SetFloat("_ZWrite", 0f);
        m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        m.renderQueue = (int)RenderQueue.Transparent;
        return m;
    }

    private static Texture2D MakeDotTexture() => MakeTexture((r) => Mathf.Pow(Mathf.Clamp01(1f - r), 2f));

    private static Texture2D MakeRingTexture() => MakeTexture((r) =>
        Mathf.Clamp01(1f - Mathf.Abs(r - .85f) / .12f) + .15f * Mathf.Clamp01(1f - r));

    private static Texture2D MakeTexture(System.Func<float, float> alphaAtRadius)
    {
        const int res = 64;
        Texture2D tex = new Texture2D(res, res, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
        Color[] pixels = new Color[res * res];

        for (int y = 0; y < res; y++)
            for (int x = 0; x < res; x++)
            {
                float dx = (x + .5f) / res * 2f - 1f;
                float dy = (y + .5f) / res * 2f - 1f;
                float r = Mathf.Sqrt(dx * dx + dy * dy);
                pixels[y * res + x] = new Color(1, 1, 1, r > 1f ? 0f : Mathf.Clamp01(alphaAtRadius(r)));
            }

        tex.SetPixels(pixels);
        tex.Apply();
        return tex;
    }

    #endregion
}
