using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using Newtonsoft.Json;

public class ConsoleMessageExporter : EditorWindow {
    private List<LogMessage> logMessages = new List<LogMessage>();

    [MenuItem("Tools/DopeCoder/Export Console Messages")]
    public static void ShowWindow() {
        GetWindow<ConsoleMessageExporter>("Export Console Messages");
    }

    private void OnEnable() {
        Application.logMessageReceived += HandleLog;
    }

    private void OnDisable() {
        Application.logMessageReceived -= HandleLog;
    }

    private void HandleLog(string logString, string stackTrace, LogType type) {
        logMessages.Add(new LogMessage {
            Message = logString,
            StackTrace = stackTrace,
            Type = type.ToString()
        });
    }

    private void OnGUI() {
        if (GUILayout.Button("Export Logs as JSON")) {
            string json = JsonConvert.SerializeObject(logMessages, Formatting.Indented);
            string path = EditorUtility.SaveFilePanel("Save Logs as JSON", "", "console_logs.json", "json");
            if (!string.IsNullOrEmpty(path)) {
                System.IO.File.WriteAllText(path, json);
                Debug.Log("Logs exported successfully!");
            }
        }
    }

    public class LogMessage {
        public string Message { get; set; }
        public string StackTrace { get; set; }
        public string Type { get; set; }
    }
}
