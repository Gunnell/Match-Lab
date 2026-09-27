using UnityEngine;

/// <summary>
/// Powerup is abstract and InputManager resolves clicks with
/// GetComponent&lt;Powerup&gt;(), so Spring needs a concrete component to sit on
/// the clickable collider. All of the behaviour lives in PowerUpManager.
/// </summary>
public class Spring : Powerup
{
    
}
