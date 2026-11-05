using System;
using FirstAidVR.Rcp;
using UnityEngine;
using UnityEditor.SceneManagement;
public static class CprRecenterChecks
{
    static int passed;
    static void Check(bool ok,string label) { if(!ok) throw new InvalidOperationException(label); passed++; }
    public static void Run()
    {
        CprReliabilityChecks.Run();
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene);
        var root=new GameObject("Fixed setup").transform;
        var anchor=new GameObject("Panel pivot").transform; anchor.SetParent(root); anchor.localPosition=new Vector3(0,1.6f,3);
        var patient=new GameObject("Patient").transform; patient.SetParent(root); patient.localPosition=new Vector3(.4f,.2f,.1f);
        var local=patient.localPosition;
        var head=new GameObject("Head").transform;
        foreach(float yaw in new[]{0,90,180,270})
        {
            head.SetPositionAndRotation(new Vector3(3,1.7f,-2),Quaternion.Euler(-20,yaw,0));
            CprWorldRecenter.Align(root,anchor,head,3,false);
            Check(Vector3.Distance(anchor.position,head.position+CprWorldRecenter.Forward(head)*3)<.001f,"menú delante con pivote desplazado: "+yaw);
            Check(Vector3.Dot(root.forward,CprWorldRecenter.Forward(head))>.999f,"orientación frontal: "+yaw);
            Check(patient.localPosition==local,"grupo conserva posición relativa");
            float height=anchor.position.y;
            CprWorldRecenter.Align(root,anchor,head,.85f,true);
            Check(Mathf.Abs(anchor.position.y-height)<.001f,"recentrar estación conserva altura");
            var fixedPose=new Pose(root.position,root.rotation);
            head.position+=Vector3.right*2; head.Rotate(0,45,0);
            Check(root.position==fixedPose.position && root.rotation==fixedPose.rotation,"movimiento normal no arrastra estación");
        }
        foreach(float initialYaw in new[]{0f,90f,175f})
        {
            var reference=Quaternion.Euler(0,initialYaw,0)*Vector3.forward;
            Check(CprScenarioActions.ObservedSide(reference,Quaternion.Euler(-10,initialYaw-35,0))==-1,"mirada horizontal izquierda");
            Check(CprScenarioActions.ObservedSide(reference,Quaternion.Euler(10,initialYaw+35,0))==1,"mirada horizontal derecha");
            Check(CprScenarioActions.ObservedSide(reference,Quaternion.Euler(60,initialYaw,0))==0,"mirar abajo sin girar no completa lado");
        }
        var config=Resources.Load<CprTrainingSettings>("RCP/TrainingSettings");
        var actions=root.gameObject.AddComponent<CprScenarioActions>(); actions.Build(config);
        var session=CprStationChecks.AtStep(config,CprPracticeStep.Safety);
        var pose=new Pose(new Vector3(0,1.6f,-.85f),Quaternion.identity);
        actions.Tick(session,pose,default,default,.02f,true);
        foreach(float yaw in new[]{-35f,35f})
        {
            pose.rotation=Quaternion.Euler(0,yaw,0);
            for(int i=0;i<34;i++)
            {
                var observation=actions.Tick(session,pose,default,default,.02f,true);
                session.Tick(.02f,default,default,Pose.identity,Pose.identity,Pose.identity,false,true,true,observation);
            }
        }
        foreach(var target in actions.SafetyTargets)
        {
            Check(Mathf.Abs(target.position.y-.015f)<.001f,"marcador sobre el suelo");
            Check(Mathf.Abs(Vector3.Dot(target.forward,Vector3.up))>.999f,"marcador horizontal legible");
        }
        Check(actions.SafetyTargets[0].position.x<pose.position.x && actions.SafetyTargets[1].position.x>pose.position.x,"marcas a ambos lados del participante");
        var fixedTarget=actions.SafetyTargets[1].position;
        actions.Tick(session,new Pose(pose.position+Vector3.right*.1f,pose.rotation),default,default,.02f,true);
        Check(actions.SafetyTargets[1].position==fixedTarget,"marcas no siguen la cabeza");
        Check(session.StepPassed && session.SafetySeenMask==3,"ambos giros completan seguridad sin apuntar al piso");
        session=CprStationChecks.Prepared(config);
        session.WorldRecentered();
        Check(session.Step==CprPracticeStep.Compressions && session.Count==0 && session.LocationErrors==0 && session.HandSeparationErrors==0,"recentrar exige calibrar manos sin penalización");
        Check(!CprWorldRecenter.PoseSettled(.1f,.1f,2),"no aplica con los dos fotogramas antiguos");
        Check(!CprWorldRecenter.PoseSettled(.4f,.05f,20),"espera después de cambiar el origen");
        Check(CprWorldRecenter.PoseSettled(.5f,.15f,25),"aplica tras estabilizarse los anclajes");
        Check(CprWorldRecenter.PoseSettled(1.6f,0,60),"movimiento continuo no bloquea recentrado");
        var camera=head.gameObject.AddComponent<Camera>(); camera.tag="MainCamera";
        var introObject=new GameObject("Introducción con laterales");
        var intro=introObject.AddComponent<CprIntroPanel>(); intro.Build(camera); intro.EnableNavigation();
        var canvas=introObject.GetComponentInChildren<Canvas>();
        var left=canvas.transform.Find("Diálogo de la guía"); var right=canvas.transform.Find("Opciones de la guía");
        var leftLocal=left.localPosition; var rightLocal=right.localPosition;
        var recenter=typeof(CprIntroPanel).GetMethod("Recenter",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic);
        foreach(float yaw in new[]{0,90,180,270})
        {
            head.SetPositionAndRotation(new Vector3(-4,1.7f,3),Quaternion.Euler(15,yaw,0)); recenter.Invoke(intro,null);
            Check(Vector3.Distance(canvas.transform.position,head.position+CprWorldRecenter.Forward(head)*1.5f)<.001f,"tres tarjetas frente a mirada tras cambio de origen "+yaw);
            Check(left.localPosition==leftLocal && right.localPosition==rightLocal,"laterales permanecen junto al centro "+yaw);
        }
        intro.AnchorToStation(root);
        var panelLocal=canvas.transform.localPosition;
        foreach(float yaw in new[]{0,90,180,270})
        {
            head.rotation=Quaternion.Euler(0,yaw,0);
            CprWorldRecenter.Align(root,root,head,.85f,true); recenter.Invoke(intro,null);
            Check(canvas.transform.localPosition==panelLocal && canvas.transform.IsChildOf(root),"selector permanece sobre paciente tras recentrar "+yaw);
            Check(Vector3.Dot(canvas.transform.position-head.position,CprWorldRecenter.Forward(head))>0,"selector de modalidad delante del participante "+yaw);
        }
        var menu=new GameObject("Menú oculto"); var menuCanvas=new GameObject("Canvas").AddComponent<Canvas>(); menuCanvas.transform.SetParent(menu.transform,false);
        menu.AddComponent<CanvasBillboard>(); menu.SetActive(false);
        head.SetPositionAndRotation(Vector3.one,Quaternion.Euler(0,180,0)); menu.SetActive(true);
        // Las comprobaciones por lotes en modo de edición no invocan MonoBehaviour.OnEnable automáticamente.
        typeof(CanvasBillboard).GetMethod("OnEnable",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).Invoke(menu.GetComponent<CanvasBillboard>(),null);
        Check(Vector3.Distance(menuCanvas.transform.position,head.position+CprWorldRecenter.Forward(head)*3)<.001f,"menú que reaparece se coloca delante");
        UnityEngine.Object.DestroyImmediate(menu); UnityEngine.Object.DestroyImmediate(introObject);
        Debug.Log($"PASS: {passed} fixed menu, recenter and safety checks");
    }
}
