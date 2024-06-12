using System.Collections.Generic;
using System.Threading.Tasks;
using Newtonsoft.Json;
using UnityEditor;
using UnityEngine;

public class AssistantChatWindow : EditorWindow {
    private string inputText = "";
    private string outputText = "";
    private Vector2 scrollPosition;

    private static List<string> chatLog = new List<string>(); // Static to persist within session

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
            ProcessInput(inputText);
            inputText = ""; // Clear input after sending
        }

        GUILayout.Space(10);

        // Scroll view for chat log
        scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition, GUILayout.Height(200));
        foreach (string message in chatLog) {
            EditorGUILayout.LabelField(message, EditorStyles.wordWrappedLabel);
        }
        EditorGUILayout.EndScrollView();

        GUILayout.Space(10);

        // Add Clear button to clear the chat log
        if (GUILayout.Button("Clear Chat Log")) {
            chatLog.Clear();
        }

        if (GUILayout.Button("Export Logs as JSON")) {
            ExportLogsAsJson();
        }

        if (GUILayout.Button("Take Screenshot")) {
            ScreenshotUtility.CaptureFullWindow();
        }
    }

    private async void ProcessInput(string input) {
        var chatMessage = new ChatMessage(input);
        chatLog.Add("You: " + input);

        // Await the simulated API call
        await chatMessage.SendToApiAsync();

        outputText = chatMessage.Response;
        chatLog.Add("Assistant: " + outputText);

        // Refresh the window to display the new messages
        Repaint();
    }

    private void HandleLog(string logString, string stackTrace, LogType type) {
        string logEntry = $"{type}: {logString}\n{stackTrace}";
        chatLog.Add(logEntry);
        Repaint(); // Update GUI to reflect new log entries
    }

    private void ExportLogsAsJson() {
        string json = JsonConvert.SerializeObject(chatLog, Formatting.Indented);
        string path = EditorUtility.SaveFilePanel("Save Logs as JSON", "", "console_logs.json", "json");
        if (!string.IsNullOrEmpty(path)) {
            System.IO.File.WriteAllText(path, json);
            Debug.Log("Logs exported successfully!");
        }
    }
}