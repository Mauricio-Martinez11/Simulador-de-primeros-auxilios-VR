using System;
using System.IO;
using FirstAidVR.Rcp;
using TMPro;
using UnityEngine;

public static class CprTravelMeterChecks
{
    public static void Preview()
    {
        UnityEditor.SceneManagement.EditorSceneManager.NewScene(UnityEditor.SceneManagement.NewSceneSetup.EmptyScene);
        var config=Resources.Load<CprTrainingSettings>("RCP/TrainingSettings");
        var view=new GameObject("Meter preview").AddComponent<CprStationView>(); view.Build(config);
        for(int i=0;i<view.Panels.Length;i++) view.Panels[i].gameObject.SetActive(i==3);
        var panel=view.Panels[3].transform;
        var camera=new GameObject("Preview camera").AddComponent<Camera>();
        camera.transform.SetPositionAndRotation(panel.position-panel.forward, panel.rotation);
        camera.orthographic=true; camera.orthographicSize=.45f;
        camera.clearFlags=CameraClearFlags.SolidColor; camera.backgroundColor=new Color(.025f,.055f,.09f);
        camera.cullingMask=~0; camera.nearClipPlane=.01f;
        var session=CprStationChecks.Prepared(config);
        for(int i=0;i<40;i++) Tick(session,0);
        for(float t=0;t<.64f;t+=.01f) Tick(session,t<60/110f?.055f*(1-Mathf.Cos(t*Mathf.PI*110/30))*.5f:0);
        for(int i=1;i<=20;i++) Tick(session,.04f*i/20);
        view.Show(session,0,0,false,true); Canvas.ForceUpdateCanvases();
        var target=new RenderTexture(600,1000,24); var image=new Texture2D(600,1000,TextureFormat.RGB24,false);
        var active=RenderTexture.active;
        camera.targetTexture=target; camera.Render(); RenderTexture.active=target;
        image.ReadPixels(new Rect(0,0,600,1000),0,0); image.Apply();
        File.WriteAllBytes("travel-meter-preview.png",image.EncodeToPNG());
        RenderTexture.active=active; camera.targetTexture=null;
        UnityEngine.Object.DestroyImmediate(image); UnityEngine.Object.DestroyImmediate(target);
    }
    static int passed;
    static CprHandSample Hand(float d,bool right=false) => new CprHandSample(new Pose(Vector3.up*((right?0:.035f)-d),Quaternion.identity));
    static void Check(bool ok,string label) { if(!ok) throw new InvalidOperationException(label); passed++; }
    static void Tick(CprPracticeSession s,float d) => s.Tick(.01f,Hand(d),Hand(d,true),new Pose(new Vector3(-.2f,0,.2f),Quaternion.identity),Pose.identity,new Pose(Vector3.up*.035f,Quaternion.identity),false,true,true);
    public static void Run()
    {
        CprV2Checks.Run();
        var config=Resources.Load<CprTrainingSettings>("RCP/TrainingSettings");
        var go=new GameObject("Travel meter test"); var view=go.AddComponent<CprStationView>(); view.Build(config);
        var panel=view.Panels[3].transform;
        var cursor=(RectTransform)panel.Find("Medición");
        var fill=(RectTransform)panel.Find("Recorrido actual");
        var peak=(RectTransform)panel.Find("Último ciclo");
        var session=CprStationChecks.Prepared(config);
        view.Show(session,0,0,false,true);
        Check(cursor.gameObject.activeSelf && cursor.sizeDelta.y>=15,"cursor visible antes de la primera muestra");
        for(int i=0;i<40;i++) Tick(session,0);
        for(int i=1;i<=20;i++) Tick(session,.055f*i/20);
        view.Show(session,0,0,false,true);
        Check(cursor.anchoredPosition.y>-80 && fill.sizeDelta.y>140,"recorrido visible durante bajada antes de completar ciclo");
        Check(session.Count==0 && session.Detector.DisplayFresh,"el medidor no requiere un ciclo contado");
        float retained=session.Detector.DisplayDepth;
        session.Detector.Tick(.01f,10,default,default,Pose.identity,true,true);
        view.Show(session,0,0,false,true);
        Check(!session.Detector.DisplayFresh && session.Detector.DisplayDepth==retained,"pérdida breve retiene lectura pero no la presenta como señal actual");
        Check(panel.Find("Estado").GetComponent<TMP_Text>().text.Contains("retenida"),"explica la pérdida de señal sin salto a cero");
        session.Detector.Suspend();
        Check(!session.Detector.DisplayFresh,"suspender invalida frescura inmediatamente");
        session.TogglePause(); view.Show(session,0,0,false,true);
        Check(cursor.gameObject.activeSelf && panel.Find("Estado").GetComponent<TMP_Text>().text.Contains("Pausado"),"pausar conserva marcador y explica estado");
        foreach(int cycles in new[]{0,1,2,30})
        {
            session=CprStationChecks.Prepared(config);
            for(int i=0;i<40;i++) Tick(session,0);
            for(float t=0;t<cycles*60/110f+.08f;t+=.01f)
                Tick(session,t<cycles*60/110f?.055f*(1-Mathf.Cos(t*Mathf.PI*110/30))*.5f:0);
            Check(session.Count==cycles,"conteo independiente del medidor: "+cycles);
            view.Show(session,0,0,false,true);
            if(cycles>0 && !session.Closed)
            {
                Check(peak.gameObject.activeSelf && peak.anchoredPosition.y>-65,"marca del último máximo permanece tras retorno: "+cycles);
                Check(cursor.anchoredPosition.y<-200,"cursor actual vuelve arriba sin confundirse con último máximo");
            }
            session.Finish(); view.Show(session,0,0,false,true);
            Check(session.Score.HasValue==(cycles==30),"cierre corto no inventa nota: "+cycles);
            Check(cycles==30 || !CprPracticeContent.Recommendation(session).Contains("Buen trabajo"),"recomendación apropiada para muestra: "+cycles);
        }
        UnityEngine.Object.DestroyImmediate(go);
        File.WriteAllText("travel-meter-checks.txt",$"PASS: {passed} travel meter and short-attempt checks. Quest test pending.");
        Debug.Log($"PASS: {passed} travel meter checks");
    }
}


