using UnityEngine;
using System.IO;

public class CameraScreenCapture : MonoBehaviour
{
    [Header("Capture Settings")]
    [SerializeField] private int captureWidth = 1024;
    [SerializeField] private int captureHeight = 1024;
    [SerializeField] private KeyCode captureKey = KeyCode.C;
    
    private Camera captureCamera;
    private const string CAPTURE_FOLDER = "ScreenCapture";

    private void Awake()
    {
        captureCamera = GetComponent<Camera>();
        
        if (captureCamera == null)
        {
            Debug.LogError("CameraScreenCapture: No Camera component found on this GameObject.");
            enabled = false;
            return;
        }
        
        string folderPath = Path.Combine(Application.dataPath, CAPTURE_FOLDER);
        if (!Directory.Exists(folderPath))
        {
            Directory.CreateDirectory(folderPath);
            Debug.Log($"Created ScreenCapture folder at: {folderPath}");
        }
    }

    private void Update()
    {
        if (Input.GetKeyDown(captureKey))
        {
            CaptureScreen();
        }
    }

    public void CaptureScreen()
    {
        Rect originalRect = captureCamera.rect;
        captureCamera.rect = new Rect(0, 0, 1, 1);
        
        RenderTexture renderTexture = new RenderTexture(captureWidth, captureHeight, 24);
        captureCamera.targetTexture = renderTexture;
        
        Texture2D screenshot = new Texture2D(captureWidth, captureHeight, TextureFormat.RGB24, false);
        
        captureCamera.Render();
        
        RenderTexture.active = renderTexture;
        screenshot.ReadPixels(new Rect(0, 0, captureWidth, captureHeight), 0, 0);
        screenshot.Apply();
        
        captureCamera.targetTexture = null;
        RenderTexture.active = null;
        captureCamera.rect = originalRect;
        Destroy(renderTexture);
        
        byte[] bytes = screenshot.EncodeToPNG();
        
        string timestamp = System.DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
        string filename = $"Capture_{timestamp}.png";
        string filepath = Path.Combine(Application.dataPath, CAPTURE_FOLDER, filename);
        
        File.WriteAllBytes(filepath, bytes);
        
        Debug.Log($"Screenshot saved to: {filepath}");
        
        Destroy(screenshot);
        
#if UNITY_EDITOR
        UnityEditor.AssetDatabase.Refresh();
#endif
    }
}
