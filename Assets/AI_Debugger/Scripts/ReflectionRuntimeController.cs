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

public class ReflectionRuntimeController : MonoBehaviour {
    public bool ScanCollidersOnly;
    public bool fullCodeScan;
    public float detectionRadius = 5f; // Radius for proximity detection
    public LayerMask detectionLayer; // Layer mask to filter which objects to detect
    [SerializeField] public Dictionary<string, ClassInfo> classCollection = new Dictionary<string, ClassInfo>();
    [SerializeField] public UnityEngine.Object customObject;

    // New properties for namespace filters
    public List<string> includedNamespaces = new List<string>();
    public List<string> excludedNamespaces = new List<string>();

    // New properties for assembly filters
    public List<string> includedAssemblies = new List<string>();
    public List<string> excludedAssemblies = new List<string>();

    public void ScanAndPopulateClasses() {
        Debug.Log("Scanning...");
        // Clearing existing data
        classCollection.Clear();

        if (ScanCollidersOnly) {
            // Find all colliders within the specified radius
            Collider[] colliders = Physics.OverlapSphere(transform.position, detectionRadius, detectionLayer);
            foreach (Collider collider in colliders) {
                GameObject obj = collider.gameObject;
                PopulateClassInfo(obj);
            }
            Debug.Log($"Total {colliders.Length}");
        } else if (fullCodeScan || includedAssemblies.Count > 0 || excludedAssemblies.Count > 0) {
            PopulateClassInfo();  // Uses the new implementation with assembly filtering
        } else {
            // Default to scanning only MonoBehaviour objects
            MonoBehaviour[] scripts = UnityEngine.Object.FindObjectsOfType<MonoBehaviour>();
            foreach (MonoBehaviour script in scripts) {
                ProcessType(script.GetType(), script);
            }
            Debug.Log($"Total {scripts.Length}");
        }
    }


    public void ListAllAssemblies() {
        var assemblies = AppDomain.CurrentDomain.GetAssemblies();
        Debug.Log("Available Assemblies:");
        foreach (var assembly in assemblies) {
            Debug.Log(assembly.FullName);
        }
    }

    public void SetIncludedAssemblies(List<string> assemblyNames) {
        includedAssemblies = assemblyNames;
    }

    public void SetExcludedAssemblies(List<string> assemblyNames) {
        excludedAssemblies = assemblyNames;
    }

    void PopulateClassInfo() {
        var assemblies = AppDomain.CurrentDomain.GetAssemblies();
        foreach (var assembly in assemblies) {
            if (includedAssemblies.Count > 0 && !includedAssemblies.Contains(assembly.GetName().Name)) {
                continue;
            }

            if (excludedAssemblies.Count > 0 && excludedAssemblies.Contains(assembly.GetName().Name)) {
                continue;
            }

            var types = assembly.GetTypes();
            foreach (var type in types) {
                if (type.IsSubclassOf(typeof(MonoBehaviour)) && !type.IsGenericTypeDefinition) {
                    var scripts = FindObjectsOfType(type);
                    foreach (var script in scripts) {
                        ProcessType(script.GetType(), script);
                    }
                }
            }
        }

        // Original approach to ensure all MonoBehaviours are considered
        MonoBehaviour[] allScripts = UnityEngine.Object.FindObjectsOfType<MonoBehaviour>();
        foreach (MonoBehaviour script in allScripts) {
            Type type = script.GetType();
            var scope = type.Namespace;

            if (fullCodeScan || ShouldIncludeNamespace(scope)) {
                ProcessType(type, script);
            }
        }
    }



    void ProcessType(Type type, object script) {
        Debug.Log(type.FullName + " is used within " + ((MonoBehaviour)script).gameObject.name);
        var classInfo = new ClassInfo();

        // Populate methods with signatures
        foreach (var method in type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly)) {
            classInfo.Methods[method.Name] = method;
        }

        // Populate fields with types
        foreach (var field in type.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)) {
            classInfo.Variables[field.Name] = field;
            object value = field.GetValue(script);
            classInfo.VariableValues[field.Name] = value;
        }

        // Add to class collection
        if (!classCollection.ContainsKey(type.FullName)) {
            classCollection[type.FullName] = classInfo;
        }
    }
    void PopulateClassInfo(GameObject obj) {
        MonoBehaviour[] monoBehaviours = obj.GetComponents<MonoBehaviour>();
        foreach (MonoBehaviour monoBehaviour in monoBehaviours) {
            var type = monoBehaviour.GetType();
            var classInfo = new ClassInfo();

            // Populate methods with signatures
            foreach (var method in type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly)) {
                classInfo.Methods[method.Name] = method;
            }

            // Populate fields with types
            foreach (var field in type.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)) {
                classInfo.Variables[field.Name] = field;
                object value = field.GetValue(monoBehaviour);
                classInfo.VariableValues[field.Name] = value;
            }

            // Add to class collection
            if (!classCollection.ContainsKey(type.FullName)) {
                classCollection[type.FullName] = classInfo;
            }
        }
    }

    bool ShouldIncludeNamespace(string scope) {
        if (string.IsNullOrEmpty(scope)) return true;
        if (includedNamespaces.Count > 0 && !includedNamespaces.Contains(scope)) return false;
        if (excludedNamespaces.Count > 0 && excludedNamespaces.Contains(scope)) return false;
        return true;
    }

    public void ParseKeyword(string _tex) {
        if (_tex.Contains("invoke function ")) {
            string _func = ParseFunctionName(_tex);
            if (!string.IsNullOrEmpty(_func)) {
                Debug.Log($"Function name {_func}");
            }
        }
    }

    public string ParseFunctionName(string input) {
        string pattern = @"invoke\s+function\s+([A-Za-z_][A-Za-z0-9_]*)\s*\(";
        Match match = Regex.Match(input, pattern);
        Debug.Log("attempt to regex match");
        if (match.Success) {
            return match.Groups[1].Value;
        } else {
            return null;
        }
    }

    public void SearchFunctions(string func) {
        Debug.Log($"Searching functions {func}");
        foreach (string _class in classCollection.Keys) {
            foreach (string _func in classCollection[_class].Methods.Keys) {
                if (_func == func) {
                    Debug.Log($"Found function {_func}");
                    SetCustomObject(FindObjectOfType(Type.GetType(_class)));
                    InvokePublicMethod(_class, _func);
                }
            }
        }
    }

    protected ParameterInfo[] GetParameterTypesOfPublicMethod(string className, string methodName) {
        return classCollection[className].Methods[methodName].GetParameters();
    }

    public void InvokePublicMethod(string className, string methodName) {
        if (GetParameterTypesOfPublicMethod(className, methodName).Length == 0) {
            classCollection[className].Methods[methodName].Invoke(customObject, new object[] { });
        }
    }

    public void PrintAllVariableValues(string className) {
        if (classCollection.TryGetValue(className, out ClassInfo classInfo)) {
            Debug.Log($"All variables in class {className}:");
            foreach (var variable in classInfo.Variables) {
                object value = classInfo.VariableValues.TryGetValue(variable.Key, out object val) ? val : "Unavailable";
                Debug.Log($"- {variable.Key}: {value}");
            }
        } else {
            Debug.Log($"Class {className} not found.");
        }
    }

    public string GetAllVariableValuesAsString(string className) {
        if (classCollection.TryGetValue(className, out ClassInfo classInfo)) {
            StringBuilder variableValues = new StringBuilder();
            variableValues.AppendLine($"All variables in class {className}:");

            foreach (var variable in classInfo.Variables) {
                object variableValue = classInfo.VariableValues.TryGetValue(variable.Key, out object val) ? val : "Unavailable";

                if (variableValue is IDictionary dictionary) {
                    variableValues.AppendLine($"- {variable.Key} (Dictionary):");
                    foreach (DictionaryEntry entry in dictionary) {
                        variableValues.AppendLine($"    - Key: {entry.Key}, Value: {entry.Value}");
                    }
                } else {
                    variableValues.AppendLine($"- {variable.Key}: {variableValue}");
                }
            }
            //Debug.Log(variableValues.ToString());
            return variableValues.ToString();
        } else {
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

                            // Ensure unique key for dictionary items
                            string uniqueKey = key;
                            int count = 1;
                            while (dictionaryObject.ContainsKey(uniqueKey)) {
                                uniqueKey = $"{key}_{count++}";
                            }
                            dictionaryObject.Add(uniqueKey, value);
                        }

                        // Ensure unique key for class properties
                        string uniqueDictName = dictName;
                        int dictCount = 1;
                        while (classObject.ContainsKey(uniqueDictName)) {
                            uniqueDictName = $"{dictName}_{dictCount++}";
                        }
                        classObject.Add(uniqueDictName, dictionaryObject);
                    } else {
                        var keyValue = line.TrimStart('-').Split(new[] { ": " }, StringSplitOptions.None);
                        string key = keyValue[0].Trim();
                        string value = keyValue.Length > 1 ? keyValue[1] : "Unavailable";

                        // Ensure unique key for class properties
                        string uniqueKey = key;
                        int count = 1;
                        while (classObject.ContainsKey(uniqueKey)) {
                            uniqueKey = $"{key}_{count++}";
                        }
                        classObject.Add(uniqueKey, value);
                    }
                }
            }

            jsonResult.Add(classEntry.Key, classObject);
        }

        return jsonResult.ToString(Formatting.Indented);
    }


    public void PrintVariableValueInAllClasses(string variableName) {
        bool variableFound = false;

        foreach (var classEntry in classCollection) {
            string className = classEntry.Key;
            ClassInfo classInfo = classEntry.Value;

            if (classInfo.Variables.TryGetValue(variableName, out FieldInfo fieldInfo)) {
                object value = classInfo.VariableValues.TryGetValue(variableName, out object val) ? val : "Unavailable";
                Debug.Log($"Variable {variableName} found in class {className}: {value}");
                variableFound = true;
            }
        }

        if (!variableFound) {
            Debug.Log($"Variable {variableName} not found in any class.");
        }
    }

    public void SetCustomObject(UnityEngine.Object obj) => customObject = obj;

    public void SetColliderTrigger(bool status) => ScanCollidersOnly = status;
}

[System.Serializable]
public class ClassInfo {
    public Dictionary<string, MethodInfo> Methods { get; set; }
    public Dictionary<string, FieldInfo> Variables { get; set; }
    public Dictionary<string, object> VariableValues { get; set; }

    public ClassInfo() {
        Methods = new Dictionary<string, MethodInfo>();
        Variables = new Dictionary<string, FieldInfo>();
        VariableValues = new Dictionary<string, object>();
    }
}
