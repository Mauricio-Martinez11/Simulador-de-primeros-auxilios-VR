using System;
using System.Reflection;
using UnityEngine;
using FirstAidVR.Rcp;
public static class CprCorrectionVoiceChecks
{
    static void Check(bool value,string message) { if(!value) throw new Exception(message); }
    public static void Run()
    {
        var gate=new CprCorrectionVoice(); float busyUntil=0,last=-100; int spoken=0;
        for(int i=0;i<6000;i++)
        {
            float now=i*.01f;
            var key=gate.Tick(.01f,"Fast",now<busyUntil,true);
            if(key==null) continue;
            Check(now>=busyUntil,"A repeated error interrupted its own audio");
            Check(now-last>=21.9f,"Repeated audio was not rate limited");
            last=now; busyUntil=now+4; spoken++;
        }
        Check(spoken==3,"Continuous fast errors should produce three notices in one minute");
        gate=new CprCorrectionVoice();
        for(int i=0;i<1000;i++) Check(gate.Tick(.01f,"Separate",true,true)==null,"Must not overlap speech");
        for(int i=0;i<1000;i++) Check(gate.Tick(.01f,null,false,true)==null,"A corrected error was queued");
        for(int i=0;i<1000;i++) Check(gate.Tick(.01f,"Fast",false,false)==null,"Muted/inactive speech");
        gate=new CprCorrectionVoice();
        for(int i=0;i<70;i++) gate.Tick(.01f,"Separate",false,true);
        for(int i=0;i<700;i++) Check(gate.Tick(.01f,"Fast",false,true)==null,"Different errors bypass global silence");
        foreach(var key in new[]{"Separate","Away","Lost","Fast","Slow","Return","Shallow","Deep","Timing"})
        { Check(CprNarration.Clip(key)!=null,"Missing corrective voice: "+key); Check(!string.IsNullOrEmpty(CprNarration.Get(key).text),"Missing text"); }
        var config=Resources.Load<CprTrainingSettings>("RCP/TrainingSettings");
        var s=CprStationChecks.Prepared(config); var go=new GameObject("Nonblocking correction check");
        var audio=go.AddComponent<CprStationAudio>(); audio.Initialize(config);
        var flags=BindingFlags.Instance|BindingFlags.NonPublic;
        typeof(CprStationAudio).GetField("activeSession",flags).SetValue(audio,s);
        typeof(CprStationAudio).GetField("lastStep",flags).SetValue(audio,6);
        typeof(CprStationAudio).GetField("instructionDone",flags).SetValue(audio,true);
        var narrator=(CprNarrator)typeof(CprStationAudio).GetField("narrator",flags).GetValue(audio);
        narrator.Play("Start"); narrator.Stop();
        for(int i=0;i<20;i++) narrator.Tick(.12f,true);
        bool heard=false;
        for(int i=0;i<1000 && !s.Closed;i++)
        {
            float depth=i<40?0:.055f*(1-Mathf.Cos((i-40)/90f*Mathf.PI*150/30))*.5f;
            audio.Prepare(s,1/90f,true);
            Check(!s.Explaining,"Correction gated compression input");
            s.Tick(1/90f,new CprHandSample(new Pose(Vector3.up*(.035f-depth),Quaternion.identity)),
                new CprHandSample(new Pose(Vector3.down*depth,Quaternion.identity)),Pose.identity,Pose.identity,
                new Pose(Vector3.up*.035f,Quaternion.identity),true,true,true);
            audio.Tick(s,1/90f,true);
            var voice=(CprNarrator)typeof(CprStationAudio).GetField("corrections",flags).GetValue(audio);
            heard|=voice.Key!=null;
        }
        Check(heard && s.Count>=15 && s.Fast>0,"Voice must run while fast cycles continue to count");
        UnityEngine.Object.DestroyImmediate(go);
        Debug.Log("PASS: correction voice spam suppression, no queue, pause/mute, clips and continuous scored cycles="+s.Count);
        CprContinuousStartChecks.Run();
        var shoulder=new CprShoulderContact();
        for(int i=0;i<100;i++) shoulder.Tick(.02f,new CprHandSample(Pose.identity),default,Pose.identity,true,false);
        Check(!shoulder.Completed,"Shoulder must wait for its explanation");
        for(int i=0;i<71;i++) shoulder.Tick(.02f,new CprHandSample(Pose.identity),default,Pose.identity,true,true);
        Check(shoulder.Completed && shoulder.Movements==2,"Documented shoulder condition must match implementation");
    }
}
