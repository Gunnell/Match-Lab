using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class GoalCard : MonoBehaviour
{
    [Header(" Elements ")]
    [SerializeField] private TextMeshProUGUI amountText;
    [SerializeField] private Image iconImage;
    [SerializeField] private GameObject backOfCard;
    [SerializeField] private GameObject checkMark;
    [Tooltip("Old Complete clip. Kept but no longer used: the complete animation is driven in code.")]
    [SerializeField] private Animator completeAnimator;
    [Tooltip("Star sprite for the hit and complete sparkles, tinted gold in code.")]
    [SerializeField] private Sprite sparkleSprite;

    [Header(" Hit (goal item collected) ")]
    [SerializeField] private float hitScale = 1.18f;
    [SerializeField] private float hitUp = .033f;
    [SerializeField] private float hitDown = .15f;
    [SerializeField] private int hitSparkleCount = 20;
    [Tooltip("Hit sparkles burst from this far below the card centre (card space, px).")]
    [SerializeField] private float hitSparkleOffset = 74f;

    [Header(" Complete ")]
    [Tooltip("Pop size when completing. The card stays at this size through wobble, spin and hold, then shrinks to 0.")]
    [SerializeField] private float completeScale = 1.3f;
    [SerializeField] private float completeScaleTime = .083f;
    [Tooltip("Z wobble targets in degrees, one per step.")]
    [SerializeField] private float[] wobbleAngles = { 3.7f, -2.56f, .6f, -.3f, 0f };
    [SerializeField] private float[] wobbleTimes = { .1f, .15f, .133f, .233f, .133f };
    [Tooltip("520 ends 160 degrees turned (back showing); 540 ends square on the back.")]
    [SerializeField] private float spinDegrees = 520f;
    [SerializeField] private float spinTime = .517f;
    [SerializeField] private float holdTime = .35f;
    [SerializeField] private float shrinkTime = .3f;
    [Tooltip("Gap after the card disappears before the others slide over.")]
    [SerializeField] private float removeDelay = .3f;
    [SerializeField] private float checkPopScale = 1.45f;
    [SerializeField] private float checkPopTime = .1f;
    [Tooltip("The outline sparkles start this long after the pop begins.")]
    [SerializeField] private float completeSparkleStart = .1f;
    [SerializeField] private float completeSparklesPerSecond = 180f;

    // Not cached in Awake: cards are created under the game panel while it is
    // still inactive, so Awake hasn't run when the deal-in starts.
    private RectTransform rect => (RectTransform)transform;
    private int hitTweenId = -1;
    private int hitSettleTweenId = -1;
    private int slideTweenId = -1;
    // The deal-in: its X move is kept running if the card completes early;
    // scale and spin are cancelled by id.
    private int appearMoveTweenId = -1;
    private int appearSpinTweenId = -1;
    private int appearScaleTweenId = -1;
    private bool isAppearing;
    private bool isCompleting;

    public bool IsCompleting => isCompleting;

    private void Awake()
    {
        if (completeAnimator != null)
            completeAnimator.enabled = false;
    }

    void Update()
    {
        backOfCard.SetActive(Vector3.Dot(transform.forward, Vector3.forward) < 0);
    }

    public void Configure(int initialAmount, Sprite goalIcon)
    {
        amountText.text = initialAmount.ToString();
        iconImage.sprite = goalIcon;
    }

    // Deal in from the left: slide to the slot, one full turn, grow from 0.
    public void PlayAppear(float fromX, float toX, float delay, float duration)
    {
        isAppearing = true;
        transform.localScale = Vector3.zero;
        transform.localRotation = Quaternion.identity;
        SetX(fromX);

        appearMoveTweenId = LeanTween.value(gameObject, fromX, toX, duration).setDelay(delay)
            .setEase(LeanTweenType.easeOutSine)
            .setOnUpdate(SetX).id;
        appearSpinTweenId = LeanTween.value(gameObject, 0f, 360f, duration).setDelay(delay)
            .setOnUpdate((float angle) => transform.localRotation = Quaternion.Euler(0, angle, 0))
            .setOnComplete(() =>
            {
                transform.localRotation = Quaternion.identity;
                isAppearing = false;
            }).id;
        appearScaleTweenId = LeanTween.scale(gameObject, Vector3.one, duration).setDelay(delay)
            .setEase(LeanTweenType.easeInOutSine).id;
    }

    // Slide to another slot (a card to the left completed). The duration
    // follows the distance from where the card is right now, so a card that
    // moves two slots (or is already mid-slide) keeps an even pace.
    public void SlideTo(float x, float secondsPerSlot, float slotStep)
    {
        if (slideTweenId >= 0)
            LeanTween.cancel(gameObject, slideTweenId);

        // A slide replaces a still-running deal-in move.
        if (appearMoveTweenId >= 0)
            LeanTween.cancel(gameObject, appearMoveTweenId);

        float currentX = rect.anchoredPosition.x;
        float slots = Mathf.Abs(currentX - x) / slotStep;

        if (slots < .01f)
        {
            SetX(x);
            return;
        }

        slideTweenId = LeanTween.value(gameObject, currentX, x, slots * secondsPerSlot)
            .setEase(LeanTweenType.easeOutSine)
            .setOnUpdate(SetX).id;
    }

    public void UpdateAmount(int newAmount)
    {
        amountText.text = newAmount.ToString();
        PlayHit();
    }

    // Quick snap up and settle, with a sparkle burst under the card.
    private void PlayHit()
    {
        if (isCompleting)
            return;

        // Only the previous hit is cancelled; appear / slide keep running.
        if (hitTweenId >= 0) LeanTween.cancel(gameObject, hitTweenId);
        if (hitSettleTweenId >= 0) LeanTween.cancel(gameObject, hitSettleTweenId);

        // While dealing in, the appear owns the scale: burst only.
        if (!isAppearing)
        {
            transform.localScale = Vector3.one;
            hitTweenId = LeanTween.scale(gameObject, Vector3.one * hitScale, hitUp)
                .setEase(LeanTweenType.easeOutSine).id;
            hitSettleTweenId = LeanTween.scale(gameObject, Vector3.one, hitDown)
                .setDelay(hitUp)
                .setEase(LeanTweenType.easeInOutSine).id;
        }

        RectTransform parent = rect.parent as RectTransform;
        Vector3 below = transform.TransformPoint(rect.rect.center + new Vector2(0, -hitSparkleOffset));
        UiSparkleEmitter.Burst(parent, parent.InverseTransformPoint(below), sparkleSprite,
            UiSparkleEmitter.Hit, hitSparkleCount);

        // TODO: hit sound + haptic once there is an audio system.
    }

    // Checkmark pops, card pops and wobbles, spins, holds, shrinks away,
    // then onRemoved lets the other cards slide over. From just after the pop
    // the card sheds sparkles from its outline; they keep flowing from where
    // it was for removeDelay after it vanishes, so it dissolves into them.
    public void Complete(Action onRemoved)
    {
        if (isCompleting)
            return;

        isCompleting = true;
        StartCoroutine(CompleteSequence(onRemoved));
    }

    private IEnumerator CompleteSequence(Action onRemoved)
    {
        // Keep the horizontal movement (slide / deal-in move) running; stop the
        // rest by id, since the complete animation drives scale and rotation now.
        CancelTween(appearSpinTweenId);
        CancelTween(appearScaleTweenId);
        CancelTween(hitTweenId);
        CancelTween(hitSettleTweenId);

        // Always pop from a clean state: a cut-off hit or deal-in may have
        // left the card at 1.05-1.18x or turned.
        isAppearing = false;
        transform.localScale = Vector3.one;
        transform.localRotation = Quaternion.identity;

        checkMark.SetActive(true);
        amountText.gameObject.SetActive(false);
        // Drawn on top; positions are ours, so sibling order doesn't affect the layout.
        transform.SetAsLastSibling();

        // TODO: complete sound + haptic once there is an audio system.

        GameObject check = checkMark;
        check.transform.localScale = Vector3.one;
        LeanTween.scale(check, Vector3.one * checkPopScale, checkPopTime).setEase(LeanTweenType.easeOutSine);
        LeanTween.scale(check, Vector3.one, checkPopTime).setDelay(checkPopTime).setEase(LeanTweenType.easeInSine);

        LeanTween.scale(gameObject, Vector3.one * completeScale, completeScaleTime).setEase(LeanTweenType.easeOutSine);

        // Every step is scheduled from t = 0, so frame hiccups can't stretch
        // the sequence. Wobble around Z: each step eases from the previous angle.
        float start = Time.time;
        float at = 0, angle = 0;
        for (int i = 0; i < wobbleAngles.Length && i < wobbleTimes.Length; i++)
        {
            float from = angle, to = wobbleAngles[i];
            LeanTweenType ease = i == 0 ? LeanTweenType.easeOutSine : LeanTweenType.easeInOutSine;
            LeanTween.value(gameObject, from, to, wobbleTimes[i]).setDelay(at).setEase(ease)
                .setOnUpdate((float z) => transform.localRotation = Quaternion.Euler(0, 0, z));
            at += wobbleTimes[i];
            angle = to;
        }

        LeanTween.value(gameObject, 0f, spinDegrees, spinTime).setDelay(at).setEase(LeanTweenType.easeOutSine)
            .setOnUpdate((float y) => transform.localRotation = Quaternion.Euler(0, y, 0));
        at += spinTime + holdTime;

        LeanTween.scale(gameObject, Vector3.zero, shrinkTime).setDelay(at).setEase(LeanTweenType.easeOutSine);
        at += shrinkTime;

        // The emitter lives in the lane, not on the card, so its sparkles stay
        // where they were emitted and outlive the card.
        // Waits are measured from start too, so a hitch can't leave the
        // emitter behind the tweens (card gone, emitter not yet detached).
        yield return WaitUntilTime(start + Mathf.Min(completeSparkleStart, at));

        UiSparkleEmitter sparkles = UiSparkleEmitter.Create((RectTransform)rect.parent, sparkleSprite, UiSparkleEmitter.Outline);
        sparkles.Play(rect, completeSparklesPerSecond);

        yield return WaitUntilTime(start + at);

        // Invisible at scale 0: keep emitting from its full-size outline for
        // the gap, then stop (live sparkles fade out) and leave the layout.
        if (sparkles != null)
            sparkles.Detach();

        yield return WaitUntilTime(start + at + removeDelay);

        if (sparkles != null)
            sparkles.Stop();

        gameObject.SetActive(false);
        onRemoved?.Invoke();
    }

    private static IEnumerator WaitUntilTime(float time)
    {
        while (Time.time < time)
            yield return null;
    }

    private void CancelTween(int id)
    {
        if (id >= 0)
            LeanTween.cancel(gameObject, id);
    }

    private void SetX(float x)
        => rect.anchoredPosition = new Vector2(x, rect.anchoredPosition.y);
}
