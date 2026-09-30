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
            tick(1.5f,default,good,false); Check(flow.Step==CprStep.LowerHand,"La derecha no sustituye a la izquierda inferior");
            tick(.6f,good,default,false); tick(.1f,default,default,false);
            Check(flow.Progress==0 && !flow.LowerLatched,"Sin captura inicial, la pérdida borra el progreso");
            tick(1.3f,good,default,false);
            Check(flow.Step==CprStep.UpperHand && flow.LowerHand==0 && flow.LowerLatched,"Captura primero la izquierda");
            tick(1.4f,good,good,false); Check(!flow.StackLatched,"No acepta manos a la misma altura");
            tick(.5f,default,default,false); Check(flow.LowerLatched,"Tolera una interrupción breve de las dos manos");
            tick(.4f,default,default,false); Check(flow.Step==CprStep.LowerHand,"Libera al perder ambas manos más allá del margen");
            tick(1.3f,good,default,false);
            tick(1.3f,default,upper,false);
            Check(flow.StackLatched && flow.UsingOcclusion,"Engancha la derecha con la izquierda ya capturada y oculta");
            tick(8,default,upper,false); Check(flow.StackLatched,"La derecha visible sostiene el enganche durante la oclusión");
            tick(.4f,default,default,false); tick(.2f,good,upper,false);
            Check(flow.StackLatched,"Recuperación breve sin parpadeo");
            tick(.06f,good,away,false); Check(flow.StackLatched,"Filtra un salto de seguimiento de pocos fotogramas");
            tick(.2f,good,upper,false); tick(.2f,good,away,false);
            Check(flow.Step==CprStep.UpperHand && flow.LowerLatched,"Separar la derecha suelta la mano superior");
            tick(1.3f,good,upper,false); tick(.2f,away,upper,false);
            Check(flow.Step==CprStep.LowerHand && !flow.StackLatched,"Apartar la izquierda libera toda la postura");
            tick(1.3f,good,default,false); tick(1.3f,good,upper,false);
            tick(2,good,default,false); Check(flow.StackLatched && flow.UsingOcclusion,"Tolera oclusión de la derecha con izquierda visible");
            flow.Suspend();
            Check(!flow.LowerLatched && flow.Progress==0 && flow.Step==CprStep.LowerHand,"Perder foco borra el enganche");
            tick(1.3f,good,default,false); tick(1.3f,good,upper,false);
            flow.BeginRelocation();
            Check(flow.Step==CprStep.MovePatient && !flow.LowerLatched,"Volver a agarrar al paciente libera las manos RCP");
            flow.Restart(); Check(flow.Step==CprStep.Safety && flow.LowerHand==-1,"Reinicio limpia la selección");

            tick(1,default,default,false); flow.Confirm(); flow.PatientPlaced();
            tick(1,good,default,false); Check(flow.Step==CprStep.Breathing,"El hombro admite la izquierda");
            tick(.02f,default,default,true); tick(10.1f,default,default,false);
            Check(flow.Step==CprStep.CallForHelp && flow.ObservationTimedOut,"La observación termina al llegar a 10 segundos");
            tick(1,default,default,false); flow.Confirm(); tick(1.3f,good,default,false);
            var controllerUpper = new CprHandSample(new Pose(top.position+Vector3.up*settings.controllerStackExtra,top.rotation),true);
            tick(1.3f,good,controllerUpper,false);
            Check(flow.StackLatched,"Permite el espacio adicional de los mandos");
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

