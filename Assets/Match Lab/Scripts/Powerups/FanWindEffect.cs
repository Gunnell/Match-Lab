using UnityEngine;
using UnityEngine.Rendering;

// Tornado from the Fan up into the board, built in code.
// The root faces the camera (local Y = screen up, local Z = away from the
// camera). The swirl rings lie in a plane tilted away from the camera, so
// they read as flat ellipses spinning around the column. Rings start narrow
// at the Fan and widen as they rise, so the column forms a funnel.
public class FanWindEffect : MonoBehaviour
{
    [Header(" Placement ")]
    [Tooltip("From the Fan: X right, Y up the screen, Z toward the camera (keeps the effect in front of flying items).")]
    [SerializeField] private Vector3 offset = new Vector3(-.2f, 1f, 12f);

    [Tooltip("Where the tornado starts, up the screen from the board's front edge (just above the rack). Negative = lower.")]
    [SerializeField] private float startAboveBoardEdge = .2f;

    [Header(" Funnel ")]
    [Tooltip("How fast the rings rise up the screen, world units per second.")]
    [SerializeField] private Vector2 riseSpeed = new Vector2(7f, 8.5f);
    [SerializeField] private Vector2 swirlLifetime = new Vector2(.9f, 1.1f);
    [Tooltip("Ring width at the top of the funnel, once the tornado is fully grown.")]
    [SerializeField] private float swirlSize = 8f;
    [Tooltip("Ring width at the Fan, as a fraction of the top width.")]
    [SerializeField] private float funnelBottom = .15f;
    [Tooltip("The tornado grows: the first rings are this fraction of full size...")]
    [SerializeField] private float growFrom = .5f;
    [Tooltip("...and rings reach full size this many seconds after the wind starts.")]
    [SerializeField] private float growTime = .35f;
    [Tooltip("When the wind stops, the whole tornado fades out over this long, so it is gone before the items fall back.")]
    [SerializeField] private float swirlFadeOut = .45f;
    [SerializeField] private float swirlRate = 32f;
    [SerializeField] private float swirlAlpha = .8f;
    [Tooltip("Degrees per second.")]
    [SerializeField] private float swirlSpin = 420f;
    [Tooltip("How far the ring plane leans away from the camera. 90 = edge-on.")]
    [SerializeField] private float swirlTilt = 64f;
    [Tooltip("Slow sway of the whole column.")]
    [SerializeField] private float swirlSway = .8f;

    [Header(" Flash ")]
    [SerializeField] private float flashSize = 9.8f;
    [SerializeField] private Vector2 flashAlpha = new Vector2(.3f, .55f);

    private ParticleSystem[] systems;
    private ParticleSystem swirls;
    private Material swirlMaterial;

    // Covers the whole emission window, so the grow curve never loops back.
    private const float SwirlCycle = 3f;

    public static FanWindEffect Create(Transform fan)
    {
        GameObject go = new GameObject("Fan Wind");
        FanWindEffect effect = go.AddComponent<FanWindEffect>();
        effect.Place(fan);
        return effect;
    }

    private void Awake() => Build();

    public void Place(Transform fan)
    {
        Vector3 right = Vector3.Cross(CameraService.ScreenUp, CameraService.TowardCamera);
        Vector3 origin = fan != null ? fan.position : transform.position;

        transform.position = origin
            + right * offset.x
            + CameraService.ScreenUp * offset.y
            + CameraService.TowardCamera * offset.z;
        transform.rotation = Quaternion.LookRotation(-CameraService.TowardCamera, CameraService.ScreenUp);

        // The tornado starts at the board's front edge (the board is centred
        // on the world origin); the glow and streaks stay at the propeller.
        if (swirls != null)
        {
            Vector3 frontEdge = new Vector3(0, 0, -BoardService.Depth * .5f);
            float edge = Vector3.Dot(frontEdge - origin, CameraService.ScreenUp);
            float start = Mathf.Max(offset.y, edge + startAboveBoardEdge);
            swirls.transform.localPosition = new Vector3(0, start - offset.y, 0);
        }
    }

    public void Play()
    {
        LeanTween.cancel(gameObject);
        SetSwirlAlpha(1f);

        foreach (ParticleSystem ps in systems)
            ps.Play(false);
    }

    // No new rings; the rings in the air keep rising while the tornado fades.
    public void Stop()
    {
        foreach (ParticleSystem ps in systems)
            ps.Stop(false, ParticleSystemStopBehavior.StopEmitting);

        LeanTween.value(gameObject, SetSwirlAlpha, 1f, 0f, swirlFadeOut)
            .setEase(LeanTweenType.easeInQuad)
            .setOnComplete(() => swirls.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear));
    }

    private void SetSwirlAlpha(float alpha)
    {
        Color c = new Color(1, 1, 1, alpha);
        if (swirlMaterial.HasProperty("_BaseColor"))
            swirlMaterial.SetColor("_BaseColor", c);
        if (swirlMaterial.HasProperty("_Color"))
            swirlMaterial.SetColor("_Color", c);
    }

    #region Build

    private void Build()
    {
        Material swirl = MakeMaterial(MakeSwirlTexture(), "Fan Swirl (runtime)");
        swirlMaterial = swirl;
        Material dot = MakeMaterial(MakeDotTexture(), "Fan Wind Dot (runtime)");

        swirls = BuildSwirls(swirl);
        systems = new[]
        {
            swirls,
            BuildFlash(dot),
            BuildInwardStreaks(dot),
            BuildOutwardStreaks(dot),
        };
    }

    private ParticleSystem BuildSwirls(Material material)
    {
        ParticleSystem ps = NewSystem("Swirls", material, 100, SwirlCycle);

        // Lean the system back: its XY plane is the ring plane. Velocity is
        // converted below, so the column still goes straight up the screen.
        ps.transform.localRotation = Quaternion.Euler(-swirlTilt, 0, 0);
        ParticleSystemRenderer renderer = ps.GetComponent<ParticleSystemRenderer>();
        renderer.alignment = ParticleSystemRenderSpace.Local;
        renderer.maxParticleSize = 5f;

        var main = ps.main;
        main.startLifetime = new ParticleSystem.MinMaxCurve(swirlLifetime.x, swirlLifetime.y);
        main.startSpeed = 0f;
        // Start size follows the system's clock: small at first, full grown after growTime.
        float grown = Mathf.Clamp01(growTime / SwirlCycle);
        main.startSize = new ParticleSystem.MinMaxCurve(swirlSize, new AnimationCurve(
            new Keyframe(0f, growFrom), new Keyframe(grown, 1f), new Keyframe(1f, 1f)));
        main.startRotation = new ParticleSystem.MinMaxCurve(0f, 2f * Mathf.PI);
        main.startColor = new Color(1, 1, 1, swirlAlpha);

        var emission = ps.emission;
        emission.rateOverTime = swirlRate;

        var shape = ps.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Circle;
        shape.radius = .1f;

        // In the tilted system's space: straight up the screen.
        Vector3 up = Quaternion.Inverse(ps.transform.localRotation) * Vector3.up;
        var velocity = ps.velocityOverLifetime;
        velocity.enabled = true;
        velocity.space = ParticleSystemSimulationSpace.Local;
        velocity.x = new ParticleSystem.MinMaxCurve(up.x * riseSpeed.x, up.x * riseSpeed.y);
        velocity.y = new ParticleSystem.MinMaxCurve(up.y * riseSpeed.x, up.y * riseSpeed.y);
        velocity.z = new ParticleSystem.MinMaxCurve(up.z * riseSpeed.x, up.z * riseSpeed.y);

        // Funnel: narrow at the bottom, opening up faster near the top.
        var sizeOverLifetime = ps.sizeOverLifetime;
        sizeOverLifetime.enabled = true;
        sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(
            new Keyframe(0f, funnelBottom, 0f, .4f),
            new Keyframe(.5f, Mathf.Lerp(funnelBottom, 1f, .35f)),
            new Keyframe(1f, 1f, 1.4f, 0f)));

        var rotationOverLifetime = ps.rotationOverLifetime;
        rotationOverLifetime.enabled = true;
        rotationOverLifetime.z = new ParticleSystem.MinMaxCurve(swirlSpin * .85f * Mathf.Deg2Rad, swirlSpin * 1.15f * Mathf.Deg2Rad);

        // Low frequency, so neighbouring rings sway together like one column.
        var noise = ps.noise;
        noise.enabled = true;
        noise.strength = swirlSway;
        noise.frequency = .25f;
        noise.scrollSpeed = .4f;

        var colorOverLifetime = ps.colorOverLifetime;
        colorOverLifetime.enabled = true;
        Gradient g = new Gradient();
        g.SetKeys(
            new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(new Color(.8f, .92f, 1f), 1f) },
            new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, .1f), new GradientAlphaKey(.85f, .7f), new GradientAlphaKey(0f, 1f) });
        colorOverLifetime.color = g;

        return ps;
    }

    private ParticleSystem BuildFlash(Material material)
    {
        ParticleSystem ps = NewSystem("Flash", material, 10, .25f);

        var main = ps.main;
        main.startLifetime = new ParticleSystem.MinMaxCurve(.25f, .3f);
        main.startSpeed = 0f;
        main.startSize = flashSize;
        main.startColor = new ParticleSystem.MinMaxGradient(new Color(1, 1, 1, flashAlpha.x), new Color(1, 1, 1, flashAlpha.y));

        var emission = ps.emission;
        emission.rateOverTime = 6f;

        var shape = ps.shape;
        shape.enabled = false;

        var colorOverLifetime = ps.colorOverLifetime;
        colorOverLifetime.enabled = true;
        colorOverLifetime.color = Fade(Color.white, Color.white, 0f, .5f, 1f);

        return ps;
    }

    // Short streaks pulled into the propeller.
    private ParticleSystem BuildInwardStreaks(Material material)
    {
        ParticleSystem ps = NewSystem("Inward Streaks", material, 100, .05f);
        Stretch(ps, .5f, -1f);

        var main = ps.main;
        main.startLifetime = .09f;
        main.startSpeed = -2.9f;
        main.startSize = new ParticleSystem.MinMaxCurve(.18f, .35f);
        main.startColor = Color.white;

        var emission = ps.emission;
        emission.rateOverTime = 100f;

        HalfCircle(ps, 1f, .68f, new Vector3(.8f, 1.06f, 1f), -1f);

        var sizeOverLifetime = ps.sizeOverLifetime;
        sizeOverLifetime.enabled = true;
        sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0, 0, 1, 1));

        var colorOverLifetime = ps.colorOverLifetime;
        colorOverLifetime.enabled = true;
        colorOverLifetime.color = Fade(Color.white, new Color(.3f, .85f, 1f), 0f, .2f, .35f);

        return ps;
    }

    // Longer streaks blown out of the propeller, up the screen.
    private ParticleSystem BuildOutwardStreaks(Material material)
    {
        ParticleSystem ps = NewSystem("Outward Streaks", material, 100, .05f);
        Stretch(ps, .8f, .1f);

        var main = ps.main;
        main.startLifetime = .11f;
        main.startSpeed = 12f;
        main.startSize = new ParticleSystem.MinMaxCurve(.15f, .55f);
        main.startColor = Color.white;

        var emission = ps.emission;
        emission.rateOverTime = 100f;

        HalfCircle(ps, .55f, 1f, new Vector3(1.35f, 1.8f, 1f), -1f);

        var sizeOverLifetime = ps.sizeOverLifetime;
        sizeOverLifetime.enabled = true;
        sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0, 1, 1, 0));

        var colorOverLifetime = ps.colorOverLifetime;
        colorOverLifetime.enabled = true;
        colorOverLifetime.color = Fade(Color.white, new Color(.6f, .9f, 1f), 0f, .25f, .3f);

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

    // Upper half circle around the propeller. The emitter sits at the column
    // start, so the circle is moved back down to the Fan.
    private static void HalfCircle(ParticleSystem ps, float radius, float thickness, Vector3 scale, float down)
    {
        var shape = ps.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Circle;
        shape.radius = radius;
        shape.radiusThickness = thickness;
        shape.arc = 180f;
        shape.scale = scale;
        shape.position = new Vector3(0, down, 0);
    }

    private static Gradient Fade(Color a, Color b, float startAlpha, float peak, float peakAlpha)
    {
        Gradient g = new Gradient();
        g.SetKeys(
            new[] { new GradientColorKey(a, 0f), new GradientColorKey(b, 1f) },
            new[] { new GradientAlphaKey(startAlpha, 0f), new GradientAlphaKey(peakAlpha, Mathf.Max(peak, .001f)), new GradientAlphaKey(0f, 1f) });
        return g;
    }

    private static Material MakeMaterial(Texture2D texture, string name)
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
        if (shader == null)
            shader = Shader.Find("Sprites/Default");

        Material m = new Material(shader) { name = name };
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

    private static Texture2D MakeDotTexture()
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
                pixels[y * res + x] = new Color(1, 1, 1, Mathf.Pow(Mathf.Clamp01(1f - r), 2f));
            }

        tex.SetPixels(pixels);
        tex.Apply();
        return tex;
    }

    // A few soft wind strands curling round the centre: each an arc that
    // spirals slightly inward, bright at the head and fading along the tail.
    private static Texture2D MakeSwirlTexture()
    {
        const int res = 256;
        // (start angle, arc length, outer radius, inward drift over the arc, width, brightness)
        float[,] strands =
        {
            { 0f,    4.4f, .86f, .14f, .10f, .9f },
            { 2.1f,  3.6f, .70f, .14f, .08f, .75f },
            { 4.0f,  3.0f, .55f, .12f, .07f, .6f },
            { 1.0f,  2.6f, .95f, .06f, .05f, .55f },
        };

        Texture2D tex = new Texture2D(res, res, TextureFormat.RGBA32, true) { wrapMode = TextureWrapMode.Clamp };
        Color[] pixels = new Color[res * res];

        for (int y = 0; y < res; y++)
            for (int x = 0; x < res; x++)
            {
                float dx = (x + .5f) / res * 2f - 1f;
                float dy = (y + .5f) / res * 2f - 1f;
                float r = Mathf.Sqrt(dx * dx + dy * dy);
                float angle = Mathf.Atan2(dy, dx);
                float alpha = 0f;

                for (int s = 0; s < strands.GetLength(0); s++)
                {
                    float along = Mathf.Repeat(angle - strands[s, 0], 2f * Mathf.PI) / strands[s, 1];
                    if (along > 1f)
                        continue;

                    float radius = strands[s, 2] - strands[s, 3] * along;
                    float width = strands[s, 4] * (1f - .6f * along);
                    float d = (r - radius) / width;
                    float head = Mathf.Clamp01(along * 8f);     // soft start
                    float tail = 1f - along;                    // fades along the tail
                    alpha += strands[s, 5] * Mathf.Exp(-d * d) * head * tail * tail;
                }

                pixels[y * res + x] = new Color(1, 1, 1, Mathf.Clamp01(alpha));
            }

        tex.SetPixels(pixels);
        tex.Apply();
        return tex;
    }

    #endregion
}
