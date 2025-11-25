using UnityEngine;
using System;

public class ItemSpotsManager : MonoBehaviour
{
    [Header (" Elements ")]
    [SerializeField] private Transform itemSpotsParent;
    private ItemSpot[] spots;

    [Header (" Settings ")]
    [SerializeField] private Vector3 itemLocalPositionOnSpot;
    [SerializeField] private Vector3 itemLocalScaleOnSpot;

    private void Awake()
    {
        InputManager.itemClicked += OnItemClicked;
        StoreSpots();
    }

    private void OnDestroy()
    {
        InputManager.itemClicked -= OnItemClicked;
    }




    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    private void OnItemClicked(Item item){

        if(!IsFreeSpotAvailable()){
            Debug.Log("No free item spot available!");
            return;
        }

        HandleItemClicked(item);
            

        
        

    }  

    private void HandleItemClicked(Item item)
    {
        MoveItemToFirstFreeSpot(item);
    } 

    private void MoveItemToFirstFreeSpot(Item item)
    {
        ItemSpot targetSpot = GetFreeSpot();
        targetSpot.Populate(item);


        item.transform.localPosition = itemLocalPositionOnSpot;
        item.transform.localScale = itemLocalScaleOnSpot;
        item.transform.localRotation = Quaternion.identity;
        item.DisableShadows();
        item.DisablePhysics();

    }

    private ItemSpot GetFreeSpot()
    {
        for(int i = 0; i < spots.Length; i++)
        {
            if(spots[i].IsEmpty())
                return spots[i];
            
        };

        return null;
        
    }

    private void StoreSpots()
    {
        spots = new ItemSpot[itemSpotsParent.childCount];

        for(int i = 0; i < itemSpotsParent.childCount; i++)
            spots[i] = itemSpotsParent.GetChild(i).GetComponent<ItemSpot>();
    }

    private bool IsFreeSpotAvailable()
    {
        for(int i = 0; i < spots.Length; i++)
        {
            if(spots[i].IsEmpty())
                return true;
        }
        return false;
        
    }
}

