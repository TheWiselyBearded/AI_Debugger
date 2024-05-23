using OpenAI.Chat;
using OpenAI;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using Newtonsoft.Json;
using OpenAI.Images;
using System.Linq;
using System.Threading.Tasks;
using System;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UnityEngine.UIElements;
using Utilities.WebRequestRest;

public class DopeCoderController : MonoBehaviour
{
    public static DopeCoderController Instance { get; private set; }
    [SerializeField]
    public DopeCoderSettings Settings;

    [SerializeField]
    [TextArea(3, 10)]
    private string systemPrompt = "You are a helpful AI debugging assistant that helps me interface and understand my code with the Reflection library.\n- If an image is requested then use \"![Image](output.jpg)\" to display it.";

    public KeywordEventManager KeywordEventManager;
    public SphereController sphereController;
    public ReflectionRuntimeController reflectionController;
    public UI_Controller uiController;
    public GPTInterfacer gptInterfacer;
    [Header("Speech Controller Properties")]
    public AudioSource speechControllerAudio;
    public GameObject speechControllerAudioPanel;
    public SpeechController speechController;

    private static bool isChatPending;

    

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this);
            return;
        }

        Instance = this;

        uiController = GetComponentInChildren<UI_Controller>();
        speechController = new SpeechController(speechControllerAudio, speechControllerAudioPanel);               
    }

    private void Start()
    {
        gptInterfacer.openAI.EnableDebug = Settings.debugMode;
        gptInterfacer.conversation.AppendMessage(new Message(Role.System, systemPrompt));
        //inputField.onSubmit.AddListener(SubmitChat);
        //submitButton.onClick.AddListener(SubmitChat);
        uiController.recordButton.onClick.AddListener(speechController.ToggleRecording);

        sphereController.SetMode(SphereController.SphereMode.Idle);
        GPTInterfacer.onGPTMessageReceived += UpdateChat;
        SpeechController.onSTT += UpdateChatSTT;
    }

    void OnEnable() {
        // Reference to the ReflectionRuntimeController        
        if (gptInterfacer.gptThreadResponse == null) InvokeRepeating(nameof(InvokeActivationOperations), 1f, 1f);
        else InvokeActivationOperations();        
    }

    public void InvokeActivationOperations() {
        // Perform the scan
        reflectionController.ScanAndPopulateClasses();

        // Get the JSON formatted runtime values
        string jsonSnapshot = reflectionController.GetAllVariableValuesAsJson();
        //Debug.Log($"Example json {jsonSnapshot}");
        gptInterfacer.SendRuntimeScanAssistantAsync(jsonSnapshot, true);    // Send the snapshot to GPT Assistant

        CancelInvoke(nameof(InvokeActivationOperations));
    }

    public void ToggleTTS(bool toggle)
    {
        Settings.tts = toggle;
        speechController.EnableAudioInterface(toggle);
    }

    public void UpdateChat(string newText, MessageColorMode.MessageType msgType = MessageColorMode.MessageType.Sender)
    {
        uiController.UpdateChat(newText, msgType);
        if (msgType == MessageColorMode.MessageType.Receiver) sphereController.SetMode(SphereController.SphereMode.Talking);
        if (msgType == MessageColorMode.MessageType.Sender) sphereController.SetMode(SphereController.SphereMode.Listening);

    }

    public void UpdateChatSTT(string text)
    {
        uiController.UpdateChat(text, MessageColorMode.MessageType.Sender);
        gptInterfacer.SubmitChatStreamRequst(text);
    }

    private void OnDestroy()
    {
        GPTInterfacer.onGPTMessageReceived -= UpdateChat;
        SpeechController.onSTT -= UpdateChatSTT;
        speechController.Destroy();
    }


    /// <summary>
    /// invoked via gui elements/event maps
    /// </summary>
    /// <param name="_">user input query via unity input field</param>
    public void SubmitChat(string _) => SubmitChatRequest();
    public void SubmitChat() => SubmitChatRequest();
    private void SubmitChatRequest()
    {
        sphereController.SetMode(SphereController.SphereMode.Listening);
        if (isChatPending || string.IsNullOrWhiteSpace(uiController.inputField.text)) { return; }
        //isChatPending = true;


        //uiController.ToggleInput(false);

        var userMessageContent = uiController.AddNewTextMessageContent(Role.User);
        userMessageContent.text = $"User: {uiController.inputField.text}";

        if (KeywordEventManager != null && KeywordEventManager.ParseKeyword())
        {
            //componentController.SearchFunctions(ParseFunctionName(chatBehaviour.inputField.text));
            Debug.Log("Keyword found, invoking event");
            //uiController.ToggleInput(true); // bc chat request is async in else block, we toggle ui back here for local commands
        }
        else
        {
            //gptInterfacer.SubmitChatStreamRequst(uiController.inputField.text);            
            gptInterfacer.SubmitAssistantResponseRequest(uiController.inputField.text);
            //_ = gptInterfacer.SendMessageToAssistantAsync(uiController.inputField.text);
        }
        uiController.inputField.text = string.Empty;
    }

    /// <summary>
    /// invoked externally via button press/mapping
    /// </summary>
    public void AnalyzeComponents()
    {
        try {
            reflectionController.ScanAndPopulateClasses();
            string jsonSnapshot = reflectionController.GetAllVariableValuesAsJson();
            gptInterfacer.SendRuntimeScanAssistantAsync(jsonSnapshot, true);    // Send the snapshot to GPT Assistant            
        }
        catch (Exception e) { Debug.LogError(e); }
        finally {
            //if (lifetimeCancellationTokenSource != null) {}
            isChatPending = false;
        }

        /*// Format the data from your ComponentRuntimeController into a string for GPT analysis
        string dataForGPT = gptInterfacer.FormatDataForGPT(reflectionController.classCollection);
        string gptPrompt = "Given the following snapshot of the runtime environment with classes, methods, and variables, can you analyze the relationships among these components and their runtime values? " +
            "Please leverage your knowledge of the code base as well using the documentation that was given to you, specifically looking at the classes specified in this message with respect to your documentation.";       
        string combinedMessage = $"{gptPrompt}\n{dataForGPT}";
        Debug.Log(combinedMessage);
        try { gptInterfacer.SubmitAssistantResponseRequest(combinedMessage); }
        catch (Exception e) { Debug.LogError(e); }
        finally
        {
            //if (lifetimeCancellationTokenSource != null) {}
            isChatPending = false;
        }*/

    }
}


[System.Serializable]
public class DopeCoderSettings
{
    [System.Serializable]
    public enum OperatingMode {
        Basic,
        Debugger,
        Creator
    }
    public OperatingMode operatingMode;
    public bool debugMode;
    public bool tts;
    public bool saveLogs;
}