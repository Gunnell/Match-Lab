using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// After the player has been idle, outlines a group of board items that would
/// make a match, then cycles through other groups until the next input.
/// </summary>
public class HintManager : MonoBehaviour
{
    [Header(" Timing ")]
    [Tooltip("Idle time before each hint. The last value repeats.")]
    [SerializeField] private float[] idleDelays = { 5f, 6f, 7f };
    [Tooltip("While still idle, the next group shows after this long.")]
    [SerializeField] private float cycleDelay = 4f;
    [SerializeField] private float showDuration = .85f;
    [SerializeField] private float stagger = .1f;

    [Header(" Look ")]
    [SerializeField] private Material outlineMaterial;
    [SerializeField] private Color outlineColor = new Color(1f, .92f, .2f);
    [SerializeField] private float outlineThickness = .08f;
    [SerializeField] private float pulseScale = 1.1f;
    [SerializeField] private float pulseTime = .2f;

    [Header(" Choice ")]
    [SerializeField] private int maxGroups = 5;
    [Tooltip("Non-goal items this many times bigger than the typical board item are hinted first, to clear space.")]
    [SerializeField] private float bigItemFactor = 1.5f;

    private enum State { Reset, Waiting, Cycling }

    private State state;
    private float timer;
    private int hintIndex;
    private int cycleIndex;
    private readonly List<List<Item>> groups = new List<List<Item>>();
    private readonly List<HintGhost> ghosts = new List<HintGhost>();
    private Coroutine showRoutine;
    private Material hintMaterial;

    private void Awake()
    {
        if (outlineMaterial != null)
        {
            hintMaterial = new Material(outlineMaterial);
            if (hintMaterial.HasProperty("_Color"))
                hintMaterial.SetColor("_Color", outlineColor);
            if (hintMaterial.HasProperty("_Thickness"))
                hintMaterial.SetFloat("_Thickness", outlineThickness);
        }

        InputManager.itemClicked += OnInput;
        InputManager.powerupClicked += OnInput;
    }

    private void OnDestroy()
    {
        InputManager.itemClicked -= OnInput;
        InputManager.powerupClicked -= OnInput;

        if (hintMaterial != null)
            Destroy(hintMaterial);
    }

    private void OnInput(Item item) => ResetLoop();
    private void OnInput(Powerup powerup) => ResetLoop();

    private void Update()
    {
        if (GameManager.instance == null || !GameManager.instance.IsGame() || InputManager.IsLocked || Input.GetMouseButtonDown(0))
        {
            ResetLoop();
            return;
        }

        switch (state)
        {
            case State.Reset:
                timer = idleDelays.Length > 0 ? idleDelays[Mathf.Min(hintIndex, idleDelays.Length - 1)] : 5f;
                cycleIndex = 0;
                groups.Clear();
                state = State.Waiting;
                break;

            case State.Waiting:
                if (ItemSpotsManager.instance.FreeSpotCount < 1)
                    return;

                timer -= Time.deltaTime;
                if (timer > 0)
                    return;

                if (!FindGroups())
                {
                    state = State.Reset;
                    return;
                }

                ShowGroup(groups[0]);
                hintIndex++;
                timer = cycleDelay;
                state = State.Cycling;
                break;

            case State.Cycling:
                timer -= Time.deltaTime;
                if (timer > 0)
                    return;

                cycleIndex++;
                ShowGroup(groups[cycleIndex % groups.Count]);
                timer = cycleDelay;
                break;
        }
    }

    private void LateUpdate()
    {
        for (int i = 0; i < ghosts.Count; i++)
            ghosts[i].Follow();
    }

    private void ResetLoop()
    {
        state = State.Reset;

        if (showRoutine != null)
        {
            StopCoroutine(showRoutine);
            showRoutine = null;
        }

        for (int i = 0; i < ghosts.Count; i++)
            ghosts[i].Hide();
    }

    // Groups of the highest board items that would make a match, the best first.
    private bool FindGroups()
    {
        groups.Clear();

        int maxBoard = Mathf.Min(ItemSpotsManager.instance.FreeSpotCount, 3);
        if (maxBoard < 1 || LevelManager.instance == null)
            return false;

        Dictionary<EItemName, int> rackCounts = new Dictionary<EItemName, int>();
        foreach (Item rackItem in ItemSpotsManager.instance.RackItemsRightToLeft())
            rackCounts[rackItem.ItemName] = rackCounts.TryGetValue(rackItem.ItemName, out int n) ? n + 1 : 1;

        List<Item> boardItems = new List<Item>();
        foreach (Item item in LevelManager.instance.ItemParent.GetComponentsInChildren<Item>())
            if (item.Spot == null && item.IsPhysicsEnabled && !item.IsBeingThrown)
                boardItems.Add(item);

        if (boardItems.Count == 0)
            return false;

        // Highest first: those are the ones the player can reach.
        boardItems.Sort((a, b) => b.transform.position.y.CompareTo(a.transform.position.y));

        Dictionary<EItemName, List<Item>> byType = new Dictionary<EItemName, List<Item>>();
        foreach (Item item in boardItems)
        {
            rackCounts.TryGetValue(item.ItemName, out int inRack);
            if (inRack + maxBoard < 3)
                continue;

            if (!byType.TryGetValue(item.ItemName, out List<Item> group))
                byType[item.ItemName] = group = new List<Item>();

            if (group.Count < maxBoard)
                group.Add(item);
        }

        List<List<Item>> candidates = new List<List<Item>>();
        foreach (List<Item> group in byType.Values)
            if (group.Count >= maxBoard)
                candidates.Add(group);

        // Groups whose lowest item sits highest come first.
        candidates.Sort((a, b) => b[b.Count - 1].transform.position.y.CompareTo(a[a.Count - 1].transform.position.y));

        float bigVolume = MedianVolume(boardItems) * bigItemFactor;
        foreach (List<Item> group in candidates)
            if (groups.Count < maxGroups && !IsGoal(group[0].ItemName) && Volume(group[0]) > bigVolume)
                groups.Add(group);

        if (groups.Count == 0)
            foreach (List<Item> group in candidates)
                if (groups.Count < maxGroups)
                    groups.Add(group);

        return groups.Count > 0;
    }

    private void ShowGroup(List<Item> group)
    {
        List<Item> items = new List<Item>();
        foreach (Item item in group)
            if (item != null && item.Spot == null)
                items.Add(item);

        // Short of three on the board: light up the ones already in the rack too.
        if (items.Count > 0 && items.Count < 3)
            foreach (Item rackItem in ItemSpotsManager.instance.RackItemsRightToLeft())
                if (items.Count < 3 && rackItem.ItemName == items[0].ItemName)
                    items.Add(rackItem);

        if (showRoutine != null)
            StopCoroutine(showRoutine);

        showRoutine = StartCoroutine(ShowItems(items));
    }

    private IEnumerator ShowItems(List<Item> items)
    {
        float duration = showDuration;

        for (int i = 0; i < items.Count; i++)
        {
            if (items[i] != null)
                GetGhost(i).Show(items[i], duration, pulseScale, pulseTime, hintMaterial);

            duration -= stagger;
            yield return new WaitForSeconds(stagger);
        }

        showRoutine = null;
    }

    private HintGhost GetGhost(int index)
    {
        while (ghosts.Count <= index)
            ghosts.Add(new HintGhost(transform));

        return ghosts[index];
    }

    private static bool IsGoal(EItemName itemName)
    {
        if (GoalManager.instance == null || GoalManager.instance.Goals == null)
            return false;

        foreach (ItemLevelData goal in GoalManager.instance.Goals)
            if (goal.amount > 0 && goal.itemPrefab != null && goal.itemPrefab.ItemName == itemName)
                return true;

        return false;
    }

    private static float Volume(Item item)
    {
        Vector3 size = item.BoardBounds.size * item.transform.parent.lossyScale.x;
        return size.x * size.y * size.z;
    }

    private static float MedianVolume(List<Item> items)
    {
        List<float> volumes = new List<float>();
        foreach (Item item in items)
            volumes.Add(Volume(item));

        volumes.Sort();
        return volumes[volumes.Count / 2];
    }

    // An outlined copy of an item's mesh drawn over it, so the item itself and
    // its physics stay untouched.
    private class HintGhost
    {
        private readonly GameObject gameObject;
        private readonly MeshFilter meshFilter;
        private readonly MeshRenderer meshRenderer;
        private Transform source;
        private Item item;
        private float shownAt;
        private float duration;
        private float pulseScale;
        private float pulseTime;

        public HintGhost(Transform parent)
        {
            gameObject = new GameObject("Hint");
            gameObject.transform.SetParent(parent, false);
            meshFilter = gameObject.AddComponent<MeshFilter>();
            meshRenderer = gameObject.AddComponent<MeshRenderer>();
            meshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            gameObject.SetActive(false);
        }

        public void Show(Item item, float duration, float pulseScale, float pulseTime, Material outline)
        {
            Renderer itemRenderer = item.GetComponentInChildren<MeshRenderer>();
            MeshFilter itemMesh = itemRenderer != null ? itemRenderer.GetComponent<MeshFilter>() : null;
            if (itemMesh == null)
                return;

            this.item = item;
            source = itemRenderer.transform;
            this.duration = duration;
            this.pulseScale = pulseScale;
            this.pulseTime = pulseTime;
            shownAt = Time.time;

            meshFilter.sharedMesh = itemMesh.sharedMesh;

            Material[] materials = itemRenderer.sharedMaterials;
            if (outline != null)
            {
                System.Array.Resize(ref materials, materials.Length + 1);
                materials[materials.Length - 1] = outline;
            }
            meshRenderer.sharedMaterials = materials;

            gameObject.SetActive(true);
            Follow();
        }

        public void Hide()
        {
            item = null;
            source = null;
            gameObject.SetActive(false);
        }

        public void Follow()
        {
            if (item == null)
                return;

            float elapsed = Time.time - shownAt;
            if (source == null || elapsed >= duration || (item.Spot == null && !item.IsPhysicsEnabled))
            {
                Hide();
                return;
            }

            // 1 -> pulseScale -> 1, repeating.
            float phase = Mathf.PingPong(elapsed / pulseTime, 1f);
            float scale = Mathf.Lerp(1f, pulseScale, Mathf.Sin(phase * Mathf.PI * .5f));

            gameObject.transform.SetPositionAndRotation(source.position, source.rotation);
            gameObject.transform.localScale = source.lossyScale * scale;
        }
    }
}
