using UnityEngine;

/// <summary>
/// Owns the 3D gameplay UI (rack, booster buttons, later the top bar).
///
/// <code>
/// Gameplay UI                 this; faces the camera, just in front of the pile
///   ├─ Top Panel Container    (later)
///   └─ Bottom Panel Container
///        └─ Bottom Panel      (GameplayBottomPanel)
/// </code>
/// Every frame, after the camera tilt has been updated, the root takes the
/// camera pivot's rotation (local X = screen right, local Y = screen up,
/// local -Z = toward the camera) and the position from
/// <see cref="DistanceCalculator"/>, which only changes its depth.
/// </summary>
[DefaultExecutionOrder(-90)]
public class GameplayUIService : MonoBehaviour
{
    public static GameplayUIService Instance { get; private set; }

    [SerializeField] private Transform topPanelContainer;
    [SerializeField] private Transform bottomPanelContainer;
    [SerializeField] private GameplayBottomPanel bottomPanel;
    [Tooltip("Layers board items use. The UI is kept in front of these.")]
    [SerializeField] private LayerMask itemsMask;

    private DistanceCalculator distanceCalculator;

    public Transform TopPanelContainer => topPanelContainer;
    public Transform BottomPanelContainer => bottomPanelContainer;
    public GameplayBottomPanel BottomPanel => bottomPanel;
    public DistanceCalculator DistanceCalculator => distanceCalculator;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        if (itemsMask == 0)
            itemsMask = LayerMask.GetMask("Items");

        distanceCalculator = new DistanceCalculator(itemsMask);

        UpdatePose();
        bottomPanel.Initialize();
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    private void LateUpdate() => UpdatePose();

    private void UpdatePose()
    {
        if (CameraService.Instance == null)
            return;

        transform.SetPositionAndRotation(distanceCalculator.GetSafePosition(), CameraService.Instance.Pivot.rotation);
    }
}
