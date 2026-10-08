using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Owns the play box and the items lying in it.
///
/// At initialisation the whole box is built in code from the constants below:
/// six thick static walls (Floor, Ceiling, East, West, North, South) on the
/// Board layer, an enter trigger and a background plane, all under a
/// <c>Board</c> root at the world origin. Nothing about the box is placed by
/// hand in the scene. Other systems read the board's dimensions from here.
///
/// Thrown items (contract for boosters that launch items onto the board from
/// in front of it):
/// <list type="number">
/// <item>At launch: <c>Physics.IgnoreCollision(itemCollider, BoardService.Instance.ThrowCollider, true)</c>,
/// so the item can pass over or through the front lip.</item>
/// <item>Restore the collision (<c>IgnoreCollision(..., false)</c>) when the item's
/// <c>OnTriggerEnter</c> hits <see cref="Trigger"/>, or after
/// <see cref="ThrowIgnoreFallbackSeconds"/>, whichever comes first.</item>
/// </list>
/// The board only provides <see cref="ThrowCollider"/> and <see cref="Trigger"/>;
/// the item / booster code implements the rule.
/// </summary>
[DefaultExecutionOrder(-100)]
public class BoardService : MonoBehaviour
{
    // Inner dimensions in world units. Sized for an orthographic, top-down-ish
    // camera fitted to ~9.5 units of width, with items ~1 unit across.
    public const float Width = 9.1f;      // X, inner, face to face
    public const float Height = 55f;      // Y, floor (y = 0) to ceiling underside; effectively open on top
    public const float Depth = 10.5f;     // Z, inner
    public const float Thickness = 42f;   // every wall, floor and ceiling; huge so fast bodies can't tunnel through
    // The enter trigger stops this far short of the front (South) wall, so a
    // strip along the front lip is outside it.
    public const float EnterTriggerDepthInset = 1.25f;

    /// <summary>A thrown item stops ignoring <see cref="ThrowCollider"/> after this long even if it never hit <see cref="Trigger"/>.</summary>
    public const float ThrowIgnoreFallbackSeconds = 1.5f;

    public const string BoardLayerName = "Board";
    // Built-in layer: items get trigger events from it, and the default
    // raycast mask skips it, so it never blocks taps on items.
    public const string TriggerLayerName = "Ignore Raycast";

    // A 10 x 10 Unity plane; the background scale is the view size over this.
    private const float BackgroundPlaneSize = 10f;
    private const float BackgroundDepthOverscan = 1.075f;

    public static BoardService Instance { get; private set; }

    [Header(" Physics ")]
    [Tooltip("Shared by every wall, floor and ceiling collider. A low-friction default is made at runtime if empty.")]
    [SerializeField] private PhysicsMaterial wallMaterial;

    [Header(" Background ")]
    [SerializeField] private Material backgroundMaterial;

    public static event Action OnBoardClear;

    private Transform root;
    private BoxCollider floor, ceiling, east, west, north, south;
    private BoxCollider trigger;
    private Transform background;

    private Vector3 innerSize = new Vector3(Width, Height, Depth);
    private float wallThickness = Thickness;

    // Item registry: one bucket per item type. Buckets are kept in a flat list
    // too, so walking every item needs no dictionary enumeration.
    private readonly Dictionary<int, List<Item>> itemsByType = new Dictionary<int, List<Item>>();
    private readonly List<List<Item>> buckets = new List<List<Item>>();
    private readonly Dictionary<Item, Renderer> itemRenderers = new Dictionary<Item, Renderer>();
    private static readonly List<Item> noItems = new List<Item>(0);

    /// <summary>Inner size of the box (X width, Y floor to ceiling, Z depth).</summary>
    public Vector3 InnerSize => innerSize;
    public float WallThickness => wallThickness;
    /// <summary>Inner volume of the box in world space; the floor's top is at y = 0.</summary>
    public Bounds InnerBounds => new Bounds(new Vector3(0, innerSize.y / 2, 0), innerSize);

    public Transform Root => root;
    /// <summary>The front (South, rack side) wall. Thrown items ignore only this collider until they are inside.</summary>
    public Collider ThrowCollider => south;
    /// <summary>Volume covering the board interior except a strip along the front lip.</summary>
    public Collider Trigger => trigger;

    public int ItemCount { get; private set; }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        CameraService.OnCameraSizeChanged += RefreshBackground;
        Initialize();
    }

    private void OnDestroy()
    {
        if (Instance != this)
            return;

        Instance = null;
        CameraService.OnCameraSizeChanged -= RefreshBackground;

        if (root != null)
            Destroy(root.gameObject);
    }

    public void Initialize()
    {
        SetBoardColliders(Width, Height, Depth, Thickness);
        RefreshBackground();
    }

    #region Box

    /// <summary>
    /// Sizes and places the box walls and the enter trigger. Builds them on the
    /// first call; later calls only re-scale and re-position the same objects.
    /// </summary>
    public void SetBoardColliders(float width, float height, float depth, float thickness)
    {
        EnsureBuilt();

        innerSize = new Vector3(width, height, depth);
        wallThickness = thickness;

        float w = width, h = height, d = depth, t = thickness;

        Place(floor, new Vector3(w + 2 * t, t, d + 2 * t), new Vector3(0, -t / 2, 0));
        Place(ceiling, new Vector3(w + 2 * t, t, d + 2 * t), new Vector3(0, h + t / 2, 0));
        Place(east, new Vector3(t, h + 2 * t, d + 2 * t), new Vector3((w + t) / 2, h / 2, 0));
        Place(west, new Vector3(t, h + 2 * t, d + 2 * t), new Vector3(-(w + t) / 2, h / 2, 0));
        Place(north, new Vector3(w + 2 * t, h + 2 * t, t), new Vector3(0, h / 2, (d + t) / 2));
        Place(south, new Vector3(w + 2 * t, h + 2 * t, t), new Vector3(0, h / 2, -(d + t) / 2));

        Place(trigger,
            new Vector3(w, h, d - EnterTriggerDepthInset),
            new Vector3(0, h / 2, EnterTriggerDepthInset / 2));
    }

    private void EnsureBuilt()
    {
        if (root != null)
            return;

        root = new GameObject("Board").transform;
        root.SetPositionAndRotation(Vector3.zero, Quaternion.identity);

        if (wallMaterial == null)
            wallMaterial = new PhysicsMaterial("Board Wall")
            {
                dynamicFriction = .2f,
                staticFriction = .2f,
                bounciness = 0f,
                frictionCombine = PhysicsMaterialCombine.Average,
                bounceCombine = PhysicsMaterialCombine.Minimum
            };

        int boardLayer = LayerMask.NameToLayer(BoardLayerName);
        if (boardLayer < 0)
        {
            Debug.LogWarning($"BoardService: layer '{BoardLayerName}' is missing, walls go on Default.");
            boardLayer = 0;
        }

        floor = CreateWall("Floor", boardLayer);
        ceiling = CreateWall("Ceiling", boardLayer);
        east = CreateWall("East", boardLayer);
        west = CreateWall("West", boardLayer);
        north = CreateWall("North", boardLayer);
        south = CreateWall("South", boardLayer);

        trigger = CreateBox("Trigger", LayerMask.NameToLayer(TriggerLayerName));
        trigger.isTrigger = true;

        background = GameObject.CreatePrimitive(PrimitiveType.Plane).transform;
        background.name = "Background";
        Destroy(background.GetComponent<Collider>());
        background.SetParent(root, false);
        if (backgroundMaterial != null)
            background.GetComponent<Renderer>().sharedMaterial = backgroundMaterial;
    }

    private BoxCollider CreateWall(string name, int layer)
    {
        BoxCollider box = CreateBox(name, layer);
        box.sharedMaterial = wallMaterial;
        return box;
    }

    private BoxCollider CreateBox(string name, int layer)
    {
        GameObject go = new GameObject(name);
        go.layer = layer;
        go.isStatic = true;
        go.transform.SetParent(root, false);

        BoxCollider box = go.AddComponent<BoxCollider>();
        box.center = Vector3.zero;
        box.size = Vector3.one;
        return box;
    }

    private static void Place(Collider collider, Vector3 localScale, Vector3 localPosition)
    {
        Transform t = collider.transform;
        t.localScale = localScale;
        t.localPosition = localPosition;
    }

    #endregion

    #region Background

    /// <summary>
    /// Sizes the background plane to fill the camera view. Runs on init and
    /// whenever <see cref="CameraService.OnCameraSizeChanged"/> fires.
    /// </summary>
    public void RefreshBackground()
    {
        CameraService cameraService = CameraService.Instance;
        if (background == null || cameraService == null)
            return;

        background.localPosition = Vector3.zero;
        background.localRotation = Quaternion.identity;
        background.localScale = new Vector3(
            cameraService.ScreenWidth / BackgroundPlaneSize,
            1f,
            cameraService.ScreenHeight / BackgroundPlaneSize * BackgroundDepthOverscan);
    }

    #endregion

    #region Item registry

    /// <summary>
    /// Adds an item that has been spawned onto the board. Items deregister when
    /// they enter the rack, get destroyed or get removed by a booster.
    /// </summary>
    public void RegisterItem(Item item)
    {
        if (item == null || itemRenderers.ContainsKey(item))
            return;

        int type = (int)item.ItemName;
        if (!itemsByType.TryGetValue(type, out List<Item> bucket))
        {
            bucket = new List<Item>();
            itemsByType.Add(type, bucket);
            buckets.Add(bucket);
        }

        bucket.Add(item);
        itemRenderers.Add(item, item.GetComponentInChildren<Renderer>());
        ItemCount++;
    }

    /// <summary>
    /// Removes an item from the board. Raises <see cref="OnBoardClear"/> when the
    /// last registered item leaves.
    /// </summary>
    public void DeregisterItem(Item item)
    {
        // A destroyed item still works as a key, so it can deregister from OnDestroy.
        if (ReferenceEquals(item, null) || !itemRenderers.Remove(item))
            return;

        if (itemsByType.TryGetValue((int)item.ItemName, out List<Item> bucket))
            bucket.Remove(item);

        ItemCount--;

        if (ItemCount == 0)
            OnBoardClear?.Invoke();
    }

    /// <summary>
    /// Every registered item, type by type. Enumerating with foreach does not
    /// allocate. Don't register or deregister while enumerating.
    /// </summary>
    public ItemEnumerable GetAllItems()
        => new ItemEnumerable(buckets);

    /// <summary>
    /// The registered items of one type. This is the live list: read it, don't
    /// modify it, and copy it first if you deregister while walking it.
    /// </summary>
    public List<Item> GetItemsOfType(int type)
        => itemsByType.TryGetValue(type, out List<Item> bucket) ? bucket : noItems;

    /// <summary>
    /// Highest top (bounds max y) among the registered items; 0 (the floor) when
    /// the board is empty.
    /// </summary>
    public float GetMaxY()
    {
        float maxY = 0f;

        foreach (KeyValuePair<Item, Renderer> pair in itemRenderers)
        {
            if (pair.Key == null)
                continue;

            float top = pair.Value != null
                ? pair.Value.bounds.max.y
                : pair.Key.transform.position.y;

            if (top > maxY)
                maxY = top;
        }

        return maxY;
    }

    public readonly struct ItemEnumerable : IEnumerable<Item>
    {
        private readonly List<List<Item>> buckets;

        public ItemEnumerable(List<List<Item>> buckets)
            => this.buckets = buckets;

        public Enumerator GetEnumerator()
            => new Enumerator(buckets);

        IEnumerator<Item> IEnumerable<Item>.GetEnumerator()
            => GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator()
            => GetEnumerator();
    }

    public struct Enumerator : IEnumerator<Item>
    {
        private readonly List<List<Item>> buckets;
        private int bucketIndex;
        private int itemIndex;

        public Enumerator(List<List<Item>> buckets)
        {
            this.buckets = buckets;
            bucketIndex = 0;
            itemIndex = -1;
            Current = null;
        }

        public Item Current { get; private set; }

        object IEnumerator.Current => Current;

        public bool MoveNext()
        {
            while (bucketIndex < buckets.Count)
            {
                List<Item> bucket = buckets[bucketIndex];
                if (++itemIndex < bucket.Count)
                {
                    Current = bucket[itemIndex];
                    return true;
                }

                bucketIndex++;
                itemIndex = -1;
            }

            Current = null;
            return false;
        }

        public void Reset()
        {
            bucketIndex = 0;
            itemIndex = -1;
            Current = null;
        }

        public void Dispose() { }
    }

    #endregion

    private void OnDrawGizmos()
    {
        Vector3 size = Application.isPlaying ? innerSize : new Vector3(Width, Height, Depth);
        float h = Mathf.Min(size.y, 15f);

        Gizmos.color = Color.cyan;
        Gizmos.DrawWireCube(new Vector3(0, h / 2, 0), new Vector3(size.x, h, size.z));

        Gizmos.color = Color.yellow;
        Gizmos.DrawWireCube(
            new Vector3(0, size.y / 2, EnterTriggerDepthInset / 2),
            new Vector3(size.x, size.y, size.z - EnterTriggerDepthInset));
    }
}
