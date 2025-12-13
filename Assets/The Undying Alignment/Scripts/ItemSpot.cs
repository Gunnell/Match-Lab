using UnityEngine;

public class ItemSpot : MonoBehaviour
{
    [Header(" Elements ")] 
    [SerializeField] private Animator animator;
    [SerializeField] private Transform itemParent;

    [Header(" Settings ")]
    private Item item;
    public Item Item => item;
   

    public void Populate(Item item)
    {
        this.item = item;
        item.transform.SetParent(itemParent);
        item.AssignSpot(this);

        
    }

    public bool IsEmpty() => item == null;

    public void Clear()
    {
        item = null;
    }

    public void BumpDown()
    {
        animator.Play("Bump", 0, 0);
        
    }

}
