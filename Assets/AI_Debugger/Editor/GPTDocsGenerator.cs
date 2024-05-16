using UnityEngine;
using UnityEditor;
using System.IO;
using System.Text;
using iTextSharp.text;
using iTextSharp.text.pdf;
using System;
using System.Threading.Tasks;

public class GPTDocsGenerator : EditorWindow {
    [MenuItem("Tools/DopeCoder/Scan Codebase")]
    public static void ShowWindow() {
        GetWindow<GPTDocsGenerator>("PDF Generator");
    }

    private string inputDirectory = "Assets"; // Default input directory is the "Assets" folder
    private string outputFileName = "GeneratedPDF.pdf"; // Default output PDF file name
    private string outputFolderPath; // Output folder path
    private float progress = 0;
    private bool isGenerating = false;

    private void OnEnable() {
        // Determine the script's directory
        string scriptPath = AssetDatabase.GetAssetPath(MonoScript.FromScriptableObject(this));
        string scriptDirectory = Path.GetDirectoryName(scriptPath);

        // Set the output folder path to a "FileScans" folder within the script's directory
        outputFolderPath = Path.Combine(scriptDirectory, "FileScans");

        // Create the output folder if it doesn't exist
        if (!Directory.Exists(outputFolderPath)) {
            Directory.CreateDirectory(outputFolderPath);
        }
    }

    private void OnGUI() {
        GUILayout.Label("PDF Generator", EditorStyles.boldLabel);

        GUILayout.Space(10);

        inputDirectory = EditorGUILayout.TextField("Input Directory", inputDirectory);
        outputFileName = EditorGUILayout.TextField("Output PDF File", outputFileName);

        GUILayout.Space(20);

        if (GUILayout.Button("Generate PDF")) {
            GeneratePDFAsync();
        }

        if (isGenerating) {
            EditorGUILayout.LabelField("Progress:", EditorStyles.boldLabel);
            Rect rect = EditorGUILayout.GetControlRect(false, 20);
            EditorGUI.ProgressBar(rect, progress, "Generating PDF");
            GUILayout.Space(20);
        }
    }

    private async void GeneratePDFAsync() {
        isGenerating = true;

        var progressIndicator = new Progress<float>(value => {
            progress = value;
            Repaint();
        });

        await Task.Run(() => {
            GeneratePDF(progressIndicator);
        });

        isGenerating = false;
        progress = 0;
        EditorUtility.DisplayDialog("PDF Generated", "PDF file created successfully!", "OK");
        Repaint();
    }

    private void GeneratePDF(IProgress<float> progress) {
        iTextSharp.text.Document doc = new iTextSharp.text.Document();

        try {
            string outputPath = Path.Combine(outputFolderPath, outputFileName);

            PdfWriter writer = PdfWriter.GetInstance(doc, new FileStream(outputPath, FileMode.Create));

            doc.Open();

            AppendCSharpFilesToPDF(doc, inputDirectory, progress);

            doc.Close();
        } catch (Exception e) {
            Debug.LogError("Error: " + e.Message);
        }
    }

    private void AppendCSharpFilesToPDF(iTextSharp.text.Document doc, string directory, IProgress<float> progress) {
        string[] csharpFiles = Directory.GetFiles(directory, "*.cs");
        string[] subDirectories = Directory.GetDirectories(directory);
        int totalFiles = csharpFiles.Length + subDirectories.Length;
        int processedFiles = 0;

        foreach (string csharpFile in csharpFiles) {
            using (StreamReader reader = new StreamReader(csharpFile, Encoding.UTF8)) {
                string fileContent = reader.ReadToEnd();

                Paragraph paragraph = new Paragraph();
                paragraph.Font = FontFactory.GetFont(FontFactory.COURIER, 12f);
                paragraph.Add(fileContent);

                doc.Add(paragraph);
            }

            processedFiles++;
            progress.Report((float)processedFiles / totalFiles);
        }

        foreach (string subDirectory in subDirectories) {
            AppendCSharpFilesToPDF(doc, subDirectory, progress);
        }
    }
}
