using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class GoalCard : MonoBehaviour
{
    [Header(" Elements ")]
    [SerializeField] private TextMeshProUGUI amountText;
    [SerializeField] private Image iconImage;
    [SerializeField] private GameObject backOfCard;
    [SerializeField] private GameObject checkMark;
    [SerializeField] private Animator completeAnimator;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        completeAnimator.enabled = false;
    }

    // Update is called once per frame
    void Update()
    {
        backOfCard.SetActive(Vector3.Dot(transform.forward, Vector3.forward) < 0);
    }

    public void Configure(int initialAmount, Sprite goalIcon)
    {
        amountText.text = initialAmount.ToString();
        iconImage.sprite = goalIcon;
        
    }

    public void UpdateAmount(int newAmount)
    {
        amountText.text = newAmount.ToString();
        Bump();
    }

    private void Bump()
    {
        LeanTween.cancel(gameObject);
        transform.localScale = Vector3.one;
        LeanTween.scale(gameObject, Vector3.one * 1.1f, .1f).setLoopPingPong(1);
    }

    public void Complete()
    {
        completeAnimator.enabled = true;

        checkMark.SetActive(true);
        amountText.gameObject.SetActive(false);
        
        completeAnimator.Play("Complete");
        
    }
}
