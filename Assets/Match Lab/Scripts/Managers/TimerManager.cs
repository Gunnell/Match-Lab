using System;
using NaughtyAttributes;
using TMPro;
using UnityEngine;

public class TimerManager : MonoBehaviour, IGameStateListener
{
    public static TimerManager instance;

    [Header(" Elements ")]
    [SerializeField] private TextMeshProUGUI timerText;

    // Raised when the freeze starts or ends, for the freeze UI.
    public static event Action<bool> frozenChanged;

    // Counts down every frame. Paused: nothing ticks (the freeze gun holds
    // the clock until its shot lands). Frozen: the freeze time runs down
    // instead of the level time.
    private float remaining;
    private bool running;
    private bool paused;
    private bool frozen;
    private float remainingFreeze;
    private float initialFreeze;
    private Action onFreezeComplete;
    private int displayedSeconds = -1;

    public bool IsRunning => running;
    public bool IsFrozen => frozen;
    public float RemainingFreeze => remainingFreeze;
    public float FreezeRatio => initialFreeze > 0 ? Mathf.Clamp01(remainingFreeze / initialFreeze) : 0;

    private void Awake()
    {
        if (instance == null)
            instance = this;
        else
            Destroy(gameObject);

        LevelManager.levelSpawned += OnLevelSpawned;
    }

    private void OnDestroy()
    {
        LevelManager.levelSpawned -= OnLevelSpawned;
    }

    private void OnLevelSpawned(Level level)
    {
        remaining = level.Duration;
        running = true;
        paused = false;
        remainingFreeze = 0;
        initialFreeze = 0;
        SetFrozen(false);
        UpdateTimerText();
    }

    private void Update()
    {
        if (!running || paused)
            return;

        if (!frozen)
        {
            remaining = Mathf.Max(0, remaining - Time.deltaTime);
            UpdateTimerText();

            if (remaining <= 0)
                TimerFinished();
        }
        else
        {
            remainingFreeze = Mathf.Max(0, remainingFreeze - Time.deltaTime);

            if (remainingFreeze <= 0)
            {
                SetFrozen(false);
                Action callback = onFreezeComplete;
                onFreezeComplete = null;
                callback?.Invoke();
            }
        }
    }

    [Button]
    private void TogglePaused()
        => SetPaused(!paused);

    public void SetPaused(bool paused)
        => this.paused = paused;

    [Button]
    private void Freeze10()
        => Freeze(10, null);

    // Using it again mid-freeze stacks: the time is added and the freeze
    // display restarts full.
    public void Freeze(float duration, Action onComplete)
    {
        SetFrozen(true);
        onFreezeComplete = onComplete;
        remainingFreeze += duration;
        initialFreeze = remainingFreeze;
    }

    private void SetFrozen(bool frozen)
    {
        if (this.frozen == frozen)
            return;

        this.frozen = frozen;

        if (!frozen)
        {
            remainingFreeze = 0;
            initialFreeze = 0;
        }

        frozenChanged?.Invoke(frozen);
    }

    // Rounded up, so a 120 s level shows 2:00 for its first second.
    // The text only changes when the shown second does.
    private void UpdateTimerText()
    {
        int seconds = Mathf.CeilToInt(remaining);

        if (seconds == displayedSeconds)
            return;

        displayedSeconds = seconds;
        timerText.text = SecondsToString(seconds);
    }

    private void TimerFinished()
    {
        StopTimer();
        GameManager.instance.SetGameState(EGameState.GAMEOVER);
    }

    public static string SecondsToString(int seconds)
    {
        return TimeSpan.FromSeconds(seconds).ToString().Substring(3);
    }

    public void GameStateChangedCallback(EGameState gameState)
    {
        if (gameState == EGameState.LEVELCOMPLETE || gameState == EGameState.GAMEOVER)
            StopTimer();
    }

    private void StopTimer()
    {
        running = false;
        paused = false;
        onFreezeComplete = null;
        SetFrozen(false);
    }
}
