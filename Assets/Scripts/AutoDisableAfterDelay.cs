using UnityEngine;

public class AutoDisableAfterDelay : MonoBehaviour
{
    [Tooltip("Duration after which this object will disable itself")]
    public float disableAfterSeconds = 5f;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        Invoke(nameof(DisableObject), disableAfterSeconds);
    }

    private void DisableObject() {
        gameObject.SetActive(false);
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
