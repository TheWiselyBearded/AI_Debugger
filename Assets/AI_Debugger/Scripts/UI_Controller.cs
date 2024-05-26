using LogicUI.FancyTextRendering;
using OpenAI;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Utilities.Extensions;

public class UI_Controller : MonoBehaviour
{
    public GameObject pfb_chatMsgUI;    

    [SerializeField]
    public Button submitButton;

    [SerializeField]
    public Button recordButton;
    public GameObject startRecordingIcon;
    public GameObject stopRecordingIcon;

    [SerializeField]
    public TMP_InputField inputField;

    [SerializeField]
    public RectTransform contentArea;

    [SerializeField]
    public ScrollRect scrollView;
    [SerializeField]
    public GameObject loadingIcon;
    [SerializeField]
    public Image loadingImage;
    private Coroutine loadingCoroutine;

    private void Awake()
    {
        OnValidate();
    }

    private void Start() {
        GPTInterfacer.onStartLoading += GPTInterfacer_onStartLoading;
        GPTInterfacer.onStopLoading += GPTInterfacer_onStopLoading;
    }

    private void OnDestroy() {
        GPTInterfacer.onStartLoading -= GPTInterfacer_onStartLoading;
        GPTInterfacer.onStopLoading -= GPTInterfacer_onStopLoading;
    }

    private void GPTInterfacer_onStartLoading() {
        loadingIcon.SetActive(true);
        if (loadingCoroutine == null) {
            loadingCoroutine = StartCoroutine(UpdateLoadingIcon());
        }
    }

    private void GPTInterfacer_onStopLoading() {
        if (loadingCoroutine != null) {
            StopCoroutine(loadingCoroutine);
            loadingCoroutine = null;
        }
        loadingIcon.SetActive(false);
    }

    private IEnumerator UpdateLoadingIcon() {
        while (true) {
            float elapsedTime = 0f;
            while (elapsedTime < 3f) {
                loadingImage.fillAmount = elapsedTime / 3f;
                elapsedTime += Time.deltaTime;
                yield return null;
            }
            loadingImage.fillAmount = 0f;
        }
    }

    private void OnValidate()
    {
        inputField.Validate();
        contentArea.Validate();
        submitButton.Validate();
        recordButton.Validate();
    }


    public void ToggleInput(bool status)
    {
        inputField.ReleaseSelection();
        inputField.interactable = status;
        submitButton.interactable = status;
    }
    
    public void ToggleMicIcon()
    {
        inputField.text = "";
        if (startRecordingIcon.activeSelf)
        {
            startRecordingIcon.SetActive(false);
            stopRecordingIcon.SetActive(true);
        } else
        {
            startRecordingIcon.SetActive(true);
            stopRecordingIcon.SetActive(false);
        }
    }

    public void UpdateChat(string newText, MessageColorMode.MessageType msgType)
    {
        //inputField.text = newText;
        var assistantMessageContent = AddNewTextMessageContent(msgType == MessageColorMode.MessageType.Sender ? Role.User : Role.Assistant);
        assistantMessageContent.GetComponent<MarkdownRenderer>().Source = newText;
        scrollView.verticalNormalizedPosition = 0f;
        //if (audioPanel.activeInHierarchy && !newText.Contains("User:")) GenerateSpeech(newText);
    }

    public TextMeshProUGUI AddNewTextMessageContent(Role role)
    {
        var textObject = Instantiate(pfb_chatMsgUI, contentArea);
        textObject.name = $"{contentArea.childCount + 1}_{role}";
        //var textObject = new GameObject($"{contentArea.childCount + 1}_{role}");
        //textObject.transform.SetParent(contentArea, false);
        var msgColorMode = textObject.GetComponent<MessageColorMode>();
        if (role == Role.User) msgColorMode.SetMode(MessageColorMode.MessageType.Sender);
        else msgColorMode.SetMode(MessageColorMode.MessageType.Receiver);

        var textMesh = msgColorMode.messageText;
        MarkdownRenderer mr = textMesh.gameObject.AddComponent<MarkdownRenderer>();
        mr.RenderSettings.Monospace.UseCustomFont = false;
        mr.RenderSettings.Lists.BulletOffsetPixels = 40;
        textMesh.fontSize = 24;
        textMesh.enableWordWrapping = true;
        return textMesh;
    }


}
