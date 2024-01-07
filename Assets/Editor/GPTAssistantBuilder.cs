using UnityEngine;
using UnityEditor;
using System;
using System.Linq;

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

    [MenuItem("Tools/GPT Assistant Builder")]
    public static void ShowWindow()
    {
        GetWindow<GPTAssistantBuilder>("GPT Assistant Builder");
    }

    private void OnEnable()
    {
        LoadFilesFromEditorPrefs();
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
        Event currentEvent = Event.current;

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
}
