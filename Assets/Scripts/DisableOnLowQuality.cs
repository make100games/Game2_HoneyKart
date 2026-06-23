using UnityEngine;

public class DisableOnLowQuality : MonoBehaviour
{
    private void Awake() {
        string quality = QualitySettings.names[
            QualitySettings.GetQualityLevel()
        ];

        if (quality == "Low")
        {
            this.gameObject.SetActive(false);
        }
    }

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
