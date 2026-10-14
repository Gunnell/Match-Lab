using UnityEngine;
using System;
using System.Collections.Generic;

public class ItemSpotsManager : MonoBehaviour, IGameStateListener
{
    public static ItemSpotsManager instance;
     
    [Header (" Elements ")]
    [Tooltip("Hosts the rack slots; its item containers' spots are used left to right.")]
    [SerializeField] private GameplayBottomPanel bottomPanel;
    private ItemSpot[] spots;
    private ItemContainer[] containers;

    [Header(" Rack Fit ")]
    // Each item type gets its own rack size: one that fits the box shows at
    // rackBase of its board size, a bigger one is shrunk until it fits
    // rackBase x the box. The box follows the real slot width.
    [Tooltip("Fit box (width, height) for a slot art width of fitBoxSlotWidth.")]
    [SerializeField] private Vector2 fitBox = new Vector2(1.5f, 2.25f);
    [Tooltip("Slot art width the fit box is sized for. The box is scaled by the real slot width / this.")]
    [SerializeField] private float fitBoxSlotWidth = 1.28f;
    [Tooltip("Rack size relative to the board size, for items that fit the box.")]
    [SerializeField] private float rackBase = .65f;
    [Tooltip("Pose of rack items relative to the camera (X right, Y up, Z away from the camera), whatever the slot's own tilt. "
        + "Tipped toward the camera and turned a little, so they read as 3D rather than flat icons.")]
    [SerializeField] private Vector3 rackItemRotation = new Vector3(-16.6f, -13f, 3.8f);
    // Merge, compaction or release in progress.
    private bool isBusy;
    private readonly HashSet<Item> movingItems = new HashSet<Item>();
    private readonly List<Item> pendingTaps = new List<Item>();
    private bool compactionPending;
    public bool IsBusy => isBusy || movingItems.Count > 0 || pendingTaps.Count > 0;
    // Board flights, so a pushed item can be re-aimed mid-air.
    private readonly Dictionary<Item, RackTarget> flightTargets = new Dictionary<Item, RackTarget>();

    private class RackTarget
    {
        public Transform parent;
        public Vector3 seat;
        public float depth;
    }
    // A full rack doesn't end the level while a booster is about to free it.
    private bool gameOverHeld;

    [Header(" Data ")]
    private Dictionary<EItemName, ItemMergeData> itemMergeDataDictionary = new Dictionary<EItemName, ItemMergeData>();
    
    [Header(" Board To Rack Flight ")]
    // Board items fly along an arc (quadratic Bezier) at constant speed, the
    // duration following the path length.
    [Tooltip("Arc control point height above the start (world +Y, mostly toward the camera), so the item looms up.")]
    [SerializeField] private float arcLift = 9.5f;
    [Tooltip("Control point pulled away from the rack by this fraction of the depth distance: a slight wind-up.")]
    [SerializeField] private float arcBackPull = .25f;
    [SerializeField] private float flightSecondsPerUnit = .0325f;
    [SerializeField] private float minFlightDuration = .25f;
    [SerializeField] private float maxFlightDuration = .6f;
    [SerializeField] private int arcSamples = 25;
    [Tooltip("Swell at the start of the flight, relative to the item's rack size.")]
    [SerializeField] private float pulseFactor = 1.42f;
    [Tooltip("Part of the flight spent swelling; the rest shrinks into the slot.")]
    [SerializeField] private float pulseGrowPortion = .23f;

    [Header(" Rack Slides ")]
    [Tooltip("Slides inside the rack. A one-slot shift (1.29 units) takes ~0.11s including the same-row x1.5.")]
    [SerializeField] private float slideSecondsPerUnit = .055f;
    [SerializeField] private float sameRowSlideMultiplier = 1.5f;
    [Tooltip("Hop over each slot passed, up on screen.")]
    [SerializeField] private float slideHopHeight = .25f;

    [Header(" Rack Full ")]
    [SerializeField] private float fullHopSideForce = 300f;
    [SerializeField] private Vector2 fullHopUpForce = new Vector2(250, 350);

    [Header(" Landing ")]
    [Tooltip("Bounce strength per unit of depth the item travelled (capped at 1). Weaker than 0.15 = no bounce.")]
    [SerializeField] private float bounceStrengthPerUnit = .2f;

    [Header(" Last Space Warning ")]
    // With 6 items and no match coming, the empty last slot pulses red.
    [SerializeField] private Color warningColor = new Color(1f, 0f, .047f);
    [Tooltip("Half a pulse (0 to full or back), seconds.")]
    [SerializeField] private float warningHalfPeriod = .5f;
    [SerializeField] private float warningStopFade = .25f;
    private bool isMergeRunning;

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
    
    private void OnItemClicked(Item item)
    {
        if (item.Spot != null || pendingTaps.Contains(item))
            return;

        // Rack full.
        if (FreeSpotCount() - pendingTaps.Count <= 0)
        {
            HopInPlace(item);
            return;
        }

        if (isBusy || pendingTaps.Count > 0 || !CanPlaceNow(item))
        {
            pendingTaps.Add(item);
            return;
        }

        Place(item);
    }

    private void Place(Item item)
    {
        item.StoreBoardPose();

        itemPickedUp?.Invoke(item);

        HandleItemClicked(item);

        // The item now holds a slot, so the rack count changed.
        UpdateLastSpaceWarning();
    }

    private bool CanPlaceNow(Item item)
    {
        if (!itemMergeDataDictionary.ContainsKey(item.ItemName))
            return true;

        if (itemMergeDataDictionary[item.ItemName].CanMergeItems())
            return false;

        ItemSpot idealSpot = GetIdealSpot(item);
        if (idealSpot == null)
            return false;

        if (idealSpot.IsEmpty())
            return true;

        // Flying items can be re-aimed, sliding ones can't.
        for (int i = IndexOf(idealSpot); i < spots.Length; i++)
        {
            Item other = spots[i].Item;
            if (other != null && movingItems.Contains(other) && !flightTargets.ContainsKey(other))
                return false;
        }

        return true;
    }

    private void HopInPlace(Item item)
    {
        Rigidbody rb = item.GetComponent<Rigidbody>();
        if (rb == null || rb.isKinematic)
            return;

        float angle = UnityEngine.Random.Range(0, 360) * Mathf.Deg2Rad;
        float up = UnityEngine.Random.Range((int)fullHopUpForce.x, (int)fullHopUpForce.y);
        rb.AddForce(new Vector3(Mathf.Cos(angle) * fullHopSideForce, up, Mathf.Sin(angle) * fullHopSideForce), ForceMode.Force);
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
            itemSpots.Sort((a, b) => IndexOf(b).CompareTo(IndexOf(a)));
        }

        int idealSpotIndex = IndexOf(itemSpots[0]) + 1;

        return idealSpotIndex < spots.Length ? spots[idealSpotIndex] : null;
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

        int spotIndex = IndexOf(idealSpot);

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

            if (flightTargets.TryGetValue(itemOnTheSpot, out RackTarget flight))
            {
                targetSpot.Populate(itemOnTheSpot);
                flight.parent = itemOnTheSpot.transform.parent;
                flight.seat = GetSeatLocalPosition(itemOnTheSpot, targetSpot, GetRackLocalRotation(itemOnTheSpot, targetSpot));
                targetSpot.SetLandingStrength(flight.depth * bounceStrengthPerUnit, true);
                continue;
            }

            MoveItemToSpot(itemOnTheSpot, targetSpot, () => HandleItemReachedSpot(itemOnTheSpot, false));

        }

        MoveItemToSpot(itemToPlace, idealSpot, () => HandleItemReachedSpot(itemToPlace));
    }
        

    private void HandleItemReachedSpot(Item item, bool checkForMerge = true)
    {
        item.Spot.BumpDown();
    }

    private void AfterArrival()
    {
        if (isBusy)
        {
            if (compactionPending && movingItems.Count == 0)
            {
                compactionPending = false;
                MoveAllItemsToLeft(HandleAllItemsMovedToTheLeft);
            }
            return;
        }

        if (TryStartMerge())
            return;

        ProcessPendingTaps();
        Settle();
    }

    private bool TryStartMerge()
    {
        foreach (ItemMergeData data in itemMergeDataDictionary.Values)
        {
            if (!data.CanMergeItems())
                continue;

            bool landed = true;
            foreach (Item groupItem in data.items)
                if (movingItems.Contains(groupItem))
                    landed = false;

            if (!landed)
                continue;

            MergeItems(data);
            return true;
        }

        return false;
    }

    private void ProcessPendingTaps()
    {
        while (pendingTaps.Count > 0 && !isBusy)
        {
            Item item = pendingTaps[0];

            bool valid = item != null && item.Spot == null && item.IsPhysicsEnabled && !item.IsBeingThrown
                && GameManager.instance.IsGame();

            if (!valid)
            {
                pendingTaps.RemoveAt(0);
                continue;
            }

            if (!IsFreeSpotAvailable())
            {
                pendingTaps.RemoveAt(0);
                HopInPlace(item);
                continue;
            }

            if (!CanPlaceNow(item))
                break;

            pendingTaps.RemoveAt(0);
            Place(item);
        }
    }

    private void Settle()
    {
        if (IsBusy)
            return;

        if (GetFreeSpot() == null && !gameOverHeld && GameManager.instance.IsGame())
        {
            GameManager.instance.SetGameState(EGameState.GAMEOVER);
            return;
        }

        UpdateLastSpaceWarning();
    }

    private void MergeItems(ItemMergeData itemMergeData)
    {
        List<Item> items = itemMergeData.items;
        itemMergeDataDictionary.Remove(itemMergeData.itemName);

        // A match is happening: no warning.
        isBusy = true;
        isMergeRunning = true;
        UpdateLastSpaceWarning();

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
        isMergeRunning = false;

        // Compact once in-flight items have landed.
        if (movingItems.Count > 0)
        {
            compactionPending = true;
            return;
        }

        MoveAllItemsToLeft(HandleAllItemsMovedToTheLeft);
    }

    private void HandleAllItemsMovedToTheLeft()
    {
        SetNotBusy();
    }

    private void SetNotBusy()
    {
        isBusy = false;

        if (TryStartMerge())
            return;

        ProcessPendingTaps();
        Settle();
    }

    // Pulse the empty last slot when the rack holds 6 items and no match is
    // coming. Starts only once the rack has settled; stops as soon as the
    // condition no longer holds.
    private void UpdateLastSpaceWarning()
    {
        if (spots == null || spots.Length == 0)
            return;

        ItemSpot lastSpot = spots[spots.Length - 1];
        int occupied = 0;
        for (int i = 0; i < spots.Length; i++)
            if (!spots[i].IsEmpty())
                occupied++;

        bool danger = occupied == spots.Length - 1 && !IsMatchPending() && GameManager.instance.IsGame();

        if (!danger)
            lastSpot.StopWarning(warningStopFade);
        else if (!IsBusy && !lastSpot.IsWarning)
            lastSpot.StartWarning(warningColor, warningHalfPeriod);
    }

    // Used by the Spring before it takes an item.
    public void StopLastSpaceWarning()
    {
        if (spots != null && spots.Length > 0)
            spots[spots.Length - 1].StopWarning(warningStopFade);
    }

    // A group of 3 about to merge, or a merge still animating.
    private bool IsMatchPending()
    {
        if (isMergeRunning)
            return true;

        foreach (ItemMergeData data in itemMergeDataDictionary.Values)
            if (data.CanMergeItems())
                return true;

        return false;
    }

    public void GameStateChangedCallback(EGameState gameState)
    {
        if (gameState == EGameState.LEVELCOMPLETE || gameState == EGameState.GAMEOVER)
            StopLastSpaceWarning();
    }




    private void MoveItemToSpot(Item item, ItemSpot targetSpot, Action completeCallback)
    {
        // Before Populate: rack-to-rack moves already have a spot.
        bool fromBoard = item.Spot == null;
        Vector3 start = item.transform.position;

        targetSpot.Populate(item);
        item.DisableShadows();
        item.DisablePhysics();

        // Board world size x the rack multiplier, as a local scale under the spot.
        Vector3 rackScale = item.BaseScale * (GetBoardParentScale() * GetRackMultiplier(item) / targetSpot.ItemParentScale);
        Quaternion rackRotation = GetRackLocalRotation(item, targetSpot);
        Vector3 seat = GetSeatLocalPosition(item, targetSpot, rackRotation);

        Vector3 target = item.transform.parent.TransformPoint(seat);
        float depth = Mathf.Abs(target.z - start.z);
        targetSpot.SetLandingStrength(depth * bounceStrengthPerUnit, fromBoard);

        movingItems.Add(item);

        RackTarget rackTarget = new RackTarget { parent = item.transform.parent, seat = seat, depth = depth };
        if (fromBoard)
            flightTargets[item] = rackTarget;

        Action onArrived = () =>
        {
            flightTargets.Remove(item);
            item.transform.localPosition = rackTarget.seat;
            item.transform.localRotation = rackRotation;
            item.transform.localScale = rackScale;
            movingItems.Remove(item);
            completeCallback?.Invoke();
            AfterArrival();
        };

        if (fromBoard)
            FlyToSpot(item, start, rackTarget, rackRotation, rackScale, depth, onArrived);
        else
            SlideToSpot(item, start, seat, rackRotation, rackScale, onArrived);
    }

    // rackItemRotation (camera-relative) as a local rotation under the spot's
    // item parent. The slot container faces the camera.
    private Quaternion GetRackLocalRotation(Item item, ItemSpot spot)
    {
        Quaternion cameraFacing = containers[IndexOf(spot)].transform.rotation;
        return Quaternion.Inverse(item.transform.parent.rotation) * cameraFacing * Quaternion.Euler(rackItemRotation);
    }

    // Rack size relative to the board size: rackBase when the item fits the
    // fit box, smaller for an item too wide or too tall for it.
    private float GetRackMultiplier(Item item)
    {
        Vector3 size = item.BoardBounds.size * GetBoardParentScale();
        float boxScale = GetSlotWidth() / fitBoxSlotWidth;
        float boxW = fitBox.x * boxScale, boxH = fitBox.y * boxScale;

        float mult = rackBase;
        if (size.x / boxW > size.y / boxH)
        {
            if (size.x > boxW)
                mult = rackBase / (size.x / boxW);     // width-dominant, too wide
        }
        else if (size.y > boxH)
            mult = rackBase / (size.y / boxH);         // height-dominant, too tall

        return mult;
    }

    // World scale of the level's item parent (the board is scaled to the screen).
    private static float GetBoardParentScale()
    {
        Transform board = LevelManager.instance != null ? LevelManager.instance.ItemParent : null;
        return board != null ? board.lossyScale.x : 1f;
    }

    // World width of the slot art.
    private float GetSlotWidth()
    {
        Renderer art = spots.Length > 0 ? spots[0].SlotRenderer : null;
        return art != null ? art.bounds.size.x : fitBoxSlotWidth;
    }

    // The item's centre sits half its rack height up the screen from the
    // slot's item point. Local to the spot's item parent, at rest.
    private Vector3 GetSeatLocalPosition(Item item, ItemSpot spot, Quaternion rackRotation)
    {
        // Board-local units to world units in the rack.
        float toRack = GetBoardParentScale() * GetRackMultiplier(item);
        Bounds bounds = item.BoardBounds;
        ItemContainer container = containers[IndexOf(spot)];

        Vector3 centre = container.GetSeatPosition(bounds.size.y * toRack * .5f);
        // Move the item's pivot so its bounds centre (at the rack pose) lands on the seat.
        Vector3 pivotToCentre = item.transform.parent.rotation * rackRotation * (bounds.center * toRack);

        return spot.RestLocalPoint(centre - pivotToCentre);
    }

    // Arc from the board: lifts toward the camera, constant speed, upright
    // early, swells then shrinks into the slot.
    private void FlyToSpot(Item item, Vector3 start, RackTarget rackTarget, Quaternion rackRotation, Vector3 rackScale, float depth, Action onArrived)
    {
        Vector3 target = rackTarget.parent.TransformPoint(rackTarget.seat);
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
                item.transform.position = QuadraticBezier(start, ctrl, rackTarget.parent.TransformPoint(rackTarget.seat), t))
            .setOnComplete(onArrived);

        RotateLocal(item, rackRotation, duration, LeanTweenType.easeOutQuint);

        float grow = duration * pulseGrowPortion;
        LeanTween.scale(item.gameObject, rackScale * pulseFactor, grow)
            .setEase(LeanTweenType.easeInOutSine);
        LeanTween.scale(item.gameObject, rackScale, duration - grow)
            .setDelay(grow)
            .setEase(LeanTweenType.easeInOutSine);
    }

    // Inside the rack: linear sideways, a small hop over every slot passed.
    private void SlideToSpot(Item item, Vector3 start, Vector3 seat, Quaternion rackRotation, Vector3 rackScale, Action onArrived)
    {
        Transform parent = item.transform.parent;
        Vector3 target = parent.TransformPoint(seat);
        float distance = Vector3.Distance(start, target);

        // Always the same row in this rack.
        float duration = Mathf.Max(.05f, distance * slideSecondsPerUnit * sameRowSlideMultiplier);
        int slotsPassed = Mathf.Max(1, Mathf.RoundToInt(distance / spotSpacing));

        Vector3 hopDirection = CameraService.ScreenUp;

        // Slots strictly between start and target glint as the item passes over them.
        List<ItemSpot> passed = new List<ItemSpot>();
        float minX = Mathf.Min(start.x, target.x), maxX = Mathf.Max(start.x, target.x);
        for (int i = 0; i < spots.Length; i++)
        {
            float x = spots[i].transform.position.x;
            if (x > minX + spotSpacing * .25f && x < maxX - spotSpacing * .25f)
                passed.Add(spots[i]);
        }

        LeanTween.value(item.gameObject, 0f, 1f, duration)
            .setOnUpdate((float t) =>
            {
                Vector3 position = Vector3.Lerp(start, parent.TransformPoint(seat), t);

                for (int i = passed.Count - 1; i >= 0; i--)
                {
                    float spotX = passed[i].transform.position.x;
                    if ((target.x > start.x && position.x >= spotX) || (target.x < start.x && position.x <= spotX))
                    {
                        passed[i].PlayPassShine();
                        passed.RemoveAt(i);
                    }
                }

                float hop = t * slotsPassed;
                float u = hop - Mathf.Floor(hop);
                float height = u < .5f
                    ? Mathf.Sin(u * 2 * Mathf.PI * .5f)                 // easeOutSine up
                    : Mathf.Cos((u - .5f) * 2 * Mathf.PI * .5f);        // easeInSine down
                if (t >= 1) height = 0;

                item.transform.position = position + hopDirection * (height * slideHopHeight);
            })
            .setOnComplete(onArrived);

        RotateLocal(item, rackRotation, duration, LeanTweenType.linear);
        LeanTween.scale(item.gameObject, rackScale, duration);
    }

    // Shortest-path rotation to a local pose (an Euler tween could spin the long way round).
    private static void RotateLocal(Item item, Quaternion target, float duration, LeanTweenType ease)
    {
        Quaternion from = item.transform.localRotation;
        LeanTween.value(item.gameObject, 0f, 1f, duration)
            .setEase(ease)
            .setOnUpdate((float t) => item.transform.localRotation = Quaternion.SlerpUnclamped(from, target, t));
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
    }

    // Released: a rack that is still full now ends the level.
    public void HoldGameOver(bool hold)
    {
        gameOverHeld = hold;

        if (!hold && !IsBusy && GetFreeSpot() == null && GameManager.instance.IsGame())
            GameManager.instance.SetGameState(EGameState.GAMEOVER);
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
        containers = bottomPanel.ItemContainers;
        spots = new ItemSpot[containers.Length];

        for(int i = 0; i < containers.Length; i++)
            spots[i] = containers[i].Spot;

        if (spots.Length >= 2)
            spotSpacing = Vector3.Distance(spots[0].transform.position, spots[1].transform.position);
    }

    // Slot position in the rack, 0 = left-most.
    private int IndexOf(ItemSpot spot) => Array.IndexOf(spots, spot);

    private int FreeSpotCount()
    {
        int free = 0;
        for (int i = 0; i < spots.Length; i++)
            if (spots[i].IsEmpty())
                free++;
        return free;
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
        if (IsBusy)
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
        UpdateLastSpaceWarning();

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
        if (IsBusy)
            return false;

        for (int i = 0; i < items.Count; i++)
        {
            RemoveItemFromMergeData(items[i]);
            items[i].Spot.Clear();
            items[i].UnassignSpot();
        }

        isBusy = true;
        UpdateLastSpaceWarning();
        MoveAllItemsToLeft(SetNotBusy);

        return true;
    }

    // The items stay in their spots but no longer match with new ones.
    public void ExcludeFromMatching(List<Item> items)
    {
        for (int i = 0; i < items.Count; i++)
            RemoveItemFromMergeData(items[i]);
    }

    public void IncludeInMatching(List<Item> items)
    {
        for (int i = 0; i < items.Count; i++)
        {
            Item item = items[i];
            if (item == null || item.Spot == null)
                continue;

            if (!itemMergeDataDictionary.ContainsKey(item.ItemName))
                CreateItemMergeData(item);
            else if (!itemMergeDataDictionary[item.ItemName].items.Contains(item))
                itemMergeDataDictionary[item.ItemName].Add(item);
        }
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
