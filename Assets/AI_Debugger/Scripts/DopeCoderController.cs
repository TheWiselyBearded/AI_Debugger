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

    public SphereController sphereController;
    public ReflectionRuntimeController componentController;
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

    public void ToggleTTS(bool toggle)
    {
        Settings.tts = toggle;
        speechController.EnableAudioInterface(toggle);
    }

    public void UpdateChat(string newText, MessageColorMode.MessageType msgType = MessageColorMode.MessageType.Sender)
    {
        uiController.UpdateChat(newText, msgType);
        if (msgType == MessageColorMode.MessageType.Reciever) sphereController.SetMode(SphereController.SphereMode.Talking);
        if (msgType == MessageColorMode.MessageType.Sender) sphereController.SetMode(SphereController.SphereMode.Listening);

    }

    public void UpdateChatSTT(string text) => uiController.UpdateChat(text, MessageColorMode.MessageType.Sender);

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
    public void SubmitChat(string _) => SubmitChat();
    private async void SubmitChat()
    {
        sphereController.SetMode(SphereController.SphereMode.Listening);
        if (isChatPending || string.IsNullOrWhiteSpace(uiController.inputField.text)) { return; }
        isChatPending = true;


        uiController.inputField.ReleaseSelection();
        uiController.inputField.interactable = false;
        uiController.submitButton.interactable = false;

        var userMessageContent = uiController.AddNewTextMessageContent(Role.User);
        userMessageContent.text = $"User: {uiController.inputField.text}";

        gptInterfacer.SubmitChatStreamRequst(uiController.inputField.text);
        uiController.inputField.text = string.Empty;
    }

    /// <summary>
    /// invoked externally via button press/mapping
    /// </summary>
    public void AnalyzeComponents()
    {
        // Format the data from your ComponentRuntimeController into a string for GPT analysis
        string dataForGPT = gptInterfacer.FormatDataForGPT(componentController.classCollection);
        // Pre-prompt for the GPT query
        string gptPrompt = "Given the following snapshot of the runtime environment with classes, methods, and variables, can you analyze the relationships among these components and their runtime values? " +
            "Please leverage your knowledge of the code base as well using the documentation that was given to you, specifically looking at the classes specified in this message with respect to your documentation.";

        // Combine the prompt with the data
        string combinedMessage = $"{gptPrompt}\n{dataForGPT}";
        Debug.Log(combinedMessage);
        try { gptInterfacer.SubmitAssistantResponseRequest(combinedMessage); }
        catch (Exception e) { Debug.LogError(e); }
        finally
        {
            //if (lifetimeCancellationTokenSource != null) {}
            isChatPending = false;
        }

    }
}


[System.Serializable]
public class DopeCoderSettings
{
    public bool debugMode;
    public bool tts;
    public bool saveLogs;
}