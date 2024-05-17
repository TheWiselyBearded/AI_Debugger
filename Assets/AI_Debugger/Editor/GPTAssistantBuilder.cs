using UnityEngine;
using UnityEditor;
using System;
using System.Linq;
using OpenAI;
using OpenAI.Assistants;
using System.Threading.Tasks;
using OpenAI.Files;
using OpenAI.Threads;
using System.Collections.Generic;
using System.IO;

[Serializable]
public class FileReference {
    public string assetPath;
    public bool markedForRemoval;
}

public class GPTAssistantBuilder : EditorWindow {
    private FileReference[] files = new FileReference[0];
    private const string EditorPrefKey = "GPTAssistantFiles";
    private float feedbackTimer = 0f;
    private float feedbackDuration = 1.0f;

    private string assistantId;
    private string fileID;
    private string assistantName = "New Assistant";
    private string modelType = "gpt-4-1106-preview";
    private bool isCreatingOrUpdating = false;

    private Vector2 scrollPosition; // Variable to manage scroll position
    private Vector2 fileScrollPosition; // Variable to manage scroll position for files


    private List<string> assistantsToDelete = new List<string>(); // Track assistants marked for deletion
    private string selectedAssistantToLoad; // Track assistant selected to load


    private bool showAssistantsList = false; // Toggle for showing/hiding assistants list
    private bool showFileList = false; // Variable to manage the visibility of the file list

    private List<AssistantResponse> assistantsList = new List<AssistantResponse>();


    [MenuItem("Tools/DopeCoder/GPT Assistant Builder")]
    public static void ShowWindow() {
        GetWindow<GPTAssistantBuilder>("GPT Assistant Builder");
    }

    private void OnEnable() {
        LoadFilesFromEditorPrefs();
        LoadAssistantId();
        if (!string.IsNullOrEmpty(assistantId)) {
            DisplayAssistantDetails(assistantId);
        } else {
            ListAssistants();
        }
    }

    private void OnDisable() {
        SaveFilesToEditorPrefs();
    }

    private void OnGUI() {
        GUILayout.Label("GPT Assistant Builder", EditorStyles.boldLabel);

        GUILayout.Space(10);

        // Collapsible section for uploading and listing local files
        showFileList = EditorGUILayout.Foldout(showFileList, "Local Files");
        if (showFileList) {
            // Drop area for uploading files
            EditorGUILayout.LabelField("Drag and drop files here:");
            Rect dropArea = GUILayoutUtility.GetRect(0f, 50f, GUILayout.ExpandWidth(true));
            GUI.Box(dropArea, "Drag and drop files here");
            UnityEngine.Event currentEvent = UnityEngine.Event.current;

            if (currentEvent.type == EventType.DragUpdated) {
                DragAndDrop.visualMode = DragAndDropVisualMode.Copy;
            }

            if (currentEvent.type == EventType.DragPerform) {
                DragAndDrop.AcceptDrag();
                feedbackTimer = Time.realtimeSinceStartup;

                foreach (UnityEngine.Object draggedObject in DragAndDrop.objectReferences) {
                    string assetPath = AssetDatabase.GetAssetPath(draggedObject);

                    if (!files.Any(file => file.assetPath == assetPath)) {
                        Array.Resize(ref files, files.Length + 1);
                        files[files.Length - 1] = new FileReference { assetPath = assetPath };
                    }
                }
            }

            if (Time.realtimeSinceStartup - feedbackTimer < feedbackDuration) {
                EditorGUI.DrawRect(dropArea, new Color(0.5f, 1f, 0.5f, 0.5f));
            }

            GUILayout.Space(10);

            // Scrollable list of local files
            fileScrollPosition = EditorGUILayout.BeginScrollView(fileScrollPosition, GUILayout.Height(200)); // Set a height for the scroll view
            for (int i = 0; i < files.Length; i++) {
                EditorGUILayout.BeginHorizontal();

                files[i].markedForRemoval = EditorGUILayout.Toggle(files[i].markedForRemoval, GUILayout.Width(20));
                EditorGUILayout.LabelField("Added File:", AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(files[i].assetPath).name);

                if (GUILayout.Button("Remove", GUILayout.Width(80))) {
                    RemoveFileAtIndex(i);
                }

                EditorGUILayout.EndHorizontal();
                GUILayout.Space(10);
            }
            EditorGUILayout.EndScrollView();
        }

        GUILayout.Space(20);

        if (GUILayout.Button("Save Files to EditorPrefs")) {
            SaveFilesToEditorPrefs();
        }


        GUILayout.Space(20);

        EditorGUILayout.LabelField("Assistant Details:");
        assistantName = EditorGUILayout.TextField("Assistant Name", assistantName);
        modelType = EditorGUILayout.TextField("Model Type", modelType);

        GUILayout.Space(20);

        if (GUILayout.Button("Create or Update Assistant")) {
            CreateOrUpdateAssistantAsync();
        }

        if (GUILayout.Button("Attach Files to Assistant")) {
            AttachFilesToAssistantAsync();
        }

        if (!string.IsNullOrEmpty(assistantId) && GUILayout.Button("Delete Assistant")) {
            DeleteAssistantAsync();
        }

        // ASSISTANT CRUD OPERATIONS
        if (GUILayout.Button("List Assistants")) {
            ListAssistantsAsync();
        }

        // Collapsible section for listing assistants
        showAssistantsList = EditorGUILayout.Foldout(showAssistantsList, "Assistants List");
        if (showAssistantsList) {
            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition, GUILayout.Height(200)); // Set a height for the scroll view
            foreach (var assistant in assistantsList) {
                EditorGUILayout.BeginHorizontal();

                // Display assistant details
                EditorGUILayout.LabelField($"{assistant.Name}: {assistant.Id}");

                // Load button
                GUI.enabled = string.IsNullOrEmpty(assistantId) || assistant.Id == assistantId;
                if (GUILayout.Button("Load", GUILayout.Width(50))) {
                    LoadAssistant(assistant.Id);
                }
                GUI.enabled = true;

                // Delete button
                if (GUILayout.Button("Delete", GUILayout.Width(60))) {
                    DeleteAssistantAsync(assistant.Id);
                }

                EditorGUILayout.EndHorizontal();
                GUILayout.Space(10);
            }
            EditorGUILayout.EndScrollView();
        }

        if (isCreatingOrUpdating) {
            GUILayout.Space(20);
            EditorGUILayout.LabelField("Processing...", EditorStyles.boldLabel);
        }



        if (isCreatingOrUpdating) {
            GUILayout.Space(20);
            EditorGUILayout.LabelField("Processing...", EditorStyles.boldLabel);
        }
    }

    private void RemoveFileAtIndex(int index) {
        if (index >= 0 && index < files.Length) {
            FileReference[] newFiles = new FileReference[files.Length - 1];
            int newIndex = 0;

            for (int i = 0; i < files.Length; i++) {
                if (i != index) {
                    newFiles[newIndex] = files[i];
                    newIndex++;
                }
            }

            files = newFiles;
        }
    }

    private void SaveFilesToEditorPrefs() {
        string[] assetPaths = files.Select(file => file.assetPath).ToArray();
        bool[] removalStates = files.Select(file => file.markedForRemoval).ToArray();

        EditorPrefs.SetString(EditorPrefKey + "Paths", string.Join(",", assetPaths));
        EditorPrefs.SetString(EditorPrefKey + "States", string.Join(",", removalStates));
        EditorPrefs.SetInt(EditorPrefKey + "Count", files.Length);
    }

    private void LoadFilesFromEditorPrefs() {
        if (EditorPrefs.HasKey(EditorPrefKey + "Paths") && EditorPrefs.HasKey(EditorPrefKey + "States") && EditorPrefs.HasKey(EditorPrefKey + "Count")) {
            string[] assetPaths = EditorPrefs.GetString(EditorPrefKey + "Paths").Split(',');
            string[] removalStateStrings = EditorPrefs.GetString(EditorPrefKey + "States").Split(',');
            int count = EditorPrefs.GetInt(EditorPrefKey + "Count");

            FileReference[] loadedFiles = new FileReference[count];

            for (int i = 0; i < count; i++) {
                bool markedForRemoval = bool.Parse(removalStateStrings[i]);
                loadedFiles[i] = new FileReference { assetPath = assetPaths[i], markedForRemoval = markedForRemoval };
            }

            files = loadedFiles;
        }
    }

    private void LoadAssistantId() {
        assistantId = EditorPrefs.GetString("AssistantId", "");
    }

    private void DisplayAssistantDetails(string assistantId) {
        // Fetch and display details like ID, name, date created, file names
        RetrieveAssistantAsync();
    }

    public async void ListAssistants() {
        var api = new OpenAIClient();
        var assistantsList = await api.AssistantsEndpoint.ListAssistantsAsync();

        foreach (var assistant in assistantsList.Items) {
            Debug.Log($"{assistant} -> {assistant.CreatedAt}, ID {assistant.Id}");
        }
    }

    public async void ListAssistantsAsync() {
        var api = new OpenAIClient();
        var assistantsListResponse = await api.AssistantsEndpoint.ListAssistantsAsync();

        assistantsList = assistantsListResponse.Items.ToList();
        showAssistantsList = true; // Show the list after fetching
        Repaint();
    }

    private async void DeleteSelectedAssistantsAsync() {
        var api = new OpenAIClient();
        foreach (var assistantId in assistantsToDelete) {
            await api.AssistantsEndpoint.DeleteAssistantAsync(assistantId);
            Debug.Log($"Deleted assistant {assistantId}");
        }
        assistantsToDelete.Clear();
        ListAssistantsAsync(); // Refresh the list
    }

    private async void DeleteAssistantAsync(string assistantIdToDelete) {
        var api = new OpenAIClient();
        if (!string.IsNullOrEmpty(assistantIdToDelete)) {
            var isDeleted = await api.AssistantsEndpoint.DeleteAssistantAsync(assistantIdToDelete);
            if (isDeleted) {
                Debug.Log($"Deleted assistant {assistantIdToDelete}");
                ListAssistantsAsync(); // Refresh the list
            }
        }
    }

    private void LoadAssistant(string assistantIdToLoad) {
        assistantId = assistantIdToLoad;
        EditorPrefs.SetString("AssistantId", assistantId);
        DisplayAssistantDetails(assistantId);
        Debug.Log($"Loaded assistant {assistantId}");
    }


    private void LoadSelectedAssistant() {
        if (!string.IsNullOrEmpty(selectedAssistantToLoad)) {
            assistantId = selectedAssistantToLoad;
            EditorPrefs.SetString("AssistantId", assistantId);
            DisplayAssistantDetails(assistantId);
            Debug.Log($"Loaded assistant {assistantId}");
        }
    }



    public async void CreateOrUpdateAssistantAsync() {
        isCreatingOrUpdating = true;
        Repaint();

        var api = new OpenAIClient();
        var retrievalTools = new OpenAI.Tool[1];
        retrievalTools[0] = OpenAI.Tool.Retrieval;

        if (string.IsNullOrEmpty(assistantId)) {
            var request = new CreateAssistantRequest(model: modelType, name: assistantName, tools: retrievalTools);
            var assistant = await api.AssistantsEndpoint.CreateAssistantAsync(request);
            assistantId = assistant.Id;
            EditorPrefs.SetString("AssistantId", assistant.Id);
            Debug.Log($"Assistant created with ID {assistant.Id}");
        } else {
            //var updateRequest = new UpdateAssistantRequest(name: assistantName, model: modelType);
            //var assistant = await api.AssistantsEndpoint.UpdateAssistantAsync(assistantId, updateRequest);
            //Debug.Log($"Assistant updated with ID {assistant.Id}");
        }

        isCreatingOrUpdating = false;
        Repaint();
    }

    public async Task<AssistantResponse> RetrieveAssistantAsync() {
        var api = new OpenAIClient();
        var assistant = await api.AssistantsEndpoint.RetrieveAssistantAsync(assistantId);
        Debug.Log($"{assistant} -> {assistant.CreatedAt}, ID {assistant.Id}");
        return assistant;
    }

    public async void DeleteAssistantAsync() {
        var api = new OpenAIClient();
        if (!string.IsNullOrEmpty(assistantId)) {
            var isDeleted = await api.AssistantsEndpoint.DeleteAssistantAsync(assistantId);
            Debug.Log($"Deleted assistant {assistantId}");
            assistantId = string.Empty;
            EditorPrefs.SetString("AssistantId", "");
        }
    }

    public async void ListAssistantFilesAsync() {
        var api = new OpenAIClient();
        var filesList = await api.AssistantsEndpoint.ListFilesAsync(assistantId);

        foreach (var file in filesList.Items) {
            Debug.Log($"{file.AssistantId}'s file -> {file.Id}");
        }
    }

    public async void AttachFilesToAssistantAsync() {
        var api = new OpenAIClient();

        foreach (var fileRef in files) {
            string absoluteFilePath = Path.GetFullPath(fileRef.assetPath);
            Debug.Log($"Uploading file: {absoluteFilePath}");

            var fileData = await api.FilesEndpoint.UploadFileAsync(absoluteFilePath, "assistants");
            var assistantFile = await api.AssistantsEndpoint.AttachFileAsync(assistantId, new FileResponse(fileData.Id, fileData.Object, fileData.Size, fileData.CreatedUnixTimeSeconds, fileData.FileName, fileData.Purpose, fileData.Status));
            Debug.Log($"Attached file {fileData.Id} to assistant {assistantId}");

            await Task.Delay(1000); // Delay for 1 second
        }
    }
}
