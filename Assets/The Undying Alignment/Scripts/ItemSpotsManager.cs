using UnityEngine;
using System;
using System.Collections.Generic;

public class ItemSpotsManager : MonoBehaviour
{
    [Header (" Elements ")]
    [SerializeField] private Transform itemSpotsParent;
    private ItemSpot[] spots;

    [Header (" Settings ")]
    [SerializeField] private Vector3 itemLocalPositionOnSpot;
    [SerializeField] private Vector3 itemLocalScaleOnSpot;
    private bool isBusy;

    [Header (" Data ")]
    private Dictionary<EItemName, ItemMergeData> itemMergeDataDictionary = new Dictionary<EItemName, ItemMergeData>();



    private void Awake()
    {
        InputManager.itemClicked += OnItemClicked;
        StoreSpots();
    }

    private void OnDestroy()
    {
        InputManager.itemClicked -= OnItemClicked;
    }




    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    private void OnItemClicked(Item item){

        if (isBusy)
        {
            Debug.LogWarning("ItemSpotsManager is busy!");
            return;
        }

        if(!IsFreeSpotAvailable()){
            Debug.LogWarning("No free item spot available!");
            return;
        }

        isBusy = true;

        HandleItemClicked(item);
            

    }  

    private void HandleItemClicked(Item item)
    {
        if (itemMergeDataDictionary.ContainsKey(item.ItemName))
            HandleItemMergeDataFound(item);
        else
            MoveItemToFirstFreeSpot(item);
    } 

    private void HandleItemMergeDataFound(Item item)
    {
        ItemSpot idealSpot = GetIdealSpot(item);

        // if (idealSpot)
        // {
        //     pass;
        // }

        itemMergeDataDictionary[item.ItemName].Add(item);
        MoveItemToIdealSpot(item, idealSpot);
    }

    private ItemSpot GetIdealSpot(Item item)
    {
        List<Item> items = itemMergeDataDictionary[item.ItemName].items;
        List<ItemSpot> itemSpots = new List<ItemSpot>();

        for(int i = 0; i < items.Count; i++)
            itemSpots.Add(items[i].Spot);
        
        if(itemSpots.Count >= 2)
        {
            itemSpots.Sort((a, b) => b.transform.GetSiblingIndex().CompareTo(a.transform.GetSiblingIndex()));
        }

        int idealSpotIndex = itemSpots[0].transform.GetSiblingIndex() + 1;

        return spots[idealSpotIndex];
    }

    private void MoveItemToIdealSpot(Item item, ItemSpot idealSpot)
    {
        if(!idealSpot.IsEmpty())
        {
            HandleIdealSpotFull(item, idealSpot);
            return;
        }

        MoveItemToSpot(item, idealSpot);
    }

    private void HandleIdealSpotFull(Item item, ItemSpot idealSpot)
    {
        MoveItemsToRightFrom(idealSpot, item);

        
    }

    private void MoveItemsToRightFrom(ItemSpot idealSpot, Item itemToPlace)
    {

        int spotIndex = idealSpot.transform.GetSiblingIndex();

        for (int i =  spots.Length - 2; i >= spotIndex; i--)
        {
            ItemSpot spot = spots[i];

            if(spot.IsEmpty())
                continue;

            Item itemOnTheSpot = spot.Item; 

            spot.Clear();

            ItemSpot targetSpot = spots[i+1];

            if (!targetSpot.IsEmpty())
            {
                Debug.LogError("ERROR, targetSpot spot should not be empty");
                isBusy = false;
                return;
            }


            MoveItemToSpot(itemOnTheSpot, targetSpot, false);

        }

        MoveItemToSpot(itemToPlace, idealSpot);
    }
        

    private void HandleItemReachedSpot(Item item, bool checkForMerge = true)
    {

        if(!checkForMerge)
            return;

        if (itemMergeDataDictionary[item.ItemName].CanMergeItems())
            MergeItems(itemMergeDataDictionary[item.ItemName]);
        else
            CheckForGameover();
    }

    private void MergeItems(ItemMergeData itemMergeData)
    {
        List<Item> items = itemMergeData.items;
        itemMergeDataDictionary.Remove(itemMergeData.itemName);

        for(int i = 0; i < items.Count; i++)
        {
            items[i].Spot.Clear();
            Destroy(items[i].gameObject);
        }

        isBusy = false; //TODO it will be deleted
            

    }




    private void MoveItemToSpot(Item item, ItemSpot targetSpot, bool checkForMerge = true)
    {
        targetSpot.Populate(item);
        
        item.transform.localPosition = itemLocalPositionOnSpot;
        item.transform.localScale = itemLocalScaleOnSpot;
        item.transform.localRotation = Quaternion.identity;
        item.DisableShadows();
        item.DisablePhysics();

        HandleItemReachedSpot(item, checkForMerge);
    }

    private void MoveItemToFirstFreeSpot(Item item)
    {
        ItemSpot targetSpot = GetFreeSpot();

        if(targetSpot == null)
        {
            Debug.LogError("Target spot can not be null");
            return;
        }

        CreateItemMergeData(item);
        targetSpot.Populate(item);


        item.transform.localPosition = itemLocalPositionOnSpot;
        item.transform.localScale = itemLocalScaleOnSpot;
        item.transform.localRotation = Quaternion.identity;
        item.DisableShadows();
        item.DisablePhysics();

        HandleFirstItemReachedSpot(item);

    }

    private void CreateItemMergeData(Item item)
    {
        itemMergeDataDictionary.Add(item.ItemName, new ItemMergeData(item));
        Debug.Log(item.name + " has been added to dict");
        
    }

    private void HandleFirstItemReachedSpot(Item item)
    {
        CheckForGameover();
    }

    private void CheckForGameover()
    {
        if(GetFreeSpot() == null)
            Debug.LogWarning("Gameover !!!!");
        else
            isBusy = false;
    }

    private ItemSpot GetFreeSpot()
    {
        for(int i = 0; i < spots.Length; i++)
        {
            if(spots[i].IsEmpty())
                return spots[i];
            
        };

        return null;
        
    }

    private void StoreSpots()
    {
        spots = new ItemSpot[itemSpotsParent.childCount];

        for(int i = 0; i < itemSpotsParent.childCount; i++)
            spots[i] = itemSpotsParent.GetChild(i).GetComponent<ItemSpot>();
    }

    private bool IsFreeSpotAvailable()
    {
        for(int i = 0; i < spots.Length; i++)
        {
            if(spots[i].IsEmpty())
                return true;
        }
        return false;
        
    }
}

