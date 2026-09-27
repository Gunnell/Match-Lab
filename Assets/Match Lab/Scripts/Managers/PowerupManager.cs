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
    [Tooltip("Scales the original forces to this world, like fanForceScale. The Fan's .28 is too weak here: "
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

    [Header(" Fan Elements ")]
    [SerializeField] private Fan fan;
    // Timeline and forces follow Match Factory's Shuffle booster.
    [Tooltip("Divides every step except the end time (ShuffleBoosterSpeedMultiplier).")]
    [SerializeField] private float fanSpeedMultiplier = 1f;
    [SerializeField] private float fanWindStartTime = .33f;
    [SerializeField] private float fanThrowTime = .4f;
    [SerializeField] private float fanWindStopTime = .91f;
    [Tooltip("Item collisions and taps come back. Not affected by the speed multiplier.")]
    [SerializeField] private float fanEndTime = 1.2f;
    [SerializeField] private Vector2 fanThrowUpForce = new Vector2(650, 750);
    [SerializeField] private float fanThrowSideForce = 300;
    [SerializeField] private Vector2 fanThrowTorque = new Vector2(20, 50);
    [Tooltip("Scales the original forces to this world. At 1 an item (mass 1) is thrown ~10 m up, "
        + "past the camera; .28 lands it inside the 0.8s window before collisions return.")]
    [SerializeField] private float fanForceScale = .28f;
    [Header(" Actions ")] 
    public static Action<Item> itemPickedUp;
    public static Action<Item> itemBackToGame;

    [Header(" Settings ")] 
    // Each powerup has its own busy flag so using one never blocks the other.
    private bool isVacuumBusy;
    private bool isSpringBusy;
    private bool isFanBusy;
    // Spring tapped while the rack was animating; fired once the rack is free.
    private bool springQueued;
    private int vacuumItemsToCollect;
    private int vacuumCounter;
    // One charge starts exactly one collection, even if the Activate clip
    // raises its start event more than once.
    private bool vacuumPending;
    
    [Header(" Data ")]
    [SerializeField] private int initialPUCount;
    private int vacuumPUCount;
    private int springPUCount;
    private int fanPUCount;

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
        if (GetVacuumTargets().Count <= 0)
            return;

        // Busy from the click, not from the animation event, so tapping
        // again while the vacuum animation plays can't spend a second charge.
        isVacuumBusy = true;
        vacuumPending = true;
        vacuumPUCount--;
        SaveData();
        vacuum.Play();
    }

    private void OnVacuumStarted()
    {
        if (!vacuumPending)
            return;

        vacuumPending = false;
        VacuumPowerup();
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
         // The board may have changed between the click and the animation
         // event, so the targets are looked up again here.
         List<Item> itemsToCollect = GetVacuumTargets();

         if (itemsToCollect.Count <= 0)
         {
             isVacuumBusy = false;
             return;
         }

         isVacuumBusy = true;
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
            // Mid-flight (e.g. being returned by Spring): leave it alone.
            if (!items[i].IsPhysicsEnabled)
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
             isVacuumBusy = false;
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
        InputManager.IsLocked = true;

        Item itemToRelease = ItemSpotsManager.instance.ReleaseLastItemOnRack();

        if (itemToRelease == null)
        {
            InputManager.IsLocked = false;
            isSpringBusy = false;
            return false;
        }

        // Not itemPickedUp: the goal count goes back up instead.
        itemBackToGame?.Invoke(itemToRelease);

        StartCoroutine(SpringThrowSequence(itemToRelease));

        return true;
    }

    private System.Collections.IEnumerator SpringThrowSequence(Item item)
    {
        // Vanish from the rack at once; the rack is already compacting.
        LeanTween.cancel(item.gameObject);
        item.transform.parent = LevelManager.instance.ItemParent;
        item.Hide();
        item.DisableShadows();

        // TODO: Spring button animation / particle hook (Spring.cs has none yet).

        yield return new WaitForSeconds(springLaunchDelay / springSpeedMultiplier);

        if (item == null)
        {
            InputManager.IsLocked = false;
            isSpringBusy = false;
            yield break;
        }

        Transform launchPoint = springLaunchPoint != null ? springLaunchPoint : spring != null ? spring.transform : null;
        if (launchPoint != null)
            item.transform.position = launchPoint.position;

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
        InputManager.IsLocked = false;
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
        InputManager.IsLocked = true;

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
        InputManager.IsLocked = false;
        isFanBusy = false;
    }

    private void ThrowToAir(Rigidbody rb)
    {
        float angle = UnityEngine.Random.Range(0, 360) * Mathf.Deg2Rad;
        float up = UnityEngine.Random.Range(fanThrowUpForce.x, fanThrowUpForce.y);

        Vector3 force = new Vector3(Mathf.Cos(angle) * fanThrowSideForce, up, Mathf.Sin(angle) * fanThrowSideForce);
        Vector3 torque = new Vector3(RandomTorqueAxis(), RandomTorqueAxis(), RandomTorqueAxis());

        // ForceMode.Force applied once, as in the original: it acts for one
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
    
    private void LoadData()
    {
        vacuumPUCount = PlayerPrefs.GetInt("VacuumPUCount", initialPUCount);
        springPUCount = PlayerPrefs.GetInt("SpringPUCount", initialPUCount);
        fanPUCount = PlayerPrefs.GetInt("FanPUCount", initialPUCount);
        UpdateVacuumVisuals();
        UpdateSpringVisuals();
        UpdateFanVisuals();
    }
    
    private void SaveData()
    {
        PlayerPrefs.SetInt("VacuumPUCount", vacuumPUCount);
        PlayerPrefs.SetInt("SpringPUCount", springPUCount);
        PlayerPrefs.SetInt("FanPUCount", fanPUCount);
    }

}
