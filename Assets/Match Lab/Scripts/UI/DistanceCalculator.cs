using UnityEngine;

/// <summary>
/// Finds the depth for the gameplay UI: just in front of (toward the camera
/// from) the highest items, on the camera's centre ray.
///
/// The camera is orthographic, so sliding a point along its centre ray only
/// changes its depth, never where it shows on screen. The UI therefore stays
/// put on screen while always being drawn in front of the pile.
/// </summary>
public class DistanceCalculator
{
    // Search starts this far along the camera's forward from the camera.
    private const float HeightCheckOffset = 37.25f;
    // Step along the camera's forward per probe.
    private const float StageDistance = 1.5f;
    // Stop once the probe point is this low (world Y): nothing to clear below.
    private const float MinHeightToCheck = 4.5f;
    // The UI sits this far in front of the pile top, toward the camera.
    private const float SafePositionOffset = 2.15f;
    // Probe slab, aligned with the camera.
    private static readonly Vector3 CheckBoxHalfExtents = new Vector3(5f, 8f, .75f);

    private readonly int itemsMask;

    private Vector3 safePosition;
    private int lastFrame = -1;

    public DistanceCalculator(int itemsMask)
    {
        this.itemsMask = itemsMask;
    }

    /// <summary>
    /// World position for the UI root. Computed once per frame; later calls in
    /// the same frame return the cached value.
    /// </summary>
    public Vector3 GetSafePosition()
    {
        if (lastFrame == Time.frameCount)
            return safePosition;

        lastFrame = Time.frameCount;

        CameraService cameraService = CameraService.Instance;
        if (cameraService == null)
            return safePosition;

        Transform cam = cameraService.Camera.transform;
        Vector3 forward = cam.forward;
        Quaternion rotation = cameraService.Pivot.rotation;

        Vector3 p = cam.position + forward * HeightCheckOffset;
        Vector3 top;

        while (true)
        {
            if (p.y <= MinHeightToCheck)
            {
                top = p;
                break;
            }

            p += forward * StageDistance;

            if (Physics.CheckBox(p, CheckBoxHalfExtents, rotation, itemsMask, QueryTriggerInteraction.Ignore))
            {
                top = p;
                break;
            }
        }

        safePosition = top - forward * SafePositionOffset;
        return safePosition;
    }
}
