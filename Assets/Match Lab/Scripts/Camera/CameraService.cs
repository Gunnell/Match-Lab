using System;
using UnityEngine;

/// <summary>
/// Orthographic gameplay camera fitted to the board.
///
/// Rig: this component sits on <c>CameraPivot</c> at the board floor centre.
/// The camera is a child 100 units back along the pivot's -Z, and the pivot
/// tilts around X, so the camera always looks at the board centre.
/// <c>Container</c>, a child of the camera created here, holds world objects
/// that stay fixed on screen (rack, top bar, powerup buttons). Place them with
/// <see cref="GetTopPosition"/> / <see cref="GetBottomPosition"/>, which are
/// camera-local Y.
///
/// The orthographic size fits <see cref="FitWidth"/> units across on tall
/// screens and holds a preset size on wider ones, so the board always fits
/// the screen width.
/// </summary>
[DefaultExecutionOrder(-110)]
public class CameraService : MonoBehaviour
{
    // World units always visible across: the board plus a small margin.
    public const float FitWidth = 9.5f;
    public const float MinSize = 9.75f;
    // Size held on wider screens, per ratio class 0, 1, 2.
    private static readonly float[] PresetSize = { 9.85f, 9.85f, 9.75f };
    private const float WideSize = 9.85f;

    // Lower bounds of aspect (width / height) for ratio classes 0..3; below the last is class 4.
    private static readonly float[] RatioClassMinAspect = { .805f, .65f, .5075f, .37357f };

    // Tilt around X: low pile -> MinRotation (board seen at an angle),
    // pile PileHeightRange above PileHeightStart -> MaxRotation (almost top-down).
    public const float MinRotation = 71.5f;
    public const float MaxRotation = 89f;
    private const float PileHeightStart = 2f;
    private const float PileHeightRange = 15f;
    // Per update, so the tilt drifts imperceptibly as the pile changes.
    private const float MaxRotationStep = .002f;

    private const float CameraDistance = 100f;
    private const float NearClip = -10f;   // negative: keeps things slightly behind the camera plane visible
    private const float FarClip = 150f;

    // Black quads just outside the view, for devices that show pixels past the camera rect.
    private const float BlockerScale = 100f;
    private const float BlockerOffset = 50f;

    public static CameraService Instance { get; private set; }

    /// <summary>Raised when <see cref="ScreenWidth"/>, <see cref="ScreenHeight"/> or a safe-area offset changes.</summary>
    public static event Action OnCameraSizeChanged;

    [SerializeField] private Camera cam;
    [Tooltip("Black quads just outside the view edges, so nothing past the camera rect shows on devices that draw there.")]
    [SerializeField] private bool viewBlockers = true;

    private Transform container;
    private Transform[] blockers;
    private float currentRotation;

    // Screen state the current size was computed for.
    private int lastScreenWidth, lastScreenHeight;
    private Rect lastSafeArea;

    public Camera Camera => cam;
    /// <summary>The tilting rig root (this transform); the camera looks along its forward.</summary>
    public Transform Pivot => transform;

    /// <summary>World direction that reads as up on screen.</summary>
    public static Vector3 ScreenUp => Instance != null ? Instance.transform.up : Vector3.forward;
    /// <summary>World direction toward the camera.</summary>
    public static Vector3 TowardCamera => Instance != null ? -Instance.transform.forward : Vector3.up;
    public Transform Container => container;

    /// <summary>World units, full view height.</summary>
    public float ScreenHeight { get; private set; }
    /// <summary>World units, full view width.</summary>
    public float ScreenWidth { get; private set; }
    public float PixelPerUnit { get; private set; }
    /// <summary>Notch height in world units.</summary>
    public float TopSafeOffset { get; private set; }
    /// <summary>Home-bar height in world units.</summary>
    public float BottomSafeOffset { get; private set; }
    public int RatioClass { get; private set; }
    /// <summary>Current pivot tilt around X, in degrees.</summary>
    public float CurrentRotation => currentRotation;

    public bool IsTopSafeAreaExist => TopSafeOffset > 0f;
    public bool IsBottomSafeAreaExist => BottomSafeOffset > 0f;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        Initialize();
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    private void Update()
    {
        if (ScreenStateChanged())
            FitToScreen();

        // No game loop drives the tilt yet, so the service does it itself.
        UpdateCameraRotation();
    }

    public void Initialize()
    {
        if (cam == null)
            cam = GetComponentInChildren<Camera>();

        transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);

        Transform camTransform = cam.transform;
        camTransform.SetParent(transform, false);
        camTransform.localPosition = new Vector3(0, 0, -CameraDistance);
        camTransform.localRotation = Quaternion.identity;
        cam.orthographic = true;
        cam.nearClipPlane = NearClip;
        cam.farClipPlane = FarClip;

        container = camTransform.Find("Container");
        if (container == null)
        {
            container = new GameObject("Container").transform;
            container.SetParent(camTransform, false);
        }

        if (viewBlockers)
            CreateViewBlockers();

        FitToScreen();
        SnapCameraRotation();
    }

    #region Size

    /// <summary>Recomputes the orthographic size for the current screen.</summary>
    public void FitToScreen()
    {
        lastScreenWidth = Screen.width;
        lastScreenHeight = Screen.height;
        lastSafeArea = Screen.safeArea;

        RatioClass = GetRatioClass(Screen.width / (float)Screen.height);
        SetCameraSize(GetFittedSize(cam.aspect, RatioClass));
    }

    /// <summary>
    /// Sets the orthographic size and the values derived from it. Raises
    /// <see cref="OnCameraSizeChanged"/> when the view size or a safe-area offset changes.
    /// </summary>
    public void SetCameraSize(float size)
    {
        float oldWidth = ScreenWidth, oldHeight = ScreenHeight;
        float oldTopSafe = TopSafeOffset, oldBottomSafe = BottomSafeOffset;

        cam.orthographicSize = size;
        ScreenHeight = 2f * size;
        ScreenWidth = ScreenHeight * cam.aspect;
        PixelPerUnit = Screen.height / ScreenHeight;

        Rect safeArea = Screen.safeArea;
        TopSafeOffset = (Screen.height - safeArea.yMax) / PixelPerUnit;
        BottomSafeOffset = safeArea.yMin / PixelPerUnit;

        PlaceViewBlockers();

        if (!Mathf.Approximately(oldWidth, ScreenWidth) || !Mathf.Approximately(oldHeight, ScreenHeight)
            || !Mathf.Approximately(oldTopSafe, TopSafeOffset) || !Mathf.Approximately(oldBottomSafe, BottomSafeOffset))
            OnCameraSizeChanged?.Invoke();
    }

    /// <summary>
    /// Orthographic size for an aspect: exactly <see cref="FitWidth"/> across on
    /// tall screens, a preset size on wider ones.
    /// </summary>
    public static float GetFittedSize(float aspect, int ratioClass)
    {
        float s = FitWidth / (2f * aspect);

        if (s >= MinSize)
            return s >= WideSize || ratioClass > 1 ? s : WideSize;

        return ratioClass < 3 ? PresetSize[ratioClass] : s;
    }

    /// <summary>0 tablets, 1 wide phones, 2 16:9 phones, 3 tall phones, 4 very tall.</summary>
    public static int GetRatioClass(float aspect)
    {
        for (int i = 0; i < RatioClassMinAspect.Length; i++)
            if (aspect >= RatioClassMinAspect[i])
                return i;

        return RatioClassMinAspect.Length;
    }

    /// <summary>Camera-local Y of the top of the view, below the notch if <paramref name="useSafeArea"/>.</summary>
    public float GetTopPosition(bool useSafeArea)
        => ScreenHeight / 2f - (useSafeArea ? TopSafeOffset : 0f);

    /// <summary>Camera-local Y of the bottom of the view, above the home bar if <paramref name="useSafeArea"/>.</summary>
    public float GetBottomPosition(bool useSafeArea)
        => -ScreenHeight / 2f + (useSafeArea ? BottomSafeOffset : 0f);

    private bool ScreenStateChanged()
        => Screen.width != lastScreenWidth
        || Screen.height != lastScreenHeight
        || Screen.safeArea != lastSafeArea;

    #endregion

    #region Tilt

    /// <summary>Tilt the current pile asks for.</summary>
    public float GetTargetRotation()
    {
        float pileHeight = BoardService.Instance != null ? BoardService.Instance.GetMaxY() : 0f;
        return MinRotation + (MaxRotation - MinRotation) * Mathf.Clamp01((pileHeight - PileHeightStart) / PileHeightRange);
    }

    /// <summary>Jumps straight to the target tilt. Call at level start.</summary>
    public void SnapCameraRotation()
    {
        currentRotation = GetTargetRotation();
        ApplyRotation();
    }

    /// <summary>
    /// Moves the tilt toward the target by at most <see cref="MaxRotationStep"/>
    /// degrees. Does nothing while the game is paused or physics is stopped.
    /// </summary>
    public void UpdateCameraRotation()
    {
        if (Time.timeScale <= 0f || Physics.simulationMode == SimulationMode.Script)
            return;

        float target = GetTargetRotation();
        if (Mathf.Approximately(currentRotation, target))
            return;

        currentRotation = Mathf.MoveTowards(currentRotation, target, MaxRotationStep);
        ApplyRotation();
    }

    private void ApplyRotation()
        => transform.localRotation = Quaternion.Euler(currentRotation, 0f, 0f);

    #endregion

    #region View blockers

    private void CreateViewBlockers()
    {
        if (blockers != null)
            return;

        Texture2D texture = new Texture2D(1, 1);
        texture.SetPixel(0, 0, Color.white);
        texture.Apply();
        Sprite sprite = Sprite.Create(texture, new Rect(0, 0, 1, 1), new Vector2(.5f, .5f), 1f);

        SortingLayer[] layers = SortingLayer.layers;
        int topSortingLayer = layers[layers.Length - 1].id;

        blockers = new Transform[4];
        for (int i = 0; i < blockers.Length; i++)
        {
            SpriteRenderer blocker = new GameObject("View Blocker").AddComponent<SpriteRenderer>();
            blocker.sprite = sprite;
            blocker.color = Color.black;
            blocker.sortingLayerID = topSortingLayer;
            blocker.sortingOrder = short.MaxValue;
            blocker.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            blocker.receiveShadows = false;

            blockers[i] = blocker.transform;
            blockers[i].SetParent(container, false);
            blockers[i].localScale = Vector3.one * BlockerScale;
        }
    }

    private void PlaceViewBlockers()
    {
        if (blockers == null)
            return;

        float x = ScreenWidth / 2f + BlockerOffset;
        float y = ScreenHeight / 2f + BlockerOffset;

        blockers[0].localPosition = new Vector3(x, 0, 0);
        blockers[1].localPosition = new Vector3(-x, 0, 0);
        blockers[2].localPosition = new Vector3(0, y, 0);
        blockers[3].localPosition = new Vector3(0, -y, 0);
    }

    #endregion
}
