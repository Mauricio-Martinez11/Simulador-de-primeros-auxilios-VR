using System;
using System.Collections.Generic;
using FirstAidVR.Rcp;
using UnityEditor;
using UnityEngine;

public static class CprStationChecks
{
    public static int Passed {get;private set;}
    private static readonly Pose Chest=new Pose(Vector3.zero,Quaternion.identity);
    private static readonly Pose Shoulder=new Pose(new Vector3(-.2f,0,.2f),Quaternion.identity);
    private static CprHandSample Hand(float depth,bool right=false) => new CprHandSample(new Pose(new Vector3(0,(right?0:.035f)-depth,0),Quaternion.identity));
    private static void Check(bool value,string message) { if(!value) throw new InvalidOperationException("Estación RCP: "+message); Passed++; }

    [MenuItem("Primeros auxilios/RCP/Verificar estación y compresiones")]
    public static void Run()
    {
        Passed=0; var settings=UnityEngine.Object.Instantiate(Resources.Load<CprTrainingSettings>("RCP/TrainingSettings"));
        try
        {
            Check(settings.stationaryPractice,"estación activa por defecto");
            Check(settings.practiceInstructions.Length==8 && Array.TrueForAll(settings.practiceInstructions,c=>c!=null),"ocho instrucciones de voz incluidas");
            Check(settings.practiceVideos.Length==6 && Array.TrueForAll(settings.practiceVideos,c=>c!=null && c.length>5),"seis demostraciones animadas locales");
            foreach(var step in new[]{CprPracticeStep.Safety,CprPracticeStep.Response,CprPracticeStep.Breathing,CprPracticeStep.Help,CprPracticeStep.Placement})
            {
                var waiting=AtStep(settings,step);
                for(int i=0;i<700;i++) waiting.Tick(.02f,default,default,Shoulder,Chest,Chest,false,true,true);
                Check(waiting.Step==step && !waiting.StepPassed,"sin acción no avanza por tiempo: "+step);
            }
            var oneSide=AtStep(settings,CprPracticeStep.Safety);
            for(int i=0;i<100;i++) oneSide.Tick(.02f,default,default,Shoulder,Chest,Chest,false,true,true,new CprPracticeObservation{safetyLeft=true});
            Check(oneSide.SafetySeenMask==1 && !oneSide.StepPassed,"mirar un solo lado no completa seguridad");
            foreach(float dt in new[]{.02f,1f/72,1f/90})
            {
                foreach(float rate in new[]{80f,110f,140f})
                {
                    var d=Detector(settings); var events=new List<CprCompressionEvent>(); d.Compression+=events.Add;
                    float t=0; Rest(d,ref t,dt);
                    Wave(d,ref t,dt,rate,.055f,8);
                    Check(events.Count==8,$"un evento por ciclo a {rate} / min, dt={dt}");
                    Check(events[7].rateEvaluable && Mathf.Abs(events[7].rate-rate)<4,"ritmo de tiempos conocidos");
                    Check(events.TrueForAll(e=>e.depthCorrect && e.fullReturn),"recorrido y retorno medidos");
                    Check(events[7].rateCorrect==(rate==110),"clasificación de ritmo");
                }
            }
            foreach(float depth in new[]{.028f,.055f,.075f})
            {
                var d=Detector(settings); var events=new List<CprCompressionEvent>(); d.Compression+=events.Add; float t=0;
                Rest(d,ref t,.01f); Wave(d,ref t,.01f,110,depth,5);
                Check(events.Count==5,"ciclos evaluables aunque el recorrido sea corto o excesivo");
                Check(events.TrueForAll(e=>e.depthCorrect==(depth==.055f)),"criterio de recorrido virtual");
            }
            {
                var d=Detector(settings); int count=0; d.Compression+=e=>count++; float t=0;
                Rest(d,ref t,.01f); Wave(d,ref t,.01f,110,.009f,8);
                Check(count==0,"temblor menor que activación no cuenta");
                for(int i=0;i<100;i++) { t+=.02f; var h=Hand(.055f); h.pose.position+=Vector3.right*.3f; d.Tick(.02f,t,h,h,Chest,true); }
                Check(count==0 && d.State==CprCaptureState.OutsideZone,"movimiento fuera de zona no cuenta");
            }
            {
                var d=Detector(settings); var events=new List<CprCompressionEvent>(); d.Compression+=events.Add; float t=0;
                Rest(d,ref t,.01f); Wave(d,ref t,.01f,110,.055f,3);
                d.Tick(.02f,t+=.02f,Hand(.03f),Hand(.03f,true),Chest,true);
                for(int i=0;i<10;i++) d.Tick(.02f,t+=.02f,default,default,Chest,true);
                for(int i=0;i<40;i++) d.Tick(.02f,t+=.02f,Hand(.055f),Hand(.055f,true),Chest,true);
                Check(events.Count==3 && d.State==CprCaptureState.Recovering,"recuperar abajo no calibra ni cuenta");
                Rest(d,ref t,.01f); Wave(d,ref t,.01f,110,.055f,1);
                Check(events.Count==4 && !events[3].rateEvaluable,"se descarta ciclo interrumpido e intervalo sin señal");
            }
            {
                var d=Detector(settings); var events=new List<CprCompressionEvent>(); d.Compression+=events.Add; float t=0;
                Rest(d,ref t,.01f);
                for(int i=0;i<300;i++) { float depth=.02f+.035f*(1-Mathf.Cos(i*.01f*Mathf.PI*4))*.5f; d.Tick(.01f,t+=.01f,Hand(depth),Hand(depth,true),Chest,true); }
                Check(events.Count>=3 && events.TrueForAll(e=>!e.fullReturn),"retorno incompleto produce corrección por ciclo");
            }
            {
                var d=Detector(settings); int count=0; d.Compression+=e=>count++; float t=0;
                Rest(d,ref t,.01f);
                for(int i=0;i<400;i++) { float depth=.055f*(1-Mathf.Cos(i*.01f*Mathf.PI*110/30))*.5f; d.Tick(.01f,t+=.01f,Hand(depth),default,Chest,true); }
                Check(count>=6,"mano superior medida sostiene compresiones con inferior oculta");
                int before=count; d.Tick(.01f,t+=.01f,default,Hand(.055f,true),Chest,true);
                Check(count==before && d.State==CprCaptureState.Recovering,"cambio de fuente no crea un evento");
            }
            var empty=new CprPracticeSession(settings); empty.Finish();
            Check(empty.Closed && !empty.Completed && !empty.Score.HasValue,"intento vacío: interrumpido y no evaluable");
            var s=Prepared(settings,true);
            Check(s.Step==CprPracticeStep.Compressions,"secuencia completa mediante acciones sin confirmar botones");
            var outsideHeadZone=AtStep(settings,CprPracticeStep.Placement);
            outsideHeadZone.Tick(.02f,default,Hand(0,true),Shoulder,Chest,new Pose(Vector3.up*.035f,Quaternion.identity),false,false,true);
            Check(outsideHeadZone.Placement.LowerLatched,"mano derecha sobre el pecho se coloca aunque la cabeza quede fuera de la zona inicial");
            var occluded=AtStep(settings,CprPracticeStep.Placement);
            var upperPose=new Pose(Vector3.up*.035f,Quaternion.identity);
            occluded.Tick(.02f,default,Hand(0,true),Shoulder,Chest,upperPose,false,true,true);
            Check(occluded.Placement.LowerLatched && !occluded.Detector.Calibrated,"derecha inferior se capta sin espera");
            occluded.Tick(.02f,Hand(0),default,Shoulder,Chest,upperPose,false,true,true);
            Check(occluded.Step==CprPracticeStep.Compressions && occluded.Detector.Calibrated,
                "izquierda superior inicia RCP aunque Quest oculte la derecha");
            for(int i=0;i<40;i++) occluded.Tick(.01f,Hand(0),default,Shoulder,Chest,upperPose,false,true,true);
            for(float t=0;t<3;t+=.01f)
            {
                float depth=.055f*(1-Mathf.Cos(t*Mathf.PI*110/30))*.5f;
                occluded.Tick(.01f,Hand(depth),default,Shoulder,Chest,upperPose,false,true,true);
            }
            Check(occluded.Count>=4,"la mano izquierda visible permite contar compresiones sin la derecha");
            int previousCount=occluded.Count;
            for(int i=0;i<100;i++) occluded.Tick(.01f,default,default,Shoulder,Chest,upperPose,false,true,true);
            Check(occluded.Step==CprPracticeStep.Compressions && occluded.Count==previousCount,
                "la pérdida de seguimiento pausa el conteo sin separar la postura");
            var moved=new CprHandSample(new Pose(Vector3.right,Quaternion.identity));
            for(int i=0;i<65;i++) occluded.Tick(.01f,moved,default,Shoulder,Chest,upperPose,false,true,true);
            Check(occluded.Step==CprPracticeStep.Compressions && occluded.Count==previousCount,
                "separar la mano superior conserva el paso de compresiones y el contador");
            float clock=0; RunSessionWave(s,settings,ref clock,110,.055f,30);
            Check(s.Closed && s.Completed && s.Count==30,"cierre al completar treinta ciclos medidos");
            Check(s.Score==100,"puntuación completa de acciones y compresiones correctas");
            float frozen=s.TotalSeconds; int frozenCount=s.Count;
            for(int i=0;i<100;i++) s.Tick(.02f,Hand(.06f),Hand(.06f,true),Shoulder,Chest,new Pose(Vector3.up*.035f,Quaternion.identity),true,true,true);
            s.Finish(); Check(s.Count==frozenCount && s.TotalSeconds==frozen,"cierre idempotente e inmutable");
            var fresh=new CprPracticeSession(settings);
            Check(fresh.Count==0 && fresh.SequenceErrors==0 && !fresh.Score.HasValue,"nuevo intento sin datos heredados");
            var technical=Prepared(settings,false); clock=0;
            RunSessionWave(technical,settings,ref clock,110,.055f,12);
            int beforeLoss=technical.Count;
            float beforeLocation=technical.LocationSeconds;
            for(int i=0;i<200;i++) technical.Tick(.01f,default,default,Shoulder,Chest,Chest,false,true,true);
            Check(technical.Count==beforeLoss && technical.TrackingInterruptions==1 && technical.UntrackedSeconds>1.9f,"pérdida técnica separada de los eventos");
            Check(technical.PauseErrors==0 && technical.LocationSeconds==beforeLocation,"pérdida de seguimiento no penaliza pausa ni ubicación");
            float beforeFocus=technical.TotalSeconds;
            for(int i=0;i<200;i++) technical.Tick(.01f,Hand(.055f),Hand(.055f,true),Shoulder,Chest,Chest,false,true,false);
            Check(technical.Count==beforeLoss && technical.TotalSeconds==beforeFocus,"pérdida de foco congela tiempo y ciclos");
            RunSessionWave(technical,settings,ref clock,110,.055f,18);
            Check(technical.Completed && technical.Count==30 && technical.Score==100,"recuperación técnica no reduce la nota de un intento correcto");
            var interrupted=Prepared(settings,false); clock=0;
            RunSessionWave(interrupted,settings,ref clock,110,.055f,4); interrupted.Finish();
            Check(!interrupted.Completed && interrupted.Score.HasValue && interrupted.Score.Value<40,"sesión corta no obtiene nota completa");
            var paused=Prepared(settings,false); paused.TogglePause(); float duration=paused.TotalSeconds;
            paused.Tick(1,Hand(.06f),Hand(.06f,true),Shoulder,Chest,Chest,true,true,true);
            Check(paused.Count==0 && paused.TotalSeconds==duration && paused.CanAdjustHeight,"pausa congela registro y permite altura");
            paused.HeightChanged(); Check(paused.Step==CprPracticeStep.Compressions && !paused.Paused,"altura requiere nueva referencia");
            Check(CprStationController.ParticipantInPosition(new Vector3(0,.7f,-.7f),Chest),"posición de participante válida");
            Check(!CprStationController.ParticipantInPosition(new Vector3(0,.7f,1),Chest),"lado contrario no habilita práctica");
            Check(!CprStationController.ParticipantInPosition(new Vector3(2,.7f,-.7f),Chest),"participante fuera de alcance no habilita práctica");
            Debug.Log($"RCP: {Passed} comprobaciones de estación y compresiones correctas.");
        }
        finally { UnityEngine.Object.DestroyImmediate(settings); }
    }

    private static CprHandSample ResponseHand(int frame)
    { return new CprHandSample(new Pose(Shoulder.position+Vector3.right*(.02f*Mathf.Sin(frame*.02f*6)),Shoulder.rotation)); }
    public static CprPracticeSession AtStep(CprTrainingSettings settings,CprPracticeStep target)
    {
        var s=new CprPracticeSession(settings);
        const float dt=.02f;
        for(int i=0;i<3500 && s.Step!=target;i++)
        {
            var before=s.Step;
            var left=s.Step==CprPracticeStep.Response?ResponseHand(i):Hand(0);
            var evidence=new CprPracticeObservation{ safetyLeft=true,safetyRight=true,helpRequested=true };
            s.Tick(dt,left,Hand(0,true),Shoulder,Chest,new Pose(Vector3.up*.035f,Quaternion.identity),true,true,true,evidence);
        }
        Check(s.Step==target,"avance automático hasta "+target);
        return s;
    }
    public static CprPracticeSession Prepared(CprTrainingSettings settings,bool legacyClicks=false)
    {
        var s=AtStep(settings,CprPracticeStep.Safety);
        s.Primary(); s.Alternative();
        Check(s.Step==CprPracticeStep.Safety && !s.StepPassed,"botones antiguos no permiten saltar acciones");
        for(int i=0;i<120;i++) s.Tick(.02f,default,default,Shoulder,Chest,Chest,false,true,true);
        Check(s.Step==CprPracticeStep.Safety && !s.StepPassed,"tiempo por sí solo no completa seguridad");
        const float dt=.02f;
        for(int i=0;i<3500 && s.Step!=CprPracticeStep.Compressions;i++)
        {
            var h=s.Step==CprPracticeStep.Response?ResponseHand(i):Hand(0);
            var evidence=new CprPracticeObservation{ safetyLeft=true,safetyRight=true,helpRequested=true };
            s.Tick(dt,h,Hand(0,true),Shoulder,Chest,new Pose(Vector3.up*.035f,Quaternion.identity),true,true,true,evidence);
        }
        Check(s.Step==CprPracticeStep.Compressions && !s.Explaining && s.Count==0,"manos preparadas habilitan el bloque sin botón");
        return s;
    }
    private static CprCompressionDetector Detector(CprTrainingSettings settings)
    {
        var d=new CprCompressionDetector(settings); d.Calibrate(Hand(0),Hand(0,true),Chest); return d;
    }
    private static void Rest(CprCompressionDetector d,ref float t,float dt)
    { for(int i=0;i<Mathf.CeilToInt(.4f/dt);i++) d.Tick(dt,t+=dt,Hand(0),Hand(0,true),Chest,true); }
    private static void Wave(CprCompressionDetector d,ref float t,float dt,float rate,float depth,int cycles)
    {
        float duration=cycles*60/rate;
        for(float local=0;local<duration;local+=dt)
        { float y=depth*(1-Mathf.Cos(local*Mathf.PI*rate/30))*.5f; d.Tick(dt,t+=dt,Hand(y),Hand(y,true),Chest,true); }
        Rest(d,ref t,dt);
    }
    private static void RunSessionWave(CprPracticeSession s,CprTrainingSettings settings,ref float clock,float rate,float depth,int cycles)
    {
        const float dt=.01f;
        for(int i=0;i<30;i++) s.Tick(dt,Hand(0),Hand(0,true),Shoulder,Chest,Chest,false,true,true);
        for(float local=0;local<cycles*60/rate+.08f;local+=dt)
        {
            float y=local<cycles*60/rate?depth*(1-Mathf.Cos(local*Mathf.PI*rate/30))*.5f:0;
            s.Tick(dt,Hand(y),Hand(y,true),Shoulder,Chest,Chest,false,true,true); clock+=dt;
        }
    }
}

