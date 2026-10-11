using UnityEngine;
using System;
public class InputManager : MonoBehaviour
{
    public static Action<Item> itemClicked;
    public static Action<Powerup> powerupClicked;

    // Held while a powerup needs the board to itself. A counter, so one
    // powerup finishing never releases another powerup's lock.
    private static int lockCount;
    public static bool IsLocked => lockCount > 0;

    public static void Lock() => lockCount++;
    public static void Unlock() => lockCount = Mathf.Max(0, lockCount - 1);


    [Header(" Settings ")]
    [SerializeField] private Material outlineMaterial;
    [SerializeField] private LayerMask powerupLayer;
    [Tooltip("Board items only. Never the board walls or the enter trigger, which sit between the camera and the items.")]
    [SerializeField] private LayerMask itemLayer;
    private Item currentItem;

    // The camera is far back (orthographic, ~100 units from the board), so
    // tap rays are never cut short.
    private const float RayDistance = Mathf.Infinity;

    private void Awake()
    {
        // Static, so it would survive a scene reload mid-lock.
        lockCount = 0;

        if (itemLayer == 0)
            itemLayer = LayerMask.GetMask("Items");
    }

    // Update is called once per frame
    void Update()
    {
        if(GameManager.instance.IsGame())
            HandleControl();
        
    }

    private void HandleControl()
    {
        if (IsLocked)
        {
            DeselectCurrentItem();
            return;
        }

        if (Input.GetMouseButtonDown(0))
        {
            HandleMouseDown();
        }
        else if(Input.GetMouseButton(0))
        {
            HandleDrag();
        }

        // Not chained to the checks above: a tap can press and release in
        // the same frame, and the release must still be handled.
        if(Input.GetMouseButtonUp(0))
        {
            HandleMouseUp();
        }
    }

    private void HandleMouseDown()
    {
        Physics.Raycast(Camera.main.ScreenPointToRay(Input.mousePosition), out RaycastHit hit, RayDistance, powerupLayer);

        if (hit.collider != null && hit.collider.TryGetComponent(out Powerup powerup))
        {
            powerupClicked?.Invoke(powerup);
            return;
        }

        // Select on press too, otherwise a quick tap that never reaches a
        // drag frame selects nothing and the release is ignored.
        HandleDrag();
    }

    private void HandleDrag()
    {

        Physics.Raycast(Camera.main.ScreenPointToRay(Input.mousePosition), out RaycastHit hit, RayDistance, itemLayer);

        if(hit.collider == null)
        {
            DeselectCurrentItem();
            return;
        }

        if(hit.collider.transform.parent == null)
        {
            return;
        }

        // Mid-air Spring throws can't be picked until they land in the board.
        if(!hit.collider.transform.parent.TryGetComponent(out Item item) || item.IsBeingThrown)
        {
            DeselectCurrentItem();
            return;
        }

        DeselectCurrentItem();

        currentItem = item;
        currentItem.Select(outlineMaterial);

        // itemClicked?.Invoke(item);

    }
    private void HandleMouseUp()
    {
        if(currentItem == null)
            return;

        currentItem.Deselect();
        itemClicked?.Invoke(currentItem);
        currentItem = null;
    }

    private void DeselectCurrentItem()
    {
        if(currentItem != null)
        {
            currentItem.Deselect();
            currentItem = null;
        }
    }
}
