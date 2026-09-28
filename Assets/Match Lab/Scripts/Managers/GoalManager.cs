using System;
using System.Collections.Generic;
using UnityEngine;

public class GoalManager : MonoBehaviour
{
    public static GoalManager instance;
    [Header(" Elements ")] 
    [SerializeField] private Transform goalCardParent;
    [SerializeField] private GoalCard goalCardPrefab;

    [Header(" Card Layout ")]
    // Cards are positioned in code, left-filled, so a completed card can
    // leave and the rest slide over (a layout group would snap instantly).
    [SerializeField] private float cardSpacing = 20f;
    [Tooltip("-16 keeps the row where it was on screen: the card visuals were re-centred 16px right on the card's pivot.")]
    [SerializeField] private float paddingLeft = -16f;
    [Tooltip("Delay between cards dealing in at level start.")]
    [SerializeField] private float appearStagger = .133f;
    [SerializeField] private float appearDuration = .433f;
    [Tooltip("Slide time per slot moved when a card leaves.")]
    [SerializeField] private float slideSecondsPerSlot = .147f;
    [SerializeField] private int maxGoals = 6;
    
    [Header(" Data ")]
    private ItemLevelData[] goals;
    // Goal index order; goalCards[i] always matches goals[i].
    private List<GoalCard>  goalCards = new List<GoalCard>();
    // Display order, left to right; completed cards leave it.
    private readonly List<GoalCard> activeCards = new List<GoalCard>();

    public ItemLevelData[] Goals => goals;
    
    private void Awake()
    {
        if (instance == null)
            instance = this;
        else
            Destroy(gameObject);
        
        LevelManager.levelSpawned += OnLevelSpawned;
        ItemSpotsManager.itemPickedUp += OnItemPickedUp;
        PowerUpManager.itemPickedUp += OnItemPickedUp;
        PowerUpManager.itemBackToGame += OnItemBackToGame;

        // Positions are driven here; the layout group would fight them.
        UnityEngine.UI.HorizontalLayoutGroup layout = goalCardParent.GetComponent<UnityEngine.UI.HorizontalLayoutGroup>();
        if (layout != null)
            layout.enabled = false;
    }


    private void OnDestroy()
    {
        LevelManager.levelSpawned -= OnLevelSpawned;
        ItemSpotsManager.itemPickedUp -= OnItemPickedUp;
        PowerUpManager.itemPickedUp -= OnItemPickedUp;
        PowerUpManager.itemBackToGame -= OnItemBackToGame;
    }
    

    private void OnLevelSpawned(Level level)
    {
        goals = level.GetGoals();
        GenerateGoalCards();
    }

    private void GenerateGoalCards()
    {
        if (goals.Length > maxGoals)
            Debug.LogWarning("Level has " + goals.Length + " goals; the goal row is designed for at most " + maxGoals + ".");

        float offScreenX = GetOffScreenLeftX();

        for (int i = 0; i < goals.Length; i++)
        {
            GoalCard card = GenerateGoalCard(goals[i]);
            card.PlayAppear(offScreenX, SlotX(i), i * appearStagger, appearDuration);
        }
    }

    private GoalCard GenerateGoalCard(ItemLevelData goal)
    {
        GoalCard  goalCard = Instantiate(goalCardPrefab, goalCardParent);
        goalCard.Configure(goal.amount, goal.itemPrefab.Icon);

        // Same place the layout group used: top-left aligned, full lane height.
        RectTransform lane = (RectTransform)goalCardParent;
        RectTransform rect = (RectTransform)goalCard.transform;
        rect.anchorMin = rect.anchorMax = new Vector2(0, 1);
        rect.sizeDelta = new Vector2(CardWidth, lane.rect.height);
        rect.anchoredPosition = new Vector2(rect.anchoredPosition.x, -lane.rect.height * (1 - rect.pivot.y));

        goalCards.Add(goalCard);
        activeCards.Add(goalCard);
        return goalCard;
    }

    private float CardWidth => ((RectTransform)goalCardPrefab.transform).sizeDelta.x;

    // Card centre x for a display slot, relative to the lane's left edge.
    private float SlotX(int slot)
    {
        float width = CardWidth;
        return paddingLeft + slot * (width + cardSpacing) + width * ((RectTransform)goalCardPrefab.transform).pivot.x;
    }

    // Left of the screen by at least one card width, in lane anchor space.
    private float GetOffScreenLeftX()
    {
        RectTransform lane = (RectTransform)goalCardParent;
        RectTransform canvas = (RectTransform)lane.GetComponentInParent<Canvas>().rootCanvas.transform;
        Vector3[] corners = new Vector3[4];
        canvas.GetWorldCorners(corners);
        float screenLeft = lane.InverseTransformPoint(corners[0]).x - lane.rect.xMin;
        return screenLeft - CardWidth * 1.5f;
    }

    // A completed card has left: the cards after it slide into the gap.
    private void OnCardRemoved(GoalCard card)
    {
        int index = activeCards.IndexOf(card);
        if (index < 0)
            return;

        activeCards.RemoveAt(index);

        for (int i = index; i < activeCards.Count; i++)
            activeCards[i].SlideTo(SlotX(i), slideSecondsPerSlot, CardWidth + cardSpacing);
    }

    private void OnItemPickedUp(Item item)
    {
        for (int i = 0; i < goals.Length; i++)
        {
            if (!goals[i].itemPrefab.ItemName.Equals(item.ItemName))
                continue;
            
            // Already complete: an extra pickup must not re-trigger completion.
            if (goals[i].amount <= 0)
                break;

            goals[i].amount--;

            if (goals[i].amount <= 0)
                CompleteGoal(i);
            else
                goalCards[i].UpdateAmount(goals[i].amount);
            break;

        }
    }

    // Spring puts an item back on the board, so it has to count again.
    private void OnItemBackToGame(Item item)
    {
        for (int i = 0; i < goals.Length; i++)
        {
            if (!goals[i].itemPrefab.ItemName.Equals(item.ItemName))
                continue;

            if (goals[i].amount <= 0)
                break;

            goals[i].amount++;
            goalCards[i].UpdateAmount(goals[i].amount);
            break;
        }
    }

    private void CompleteGoal(int goalIdx)
    {
        Debug.Log("Goal Complete: " + goals[goalIdx].itemPrefab.ItemName);
        GoalCard card = goalCards[goalIdx];
        card.Complete(() => OnCardRemoved(card));
        CheckForLevelComplete();
    }

    private void CheckForLevelComplete()
    {
        int i;
        for(i = 0; i < goals.Length; i++)
            if (goals[i].amount > 0)
                return;
        GameManager.instance.SetGameState(EGameState.LEVELCOMPLETE);

    }
}
