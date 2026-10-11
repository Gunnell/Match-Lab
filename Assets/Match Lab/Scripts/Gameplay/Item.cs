using System.Collections.Generic;
using UnityEngine;
[RequireComponent(typeof(Rigidbody))]


public class Item : MonoBehaviour
{

    [Header(" Data ")]
    [SerializeField] private EItemName itemName;
    public EItemName ItemName => itemName;
    private ItemSpot spot;
    public ItemSpot Spot => spot;

    [SerializeField] private Sprite icon;
    public Sprite Icon => icon;

    [Tooltip("Size on the board relative to the prefab's scale, per item type. Applied once when the item is created.")]
    [SerializeField] private float boardSize = 1f;
    [Header(" Elements ")]
    [SerializeField] private Renderer renderer;
    [SerializeField] private Collider collider;

    private Material baseMaterial;
    private Vector3 baseScale;
    private Vector3 boardPosition;
    public Vector3 BoardPosition => boardPosition;
    private Quaternion boardRotation;
    public Quaternion BoardRotation => boardRotation;

    // Spring throw: the item flies in over the board's front wall, which it
    // ignores until it is inside the board (or a fallback timer ends it).
    private Collider ignoredThrowCollider;
    public bool IsBeingThrown { get; private set; }
    // [SerializeField] private Collider collider;

    private void Awake()
    {
        baseMaterial = renderer.material;
        transform.localScale *= boardSize;
        baseScale = transform.localScale;
    }

    /// <summary>
    /// The local scale the item had on the board, before it was shrunk onto a spot.
    /// </summary>
    public Vector3 BaseScale => baseScale;

    // Measured once per item type (every item of a type shares one prefab).
    private static readonly Dictionary<EItemName, Bounds> boardBoundsCache = new Dictionary<EItemName, Bounds>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => boardBoundsCache.Clear();

    /// <summary>
    /// The renderer's bounds in the item's own axes at board scale (X width, Y height),
    /// whatever the item's current rotation or scale. In the units of the level's
    /// item parent: multiply by its world scale for world units.
    /// </summary>
    public Bounds BoardBounds
    {
        get
        {
            if (boardBoundsCache.TryGetValue(itemName, out Bounds cached))
                return cached;

            Bounds local = renderer.localBounds;
            Vector3 min = local.min, max = local.max;
            Bounds bounds = default;

            for (int i = 0; i < 8; i++)
            {
                Vector3 corner = new Vector3((i & 1) == 0 ? min.x : max.x, (i & 2) == 0 ? min.y : max.y, (i & 4) == 0 ? min.z : max.z);
                // Item space at scale 1, then at board scale.
                Vector3 point = transform.InverseTransformPoint(renderer.transform.TransformPoint(corner));
                point = Vector3.Scale(point, baseScale);

                if (i == 0)
                    bounds = new Bounds(point, Vector3.zero);
                else
                    bounds.Encapsulate(point);
            }

            boardBoundsCache[itemName] = bounds;
            return bounds;
        }
    }

    /// <summary>
    /// Remembers how the item lay on the board, so Spring can send it back there.
    /// </summary>
    public void StoreBoardPose()
    {
        boardPosition = transform.position;
        boardRotation = transform.rotation;
    }

    public void AssignSpot(ItemSpot spot)
        => this.spot = spot;
    
    public void UnassignSpot()
        => spot = null;

    public void DisableShadows()
    {
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
    }
    
    public void EnableShadows()
    {
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
    }

    public void DisablePhysics()
    {
        // rig.isKinematic = true;
        // collider.enabled = false;
        GetComponent<Rigidbody>().isKinematic = true;
        collider.enabled = false;
        // A vacuumed or re-picked item must not keep a stale ignore.
        EndThrow();
    }
    
    public bool IsPhysicsEnabled => !GetComponent<Rigidbody>().isKinematic;

    public void EnablePhysics()
    {
        GetComponent<Rigidbody>().isKinematic = false;
        collider.enabled = true;
    }
    
    public void Hide()
        => renderer.enabled = false;

    public void Show()
        => renderer.enabled = true;

    public void BeginThrow(Collider throwCollider)
    {
        IsBeingThrown = true;

        if (throwCollider == null)
            return;

        Physics.IgnoreCollision(collider, throwCollider, true);
        ignoredThrowCollider = throwCollider;
    }

    public void EndThrow()
    {
        if (!IsBeingThrown)
            return;

        if (ignoredThrowCollider != null)
            Physics.IgnoreCollision(collider, ignoredThrowCollider, false);

        ignoredThrowCollider = null;
        IsBeingThrown = false;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (IsBeingThrown && BoardService.Instance != null && other == BoardService.Instance.Trigger)
            EndThrow();
    }

    public void Select(Material outlineMaterial)
    {
        renderer.materials = new Material[2] {baseMaterial, outlineMaterial};


    }

    public void Deselect()
    {
        renderer.materials = new Material[] {baseMaterial};
    }
    
}
