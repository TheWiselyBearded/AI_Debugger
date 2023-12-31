using UnityEngine;
using System.Reflection;
using System.Collections.Generic;
using System;
using System.Text.RegularExpressions;

public class ReflectionRuntimeController : MonoBehaviour
{
    public float detectionRadius = 5f; // Radius for proximity detection
    public LayerMask detectionLayer; // Layer mask to filter which objects to detect
    [SerializeField] public Dictionary<string, ClassInfo> classCollection = new Dictionary<string, ClassInfo>();
    [SerializeField] public UnityEngine.Object customObject;

    void Update()
    {
        // Check for space bar press
        if (Input.GetKeyDown(KeyCode.Space))
        {
            ScanAndPopulateClasses();
        }
        //if (Input.GetKeyDown(KeyCode.RightArrow))
        //{
        //    SetCustomObject(FindObjectOfType<ColorChangeScript>());
        //    InvokePublicMethod("ColorChangeScript", "ChangeColor");
        //}
    }

    void ScanAndPopulateClasses()
    {
        Debug.Log("Scanning...");
        // Clearing existing data
        classCollection.Clear();

        // Find all colliders within the specified radius
        Collider[] colliders = Physics.OverlapSphere(transform.position, detectionRadius, detectionLayer);
        foreach (Collider collider in colliders)
        {
            GameObject obj = collider.gameObject;
            PopulateClassInfo(obj);
        }
        Debug.Log($"Total {colliders.Length}");
    }

    void PopulateClassInfo(GameObject obj)
    {
        MonoBehaviour[] monoBehaviours = obj.GetComponents<MonoBehaviour>();
        foreach (MonoBehaviour monoBehaviour in monoBehaviours)
        {
            var type = monoBehaviour.GetType();
            var classInfo = new ClassInfo();

            // Populate methods
            foreach (var method in type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly))
            {
                classInfo.Methods[method.Name] = method;
            }

            // Populate variables
            //foreach (var field in type.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly))
            foreach (var field in type.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
            {
                classInfo.Variables[field.Name] = field;
            }

            // Add to class collection
            if (!classCollection.ContainsKey(type.Name))
            {
                classCollection[type.Name] = classInfo;
                Debug.Log($"Class name {type.Name}");
            }
        }
    }

    public void ParseKeyword(string _tex)
    {
        if (_tex.Contains("invoke function "))
        {
            string _func = ParseFunctionName(_tex);
            if (_func != null || _func != "")
            {
                Debug.Log($"Function name {_func}");

            }

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

    public void SearchFunctions(string func)
    {
        Debug.Log($"Searching functions {func}");
        foreach (string _class in classCollection.Keys)
        {
            foreach (string _func in classCollection[_class].Methods.Keys)
            {
                if (_func == func)
                {
                    Debug.Log($"Found function {_func}");
                    SetCustomObject(FindObjectOfType(Type.GetType(_class)));
                    InvokePublicMethod(_class, _func);
                }
            }
        }
    }

    // TODO: Make it where ChatGPT can return responses informing the runtime as to what methods to invoke if
    // user asks what methods to call for invoking a chain of events
    protected ParameterInfo[] GetParameterTypesOfPublicMethod(string className, string methodName)
    {
        return classCollection[className].Methods[methodName].GetParameters();
    }

    public void InvokePublicMethod(string className, string methodName)
    {
        // for now, only invoke if method contains no parameters
        if (GetParameterTypesOfPublicMethod(className, methodName).Length == 0)
        {
            classCollection[className].Methods[methodName].Invoke(customObject, new object[] { });
        }
    }

    public void SetCustomObject(UnityEngine.Object obj) => customObject = obj;

}

// Custom class to hold method and variable info
[System.Serializable]
public class ClassInfo
{
    public Dictionary<string, MethodInfo> Methods { get; set; }
    public Dictionary<string, FieldInfo> Variables { get; set; }   

    public ClassInfo()
    {
        Methods = new Dictionary<string, MethodInfo>();
        Variables = new Dictionary<string, FieldInfo>();
    }
}