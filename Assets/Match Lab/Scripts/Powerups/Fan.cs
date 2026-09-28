using UnityEngine;

/// <summary>
/// Clickable Fan powerup. The shuffle
/// itself lives in PowerUpManager; this animates the fan: a pop on
/// activation, the rotor spinning for the length of the shuffle, and an
/// optional wind particle effect.
/// </summary>
public class Fan : Powerup
{
    [Header(" Elements ")]
    [SerializeField] private Transform rotor;
    [SerializeField] private ParticleSystem windParticles;

    [Header(" Settings ")]
    [SerializeField] private float maxSpinSpeed = 1800f;
    [SerializeField] private float spinUpDuration = .2f;
    [SerializeField] private float spinDownDuration = .5f;

    private float spinTimer;
    private float spinDuration;
    private float baseScale;

    private void Awake()
    {
        baseScale = transform.localScale.x;
    }

    public void Play(float duration)
    {
        spinDuration = duration;
        spinTimer = 0;

        LeanTween.cancel(gameObject);
        transform.localScale = Vector3.one * baseScale;
        LeanTween.scale(gameObject, Vector3.one * baseScale * 1.2f, .12f)
            .setEase(LeanTweenType.easeOutQuad)
            .setLoopPingPong(1);
    }

    public void StartWind()
    {
        if (windParticles != null)
            windParticles.Play();
    }

    public void StopWind()
    {
        if (windParticles != null)
            windParticles.Stop();
    }

    private void Update()
    {
        if (rotor == null || spinTimer >= spinDuration)
            return;

        spinTimer += Time.deltaTime;

        float up = Mathf.Clamp01(spinTimer / spinUpDuration);
        float down = Mathf.Clamp01((spinDuration - spinTimer) / spinDownDuration);
        float speed = maxSpinSpeed * Mathf.Min(up, down);

        rotor.Rotate(Vector3.up, speed * Time.deltaTime, Space.Self);
    }
}
