using UnityEngine;
using System;
using System.Collections.Generic;

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
    public bool IsBusy => isBusy;

    [Header(" Data ")]
    private Dictionary<EItemName, ItemMergeData> itemMergeDataDictionary = new Dictionary<EItemName, ItemMergeData>();
    
    [Header(" Board To Rack Flight ")]
    // Board items fly along an arc (quadratic Bezier) at constant speed, the
    // duration following the path length.
    [Tooltip("Arc control point height above the start. World +Y is toward the camera, so the item looms up.")]
    [SerializeField] private float arcLift = 4.75f;
    [Tooltip("Control point pulled away from the rack by this fraction of the depth distance: a slight wind-up.")]
    [SerializeField] private float arcBackPull = .25f;
    [SerializeField] private float flightSecondsPerUnit = .065f;
    [SerializeField] private float minFlightDuration = .25f;
    [SerializeField] private float maxFlightDuration = .6f;
    [SerializeField] private int arcSamples = 25;
    [Tooltip("Swell at the start of the flight, relative to the item's size when it leaves the board.")]
    [SerializeField] private float pulseFactor = 1.15f;
    [Tooltip("Part of the flight spent swelling; the rest shrinks into the slot.")]
    [SerializeField] private float pulseGrowPortion = .23f;

    [Header(" Rack Slides ")]
    [Tooltip("Slides inside the rack. A one-slot shift (0.474 units) takes ~0.14s including the same-row x1.5.")]
    [SerializeField] private float slideSecondsPerUnit = .2f;
    [SerializeField] private float sameRowSlideMultiplier = 1.5f;
    [Tooltip("Hop over each slot passed, in world +Z (up on screen).")]
    [SerializeField] private float slideHopHeight = .12f;

    [Header(" Landing ")]
    [Tooltip("Bounce strength per unit of depth the item travelled (capped at 1). Weaker than 0.15 = no bounce.")]
    [SerializeField] private float bounceStrengthPerUnit = .2f;

    // One tap is buffered while the rack is animating, so fast taps aren't
    // dropped. It is processed (and only then counted) once the rack is free.
    private Item bufferedItem;
    private float spotSpacing = .5f;

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
            // The latest tap replaces an older one.
            bufferedItem = item;
            return;
        }

        if(!IsFreeSpotAvailable()){
            Debug.LogWarning("No free item spot available!");
            return;
        }

        isBusy = true;
        item.StoreBoardPose();
        
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
                SetNotBusy();
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
            SetNotBusy();
        else
            MoveAllItemsToLeft(HandleAllItemsMovedToTheLeft);
    }

    private void HandleAllItemsMovedToTheLeft()
    {
        SetNotBusy();
    }

    // The single place the rack becomes free. Fires a buffered tap if it is
    // still a valid pick (not vacuumed, re-picked or mid-flight; level running).
    private void SetNotBusy()
    {
        isBusy = false;

        Item item = bufferedItem;
        bufferedItem = null;

        if (item == null || item.Spot != null || !item.IsPhysicsEnabled || item.IsBeingThrown)
            return;

        if (!GameManager.instance.IsGame())
            return;

        OnItemClicked(item);
    }




    private void MoveItemToSpot(Item item, ItemSpot targetSpot, Action completeCallback)
    {
        // Before Populate: rack-to-rack moves already have a spot.
        bool fromBoard = item.Spot == null;
        Vector3 start = item.transform.position;

        targetSpot.Populate(item);
        item.DisableShadows();
        item.DisablePhysics();

        Vector3 target = item.transform.parent.TransformPoint(itemLocalPositionOnSpot);
        float depth = Mathf.Abs(target.z - start.z);
        targetSpot.SetLandingStrength(depth * bounceStrengthPerUnit);

        Action onArrived = () =>
        {
            item.transform.localPosition = itemLocalPositionOnSpot;
            item.transform.localRotation = Quaternion.identity;
            item.transform.localScale = itemLocalScaleOnSpot;
            completeCallback?.Invoke();
        };

        if (fromBoard)
            FlyToSpot(item, start, depth, onArrived);
        else
            SlideToSpot(item, start, onArrived);
    }

    // Arc from the board: lifts toward the camera, constant speed, upright
    // early, swells then shrinks into the slot.
    private void FlyToSpot(Item item, Vector3 start, float depth, Action onArrived)
    {
        Transform parent = item.transform.parent;
        Vector3 target = parent.TransformPoint(itemLocalPositionOnSpot);
        Vector3 ctrl = new Vector3((start.x + target.x) * .5f, start.y + arcLift, start.z + depth * arcBackPull);

        // Duration from the arc length.
        int samples = Mathf.Max(2, arcSamples);
        float length = 0;
        Vector3 previous = start;
        for (int i = 1; i < samples; i++)
        {
            Vector3 point = QuadraticBezier(start, ctrl, target, i / (float)(samples - 1));
            length += Vector3.Distance(previous, point);
            previous = point;
        }

        float duration = Mathf.Clamp(length * flightSecondsPerUnit, minFlightDuration, maxFlightDuration);

        // Linear parameter (even speed along the arc); the rack target is re-read in
        // case the rack moves.
        LeanTween.value(item.gameObject, 0f, 1f, duration)
            .setOnUpdate((float t) =>
                item.transform.position = QuadraticBezier(start, ctrl, parent.TransformPoint(itemLocalPositionOnSpot), t))
            .setOnComplete(onArrived);

        LeanTween.rotateLocal(item.gameObject, Vector3.zero, duration)
            .setEase(LeanTweenType.easeOutQuint);

        float grow = duration * pulseGrowPortion;
        LeanTween.scale(item.gameObject, item.transform.localScale * pulseFactor, grow)
            .setEase(LeanTweenType.easeInOutSine);
        LeanTween.scale(item.gameObject, itemLocalScaleOnSpot, duration - grow)
            .setDelay(grow)
            .setEase(LeanTweenType.easeInOutSine);
    }

    // Inside the rack: linear sideways, a small hop over every slot passed.
    private void SlideToSpot(Item item, Vector3 start, Action onArrived)
    {
        Transform parent = item.transform.parent;
        Vector3 target = parent.TransformPoint(itemLocalPositionOnSpot);
        float distance = Vector3.Distance(start, target);

        // Always the same row in this rack.
        float duration = Mathf.Max(.05f, distance * slideSecondsPerUnit * sameRowSlideMultiplier);
        int slotsPassed = Mathf.Max(1, Mathf.RoundToInt(distance / spotSpacing));

        LeanTween.value(item.gameObject, 0f, 1f, duration)
            .setOnUpdate((float t) =>
            {
                Vector3 position = Vector3.Lerp(start, parent.TransformPoint(itemLocalPositionOnSpot), t);

                float hop = t * slotsPassed;
                float u = hop - Mathf.Floor(hop);
                float height = u < .5f
                    ? Mathf.Sin(u * 2 * Mathf.PI * .5f)                 // easeOutSine up
                    : Mathf.Cos((u - .5f) * 2 * Mathf.PI * .5f);        // easeInSine down
                if (t >= 1) height = 0;

                item.transform.position = position + Vector3.forward * (height * slideHopHeight);
            })
            .setOnComplete(onArrived);

        LeanTween.rotateLocal(item.gameObject, Vector3.zero, duration);
        LeanTween.scale(item.gameObject, itemLocalScaleOnSpot, duration);
    }

    private static Vector3 QuadraticBezier(Vector3 p0, Vector3 p1, Vector3 p2, float t)
    {
        float u = 1 - t;
        return u * u * p0 + 2 * u * t * p1 + t * t * p2;
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
            SetNotBusy();
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

        if (spots.Length >= 2)
            spotSpacing = Vector3.Distance(spots[0].transform.position, spots[1].transform.position);
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

    public Item ReleaseLastItemOnRack()
    {
        if (isBusy)
        {
            Debug.LogWarning("ItemSpotsManager is busy!");
            return null;
        }

        ItemSpot spot = GetLastOccupiedSpot();

        if (spot == null)
            return null;

        isBusy = true;

        Item item = spot.Item;

        RemoveItemFromMergeData(item);

        item.Spot.Clear();
        item.UnassignSpot();

        MoveAllItemsToLeft(SetNotBusy);

        return item;
    }

    // Rack items from the right-most slot to the left-most.
    public IEnumerable<Item> RackItemsRightToLeft()
    {
        for (int i = spots.Length - 1; i >= 0; i--)
        {
            if (!spots[i].IsEmpty())
                yield return spots[i].Item;
        }
    }

    // Takes the given rack items out (e.g. for the Vacuum) and compacts the
    // rack. Returns false while the rack is busy; the caller retries later.
    // No itemPickedUp: these items were counted when they entered the rack.
    public bool TryReleaseItems(List<Item> items)
    {
        if (isBusy)
            return false;

        for (int i = 0; i < items.Count; i++)
        {
            RemoveItemFromMergeData(items[i]);
            items[i].Spot.Clear();
            items[i].UnassignSpot();
        }

        isBusy = true;
        MoveAllItemsToLeft(SetNotBusy);

        return true;
    }

    // The rack is always compacted to the left, so this is the rightmost item.
    private ItemSpot GetLastOccupiedSpot()
    {
        for (int i = spots.Length - 1; i >= 0; i--)
        {
            if (!spots[i].IsEmpty())
                return spots[i];
        }

        return null;
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
}
