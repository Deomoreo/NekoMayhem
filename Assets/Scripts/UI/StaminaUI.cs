using UnityEngine;
using UnityEngine.UI;

public class StaminaUI : MonoBehaviour
{
    public Slider staminaSlider;
    //private CatDash playerDash;
    private CanvasGroup canvasGroup;

    private void Start()
    {
        //playerDash = FindObjectOfType<CatDash>(); 
        //staminaSlider.maxValue = playerDash.maxStamina;
        //staminaSlider.value = playerDash.maxStamina;

        canvasGroup = GetComponent<CanvasGroup>();
        canvasGroup.alpha = 0;
    }

    private void Update()
    {
        //if (playerDash != null)
        //{
        //    float staminaPercent = playerDash.GetCurrentStamina() / playerDash.maxStamina;
        //    staminaSlider.value = playerDash.GetCurrentStamina();

        //    if (staminaPercent < 1f)
        //    {
        //        canvasGroup.alpha = 1;
        //    }
        //    else
        //    {
        //        canvasGroup.alpha = 0;
        //    }
        //}
    }
}
