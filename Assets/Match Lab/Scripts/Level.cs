using UnityEngine;

public class  Level : MonoBehaviour
{
    [Header(" Elements ")]
    [SerializeField] private ItemPlacer itemPlacer;
    public Transform ItemParent => itemPlacer.transform;
    
    [Header(" Settings ")]
    [SerializeField]  private int duration;
    public int Duration => duration;

    public Item[] GetItems()
    {
        return itemPlacer.GetItems();
    }
    
    

    public ItemLevelData[] GetGoals()
        => itemPlacer.GetGoals();

}
