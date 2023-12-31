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
        var messages = new List<Message>
        {
            new Message(Role.System, combinedMessage),
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

        // Send this data to GPT and handle the response
        /*var response = await openAI.ChatEndpoint.StreamCompletionAsync(chatRequest, partialResponse =>
        {
            Console.Write(partialResponse.FirstChoice.Delta.ToString());
        });

        var choice = response.FirstChoice;*/
        //Debug.Log($"[{choice.Index}] {choice.Message.Role}: {choice.Message.Content} | Finish Reason: {choice.FinishReason}");

        
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
                formattedData.AppendLine($"- {variable.Key}: Type: {variable.Value.FieldType.Name}");
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
}
