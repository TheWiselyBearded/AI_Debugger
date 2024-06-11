using System.Collections.Generic;
using Newtonsoft.Json;
using UnityEditor;
using UnityEngine;

public class AssistantChatWindow : EditorWindow {
    private string inputText = "";
    private string outputText = "";

    private List<ConsoleMessageExporter.LogMessage> logMessages = new List<ConsoleMessageExporter.LogMessage>();

    [MenuItem("Tools/DopeCoder/Assistant Chat")]
    public static void ShowWindow() {
        GetWindow<AssistantChatWindow>("Assistant Chat");
    }

    private void OnEnable() {
        Application.logMessageReceived += HandleLog;
    }

    private void OnDisable() {
        Application.logMessageReceived -= HandleLog;
    }

    private void OnGUI() {
        GUILayout.Label("Assistant Chat", EditorStyles.boldLabel);

        inputText = EditorGUILayout.TextField("Input", inputText);

        if (GUILayout.Button("Send")) {
            outputText = ProcessInput(inputText);
        }

        EditorGUILayout.LabelField("Output", outputText);

        if (GUILayout.Button("Export Console Logs")) {
            ExportLogsAsJson();
        }

        if (GUILayout.Button("Take Screenshot")) {
            ScreenshotUtility.CaptureFullWindow();
        }
    }

    private string ProcessInput(string input) {
        // Handle input and generate response
        return "You said: " + input;
    }

    private void HandleLog(string logString, string stackTrace, LogType type) {
        logMessages.Add(new ConsoleMessageExporter.LogMessage {
            Message = logString,
            StackTrace = stackTrace,
            Type = type.ToString()
        });
    }

    private void ExportLogsAsJson() {
        string json = JsonConvert.SerializeObject(logMessages, Formatting.Indented);
        string path = EditorUtility.SaveFilePanel("Save Logs as JSON", "", "console_logs.json", "json");
        if (!string.IsNullOrEmpty(path)) {
            System.IO.File.WriteAllText(path, json);
            Debug.Log("Logs exported successfully!");
        }
    }
}
