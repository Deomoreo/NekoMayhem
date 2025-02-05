using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class ScreenFader : MonoBehaviour
{
    // Immagine full‑screen da utilizzare per il fade (assicurarsi che copra tutto lo schermo)
    public Image fadeImage;
    public float fadeDuration = 0.5f;

    public IEnumerator FadeOut()
    {
        float timer = 0f;
        if (fadeImage != null)
        {
            Color c = fadeImage.color;
            c.a = 0f;
            fadeImage.color = c;
        }

        while (timer < fadeDuration)
        {
            timer += Time.deltaTime;
            if (fadeImage != null)
            {
                float alpha = Mathf.Lerp(0f, 1f, timer / fadeDuration);
                fadeImage.color = new Color(fadeImage.color.r, fadeImage.color.g, fadeImage.color.b, alpha);
            }
            yield return null;
        }

        if (fadeImage != null)
            fadeImage.color = new Color(fadeImage.color.r, fadeImage.color.g, fadeImage.color.b, 1f);
    }

    public IEnumerator FadeIn()
    {
        float timer = 0f;
        if (fadeImage != null)
        {
            Color c = fadeImage.color;
            c.a = 1f;
            fadeImage.color = c;
        }

        while (timer < fadeDuration)
        {
            timer += Time.deltaTime;
            if (fadeImage != null)
            {
                float alpha = Mathf.Lerp(1f, 0f, timer / fadeDuration);
                fadeImage.color = new Color(fadeImage.color.r, fadeImage.color.g, fadeImage.color.b, alpha);
            }
            yield return null;
        }

        if (fadeImage != null)
            fadeImage.color = new Color(fadeImage.color.r, fadeImage.color.g, fadeImage.color.b, 0f);
    }
}
