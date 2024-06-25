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
using UnityEngine.InputSystem;

public class DopeCoderController : MonoBehaviour
{
    public static DopeCoderController Instance { get; private set; }
    [SerializeField]
    public DopeCoderSettings Settings;

    //[SerializeField]
    //[TextArea(3, 10)]
    //private string systemPrompt = "You are a helpful AI debugging assistant that helps me interface and understand my code with the Reflection library.\n- If an image is requested then use \"![Image](output.jpg)\" to display it.";

    private GameObject dopeCoderInstance;
    public Vector3 interfaceOffset = new Vector3(0.5f, 0, 2f);
    public ReflectionRuntimeController reflectionController;
    [HideInInspector] public KeywordEventManager KeywordEventManager;
    [HideInInspector] public SphereController sphereController;
    [HideInInspector] public UI_Controller uiController;
    [HideInInspector] public GPTInterfacer gptInterfacer;
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
        AssignReferences();
    }

    private void AssignReferences() {
        dopeCoderInstance = transform.GetChild(0).gameObject;
        uiController = GetComponent<UI_Controller>();
        speechController = new SpeechController(speechControllerAudio, speechControllerAudioPanel);
        sphereController = transform.GetChild(0).GetComponentInChildren<SphereController>();        
    }

    void OnActivation() {
        AssignReferences();        

        gptInterfacer.openAI.EnableDebug = Settings.debugMode;
        uiController.recordButton.onClick.AddListener(speechController.ToggleRecording);

        sphereController.SetMode(SphereController.SphereMode.Idle);
        GPTInterfacer.onGPTMessageReceived += UpdateChat;
        SpeechController.onSTT += UpdateChatSTT;

        // Reference to the ReflectionRuntimeController
        if (!Settings.scanOnStart) return;
        //if (gptInterfacer.gptThreadResponse == null) InvokeRepeating(nameof(InvokeActivationOperations), 1f, 1f);
        //else InvokeActivationOperations();
        if (gptInterfacer.gptThreadResponse == null) InvokeActivationOperations();
        else InvokeActivationOperations();
    }

    void OnDeactivation() {
        gptInterfacer.openAI.EnableDebug = Settings.debugMode;
        uiController.recordButton.onClick.RemoveListener(speechController.ToggleRecording);

        sphereController.SetMode(SphereController.SphereMode.Idle);
        GPTInterfacer.onGPTMessageReceived -= UpdateChat;
        SpeechController.onSTT -= UpdateChatSTT;
    }

    public void ActivateInterface() {
        dopeCoderInstance.SetActive(true);
        OnActivation();
        // position in front of user.
        Transform vrCamera = Camera.main.transform;
        // Calculate the new position
        Vector3 newPosition = vrCamera.position + vrCamera.forward * interfaceOffset.z + vrCamera.right * interfaceOffset.x + vrCamera.up * interfaceOffset.y;
        dopeCoderInstance.transform.position = newPosition;
        // align the object's rotation with the camera's rotation (yaw only)
        Vector3 forward = new Vector3(vrCamera.forward.x, 0, vrCamera.forward.z).normalized;
        dopeCoderInstance.transform.rotation = Quaternion.LookRotation(forward);
    }

    public void DisableInterface() {
        dopeCoderInstance.SetActive(false);

    }

    public void ToggleDopeCoderInterface() {                
        if (!dopeCoderInstance.activeInHierarchy) ActivateInterface();
        else DisableInterface();
    }

    public void OnMyAction(InputAction.CallbackContext context) {
        if (context.started)
            Debug.Log("Action was started");
        else if (context.performed) {
            Debug.Log("Action was performed");
            ToggleDopeCoderInterface();
        } else if (context.canceled)
            Debug.Log("Action was cancelled");
    }

    public void InvokeActivationOperations() {
        // Perform the scan
        reflectionController.ScanAndPopulateClasses();

        // Get the JSON formatted runtime values
        //string jsonSnapshot = reflectionController.GetAllVariableValuesAsJson();
        string jsonSnapshot = reflectionController.GetAllClassInfoAsJson();
        //Debug.Log($"Example json {jsonSnapshot}");
        Debug.Log("InvokeActivationOperarions()");
        gptInterfacer.SendRuntimeScanAssistantAsync(jsonSnapshot, true);    // Send the snapshot to GPT Assistant

        CancelInvoke(nameof(InvokeActivationOperations));
    }

    public void ToggleTTS(bool toggle)
    {
        Settings.tts = toggle;
        speechController.EnableAudioInterface(toggle);
    }

    public void ToggleScanOnStart(bool toggle) => Settings.scanOnStart = toggle;        
    

    public void UpdateChat(string newText, MessageColorMode.MessageType msgType = MessageColorMode.MessageType.Sender)
    {
        uiController.UpdateChat(newText, msgType);
        if (Settings.tts && msgType == MessageColorMode.MessageType.Receiver) sphereController.SetMode(SphereController.SphereMode.Talking);
        if (msgType == MessageColorMode.MessageType.Sender) sphereController.SetMode(SphereController.SphereMode.Listening);

    }

    public void UpdateChatSTT(string text)
    {
        uiController.inputField.text = text;
        SubmitChatRequest();
        //uiController.UpdateChat(text, MessageColorMode.MessageType.Sender);
        //gptInterfacer.SubmitAssistantResponseRequest(text);
        //gptInterfacer.SubmitChatStreamRequst(text);
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
            ///componentController.SearchFunctions(ParseFunctionName(chatBehaviour.inputField.text));
            Debug.Log("Keyword found, invoking event");
            //uiController.ToggleInput(true); // bc chat request is async in else block, we toggle ui back here for local commands
        }
        else
        {
            gptInterfacer.SubmitAssistantResponseRequest(uiController.inputField.text);
            //gptInterfacer.SubmitChatStreamRequst(uiController.inputField.text);            
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
    public bool scanOnStart;
}