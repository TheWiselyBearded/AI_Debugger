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
    public ChatBehaviour chatBehaviour;
    public ReflectionRuntimeController componentController; // Reference to your component controller
    private OpenAIClient openAI; // OpenAI Client
    public string AssistantID;

    #region GPTAssistantIDs
    private string threadID;
    private string messageID;
    private string runID;
    #endregion

    private void Start()
    {
        // Initialize the OpenAI Client
        openAI = new OpenAIClient();
    }

    private void Update()
    {
        // Check if the 'Q' key is pressed
        if (Input.GetKeyDown(KeyCode.Q))
        {
            // Call the AnalyzeComponents method
            Debug.Log("running task");
            AnalyzeComponents();
        }
        if (Input.GetKeyDown(KeyCode.LeftArrow)) RetrieveAssistant();
        if (Input.GetKeyDown(KeyCode.RightArrow)) RetrieveAssistantResponse();
    }

    private async void AnalyzeComponents()
    {
        // Format the data from your ComponentRuntimeController into a string for GPT analysis
        string dataForGPT = FormatDataForGPT(componentController.classCollection);

        // Pre-prompt for the GPT query
        string gptPrompt = "Given the following snapshot of the runtime environment with classes, methods, and variables, can you analyze the relationships among these components and their runtime values? Please leverage your knowledge of the code base as well, specifically looking at the classes specified in this message with respect to your documentation.";

        // Combine the prompt with the data
        string combinedMessage = $"{gptPrompt}\n{dataForGPT}";

        Debug.Log(combinedMessage);
        // Create a message list for the chat request
        var messages = new List<OpenAI.Chat.Message>
        {
            new OpenAI.Chat.Message(Role.System, combinedMessage),
        };

        try
        {
            /*var chatRequest = new ChatRequest(messages, Model.GPT3_5_Turbo);
            var result = await openAI.ChatEndpoint.GetCompletionAsync(chatRequest);
            var response = result.ToString();*/
            RetrieveAssistant(combinedMessage);

            //Debug.Log(response);
            
            //ProcessGPTResponse(response);
        }
        catch (Exception e)
        {
            Debug.LogError(e);
        }
        finally
        {
            //if (lifetimeCancellationTokenSource != null) {}
            //isChatPending = false;
        }

    }

    
    private async void RetrieveAssistant(string txt = "What exactly is all the code doing around me and what relationships do the scripts have with one another?")
    {
        var assistant = await openAI.AssistantsEndpoint.RetrieveAssistantAsync(AssistantID);
        Debug.Log($"{assistant} -> {assistant.CreatedAt}");

        var thread = await openAI.ThreadsEndpoint.CreateThreadAsync();
        var request = new CreateMessageRequest(txt);
        var message = await openAI.ThreadsEndpoint.CreateMessageAsync(thread.Id, request);
        threadID = thread.Id;
        // OR use extension method for convenience!
        //var message = await thread.CreateMessageAsync("Hello World!");
        messageID = message.Id;
        Debug.Log($"{message.Id}: {message.Role}: {message.PrintContent()}");

        var run = await thread.CreateRunAsync(assistant);
        Debug.Log($"[{run.Id}] {run.Status} | {run.CreatedAt}");
        runID = run.Id;
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
            UpdateChat($"{message.Role}: {message.PrintContent()}");
        }
    }


    /// <summary>
    /// invoke via button press
    /// </summary>
    public void SubmitChat()
    {
        if (ParseKeyword(chatBehaviour.inputField.text))
        {
            //componentController.SearchFunctions(ParseFunctionName(chatBehaviour.inputField.text));
            Debug.Log($"Keyword found");
        } else
        {
            //chatBehaviour.SubmitChat(chatBehaviour.inputField.text);
            RetrieveAssistant(chatBehaviour.inputField.text);
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


    private void ProcessGPTResponse(string gptResponse)
    {
        // Process the GPT response to extract useful information
        // ...

        Debug.Log("GPT Analysis:\n" + gptResponse);
        chatBehaviour.UpdateChat(gptResponse);
    }


    public void UpdateChat(string newText)
    {
        chatBehaviour.conversation.AppendMessage(new OpenAI.Chat.Message(Role.Assistant, newText));
        //inputField.text = newText;
        var assistantMessageContent = chatBehaviour.AddNewTextMessageContent(Role.Assistant);
        assistantMessageContent.text = newText;
        chatBehaviour.scrollView.verticalNormalizedPosition = 0f;

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
