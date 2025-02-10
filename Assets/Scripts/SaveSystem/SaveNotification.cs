using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class SaveNotification : MonoBehaviour
{
    public Text messageText; 
    private float displayDuration = 3f;
    private float fadeDuration = 1f;
    private CanvasGroup canvasGroup;

    void Start()
    {
        gameObject.SetActive(false);

        if (gameObject != null)
        {
            canvasGroup = gameObject.GetComponent<CanvasGroup>();

            if (canvasGroup == null)
            {
                canvasGroup = gameObject.AddComponent<CanvasGroup>();
                canvasGroup.alpha = 0f;
            }

        }
    }

    public void ShowMessage(string message)
    {
        canvasGroup.alpha = 1;
        messageText.text = message;
        gameObject.SetActive(true);
        StartCoroutine(FadeOutAfterDelay());
    }


    private IEnumerator FadeOutAfterDelay()
    {
        yield return new WaitForSeconds(displayDuration); // Wait before fading

        float elapsedTime = 0f;
        float startAlpha = canvasGroup.alpha;

        while (elapsedTime < fadeDuration)
        {
            elapsedTime += Time.deltaTime;
            canvasGroup.alpha = Mathf.Lerp(startAlpha, 0, elapsedTime / fadeDuration);
            yield return null;
        }

        gameObject.SetActive(false); // Hide after fading
    }
}
