using System;
using UnityEngine;

public class Vacuum : Powerup
{
    [Header(" Elements ")]
    [SerializeField] private Animator animator;
    [SerializeField] private float finishBlend = .25f;

    [Header(" Actions ")] 
    public static Action started;
    

    // Called by the Activate clip. Not used by PowerUpManager anymore.
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

    // Stop sucking once everything is in, instead of finishing the clip.
    public void Finish()
    {
        animator.CrossFadeInFixedTime("Idle", finishBlend, 0);
    }

    
}
