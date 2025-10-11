using FusionVRPlus.Networking;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Reflection;
using UnityEditor;
using UnityEditor.Experimental.SceneManagement;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Networking;

[CustomEditor(typeof(FusionVRPlusManager))]
public class FusionVRPlusManagerGUI : Editor
{
    public override void OnInspectorGUI()
    {
        FusionVRPlusManager manager = (FusionVRPlusManager)target;

        if (PrefabStageUtility.GetCurrentPrefabStage() == null)
        {
            manager.CheckDefaultValues();
        }

        base.OnInspectorGUI();
    }
}
