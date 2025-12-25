using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class GoalCard : MonoBehaviour
{
    [Header(" Elements ")]
    [SerializeField] private TextMeshProUGUI amountText;
    [SerializeField] private Image iconImage;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    public void Configure(int initialAmount, Sprite goalIcon)
    {
        amountText.text = initialAmount.ToString();
        iconImage.sprite = goalIcon;
        
    }

    public void UpdateAmount(int newAmount)
    {
        amountText.text = newAmount.ToString();
    }

    public void Complete()
    {
        gameObject.SetActive(false);
    }
}
