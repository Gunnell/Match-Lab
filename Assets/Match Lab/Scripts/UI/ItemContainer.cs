using UnityEngine;

/// <summary>
/// One rack slot in the bottom panel.
///
/// <code>
/// Item Container N
///   └─ Animator          the part that bounces
///        ├─ Item Parent  slightly toward the camera
///        └─ View         holds the slot's ItemSpot (mesh, shine, flash, contact shadow)
/// </code>
/// The landing / merge bounce is played by the <see cref="ItemSpot"/> itself,
/// and rack items attach to that spot's own item parent.
/// </summary>
public class ItemContainer : MonoBehaviour
{
    [SerializeField] private int index;
    [SerializeField] private Transform animatorRoot;
    [SerializeField] private Transform itemParent;
    [SerializeField] private Transform view;
    [SerializeField] private ItemSpot spot;

    /// <summary>0 to 6, left to right.</summary>
    public int Index => index;
    public ItemSpot Spot => spot;
    public Transform AnimatorRoot => animatorRoot;
    public Transform ItemParent => itemParent;
    public Transform View => view;

    public Vector3 GetChildPosition() => itemParent.position;

    /// <summary>
    /// Sizes the slot art to <paramref name="width"/> world units across and tilts
    /// its top face <paramref name="tilt"/> degrees toward the camera. Only the
    /// View transform changes; the spot keeps its own transform.
    /// </summary>
    public void ApplyView(float width, float tilt)
    {
        view.localRotation = Quaternion.Euler(-tilt, 0f, 0f);

        Renderer art = spot != null ? spot.SlotRenderer : null;
        if (art == null)
            return;

        // Art width at a View scale of 1. The rig only rotates around X, so X scales stay separable.
        float viewScale = view.lossyScale.x;
        float unitWidth = art.localBounds.size.x * art.transform.lossyScale.x / viewScale;
        if (unitWidth > 0f)
            view.localScale = view.localScale / view.localScale.x * (width / unitWidth);
    }

    /// <summary>
    /// Where an item rests in this slot: <see cref="GetChildPosition"/> raised
    /// along screen-up by <paramref name="halfHeight"/> (half the item's rack height).
    /// </summary>
    public Vector3 GetSeatPosition(float halfHeight)
    {
        Quaternion rotation = CameraService.Instance != null
            ? CameraService.Instance.Pivot.rotation
            : transform.rotation;

        return GetChildPosition() + rotation * Vector3.up * halfHeight;
    }
}
