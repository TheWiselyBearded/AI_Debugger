using UnityEngine;
using UnityEditor;
using System;
using System.Linq;
using OpenAI;
using OpenAI.Assistants;
using System.Threading.Tasks;
using OpenAI.Files;
using OpenAI.Threads;

[Serializable]
public class FileReference
{
    public string assetPath;
    public bool markedForRemoval;
}

public class GPTAssistantBuilder : EditorWindow
{
    private FileReference[] files = new FileReference[0];
    private const string EditorPrefKey = "GPTAssistantFiles";
    private float feedbackTimer = 0f;
    private float feedbackDuration = 1.0f;

    private string assistantId;
    private string fileID;

    [MenuItem("Tools/GPT Assistant Builder")]
    public static void ShowWindow()
    {
        GetWindow<GPTAssistantBuilder>("GPT Assistant Builder");
    }

    private void OnEnable()
    {
        LoadFilesFromEditorPrefs();
        LoadAssistantId();
        if (!string.IsNullOrEmpty(assistantId))
        {
            DisplayAssistantDetails(assistantId);
        }
        else
        {
            ListAssistants();
        }
    }

    private void OnDisable()
    {
        SaveFilesToEditorPrefs();
    }

    private void OnGUI()
    {
        GUILayout.Label("GPT Assistant Builder", EditorStyles.boldLabel);

        GUILayout.Space(10);

        EditorGUILayout.LabelField("Drag and drop files here:");

        Rect dropArea = GUILayoutUtility.GetRect(0f, 50f, GUILayout.ExpandWidth(true));
        GUI.Box(dropArea, "Drag and drop files here");
        UnityEngine.Event currentEvent = UnityEngine.Event.current;

        if (currentEvent.type == EventType.DragUpdated)
        {
            DragAndDrop.visualMode = DragAndDropVisualMode.Copy;
        }

        if (currentEvent.type == EventType.DragPerform)
        {
            DragAndDrop.AcceptDrag();
            feedbackTimer = Time.realtimeSinceStartup;

            foreach (UnityEngine.Object draggedObject in DragAndDrop.objectReferences)
            {
                string assetPath = AssetDatabase.GetAssetPath(draggedObject);

                if (!files.Any(file => file.assetPath == assetPath))
                {
                    Array.Resize(ref files, files.Length + 1);
                    files[files.Length - 1] = new FileReference { assetPath = assetPath };
                }
            }
        }

        if (Time.realtimeSinceStartup - feedbackTimer < feedbackDuration)
        {
            EditorGUI.DrawRect(dropArea, new Color(0.5f, 1f, 0.5f, 0.5f));
        }

        GUILayout.Space(20);

        for (int i = 0; i < files.Length; i++)
        {
            EditorGUILayout.BeginHorizontal();

            files[i].markedForRemoval = EditorGUILayout.Toggle(files[i].markedForRemoval, GUILayout.Width(20));
            EditorGUILayout.LabelField("Added File:", AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(files[i].assetPath).name);

            if (GUILayout.Button("Remove", GUILayout.Width(80)))
            {
                RemoveFileAtIndex(i);
            }

            EditorGUILayout.EndHorizontal();
        }

        GUILayout.Space(20);

        if (GUILayout.Button("Save Files to EditorPrefs"))
        {
            SaveFilesToEditorPrefs();
        }

        // Display assistant details
        if (!string.IsNullOrEmpty(assistantId))
        {
            GUILayout.Label($"Assistant ID: {assistantId}");
            // Display other assistant details
        }

        if (GUILayout.Button("Create New Assistant"))
        {
            CreateAssistant();
        }

        if (GUILayout.Button("Attach Files to Assistant"))
        {
            AttachFilesToAssistant();
        }

        if (!string.IsNullOrEmpty(assistantId) && GUILayout.Button("Delete Assistant"))
        {
            DeleteAssistant();
            assistantId = "";
        }
    }

    private void RemoveFileAtIndex(int index)
    {
        if (index >= 0 && index < files.Length)
        {
            FileReference[] newFiles = new FileReference[files.Length - 1];
            int newIndex = 0;

            for (int i = 0; i < files.Length; i++)
            {
                if (i != index)
                {
                    newFiles[newIndex] = files[i];
                    newIndex++;
                }
            }

            files = newFiles;
        }
    }

    private void SaveFilesToEditorPrefs()
    {
        for (int i = 0; i < files.Length; i++)
        {
            Debug.Log("Saved File: " + AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(files[i].assetPath).name);
        }

        string[] assetPaths = files.Select(file => file.assetPath).ToArray();
        bool[] removalStates = files.Select(file => file.markedForRemoval).ToArray();

        EditorPrefs.SetString(EditorPrefKey + "Paths", string.Join(",", assetPaths));
        EditorPrefs.SetString(EditorPrefKey + "States", string.Join(",", removalStates));
        EditorPrefs.SetInt(EditorPrefKey + "Count", files.Length);
    }

    private void LoadFilesFromEditorPrefs()
    {
        if (EditorPrefs.HasKey(EditorPrefKey + "Paths") && EditorPrefs.HasKey(EditorPrefKey + "States") && EditorPrefs.HasKey(EditorPrefKey + "Count"))
        {
            string[] assetPaths = EditorPrefs.GetString(EditorPrefKey + "Paths").Split(',');
            string[] removalStateStrings = EditorPrefs.GetString(EditorPrefKey + "States").Split(',');
            int count = EditorPrefs.GetInt(EditorPrefKey + "Count");

            FileReference[] loadedFiles = new FileReference[count];

            for (int i = 0; i < count; i++)
            {
                bool markedForRemoval = bool.Parse(removalStateStrings[i]);
                loadedFiles[i] = new FileReference { assetPath = assetPaths[i], markedForRemoval = markedForRemoval };
            }

            files = loadedFiles;
        }
    }

    // Method to save and load assistant ID
    private void LoadAssistantId()
    {
        assistantId = EditorPrefs.GetString("AssistantId", "");
    }

    private void DisplayAssistantDetails(string assistantId)
    {
        // Fetch and display details like ID, name, date created, file names
        // You'll need to call RetrieveAssistant and ListAssistantFiles
    }

    public async void ListAssistants()
    {
        var api = new OpenAIClient();
        var assistantsList = await api.AssistantsEndpoint.ListAssistantsAsync();

        foreach (var assistant in assistantsList.Items)
        {
            Debug.Log($"{assistant} -> {assistant.CreatedAt}, ID {assistant.Id}");
        }
    }

    public async void CreateAssistant()
    {
        var api = new OpenAIClient();
        var request = new CreateAssistantRequest("gpt-4-1106-preview");
        var assistant = await api.AssistantsEndpoint.CreateAssistantAsync(request);
        Debug.Log($"Assistant ID {assistant.Id}");

        // Save the assistant ID
        EditorPrefs.SetString("AssistantId", assistant.Id);
        assistantId = assistant.Id;

        await Task.Delay(2000);
        /*
        // Implement file upload and attachment logic
        foreach (var fileRef in files)
        {
            //var fileUploadRequest = new FileUploadRequest(fileRef.assetPath, "assistant");
            //var file = await api.FilesEndpoint.UploadFileAsync(fileUploadRequest);
            //var assistantFile = await api.AssistantsEndpoint.AttachFileAsync(assistantId, file);
            var assistantFile = await assistant.UploadFileAsync(fileRef.assetPath);
            await Task.Delay(1000);
        }*/
        Debug.Log("Sent off api requests to create assistant");
    }



    public async Task<AssistantResponse> RetrieveAssistant()
    {
        var api = new OpenAIClient();
        var assistant = await api.AssistantsEndpoint.RetrieveAssistantAsync(assistantId);
        Debug.Log($"{assistant} -> {assistant.CreatedAt}, ID {assistant.Id}");
        return assistant;
    }

    public async void DeleteAssistant()
    {
        var api = new OpenAIClient();
        var isDeleted = await api.AssistantsEndpoint.DeleteAssistantAsync(assistantId);
        
    }

    public async void ListAssistantFiles()
    {
        var api = new OpenAIClient();
        var filesList = await api.AssistantsEndpoint.ListFilesAsync(assistantId);
        
        foreach (var file in filesList.Items)
        {
            Debug.Log($"{file.AssistantId}'s file -> {file.Id}");
        }
    }

    public async void AttachFilesToAssistant()
    {
        /*var api = new OpenAIClient();

        foreach (var fileRef in files)
        {
            string absoluteFilePath = System.IO.Path.GetFullPath(fileRef.assetPath);
            Debug.Log($"Uploading file: {absoluteFilePath}");

            var fileUploadRequest = new FileUploadRequest(absoluteFilePath, "assistant");
            var file = await api.FilesEndpoint.UploadFileAsync(fileUploadRequest);
            Debug.Log($"Uploaded file ID: {file.Id}");

            var assistantFile = await api.AssistantsEndpoint.AttachFileAsync(assistantId, file);
            Debug.Log($"Attached file {file.Id} to assistant {assistantId}");
            
            await Task.Delay(1000); // Delay for 1 second
        }*/
        var api = new OpenAIClient();
        var fileData = await api.FilesEndpoint.UploadFileAsync(files[0].assetPath, "assistants");
        var assistantFile = await api.AssistantsEndpoint.AttachFileAsync(assistantId, new FileResponse(fileData.Id, fileData.Object, fileData.Size, fileData.CreatedUnixTimeSeconds, fileData.FileName, fileData.Purpose, fileData.Status));
        Debug.Log($"Attached file {fileData.Id} to assistant {assistantId}");
    }


}
