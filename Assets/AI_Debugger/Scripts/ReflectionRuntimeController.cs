using UnityEngine;
using System.Reflection;
using System.Collections.Generic;
using System;
using System.Text.RegularExpressions;
using System.Text;
using System.Collections;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.IO;

public class ReflectionRuntimeController : MonoBehaviour
{
    public bool ScanCollidersOnly; 

    public float detectionRadius = 5f; // Radius for proximity detection
    public LayerMask detectionLayer; // Layer mask to filter which objects to detect
    [SerializeField] public Dictionary<string, ClassInfo> classCollection = new Dictionary<string, ClassInfo>();
    [SerializeField] public UnityEngine.Object customObject;


    public void ScanAndPopulateClasses()
    {
        Debug.Log("Scanning...");
        // Clearing existing data
        classCollection.Clear();
        
        if (ScanCollidersOnly) {    // Find all colliders within the specified radius
            Collider[] colliders = Physics.OverlapSphere(transform.position, detectionRadius, detectionLayer);
            foreach (Collider collider in colliders) {
                GameObject obj = collider.gameObject;
                PopulateClassInfo(obj);
            }
            Debug.Log($"Total {colliders.Length}");
        } else {
            PopulateClassInfo();
            Debug.Log($"Total {classCollection.Count}");
        }

    }

    void PopulateClassInfo() {

        MonoBehaviour[] scripts = UnityEngine.Object.FindObjectsOfType<MonoBehaviour>();
        foreach (MonoBehaviour script in scripts) {
            Type type = script.GetType();
            var scope = type.Namespace;
            if (scope == null || (!scope.StartsWith("Unity") && !scope.StartsWith("UnityEngine.UI") && !scope.StartsWith("TMPro"))) {
                Debug.Log(script.GetType().FullName + " is used within " + script.gameObject.name);
                var classInfo = new ClassInfo();

                // Populate methods
                foreach (var method in type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly)) {
                    classInfo.Methods[method.Name] = method;
                }

                //foreach (var field in type.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly))
                foreach (var field in type.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)) {
                    classInfo.Variables[field.Name] = field;
                    // Retrieve and store the current value of the variable
                    object value = field.GetValue(script);
                    classInfo.VariableValues[field.Name] = value;
                }

                // Add to class collection
                if (!classCollection.ContainsKey(type.Name)) {
                    classCollection[type.Name] = classInfo;
                }

            }
        }
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

            //foreach (var field in type.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly))
            foreach (var field in type.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
            {
                classInfo.Variables[field.Name] = field;
                // Retrieve and store the current value of the variable
                object value = field.GetValue(monoBehaviour);
                classInfo.VariableValues[field.Name] = value;
            }

            // Add to class collection
            if (!classCollection.ContainsKey(type.Name))
            {
                classCollection[type.Name] = classInfo;
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

    public void PrintAllVariableValues(string className)
    {
        if (classCollection.TryGetValue(className, out ClassInfo classInfo))
        {
            Debug.Log($"All variables in class {className}:");
            foreach (var variable in classInfo.Variables)
            {
                object value = classInfo.VariableValues.TryGetValue(variable.Key, out object val) ? val : "Unavailable";
                Debug.Log($"- {variable.Key}: {value}");
            }
        }
        else
        {
            Debug.Log($"Class {className} not found.");
        }
    }


    public string GetAllVariableValuesAsString(string className)
    {
        if (classCollection.TryGetValue(className, out ClassInfo classInfo))
        {
            StringBuilder variableValues = new StringBuilder();
            variableValues.AppendLine($"All variables in class {className}:");

            foreach (var variable in classInfo.Variables)
            {
                object variableValue = classInfo.VariableValues.TryGetValue(variable.Key, out object val) ? val : "Unavailable";

                // Check if the variable is a dictionary
                if (variableValue is IDictionary dictionary)
                {
                    variableValues.AppendLine($"- {variable.Key} (Dictionary):");
                    foreach (DictionaryEntry entry in dictionary)
                    {
                        variableValues.AppendLine($"    - Key: {entry.Key}, Value: {entry.Value}");
                    }
                }
                else
                {
                    variableValues.AppendLine($"- {variable.Key}: {variableValue}");
                }
            }
            Debug.Log(variableValues.ToString());
            return variableValues.ToString();
        }
        else
        {
            return $"Class {className} not found.";
        }
    }

    public string GetAllVariableValuesAsJson() {
        JObject jsonResult = new JObject();

        foreach (var classEntry in classCollection) {
            JObject classObject = new JObject();
            string classString = GetAllVariableValuesAsString(classEntry.Key);

            using (StringReader reader = new StringReader(classString)) {
                string line;
                while ((line = reader.ReadLine()) != null) {
                    if (line.StartsWith("All variables in class")) {
                        continue; // Skip the header line
                    }

                    if (line.Contains("(Dictionary):")) {
                        string dictName = line.Split(new[] { " (Dictionary):" }, StringSplitOptions.None)[0].TrimStart('-').Trim();
                        JObject dictionaryObject = new JObject();

                        while ((line = reader.ReadLine()) != null && line.StartsWith("    - ")) {
                            var keyValue = line.Trim().Split(new[] { ", Value: " }, StringSplitOptions.None);
                            string key = keyValue[0].Replace("Key: ", "").Trim();
                            string value = keyValue.Length > 1 ? keyValue[1] : "Unavailable";
                            dictionaryObject.Add(key, value);
                        }

                        classObject.Add(dictName, dictionaryObject);
                    } else {
                        var keyValue = line.TrimStart('-').Split(new[] { ": " }, StringSplitOptions.None);
                        string key = keyValue[0].Trim();
                        string value = keyValue.Length > 1 ? keyValue[1] : "Unavailable";
                        classObject.Add(key, value);
                    }
                }
            }

            jsonResult.Add(classEntry.Key, classObject);
        }

        return jsonResult.ToString(Formatting.Indented);
    }


    public void PrintVariableValueInAllClasses(string variableName)
    {
        bool variableFound = false;

        foreach (var classEntry in classCollection)
        {
            string className = classEntry.Key;
            ClassInfo classInfo = classEntry.Value;

            if (classInfo.Variables.TryGetValue(variableName, out FieldInfo fieldInfo))
            {
                object value = classInfo.VariableValues.TryGetValue(variableName, out object val) ? val : "Unavailable";
                Debug.Log($"Variable {variableName} found in class {className}: {value}");
                variableFound = true;
            }
        }

        if (!variableFound)
        {
            Debug.Log($"Variable {variableName} not found in any class.");
        }
    }


    public void SetCustomObject(UnityEngine.Object obj) => customObject = obj;

    public void SetColliderTrigger(bool status) => ScanCollidersOnly = status;

}

[System.Serializable]
public class ClassInfo
{
    public Dictionary<string, MethodInfo> Methods { get; set; }
    public Dictionary<string, FieldInfo> Variables { get; set; }
    public Dictionary<string, object> VariableValues { get; set; } // Store runtime values

    public ClassInfo()
    {
        Methods = new Dictionary<string, MethodInfo>();
        Variables = new Dictionary<string, FieldInfo>();
        VariableValues = new Dictionary<string, object>();
    }
}