using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class MessagePanel : MonoBehaviour
{
    public GameObject messagePanel;
    public Text messageText;
    private float fadeOutDelay = 4f;
    private float fadeDuration = 1f;
    private CanvasGroup canvasGroup;


    private static MessagePanel instance;

    // Start is called before the first frame update
    void OnEnable()
    {
        messagePanel.SetActive(true);

        if (messagePanel != null)
        {
            canvasGroup = messagePanel.GetComponent<CanvasGroup>();

            if (canvasGroup == null)
            {
                canvasGroup = messagePanel.AddComponent<CanvasGroup>();
            }
            
            canvasGroup.alpha = 0; // panel hidden
        }
    }

    // Show the panel with dynamic text
    public void ShowMessage(string message) 
    { 
        if(messagePanel != null)
        {

            if (!messagePanel.activeSelf)
            {
                messagePanel.SetActive(true);
            }

            messageText.text = message;
            StartCoroutine(ManageFading());
        }
    }

    private IEnumerator ManageFading()
    {
        // fade in
        yield return StartCoroutine(FadeIn());

        // wait timer
        yield return new WaitForSeconds(fadeOutDelay);

        // fade out
        yield return StartCoroutine(FadeOut());
    }

    private IEnumerator FadeIn()
    {
        while (canvasGroup.alpha < 1)
        {
            canvasGroup.alpha += Time.deltaTime / fadeDuration;
            yield return null;
        }
    }

    private IEnumerator FadeOut()
    {
        while (canvasGroup.alpha > 0)
        {
            canvasGroup.alpha -= Time.deltaTime / fadeDuration;
            yield return null;
        }

        canvasGroup.alpha = 0;
    }
}
