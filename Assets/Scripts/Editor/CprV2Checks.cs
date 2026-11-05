using System;
using System.IO;
using System.Linq;
using FirstAidVR.Rcp;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

public static class CprV2Checks
{
    private static int passed;
    private static void Check(bool condition,string detail)
    { if(!condition) throw new InvalidOperationException("V2: "+detail); passed++; }
    private static void Tick(CprPracticeSession session,bool look=true)
    {
        var chest=Pose.identity; var left=new CprHandSample(new Pose(Vector3.up*.035f,Quaternion.identity));
        var right=new CprHandSample(chest);
        session.Tick(.02f,left,right,new Pose(new Vector3(-.2f,0,.2f),Quaternion.identity),chest,left.pose,look,true,true,
            new CprPracticeObservation {safetyLeft=true,safetyRight=true,helpRequested=true});
    }
    [MenuItem("Primeros auxilios/RCP/Verificar bienvenida V2 y tiempos")]
    public static void Run()
    {
        passed=0;
        CprStationChecks.Run(); CprHandInputChecks.Run(); CprReliabilityChecks.Run(); CprBreathingChecks.Run();
        var config=Resources.Load<CprTrainingSettings>("RCP/TrainingSettings");
        var script=JsonUtility.FromJson<CprDialogueScript>(Resources.Load<TextAsset>("RCP/Narration/Script").text);
        foreach(var entry in script.entries)
        {
            var clip=CprNarration.Clip(entry.key);
            Check(clip!=null && clip.length>1,"audio local completo: "+entry.key);
            Check(!entry.text.Contains("DEA") && !entry.text.ToLowerInvariant().Contains("desfibril"),"contenido aprobado: "+entry.key);
        }
        Check(!CprNarration.Get("Step05").text.Contains("codos"),"audio de colocación corregido");
        Check(!CprNarration.Get("Step06").text.Contains("treinta") && !CprNarration.Get("Step06").text.Contains("pecho real"),"audio de compresiones corregido");
        foreach(var step in new[]{CprPracticeStep.Welcome,CprPracticeStep.Safety,CprPracticeStep.Breathing,CprPracticeStep.Placement,CprPracticeStep.Compressions})
        {
            var s=CprStationChecks.AtStep(config,step); s.SetPresentationGate(true,true);
            for(int i=0;i<2000;i++) Tick(s);
            Check(s.Step==step && !s.StepPassed && s.Count==0,"no registra ni avanza durante explicación: "+step);
            if(step==CprPracticeStep.Breathing) Check(!s.BreathingStarted && s.BreathingElapsedSeconds==0,"reloj de respiración espera audio");
        }
        foreach(var step in new[]{CprPracticeStep.Safety,CprPracticeStep.Help,CprPracticeStep.Placement})
        {
            var s=CprStationChecks.AtStep(config,step); s.SetPresentationGate(false,true);
            for(int i=0;i<1000 && !s.StepPassed;i++) Tick(s);
            Check(s.StepPassed && s.Step==step,"acción reconocida espera confirmación: "+step);
            for(int i=0;i<1000;i++) Tick(s);
            Check(s.Step==step,"no corta confirmación por tiempo transcurrido: "+step);
            s.SetPresentationGate(false,false); Tick(s);
            Check(s.Step==step+1,"avanza una sola vez después de confirmación: "+step);
        }
        EditorSceneManager.OpenScene("Assets/Scenes/1_MainMenu.unity");
        var exit=UnityEngine.Object.FindObjectsOfType<Button>().First(b=>b.name=="B_Salir");
        Check(exit.onClick.GetPersistentEventCount()==1 && exit.onClick.GetPersistentMethodName(0)=="QuitGame","Salir conectado al cierre de aplicación");
        EditorSceneManager.OpenScene("Assets/Scenes/2_Tutorial_RCP.unity");
        var tutorial=UnityEngine.Object.FindObjectOfType<CprTutorial>();
        var station=tutorial.GetComponent<CprStationController>(); station.BuildLayout(config,tutorial.patient);
        Check(station.View.Button(CprStationAction.Realign)!=null,"opción para realinear apoyo");
        var empty=new CprPracticeSession(config); empty.Finish(); station.View.Show(empty,0,0,false,true);
        var summary=station.View.GetComponentsInChildren<TMP_Text>(true).First(t=>t.name=="Criterios del resultado");
        Check(summary.text.Contains("No evaluable: faltan intervalos"),"ritmo sin datos no muestra cero puntos");
        var camera=UnityEngine.Object.FindObjectOfType<Camera>();
        var go=new GameObject("Comprobación de bienvenida"); var panel=go.AddComponent<CprIntroPanel>(); panel.Build(camera); panel.Present("Welcome","Entorno virtual","Realidad mixta");
        Check(go.GetComponentsInChildren<Button>().Length==2,"dos modalidades seleccionables");
        Check(go.GetComponentsInChildren<Oculus.Interaction.RayInteractable>().Length>0,"selección por láser conectada");
        var breathing=go.AddComponent<CprBreathingPanel>(); breathing.Bind(camera);
        Check(breathing.PanelTransform.GetComponentInChildren<CprCaseTrace>(true)==null,"sin cardiograma");
        var breath=CprStationChecks.AtStep(config,CprPracticeStep.Breathing); breath.SetPresentationGate(true,true); breathing.Show(breath,true);
        Check(breathing.BreathingText.Contains("ESCUCHA"),"respiración explica antes de observar");
        Directory.CreateDirectory("Logs/V2Validation");
        File.WriteAllText("Logs/V2Validation/checks.txt",$"PASS: {passed} V2 checks. Station, hand input, reliability and breathing regressions passed. No APK. Quest passthrough/hands retest pending.\n");
        Debug.Log($"PASS: {passed} V2 checks");
        UnityEngine.Object.DestroyImmediate(go);
    }
}
