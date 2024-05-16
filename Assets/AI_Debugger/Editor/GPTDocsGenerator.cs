using UnityEngine;
using UnityEditor;
using System.IO;
using System.Text;
using iTextSharp.text;
using iTextSharp.text.pdf;
using System;
using System.Threading.Tasks;
using System.IO.Compression;

public class GPTDocsGenerator : EditorWindow {
    [MenuItem("Tools/DopeCoder/Scan Codebase")]
    public static void ShowWindow() {
        GetWindow<GPTDocsGenerator>("PDF Generator");
    }

    private string inputDirectory = "Assets"; // Default input directory is the "Assets" folder
    private string outputFileName = "GeneratedPDF.pdf"; // Default output PDF file name
    private string outputZipFileName = "Codebase.zip"; // Default output zip file name
    private string outputFolderPath; // Output folder path
    private float progress = 0;
    private bool isGenerating = false;
    private bool createZip = false; // Toggle option for creating zip file
    private bool includeAllFilesInZip = false; // Toggle option for including all files in the zip

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
        outputZipFileName = EditorGUILayout.TextField("Output Zip File", outputZipFileName);

        GUILayout.Space(10);

        createZip = EditorGUILayout.Toggle("Create Zip File Instead", createZip);
        if (createZip) {
            includeAllFilesInZip = EditorGUILayout.Toggle("Include All Files in Zip", includeAllFilesInZip);
        }

        GUILayout.Space(20);

        if (GUILayout.Button("Generate")) {
            GenerateAsync();
        }

        if (isGenerating) {
            EditorGUILayout.LabelField("Progress:", EditorStyles.boldLabel);
            Rect rect = EditorGUILayout.GetControlRect(false, 20);
            EditorGUI.ProgressBar(rect, progress, "Processing...");
            GUILayout.Space(20);
        }
    }

    private async void GenerateAsync() {
        isGenerating = true;

        var progressIndicator = new Progress<float>(value => {
            progress = value;
            Repaint();
        });

        await Task.Run(() => {
            if (createZip) {
                CreateZip(progressIndicator);
            } else {
                GeneratePDF(progressIndicator);
            }
        });

        isGenerating = false;
        progress = 0;
        string message = createZip ? "Zip file created successfully!" : "PDF file created successfully!";
        EditorUtility.DisplayDialog("Generation Complete", message, "OK");
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

    private void CreateZip(IProgress<float> progress) {
        try {
            string outputPath = Path.Combine(outputFolderPath, outputZipFileName);

            if (File.Exists(outputPath)) {
                File.Delete(outputPath);
            }

            if (includeAllFilesInZip) {
                ZipFile.CreateFromDirectory(inputDirectory, outputPath);
            } else {
                using (ZipArchive zipArchive = ZipFile.Open(outputPath, ZipArchiveMode.Create)) {
                    AddFilesToZip(inputDirectory, zipArchive, "*.cs");
                }
            }

            progress.Report(1.0f); // Report 100% progress
        } catch (Exception e) {
            Debug.LogError("Error: " + e.Message);
        }
    }

    private void AddFilesToZip(string directory, ZipArchive zipArchive, string searchPattern) {
        string[] files = Directory.GetFiles(directory, searchPattern);
        foreach (string file in files) {
            zipArchive.CreateEntryFromFile(file, Path.GetRelativePath(inputDirectory, file));
        }

        string[] subDirectories = Directory.GetDirectories(directory);
        foreach (string subDirectory in subDirectories) {
            AddFilesToZip(subDirectory, zipArchive, searchPattern);
        }
    }
}
