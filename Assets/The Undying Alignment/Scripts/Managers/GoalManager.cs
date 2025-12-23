using System;
using UnityEngine;

public class GoalManager : MonoBehaviour
{
    
    [Header(" Data ")]
    private ItemLevelData[] goals;
    
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
            break;
            
        }
    }

    private void CompleteGoal(int goalIdx)
    {
        Debug.Log("Goal Complete: " + goals[goalIdx].itemPrefab.ItemName);
        CheckForLevelComplete();
    }

    private void CheckForLevelComplete()
    {
        int i;
        for(i = 0; i < goals.Length; i++)
            if (goals[i].amount > 0)
                return;

        Debug.Log("Level Complete");


    }
}
