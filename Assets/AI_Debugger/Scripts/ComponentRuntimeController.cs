using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;

public class ComponentRuntimeController : MonoBehaviour
{
    [SerializeField] public UnityEngine.Object customObject;
    public Dictionary<string, FieldInfo> publicVariables;
    public Dictionary<string, MethodInfo> publicMethods;
    //public List<FieldInfo> publicVariables;
    public List<FieldInfo> privateVariables;

    private void Awake() {
        publicVariables = new Dictionary<string, FieldInfo>();
        publicMethods = new Dictionary<string, MethodInfo>();
        privateVariables = new List<FieldInfo>();
        //if (customObject == null) customObject = GetComponent<UnityEngine.Object>();
    }

    public void SetCustomObject(UnityEngine.Object obj) {
        customObject = obj;
        PopulateClassProperties();
    }

    void PopulateClassProperties()
    {
        if (customObject != null) {
            foreach (FieldInfo ft in customObject.GetType().GetFields(BindingFlags.Public | BindingFlags.NonPublic |
                         BindingFlags.Instance)) {
                Debug.Log($"Public variable name {ft.Name} and type {ft.FieldType}");
                publicVariables.Add(ft.Name, ft);
            }
            foreach (FieldInfo ft in customObject.GetType().GetFields(BindingFlags.NonPublic |
                         BindingFlags.Instance)) {
                Debug.Log($"Private variable name {ft.Name} and type {ft.FieldType}");
                privateVariables.Add(ft);
            }
            foreach (MethodInfo mI in customObject.GetType().GetMethods(BindingFlags.Public | BindingFlags.Instance))
            {
                Debug.Log($"public function name {mI.Name} and type {mI.ReturnType}");
                // For now, only add if not duplicate instance
                if (!publicMethods.ContainsKey(mI.Name))
                    publicMethods.Add(mI.Name, mI);
            }
            //return from p in customObject.GetType().GetFields()
            //       where p.FieldType == typeof(T)
            //       select new KeyValuePair<string, T>(p.Name, (T)p.GetValue(obj));
        }
    }


    public string GetValueOfPublicVariable(string varName) {
        return publicVariables[varName].GetValue(customObject).ToString();
    }

    public void SetValueOfPublicVariable(string varName, object newVal) {
        publicVariables[varName].SetValue(customObject, newVal);
    }

    public Type GetTypeOfPublicVariable(string varName) {
        return publicVariables[varName].GetValue(customObject).GetType();
    }
    /** METHODS **/
    public MethodInfo GetPublicMethod(string methodName) {
        return publicMethods[methodName];
    }

    public ParameterInfo[] GetParameterTypesOfPublicMethod(string methodName) {
        return publicMethods[methodName].GetParameters();
    }

    public void InvokePublicMethod(string methodName) {
        // for now, only invoke if method contains no parameters
        if (GetParameterTypesOfPublicMethod(methodName).Length == 0) {
            publicMethods[methodName].Invoke(customObject, new object[] { });
        }
    }

    public Type GetReturnTypeOfPublicMethod(string methodName) {
        return publicMethods[methodName].ReturnType;
    }
}
