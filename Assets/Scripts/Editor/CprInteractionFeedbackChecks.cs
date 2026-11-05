using System;
using System.Collections.Generic;
using System.IO;
using FirstAidVR.Rcp;
using UnityEngine;

public static class CprInteractionFeedbackChecks
{
    private static int passed;
    private static readonly Pose Chest=new Pose(Vector3.zero,Quaternion.identity);
    private static CprHandSample Hand(float depth,bool right=false)
    { return new CprHandSample(new Pose(Vector3.up*((right?0:.035f)-depth),Quaternion.identity)); }
    private static void Check(bool ok,string message)
    { if(!ok) throw new InvalidOperationException("RCP interacción: "+message); passed++; }
    public static void RunAll() { Run(); CprStationPlayChecks.BuildAndRun(); }
    public static void Run()
    {
        CprBreathingChecks.Run();
        CprReliabilityChecks.Run();
        passed=0;
        var settings=ScriptableObject.CreateInstance<CprTrainingSettings>();
        var go=new GameObject("Pruebas de teléfono");
        try
        {
            var sceneConfig=Resources.Load<CprTrainingSettings>("RCP/TrainingSettings");
            var actions=go.AddComponent<CprScenarioActions>(); actions.Build(sceneConfig);
            for(int side=0;side<2;side++)
            {
                actions.ResetPhone();
                var s=CprStationChecks.AtStep(settings,CprPracticeStep.Help);
                var head=new Pose(actions.Phone.position+Vector3.up*.7f,Quaternion.identity);
                var hand=new CprHandSample(new Pose(actions.Phone.position,Quaternion.Euler(12,25,8)),false,.8f,true);
                for(int i=0;i<12;i++) actions.Tick(s,head,side==0?hand:default,side==1?hand:default,.02f,true);
                Check(actions.PhoneHolder==side,"agarre con "+(side==0?"izquierda":"derecha"));
                var expected=CprScenarioActions.PhonePose(hand,side==0);
                Check(Vector3.Distance(actions.Phone.position,expected.position)<.0001f,"teléfono fuera del centro del puño "+side);
                Check(Vector3.Dot(actions.Phone.forward,hand.pose.rotation*Vector3.up)>.999f,"pantalla orientada hacia la palma abierta "+side);
                Check(Vector3.Dot(actions.Phone.up,hand.pose.rotation*Vector3.forward)>.999f,"eje largo alineado con dedos "+side);
                hand.pose.position=head.position+Vector3.right*(side==0?-.15f:.15f);
                CprPracticeObservation result=default;
                for(int i=0;i<55;i++) result=actions.Tick(s,head,side==0?hand:default,side==1?hand:default,.02f,true);
                Check(result.helpRequested,"llamada simulada con "+side);
                hand.grip=.05f;
                actions.Tick(s,head,side==0?hand:default,side==1?hand:default,.02f,true);
                Check(!actions.HoldingPhone,"abrir la mano suelta el teléfono "+side);
            }
            foreach(bool useGaps in new[]{false,true})
            {
                var detector=new CprCompressionDetector(settings);
                detector.Calibrate(Hand(0),Hand(0,true),Chest);
                float t=0; var events=new List<CprCompressionEvent>(); detector.Compression+=events.Add;
                for(int i=0;i<30;i++) detector.Tick(.01f,t+=.01f,Hand(0),Hand(0,true),Chest,true,true);
                int frame=0;
                for(float time=0;time<8*60/110f+.05f;time+=.01f,frame++)
                {
                    float d=time<8*60/110f?.055f*(1-Mathf.Cos(time*Mathf.PI*110/30))*.5f:0;
                    bool left=frame%4<2, missing=useGaps && frame%31==12;
                    detector.Tick(.01f,t+=.01f,missing||!left?default:Hand(d),missing||left?default:Hand(d,true),Chest,true,true);
                    Check(detector.DisplayAvailable,"recorrido visible con cambios de fuente "+frame);
                }
                Check(events.Count==8,"ocho ciclos con alternancia de manos y pérdidas breves="+useGaps+"; count="+events.Count);
                Check(events.TrueForAll(e=>e.depthCorrect && e.fullReturn),"precisión del recorrido y retorno con oclusión");
                Check(events[7].rateCorrect && Mathf.Abs(events[7].rate-110)<2,"ritmo continuo con fuentes alternadas");
                int before=events.Count;
                for(int i=0;i<40;i++) detector.Tick(.01f,t+=.01f,default,default,Chest,true,true);
                Check(detector.DisplayAvailable && !detector.DisplayFresh,"última lectura visible e identificada como antigua");
                Check(events.Count==before,"sin señal no inventa ciclos");
            }
            {
                var d=new CprCompressionDetector(settings); d.Calibrate(Hand(0),Hand(0,true),Chest);
                d.Tick(.01f,0,Hand(.045f),default,Chest,true,true);
                Check(d.State==CprCaptureState.Recovering && d.DisplayFresh && Mathf.Abs(d.DisplayDepth-.045f)<.001f,
                    "muestra el recorrido aunque todavía espere el reposo");
            }
            float interval=60/110f;
            foreach(var pair in new[]{new Vector2(-.16f,(float)CprBeatResult.Early),new Vector2(.04f,(float)CprBeatResult.OnTime),new Vector2(.20f,(float)CprBeatResult.Late)})
            {
                var pacing=new CprPacing(110); pacing.Tick(.01f,0);
                float time=interval*CprBeatClock.GreenStart+pair.x;
                pacing.Tick(time,time); pacing.ObservePress(time);
                Check(pacing.LastResult==(CprBeatResult)(int)pair.y,"clasifica presión respecto al pitido "+pair.x);
                Check(Mathf.Abs(pacing.LastOffsetSeconds-pair.x)<.001f,"desfase temporal reproducible");
            }
            var extra=new CprPacing(110); extra.Tick(.01f,0); extra.Tick(interval*.8f,interval*.8f);
            Check(extra.PulseVersion==1,"un pulso al llegar a verde");
            extra.ObservePress(1); extra.Tick(.04f,1.04f); extra.ObservePress(1.04f);
            Check(extra.LastResult==CprBeatResult.ExtraPress,"dos presiones por pulso reciben corrección");
            int presses=extra.OnTime+extra.Early+extra.Late+extra.Extra;
            extra.Tick(interval*3,3);
            Check(extra.OnTime+extra.Early+extra.Late+extra.Extra==presses,"el metrónomo por sí solo no registra presiones");
            extra.Reset(); Check(!extra.FeedbackVisible,"pausa limpia aviso temporal");
            File.WriteAllText("interaction-feedback-checks.txt",$"PASS: {passed} phone, measurement continuity, display and beat-response checks. Reliability regression passed. Controlled inputs in Unity; physical Quest retest pending.\n");
            Debug.Log($"PASS: {passed} phone and feedback checks");
        }
        finally { UnityEngine.Object.DestroyImmediate(go); UnityEngine.Object.DestroyImmediate(settings); }
    }
}
