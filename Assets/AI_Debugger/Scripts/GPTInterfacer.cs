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
using OpenAI.Assistants;
using System.Runtime.InteropServices;
using Newtonsoft.Json;
using UnityEngine.TerrainTools;

public class GPTInterfacer : MonoBehaviour
{
    public string AssistantID;
    private bool isChatPending;

    #region OpenAI_Variables
    public OpenAIClient openAI;
    public Conversation conversation = new Conversation();
    public CancellationTokenSource lifetimeCancellationTokenSource;
    #endregion

    public delegate void GPTMessageReceived(string text, MessageColorMode.MessageType messageType);
    public static GPTMessageReceived onGPTMessageReceived;

    public static event System.Action onStartLoading;
    public static event System.Action onStopLoading;

    #region GPTAssistantVariables
    public AssistantResponse assistant;
    public ThreadResponse gptThreadResponse;
    private string threadID;
    private string messageID;
    private string runID;
    private const int GPT4_CHARACTERLIMIT = 32768;
    #endregion

    public Dictionary<string, MessageResponse> gptDebugMessages;

    private void Awake()
    {
        openAI = new OpenAIClient(new OpenAIAuthentication().LoadFromEnvironment());
        lifetimeCancellationTokenSource = new CancellationTokenSource();
        gptDebugMessages = new Dictionary<string, MessageResponse>();
    }


    protected async void Start()
    {
        DopeCoderController.Instance.uiController.inputField.onSubmit.AddListener(DopeCoderController.Instance.SubmitChat);

        await InitializeAssistantSessionAsync();
    }


    public async Task InitializeAssistantSessionAsync() {
        assistant = await openAI.AssistantsEndpoint.RetrieveAssistantAsync(AssistantID);
        gptThreadResponse = await openAI.ThreadsEndpoint.CreateThreadAsync();

        //run = await run.WaitForStatusChangeAsync();        
        // submit the tool outputs
        threadID = gptThreadResponse.Id;
        Debug.Log($"Initialized assistant session: {assistant.Name} with thread ID: {gptThreadResponse.Id}");
    }

    //public async Task<string> SendMessageToAssistantAsync(string message) {
    //    var request = new CreateMessageRequest(message);
    //    var response = await openAI.ThreadsEndpoint.CreateMessageAsync(gptThreadResponse.Id, request);
    //    //onGPTMessageReceived?.Invoke(response.PrintContent(), MessageColorMode.MessageType.Reciever);
    //    return response.PrintContent();
    //}

    public async void SendRuntimeScanAssistantAsync(string message, bool isSnapshot = false) {
        string jsonMessage;
        if (isSnapshot) {
            jsonMessage = "{ \"type\": \"snapshot\", \"content\": " + message + " }";
        } else {
            jsonMessage = message; // Assuming it's already a JSON string for subsequent messages
        }

        conversation.AppendMessage(new OpenAI.Chat.Message(Role.User, jsonMessage));
        string notificationMessage = "{ \"type\": \"update\", \"content\": \"Runtime values have been scanned and shared. Follow-up questions will be provided soon.\" }";
        conversation.AppendMessage(new OpenAI.Chat.Message(Role.User, notificationMessage));
        Debug.Log($"About to issue message {jsonMessage}");

        try {
            if (gptThreadResponse != null) {
                if (jsonMessage.Length > GPT4_CHARACTERLIMIT) {
                    // Notify about the incoming JSON file
                    //string updateMessage = "{ \"type\": \"update\", \"content\": \"The snapshot text is too long, sharing a JSON file instead.\" }";
                    //await gptThreadResponse.CreateMessageAsync(updateMessage);
                    //var run = await gptThreadResponse.CreateRunAsync(assistant);
                    //_ = await AwaitAssistantResponseAsync();
                    // Handle character limit and create JSON file
                    CreateMessageRequest request = await HandleCharacterLimitAndCreateJsonFileAsync(jsonMessage);
                    await gptThreadResponse.CreateMessageAsync(request);
                } else {
                    await gptThreadResponse.CreateMessageAsync(jsonMessage);
                }

                await gptThreadResponse.CreateMessageAsync(notificationMessage);
            } else {
                Debug.LogWarning("gptThreadResponse is null. Unable to create messages.");
            }
        } catch (Exception ex) {
            Debug.LogError($"Error creating messages: {ex.Message}");
        }

        try {
            if (gptThreadResponse != null) {
                var run = await gptThreadResponse.CreateRunAsync(assistant);
                Debug.Log($"[{run.Id}] {run.Status} | {run.CreatedAt}");
                _ = await AwaitAssistantResponseAsync();
            } else {
                Debug.LogWarning("gptThreadResponse is null. Unable to create run.");
            }
        } catch (Exception ex) {
            Debug.LogError($"Error creating run: {ex.Message}");
        }
    }

    private async Task<CreateMessageRequest> HandleCharacterLimitAndCreateJsonFileAsync(string msg) {
        /*if (msg.Length <= GPT4_CHARACTERLIMIT) {
            msg = "{\"type\": \"snapshot\", \"content\":\"" + msg + "\"}";
            return new CreateMessageRequest(msg);
        }*/
        

        //msg += " Please first make sure to read the file attached to this message before responding.";
        //string jsonString = JsonConvert.SerializeObject(new { type = "snapshot", content = msg });

        string tempFilePath = Path.Combine(Application.temporaryCachePath, $"tempFile_{DateTime.Now:yyyy-MM-dd_HH-mm-ss}.json");
        await File.WriteAllTextAsync(tempFilePath, msg);

        var fileData = await openAI.FilesEndpoint.UploadFileAsync(tempFilePath, "assistants");
        Debug.Log($"Exceeded character count, creating JSON file upload req {fileData.Id}");

        //msg = "Okay, based on everything in the JSON file I've given you in this message thread, what are some of the most important classes you've identified? Please explain how everything works.";
        string updateMessage = "{ \"type\": \"update\", \"content\": \"The snapshot text is too long, sharing a JSON file instead.\" }";
        //await gptThreadResponse.CreateMessageAsync(updateMessage);
        return new CreateMessageRequest(updateMessage, new[] { fileData.Id });
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


    public async void SubmitAssistantResponseRequest(string msg = "What exactly is all the code doing around me and what relationships do the scripts have with one another?") {
        isChatPending = true;

        // Retrieve the assistant if not already done
        if (assistant == null || assistant.Id != AssistantID) {
            assistant = await openAI.AssistantsEndpoint.RetrieveAssistantAsync(AssistantID);
            Debug.Log($"{assistant} -> {assistant.CreatedAt}");
        }

        // Create a new thread if not already created
        if (gptThreadResponse == null) {
            gptThreadResponse = await openAI.ThreadsEndpoint.CreateThreadAsync();
            threadID = gptThreadResponse.Id;
        }

        CreateMessageRequest request;
        // Handle character limit and create JSON file
        if (msg.Length > GPT4_CHARACTERLIMIT) {
            // Notify about the incoming JSON file
            //string updateMessage = "{ \"type\": \"update\", \"content\": \"The snapshot text is too long, sharing a JSON file instead.\" }";
            //await gptThreadResponse.CreateMessageAsync(updateMessage);
            // Handle character limit and create JSON file
            request = await HandleCharacterLimitAndCreateJsonFileAsync(msg);
            //await gptThreadResponse.CreateMessageAsync(request);
        } else {
            request = new CreateMessageRequest(msg);
        }


        // Send the message
        MessageResponse message = await openAI.ThreadsEndpoint.CreateMessageAsync(threadID, request);
        messageID = message.Id;
        Debug.Log($"{message.Id}: {message.Role}: {message.PrintContent()}");

        if (!gptDebugMessages.ContainsKey(message.Id)) {
            gptDebugMessages.Add(message.Id, message);
            //onGPTMessageReceived?.Invoke($"User: {message.PrintContent()}", MessageColorMode.MessageType.Sender);
        }

        // Create a run to get the assistant's response
        var run = await gptThreadResponse.CreateRunAsync(assistant);
        Debug.Log($"[{run.Id}] {run.Status} | {run.CreatedAt}");
        runID = run.Id;

        // Retrieve and process the assistant's response
        var messageList = await AwaitAssistantResponseAsync();
        for (int index = messageList.Items.Count - 1; index >= 0; index--) {
            var _message = messageList.Items[index];
            Debug.Log($"{_message.Id}: {_message.Role}: {_message.PrintContent()}");
            if (!gptDebugMessages.ContainsKey(_message.Id)) {
                gptDebugMessages.Add(_message.Id, _message);
                ProcessAssistantResponse(_message.PrintContent());
            }
        }
        isChatPending = false;
    }

    private void ProcessAssistantResponse(string jsonResponse) {
        try {
            var responseObject = JsonConvert.DeserializeObject<AssistantResponseDataType>(jsonResponse);
            if (responseObject?.Type == "response") {
                onGPTMessageReceived?.Invoke(responseObject.Content, MessageColorMode.MessageType.Receiver);
            }
        } catch (Exception ex) {
            Debug.LogError($"Error processing assistant response: {ex.Message}");
        }
    }



    private async Task<CreateMessageRequest> HandleCharacterLimitAndFileUploadsAsync(string msg) {
        if (msg.Length <= GPT4_CHARACTERLIMIT) {
            msg = "{\"type\": \"question\", \"content\":\"" + msg + "\"}";
            return new CreateMessageRequest(msg);
        }

        msg += "Please first make sure to read the file attached to this message before responding.";
        MemoryStream ms = await WriteGPTQueryToStream(msg);
        int numberOfFiles = (int)Math.Ceiling((double)ms.Length / GPT4_CHARACTERLIMIT);
        FileReference[] files = new FileReference[numberOfFiles];
        string[] fileIDs = new string[numberOfFiles];
        for (int i = 0; i < numberOfFiles; i++) {
            string tempFilePath = Path.Combine(Application.temporaryCachePath, $"tempFile_{i}_{DateTime.Now:yyyy-MM-dd_HH-mm-ss}.txt");

            using (FileStream file = new FileStream(tempFilePath, FileMode.Create, FileAccess.Write)) {
                ms.Position = i * GPT4_CHARACTERLIMIT;
                byte[] buffer = new byte[Math.Min(GPT4_CHARACTERLIMIT, ms.Length - ms.Position)];
                ms.Read(buffer, 0, buffer.Length);
                file.Write(buffer, 0, buffer.Length);
            }
            files[i] = new FileReference { assetPath = tempFilePath };

            var fileData = await openAI.FilesEndpoint.UploadFileAsync(files[i].assetPath, "assistants");
            Debug.Log($"Exceeded character count, creating file upload req {fileData.Id}");
            fileIDs[i] = fileData.Id;
        }
        msg = "Okay, based on everything in the text files I've given you in this message thread, what are some of the most important classes you've identified? Please explain how everything works";
        return new CreateMessageRequest(msg, fileIDs);
    }


    

    private async Task<string> WriteMessageToJsonFile(string message, string fileName) {
        string tempFilePath = Path.Combine(Application.temporaryCachePath, fileName);
        var jsonObject = new {
            type = "question",
            content = message
        };
        string jsonString = JsonConvert.SerializeObject(jsonObject);
        await File.WriteAllTextAsync(tempFilePath, jsonString);
        return tempFilePath;
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

            onGPTMessageReceived?.Invoke(response, MessageColorMode.MessageType.Receiver);
            isChatPending = false;
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

    private async Task<ListResponse<MessageResponse>> AwaitAssistantResponseAsync()
    {
        var run = await openAI.ThreadsEndpoint.RetrieveRunAsync(threadID, runID);
        Debug.Log($"[{run.Id}] {run.Status} | {run.CreatedAt}");
        RunStatus status = run.Status;
        onStartLoading?.Invoke(); // start ui moving
        while (status != RunStatus.Completed)
        {            
            run = await openAI.ThreadsEndpoint.RetrieveRunAsync(threadID, runID);
            //Debug.Log($"[{run.Id}] {run.Status} | {run.CreatedAt}");
            status = run.Status;
            await System.Threading.Tasks.Task.Delay(500);
        }        
        onStopLoading?.Invoke(); // deactivate ui
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


[System.Serializable]
public class AssistantResponseDataType {
    [JsonProperty("type")]
    public string Type { get; set; }

    [JsonProperty("content")]
    public string Content { get; set; }
}
