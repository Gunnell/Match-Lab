using System;
using System.Collections.Generic;
using NaughtyAttributes;
using UnityEngine;

public class PowerUpManager : MonoBehaviour
{
    [Header(" Vacuum Elements")]
    [SerializeField] private Vacuum vacuum;
    [SerializeField] private Transform vacuumEndPosition;

    [Header(" Spring Elements ")]
    [SerializeField] private Spring spring;
    // A little slower than the rack add animation so the return reads clearly.
    [SerializeField] private float springReturnDuration = .3f;
    [SerializeField] private LeanTweenType springReturnEasing = LeanTweenType.easeInOutCubic;
    [Header(" Actions ")] 
    public static Action<Item> itemPickedUp;
    public static Action<Item> itemBackToGame;

    [Header(" Settings ")] 
    private bool isBusy;
    private int vacuumItemsToCollect;
    private int vacuumCounter;
    
    [Header(" Data ")]
    [SerializeField] private int initialPUCount;
    private int vacuumPUCount;
    private int springPUCount;

    private void Awake()
    {
        Vacuum.started += OnVacuumStarted;
        InputManager.powerupClicked += OnPowerupClicked;
        LoadData();
    }

    private void OnDestroy()
    {
        Vacuum.started -= OnVacuumStarted;
        InputManager.powerupClicked -= OnPowerupClicked;

    }

    private void OnPowerupClicked(Powerup powerup)
    {
        if (isBusy || powerup == null) return;

        switch (powerup.Type)
        {
            case EPowerupType.Vacuum:
                HandleVacuumClicked();
                UpdateVacuumVisuals();
                break;

            case EPowerupType.Spring:
                HandleSpringClicked();
                UpdateSpringVisuals();
                break;
        }

    }

    private void HandleVacuumClicked()
    {
        if (vacuumPUCount <= 0)
        {
            // TODO: rewarded video / coins. Free refill placeholder for now.
            vacuumPUCount = 3;
            SaveData();
            return;
        }

        // Don't spend a charge when there is nothing to collect.
        if (GetVacuumTargets().Count <= 0)
            return;

        // Busy from the click, not from the animation event, so tapping
        // again while the vacuum animation plays can't spend a second charge.
        isBusy = true;
        vacuumPUCount--;
        SaveData();
        vacuum.Play();
    }

    private void OnVacuumStarted()
    {
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
    #region Vacuum Powerup
    [Button]
    private void VacuumPowerup()
    {
         // The board may have changed between the click and the animation
         // event, so the targets are looked up again here.
         List<Item> itemsToCollect = GetVacuumTargets();

         if (itemsToCollect.Count <= 0)
         {
             isBusy = false;
             return;
         }

         isBusy = true;
         vacuumCounter = 0;
         vacuumItemsToCollect = itemsToCollect.Count;


         for (int i = 0; i < itemsToCollect.Count; i++)
         {
             itemsToCollect[i].DisablePhysics();
             
             Item itemToCollect = itemsToCollect[i];
             List<Vector3> points = new List<Vector3>();
             
             points.Add(itemsToCollect[i].transform.position);
             points.Add(itemsToCollect[i].transform.position);
             
             points.Add(itemsToCollect[i].transform.position + Vector3.up * 2);
             points.Add(vacuumEndPosition.position + Vector3.up * 2);
             
             points.Add(vacuumEndPosition.position);
             points.Add(vacuumEndPosition.position);




             LeanTween.moveSpline(itemsToCollect[i].gameObject, points.ToArray(), .8f)
                 .setOnComplete(() => ItemReachedVacuum(itemToCollect));
             
             
            /* LeanTween.move(itemsToCollect[i].gameObject, vacuumEndPosition.position, .5f)
                 .setEase(LeanTweenType.easeInCubic)
                 .setOnComplete(() => ItemReachedVacuum(itemToCollect)); */
             
             LeanTween.scale(itemsToCollect[i].gameObject, Vector3.zero, .8f);
         }

         for (int i = itemsToCollect.Count-1; i >= 0; i--)
         {
             itemPickedUp?.Invoke(itemsToCollect[i]);
             //Destroy(itemsToCollect[i].gameObject);
         }


    }

    // Up to 3 board items of the goal with the greatest remaining amount.
    private List<Item> GetVacuumTargets()
    {
        List<Item> targets = new List<Item>();

        ItemLevelData? greatestGoal = GetGreatestGoal(GoalManager.instance.Goals);

        if (greatestGoal == null)
            return targets;

        EItemName goalName = greatestGoal.Value.itemPrefab.ItemName;
        Item[] items = LevelManager.instance.Items;

        for (int i = 0; i < items.Length; i++)
        {
            if (items[i] == null)
                continue;
            if (items[i].Spot != null)
                continue;
            if (items[i].ItemName != goalName)
                continue;

            targets.Add(items[i]);

            if (targets.Count >= 3)
                break;
        }

        return targets;
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
            if (goals[i].amount <= 0)
                continue;

            if (goals[i].amount > max)
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
        if (vacuum == null)
            return;

        vacuum.UpdateVisuals(vacuumPUCount);
    }
    #endregion
    
    #region Spring Powerup

    private void HandleSpringClicked()
    {
        if (springPUCount <= 0)
        {
            // TODO: rewarded video. Same free refill placeholder as the vacuum.
            springPUCount = 3;
            SaveData();
            return;
        }

        // Only spend a charge if an item actually came off the rack, otherwise
        // a click on a full-but-busy board would eat one for nothing.
        if (!SpringPowerup())
            return;

        springPUCount--;
        SaveData();
    }

    [Button]
    public bool SpringPowerup()
    {
        if (isBusy) return false;

        isBusy = true;

        Item itemToRelease = ItemSpotsManager.instance.ReleaseLastPlacedItem();

        if (itemToRelease == null)
        {
            isBusy = false;
            return false;
        }

        // Not itemPickedUp: the goal count goes back up instead.
        itemBackToGame?.Invoke(itemToRelease);

        ReturnItemToBoard(itemToRelease);

        return true;
    }

    // Plays the rack's add animation in reverse: the item retraces its path
    // back to where it was picked, then physics takes over again.
    private void ReturnItemToBoard(Item item)
    {
        item.transform.parent = LevelManager.instance.ItemParent;
        LeanTween.cancel(item.gameObject);

        LeanTween.move(item.gameObject, item.BoardPosition, springReturnDuration)
            .setEase(springReturnEasing);
        LeanTween.rotate(item.gameObject, item.BoardRotation.eulerAngles, springReturnDuration)
            .setEase(springReturnEasing);
        LeanTween.scale(item.gameObject, item.BaseScale, springReturnDuration)
            .setEase(springReturnEasing)
            .setOnComplete(() =>
            {
                item.EnablePhysics();
                item.EnableShadows();
                isBusy = false;
            });
    }

    private void UpdateSpringVisuals()
    {
        if (spring == null)
            return;

        spring.UpdateVisuals(springPUCount);
    }

    #endregion
    
    private void LoadData()
    {
        vacuumPUCount = PlayerPrefs.GetInt("VacuumPUCount", initialPUCount);
        springPUCount = PlayerPrefs.GetInt("SpringPUCount", initialPUCount);
        UpdateVacuumVisuals();
        UpdateSpringVisuals();
    }
    
    private void SaveData()
    {
        PlayerPrefs.SetInt("VacuumPUCount", vacuumPUCount);
        PlayerPrefs.SetInt("SpringPUCount", springPUCount);
    }

}
