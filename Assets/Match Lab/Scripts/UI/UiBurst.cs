using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Particle-like burst made of UI images, for Screen Space Overlay canvases
/// where ParticleSystems don't render. Each dot flies outward while fading and
/// shrinking, then is destroyed. Dots never block raycasts.
/// </summary>
public static class UiBurst
{
    /// <param name="parent">Where the dots live (use one that doesn't scale or spin with the source).</param>
    /// <param name="localPosition">Burst centre in parent space.</param>
    /// <param name="arc">Emission arc in degrees (x = from, y = to); 0 = right, 90 = up, 270 = down.</param>
    public static void Play(RectTransform parent, Vector2 localPosition, Sprite sprite, Color color,
        int count, Vector2 distance, Vector2 arc, float size, float duration)
    {
        if (parent == null)
            return;

        for (int i = 0; i < count; i++)
        {
            GameObject dot = new GameObject("Burst Dot", typeof(RectTransform), typeof(Image));
            RectTransform rect = (RectTransform)dot.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = parent.pivot;
            rect.sizeDelta = Vector2.one * size * Random.Range(.7f, 1.2f);
            rect.anchoredPosition = localPosition;
            rect.SetAsLastSibling();

            Image image = dot.GetComponent<Image>();
            image.sprite = sprite;
            image.color = color;
            image.raycastTarget = false;

            float angle = Random.Range(arc.x, arc.y) * Mathf.Deg2Rad;
            Vector2 target = localPosition + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * Random.Range(distance.x, distance.y);
            float life = duration * Random.Range(.8f, 1.1f);

            LeanTween.value(dot, 0f, 1f, life)
                .setEase(LeanTweenType.easeOutSine)
                .setOnUpdate((float t) =>
                {
                    rect.anchoredPosition = Vector2.Lerp(localPosition, target, t);
                    rect.localScale = Vector3.one * (1 - t * .8f);
                    Color c = color;
                    c.a = color.a * (1 - t);
                    image.color = c;
                })
                .setOnComplete(() => Object.Destroy(dot));
        }
    }

    // Converts a point from one RectTransform's space into another's.
    public static Vector2 ToLocal(RectTransform from, Vector2 point, RectTransform to)
        => to.InverseTransformPoint(from.TransformPoint(point));
}
