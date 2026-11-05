using System;
using FirstAidVR.Rcp;
using UnityEditor;
using UnityEngine;

public static class CprFlowChecks
{
    public static int Passed { get; private set; }
    [MenuItem("Primeros auxilios/RCP/Verificar secuencia y detección")]
    public static void Run()
    {
        Passed = 0;
        var settings = ScriptableObject.CreateInstance<CprTrainingSettings>();
        try
        {
            var target = new Pose(Vector3.zero, Quaternion.identity);
            var top = new Pose(Vector3.up * settings.stackedHandHeight, Quaternion.identity);
            var good = new CprHandSample(target);
            var upper = new CprHandSample(top);
            var away = new CprHandSample(new Pose(Vector3.right, Quaternion.identity));
            Check(!CprFlow.Matches(default,target,.1f,45,65),"Rechaza manos sin seguimiento");
            Check(!CprFlow.Matches(new CprHandSample(new Pose(Vector3.zero,Quaternion.Euler(180,0,0))),target,.1f,45,65),"Rechaza palma invertida");
            Check(!CprFlow.Matches(away,target,.1f,45,65),"Rechaza mano alejada");
            Check(!CprFlow.Matches(new CprHandSample(new Pose(Vector3.zero,Quaternion.Euler(0,180,0))),target,.1f,45,65),"Rechaza dedos invertidos");
            var flow = new CprFlow(settings);
            Action<float,CprHandSample,CprHandSample,bool> tick = (seconds,l,r,look) =>
            { for(int i=0;i<Mathf.CeilToInt(seconds/.02f);i++) flow.Tick(.02f,l,r,target,target,top,look); };
            flow.Confirm(); Check(flow.Step==CprStep.Safety,"No confirma inmediatamente");
            tick(1,default,default,false); flow.Confirm();
            Check(flow.Step==CprStep.MovePatient,"La seguridad abre el traslado");
            tick(2,good,upper,true); flow.Confirm();
            Check(flow.Step==CprStep.MovePatient,"No omite el traslado por mirada o contacto");
            flow.PatientPlaced(); Check(flow.Step==CprStep.Response,"Solo el snap confirmado inicia la respuesta");
            tick(1,default,good,false); Check(flow.Step==CprStep.Breathing,"El hombro admite la derecha");
            tick(2,default,default,false); Check(flow.Progress==0,"No observa sin mirar");
            tick(5.1f,default,default,true); Check(flow.Step==CprStep.CallForHelp,"Completa la observación");
            tick(1,default,default,false); flow.Confirm();
            tick(.1f,good,default,false); Check(flow.Step==CprStep.LowerHand,"La izquierda no sustituye a la derecha inferior");
            tick(.02f,default,good,false);
            Check(flow.Step==CprStep.UpperHand && flow.LowerHand==1 && flow.LowerLatched,"La derecha se coloca sin espera");
            tick(.2f,default,away,false);
            Check(flow.Step==CprStep.LowerHand && !flow.LowerLatched,"Mover la derecha separa su mano del holograma");
            tick(.02f,default,good,false);
            var reversed=new CprHandSample(new Pose(target.position,Quaternion.Euler(180,0,0)));
            tick(.2f,default,reversed,false);
            Check(flow.Step==CprStep.LowerHand,"Cambiar la postura de la palma suelta el enganche");
            tick(.02f,default,good,false);
            tick(.1f,good,good,false); Check(!flow.StackLatched,"No acepta manos a la misma altura");
            tick(.5f,default,default,false); Check(flow.LowerLatched,"Tolera una interrupción breve de las dos manos");
            tick(.4f,default,default,false); Check(flow.Step==CprStep.LowerHand,"Libera al perder ambas manos más allá del margen");
            tick(.02f,default,good,false);
            tick(.02f,upper,default,false);
            Check(flow.StackLatched && flow.UsingOcclusion,"La izquierda superior engancha con la derecha oculta");
            tick(8,upper,default,false); Check(flow.StackLatched,"La izquierda visible sostiene el enganche durante la oclusión");
            tick(8,default,default,false);
            Check(flow.StackLatched && flow.UsingOcclusion,"La pérdida de seguimiento no desengancha dos manos ya colocadas");
            tick(.2f,upper,good,false);
            Check(flow.StackLatched,"Recuperación del seguimiento sin parpadeo");
            tick(.06f,away,good,false); Check(flow.StackLatched,"Filtra un salto de seguimiento de pocos fotogramas");
            tick(.2f,upper,good,false); tick(.2f,away,good,false);
            Check(flow.Step==CprStep.UpperHand && flow.LowerLatched,"Separar la izquierda suelta la mano superior");
            tick(.02f,upper,good,false); tick(.2f,upper,away,false);
            Check(flow.Step==CprStep.LowerHand && !flow.StackLatched,"Apartar la derecha libera toda la postura");
            tick(.02f,default,good,false); tick(.02f,upper,good,false);
            tick(.2f,default,good,false); Check(flow.StackLatched,"Tolera pérdida breve de la izquierda superior");
            flow.Suspend();
            Check(!flow.LowerLatched && flow.Progress==0 && flow.Step==CprStep.LowerHand,"Perder foco borra el enganche");
            tick(.02f,default,good,false); tick(.02f,upper,good,false);
            flow.BeginRelocation();
            Check(flow.Step==CprStep.MovePatient && !flow.LowerLatched,"Volver a agarrar al paciente libera las manos RCP");
            flow.Restart(); Check(flow.Step==CprStep.Safety && flow.LowerHand==-1,"Reinicio limpia la selección");

            tick(1,default,default,false); flow.Confirm(); flow.PatientPlaced();
            tick(1,good,default,false); Check(flow.Step==CprStep.Breathing,"El hombro admite la izquierda");
            tick(.02f,default,default,true); tick(10.1f,default,default,false);
            Check(flow.Step==CprStep.CallForHelp && flow.ObservationTimedOut,"La observación termina al llegar a 10 segundos");
            tick(1,default,default,false); flow.Confirm(); tick(.02f,default,good,false);
            var controllerUpper = new CprHandSample(new Pose(top.position+Vector3.up*settings.controllerStackExtra,top.rotation),true);
            tick(.02f,controllerUpper,good,false);
            Check(flow.StackLatched,"Permite el espacio adicional de los mandos");
            for(int i=0;i<50;i++) flow.TickCompression(.02f,default,default,target,top);
            Check(flow.StackLatched,"Durante RCP la pérdida de las dos manos conserva el enganche sin fingir movimiento");
            var pressing=new CprHandSample(new Pose(controllerUpper.pose.position-Vector3.up*.055f,Quaternion.identity),true);
            flow.TickCompression(.02f,pressing,default,target,top);
            Check(flow.StackLatched,"La mano superior puede bajar durante la compresión sin soltarse");
            for(int i=0;i<30;i++) flow.TickCompression(.02f,away,default,target,top);
            Check(!flow.StackLatched,"Una mano superior visiblemente apartada libera la postura durante RCP");
            Debug.Log($"RCP: {Passed} verificaciones de secuencia y oclusión correctas.");
        }
        finally { UnityEngine.Object.DestroyImmediate(settings); }
    }
    private static void Check(bool condition,string message)
    {
        if(!condition) throw new InvalidOperationException("RCP: "+message);
        Passed++;
    }
}


