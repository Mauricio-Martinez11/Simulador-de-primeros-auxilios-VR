using System;
using System.IO;
using FirstAidVR.Rcp;
using UnityEngine;

public static class CprReliabilityChecks
{
    public static int Passed { get; private set; }
    private static readonly Pose Chest=new Pose(Vector3.zero,Quaternion.identity);
    private static readonly Pose Upper=new Pose(Vector3.up*.035f,Quaternion.identity);
    private static readonly Pose Shoulder=new Pose(Vector3.forward*.3f,Quaternion.identity);
    private static CprHandSample Hand(float depth=0, bool right=false, float lateral=0)
    { return new CprHandSample(new Pose(new Vector3(lateral,(right?0:.035f)-depth,0),Quaternion.identity)); }
    private static void Tick(CprPracticeSession s,CprHandSample left,CprHandSample right,float dt=.01f,bool focus=true)
    { s.Tick(dt,left,right,Shoulder,Chest,Upper,false,false,focus); }
    private static void Hold(CprPracticeSession s,CprHandSample left,CprHandSample right,float seconds)
    { for(int i=0;i<Mathf.CeilToInt(seconds/.01f);i++) Tick(s,left,right); }
    private static void Restore(CprPracticeSession s) { Hold(s,Hand(),Hand(0,true),.4f); }
    private static void Wave(CprPracticeSession s,int cycles,float depth,bool hideLower=false,bool noisyLower=false)
    {
        float duration=cycles*60/110f;
        for(float t=0;t<duration+.03f;t+=.01f)
        {
            float d=t<duration?depth*(1-Mathf.Cos(t*Mathf.PI*110/30))*.5f:0;
            var lower=hideLower?default:Hand(d,true);
            if(noisyLower) lower.pose.rotation=Quaternion.Euler(180,0,180);
            Tick(s,Hand(d),lower);
        }
    }
    private static void Check(bool ok,string reason)
    { if(!ok) throw new InvalidOperationException("RCP estabilidad: "+reason); Passed++; }

    public static void RunAll()
    {
        Run();
        CprStationPlayChecks.BuildAndRun();
    }

    public static void Run()
    {
        CprHandInputChecks.Run();
        Passed=0;
        var config=ScriptableObject.CreateInstance<CprTrainingSettings>();
        try
        {
            var s=CprStationChecks.Prepared(config); Restore(s);
            Wave(s,6,.055f,true);
            Check(s.Count==6 && s.Placement.StackLatched,"la izquierda visible basta para medir aunque no mire al pecho");
            int before=s.Count;
            Hold(s,default,default,30);
            Check(s.Count==before && s.Placement.StackLatched,"30 s fuera de vista conservan la unión sin ciclos inventados");
            Check(s.LocationErrors==0 && s.HandSeparationErrors==0 && s.PauseErrors==0,"la pérdida técnica no crea errores del usuario");
            var noisy=Hand(0,true,.5f); noisy.reliable=false;
            Hold(s,Hand(),noisy,2);
            Check(s.Placement.StackLatched && s.HandSeparationErrors==0,"mano inferior de baja confianza lejos no suelta la postura");
            Restore(s); Wave(s,6,.055f,false,true);
            Check(s.Count==before+6 && s.Placement.StackLatched,"rotación invertida de la mano oculta no bloquea ni suelta");
            Hold(s,Hand(0,false,.3f),Hand(0,true),.15f); Restore(s);
            Check(s.Placement.StackLatched && s.HandSeparationErrors==0,"salto breve de alta confianza no confirma separación");
            before=s.Count;
            for(int i=0;i<50;i++) Tick(s,default,default,.01f,false);
            Check(s.Placement.StackLatched,"perder foco conserva la unión");
            Restore(s); s.TogglePause(); s.TogglePause(); Restore(s);
            Check(s.Placement.StackLatched && s.Count==before,"pausar y reanudar no exige colocar las manos otra vez");
            Wave(s,18,.055f,true);
            Check(s.Completed && s.Score==100,"intento completo con oclusión y foco recuperado conserva 100");

            var evidence=CprStationChecks.Prepared(config); Restore(evidence);
            for(int incident=0;incident<5;incident++)
            {
                Hold(evidence,Hand(0,false,.24f),Hand(0,true),.7f);
                Check(evidence.HandSeparationErrors==incident+1,"separación sostenida cuenta una vez "+incident);
                Hold(evidence,Hand(0,false,.24f),Hand(0,true),.7f);
                Check(evidence.HandSeparationErrors==incident+1,"mantener separadas no duplica el incidente "+incident);
                Restore(evidence);
                Check(evidence.Placement.StackLatched,"recupera la postura tras separación "+incident);
            }
            for(int incident=0;incident<5;incident++)
            {
                Hold(evidence,Hand(0,false,.3f),Hand(0,true,.3f),.7f);
                Check(evidence.LocationErrors==incident+1,"alejar el conjunto cuenta una vez "+incident);
                Restore(evidence);
            }
            Wave(evidence,6,.055f);
            Wave(evidence,24,.028f);
            Check(evidence.Completed && evidence.Count==30,"bloque con incidencias conserva sus treinta ciclos");
            Check(evidence.DepthHits==6 && evidence.Shallow==24,"ejemplo explicable: seis recorridos correctos, veinticuatro cortos");
            Check(evidence.Score==60,"ejemplo 60 = preparación20 + colocación0 + recorrido5 + ritmo25 + retorno10");
            Check(evidence.TrackingInterruptions==0,"una separación confirmada con muestras fiables no se registra como pérdida técnica");
            var b=evidence.Breakdown;
            Check(b.preparation==20 && b.placement==0 && Mathf.Abs(b.travel-5)<.001f && b.rhythm==25 && b.recoil==10 && b.pauseDeduction==0,"desglose reproduce exactamente la nota");
            string directory=Path.GetFullPath("Logs/SimulatedReportChecks");
            string path=CprAttemptReport.Save(evidence,directory,"PRUEBA AUTOMATIZADA / DATOS SIMULADOS","poses sintéticas de prueba");
            var roundtrip=JsonUtility.FromJson<CprAttemptReport>(File.ReadAllText(Path.ChangeExtension(path,".json")));
            Check(roundtrip.score.total==60 && roundtrip.compressions.Length==30 && roundtrip.handsAway==5 && roundtrip.handsSeparated==5,"archivo JSON conserva nota, ciclos e incidencias");
            Check(roundtrip.environment.Contains("SIMULADOS") && File.ReadAllText(path).Contains("CRONOLOGÍA"),"registro de prueba identificado y con cronología legible");
            string again=CprAttemptReport.Save(evidence,directory,"PRUEBA AUTOMATIZADA / DATOS SIMULADOS","poses sintéticas de prueba");
            Check(again==path && !File.Exists(path+".tmp"),"guardar otra vez no duplica el intento");

            var zone=CprStationChecks.Prepared(config); Restore(zone);
            Hold(zone,Hand(0,false,.11f),Hand(0,true,.11f),2);
            Check(zone.LocationErrors==1 && zone.Placement.StackLatched,"salida moderada de zona cuenta una vez y conserva el enganche");
            Hold(zone,Hand(0,false,.3f),Hand(0,true,.3f),.7f);
            Check(zone.LocationErrors==1,"alejar más las mismas manos no duplica el episodio fuera de zona");
            Restore(zone); Hold(zone,Hand(0,false,.11f),Hand(0,true,.11f),1);
            Check(zone.LocationErrors==2,"nuevo episodio tras recuperar el centro cuenta de nuevo");
            var empty=new CprPracticeSession(config); empty.Finish();
            Check(!CprAttemptReport.From(empty,"test","none").score.evaluable,"sin ciclos no se publica una nota");
            File.WriteAllText("reliability-checks.txt",$"PASS: {Passed} reliability, scoring and persistence checks. Input, flow and station checks passed. Synthetic data; Quest hardware verification pending.\nSimulated report: {path}\n");
            Debug.Log($"PASS: {Passed} RCP reliability checks");
        }
        finally { UnityEngine.Object.DestroyImmediate(config); }
    }
}
