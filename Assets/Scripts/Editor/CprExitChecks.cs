using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

[InitializeOnLoad]
public static class CprExitChecks
{
    const string State="RCP.ExitCheck";
    static double deadline;
    static CprExitChecks()
    {
        EditorApplication.playModeStateChanged+=state=>
        {
            if(state==PlayModeStateChange.EnteredPlayMode && SessionState.GetInt(State,0)==1)
                EditorApplication.delayCall+=InvokeExit;
            if(state==PlayModeStateChange.EnteredEditMode && SessionState.GetInt(State,0)==2)
            {
                SessionState.EraseInt(State); EditorApplication.update-=Watch;
                Debug.Log("PASS: real Salir button stops Unity Play Mode through QuitGame");
                if(Application.isBatchMode) EditorApplication.delayCall+=()=>EditorApplication.Exit(0);
            }
        };
    }
    public static void Run()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/1_MainMenu.unity");
        SessionState.SetInt(State,1); EditorApplication.isPlaying=true;
    }
    static void InvokeExit()
    {
        var button=UnityEngine.Object.FindObjectsOfType<Button>(true).First(b=>b.name=="B_Salir");
        SessionState.SetInt(State,2); deadline=EditorApplication.timeSinceStartup+5;
        EditorApplication.update+=Watch; button.onClick.Invoke();
    }
    static void Watch()
    {
        if(EditorApplication.timeSinceStartup<deadline) return;
        EditorApplication.update-=Watch; SessionState.EraseInt(State);
        Debug.LogError("Salir did not stop Play Mode in five seconds");
        if(Application.isBatchMode) EditorApplication.Exit(1);
    }
}
