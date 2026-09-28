using UnityEngine;

/// <summary>
/// Clickable Freeze Gun powerup. The freeze itself lives in PowerUpManager;
/// this is the gun: a recoil pop when used and the muzzle the shot leaves from.
/// </summary>
public class FreezeGun : Powerup
{
    [Header(" Elements ")]
    [SerializeField] private Transform muzzle;
    public Transform Muzzle => muzzle != null ? muzzle : transform;

    private float baseScale;

    private void Awake()
    {
        baseScale = transform.localScale.x;
    }

    public void Play()
    {
        LeanTween.cancel(gameObject);
        transform.localScale = Vector3.one * baseScale;
        LeanTween.scale(gameObject, Vector3.one * baseScale * 1.2f, .12f)
            .setEase(LeanTweenType.easeOutQuad)
            .setLoopPingPong(1);
    }
}
