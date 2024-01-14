using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using UnityEngine;
using Debug = UnityEngine.Debug;

public class QuickAnalyze : MonoBehaviour
{
    public void Update() {
        if (Input.GetKeyDown(KeyCode.Space)) FindScripts();
    }

    public void FindScripts() {
        MonoBehaviour[] scripts = UnityEngine.Object.FindObjectsOfType<MonoBehaviour>();
        foreach (MonoBehaviour script in scripts) {
            Type scriptType = script.GetType();
            var scope = scriptType.Namespace;
            if (scope == null || !scope.StartsWith("Unity"))
                Debug.Log(script.GetType().FullName + " is used within " + script.gameObject.name);
        }
    }
}
