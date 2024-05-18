using Newtonsoft.Json.Linq;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;
using OpenAI.Threads;
using OpenAI.Chat;
using OpenAI;
using System.Threading;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine.EventSystems;
using Utilities.WebRequestRest;

public class GPTInterfacer : MonoBehaviour
{
    public string AssistantID;
    public string AssistantIDGPT3;
    public string AssistantIDGPT4;
    private bool isChatPending;

    #region OpenAI_Variables
    public OpenAIClient openAI;
    public Conversation conversation = new Conversation();
    public CancellationTokenSource lifetimeCancellationTokenSource;
    #endregion

    public delegate void GPTMessageReceived(string text, MessageColorMode.MessageType messageType);
    public static GPTMessageReceived onGPTMessageReceived;

    #region GPTAssistantVariables
    private string threadID;
    private string messageID;
    private string runID;
    protected ThreadResponse GPTthread;
    private const int GPT4_CHARACTERLIMIT = 32768;
    #endregion

    public Dictionary<string, MessageResponse> gptDebugMessages;

    private void Awake()
    {
        openAI = new OpenAIClient(new OpenAIAuthentication().LoadFromEnvironment());
        lifetimeCancellationTokenSource = new CancellationTokenSource();
        gptDebugMessages = new Dictionary<string, MessageResponse>();
    }


    protected void Start()
    {
        DopeCoderController.Instance.uiController.inputField.onSubmit.AddListener(DopeCoderController.Instance.SubmitChat);      
    }

    protected void OnDestroy()
    {
        if (DopeCoderController.Instance.Settings.saveLogs) WriteConversationToFile();
        
        gptDebugMessages = null;
        DopeCoderController.Instance.uiController.inputField.onSubmit.RemoveAllListeners();

        lifetimeCancellationTokenSource.Cancel();
        lifetimeCancellationTokenSource.Dispose();
        lifetimeCancellationTokenSource = null;

    }


    public async void SubmitAssistantResponseRequest(string msg = "What exactly is all the code doing around me and what relationships do the scripts have with one another?")
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

        // check if char count exceed, if so make file, then submit to GPTAssistant
        if (msg.Length > GPT4_CHARACTERLIMIT)
        { 
            msg += "Please first make sure to read the file attached to this message before responding.";
            MemoryStream ms = await WriteGPTQueryToStream(msg);
            // Calculate the number of files needed
            int numberOfFiles = (int)Math.Ceiling((double)ms.Length / GPT4_CHARACTERLIMIT);
            FileReference[] files = new FileReference[numberOfFiles];
            string[] fileIDs = new string[numberOfFiles];
            for (int i = 0; i < numberOfFiles; i++)
            {
                string tempFilePath = Path.Combine(Application.temporaryCachePath, $"tempFile_{i}_{DateTime.Now:yyyy-MM-dd_HH-mm-ss}.txt");

                using (FileStream file = new FileStream(tempFilePath, System.IO.FileMode.Create, FileAccess.Write))
                {
                    // Copy the portion of the MemoryStream into the file
                    ms.Position = i * GPT4_CHARACTERLIMIT;
                    byte[] buffer = new byte[Math.Min(GPT4_CHARACTERLIMIT, ms.Length - ms.Position)];
                    ms.Read(buffer, 0, buffer.Length);
                    file.Write(buffer, 0, buffer.Length);
                }
                files[i] = new FileReference { assetPath = tempFilePath };

                var fileData = await openAI.FilesEndpoint.UploadFileAsync(files[i].assetPath, "assistants");
                Debug.Log($"Exceeded character count, creating file upload req {fileData.Id}");
                fileIDs[i] = fileData.Id;
                // Optionally, delete the temporary file after upload
                //File.Delete(tempFilePath);
            }
            msg = "okay, based on everything in the text files I've given you in this message thread, what are some of the most important classes you've identified? Please explain how everything works";
            //request = new CreateMessageRequest(msg, new[] { fileIDs[0], fileIDs[1], fileIDs[2] }); 
            request = new CreateMessageRequest(msg, fileIDs);

            message = await openAI.ThreadsEndpoint.CreateMessageAsync(threadID, request);
            //var messageFileMsg = await DopeCoderController.Instance.openAI.ThreadsEndpoint.CreateMessageAsync(threadID, requestFileMsg);
        }
        else
        {
            request = new CreateMessageRequest(msg);
            message = await openAI.ThreadsEndpoint.CreateMessageAsync(threadID, request);
        }


        messageID = message.Id;
        Debug.Log($"{message.Id}: {message.Role}: {message.PrintContent()}");

        if (!gptDebugMessages.ContainsKey(message.Id))
        {
            gptDebugMessages.Add(message.Id, message);
            onGPTMessageReceived?.Invoke($"User: {message.PrintContent()}", MessageColorMode.MessageType.Sender);
        }

        var run = await GPTthread.CreateRunAsync(assistant);
        Debug.Log($"[{run.Id}] {run.Status} | {run.CreatedAt}");
        runID = run.Id;

        var messageList = await RetrieveAssistantResponseAsync();
        for (int index = messageList.Items.Count - 1; index >= 0; index--)
        {
            var _message = messageList.Items[index];
            Debug.Log($"{_message.Id}: {_message.Role}: {_message.PrintContent()}");
            if (!gptDebugMessages.ContainsKey(_message.Id))
            {
                gptDebugMessages.Add(_message.Id, _message);
                onGPTMessageReceived?.Invoke($"{_message.Role}: {_message.PrintContent()}",
                    _message.Role == Role.User ? MessageColorMode.MessageType.Sender : MessageColorMode.MessageType.Reciever);
            }
        }
    }

    public async void SubmitChatStreamRequst(string text)
    {
        conversation.AppendMessage(new OpenAI.Chat.Message(Role.User, text));
        var assistantMessageContent = DopeCoderController.Instance.uiController.AddNewTextMessageContent(Role.Assistant);
        assistantMessageContent.text = "Assistant: ";

        try
        {
            var request = new ChatRequest(conversation.Messages);
            var response = await openAI.ChatEndpoint.StreamCompletionAsync(request, resultHandler: deltaResponse =>
            {
                if (deltaResponse?.FirstChoice?.Delta == null) { return; }

                assistantMessageContent.text += deltaResponse.FirstChoice.Delta.ToString(); // populate response text
                DopeCoderController.Instance.uiController.scrollView.verticalNormalizedPosition = 0f;    // set ui to align with new message
            }, lifetimeCancellationTokenSource.Token);
            
            conversation.AppendMessage(response.FirstChoice.Message);

            onGPTMessageReceived?.Invoke(response, MessageColorMode.MessageType.Reciever);
        }
        catch (Exception e)
        {
            switch (e)
            {
                case TaskCanceledException:
                case OperationCanceledException:
                    break;
                default:
                    Debug.LogError(e);
                    break;
            }
        }
        finally
        {
            if (lifetimeCancellationTokenSource is { IsCancellationRequested: false })
            {
                DopeCoderController.Instance.uiController.inputField.interactable = true;
                EventSystem.current.SetSelectedGameObject(DopeCoderController.Instance.uiController.inputField.gameObject);
                DopeCoderController.Instance.uiController.submitButton.interactable = true;
            }

            isChatPending = false;
        }

    }

    private async Task<ListResponse<MessageResponse>> RetrieveAssistantResponseAsync()
    {
        var run = await openAI.ThreadsEndpoint.RetrieveRunAsync(threadID, runID);
        Debug.Log($"[{run.Id}] {run.Status} | {run.CreatedAt}");
        RunStatus status = run.Status;
        while (status != RunStatus.Completed)
        {
            run = await openAI.ThreadsEndpoint.RetrieveRunAsync(threadID, runID);
            Debug.Log($"[{run.Id}] {run.Status} | {run.CreatedAt}");
            status = run.Status;
            await System.Threading.Tasks.Task.Delay(500);
        }
        var messageList = await openAI.ThreadsEndpoint.ListMessagesAsync(threadID);

        return messageList;
    }

    /// <summary>
    /// When character count is exceeded, return a MemoryStream containing the GPT query text.
    /// </summary>
    /// <param name="text">Query Text for GPT</param>
    /// <returns>MemoryStream containing the text</returns>
    private async Task<MemoryStream> WriteGPTQueryToStream(string text)
    {
        var memoryStream = new MemoryStream();
        using (StreamWriter writer = new StreamWriter(memoryStream, Encoding.UTF8, 1024, leaveOpen: true))
        {
            await writer.WriteAsync(text);
            await writer.FlushAsync(); // Ensure all data is written to the stream

            // Reset the position of the stream to the beginning
            memoryStream.Position = 0;

            return memoryStream;
        }
    }


    public string FormatDataForGPT(Dictionary<string, ClassInfo> classCollection)
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

    /// <summary>
    /// TODO: Format writing of file to be more properly ordered and formatted (distinguish user/assistant)
    /// </summary>
    private void WriteConversationToFile()
    {
        // iterate over all messages and create a long string for now
        StringBuilder formattedData = new StringBuilder();

        foreach (MessageResponse mr in gptDebugMessages.Values)
        {
            formattedData.AppendLine($"{mr.Role}: {mr.PrintContent()}");
        }
        // Fire and forget method to write to a file asynchronously
        System.Threading.Tasks.Task.Run(async () => {
            string filename = $"ConversationData_{DateTime.Now:yyyy-MM-dd_HH-mm-ss}.txt";
            string path = Path.Combine(Application.streamingAssetsPath, filename);
            using (StreamWriter writer = new StreamWriter(path, false))
            {
                await writer.WriteAsync(formattedData.ToString());
                Debug.Log($"Wrote conversation file to {path}");
            }
        });
    }
}
