using System;
using System.Collections.Generic;
using NaughtyAttributes;
using NUnit.Framework;
using UnityEngine;

public class PowerUpManager : MonoBehaviour
{
    [Header(" Vacuum Elements")]
    [SerializeField] private Vacuum vacuum;
    [SerializeField] private Transform vacuumEndPosition;
    [Header(" Actions ")] 
    public static Action<Item> itemPickedUp;

    [Header(" Settings ")] 
    private bool isBusy;
    private int vacuumItemsToCollect;
    private int vacuumCounter;
    
    [Header(" Data ")]
    [SerializeField] private int initialPUCount;
    private int vacuumPUCount;

    private void Awake()
    {
        LoadData();
        Vacuum.started += OnVacuumStarted;
        InputManager.powerupClicked += OnPowerupClicked;
    }

    private void OnDestroy()
    {
        Vacuum.started -= OnVacuumStarted;
        InputManager.powerupClicked -= OnPowerupClicked;

    }

    private void OnPowerupClicked(Powerup powerup)
    {
        if (isBusy) return;

        switch (powerup.Type)
        {
            case EPowerupType.Vacuum:
                HandleVacuumClicked();
                UpdateVacuumVisuals();
                break;
        }

    }

    private void HandleVacuumClicked()
    {
        
        if (vacuumPUCount <= 0)
        {
            vacuumPUCount = 3;
            SaveData();
        }
        else
        {
           // isBusy = true;
            vacuumPUCount--; 
            SaveData();
            vacuum.Play();
        } 
    }

    private void OnVacuumStarted()
    {
        if (isBusy) return; //g
        VacuumPowerup();
    }

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
         
         isBusy = true; //g
         vacuumCounter = 0;
         
         List<Item> itemsToCollect = new List<Item>();
         for(int i = 0; i < items.Length; i++)
         {
             if(items[i] == null)
                 continue;
             if(items[i].ItemName == goal.itemPrefab.ItemName)
             {
                 itemsToCollect.Add(items[i]);
                 
                 if (itemsToCollect.Count >= 3)
                     break; 
             }
         }
         
         vacuumItemsToCollect = itemsToCollect.Count;


         for (int i = 0; i < itemsToCollect.Count; i++)
         {
             itemsToCollect[i].DisablePhysics();
             
             Item itemToCollect = itemsToCollect[i];
             
             LeanTween.move(itemsToCollect[i].gameObject, vacuumEndPosition.position, .5f)
                 .setEase(LeanTweenType.easeInCubic)
                 .setOnComplete(() => ItemReachedVacuum(itemToCollect));
             
             LeanTween.scale(itemsToCollect[i].gameObject, Vector3.zero, .5f);
         }

         for (int i = itemsToCollect.Count-1; i >= 0; i--)
         {
             itemPickedUp?.Invoke(itemsToCollect[i]);
             //Destroy(itemsToCollect[i].gameObject);
         }


    }

    private void ItemReachedVacuum(Item item)
    {
         vacuumCounter++;
         if (vacuumCounter >= vacuumItemsToCollect)
             isBusy = false;
         Destroy(item.gameObject);
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
    private void UpdateVacuumVisuals()
    {
        vacuum.UpdateVisuals(vacuumPUCount);
    }
    private void LoadData()
    {
        vacuumPUCount = PlayerPrefs.GetInt("VacuumPUCount", initialPUCount);
        UpdateVacuumVisuals();
    }
    
    private void SaveData()
    {
        PlayerPrefs.SetInt("VacuumPUCount", vacuumPUCount);
    }

}
