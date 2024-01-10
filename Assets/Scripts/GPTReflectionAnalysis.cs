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

public class GPTReflectionAnalysis : MonoBehaviour
{
    public ChatWindow chatWindow;
    public ReflectionRuntimeController componentController; // Reference to your component controller
    private OpenAIClient openAI; // OpenAI Client
    public string AssistantID;

    public Dictionary<string, MessageResponse> gptDebugMessages;

    private static bool isChatPending;  // manage state of chat requests to prevent spamming

    #region GPTAssistantIDs
    private string threadID;
    private string messageID;
    private string runID;
    protected OpenAI.Threads.ThreadResponse GPTthread;
    #endregion

    private void Awake()
    {
        // Initialize the OpenAI Client
        openAI = new OpenAIClient();
        gptDebugMessages = new Dictionary<string, MessageResponse>();
    }


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

        try
        {
            /*var chatRequest = new ChatRequest(messages, Model.GPT3_5_Turbo);
            var result = await openAI.ChatEndpoint.GetCompletionAsync(chatRequest);
            var response = result.ToString();*/
            RetrieveAssistant(combinedMessage);

            //Debug.Log(response);         
        }
        catch (Exception e)
        {
            Debug.LogError(e);
        }
        finally
        {
            //if (lifetimeCancellationTokenSource != null) {}
            isChatPending = false;
        }

    }

    
    private async void RetrieveAssistant(string txt = "What exactly is all the code doing around me and what relationships do the scripts have with one another?")
    {
        isChatPending = true;
        var assistant = await openAI.AssistantsEndpoint.RetrieveAssistantAsync(AssistantID);
        Debug.Log($"{assistant} -> {assistant.CreatedAt}");

        if (threadID == null || threadID == string.Empty)
        {
            GPTthread = await openAI.ThreadsEndpoint.CreateThreadAsync();
            threadID = GPTthread.Id;
        }
        //txt += " Please provide responses in rich text format (rtf).";
        var request = new CreateMessageRequest(txt);
        var message = await openAI.ThreadsEndpoint.CreateMessageAsync(threadID, request);
        
        messageID = message.Id;
        Debug.Log($"{message.Id}: {message.Role}: {message.PrintContent()}");

        if (!gptDebugMessages.ContainsKey(message.Id))
        {
            gptDebugMessages.Add(message.Id, message);
            UpdateChat($"{message.Role}: {message.PrintContent()}");
        }

        var run = await GPTthread.CreateRunAsync(assistant);
        Debug.Log($"[{run.Id}] {run.Status} | {run.CreatedAt}");
        runID = run.Id;

        var messageList = await RetrieveAssistantResponseAsync();
        // TODO: Remove duplicate message instances, perhaps storing a list at runtime of message instnaces and checking for duplicates,
        // maybe use hashmap to key by message id
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

        /*foreach (var _message in messageList.Items)
        {
            Debug.Log($"{_message.Id}: {_message.Role}: {_message.PrintContent()}");
            if (!gptDebugMessages.ContainsKey(_message.Id))
            {
                gptDebugMessages.Add(_message.Id, _message);
                UpdateChat($"{_message.Role}: {_message.PrintContent()}");
            }
        }*/
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
    /// invoke via button press
    /// </summary>
    public void SubmitChat()
    {
        if (ParseKeyword(chatWindow.inputField.text))
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



    public void UpdateChat(string newText)
    {
        chatWindow.UpdateChat(newText);
        //chatWindow.conversation.AppendMessage(new OpenAI.Chat.Message(Role.Assistant, newText));
        //inputField.text = newText;
        //var assistantMessageContent = chatWindow.AddNewTextMessageContent(Role.Assistant);
        //assistantMessageContent.text = newText;
        //chatWindow.scrollView.verticalNormalizedPosition = 0f;

    }

    /// <summary>
    /// Anytime submitchat is invoked, we first search for keywords
    /// Example:
    /// invoke function ScanAndPopulateClasses()
    /// view variables of ChatBehavior
    /// view variables of ColorChangeScript
    /// </summary>
    /// <param name="_tex"></param>
    /// <returns></returns>
    public bool ParseKeyword(string _text)
    {
        Debug.Log($"Input text {_text}");
        if (_text.Contains("invoke function "))
        {
            string _func = ParseFunctionName(_text);
            if (!string.IsNullOrEmpty(_func))
            {
                Debug.Log($"Function name {_func}");
                componentController.SearchFunctions(_func);
                return true;
            }
        }
        else if (_text.Contains("view variables of "))
        {
            string className = ParseClassName(_text, "view variables of ");
            if (!string.IsNullOrEmpty(className))
            {
                Debug.Log($"Viewing variables of class {className}");
                //componentController.PrintAllVariableValues(className);
                string localQueryResponse = componentController.GetAllVariableValuesAsString(className);
                UpdateChat(localQueryResponse);
                //chatBehaviour.GenerateSpeech(localQueryResponse);
                return true;
            }
        }
        else if (_text.Contains("view variable "))
        {
            string variableName = ParseVariableName(_text, "view variable ");
            if (!string.IsNullOrEmpty(variableName))
            {
                Debug.Log($"Viewing variable {variableName}");
                componentController.PrintVariableValueInAllClasses(variableName);
                return true;
            }
        }
        return false;
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
