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
using OpenAI.Threads;
using OpenAI;
using System.Linq;
using System.Threading.Tasks;
using System.Threading;

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
        //Debug.Log("Available Assemblies:");
        //foreach (var assembly in assemblies) {
        //    Debug.Log(assembly.FullName);
        //}
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
        //Debug.Log(type.FullName + " is used within " + ((MonoBehaviour)script).gameObject.name);
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
        string normalizedFuncName = func.Replace(" ", "").ToLower();
        string bestMatch = null;
        double bestAccuracy = 0.0;
        string bestClass = null;

        foreach (var classEntry in classCollection) {
            foreach (var methodEntry in classEntry.Value.Methods) {
                string normalizedMethodName = methodEntry.Key.Replace(" ", "").ToLower();
                double accuracy = 1.0 - (double)LevenshteinDistance(normalizedFuncName, normalizedMethodName) / Math.Max(normalizedFuncName.Length, normalizedMethodName.Length);

                if (accuracy > bestAccuracy) {
                    bestAccuracy = accuracy;
                    bestMatch = methodEntry.Key;
                    bestClass = classEntry.Key;
                }
            }
        }

        if (bestMatch != null && bestAccuracy >= 0.95) {
            Debug.Log($"Found function '{bestMatch}' in class '{bestClass}' with accuracy {bestAccuracy:P}");
            SetCustomObject(FindObjectOfType(Type.GetType(bestClass)));
            InvokePublicMethod(bestClass, bestMatch);
        } else if (bestMatch != null) {
            Debug.Log($"Closest match for function '{func}' is '{bestMatch}' in class '{bestClass}' with accuracy {bestAccuracy:P}");
        } else {
            Debug.Log($"Function '{func}' not found.");
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

    public string GetAllVariableValuesAsString(string className) {
        string normalizedClassName = className.Replace(" ", "").ToLower();
        string bestMatch = null;
        double bestAccuracy = 0.0;

        foreach (var entry in classCollection) {
            string normalizedEntryName = entry.Key.Replace(" ", "").ToLower();
            double accuracy = 1.0 - (double)LevenshteinDistance(normalizedClassName, normalizedEntryName) / Math.Max(normalizedClassName.Length, normalizedEntryName.Length);

            if (accuracy > bestAccuracy) {
                bestAccuracy = accuracy;
                bestMatch = entry.Key;
            }
        }

        if (bestMatch != null && bestAccuracy >= 0.95) {
            var classInfo = classCollection[bestMatch];
            StringBuilder variableValues = new StringBuilder();
            variableValues.AppendLine($"All variables in class {bestMatch}:");

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
            return variableValues.ToString();
        } else if (bestMatch != null) {
            var classInfo = classCollection[bestMatch];
            StringBuilder variableValues = new StringBuilder();
            variableValues.AppendLine($"All variables in class {bestMatch}:");
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
            return variableValues.ToString();
        } else {
            return $"Class '{className}' not found.";
        }
    }


    public string GetAllClassInfoAsJson() {
        JObject jsonResult = new JObject();

        foreach (var classEntry in classCollection) {
            JObject classObject = new JObject();

            // Add variables
            JObject variablesObject = new JObject();
            foreach (var variableEntry in classEntry.Value.Variables) {
                object variableValue = classEntry.Value.VariableValues.TryGetValue(variableEntry.Key, out object val) ? val : "Unavailable";

                if (variableValue is IDictionary dictionary) {
                    JObject dictionaryObject = new JObject();
                    foreach (DictionaryEntry entry in dictionary) {
                        dictionaryObject.Add(entry.Key.ToString(), entry.Value?.ToString() ?? "null");
                    }
                    variablesObject.Add(variableEntry.Key, dictionaryObject);
                } else {
                    variablesObject.Add(variableEntry.Key, variableValue?.ToString() ?? "null");
                }
            }
            classObject.Add("Variables", variablesObject);

            // Add methods
            JArray methodsArray = new JArray();
            foreach (var methodEntry in classEntry.Value.Methods) {
                JObject methodObject = new JObject();
                methodObject.Add("Name", methodEntry.Key);

                JArray parametersArray = new JArray();
                foreach (var parameter in methodEntry.Value.GetParameters()) {
                    JObject parameterObject = new JObject();
                    parameterObject.Add("Name", parameter.Name);
                    parameterObject.Add("Type", parameter.ParameterType.Name);
                    parametersArray.Add(parameterObject);
                }
                methodObject.Add("Parameters", parametersArray);

                methodsArray.Add(methodObject);
            }
            classObject.Add("Methods", methodsArray);

            jsonResult.Add(classEntry.Key, classObject);
        }

        return jsonResult.ToString(Formatting.Indented);
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

    private int LevenshteinDistance(string source, string target) {
        if (string.IsNullOrEmpty(source)) return string.IsNullOrEmpty(target) ? 0 : target.Length;
        if (string.IsNullOrEmpty(target)) return source.Length;

        int[,] matrix = new int[source.Length + 1, target.Length + 1];

        for (int i = 0; i <= source.Length; i++) matrix[i, 0] = i;
        for (int j = 0; j <= target.Length; j++) matrix[0, j] = j;

        for (int i = 1; i <= source.Length; i++) {
            for (int j = 1; j <= target.Length; j++) {
                int cost = target[j - 1] == source[i - 1] ? 0 : 1;
                matrix[i, j] = Math.Min(Math.Min(matrix[i - 1, j] + 1, matrix[i, j - 1] + 1), matrix[i - 1, j - 1] + cost);
            }
        }

        return matrix[source.Length, target.Length];
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
