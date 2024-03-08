using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class MessageColorMode : MonoBehaviour
{
    public enum MessageType
    {
        Sender,
        Reciever
    }

    [SerializeField]
    public MessageUIProperties senderMessage, receiverMessage;
    public RectTransform rectTransform;
    public TextMeshProUGUI messageText;

    public MessageType messageType;
    public Image image;
    public void SetMessageColor() => image.color = messageType == MessageType.Sender ? senderMessage.color : receiverMessage.color;
    public void SetMode(MessageType _messageType)
    {
        messageType = _messageType;
        SetMessageColor();
        // rectTransform.offsetMin = messageType == MessageType.Sender ? senderMessage.offset : receiverMessage.offset;
        
        // Get the current anchored position
        if (messageType == MessageType.Sender)
        {
            Vector2 anchoredPosition = rectTransform.anchoredPosition;
            anchoredPosition.x += 40f;
            rectTransform.anchoredPosition = anchoredPosition;
        }
    }

    public void SetMessageText(string text)
    {
        messageText.SetText(text);
    }


    public void Awake()
    {
        if (image == null) image = GetComponent<Image>();
        if (rectTransform == null) rectTransform = GetComponent<RectTransform>();
    }

    private void Start() => SetMode(messageType);

    public string debugText;
    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.C)) SetMessageText(debugText);
    }
}

[System.Serializable]
public class MessageUIProperties
{
    public Color32 color;
    public Vector2 offset;
}