using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;

public class ExpandUI : MonoBehaviour
{
    public RectTransform rectTransform;    
    public TextMeshProUGUI uiButton;
    private float originalLeft;

    protected bool isOriginalWidth;
    

    public void Start()
    {
        isOriginalWidth = false;        
        originalLeft = rectTransform.offsetMin.x; // This should be 0 if the panel is stretched full width to start with
    }

    public void ToggleWidth()
    {
        isOriginalWidth = !isOriginalWidth;
        if (isOriginalWidth)
        {
            // Set to half width
            rectTransform.offsetMin = new Vector2(originalLeft + rectTransform.rect.width / 2, rectTransform.offsetMin.y);
            uiButton.text = "<";
        }
        else
        {
            // Set back to full width
            rectTransform.offsetMin = new Vector2(originalLeft, rectTransform.offsetMin.y);
            uiButton.text = ">";
        }

        Debug.Log($"New Left Offset: {rectTransform.offsetMin.x}");
    }


}
