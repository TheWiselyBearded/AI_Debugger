using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using OpenAI.Chat;
using OpenAI;
using System.Threading;
using Unity.Collections.LowLevel.Unsafe;
using System;
using UnityEngine.EventSystems;
using OpenAI.Models;
using System.Threading.Tasks;
using System.Reflection;
using System.Text;
using System.Linq;
using OpenAI.Samples.Chat;
using UnityEngine.UIElements;
using System.Text.RegularExpressions;
using OpenAI.Threads;
using UnityEditor.VersionControl;
using Utilities.WebRequestRest;
//using Unity.VisualScripting;
using System.IO;
using OpenAI.Files;

public class GPTReflectionAnalysis : MonoBehaviour
{
    public ChatWindow chatWindow;
    public ReflectionRuntimeController componentController; // Reference to your component controller
    private OpenAIClient openAI; // OpenAI Client
    public string AssistantID;
    public string AssistantIDGPT3;
    public string AssistantIDGPT4;

    public Dictionary<string, MessageResponse> gptDebugMessages;

    public KeywordEvent[] keywordEvents;


    protected bool saveMode;
    private static bool isChatPending;  // manage state of chat requests to prevent spamming


    #region GPTAssistantVariables
    private string threadID;
    private string messageID;
    private string runID;
    protected OpenAI.Threads.ThreadResponse GPTthread;
    private const int GPT4_CHARACTERLIMIT = 32768;
    #endregion

    private void Awake()
    {
        // Initialize the OpenAI Client
        openAI = new OpenAIClient();
        gptDebugMessages = new Dictionary<string, MessageResponse>();
    }


    protected void Start()
    {
        chatWindow.inputField.onSubmit.AddListener(SubmitChat);
        ChatWindow.onSTT += ProcessVoiceInput;
    }

    public void ProcessVoiceInput(string voiceInput) => RetrieveAssistant(voiceInput);

    /// <summary>
    /// invoked via settings button
    /// </summary>    
    public void SetSaveMode(bool status) => saveMode = status;

    /// <summary>
    /// invoked via settings button
    /// </summary>
    public void SetGPT3Mode(bool status) {
        if (status) AssistantID = AssistantIDGPT3;
        else AssistantID = AssistantIDGPT4;
    }

    protected void OnDestroy()
    {
        if (saveMode) {
            WriteConversationToFile();
        }
        gptDebugMessages = null;
        chatWindow.inputField.onSubmit.RemoveAllListeners();
        ChatWindow.onSTT -= ProcessVoiceInput;
    }

    public void SubmitChat(string _) => SubmitChat();

    /// <summary>
    /// invoked externally via button press/mapping
    /// </summary>
    public void AnalyzeComponents()
    {
        // Format the data from your ComponentRuntimeController into a string for GPT analysis
        string dataForGPT = FormatDataForGPT(componentController.classCollection);
        // Pre-prompt for the GPT query
        string gptPrompt = "Given the following snapshot of the runtime environment with classes, methods, and variables, can you analyze the relationships among these components and their runtime values? " +
            "Please leverage your knowledge of the code base as well using the documentation that was given to you, specifically looking at the classes specified in this message with respect to your documentation.";

        // Combine the prompt with the data
        string combinedMessage = $"{gptPrompt}\n{dataForGPT}";
        Debug.Log(combinedMessage);
        try { RetrieveAssistant(combinedMessage); }
        catch (Exception e) { Debug.LogError(e); }
        finally {
            //if (lifetimeCancellationTokenSource != null) {}
            isChatPending = false;
        }

    }

    
    private async void RetrieveAssistant(string msg = "What exactly is all the code doing around me and what relationships do the scripts have with one another?")
    {
        isChatPending = true;
        var assistant = await openAI.AssistantsEndpoint.RetrieveAssistantAsync(AssistantID);
        Debug.Log($"{assistant} -> {assistant.CreatedAt}");

        if (threadID == null || threadID == string.Empty)
        {
            GPTthread = await openAI.ThreadsEndpoint.CreateThreadAsync();
            threadID = GPTthread.Id;
        }

        CreateMessageRequest request;
        MessageResponse message;

        if (msg.Length > GPT4_CHARACTERLIMIT) { // check if char count exceed, if so make file, then submit to GPTAssistant
            msg += "Please first make sure to read the file attached to this message before responding.";
            MemoryStream ms = await WriteGPTQueryToStream(msg);
            // Calculate the number of files needed
            int numberOfFiles = (int)Math.Ceiling((double)ms.Length / GPT4_CHARACTERLIMIT);
            List<string> fileIds = new List<string>();
            for (int i = 0; i < numberOfFiles; i++) {
                string tempFilePath = Path.Combine(Application.temporaryCachePath, $"tempFile_{i}_{DateTime.Now:yyyy-MM-dd_HH-mm-ss}.txt");

                using (FileStream file = new FileStream(tempFilePath, System.IO.FileMode.Create, FileAccess.Write)) {
                    // Copy the portion of the MemoryStream into the file
                    ms.Position = i * GPT4_CHARACTERLIMIT;
                    byte[] buffer = new byte[Math.Min(GPT4_CHARACTERLIMIT, ms.Length - ms.Position)];
                    ms.Read(buffer, 0, buffer.Length);
                    file.Write(buffer, 0, buffer.Length);
                }

                var fileData = await openAI.FilesEndpoint.UploadFileAsync(tempFilePath, "assistants");
                Debug.Log($"Exceeded character count, creating file upload req {fileData.Id}");
                fileIds.Add(fileData.Id);
                // Optionally, delete the temporary file after upload
                //File.Delete(tempFilePath);
            }
            request = new CreateMessageRequest(msg, fileIds);            
            message = await openAI.ThreadsEndpoint.CreateMessageAsync(threadID, request);
            //var messageFileMsg = await openAI.ThreadsEndpoint.CreateMessageAsync(threadID, requestFileMsg);
        } else {
            request = new CreateMessageRequest(msg);
            message = await openAI.ThreadsEndpoint.CreateMessageAsync(threadID, request);
        }

        
        messageID = message.Id;
        Debug.Log($"{message.Id}: {message.Role}: {message.PrintContent()}");

        if (!gptDebugMessages.ContainsKey(message.Id))
        {
            gptDebugMessages.Add(message.Id, message);
            UpdateChat($"User: {message.PrintContent()}");
        }

        var run = await GPTthread.CreateRunAsync(assistant);
        Debug.Log($"[{run.Id}] {run.Status} | {run.CreatedAt}");
        runID = run.Id;

        var messageList = await RetrieveAssistantResponseAsync();        
        for (int index = messageList.Items.Count-1; index >= 0; index--)
        {
            var _message = messageList.Items[index];
            Debug.Log($"{_message.Id}: {_message.Role}: {_message.PrintContent()}");
            if (!gptDebugMessages.ContainsKey(_message.Id))
            {
                gptDebugMessages.Add(_message.Id, _message);
                UpdateChat($"{_message.Role}: {_message.PrintContent()}");
            }
        }        
    }


    private async Task<ListResponse<MessageResponse>> RetrieveAssistantResponseAsync()
    {
        var run = await openAI.ThreadsEndpoint.RetrieveRunAsync(threadID, runID);
        //Debug.Log($"[{run.Id}] {run.Status} | {run.CreatedAt}");
        RunStatus status = run.Status;
        while (status != RunStatus.Completed)
        {
            run = await openAI.ThreadsEndpoint.RetrieveRunAsync(threadID, runID);
            Debug.Log($"[{run.Id}] {run.Status} | {run.CreatedAt}");
            status = run.Status;
            await System.Threading.Tasks.Task.Delay(1000);
        }
        var messageList = await openAI.ThreadsEndpoint.ListMessagesAsync(threadID);
        
        return messageList;
    }


    private async void RetrieveAssistantResponse()
    {
        //var message = await openAI.ThreadsEndpoint.RetrieveMessageAsync(threadID, messageID);        
        //Debug.Log($"{message.Id}: {message.Role}: {message.PrintContent()}");
        var run = await openAI.ThreadsEndpoint.RetrieveRunAsync(threadID, runID);
        Debug.Log($"[{run.Id}] {run.Status} | {run.CreatedAt}");
        var messageList = await openAI.ThreadsEndpoint.ListMessagesAsync(threadID);

        foreach (var message in messageList.Items)
        {
            Debug.Log($"{message.Id}: {message.Role}: {message.PrintContent()}");            
        }
    }

    /// <summary>
    /// When character count is exceeded, return a MemoryStream containing the GPT query text.
    /// </summary>
    /// <param name="text">Query Text for GPT</param>
    /// <returns>MemoryStream containing the text</returns>
    private async Task<MemoryStream> WriteGPTQueryToStream(string text) {
        // Create a MemoryStream
        var memoryStream = new MemoryStream();

        // Use StreamWriter to write the text to the MemoryStream
        using (StreamWriter writer = new StreamWriter(memoryStream, Encoding.UTF8, 1024,leaveOpen: true)) {
            await writer.WriteAsync(text);
            await writer.FlushAsync(); // Ensure all data is written to the stream

            // Reset the position of the stream to the beginning
            memoryStream.Position = 0;

            return memoryStream;
        }
    }

    /// <summary>
    /// TODO: Format writing of file to be more properly ordered and formatted (distinguish user/assistant)
    /// </summary>
    private void WriteConversationToFile() {
        // iterate over all messages and create a long string for now
        StringBuilder formattedData = new StringBuilder();

        foreach (MessageResponse mr in gptDebugMessages.Values) {
            formattedData.AppendLine($"{mr.Role}: {mr.PrintContent()}");           
        }
        // Fire and forget method to write to a file asynchronously
        System.Threading.Tasks.Task.Run(async () => {
            string filename = $"ConversationData_{DateTime.Now:yyyy-MM-dd_HH-mm-ss}.txt";
            string path = Path.Combine(Application.streamingAssetsPath,filename);
            using (StreamWriter writer = new StreamWriter(path, false)) {
                await writer.WriteAsync(formattedData.ToString());
                Debug.Log($"Wrote conversation file to {path}");
            }
        });        
    }

    /// <summary>
    /// invoke via button press
    /// </summary>
    public void SubmitChat()
    {
        if (ParseKeyword())
        {
            //componentController.SearchFunctions(ParseFunctionName(chatBehaviour.inputField.text));
            Debug.Log($"Keyword found");
        } else
        {
            //chatBehaviour.SubmitChat(chatBehaviour.inputField.text);
            RetrieveAssistant(chatWindow.inputField.text);
        }
    }

    private string FormatDataForGPT(Dictionary<string, ClassInfo> classCollection)
    {
        StringBuilder formattedData = new StringBuilder();

        foreach (var classEntry in classCollection)
        {
            formattedData.AppendLine($"Class: {classEntry.Key}");

            formattedData.AppendLine("Methods:");
            foreach (var method in classEntry.Value.Methods)
            {
                formattedData.AppendLine($"- {method.Key}: Parameters: {string.Join(", ", method.Value.GetParameters().Select(p => p.ParameterType.Name + " " + p.Name))}, Return Type: {method.Value.ReturnType.Name}");
            }

            formattedData.AppendLine("Variables:");
            foreach (var variable in classEntry.Value.Variables)
            {
                // Retrieve the runtime value of the variable
                object value = classEntry.Value.VariableValues.TryGetValue(variable.Key, out object val) ? val : "Unavailable";
                formattedData.AppendLine($"- {variable.Key}: Type: {variable.Value.FieldType.Name}, Value: {value}");
            }

            formattedData.AppendLine(); // Separator for readability
        }

        return formattedData.ToString();
    }



    public void UpdateChat(string newText) => chatWindow.UpdateChat(newText);



    /// <summary>
    /// Anytime submitchat is invoked, we first search for keywords
    /// Example:
    /// invoke function ScanAndPopulateClasses()
    /// view variables of ChatBehavior
    /// view variables of ColorChangeScript
    /// </summary>
    /// <param name="_tex"></param>
    /// <returns></returns>

    private bool ParseKeyword()
    {
        string input = chatWindow.inputField.text;
        foreach (KeywordEvent k in keywordEvents)
        {
            if (input.Contains(k.Keyword, StringComparison.OrdinalIgnoreCase))
            {
                Debug.Log($"Keyword {k.Keyword}");
                k.keywordEvent.Invoke();
                return true;
            }
        }
        return false;
    }


    public void GetHelpText()
    {
        StringBuilder helpTextStrBuilder = new StringBuilder();
        helpTextStrBuilder.AppendLine("## Available Commands\n");

        foreach (KeywordEvent k in keywordEvents)
        {
            helpTextStrBuilder.AppendLine($"- **{k.Keyword}**: {k.Description}");
        }

        string helpText = helpTextStrBuilder.ToString();
        UpdateChat(helpText);
    }


    public void InvokeFunction()
    {
        string _text = chatWindow.inputField.text;
        string _func = ParseFunctionName(_text);
        if (!string.IsNullOrEmpty(_func))
        {
            Debug.Log($"Function name {_func}");
            componentController.SearchFunctions(_func);
        }
    }

    public void ViewClassVariables()
    {
        string _text = chatWindow.inputField.text;
        string className = ParseClassName(_text, "view variables of ");
        if (!string.IsNullOrEmpty(className))
        {
            Debug.Log($"Viewing variables of class {className}");
            //componentController.PrintAllVariableValues(className);
            // update references
            componentController.ScanAndPopulateClasses();
            string localQueryResponse = componentController.GetAllVariableValuesAsString(className);
            UpdateChat(localQueryResponse);            
        }
    }

    public void ViewVariable()
    {
        string _text = chatWindow.inputField.text;
        string variableName = ParseVariableName(_text, "view variable ");
        if (!string.IsNullOrEmpty(variableName))
        {
            // update references
            componentController.ScanAndPopulateClasses();
            Debug.Log($"Viewing variable {variableName}");
            componentController.PrintVariableValueInAllClasses(variableName);
        }
    }

    


    public string ParseFunctionName(string input)
    {
        // Define a regular expression pattern to match "invoke function" followed by a function name in parentheses
        string pattern = @"invoke\s+function\s+([A-Za-z_][A-Za-z0-9_]*)\s*\(";

        // Use Regex to find a match
        Match match = Regex.Match(input, pattern);
        Debug.Log("attempt to regex match");
        if (match.Success)
        {
            // Extract and return the function name from the matched group
            return match.Groups[1].Value;
        }
        else
        {
            // If no match is found, return null or an empty string, depending on your preference
            return null;
        }
    }

    public string ParseClassName(string input, string patternStart)
    {
        string pattern = patternStart + @"([A-Za-z_][A-Za-z0-9_]*)";
        Match match = Regex.Match(input, pattern);
        if (match.Success)
        {
            return match.Groups[1].Value;
        }
        return null;
    }

    public string ParseVariableName(string input, string patternStart)
    {
        return ParseClassName(input, patternStart); // Reusing the same logic as class name parsing
    }

    
}


[System.Serializable]
public class KeywordEvent
{
    public string Keyword;
    public string Description;
    public UnityEngine.Events.UnityEvent keywordEvent;
}