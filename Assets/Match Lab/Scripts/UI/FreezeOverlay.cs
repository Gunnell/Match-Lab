using UnityEngine;

/// <summary>
/// Frost shown while the timer is frozen: soft frost on the left and right
/// screen edges, a small frost around the timer, and snow along the edges.
/// Driven by a Ratio (1 = invisible, 0 = full frost), so a dissolve shader
/// can take over later without code changes. Never blocks taps.
/// </summary>
public class FreezeOverlay : MonoBehaviour
{
    [Header(" Elements ")]
    [SerializeField] private CanvasGroup screenFrost;
    [SerializeField] private CanvasGroup timerFrost;
    [SerializeField] private ParticleSystem[] snowParticles;

    [Header(" Settings ")]
    [SerializeField] private AnimationCurve fadeCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);
    [SerializeField] private float fadeInDuration = 1.85f;
    [SerializeField] private float fadeOutDuration = 1.85f;
    [SerializeField] private float timerFadeDuration = .65f;
    [Tooltip("Peak alpha of the screen frost. Keep it subtle so the board stays readable.")]
    [Range(0, 1)]
    [SerializeField] private float maxAlpha = .6f;

    private float ratio = 1;
    private float timerRatio = 1;

    public float Ratio
    {
        get => ratio;
        set
        {
            ratio = Mathf.Clamp01(value);
            Apply();
        }
    }

    private void Awake()
    {
        DisableRaycasts(screenFrost);
        DisableRaycasts(timerFrost);
        ResetOverlay();
    }

    public void FadeIn()
    {
        Fade(0, fadeInDuration);

        for (int i = 0; i < snowParticles.Length; i++)
            if (snowParticles[i] != null)
                snowParticles[i].Play();
    }

    public void FadeOut()
    {
        Fade(1, fadeOutDuration);
        StopSnow();
    }

    // Win or lose mid-freeze: gone at once.
    public void ResetOverlay()
    {
        LeanTween.cancel(gameObject);
        ratio = 1;
        timerRatio = 1;
        Apply();
        StopSnow();
    }

    private void Fade(float target, float duration)
    {
        LeanTween.cancel(gameObject);

        LeanTween.value(gameObject, ratio, target, duration)
            .setEase(fadeCurve)
            .setOnUpdate((float value) => Ratio = value);

        LeanTween.value(gameObject, timerRatio, target, timerFadeDuration)
            .setEase(LeanTweenType.easeOutSine)
            .setOnUpdate((float value) =>
            {
                timerRatio = value;
                Apply();
            });
    }

    private void Apply()
    {
        if (screenFrost != null)
            screenFrost.alpha = (1 - ratio) * maxAlpha;

        if (timerFrost != null)
            timerFrost.alpha = 1 - timerRatio;
    }

    private void StopSnow()
    {
        for (int i = 0; i < snowParticles.Length; i++)
            if (snowParticles[i] != null)
                snowParticles[i].Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
    }

    private static void DisableRaycasts(CanvasGroup group)
    {
        if (group == null)
            return;

        group.blocksRaycasts = false;
        group.interactable = false;

        foreach (UnityEngine.UI.Graphic graphic in group.GetComponentsInChildren<UnityEngine.UI.Graphic>(true))
            graphic.raycastTarget = false;
    }
}
