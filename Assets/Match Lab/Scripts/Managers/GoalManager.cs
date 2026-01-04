using System;
using System.Collections.Generic;
using UnityEngine;

public class GoalManager : MonoBehaviour
{
    [Header(" Elements ")] 
    [SerializeField] private Transform goalCardParent;
    [SerializeField] private GoalCard goalCardPrefab;
    
    [Header(" Data ")]
    private ItemLevelData[] goals;
    private List<GoalCard>  goalCards = new List<GoalCard>();
    
    private void Awake()
    {
        LevelManager.levelSpawned += OnLevelSpawned;
        ItemSpotsManager.itemPickedUp += OnItemPickedUp;
    }

    void Start()
    {
    }
    void Update()
    {}


    private void OnDestroy()
    {
        LevelManager.levelSpawned -= OnLevelSpawned;
        ItemSpotsManager.itemPickedUp -= OnItemPickedUp;
        
    }
    

    private void OnLevelSpawned(Level level)
    {
        goals = level.GetGoals();
        GenerateGoalCards();
    }

    private void GenerateGoalCards()
    {
        for (int i = 0; i < goals.Length; i++)
            GenerateGoalCard(goals[i]);
        

    }

    private void GenerateGoalCard(ItemLevelData goal)
    {
        GoalCard  goalCard = Instantiate(goalCardPrefab, goalCardParent);
        goalCard.Configure(goal.amount, goal.itemPrefab.Icon);
        goalCards.Add(goalCard);
    }

    private void OnItemPickedUp(Item item)
    {
        for (int i = 0; i < goals.Length; i++)
        {
            if (!goals[i].itemPrefab.ItemName.Equals(item.ItemName))
                continue;
            
            goals[i].amount--;

            if (goals[i].amount <= 0)
                CompleteGoal(i);
            else
                goalCards[i].UpdateAmount(goals[i].amount);
            break;
            
        }
    }

    private void CompleteGoal(int goalIdx)
    {
        Debug.Log("Goal Complete: " + goals[goalIdx].itemPrefab.ItemName);
        goalCards[goalIdx].Complete(); 
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
