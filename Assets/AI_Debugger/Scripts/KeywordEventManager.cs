using System;
using System.Collections;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Text;
using UnityEngine;

public class KeywordEventManager : MonoBehaviour
{
    [SerializeField] public KeywordEvent[] keywordEvents;

    /// <summary>
    /// Anytime submitchat is invoked, we first search for keywords
    /// Example:
    /// invoke function ScanAndPopulateClasses()
    /// view variables of ChatBehavior
    /// view variables of ColorChangeScript
    /// </summary>
    /// <param name="_tex"></param>
    /// <returns></returns>

    public bool ParseKeyword()
    {
        string input = DopeCoderController.Instance.uiController.inputField.text;
        foreach (KeywordEvent k in keywordEvents)
        {
            if (input.Contains(k.Keyword, StringComparison.OrdinalIgnoreCase))
            {
                Debug.Log($"Keyword {k.Keyword}");
                k.keywordEvent.Invoke();
                return true;
            }
        }
        return false;
    }


    public void GetHelpText()
    {
        StringBuilder helpTextStrBuilder = new StringBuilder();
        helpTextStrBuilder.AppendLine("## Available Commands\n");

        foreach (KeywordEvent k in keywordEvents)
        {
            helpTextStrBuilder.AppendLine($"- **{k.Keyword}**: {k.Description}");
        }

        string helpText = helpTextStrBuilder.ToString();
        DopeCoderController.Instance.UpdateChat(helpText, MessageColorMode.MessageType.Receiver);
    }


    public void InvokeFunction()
    {
        string _text = DopeCoderController.Instance.uiController.inputField.text;
        string _func = ParseFunctionName(_text);
        if (!string.IsNullOrEmpty(_func))
        {
            Debug.Log($"Function name {_func}");
            DopeCoderController.Instance.componentController.SearchFunctions(_func);
        }
    }

    public void ViewClassVariables()
    {
        string _text = DopeCoderController.Instance.uiController.inputField.text;
        string className = ParseClassName(_text, "view variables of ");
        if (!string.IsNullOrEmpty(className))
        {
            Debug.Log($"Viewing variables of class {className}");
            //componentController.PrintAllVariableValues(className);
            // update references
            DopeCoderController.Instance.componentController.ScanAndPopulateClasses();
            string localQueryResponse = DopeCoderController.Instance.componentController.GetAllVariableValuesAsString(className);
            DopeCoderController.Instance.UpdateChat(localQueryResponse, MessageColorMode.MessageType.Receiver);
        }
    }

    public void ViewVariable()
    {
        string _text = DopeCoderController.Instance.uiController.inputField.text;
        string variableName = ParseVariableName(_text, "view variable ");
        if (!string.IsNullOrEmpty(variableName))
        {
            // update references
            DopeCoderController.Instance.componentController.ScanAndPopulateClasses();
            Debug.Log($"Viewing variable {variableName}");
            DopeCoderController.Instance.componentController.PrintVariableValueInAllClasses(variableName);
        }
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

    public string ParseClassName(string input, string patternStart)
    {
        string pattern = patternStart + @"([A-Za-z_][A-Za-z0-9_]*)";
        Match match = Regex.Match(input, pattern);
        if (match.Success)
        {
            return match.Groups[1].Value;
        }
        return null;
    }

    public string ParseVariableName(string input, string patternStart)
    {
        return ParseClassName(input, patternStart); // Reusing the same logic as class name parsing
    }


}

[System.Serializable]
public class KeywordEvent
{
    public string Keyword;
    public string Description;
    public UnityEngine.Events.UnityEvent keywordEvent;
}

[Serializable]
public class FileReference
{
    public string assetPath;
    public bool markedForRemoval;
}