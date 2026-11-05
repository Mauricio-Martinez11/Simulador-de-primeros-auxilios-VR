using System;
using System.IO;
using System.Linq;
using FirstAidVR.Rcp;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class CprStationTools
{
    public const string ScenePath="Assets/Scenes/2_Tutorial_RCP.unity";

    [MenuItem("Primeros auxilios/RCP/Preparar estación fija en la escena")]
    public static void BuildScene()
    {
        if(!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        var scene=EditorSceneManager.OpenScene(ScenePath);
        var tutorial=UnityEngine.Object.FindObjectOfType<CprTutorial>();
        var settings=Resources.Load<CprTrainingSettings>("RCP/TrainingSettings");
        settings.stationaryPractice=true;
        settings.practiceInstructions=new AudioClip[8];
        for(int i=0;i<8;i++) settings.practiceInstructions[i]=AssetDatabase.LoadAssetAtPath<AudioClip>($"Assets/Audio/RCP/Step{i:00}.wav");
        CprLessonVideoTools.Assign(settings);
        var patient=scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<Transform>(true)).First(t=>t.name=="Hombre_Paciente");
        tutorial.patient=patient;
        var station=tutorial.GetComponent<CprStationController>(); if(station==null) station=tutorial.gameObject.AddComponent<CprStationController>();
        station.BuildLayout(settings,patient,tutorial.chestAnchor,tutorial.shoulderAnchor);
        station.View.Rebuild(settings);
        foreach(var canvas in station.View.Panels) CprCanvasInteraction.Attach(canvas);
        foreach(var component in patient.GetComponentsInChildren<Component>(true))
            if(component!=null && PrefabUtility.IsPartOfPrefabInstance(component)) PrefabUtility.RecordPrefabInstancePropertyModifications(component);
        var camera=scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<Camera>(true)).FirstOrDefault();
        if(camera!=null)
        {
            camera.transform.position=station.StationRoot.TransformPoint(new Vector3(0,1.0f,-2.7f));
            camera.transform.LookAt(station.StationRoot.TransformPoint(new Vector3(.15f,.38f,.12f)));
            camera.nearClipPlane=.03f;
        }
        EditorUtility.SetDirty(settings); AssetDatabase.SaveAssets();
        EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
        Debug.Log("RCP: estación fija guardada con paciente, cuatro pantallas, demostraciones ilustradas y control de altura.");
    }

    public static void BuildAndCheck()
    {
        try
        {
            BuildScene(); CprProjectChecks.Run(); CprStationChecks.Run();
            CaptureScene();
            File.WriteAllText("Station-checks.txt",$"PASS: {CprStationChecks.Passed} fixed station checks; scene prepared; no APK generated.\n");
        }
        catch(Exception e) { Debug.LogException(e); File.WriteAllText("Station-checks.txt","FAIL: "+e); if(Application.isBatchMode) EditorApplication.Exit(1); }
    }

    public static void CaptureScene()
    {
        var station=UnityEngine.Object.FindObjectOfType<CprStationController>();
        if(station==null) throw new InvalidOperationException("No hay estación para capturar.");
        var camera=Camera.main;
        var rt=new RenderTexture(1920,1080,24); var texture=new Texture2D(1920,1080,TextureFormat.RGB24,false);
        var old=camera.targetTexture; var oldActive=RenderTexture.active;
        try
        {
            foreach(var text in station.View.GetComponentsInChildren<TMPro.TextMeshProUGUI>(true)) text.ForceMeshUpdate(true,true);
            Canvas.ForceUpdateCanvases();
            camera.targetTexture=rt; camera.Render(); RenderTexture.active=rt;
            texture.ReadPixels(new Rect(0,0,1920,1080),0,0); texture.Apply();
            File.WriteAllBytes("Station-preview.png",texture.EncodeToPNG());
        }
        finally { camera.targetTexture=old; RenderTexture.active=oldActive; UnityEngine.Object.DestroyImmediate(texture); UnityEngine.Object.DestroyImmediate(rt); }
    }
}
