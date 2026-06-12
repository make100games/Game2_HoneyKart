using UnityEngine;
using UnityEngine.UI;
using System.Collections;

public class FadeFromBlack : MonoBehaviour
{
    [Tooltip("Image to fade out")]
    public Image fadeImage;

    [Tooltip("Delay before fade-out starts")]
    public float initialDelay = 1f;

    [Tooltip("Duration of fade-in")]
    public float fadeDuration = 5f;

    private void Start() {
        StartCoroutine(FadeOut());
    }

    private IEnumerator FadeOut() {
        yield return new WaitForSeconds(initialDelay);

        Color color = fadeImage.color;
        float elapsed = 0f;
        while(elapsed < fadeDuration) {
            elapsed += Time.deltaTime;
            color.a = Mathf.Lerp(1f, 0f, elapsed / fadeDuration);
            fadeImage.color = color;

            yield return null;
        }
        color.a = 0f;
        fadeImage.color = color;
    }
}
