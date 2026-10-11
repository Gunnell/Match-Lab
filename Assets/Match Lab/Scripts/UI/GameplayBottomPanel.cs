using NaughtyAttributes;
using UnityEngine;

/// <summary>
/// The rack (7 item containers) and the booster buttons, anchored to the
/// bottom edge of the safe area. Local X is screen right, local Y screen up,
/// local -Z toward the camera.
///
/// <code>
/// Bottom Panel
///   ├─ Match Container              (0, 3.08, 0)    the rack, 7 slots 1.29 apart
///   │    └─ Item Containers
///   │         └─ Item Container 0..6
///   └─ Booster Container            (0, 1.15, 0)    clear gap under the rack
///        └─ In-Game Booster Container
///             ├─ Background
///             └─ Vacuum / Spring / Fan / Freezer Slot   (z -2, in front)
/// </code>
/// </summary>
public class GameplayBottomPanel : MonoBehaviour
{
    [Header(" Rack ")]
    [Tooltip("Left to right.")]
    [SerializeField] private ItemContainer[] itemContainers;

    [Header(" Booster Buttons ")]
    [SerializeField] private Transform vacuumSlot;
    [SerializeField] private Transform springSlot;
    [SerializeField] private Transform fanSlot;
    [SerializeField] private Transform freezerSlot;
    [SerializeField] private Transform boosterBackground;

    [Header(" View ")]
    [Tooltip("Slot art width as a share of the slot spacing, leaving a gap between slots.")]
    [SerializeField, Range(.3f, 1f)] private float slotWidthFraction = .77f;
    [Tooltip("Degrees the slots' top faces tilt toward the camera, so they're seen slightly from above.")]
    [SerializeField] private float slotViewTilt = 16f;
    [Tooltip("Degrees the powerups (and their background) tilt toward the camera, so they're seen slightly from above.")]
    [SerializeField] private float powerupViewTilt = 21f;

    private bool initialized;

    /// <summary>Index 0 to 6, left to right.</summary>
    public ItemContainer[] ItemContainers => itemContainers;
    public Transform VacuumSlot => vacuumSlot;
    public Transform SpringSlot => springSlot;
    public Transform FanSlot => fanSlot;
    public Transform FreezerSlot => freezerSlot;

    public void Initialize()
    {
        if (initialized)
            return;

        initialized = true;
        ApplyView();
        CameraService.OnCameraSizeChanged += Place;
        Place();
    }

    /// <summary>Slot size and tilt, powerup tilt. Transforms of the panel's own nodes only.</summary>
    [Button]
    public void ApplyView()
    {
        if (itemContainers == null || itemContainers.Length == 0)
            return;

        // Average over the row (the layout rounds individual gaps).
        int last = itemContainers.Length - 1;
        float spacing = last > 0
            ? Mathf.Abs(itemContainers[last].transform.localPosition.x - itemContainers[0].transform.localPosition.x) / last
            : 1f;

        for (int i = 0; i < itemContainers.Length; i++)
            itemContainers[i].ApplyView(spacing * slotWidthFraction, slotViewTilt);

        Quaternion powerupTilt = Quaternion.Euler(-powerupViewTilt, 0f, 0f);
        foreach (Transform t in new[] { vacuumSlot, springSlot, fanSlot, freezerSlot, boosterBackground })
            if (t != null)
                t.localRotation = powerupTilt;
    }

    private void OnDestroy()
    {
        if (initialized)
            CameraService.OnCameraSizeChanged -= Place;
    }

    /// <summary>Puts the panel origin on the bottom edge of the safe area.</summary>
    public void Place()
    {
        if (CameraService.Instance != null)
            transform.localPosition = Vector3.up * CameraService.Instance.GetBottomPosition(useSafeArea: true);
    }

    /// <summary>The slot whose <see cref="ItemContainer.GetChildPosition"/> is nearest to <paramref name="worldPos"/>.</summary>
    public ItemContainer FindClosestItemContainer(Vector3 worldPos)
    {
        ItemContainer closest = null;
        float best = float.MaxValue;

        for (int i = 0; i < itemContainers.Length; i++)
        {
            float d = (itemContainers[i].GetChildPosition() - worldPos).sqrMagnitude;
            if (d < best)
            {
                best = d;
                closest = itemContainers[i];
            }
        }

        return closest;
    }
}
