using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// While the timer is frozen, swaps the time for the freeze countdown, shows
/// a snowflake and a draining freeze bar, and tints the timer icy blue.
/// </summary>
public class FreezeTimerView : MonoBehaviour
{
    [Header(" Elements ")]
    [SerializeField] private TextMeshProUGUI timerText;
    [SerializeField] private TextMeshProUGUI freezeTimerText;
    [Tooltip("Optional. Filled image draining from full to empty over the freeze.")]
    [SerializeField] private Image freezeFill;
    [SerializeField] private GameObject snowflake;
    [SerializeField] private Image containerImage;

    [Header(" Settings ")]
    [SerializeField] private Color frozenTint = new Color(.35f, .75f, 1f, .75f);

    private Color normalTint;
    private bool frozen;
    private int displayedSeconds = -1;

    private void Awake()
    {
        if (containerImage != null)
            normalTint = containerImage.color;

        TimerManager.frozenChanged += OnFrozenChanged;
        OnFrozenChanged(false);
    }

    private void OnDestroy()
    {
        TimerManager.frozenChanged -= OnFrozenChanged;
    }

    private void OnFrozenChanged(bool frozen)
    {
        this.frozen = frozen;
        displayedSeconds = -1;

        timerText.gameObject.SetActive(!frozen);
        freezeTimerText.gameObject.SetActive(frozen);

        if (freezeFill != null)
            freezeFill.gameObject.SetActive(frozen);

        if (snowflake != null)
            snowflake.SetActive(frozen);

        if (containerImage != null)
            containerImage.color = frozen ? frozenTint : normalTint;

        if (frozen)
            Refresh();
    }

    private void Update()
    {
        if (frozen)
            Refresh();
    }

    private void Refresh()
    {
        TimerManager timer = TimerManager.instance;
        if (timer == null)
            return;

        if (freezeFill != null)
            freezeFill.fillAmount = timer.FreezeRatio;

        int seconds = Mathf.CeilToInt(timer.RemainingFreeze);
        if (seconds == displayedSeconds)
            return;

        displayedSeconds = seconds;
        freezeTimerText.text = TimerManager.SecondsToString(seconds);
    }
}
