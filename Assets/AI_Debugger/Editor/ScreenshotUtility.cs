using UnityEditor;
using UnityEngine;

public class ScreenshotUtility : EditorWindow {
    [MenuItem("Tools/DopeCoder/Take Screenshot")]
    public static void ShowWindow() {
        GetWindow<ScreenshotUtility>("Take Screenshot");
    }

    private void OnGUI() {
        if (GUILayout.Button("Capture Entire Window")) {
            CaptureFullWindow();
        }

        if (GUILayout.Button("Capture Specific Area")) {
            // Implement specific area capture logic here
            CaptureSpecificArea(new Rect(100, 100, 200, 200));
        }
    }

    public static void CaptureFullWindow() {
        string path = EditorUtility.SaveFilePanel("Save Screenshot", "", "screenshot.png", "png");
        if (!string.IsNullOrEmpty(path)) {
            ScreenCapture.CaptureScreenshot(path);
            Debug.Log("Screenshot saved successfully!");
        }
    }

    private void CaptureSpecificArea(Rect area) {
        // Implement your area capture logic here
        // This requires more complex handling like rendering to a texture
        // and extracting pixels from it
    }
}
