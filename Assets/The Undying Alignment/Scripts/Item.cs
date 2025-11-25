using UnityEngine;
[RequireComponent(typeof(Rigidbody))]


public class Item : MonoBehaviour
{

    [Header(" Elements ")]
    [SerializeField] private Renderer renderer;
    [SerializeField] private Collider collider;

    private Material baseMaterial;
    // [SerializeField] private Collider collider;

    private void Awake()
    {
        baseMaterial = renderer.material;
    }

    public void DisableShadows()
    {
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
    }

    public void DisablePhysics()
    {
        // rig.isKinematic = true;
        // collider.enabled = false;
        GetComponent<Rigidbody>().isKinematic = true;
        collider.enabled = false;
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
