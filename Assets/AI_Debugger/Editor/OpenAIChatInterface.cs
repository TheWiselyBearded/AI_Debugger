using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using GluonGui.WorkspaceWindow.Views.WorkspaceExplorer;
using Newtonsoft.Json;
using OpenAI;
using OpenAI.Assistants;
using OpenAI.Chat;
using OpenAI.Files;
using OpenAI.FineTuning;
using OpenAI.Models;
using OpenAI.Threads;
using Unity.Jobs;
using UnityEditor;
using UnityEngine;
using static Codice.CM.Common.Serialization.PacketFileReader;
using static GPTInterfacer;

public class OpenAIChatInterface {
    private OpenAIClient openAIClient;
    private string assistantID;
    private ThreadResponse threadResponse;
    public AssistantResponse assistant;
    private string threadID;
    private Conversation conversation;
    public Dictionary<string, MessageResponse> assistantResponses;

    public OpenAIChatInterface(string assistantID) {
        assistantResponses = new Dictionary<string, MessageResponse>();
        this.assistantID = assistantID;
        openAIClient = new OpenAIClient(new OpenAIAuthentication().LoadFromEnvironment());
        conversation = new Conversation();
    }

    public async Task InitializeSessionAsync() {
        assistant = await openAIClient.AssistantsEndpoint.RetrieveAssistantAsync(assistantID);
        threadResponse = await openAIClient.ThreadsEndpoint.CreateThreadAsync();
        threadID = threadResponse.Id;
        Debug.Log($"Initialized assistant session: {assistant.Name} with thread ID: {threadID}");
    }

    public async Task<string> SendMessageAsync(string jsonMessage, string content) {
        conversation.AppendMessage(new OpenAI.Chat.Message(Role.User, content));

        if (threadResponse == null) {
            Debug.LogError("Thread response is null. Make sure to initialize the session first.");
            return null;
        }

        var request = new OpenAI.Threads.Message(jsonMessage);
        var response = await openAIClient.ThreadsEndpoint.CreateMessageAsync(threadID, request);
        Debug.Log($"Message sent: {response.Id}");

        // Wait for the assistant's response
        var run = await threadResponse.CreateRunAsync(assistant);
        var newResponses = await AwaitAssistantResponseAsync(run.Id);
        string messageContent = "";
        for (int index = newResponses.Items.Count - 1; index >= 0; index--) {
            var _message = newResponses.Items[index];
            if (!assistantResponses.ContainsKey(_message.Id)) {
                assistantResponses.Add(_message.Id, _message);
                messageContent = ProcessAssistantResponse(_message.PrintContent());
                Debug.Log($"{_message.Id}: {_message.Role}: {_message.PrintContent()}");
            }
        }

        return messageContent;
    }


    /// <summary>    
    /// </summary>
    /// <param name="runID"></param>
    /// <returns></returns>
    private async Task<ListResponse<MessageResponse>> AwaitAssistantResponseAsync(string runID) {
        var run = await openAIClient.ThreadsEndpoint.RetrieveRunAsync(threadID, runID);
        RunStatus status = run.Status;
        while (status != RunStatus.Completed) {
            run = await openAIClient.ThreadsEndpoint.RetrieveRunAsync(threadID, runID);
            status = run.Status;
            await Task.Delay(500);
        }
        var messageList = await openAIClient.ThreadsEndpoint.ListMessagesAsync(threadID);
        return messageList;
    }

    private string ProcessAssistantResponse(string jsonResponse) {
        try {
            var responseObject = JsonConvert.DeserializeObject<AssistantResponseDataType>(jsonResponse);
            if (responseObject?.Type == "response") {
                return responseObject.Content;
            } 
        } catch (Exception ex) {
            //Debug.LogError($"Error processing assistant response: {ex.Message}");
            return jsonResponse;
        }
        return "";
    }


    public async Task<string> SendChatMessage(string message) {
        if (conversation.Messages.Count == 0) {
            conversation.AppendMessage(new OpenAI.Chat.Message(Role.System, "You are a helpful assistant capable of answering Unity specific questions and anything related to Unity from programming to modeling to scene design. Further, if given files, you can make sense of the files."));
            conversation.AppendMessage(new OpenAI.Chat.Message(Role.User, "How can I create a trigger based event for my scene?"));
            conversation.AppendMessage(new OpenAI.Chat.Message(Role.Assistant, "Create a GameObject, then add a box collider with OnTrigger enabled. Write a custom script that then has a OnTriggerEnter(Collder other) method to process collision events."));            
        }
        conversation.AppendMessage(new OpenAI.Chat.Message(Role.User, message));
        var chatRequest = new ChatRequest(conversation.Messages, Model.GPT4o);
        //var response = await openAIClient.ChatEndpoint.StreamCompletionAsync(chatRequest, partialResponse =>
        //{
        //    Debug.Log(partialResponse.FirstChoice.Delta.ToString());
        //});
        var response = await openAIClient.ChatEndpoint.GetCompletionAsync(chatRequest);
        var choice = response.FirstChoice;
        Debug.Log($"[{choice.Index}] {choice.Message.Role}: {choice.Message} | Finish Reason: {choice.FinishReason}");
        return response.FirstChoice.ToString();
    }

    public async Task<string> SendUploadFile(string assetPath) {
        var file = await openAIClient.FilesEndpoint.UploadFileAsync(assetPath, FilePurpose.Assistants);        
        var message = await threadResponse.CreateMessageAsync(new(
            content: "I'd like to discuss this file with you",
            attachments: new[] { new Attachment(file.Id, OpenAI.Tool.FileSearch) }));
        var run = await threadResponse.CreateRunAsync(assistant);
        while (run.Status != RunStatus.Completed) {
            run = await run.WaitForStatusChangeAsync();
            Debug.Log($"status {run.Status}");
        }
        var responseMessage = await threadResponse.ListMessagesAsync();
        foreach (var response in responseMessage.Items.Reverse()) {
            Debug.Log($"{response.Role}: {response.PrintContent()}");
        }
        return responseMessage.Items[0].PrintContent();
    }

}