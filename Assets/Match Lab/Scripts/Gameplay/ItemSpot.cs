using System.Collections.Generic;
using UnityEngine;

public class ItemSpot : MonoBehaviour
{
    [Header(" Elements ")] 
    [SerializeField] private Animator animator;
    [SerializeField] private Transform itemParent;
    [Tooltip("The slot's own mesh, tinted by PlayFlash. Found under the item parent if empty.")]
    [SerializeField] private Renderer slotRenderer;

    [Header(" Landing Bounce ")]
    [Tooltip("Size of the bounce in the spot's local units, at full strength.")]
    [SerializeField] private float bounceAmplitude = .031f;
    [Tooltip("Bounces weaker than this are skipped.")]
    [SerializeField] private float minBounceStrength = .15f;
    [Tooltip("Size of the merge jump (slot dips as its item leaves for a merge), about 1.44x the landing bounce.")]
    [SerializeField] private float jumpAmplitude = .045f;

    [Header(" Contact Shadow ")]
    [Tooltip("Soft dark ellipse on the slot surface. Its alpha follows how close the nearest rack item is.")]
    [SerializeField] private SpriteRenderer contactShadow;
    [Tooltip("Items closer than this (world units, from the slot centre) get the full shadow.")]
    [SerializeField] private float shadowNear = .24f;
    [Tooltip("Fade rate past shadowNear: the shadow is gone at shadowNear + 1 / shadowFalloff.")]
    [SerializeField] private float shadowFalloff = 2.3f;
    [Tooltip("Only items within this sideways distance (world, along the rack) count, so a neighbour's item doesn't shade this slot.")]
    [SerializeField] private float shadowHalfWidth = .25f;
    [SerializeField] private float shadowMaxAlpha = .35f;
    [Tooltip("Share of the gap closed per 60 Hz frame; gaps under shadowSnap are closed at once.")]
    [SerializeField] private float shadowSmoothing = .125f;
    [SerializeField] private float shadowSnap = .125f;

    [Header(" Shine ")]
    // A tint over the slot (blend toward a colour, 0 = the slot's own look),
    // used by the landing / pass shines, the Spring flash and the red warning.
    [SerializeField] private Color landingShineColor = Color.white;
    [Tooltip("Shine peak when an item lands from the board (only when the landing bounce plays).")]
    [SerializeField] private float landingShinePeak = .42f;
    [Tooltip("Landing shine multiplier for a slide inside the rack.")]
    [SerializeField] private float slideLandingShineScale = .35f;
    [Tooltip("Faint glint when a sliding item passes over this slot.")]
    [SerializeField] private float passShinePeak = .147f;
    [SerializeField] private float shineFadeTime = .333f;

    // Damped bounce: down, up, down, settle. Times are cumulative seconds;
    // offsets are fractions of the amplitude. Each step eases in-out sine.
    private static readonly float[] bounceTimes = { 0f, .05f, .133f, .25f, .383f };
    private static readonly float[] bounceY = { 0f, -1f, .366f, -.284f, 0f };
    private static readonly float[] bounceZ = { 0f, -.247f, .165f, -.123f, 0f };

    // Merge jump: Y only, fixed strength. First step eases out, the rest in-out.
    private static readonly float[] jumpTimes = { 0f, .05f, .117f, .183f, .300f };
    private static readonly float[] jumpY = { 0f, -1f, .506f, -.277f, 0f };
    private static readonly float[] jumpZ = { 0f, 0f, 0f, 0f, 0f };

    [Header(" Settings ")]
    private Item item;
    public Item Item => item;

    // The item that last left this slot (merge, Vacuum, Spring, compaction),
    // so the shadow fades out as it moves away instead of vanishing.
    private Item previousItem;
    private float shadowAlpha;
    // Every spot's current and previous item are shadow candidates for every
    // spot, so a slot also darkens as an item slides past it.
    private static readonly List<ItemSpot> allSpots = new List<ItemSpot>();

    private Vector3 restPosition;
    private float landingStrength;
    private bool landingFromBoard = true;

    private Color shineColor = Color.white;
    private float shineAlpha;
    private bool isWarning;
    public bool IsWarning => isWarning;

    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int ColorId = Shader.PropertyToID("_Color");
    private MaterialPropertyBlock flashBlock;

    private void Awake()
    {
        // The Bump clip's Animator (write defaults on) would overwrite the
        // tweened bounce every frame, so the bounce is driven by code instead.
        if (animator != null)
            animator.enabled = false;

        restPosition = itemParent.localPosition;

        // Items are parented here later, so at this point the only renderer is the slot's own.
        if (slotRenderer == null)
            slotRenderer = itemParent.GetComponentInChildren<Renderer>(true);

        SetShadowAlpha(0);
    }

    private void OnEnable() => allSpots.Add(this);

    private void OnDisable() => allSpots.Remove(this);

    // After the item tweens have moved things this frame.
    private void LateUpdate()
    {
        if (contactShadow == null)
            return;

        float target = 0;
        float d = NearestItemDistance();

        if (d < float.MaxValue)
        {
            float t = Mathf.Clamp01((d - shadowNear) * shadowFalloff);
            target = (1 - t) * (1 - t) * shadowMaxAlpha;
        }

        float gap = target - shadowAlpha;
        if (Mathf.Abs(gap) > shadowSnap)
            shadowAlpha += gap * (1 - Mathf.Pow(1 - shadowSmoothing, Time.deltaTime * 60f));
        else
            shadowAlpha = target;

        SetShadowAlpha(shadowAlpha);
    }

    private float NearestItemDistance()
    {
        float nearest = float.MaxValue;

        for (int i = 0; i < allSpots.Count; i++)
        {
            nearest = Mathf.Min(nearest, DistanceTo(allSpots[i].item));
            nearest = Mathf.Min(nearest, DistanceTo(allSpots[i].previousItem));
        }

        return nearest;
    }

    // Distance from the slot centre, or MaxValue when outside this slot's lane.
    private float DistanceTo(Item candidate)
    {
        if (candidate == null)
            return float.MaxValue;

        Vector3 offset = candidate.transform.position - transform.position;
        if (Mathf.Abs(Vector3.Dot(offset, transform.right)) > shadowHalfWidth)
            return float.MaxValue;

        return offset.magnitude;
    }

    private void SetShadowAlpha(float alpha)
    {
        if (contactShadow == null)
            return;

        Color c = contactShadow.color;
        c.a = alpha;
        contactShadow.color = c;
        contactShadow.enabled = alpha > 0;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => allSpots.Clear();

    public void Populate(Item item)
    {
        this.item = item;
        item.transform.SetParent(itemParent);
        item.AssignSpot(this);

        
    }

    public bool IsEmpty() => item == null;

    public void Clear()
    {
        if (item != null)
            previousItem = item;

        item = null;
    }

    // Spring: tint to the colour and back (in easeOutSine, out easeInSine).
    public void PlayFlash(Color color, float inTime, float outTime)
    {
        if (slotRenderer == null)
            return;

        GameObject target = slotRenderer.gameObject;
        LeanTween.cancel(target);
        shineColor = color;

        LeanTween.value(target, shineAlpha, 1f, inTime)
            .setEase(LeanTweenType.easeOutSine)
            .setOnUpdate(SetShine);

        LeanTween.value(target, 1f, 0f, outTime)
            .setDelay(inTime)
            .setEase(LeanTweenType.easeInSine)
            .setOnUpdate(SetShine)
            .setOnComplete(() => SetShine(0));
    }

    // Snap to the peak and fade out. Skipped while the warning owns the slot.
    public void PlayShine(Color color, float peak, float fadeTime)
    {
        if (slotRenderer == null || isWarning)
            return;

        GameObject target = slotRenderer.gameObject;
        LeanTween.cancel(target);
        shineColor = color;
        SetShine(peak);

        LeanTween.value(target, peak, 0f, fadeTime)
            .setEase(LeanTweenType.easeOutSine)
            .setOnUpdate(SetShine)
            .setOnComplete(() => SetShine(0));
    }

    // A sliding item passed over this slot.
    public void PlayPassShine()
        => PlayShine(landingShineColor, passShinePeak, shineFadeTime);

    // Pulses 0 -> 1 -> 0 (each half easeInOutSine) until stopped.
    public void StartWarning(Color color, float halfPeriod)
    {
        if (slotRenderer == null || isWarning)
            return;

        GameObject target = slotRenderer.gameObject;
        LeanTween.cancel(target);
        isWarning = true;
        shineColor = color;

        LeanTween.value(target, 0f, 1f, halfPeriod)
            .setEase(LeanTweenType.easeInOutSine)
            .setLoopPingPong(-1)
            .setOnUpdate(SetShine);
    }

    // Fades from wherever the pulse is to 0.
    public void StopWarning(float fadeTime)
    {
        if (slotRenderer == null || !isWarning)
            return;

        GameObject target = slotRenderer.gameObject;
        LeanTween.cancel(target);
        isWarning = false;

        LeanTween.value(target, shineAlpha, 0f, fadeTime)
            .setEase(LeanTweenType.easeOutSine)
            .setOnUpdate(SetShine)
            .setOnComplete(() => SetShine(0));
    }

    // Tint = blend from the material's own colour toward the shine colour,
    // through a property block (no material copy). At 0 the block is cleared,
    // which restores the original look exactly.
    private void SetShine(float alpha)
    {
        shineAlpha = Mathf.Clamp01(alpha);

        if (shineAlpha <= 0)
        {
            slotRenderer.SetPropertyBlock(null);
            return;
        }

        Material material = slotRenderer.sharedMaterial;
        int colorId = material.HasProperty(BaseColorId) ? BaseColorId : ColorId;

        if (flashBlock == null)
            flashBlock = new MaterialPropertyBlock();

        flashBlock.SetColor(colorId, Color.Lerp(material.GetColor(colorId), shineColor, shineAlpha));
        slotRenderer.SetPropertyBlock(flashBlock);
    }

    // How hard the next landing hits, 0..1 (set when an item is sent here).
    public void SetLandingStrength(float strength, bool fromBoard = true)
    {
        landingStrength = Mathf.Clamp01(strength);
        landingFromBoard = fromBoard;
    }

    public void BumpDown()
    {
        float strength = landingStrength;
        landingStrength = 0;

        if (strength <= minBounceStrength)
            return;

        PlayProfile(bounceTimes, bounceY, bounceZ, strength * bounceAmplitude, false);
        PlayShine(landingShineColor, landingShinePeak * (landingFromBoard ? 1f : slideLandingShineScale), shineFadeTime);
    }

    // Slot dips as its item leaves for a merge.
    public void PlayJump()
        => PlayProfile(jumpTimes, jumpY, jumpZ, jumpAmplitude, true);

    // Plays a keyframed offset of the item parent, cancelling any running one.
    private void PlayProfile(float[] times, float[] ys, float[] zs, float scale, bool firstStepEaseOut)
    {
        GameObject target = itemParent.gameObject;
        LeanTween.cancel(target);
        itemParent.localPosition = restPosition;

        float total = times[times.Length - 1];

        LeanTween.value(target, 0f, total, total)
            .setOnUpdate((float time) =>
            {
                int step = 1;
                while (step < times.Length - 1 && time > times[step])
                    step++;

                float k = Mathf.InverseLerp(times[step - 1], times[step], time);
                k = firstStepEaseOut && step == 1
                    ? Mathf.Sin(k * Mathf.PI * .5f)          // ease out sine
                    : -(Mathf.Cos(Mathf.PI * k) - 1) * .5f;  // ease in-out sine

                float y = Mathf.Lerp(ys[step - 1], ys[step], k);
                float z = Mathf.Lerp(zs[step - 1], zs[step], k);
                itemParent.localPosition = restPosition + new Vector3(0, y, z) * scale;
            })
            .setOnComplete(() => itemParent.localPosition = restPosition);
    }

}
