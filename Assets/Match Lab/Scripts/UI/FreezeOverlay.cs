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

    [Tooltip("Half the view height the snow emitters were laid out for (their parent sits on the camera). "
        + "The snow is scaled from this to the camera's real view, so it stays on the screen edges whatever the camera.")]
    [SerializeField] private float snowDesignHalfHeight = 1.87f;

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

    private Vector3[] snowDesignPositions;
    private Vector3[] snowDesignShapeScales;

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
        StoreSnowLayout();
        ResetOverlay();
    }

    public void FadeIn()
    {
        Fade(0, fadeInDuration);
        FitSnowToCamera();

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

    private void StoreSnowLayout()
    {
        snowDesignPositions = new Vector3[snowParticles.Length];
        snowDesignShapeScales = new Vector3[snowParticles.Length];

        for (int i = 0; i < snowParticles.Length; i++)
        {
            if (snowParticles[i] == null)
                continue;

            snowDesignPositions[i] = snowParticles[i].transform.localPosition;
            snowDesignShapeScales[i] = snowParticles[i].shape.scale;

            // Flakes and their drift scale with the parent, see FitSnowToCamera.
            var main = snowParticles[i].main;
            main.scalingMode = ParticleSystemScalingMode.Hierarchy;
        }
    }

    // The emitters sit on a parent under the camera. Scale that parent so the
    // layout matches the camera's view, then pin each emitter to its screen
    // edge: top (laid out above centre) or left/right (laid out beside it).
    private void FitSnowToCamera()
    {
        Camera cam = Camera.main;
        if (cam == null || snowParticles.Length == 0 || snowParticles[0] == null || snowDesignHalfHeight <= 0)
            return;

        Transform root = snowParticles[0].transform.parent;
        if (root == null)
            return;

        float depth = Mathf.Abs(snowDesignPositions[0].z);
        float halfHeight = cam.orthographic
            ? cam.orthographicSize
            : depth * Mathf.Tan(cam.fieldOfView * .5f * Mathf.Deg2Rad);

        float k = halfHeight / snowDesignHalfHeight;
        root.localScale = Vector3.one * k;

        // In the parent's (scaled) space.
        float halfH = snowDesignHalfHeight;
        float halfW = halfH * cam.aspect;

        for (int i = 0; i < snowParticles.Length; i++)
        {
            if (snowParticles[i] == null)
                continue;

            Vector3 p = snowDesignPositions[i];
            Vector3 shapeScale = snowDesignShapeScales[i];

            if (Mathf.Abs(p.y) > Mathf.Abs(p.x))
            {
                p.y = Mathf.Sign(p.y) * halfH;
                shapeScale.x = halfW * 2f;
            }
            else
            {
                p.x = Mathf.Sign(p.x) * halfW;
                shapeScale.y = halfH * 2f;
            }

            snowParticles[i].transform.localPosition = p;
            var shape = snowParticles[i].shape;
            shape.scale = shapeScale;
        }
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
