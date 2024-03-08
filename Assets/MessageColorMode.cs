using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class MessageColorMode : MonoBehaviour
{
    public enum MessageType
    {
        Sender,
        Reciever
    }

    [SerializeField]
    public MessageUIProperties senderMessage, receiverMessage;
    private RectTransform rectTransform;

    public MessageType messageType;
    public Image image;
    public void SetMessageColor() => image.color = messageType == MessageType.Sender ? senderMessage.color : receiverMessage.color;
    public void SetMode(MessageType _messageType)
    {
        messageType = _messageType;
        SetMessageColor();
        // rectTransform.offsetMin = messageType == MessageType.Sender ? senderMessage.offset : receiverMessage.offset;
        // Get the current anchored position
        Vector2 anchoredPosition = rectTransform.anchoredPosition;
        anchoredPosition.x += 80f;

        // Set the new anchored position
        rectTransform.anchoredPosition = anchoredPosition;
    }


    public void Awake()
    {
        if (image == null) image = GetComponent<Image>();
        rectTransform = GetComponent<RectTransform>();
    }

    private void Start() => SetMode(messageType);
}

[System.Serializable]
public class MessageUIProperties
{
    public Color32 color;
    public Vector2 offset;
}