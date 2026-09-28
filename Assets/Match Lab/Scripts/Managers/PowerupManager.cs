using System;
using System.Collections.Generic;
using NaughtyAttributes;
using UnityEngine;

public class PowerUpManager : MonoBehaviour, IGameStateListener
{
    [Header(" Vacuum Elements")]
    [SerializeField] private Vacuum vacuum;
    [SerializeField] private Transform vacuumEndPosition;
    [Tooltip("How long to wait for a busy rack before re-planning, then giving up with a refund.")]
    [SerializeField] private float vacuumRackWaitTimeout = 2f;
    // Flight: each item follows a cubic Bezier (start, ctrl, approach, nozzle)
    // at constant speed, easing in so it speeds up into the nozzle.
    [Tooltip("The approach point sits this far from the nozzle toward the board, so items come in from the board side.")]
    [SerializeField] private float vacuumApproachDistance = 1.1f;
    [Tooltip("Board items: control point moves toward the nozzle in x by at most this much.")]
    [SerializeField] private float vacuumBoardCtrlMaxX = 2.1f;
    [Tooltip("Board items: control point this high above the top of the pile.")]
    [SerializeField] private float vacuumBoardCtrlLift = .6f;
    [Tooltip("Board items: control point this far into the board from the start.")]
    [SerializeField] private float vacuumBoardCtrlZ = 1.25f;
    [Tooltip("Rack items: control point moves toward the nozzle in x by at most this much.")]
    [SerializeField] private float vacuumRackCtrlMaxX = 1.6f;
    [Tooltip("Rack items: control point this far into the board from the start (kept at rack height).")]
    [SerializeField] private float vacuumRackCtrlZ = 2.7f;
    [Tooltip("Seconds per metre of path, board items.")]
    [SerializeField] private float vacuumBoardSecondsPerMetre = .09f;
    [Tooltip("Seconds per metre of path, rack items.")]
    [SerializeField] private float vacuumRackSecondsPerMetre = .12f;
    [SerializeField] private float vacuumStagger = .35f;
    [SerializeField] private int vacuumPathSamples = 25;
    [SerializeField] private float vacuumSwellScale = 1.42f;
    [SerializeField] private float vacuumArrivalScale = .45f;

    [Header(" Spring Elements ")]
    [SerializeField] private Spring spring;
    // The released item vanishes from the rack, then is launched from the
    // Spring button onto the board with physics.
    [Tooltip("Divides the launch delay only.")]
    [SerializeField] private float springSpeedMultiplier = 1f;
    [Tooltip("Where the item appears and is launched from. Falls back to the Spring button.")]
    [SerializeField] private Transform springLaunchPoint;
    [Tooltip("Aim point for the throw. Falls back to straight ahead (+z).")]
    [SerializeField] private Transform springBoardCenter;
    [Tooltip("Board wall between the button and the board; the thrown item passes through it until it is inside the board.")]
    [SerializeField] private Collider springThrowCollider;
    [SerializeField] private float springLaunchDelay = .265f;
    [Tooltip("Rack size to full size after launch. Not affected by the speed multiplier.")]
    [SerializeField] private float springScaleDuration = .65f;
    [SerializeField] private float springSideRandom = .3f;
    [SerializeField] private float springSideMultiplier = 2f;
    [Tooltip("Launch this far to either side of the board centre and the throw is nudged back toward the middle.")]
    [SerializeField] private float springEdgeDistance = 1f;
    [SerializeField] private float springEdgeBonus = .12f;
    [SerializeField] private float springSideForce = 275f;
    [SerializeField] private float springUpForce = 525f;
    [SerializeField] private float springUpMultiplier = 1.05f;
    [SerializeField] private float springForwardForce = 275f;
    [SerializeField] private float springForwardMultiplier = 2.15f;
    [SerializeField] private Vector2 springTorqueRange = new Vector2(40, 120);
    [Tooltip("Scales the launch forces to this world, like fanForceScale. The Fan's .28 is too weak here: "
        + "the button sits behind the board's front wall, and at .28 the item lands inside that wall. "
        + ".75 carries it well into the board (lands between the front third and the middle, depending on the pile).")]
    [SerializeField] private float springForceScale = .75f;
    [Tooltip("Extra downward pull after launch so the item lands quickly.")]
    [SerializeField] private float springExtraFall = 1500f;
    [SerializeField] private float springFallMultiplier = 1f;
    [SerializeField] private float springFallDuration = 1.5f;
    [Tooltip("Added to linear damping every frame for springDampingDuration, so the item settles instead of skidding.")]
    [SerializeField] private float springDampingPerFrame = .045f;
    [SerializeField] private float springDampingDuration = 1f;
    [Tooltip("Stop ignoring the throw collider after this long even if the item never entered the board.")]
    [SerializeField] private float springBoardEnterFallback = 1.5f;
    // Effects covering the cut between the item vanishing from the rack and
    // appearing at the Spring. Timings are divided by springSpeedMultiplier.
    [SerializeField] private Color springSlotFlashColor = new Color(.455f, 0f, .576f);
    [SerializeField] private float springSlotFlashIn = .133f;
    [SerializeField] private float springSlotFlashOut = .133f;
    [Tooltip("Burst where the item vanished. Should destroy itself when done.")]
    [SerializeField] private ParticleSystem springSlotBurstPrefab;
    [Tooltip("Burst position relative to the slot (world; +Y toward the camera, +Z up on screen).")]
    [SerializeField] private Vector3 springSlotBurstOffset = new Vector3(0, .28f, .18f);

    [Header(" Fan Elements ")]
    [SerializeField] private Fan fan;
    [Tooltip("Divides every step except the end time.")]
    [SerializeField] private float fanSpeedMultiplier = 1f;
    [SerializeField] private float fanWindStartTime = .33f;
    [SerializeField] private float fanThrowTime = .4f;
    [SerializeField] private float fanWindStopTime = .91f;
    [Tooltip("Item collisions and taps come back. Not affected by the speed multiplier.")]
    [SerializeField] private float fanEndTime = 1.2f;
    [SerializeField] private Vector2 fanThrowUpForce = new Vector2(650, 750);
    [SerializeField] private float fanThrowSideForce = 300;
    [SerializeField] private Vector2 fanThrowTorque = new Vector2(20, 50);
    [Tooltip("Scales the throw forces to this world. At 1 an item (mass 1) is thrown ~10 m up, "
        + "past the camera; .28 lands it inside the 0.8s window before collisions return.")]
    [SerializeField] private float fanForceScale = .28f;

    [Header(" Freeze Gun Elements ")]
    [SerializeField] private FreezeGun freezeGun;
    [Tooltip("What the gun fires at the timer. A placeholder sphere is made if left empty.")]
    [SerializeField] private GameObject freezeProjectilePrefab;
    [Tooltip("The timer the shot flies to (Timer Container).")]
    [SerializeField] private RectTransform freezeTarget;
    [SerializeField] private float freezeDuration = 10f;
    [Tooltip("Tap to shot. The timer is paused meanwhile.")]
    [SerializeField] private float freezeFireDelay = .8f;
    [Tooltip("Divides the fire delay.")]
    [SerializeField] private float freezeSpeedMultiplier = 1f;
    [Tooltip("Constant speed flight: seconds per unit of distance.")]
    [SerializeField] private float freezeProjectileSecondsPerUnit = .035f;
    [Tooltip("The shot ends this far in front of the camera, under the timer on screen, so it flies over the pile instead of under it.")]
    [SerializeField] private float freezeTargetDepth = 4f;
    [SerializeField] private float freezeProjectileRecycleDelay = .5f;
    [SerializeField] private FreezeOverlay freezeOverlay;
    [Tooltip("Burst played where the shot hits the timer.")]
    [SerializeField] private ParticleSystem freezeHitParticles;
    [Header(" Actions ")] 
    public static Action<Item> itemPickedUp;
    public static Action<Item> itemBackToGame;

    [Header(" Settings ")] 
    // Each powerup has its own busy flag so using one never blocks the other.
    private bool isVacuumBusy;
    private bool isSpringBusy;
    private bool isFanBusy;
    // Only between tap and shot; a new use is allowed once the gun has fired, even mid-freeze.
    private bool isFreezeBusy;
    // Shots tapped but not landed yet: the timer stays paused while any are pending.
    private int freezeShotsPending;
    private readonly List<GameObject> freezeProjectiles = new List<GameObject>();
    private readonly List<Coroutine> freezeRoutines = new List<Coroutine>();
    // Spring tapped while the rack was animating; fired once the rack is free.
    private bool springQueued;
    private int vacuumItemsToCollect;
    private int vacuumCounter;
    // One charge starts exactly one collection, even if the Activate clip
    // raises its start event more than once.
    private bool vacuumPending;
    private VacuumPlan vacuumPlan;
    // Whether the running use paid a charge (the editor button doesn't), so an abort only refunds what was spent.
    private bool vacuumChargeSpent;

    // What one Vacuum use collects: always 3 items of one goal type, the
    // rack copies first, the rest from the board.
    private class VacuumPlan
    {
        public EItemName type;
        public readonly List<Item> fromRack = new List<Item>();
        public readonly List<Item> fromBoard = new List<Item>();
    }
    
    [Header(" Data ")]
    [SerializeField] private int initialPUCount;
    private int vacuumPUCount;
    private int springPUCount;
    private int fanPUCount;
    private int freezePUCount;

    private void Awake()
    {
        Vacuum.started += OnVacuumStarted;
        InputManager.powerupClicked += OnPowerupClicked;
        // Collision matrix changes are global and outlive the scene.
        SetItemCollisions(true);
        LoadData();
    }

    private void OnDestroy()
    {
        Vacuum.started -= OnVacuumStarted;
        InputManager.powerupClicked -= OnPowerupClicked;
        SetItemCollisions(true);
    }

    private void OnPowerupClicked(Powerup powerup)
    {
        if (powerup == null) return;

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

            case EPowerupType.Fan:
                HandleFanClicked();
                UpdateFanVisuals();
                break;

            case EPowerupType.FreezeGun:
                HandleFreezeClicked();
                UpdateFreezeVisuals();
                break;
        }

    }

    private void HandleVacuumClicked()
    {
        if (isVacuumBusy)
            return;

        if (vacuumPUCount <= 0)
        {
            // TODO: rewarded video / coins. Free refill placeholder for now.
            vacuumPUCount = 3;
            SaveData();
            return;
        }

        // Don't spend a charge when there is nothing to collect.
        VacuumPlan plan = BuildVacuumPlan();
        if (plan == null)
            return;

        // Locked from the tap until the flight starts, so the planned items
        // can't be tapped away during the animation.
        vacuumPUCount--;
        SaveData();
        vacuumChargeSpent = true;
        isVacuumBusy = true;
        InputManager.Lock();
        vacuumPlan = plan;
        vacuumPending = true;
        vacuum.Play();
    }

    private void OnVacuumStarted()
    {
        if (!vacuumPending)
            return;

        vacuumPending = false;
        StartCoroutine(VacuumSequence());
    }

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    void Update()
    {
        if (springQueued)
            TryFireQueuedSpring();
    }

    #region Vacuum Powerup
    [Button]
    private void VacuumPowerup()
    {
        // Editor test button: run a full use without the animation.
        if (isVacuumBusy) return;

        vacuumPlan = BuildVacuumPlan();
        if (vacuumPlan == null) return;

        vacuumChargeSpent = false;
        isVacuumBusy = true;
        InputManager.Lock();
        StartCoroutine(VacuumSequence());
    }

    private System.Collections.IEnumerator VacuumSequence()
    {
        float waited = 0;
        bool replannedAfterTimeout = false;

        while (true)
        {
            if (!GameManager.instance.IsGame())
            {
                AbortVacuum();
                yield break;
            }

            // Input was locked since the tap, but a merge or compaction can
            // still have moved things. Re-plan if the stored items went stale.
            if (!IsVacuumPlanValid(vacuumPlan))
                vacuumPlan = BuildVacuumPlan();

            if (vacuumPlan == null)
            {
                AbortVacuum();
                yield break;
            }

            if (vacuumPlan.fromRack.Count == 0 || ItemSpotsManager.instance.TryReleaseItems(vacuumPlan.fromRack))
                break;

            // Rack busy (item flying in, merge, compaction): wait for it.
            waited += Time.deltaTime;
            if (waited > vacuumRackWaitTimeout)
            {
                if (replannedAfterTimeout)
                {
                    AbortVacuum();
                    yield break;
                }

                replannedAfterTimeout = true;
                waited = 0;
                vacuumPlan = BuildVacuumPlan();
            }

            yield return null;
        }

        StartVacuumFlight(vacuumPlan);
        vacuumPlan = null;
    }

    // Couldn't fire: give the charge back and release the board.
    private void AbortVacuum()
    {
        vacuumPlan = null;

        if (vacuumChargeSpent)
        {
            vacuumPUCount++;
            SaveData();
            UpdateVacuumVisuals();
        }

        vacuumChargeSpent = false;
        isVacuumBusy = false;
        InputManager.Unlock();
    }

    private void StartVacuumFlight(VacuumPlan plan)
    {
        List<Item> itemsToCollect = new List<Item>();

        // Rack items sit shrunk under their spot: bring them back into the level.
        for (int i = 0; i < plan.fromRack.Count; i++)
        {
            Item item = plan.fromRack[i];
            LeanTween.cancel(item.gameObject);
            item.transform.SetParent(LevelManager.instance.ItemParent, true);
            item.DisablePhysics();
            item.DisableShadows();
            itemsToCollect.Add(item);
        }

        for (int i = 0; i < plan.fromBoard.Count; i++)
        {
            plan.fromBoard[i].DisablePhysics();
            itemsToCollect.Add(plan.fromBoard[i]);
        }

        vacuumCounter = 0;
        vacuumItemsToCollect = itemsToCollect.Count;

        float topOfPile = GetTopOfPile();

        // TODO: vacuum start sound once there is an audio system.
        for (int i = 0; i < itemsToCollect.Count; i++)
        {
            bool fromRack = i < plan.fromRack.Count;
            FlyToVacuum(itemsToCollect[i], fromRack, i * vacuumStagger, topOfPile);
        }

        // Only board items count: rack items were counted when they entered the rack.
        for (int i = 0; i < plan.fromBoard.Count; i++)
            itemPickedUp?.Invoke(plan.fromBoard[i]);

        // Flying items are kinematic with colliders off, so taps can come back.
        InputManager.Unlock();
    }

    // Type: the right-most rack item whose goal is still open, otherwise a
    // random open goal. Then every rack copy of it, topped up from the board
    // to exactly 3. Null when that is not possible: never collect fewer than 3.
    private VacuumPlan BuildVacuumPlan()
    {
        ItemLevelData[] goals = GoalManager.instance.Goals;
        EItemName? type = null;

        foreach (Item rackItem in ItemSpotsManager.instance.RackItemsRightToLeft())
        {
            if (IsOpenGoal(goals, rackItem.ItemName))
            {
                type = rackItem.ItemName;
                break;
            }
        }

        if (type == null)
        {
            List<EItemName> openGoals = new List<EItemName>();
            for (int i = 0; i < goals.Length; i++)
                if (goals[i].amount > 0)
                    openGoals.Add(goals[i].itemPrefab.ItemName);

            if (openGoals.Count <= 0)
                return null;

            type = openGoals[UnityEngine.Random.Range(0, openGoals.Count)];
        }

        VacuumPlan plan = new VacuumPlan { type = type.Value };

        foreach (Item rackItem in ItemSpotsManager.instance.RackItemsRightToLeft())
            if (rackItem.ItemName == plan.type)
                plan.fromRack.Add(rackItem);

        int needed = 3 - plan.fromRack.Count;
        Item[] items = LevelManager.instance.Items;

        for (int i = 0; i < items.Length && plan.fromBoard.Count < needed; i++)
        {
            if (IsFreeBoardItem(items[i]) && items[i].ItemName == plan.type)
                plan.fromBoard.Add(items[i]);
        }

        if (plan.fromBoard.Count < needed)
            return null;

        return plan;
    }

    private bool IsVacuumPlanValid(VacuumPlan plan)
    {
        if (plan == null)
            return false;

        List<Item> rackItems = new List<Item>(ItemSpotsManager.instance.RackItemsRightToLeft());

        for (int i = 0; i < plan.fromRack.Count; i++)
            if (plan.fromRack[i] == null || !rackItems.Contains(plan.fromRack[i]))
                return false;

        for (int i = 0; i < plan.fromBoard.Count; i++)
            if (!IsFreeBoardItem(plan.fromBoard[i]))
                return false;

        return true;
    }

    private static bool IsOpenGoal(ItemLevelData[] goals, EItemName type)
    {
        for (int i = 0; i < goals.Length; i++)
            if (goals[i].itemPrefab.ItemName == type)
                return goals[i].amount > 0;

        return false;
    }

    // On the board, not in the rack and not mid-flight.
    private static bool IsFreeBoardItem(Item item)
        => item != null && item.Spot == null && item.IsPhysicsEnabled;

    private void FlyToVacuum(Item item, bool fromRack, float delay, float topOfPile)
    {
        Vector3 start = item.transform.position;
        Vector3 target = vacuumEndPosition.position;
        Vector3 approach = target + Vector3.forward * vacuumApproachDistance;

        Vector3 ctrl = fromRack
            ? new Vector3(Mathf.MoveTowards(start.x, target.x, vacuumRackCtrlMaxX), start.y, start.z + vacuumRackCtrlZ)
            : new Vector3(Mathf.MoveTowards(start.x, target.x, vacuumBoardCtrlMaxX), topOfPile + vacuumBoardCtrlLift, start.z + vacuumBoardCtrlZ);

        // Sample the curve and its cumulative length, so it can be walked at constant speed.
        int count = Mathf.Max(2, vacuumPathSamples);
        Vector3[] points = new Vector3[count];
        float[] lengths = new float[count];

        for (int i = 0; i < count; i++)
        {
            points[i] = CubicBezier(start, ctrl, approach, target, i / (float)(count - 1));
            lengths[i] = i == 0 ? 0 : lengths[i - 1] + Vector3.Distance(points[i - 1], points[i]);
        }

        float totalLength = lengths[count - 1];
        float duration = Mathf.Max(.1f, totalLength * (fromRack ? vacuumRackSecondsPerMetre : vacuumBoardSecondsPerMetre));

        LeanTween.value(item.gameObject, 0f, 1f, duration)
            .setDelay(delay)
            .setEase(LeanTweenType.easeInSine)
            .setOnUpdate((float k) => item.transform.position = SamplePath(points, lengths, k * totalLength))
            .setOnComplete(() => ItemReachedVacuum(item));

        // Swell, settle back, then shrink into the nozzle in the last 0.05s.
        Vector3 baseScale = item.transform.localScale;
        float body = duration - .05f;

        LeanTween.scale(item.gameObject, baseScale * vacuumSwellScale, body * .25f)
            .setDelay(delay)
            .setEase(LeanTweenType.easeInSine);
        LeanTween.scale(item.gameObject, baseScale, body * .65f)
            .setDelay(delay + body * .25f)
            .setEase(LeanTweenType.easeOutSine);
        LeanTween.scale(item.gameObject, baseScale * vacuumArrivalScale, .05f)
            .setDelay(delay + body);

        // TODO: per-item arrival sound ~0.13s before duration, once there is an audio system.
    }

    private static Vector3 CubicBezier(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float t)
    {
        float u = 1 - t;
        return u * u * u * p0 + 3 * u * u * t * p1 + 3 * u * t * t * p2 + t * t * t * p3;
    }

    private static Vector3 SamplePath(Vector3[] points, float[] lengths, float distance)
    {
        for (int i = 1; i < points.Length; i++)
        {
            if (distance <= lengths[i])
            {
                float segment = lengths[i] - lengths[i - 1];
                float t = segment > 0 ? (distance - lengths[i - 1]) / segment : 0;
                return Vector3.Lerp(points[i - 1], points[i], t);
            }
        }

        return points[points.Length - 1];
    }

    // Highest point of the items lying on the board.
    private float GetTopOfPile()
    {
        float top = LevelManager.instance.ItemParent.position.y;
        Item[] items = LevelManager.instance.Items;

        for (int i = 0; i < items.Length; i++)
        {
            if (!IsFreeBoardItem(items[i]))
                continue;

            Collider c = items[i].GetComponentInChildren<Collider>();
            if (c != null)
                top = Mathf.Max(top, c.bounds.max.y);
        }

        return top;
    }

    private void ItemReachedVacuum(Item item)
    {
         vacuumCounter++;
         if (vacuumCounter >= vacuumItemsToCollect)
             isVacuumBusy = false;
         Destroy(item.gameObject);
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
        if (isSpringBusy || springQueued)
            return;

        if (springPUCount <= 0)
        {
            // TODO: rewarded video. Same free refill placeholder as the vacuum.
            springPUCount = 3;
            SaveData();
            return;
        }

        // The rack is mid-animation (item flying in, merge, compaction).
        // Don't drop the tap: fire as soon as the rack is free.
        if (ItemSpotsManager.instance.IsBusy)
        {
            springQueued = true;
            return;
        }

        FireSpring();
    }

    private void TryFireQueuedSpring()
    {
        if (!GameManager.instance.IsGame())
        {
            springQueued = false;
            return;
        }

        if (ItemSpotsManager.instance.IsBusy)
            return;

        // The Fan owns the input lock; wait for it to finish.
        if (isFanBusy)
            return;

        springQueued = false;
        FireSpring();
        UpdateSpringVisuals();
    }

    // Only spend a charge if an item actually came off the rack.
    private void FireSpring()
    {
        if (!SpringPowerup())
            return;

        springPUCount--;
        SaveData();
    }

    [Button]
    public bool SpringPowerup()
    {
        if (isSpringBusy || isFanBusy) return false;

        isSpringBusy = true;
        InputManager.Lock();

        // The slot the item leaves, for the flash (the release clears it).
        ItemSpot fromSpot = null;
        foreach (Item rackItem in ItemSpotsManager.instance.RackItemsRightToLeft())
        {
            fromSpot = rackItem.Spot;
            break;
        }

        Item itemToRelease = ItemSpotsManager.instance.ReleaseLastItemOnRack();

        if (itemToRelease == null)
        {
            InputManager.Unlock();
            isSpringBusy = false;
            return false;
        }

        // Not itemPickedUp: the goal count goes back up instead.
        itemBackToGame?.Invoke(itemToRelease);

        StartCoroutine(SpringThrowSequence(itemToRelease, fromSpot));

        return true;
    }

    private System.Collections.IEnumerator SpringThrowSequence(Item item, ItemSpot fromSpot)
    {
        Vector3 burstBase = fromSpot != null ? fromSpot.transform.position : item.transform.position;
        float launchDelay = springLaunchDelay / springSpeedMultiplier;

        // Vanish from the rack at once; the rack is already compacting.
        LeanTween.cancel(item.gameObject);
        item.transform.parent = LevelManager.instance.ItemParent;
        item.Hide();
        item.DisableShadows();

        // The effects cover the cut: slot flash, burst where the item was,
        // and the Spring charging.
        if (fromSpot != null)
            fromSpot.PlayFlash(springSlotFlashColor, springSlotFlashIn / springSpeedMultiplier, springSlotFlashOut / springSpeedMultiplier);

        if (springSlotBurstPrefab != null)
            Instantiate(springSlotBurstPrefab, burstBase + springSlotBurstOffset, Quaternion.identity).Play();

        if (spring != null)
            spring.PlayLoad(launchDelay);

        // TODO: slot sound / haptic once there is an audio system.

        yield return new WaitForSeconds(launchDelay);

        // Item gone or level over during the wait: release everything.
        if (item == null || !GameManager.instance.IsGame())
        {
            if (spring != null)
                spring.StopLoad();

            InputManager.Unlock();
            isSpringBusy = false;
            yield break;
        }

        Transform launchPoint = springLaunchPoint != null ? springLaunchPoint : spring != null ? spring.transform : null;
        if (launchPoint != null)
            item.transform.position = launchPoint.position;

        if (spring != null)
        {
            spring.PlayRelease();
            spring.PlayLaunchFx();
        }

        // TODO: launch sound + haptic once there is an audio system.

        item.Show();
        item.EnablePhysics();
        item.EnableShadows();

        Rigidbody rb = item.GetComponent<Rigidbody>();
        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;

        LeanTween.scale(item.gameObject, item.BaseScale, springScaleDuration)
            .setEase(LeanTweenType.easeOutQuad);

        item.BeginThrow(springThrowCollider);
        LaunchSpringItem(rb);

        StartCoroutine(SpringExtraFall(item, rb));
        StartCoroutine(SpringDamping(item, rb));
        StartCoroutine(SpringBoardEnterFallback(item));

        // Free at launch, not at landing, so the next Spring can go right away.
        InputManager.Unlock();
        isSpringBusy = false;
    }

    // Mostly up and into the board, with a little random sideways drift that
    // is pushed back toward the middle near the board's side edges.
    private void LaunchSpringItem(Rigidbody rb)
    {
        Vector3 launchPos = rb.position;
        Vector3 forward = Vector3.forward;

        if (springBoardCenter != null)
        {
            Vector3 toCenter = springBoardCenter.position - launchPos;
            toCenter.y = 0;
            if (toCenter.sqrMagnitude > .0001f)
                forward = toCenter.normalized;
        }

        Vector3 right = Vector3.Cross(Vector3.up, forward);
        Vector3 center = springBoardCenter != null ? springBoardCenter.position : launchPos;
        float lateral = Vector3.Dot(launchPos - center, right);

        float side = UnityEngine.Random.Range(0f, springSideRandom) * springSideMultiplier;
        int dir;

        if (lateral > springEdgeDistance)
        {
            dir = -1;
            side += springEdgeBonus;
        }
        else if (lateral < -springEdgeDistance)
        {
            dir = 1;
            side += springEdgeBonus;
        }
        else
            dir = UnityEngine.Random.value < .5f ? -1 : 1;

        Vector3 force = right * (dir * side * springSideForce)
            + Vector3.up * (springUpMultiplier * springUpForce)
            + forward * ((side * .25f + 1f) * springForwardForce * springForwardMultiplier);

        Vector3 torque = new Vector3(RandomSpringTorque(), RandomSpringTorque(), RandomSpringTorque());

        // ForceMode.Force applied once, as with the Fan.
        rb.AddForce(force * springForceScale, ForceMode.Force);
        rb.AddTorque(torque * springForceScale, ForceMode.Force);
    }

    private float RandomSpringTorque()
    {
        float value = UnityEngine.Random.Range((int)springTorqueRange.x, (int)springTorqueRange.y);
        return UnityEngine.Random.value < .5f ? -value : value;
    }

    // Still a free item on the board (not destroyed, vacuumed or re-picked).
    private static bool IsFreeOnBoard(Item item)
        => item != null && item.gameObject.activeInHierarchy && item.Spot == null && item.IsPhysicsEnabled;

    private System.Collections.IEnumerator SpringExtraFall(Item item, Rigidbody rb)
    {
        float timer = 0;

        while (timer < springFallDuration)
        {
            yield return new WaitForFixedUpdate();

            if (!IsFreeOnBoard(item))
                yield break;

            rb.AddForce(Vector3.down * (springExtraFall * Time.fixedDeltaTime * springFallMultiplier * springForceScale), ForceMode.Force);
            timer += Time.fixedDeltaTime;
        }
    }

    private System.Collections.IEnumerator SpringDamping(Item item, Rigidbody rb)
    {
        float originalDamping = rb.linearDamping;
        float timer = 0;

        while (timer < springDampingDuration)
        {
            yield return null;

            if (!IsFreeOnBoard(item))
                break;

            rb.linearDamping += springDampingPerFrame;
            timer += Time.deltaTime;
        }

        if (rb != null)
            rb.linearDamping = originalDamping;
    }

    private System.Collections.IEnumerator SpringBoardEnterFallback(Item item)
    {
        yield return new WaitForSeconds(springBoardEnterFallback);

        if (item != null)
            item.EndThrow();
    }

    private void UpdateSpringVisuals()
    {
        if (spring == null)
            return;

        spring.UpdateVisuals(springPUCount);
    }

    #endregion

    #region Fan Powerup

    private void HandleFanClicked()
    {
        if (isFanBusy)
            return;

        if (fanPUCount <= 0)
        {
            // TODO: rewarded video. Same free refill placeholder as the others.
            fanPUCount = 3;
            SaveData();
            return;
        }

        if (!FanPowerup())
            return;

        fanPUCount--;
        SaveData();
    }

    // Shuffle: every item on the board gets one random launch and physics
    // does the rest. Items ignore each other while airborne, so a dense pile
    // spreads out instead of colliding mid-air.
    [Button]
    public bool FanPowerup()
    {
        // Not during a Spring's launch delay: both use the input lock.
        if (isFanBusy || isSpringBusy) return false;

        if (GetBoardItems().Count <= 0)
            return false;

        StartCoroutine(FanSequence());
        return true;
    }

    private System.Collections.IEnumerator FanSequence()
    {
        isFanBusy = true;
        InputManager.Lock();

        float windStart = fanWindStartTime / fanSpeedMultiplier;
        float throwTime = fanThrowTime / fanSpeedMultiplier;
        float windStop = fanWindStopTime / fanSpeedMultiplier;

        if (fan != null)
            fan.Play(fanEndTime);

        yield return new WaitForSeconds(windStart);
        if (fan != null)
            fan.StartWind();

        yield return new WaitForSeconds(throwTime - windStart);
        // TODO: whoosh sound once there is an audio system.
#if UNITY_ANDROID || UNITY_IOS
        Handheld.Vibrate();
#endif
        SetItemCollisions(false);

        List<Item> boardItems = GetBoardItems();
        for (int i = 0; i < boardItems.Count; i++)
            ThrowToAir(boardItems[i].GetComponent<Rigidbody>());

        yield return new WaitForSeconds(windStop - throwTime);
        if (fan != null)
            fan.StopWind();

        // The end time is absolute and not sped up.
        yield return new WaitForSeconds(Mathf.Max(0, fanEndTime - windStop));

        SetItemCollisions(true);
        InputManager.Unlock();
        isFanBusy = false;
    }

    private void ThrowToAir(Rigidbody rb)
    {
        float angle = UnityEngine.Random.Range(0, 360) * Mathf.Deg2Rad;
        float up = UnityEngine.Random.Range(fanThrowUpForce.x, fanThrowUpForce.y);

        Vector3 force = new Vector3(Mathf.Cos(angle) * fanThrowSideForce, up, Mathf.Sin(angle) * fanThrowSideForce);
        Vector3 torque = new Vector3(RandomTorqueAxis(), RandomTorqueAxis(), RandomTorqueAxis());

        // ForceMode.Force applied once: it acts for one
        // physics step, i.e. a velocity change of force * fixedDeltaTime / mass.
        rb.AddForce(force * fanForceScale, ForceMode.Force);
        rb.AddTorque(torque * fanForceScale, ForceMode.Force);
    }

    // Each axis 20-49 with a random sign.
    private float RandomTorqueAxis()
    {
        float value = UnityEngine.Random.Range((int)fanThrowTorque.x, (int)fanThrowTorque.y);
        return UnityEngine.Random.value < .5f ? -value : value;
    }

    private static void SetItemCollisions(bool enabled)
    {
        int itemsLayer = LayerMask.NameToLayer("Items");
        Physics.IgnoreLayerCollision(itemsLayer, itemsLayer, !enabled);
    }

    // Items lying on the board: not in the rack and not mid-flight
    // (being vacuumed or returned by Spring).
    private List<Item> GetBoardItems()
    {
        List<Item> boardItems = new List<Item>();
        Item[] items = LevelManager.instance.Items;

        for (int i = 0; i < items.Length; i++)
        {
            if (items[i] == null || items[i].Spot != null || !items[i].IsPhysicsEnabled)
                continue;

            boardItems.Add(items[i]);
        }

        return boardItems;
    }

    private void UpdateFanVisuals()
    {
        if (fan == null)
            return;

        fan.UpdateVisuals(fanPUCount);
    }

    #endregion

    #region Freeze Gun Powerup

    private void HandleFreezeClicked()
    {
        if (isFreezeBusy)
            return;

        if (freezePUCount <= 0)
        {
            // TODO: rewarded video. Same free refill placeholder as the others.
            freezePUCount = 3;
            SaveData();
            return;
        }

        if (!FreezePowerup())
            return;

        freezePUCount--;
        SaveData();
    }

    // Tap: the timer stops at once. The gun fires after the delay, and when
    // the shot lands the freeze starts (or stacks). No input lock: the player
    // keeps playing the whole time.
    [Button]
    public bool FreezePowerup()
    {
        if (isFreezeBusy) return false;

        TimerManager timer = TimerManager.instance;
        if (!GameManager.instance.IsGame() || timer == null || !timer.IsRunning)
            return false;

        isFreezeBusy = true;
        freezeShotsPending++;
        timer.SetPaused(true);

        if (freezeGun != null)
            freezeGun.Play();

        // TODO: gun sound / haptic once there is an audio system.
        freezeRoutines.Add(StartCoroutine(FreezeFireSequence()));
        return true;
    }

    private System.Collections.IEnumerator FreezeFireSequence()
    {
        yield return new WaitForSeconds(freezeFireDelay / freezeSpeedMultiplier);

        isFreezeBusy = false;

        if (!GameManager.instance.IsGame())
            yield break;

        Vector3 start = freezeGun != null ? freezeGun.Muzzle.position : transform.position;
        GameObject projectile = SpawnFreezeProjectile(start);
        freezeProjectiles.Add(projectile);

        float duration = Mathf.Max(.05f, Vector3.Distance(start, GetFreezeTargetPoint()) * freezeProjectileSecondsPerUnit);

        // Target recomputed every frame in case the camera moves.
        LeanTween.value(projectile, 0f, 1f, duration)
            .setOnUpdate((float k) => projectile.transform.position = Vector3.Lerp(start, GetFreezeTargetPoint(), k))
            .setOnComplete(() => OnFreezeShotLanded(projectile));
    }

    private void OnFreezeShotLanded(GameObject projectile)
    {
        freezeShotsPending = Mathf.Max(0, freezeShotsPending - 1);

        LeanTween.scale(projectile, Vector3.zero, .15f);
        Destroy(projectile, freezeProjectileRecycleDelay);
        freezeProjectiles.Remove(projectile);

        TimerManager timer = TimerManager.instance;
        if (!GameManager.instance.IsGame() || timer == null || !timer.IsRunning)
            return;

        timer.Freeze(freezeDuration, OnFreezeEnded);
        timer.SetPaused(freezeShotsPending > 0);

        if (freezeOverlay != null)
            freezeOverlay.FadeIn();

        if (freezeHitParticles != null)
            Instantiate(freezeHitParticles, projectile.transform.position, Quaternion.identity, transform).Play();
        // TODO: hit sound / haptic once there is an audio system.
    }

    private void OnFreezeEnded()
    {
        if (freezeOverlay != null)
            freezeOverlay.FadeOut();
    }

    // The point in front of the camera that sits under the timer on screen.
    private Vector3 GetFreezeTargetPoint()
    {
        Camera cam = Camera.main;

        if (freezeTarget == null || cam == null)
            return transform.position;

        // Overlay canvas: the RectTransform's position is already in screen space.
        Vector2 screen = RectTransformUtility.WorldToScreenPoint(null, freezeTarget.position);
        return cam.ScreenToWorldPoint(new Vector3(screen.x, screen.y, freezeTargetDepth));
    }

    private GameObject SpawnFreezeProjectile(Vector3 position)
    {
        if (freezeProjectilePrefab != null)
            return Instantiate(freezeProjectilePrefab, position, Quaternion.identity);

        // Placeholder: a small icy sphere.
        GameObject sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        Destroy(sphere.GetComponent<Collider>());
        sphere.name = "Freeze Projectile (placeholder)";
        sphere.transform.position = position;
        sphere.transform.localScale = Vector3.one * .25f;
        sphere.GetComponent<Renderer>().material.color = new Color(.6f, .9f, 1f);
        return sphere;
    }

    // Win or lose mid-sequence: nothing may stay paused, flying or visible.
    private void ResetFreeze()
    {
        for (int i = 0; i < freezeRoutines.Count; i++)
            if (freezeRoutines[i] != null)
                StopCoroutine(freezeRoutines[i]);

        freezeRoutines.Clear();

        for (int i = 0; i < freezeProjectiles.Count; i++)
        {
            if (freezeProjectiles[i] == null)
                continue;

            LeanTween.cancel(freezeProjectiles[i]);
            Destroy(freezeProjectiles[i]);
        }

        freezeProjectiles.Clear();
        freezeShotsPending = 0;
        isFreezeBusy = false;

        if (freezeOverlay != null)
            freezeOverlay.ResetOverlay();
    }

    public void GameStateChangedCallback(EGameState gameState)
    {
        if (gameState == EGameState.LEVELCOMPLETE || gameState == EGameState.GAMEOVER)
            ResetFreeze();
    }

    private void UpdateFreezeVisuals()
    {
        if (freezeGun == null)
            return;

        freezeGun.UpdateVisuals(freezePUCount);
    }

    #endregion
    
    private void LoadData()
    {
        vacuumPUCount = PlayerPrefs.GetInt("VacuumPUCount", initialPUCount);
        springPUCount = PlayerPrefs.GetInt("SpringPUCount", initialPUCount);
        fanPUCount = PlayerPrefs.GetInt("FanPUCount", initialPUCount);
        freezePUCount = PlayerPrefs.GetInt("FreezePUCount", initialPUCount);
        UpdateVacuumVisuals();
        UpdateSpringVisuals();
        UpdateFanVisuals();
        UpdateFreezeVisuals();
    }
    
    private void SaveData()
    {
        PlayerPrefs.SetInt("VacuumPUCount", vacuumPUCount);
        PlayerPrefs.SetInt("SpringPUCount", springPUCount);
        PlayerPrefs.SetInt("FanPUCount", fanPUCount);
        PlayerPrefs.SetInt("FreezePUCount", freezePUCount);
    }

}
