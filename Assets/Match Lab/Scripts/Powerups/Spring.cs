using UnityEngine;

/// <summary>
/// Clickable Spring powerup. The behaviour lives in PowerUpManager; this
/// squashes the spring visual while it charges, pops it on release and plays
/// the launch burst.
/// </summary>
public class Spring : Powerup
{
    [Header(" Elements ")]
    [Tooltip("Optional. The visual that squashes and pops.")]
    [SerializeField] private Transform visual;
    [Tooltip("Optional. Burst played at the launch point when the item is fired.")]
    [SerializeField] private ParticleSystem launchParticles;

    private Vector3 visualBaseScale;

    private void Awake()
    {
        if (visual != null)
            visualBaseScale = visual.localScale;
    }

    // Compresses while it charges.
    public void PlayLoad(float duration)
    {
        if (visual == null)
            return;

        LeanTween.cancel(visual.gameObject);
        visual.localScale = visualBaseScale;
        LeanTween.scale(visual.gameObject, Vector3.Scale(visualBaseScale, new Vector3(1.1f, .75f, 1.1f)), duration)
            .setEase(LeanTweenType.easeInQuad);
    }

    public void PlayLaunchFx()
    {
        if (launchParticles != null)
            launchParticles.Play();
    }

    // Level ended mid-charge: back to rest without the pop.
    public void StopLoad()
    {
        if (visual == null)
            return;

        LeanTween.cancel(visual.gameObject);
        visual.localScale = visualBaseScale;
    }

    // Pops up as the item is fired, then settles.
    public void PlayRelease()
    {
        if (visual == null)
            return;

        LeanTween.cancel(visual.gameObject);
        LeanTween.scale(visual.gameObject, Vector3.Scale(visualBaseScale, new Vector3(1f, 1.2f, 1f)), .08f)
            .setEase(LeanTweenType.easeOutBack);
        LeanTween.scale(visual.gameObject, visualBaseScale, .15f)
            .setDelay(.08f)
            .setEase(LeanTweenType.easeOutBack);
    }
}
