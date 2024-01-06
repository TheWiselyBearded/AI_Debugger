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

public class GPTReflectionAnalysis : MonoBehaviour
{
    public ChatBehaviour chatBehaviour;
    public ReflectionRuntimeController componentController; // Reference to your component controller
    private OpenAIClient openAI; // OpenAI Client

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
    }

    private async void AnalyzeComponents()
    {
        // Format the data from your ComponentRuntimeController into a string for GPT analysis
        string dataForGPT = FormatDataForGPT(componentController.classCollection);

        // Pre-prompt for the GPT query
        string gptPrompt = "Given the following snapshot of the runtime environment with classes, methods, and variables, can you analyze the relationships among these components and their runtime values?";

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
            var chatRequest = new ChatRequest(messages, Model.GPT3_5_Turbo);
            var result = await openAI.ChatEndpoint.GetCompletionAsync(chatRequest);
            var response = result.ToString();

            //Debug.Log(response);
            
            ProcessGPTResponse(response);
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


    /// <summary>
    /// invoke via button press
    /// </summary>
    public void SubmitChat()
    {
        if (ParseKeyword(chatBehaviour.inputField.text))
        {
            componentController.SearchFunctions(ParseFunctionName(chatBehaviour.inputField.text));
        } else
        {
            chatBehaviour.SubmitChat(chatBehaviour.inputField.text);
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

    public bool ParseKeyword(string _tex)
    {
        Debug.Log($"input text {_tex}");
        if (_tex.Contains("invoke function "))
        {
            string _func = ParseFunctionName(_tex);
            if (_func != null || _func != "")
            {
                Debug.Log($"Function name {_func}");
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

}
