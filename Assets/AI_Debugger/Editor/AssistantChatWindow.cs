using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Newtonsoft.Json;
using UnityEditor;
using UnityEngine;

public class AssistantChatWindow : EditorWindow {
    private string inputText = "";
    private Vector2 scrollPosition;
    private OpenAIChatInterface openAIChatInterface;

    private static List<ChatEntry> chatLog = new List<ChatEntry>(); // Static to persist within session
    private Texture2D fileIcon;
    private ConsoleLogListener consoleLogListener;

    // Enum for the modes
    private enum Mode {
        AssistantMode,
        ChatMode
    }

    private Mode currentMode = Mode.AssistantMode;

    // Dropdown for mode selection
    private string[] modes = { "Assistant Mode", "Chat Mode" };
    private int selectedModeIndex = 0;

    [MenuItem("Tools/DopeCoder/Assistant Chat")]
    public static void ShowWindow() {
        GetWindow<AssistantChatWindow>("Assistant Chat");
    }

    private async void OnEnable() {
        fileIcon = EditorGUIUtility.IconContent("d_TextAsset Icon").image as Texture2D;
        consoleLogListener = new ConsoleLogListener();
        openAIChatInterface = new OpenAIChatInterface(EditorPrefs.GetString("AssistantId", ""));

        await openAIChatInterface.InitializeSessionAsync();
    }

    private void OnDisable() {
        consoleLogListener = null; // Dispose the listener to stop collecting logs
    }

    private void OnGUI() {
        GUILayout.BeginVertical();

        // Dropdown for selecting mode
        GUILayout.BeginHorizontal();
        GUILayout.Label("Mode: ", GUILayout.Width(50));
        selectedModeIndex = EditorGUILayout.Popup(selectedModeIndex, modes, GUILayout.Width(200));
        currentMode = (Mode)selectedModeIndex; // Update currentMode based on dropdown selection
        GUILayout.EndHorizontal();

        GUILayout.Label("Assistant Chat", EditorStyles.boldLabel);
        scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition, GUILayout.Height(400));
        foreach (var entry in chatLog) {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(entry.User, EditorStyles.boldLabel, GUILayout.Width(100));

            if (entry.IsFile) {
                if (GUILayout.Button(new GUIContent(Path.GetFileName(entry.FilePath), fileIcon), GUILayout.Width(200), GUILayout.Height(20))) {
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(entry.FilePath) { UseShellExecute = true });
                }
            } else {
                // Make the text selectable
                EditorGUILayout.SelectableLabel(entry.Message, EditorStyles.wordWrappedLabel, GUILayout.Height(EditorGUIUtility.singleLineHeight * entry.Message.Split('\n').Length + 4));
            }

            EditorGUILayout.EndHorizontal();
        }
        EditorGUILayout.EndScrollView();

        GUILayout.Space(10);
        GUILayout.BeginHorizontal();
        inputText = EditorGUILayout.TextField(inputText, GUILayout.ExpandWidth(true));

        if (GUILayout.Button("Send", GUILayout.Width(80))) {
            ProcessInput(inputText);
            inputText = ""; // Clear input after sending
        }
        GUILayout.EndHorizontal();

        GUILayout.Space(10);
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("Clear Chat Log", GUILayout.Width(100))) {
            chatLog.Clear();
        }
        if (GUILayout.Button("Export Logs as JSON", GUILayout.Width(150))) {
            ExportLogsAsJson();
        }
        GUILayout.EndHorizontal();

        GUILayout.Space(10);
        Rect dropArea = GUILayoutUtility.GetRect(0.0f, 50.0f, GUILayout.ExpandWidth(true));
        GUI.Box(dropArea, "Drag and Drop Files Here", EditorStyles.helpBox);

        HandleDragAndDrop(dropArea);

        GUILayout.EndVertical();
    }

    private async void ProcessInput(string input) {
        if (string.IsNullOrEmpty(input)) return;

        // Add user message to chat log
        chatLog.Add(new ChatEntry { User = "You", Message = input, IsFile = false });

        // Depending on the current mode, modify how the message is handled
        string response = "";
        if (currentMode == Mode.AssistantMode) {
            string jsonInput = JsonConvert.SerializeObject(new { type = "question", content = input });
            response = await openAIChatInterface.SendMessageAsync(jsonInput, input);
        } else if (currentMode == Mode.ChatMode) {
            response = await openAIChatInterface.SendChatMessage(input);
        }

        // Add response to chat log
        chatLog.Add(new ChatEntry { User = "Assistant", Message = response, IsFile = false });

        // Refresh the window to display new messages
        Repaint();
    }

    private void ExportLogsAsJson() {
        if (consoleLogListener == null) {
            Debug.LogError("Console log listener is not initialized.");
            return;
        }

        string tempPath = consoleLogListener.ExportLogsAsJson();
        if (!string.IsNullOrEmpty(tempPath)) {
            chatLog.Add(new ChatEntry { User = "System", Message = "Exported JSON log", IsFile = true, FilePath = tempPath });
            Debug.Log($"Logs exported to: {tempPath}");
        } else {
            chatLog.Add(new ChatEntry { User = "System", Message = "No logs to export", IsFile = false });
            Debug.LogWarning("No logs to export.");
        }

        Repaint();
    }

    private async void HandleDragAndDrop(Rect dropArea) {        
        Event evt = Event.current;
        string response = "";
        switch (evt.type) {
            case EventType.DragUpdated:
            case EventType.DragPerform:
                if (!dropArea.Contains(evt.mousePosition))
                    return;

                DragAndDrop.visualMode = DragAndDropVisualMode.Copy;

                if (evt.type == EventType.DragPerform) {
                    DragAndDrop.AcceptDrag();
                    if (currentMode == Mode.AssistantMode) {
                        foreach (var draggedObject in DragAndDrop.objectReferences) {
                            string assetPath = AssetDatabase.GetAssetPath(draggedObject);
                            if (!string.IsNullOrEmpty(assetPath)) {
                                string tempPath = Path.Combine(Path.GetTempPath(), Path.GetFileName(assetPath));
                                File.Copy(assetPath, tempPath, true);
                                chatLog.Add(new ChatEntry { User = "You", Message = "Uploaded a file", IsFile = true, FilePath = tempPath });
                                response = await openAIChatInterface.SendUploadFile(assetPath);
                            }
                        }
                        chatLog.Add(new ChatEntry { User = "Assistant", Message = response, IsFile = false });
                    }
                    Repaint();
                }
                break;
        }
    }
}

public class ChatEntry {
    public string User { get; set; }
    public string Message { get; set; }
    public bool IsFile { get; set; }
    public string FilePath { get; set; }
}


public class ConsoleLogListener {
    private List<LogMessage> logMessages = new List<LogMessage>();
    private string tempFilePath;

    public ConsoleLogListener() {
        Application.logMessageReceived += HandleLog;
        tempFilePath = Path.Combine(Path.GetTempPath(), "console_logs.json");
        UpdateJsonFile();
    }

    ~ConsoleLogListener() {
        Application.logMessageReceived -= HandleLog;
    }

    private void HandleLog(string logString, string stackTrace, LogType type) {
        logMessages.Add(new LogMessage {
            Message = logString,
            StackTrace = stackTrace,
            Type = type.ToString()
        });

        // Update the temporary JSON file each time a new log is added
        UpdateJsonFile();
    }

    private void UpdateJsonFile() {
        string json = JsonConvert.SerializeObject(logMessages, Formatting.Indented);
        File.WriteAllText(tempFilePath, json);
    }

    public string ExportLogsAsJson() {
        if (logMessages.Count == 0) {
            Debug.LogWarning("No logs to export.");
            return null;
        }

        // Return the path of the temporary JSON file
        return tempFilePath;
    }

    public class LogMessage {
        public string Message { get; set; }
        public string StackTrace { get; set; }
        public string Type { get; set; }
    }
}