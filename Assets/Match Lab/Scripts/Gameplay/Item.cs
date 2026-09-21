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
    [Header(" Elements ")]
    [SerializeField] private Renderer renderer;
    [SerializeField] private Collider collider;

    private Material baseMaterial;
    private Vector3 baseScale;
    // [SerializeField] private Collider collider;

    private void Awake()
    {
        baseMaterial = renderer.material;
        baseScale = transform.localScale;
    }

    /// <summary>
    /// Undoes the shrink applied when the item was moved onto a spot.
    /// Call after reparenting, the parent's scale feeds into localScale.
    /// </summary>
    public void RestoreScale()
        => transform.localScale = baseScale;

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
    }
    
    public void EnablePhysics()
    {
        GetComponent<Rigidbody>().isKinematic = false;
        collider.enabled = true;
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
