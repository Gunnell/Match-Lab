using UnityEngine;
using System;
public class InputManager : MonoBehaviour
{
    public static Action<Item> itemClicked;

    [Header(" Settings ")]
    [SerializeField] private Material outlineMaterial;
    private Item currentItem;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        if(GameManager.instance.IsGame())
            HandleControl();
        
    }

    private void HandleControl()
    {
        if(Input.GetMouseButton(0))
        {
            HandleDrag();
        }
        else if(Input.GetMouseButtonUp(0))
        {
            HandleMouseUp();
        }
    }

    private void HandleDrag()
    {
        // Handle the mouse down event here

        Physics.Raycast(Camera.main.ScreenPointToRay(Input.mousePosition), out RaycastHit hit, 100);

        if(hit.collider == null)
        {
            DeselectCurrentItem();
            return;
        }

        if(hit.collider.transform.parent == null)
        {
            return;
        }

        if(!hit.collider.transform.parent.TryGetComponent(out Item item))
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
