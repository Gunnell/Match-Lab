using UnityEngine;
using System;
using System.Collections.Generic;
using Random = UnityEngine.Random;

public class ItemSpotsManager : MonoBehaviour
{
    public static ItemSpotsManager instance;
     
    [Header (" Elements ")]
    [SerializeField] private Transform itemSpotsParent;
    private ItemSpot[] spots;

    [Header(" Settings ")]
    [SerializeField] private Vector3 itemLocalPositionOnSpot;
    [SerializeField] private Vector3 itemLocalScaleOnSpot;
    private bool isBusy;

    [Header(" Data ")]
    private Dictionary<EItemName, ItemMergeData> itemMergeDataDictionary = new Dictionary<EItemName, ItemMergeData>();
    
    [Header(" Animation Settings")]
    [SerializeField] private float animationDuration;
    [SerializeField] private LeanTweenType animationEasing;

    [Header(" Actions ")]
    public static Action<List<Item>> mergeStarted;
    public static Action<Item> itemPickedUp;


    private void Awake()
    {
        if (instance == null)
            instance = this;
        else
            Destroy(gameObject);
        
        InputManager.itemClicked += OnItemClicked;
        MergeManager.mergeCompleted += OnMergeCompleted;
        StoreSpots();
    }

    private void OnDestroy()
    {
        InputManager.itemClicked -= OnItemClicked;
        MergeManager.mergeCompleted -= OnMergeCompleted;
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
        
        itemPickedUp?.Invoke(item);

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

        MoveItemToSpot(item, idealSpot, () => HandleItemReachedSpot(item));
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


            MoveItemToSpot(itemOnTheSpot, targetSpot, () => HandleItemReachedSpot(itemOnTheSpot, false));

        }

        MoveItemToSpot(itemToPlace, idealSpot, () => HandleItemReachedSpot(itemToPlace));
    }
        

    private void HandleItemReachedSpot(Item item, bool checkForMerge = true)
    {
        item.Spot.BumpDown();
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
            items[i].Spot.Clear();

        // The spots are free immediately, but the merged items are still
        // animating. Wait for MergeManager to finish before compacting,
        // otherwise the survivors slide left through the merge animation.
        mergeStarted?.Invoke(items);
    }

    private void MoveAllItemsToLeft(Action completeCallback)
    {
        List<Item> itemsToMove = new List<Item>();
        List<ItemSpot> targetSpots = new List<ItemSpot>();

        int targetIndex = 0;

        for(int i = 0; i < spots.Length; i++)
        {
            ItemSpot spot = spots[i];

            if (spot.IsEmpty())
                continue;

            if (i != targetIndex)
            {
                itemsToMove.Add(spot.Item);
                targetSpots.Add(spots[targetIndex]);
                spot.Clear();
            }

            targetIndex++;
        }

        if(itemsToMove.Count <= 0)
        {
            completeCallback?.Invoke();
            return;
        }

        for(int i = 0; i < itemsToMove.Count; i++)
        {
            Item itemToMove = itemsToMove[i];

            Action callback = () => HandleItemReachedSpot(itemToMove, false);

            if (i == itemsToMove.Count - 1)
                callback += completeCallback;

            MoveItemToSpot(itemToMove, targetSpots[i], callback);
        }
        
    }

    private void OnMergeCompleted()
    {
        if (itemMergeDataDictionary.Count <= 0)
            isBusy = false;
        else
            MoveAllItemsToLeft(HandleAllItemsMovedToTheLeft);
    }

    private void HandleAllItemsMovedToTheLeft()
    {
        isBusy = false;
    }




    private void MoveItemToSpot(Item item, ItemSpot targetSpot, Action completeCallback)
    {
        targetSpot.Populate(item);
        
        // item.transform.localPosition = itemLocalPositionOnSpot;
        // item.transform.localScale = itemLocalScaleOnSpot;
        // item.transform.localRotation = Quaternion.identity;
        
        LeanTween.moveLocal(item.gameObject, itemLocalPositionOnSpot, animationDuration)
            .setEase(animationEasing);
        LeanTween.scale(item.gameObject, itemLocalScaleOnSpot, animationDuration)
            .setEase(animationEasing);
        LeanTween.rotateLocal(item.gameObject, Vector3.zero, animationDuration)
            .setOnComplete(completeCallback);
        
        item.DisableShadows();
        item.DisablePhysics();
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

        MoveItemToSpot(item, targetSpot, () => HandleFirstItemReachedSpot(item));
        
    }

    private void CreateItemMergeData(Item item)
    {
        itemMergeDataDictionary.Add(item.ItemName, new ItemMergeData(item));
        Debug.Log(item.name + " has been added to dict");
        
    }

    private void HandleFirstItemReachedSpot(Item item)
    {
        item.Spot.BumpDown();
        CheckForGameover();
    }

    private void CheckForGameover()
    {
        if(GetFreeSpot() == null)
            GameManager.instance.SetGameState(EGameState.GAMEOVER);
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

    public Item ReleaseRandomItem(Action completeCallback)
    {
        if (isBusy)
        {
            Debug.LogWarning("ItemSpotsManager is busy!");
            return null;
        }

        ItemSpot spot = GetRandomOccupiedSpot();

        if (spot == null)
            return null;

        isBusy = true;

        Item item = spot.Item;

        RemoveItemFromMergeData(item);

        spot.Clear();
        item.UnassignSpot();

        MoveAllItemsToLeft(() =>
        {
            isBusy = false;
            completeCallback?.Invoke();
        });

        return item;
    }

    private void RemoveItemFromMergeData(Item item)
    {
        if (!itemMergeDataDictionary.ContainsKey(item.ItemName))
            return;

        List<Item> items = itemMergeDataDictionary[item.ItemName].items;
        items.Remove(item);

        if (items.Count <= 0)
            itemMergeDataDictionary.Remove(item.ItemName);
    }

    public ItemSpot GetRandomOccupiedSpot()
    {
        List<ItemSpot> occupiedSpots = new List<ItemSpot>();
        for (int i = 0; i < spots.Length; i++)
        {
            if (!spots[i].IsEmpty())
            {
                occupiedSpots.Add(spots[i]); 
            }
        }

        if (occupiedSpots.Count <= 0)
            return null;
        
        return occupiedSpots[Random.Range(0, occupiedSpots.Count)];

    }
}

