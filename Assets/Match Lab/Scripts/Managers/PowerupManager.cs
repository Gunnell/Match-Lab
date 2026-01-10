using System;
using System.Collections.Generic;
using NaughtyAttributes;
using NUnit.Framework;
using UnityEngine;

public class PowerUpManager : MonoBehaviour
{
    [Header(" Actions ")] 
    public static Action<Item> itemPickedUp;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    [Button]
    private void VacuumPowerup()
    {
         // Collect 3 target/ goal items from the board 
         // Grab items
         // Grab the goal items
         // Grab the goal that has the greatest amount
         // Grab 3 items
         Item[] items = LevelManager.instance.Items;
         ItemLevelData[] goals = GoalManager.instance.Goals;
         ItemLevelData? greatestGoal = GetGreatestGoal(goals);

         if (greatestGoal == null)
             return;
         
         ItemLevelData goal = (ItemLevelData)greatestGoal;
         
         List<Item> itemsToCollect = new List<Item>();
         for(int i = 0; i < items.Length; i++)
         {
             if(items[i].ItemName == goal.itemPrefab.ItemName)
             {
                 itemsToCollect.Add(items[i]);
                 
                 if (itemsToCollect.Count >= 3)
                     break; 
             }
         }

         for (int i = itemsToCollect.Count-1; i >= 0; i--)
         {
             itemPickedUp?.Invoke(itemsToCollect[i]);
             Destroy(itemsToCollect[i].gameObject);
         }


    }

    public ItemLevelData? GetGreatestGoal(ItemLevelData[] goals) 
    {
        int max = 0;
        int goalIdx = -1;

        for (int i = 0; i < goals.Length; i++)
        {
            if (goals[i].amount >= max)
            {
                max = goals[i].amount;
                goalIdx = i;
            }
        }
        
        if(goalIdx <= -1)
            return null; 
        
        return goals[goalIdx]; 
    }

}
