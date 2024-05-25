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

    private bool showAssistantFiles = true; // Variable to manage the visibility of the files list

    private bool showAssistantsList = false; // Toggle for showing/hiding assistants list
    private bool showFileList = false; // Variable to manage the visibility of the file list

    private List<AssistantResponse> assistantsList = new List<AssistantResponse>();

    private AssistantResponse assignedAssistant; // Store the assigned assistant details
    private List<AssistantFileResponse> assistantFiles = new List<AssistantFileResponse>(); // Store files assigned to the assistant

    private Vector2 assistantFilesScrollPosition; // Scroll position for assistant files list

    private List<VectorStoreData> vectorStores = new List<VectorStoreData>();
    private Vector2 vectorStoreScrollPosition; // Scroll position for vector stores list
    private bool showVectorStores = false; // Variable to manage the visibility of the vector stores list

    private bool showDirectories = false; // Variable to manage the visibility of the directories list
    private List<string> directories = new List<string>(); // List to store all directories
    private Vector2 directoryScrollPosition; // Scroll position for directories list
    private Dictionary<string, bool> directorySelection = new Dictionary<string, bool>(); // Dictionary to store the selection state of each directory
    private Dictionary<string, bool> directoryFoldouts = new Dictionary<string, bool>(); // Store foldout state for each directory
    private Dictionary<string, List<string>> directoryFiles = new Dictionary<string, List<string>>(); // Store files for each directory
    private Dictionary<string, Dictionary<string, bool>> directoryFileSelection = new Dictionary<string, Dictionary<string, bool>>(); // Store selection state for each file within each directory
    private List<string> additionalDirectories = new List<string>(); // List to store additional directories
    private bool showAssets = false; // Foldout state for Assets
    private bool showLibraryPackageCache = false; // Foldout state for Library/PackageCache



    [MenuItem("Tools/DopeCoder/GPT Assistant Builder")]
    public static void ShowWindow() {
        GetWindow<GPTAssistantBuilder>("GPT Assistant Builder");
    }

    private GPTUtilities gptUtilities;

    private void OnEnable() {
        LoadFilesFromEditorPrefs();
        LoadAssistantId();
        gptUtilities = new GPTUtilities();
        gptUtilities.Init();
        if (!string.IsNullOrEmpty(assistantId)) {
            LoadAssistant(assistantId); // Automatically load the assigned assistant
        } else {
            ListAssistantsAsync();
        }
        ApplyAssistantIdToInterfacer();
        LoadAllDirectories(); // Load directories on enable
    }


    private void OnDisable() {
        gptUtilities.vectorStoreAPI = null;
        SaveFilesToEditorPrefs();
    }

    private void OnGUI() {
        GUILayout.Label("GPT Assistant Builder", EditorStyles.boldLabel);
        GUILayout.Space(10);

        DrawAssistantDetails();
        DrawLocalFilesSection();
        DrawDirectoriesSection();
        DrawAssistantActions();
        DrawAssistantsListSection();
    }

    private void DrawAssistantDetails() {
        if (assignedAssistant != null) {
            GUILayout.Label($"Assigned Assistant: {assignedAssistant.Name} ({assignedAssistant.Id})", EditorStyles.boldLabel);

            // Collapsible section for listing vector stores
            showVectorStores = EditorGUILayout.Foldout(showVectorStores, "Vector Stores");
            if (showVectorStores) {
                vectorStoreScrollPosition = EditorGUILayout.BeginScrollView(vectorStoreScrollPosition, GUILayout.Height(100));
                foreach (var store in vectorStores) {
                    EditorGUILayout.LabelField($"ID: {store.Id}, Name: {store.Name}, Created At: {store.CreatedAt}");
                }
                EditorGUILayout.EndScrollView();
            }

            // Collapsible section for listing files assigned to the assistant
            showAssistantFiles = EditorGUILayout.Foldout(showAssistantFiles, "Files");
            if (showAssistantFiles) {
                assistantFilesScrollPosition = EditorGUILayout.BeginScrollView(assistantFilesScrollPosition, GUILayout.Height(100));
                foreach (var file in assistantFiles) {
                    EditorGUILayout.LabelField(file.Id);
                }
                EditorGUILayout.EndScrollView();
            }

            GUILayout.Space(10);

            // Section to select and upload a file to the assistant
            GUILayout.Label("Upload File to Assistant", EditorStyles.boldLabel);
            if (GUILayout.Button("Select File to Upload")) {
                string path = EditorUtility.OpenFilePanel("Select File to Upload", "", "");
                if (!string.IsNullOrEmpty(path)) {
                    UploadFileToAssistantAsync(path);
                }
            }

            GUILayout.Space(10);

            // Unload button
            if (GUILayout.Button("Unload Assistant")) {
                UnloadAssistant();
            }

            GUILayout.Space(20);
        }
    }

    private void DrawLocalFilesSection() {
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

            GUILayout.Space(10);

            if (GUILayout.Button("Save Files to EditorPrefs")) {
                SaveFilesToEditorPrefs();
            }

            GUILayout.Space(20);
        }
    }

    private void DrawDirectoriesSection() {
        showDirectories = EditorGUILayout.Foldout(showDirectories, "All Directories");
        if (showDirectories) {
            directoryScrollPosition = EditorGUILayout.BeginScrollView(directoryScrollPosition, GUILayout.Height(200)); // Set a height for the scroll view

            // Foldout for Assets
            showAssets = EditorGUILayout.Foldout(showAssets, "Assets");
            if (showAssets) {
                foreach (string dir in directories) {
                    DrawDirectoryAndFiles(dir);
                }
            }

            // Foldout for Library/PackageCache
            showLibraryPackageCache = EditorGUILayout.Foldout(showLibraryPackageCache, "Library/PackageCache");
            if (showLibraryPackageCache) {
                foreach (string dir in additionalDirectories) {
                    DrawDirectoryAndFiles(dir);
                }
            }

            EditorGUILayout.EndScrollView();
        }
    }

    private void DrawAssistantActions() {
        // Assistant Details and Actions
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

        if (GUILayout.Button("List Assistants")) {
            ListAssistantsAsync();
        }
    }

    private void DrawAssistantsListSection() {
        showAssistantsList = EditorGUILayout.Foldout(showAssistantsList, "Assistants List");
        if (showAssistantsList) {
            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition, GUILayout.Height(200)); // Set a height for the scroll view
            foreach (var assistant in assistantsList) {
                EditorGUILayout.BeginHorizontal();

                // Display assistant details
                EditorGUILayout.LabelField($"{assistant.Name}: {assistant.Id}");

                // Load/Unload button
                if (assistant.Id == assistantId) {
                    if (GUILayout.Button("Unload", GUILayout.Width(60))) {
                        UnloadAssistant();
                    }
                } else {
                    if (GUILayout.Button("Load", GUILayout.Width(50))) {
                        LoadAssistant(assistant.Id);
                    }
                }

                // Delete button
                if (GUILayout.Button("Delete", GUILayout.Width(60))) {
                    DeleteAssistantAsync(assistant.Id);
                }

                EditorGUILayout.EndHorizontal();
                GUILayout.Space(10);
            }
            EditorGUILayout.EndScrollView();
        }
    }


    private void DrawDirectoryAndFiles(string dir) {
        EditorGUILayout.BeginHorizontal();
        bool newDirSelection = EditorGUILayout.Toggle(directorySelection[dir], GUILayout.Width(20));
        if (newDirSelection != directorySelection[dir]) {
            directorySelection[dir] = newDirSelection;
            // Update file selection based on directory selection
            foreach (var file in directoryFiles[dir]) {
                directoryFileSelection[dir][file] = newDirSelection;
            }
        }
        directoryFoldouts[dir] = EditorGUILayout.Foldout(directoryFoldouts[dir], dir);
        EditorGUILayout.EndHorizontal();

        if (directoryFoldouts[dir]) {
            foreach (string file in directoryFiles[dir]) {
                EditorGUILayout.BeginHorizontal();
                GUILayout.Space(20); // Indent files
                directoryFileSelection[dir][file] = EditorGUILayout.Toggle(directoryFileSelection[dir][file], GUILayout.Width(20));
                EditorGUILayout.LabelField(file);
                EditorGUILayout.EndHorizontal();
            }

            // Recursively draw subdirectories and files
            foreach (string subDir in directories.Where(d => d.StartsWith(dir + Path.DirectorySeparatorChar))) {
                DrawDirectoryAndFiles(subDir);
            }
        }

        GUILayout.Space(10);
    }




    private void ApplyAssistantIdToInterfacer() {
        // Find the GPTInterfacer in the active scene
        GPTInterfacer gptInterfacer = FindObjectOfType<GPTInterfacer>();
        if (gptInterfacer != null) {
            gptInterfacer.AssistantID = assistantId;
            Debug.Log($"AssistantID set to GPTInterfacer: {assistantId}");
        } else {
            Debug.LogWarning("GPTInterfacer not found in the scene.");
        }
        EditorUtility.SetDirty(gptInterfacer);
    }

    private async void UploadFileToAssistantAsync(string filePath) {
        if (string.IsNullOrEmpty(assignedAssistant.Id) || !vectorStores.Any()) {
            Debug.LogError("No assistant assigned or no vector stores available.");
            return;
        }

        try {
            string vectorStoreId = vectorStores.First().Id;
            await gptUtilities.CreateAndUploadVectorStoreFile(vectorStoreId, filePath);
            Debug.Log($"File uploaded and associated with vector store ID {vectorStoreId}.");
            LoadAssistant(assignedAssistant.Id); // Refresh the assistant details and files
        } catch (Exception e) {
            Debug.LogError($"Failed to upload file: {e.Message}");
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

    private void LoadAllDirectories() {
        directories.Clear();
        directorySelection.Clear();
        directoryFoldouts.Clear();
        directoryFiles.Clear();
        directoryFileSelection.Clear();
        additionalDirectories.Clear();

        // Load directories and files within Assets
        string[] allDirectories = Directory.GetDirectories(Application.dataPath, "*", SearchOption.AllDirectories);
        foreach (string dir in allDirectories) {
            string relativePath = "Assets" + dir.Substring(Application.dataPath.Length);
            if (LoadDirectoryFiles(dir, relativePath)) {
                directories.Add(relativePath);
                directorySelection[relativePath] = false;
                directoryFoldouts[relativePath] = false;
            }
        }

        // Load directories and files within Library/PackageCache
        string libraryPackageCachePath = Path.Combine(Directory.GetParent(Application.dataPath).FullName, "Library/PackageCache");
        if (Directory.Exists(libraryPackageCachePath)) {
            string[] packageCacheDirectories = Directory.GetDirectories(libraryPackageCachePath, "*", SearchOption.AllDirectories);
            foreach (string dir in packageCacheDirectories) {
                string relativePath = "Library/PackageCache" + dir.Substring(libraryPackageCachePath.Length);
                if (LoadDirectoryFiles(dir, relativePath)) {
                    additionalDirectories.Add(relativePath);
                    directorySelection[relativePath] = false;
                    directoryFoldouts[relativePath] = false;
                }
            }
        }
    }

    private bool LoadDirectoryFiles(string dir, string relativePath) {
        // Load relevant files within the directory
        string[] files = Directory.GetFiles(dir)
            .Where(file => file.EndsWith(".cs", StringComparison.OrdinalIgnoreCase) ||
                           file.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase) ||
                           file.EndsWith(".md", StringComparison.OrdinalIgnoreCase) ||
                           file.EndsWith(".txt", StringComparison.OrdinalIgnoreCase))
            .ToArray();

        if (files.Length == 0) {
            return false; // Skip directories that don't contain relevant files
        }

        List<string> relativeFiles = new List<string>();
        Dictionary<string, bool> fileSelection = new Dictionary<string, bool>();
        foreach (string file in files) {
            string relativeFilePath = relativePath + file.Substring(dir.Length);
            relativeFiles.Add(relativeFilePath);
            fileSelection[relativeFilePath] = false; // Initialize selection state
        }
        directoryFiles[relativePath] = relativeFiles;
        directoryFileSelection[relativePath] = fileSelection;

        // Recursively load subdirectories
        string[] subDirectories = Directory.GetDirectories(dir);
        foreach (string subDir in subDirectories) {
            string subRelativePath = relativePath + subDir.Substring(dir.Length);
            if (LoadDirectoryFiles(subDir, subRelativePath)) {
                directories.Add(subRelativePath);
                directorySelection[subRelativePath] = false;
                directoryFoldouts[subRelativePath] = false;
            }
        }

        return true; // Include this directory
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


    private async void LoadAssistant(string assistantIdToLoad) {
        var api = new OpenAIClient();
        assignedAssistant = await api.AssistantsEndpoint.RetrieveAssistantAsync(assistantIdToLoad);
        var filesListResponse = await api.AssistantsEndpoint.ListFilesAsync(assistantIdToLoad);
        assistantFiles = filesListResponse.Items.ToList();
        assistantId = assistantIdToLoad;

        // Apply the assistantId to the GPTInterfacer
        ApplyAssistantIdToInterfacer();

        EditorPrefs.SetString("AssistantId", assistantId);
        Debug.Log($"Loaded assistant {assistantId}");

        // Retrieve vector stores for the assistant
        await ListVectorStoresForAssistantAsync();

        Repaint();
    }

    private async Task ListVectorStoresForAssistantAsync() {
        try {
            var response = await gptUtilities.vectorStoreAPI.GetVectorStoresAsync();
            vectorStores = response.Data;
            if (vectorStores.Any()) {
                Debug.Log("Vector stores retrieved successfully.");
            } else {
                Debug.Log("No vector stores found for the assistant.");
            }
            Repaint();
        } catch (Exception e) {
            Debug.LogError($"Failed to retrieve vector stores: {e.Message}");
        }
    }



    private async void ListAssistantsAsync() {
        var api = new OpenAIClient();
        var assistantsListResponse = await api.AssistantsEndpoint.ListAssistantsAsync();
        assistantsList = assistantsListResponse.Items.ToList();
        showAssistantsList = true; // Show the list after fetching
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

    private void UnloadAssistant() {
        assignedAssistant = null;
        assistantFiles.Clear();
        assistantId = string.Empty;
        EditorPrefs.SetString("AssistantId", assistantId);
        Debug.Log("Unloaded assistant");
        Repaint();
    }

}
