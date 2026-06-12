using UnityEngine;
using System.Collections;

public class UIShine : MonoBehaviour
{
    public Material mat;
    public float duration = 20f;
    public float waitTime = 3f;

    void Start()
    {
        StartCoroutine(ShineLoop());
    }

    private IEnumerator ShineLoop() {
        while(true) {
            // Move shine left to right
            float t = 0f;

            while(t < duration) {
                t += Time.deltaTime;
                float pos = Mathf.Lerp(-0.2f, 1.2f, t / duration);
                mat.SetFloat("_ShinePosition", pos);
                yield return null;
            }

            // Pause before next shine effect
            yield return new WaitForSeconds(waitTime);
        }
    }
}
