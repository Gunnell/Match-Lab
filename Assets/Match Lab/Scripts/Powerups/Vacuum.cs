using System;
using UnityEngine;

public class Vacuum : Powerup
{
    [Header(" Elements ")]
    [SerializeField] private Animator animator;
    
    [Header(" Actions ")] 
    public static Action started;
    

    private void TriggerPowerupStart()
    {
        started?.Invoke();
    }
    
    public void Play()
    {
        // Restart from 0. Play("Activate") alone keeps a running clip going,
        // so a tap late in the previous activation would never reach the
        // TriggerPowerupStart event again and the vacuum would stay busy.
        animator.Play("Activate", 0, 0f);
    }

    
}
