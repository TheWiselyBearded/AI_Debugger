using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Newtonsoft.Json;
using OpenAI;
using OpenAI.Assistants;
using OpenAI.Chat;
using OpenAI.Threads;
using UnityEditor;
using UnityEngine;
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

    public async Task<string> SendMessageAsync(string message) {
        string jsonMessage = JsonConvert.SerializeObject(new { type = "question", content = message });
        conversation.AppendMessage(new OpenAI.Chat.Message(Role.User, message));

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
    /// TODO: Parse json response to obtain content portion then print to log
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
            Debug.LogError($"Error processing assistant response: {ex.Message}");
        }
        return "";
    }
}