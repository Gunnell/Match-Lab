using UnityEngine;

public enum EPowerupType
{
    Vacuum = 0,
    Spring = 1,
    Fan = 2,
    FreezeGun = 3
}

public abstract class Powerup : MonoBehaviour
{
    [Header(" Settings ")]
    [SerializeField] private EPowerupType type;
    public EPowerupType Type => type;
    
}
