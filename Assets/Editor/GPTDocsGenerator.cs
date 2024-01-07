using UnityEngine;
using UnityEditor;
using System.IO;
using System.Text;
using iTextSharp.text;
using iTextSharp.text.pdf;
using Mono.Cecil.Cil;
using System;

public class GPTDocsGenerator : EditorWindow
{
    [MenuItem("Tools/Generate PDF from C# Files")]
    public static void ShowWindow()
    {
        GetWindow<GPTDocsGenerator>("PDF Generator");
    }

    private string inputDirectory = "Assets"; // Default input directory is the "Assets" folder
    private string outputFilePath = "GeneratedPDF.pdf"; // Default output PDF file path

    private void OnGUI()
    {
        GUILayout.Label("PDF Generator", EditorStyles.boldLabel);

        GUILayout.Space(10);

        inputDirectory = EditorGUILayout.TextField("Input Directory", inputDirectory);
        outputFilePath = EditorGUILayout.TextField("Output PDF File", outputFilePath);

        GUILayout.Space(20);

        if (GUILayout.Button("Generate PDF"))
        {
            GeneratePDF();
        }
    }

    private void GeneratePDF()
    {
        iTextSharp.text.Document doc = new iTextSharp.text.Document();

        try
        {
            // Get the path to the StreamingAssets folder
            //string streamingAssetsPath = Path.Combine(Application.dataPath, "StreamingAssets");
            //string outputPath = Path.Combine(streamingAssetsPath, outputFilePath);
            string streamingAssetsPath = Application.streamingAssetsPath;
            string outputPath = Path.Combine(streamingAssetsPath, outputFilePath);

            // Create the StreamingAssets folder if it doesn't exist
            if (!Directory.Exists(streamingAssetsPath))
            {
                Directory.CreateDirectory(streamingAssetsPath);
            }

            PdfWriter writer = PdfWriter.GetInstance(doc, new FileStream(outputPath, FileMode.Create));

            doc.Open();

            AppendCSharpFilesToPDF(doc, inputDirectory);

            doc.Close();

            EditorUtility.DisplayDialog("PDF Generated", "PDF file created and saved to: " + outputPath, "OK");
        }
        catch (Exception e)
        {
            Debug.LogError("Error: " + e.Message);
        }
    }


    private void AppendCSharpFilesToPDF(iTextSharp.text.Document doc, string directory)
    {
        string[] csharpFiles = Directory.GetFiles(directory, "*.cs");

        foreach (string csharpFile in csharpFiles)
        {
            StreamReader reader = new StreamReader(csharpFile, Encoding.UTF8);
            string fileContent = reader.ReadToEnd();
            reader.Close();

            Paragraph paragraph = new Paragraph();
            paragraph.Font = FontFactory.GetFont(FontFactory.COURIER, 12f);
            paragraph.Add(fileContent);

            doc.Add(paragraph);
        }

        string[] subDirectories = Directory.GetDirectories(directory);

        foreach (string subDirectory in subDirectories)
        {
            AppendCSharpFilesToPDF(doc, subDirectory);
        }
    }
}
