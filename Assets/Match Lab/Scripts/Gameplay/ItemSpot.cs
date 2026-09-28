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
    [SerializeField] private float bounceAmplitude = .02f;
    [Tooltip("Bounces weaker than this are skipped.")]
    [SerializeField] private float minBounceStrength = .15f;

    // Damped bounce: down, up, down, settle. Times are cumulative seconds;
    // offsets are fractions of the amplitude. Each step eases in-out sine.
    private static readonly float[] bounceTimes = { 0f, .05f, .133f, .25f, .383f };
    private static readonly float[] bounceY = { 0f, -1f, .366f, -.284f, 0f };
    private static readonly float[] bounceZ = { 0f, -.247f, .165f, -.123f, 0f };

    [Header(" Settings ")]
    private Item item;
    public Item Item => item;

    private Vector3 restPosition;
    private float landingStrength;

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
    }

    public void Populate(Item item)
    {
        this.item = item;
        item.transform.SetParent(itemParent);
        item.AssignSpot(this);

        
    }

    public bool IsEmpty() => item == null;

    public void Clear()
    {
        item = null;
    }

    // Tints the slot to the colour and back, through a property block so no
    // material copy is made. Restores the material's own colour exactly.
    public void PlayFlash(Color color, float inTime, float outTime)
    {
        if (slotRenderer == null)
            return;

        Material material = slotRenderer.sharedMaterial;
        int colorId = material.HasProperty(BaseColorId) ? BaseColorId : ColorId;
        Color baseColor = material.GetColor(colorId);
        GameObject target = slotRenderer.gameObject;

        if (flashBlock == null)
            flashBlock = new MaterialPropertyBlock();

        LeanTween.cancel(target);

        LeanTween.value(target, 0f, 1f, inTime)
            .setEase(LeanTweenType.easeOutSine)
            .setOnUpdate((float k) => SetTint(colorId, Color.Lerp(baseColor, color, k)));

        LeanTween.value(target, 1f, 0f, outTime)
            .setDelay(inTime)
            .setEase(LeanTweenType.easeInSine)
            .setOnUpdate((float k) => SetTint(colorId, Color.Lerp(baseColor, color, k)))
            .setOnComplete(() => slotRenderer.SetPropertyBlock(null));
    }

    private void SetTint(int colorId, Color color)
    {
        flashBlock.SetColor(colorId, color);
        slotRenderer.SetPropertyBlock(flashBlock);
    }

    // How hard the next landing hits, 0..1 (set when an item is sent here).
    public void SetLandingStrength(float strength)
        => landingStrength = Mathf.Clamp01(strength);

    public void BumpDown()
    {
        float strength = landingStrength;
        landingStrength = 0;

        if (strength <= minBounceStrength)
            return;

        GameObject target = itemParent.gameObject;
        LeanTween.cancel(target);
        itemParent.localPosition = restPosition;

        float scale = strength * bounceAmplitude;
        float total = bounceTimes[bounceTimes.Length - 1];

        LeanTween.value(target, 0f, total, total)
            .setOnUpdate((float time) =>
            {
                int step = 1;
                while (step < bounceTimes.Length - 1 && time > bounceTimes[step])
                    step++;

                float k = Mathf.InverseLerp(bounceTimes[step - 1], bounceTimes[step], time);
                k = -(Mathf.Cos(Mathf.PI * k) - 1) * .5f; // ease in-out sine

                float y = Mathf.Lerp(bounceY[step - 1], bounceY[step], k);
                float z = Mathf.Lerp(bounceZ[step - 1], bounceZ[step], k);
                itemParent.localPosition = restPosition + new Vector3(0, y, z) * scale;
            })
            .setOnComplete(() => itemParent.localPosition = restPosition);
    }

}
