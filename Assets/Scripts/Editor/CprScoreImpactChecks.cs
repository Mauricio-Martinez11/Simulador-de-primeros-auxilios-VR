using System;
using System.IO;
using System.Linq;
using System.Reflection;
using FirstAidVR.Rcp;
using TMPro;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

public static class CprScoreImpactChecks
{
    static int passed;
    static void Check(bool ok,string message) { if(!ok) throw new InvalidOperationException(message); passed++; }
    static void Set(CprPracticeSession s,string name,int value) => typeof(CprPracticeSession).GetField("<"+name+">k__BackingField",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(s,value);
    public static void Run()
    {
        CprV2Checks.Run();
        var config=Resources.Load<CprTrainingSettings>("RCP/TrainingSettings");
        var session=CprStationChecks.Prepared(config);
        for(float t=-.4f;t<30*60/110f+.1f;t+=.01f)
        {
            float depth=t<0 || t>=30*60/110f?0:.055f*(1-Mathf.Cos(t*Mathf.PI*110/30))*.5f;
            var left=new CprHandSample(new Pose(Vector3.up*(.035f-depth),Quaternion.identity));
            var right=new CprHandSample(new Pose(Vector3.down*depth,Quaternion.identity));
            session.Tick(.01f,left,right,Pose.identity,Pose.identity,Pose.identity,false,true,true);
        }
        Check(session.Count==30 && session.Score==100,"base completa correcta de 100 puntos");
        Set(session,"LocationErrors",5); Set(session,"HandSeparationErrors",5);
        Set(session,"DepthHits",6); Set(session,"Shallow",24);
        var rows=CprScoreImpact.From(session);
        Check(session.Score==60,"ejemplo 60 puntos no cambia fórmula");
        Check(Math.Abs(rows[0].points-10)<.001f && Math.Abs(rows[1].points-10)<.001f && Math.Abs(rows[2].points-20)<.001f,"incidencias explican exactamente 40 puntos");
        Check(Math.Abs(rows.Sum(r=>r.points)-(100-session.Score.Value))<.001f,"suma de efectos concuerda con nota");
        var report=CprAttemptReport.From(session,"PRUEBA SIMULADA","poses sintéticas");
        var roundtrip=JsonUtility.FromJson<CprAttemptReport>(JsonUtility.ToJson(report));
        Check(roundtrip.scoreImpacts.Length==8 && roundtrip.impactSummary==CprScoreImpact.Summary(session),"JSON incluye el mismo desglose de pantalla");
        Check(report.ToReadableText().Contains(CprScoreImpact.Summary(session)),"TXT conserva el mismo resumen");
        Set(session,"LocationErrors",15); Set(session,"HandSeparationErrors",5); Set(session,"PauseErrors",9);
        rows=CprScoreImpact.From(session);
        Check(Math.Abs(rows[0].points-15)<.001f && Math.Abs(rows[1].points-5)<.001f,"límite compartido de colocación no duplica descuento");
        Check(rows[7].points==10,"pausas respetan tope de diez puntos");
        var view=new GameObject("Puntos test").AddComponent<CprStationView>(); view.Build(config);
        view.ToggleResults(); view.Show(session,0,0,false,true); Canvas.ForceUpdateCanvases();
        var detail=view.Panels[1].transform.Find("Criterios del resultado").GetComponent<TMP_Text>(); detail.ForceMeshUpdate();
        Check(detail.text==CprScoreImpact.Summary(session),"pantalla usa exactamente los impactos exportados");
        Check(!detail.isTextOverflowing,"detalle cabe en panel");
        UnityEngine.Object.DestroyImmediate(view.gameObject);
        EditorSceneManager.OpenScene("Assets/Scenes/1_MainMenu.unity");
        var buttons=UnityEngine.Object.FindObjectsOfType<Button>(true);
        Check(buttons.First(b=>b.name=="B_Escenario_Real").onClick.GetPersistentMethodName(0)=="LoadMixedRealityTutorial","acceso real conectado a realidad mixta");
        Check(!buttons.First(b=>b.name=="B_Otros_Escenarios").interactable,"escenarios no implementados no son botón activo vacío");
        Check(buttons.First(b=>b.name=="B_Salir").onClick.GetPersistentMethodName(0)=="QuitGame","Salir conectado al cierre de aplicación");
        Directory.CreateDirectory("Logs/V2Validation");
        File.WriteAllText("Logs/V2Validation/score-impact.txt",$"PASS: {passed} score impact and menu checks. Quest verification pending.");
        Debug.Log($"PASS: {passed} score impact and menu checks");
    }
}
