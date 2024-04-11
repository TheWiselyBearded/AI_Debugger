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

    [SerializeField]
    public TMP_InputField inputField;

    [SerializeField]
    public RectTransform contentArea;

    [SerializeField]
    public ScrollRect scrollView; 
    

    private void Awake()
    {
        OnValidate();
    }

    private void OnValidate()
    {
        inputField.Validate();
        contentArea.Validate();
        submitButton.Validate();
        recordButton.Validate();
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
        else msgColorMode.SetMode(MessageColorMode.MessageType.Reciever);

        var textMesh = msgColorMode.messageText;
        MarkdownRenderer mr = textMesh.gameObject.AddComponent<MarkdownRenderer>();
        mr.RenderSettings.Monospace.UseCustomFont = false;
        mr.RenderSettings.Lists.BulletOffsetPixels = 40;
        textMesh.fontSize = 24;
        textMesh.enableWordWrapping = true;
        return textMesh;
    }
}
