using UnityEngine;

public class ItemSpot : MonoBehaviour
{

    [Header("Settings")]
    private Item item;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    public void Populate(Item item)
    {
        this.item = item;
        item.transform.SetParent(transform);

        
    }

    public bool IsEmpty() => item == null;

}
