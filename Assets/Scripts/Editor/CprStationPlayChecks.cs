using System;
using System.Collections;
using System.IO;
using System.Linq;
using FirstAidVR.Rcp;
using Oculus.Interaction;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

[InitializeOnLoad]
public static class CprStationPlayChecks
{
    private const string Pending="RCP.StationChecks.Pending", PureCount="RCP.StationChecks.PureCount", Exit="RCP.StationChecks.Exit";
    private static IEnumerator routine;
    private static int frame, passed;
    static CprStationPlayChecks()
    {
        EditorApplication.playModeStateChanged+=state=>
        {
            if(state==PlayModeStateChange.EnteredPlayMode && SessionState.GetBool(Pending,false)) EditorApplication.delayCall+=StartTests;
            if(state==PlayModeStateChange.EnteredEditMode && SessionState.GetInt(Exit,-1)>=0)
            {
                int code=SessionState.GetInt(Exit,-1); SessionState.EraseInt(Exit);
                if(Application.isBatchMode) EditorApplication.delayCall+=()=>EditorApplication.Exit(code);
            }
        };
    }
    public static void BuildAndRun()
    {
        CprStationTools.BuildAndCheck();
        SessionState.SetInt(PureCount,CprStationChecks.Passed);
        var tutorial=UnityEngine.Object.FindObjectOfType<CprTutorial>(); tutorial.desktopPreview=true;
        SessionState.SetBool(Pending,true);
        EditorApplication.isPlaying=true;
    }
    private static void StartTests()
    {
        SessionState.SetBool(Pending,false); passed=0; frame=-1; routine=Run(); EditorApplication.update+=Advance;
    }
    private static void Advance()
    {
        if(frame==Time.frameCount) return; frame=Time.frameCount;
        int code=0;
        try
        {
            if(routine.MoveNext()) return;
            File.WriteAllText("Station-checks.txt",$"PASS: {SessionState.GetInt(PureCount,0)} logic checks; {passed} scene, UI and lifecycle checks. No APK generated.\n");
        }
        catch(Exception e) { Debug.LogException(e); File.WriteAllText("Station-checks.txt","FAIL: "+e); code=1; }
        EditorApplication.update-=Advance; (routine as IDisposable)?.Dispose(); routine=null;
        SessionState.SetInt(Exit,code); EditorApplication.isPlaying=false;
    }
    private static void Check(bool ok,string message)
    {
        if(!ok) throw new InvalidOperationException("Estación en Play Mode: "+message);
        passed++;
    }
    private static IEnumerator Run()
    {
        var controller=UnityEngine.Object.FindObjectOfType<CprStationController>();
        for(int i=0;i<120 && controller.Session==null;i++) yield return null;
        Check(controller.Session!=null,"inicialización del coordinador");
        controller.enabled=false; // La entrada de prueba determinista controla ahora esta instancia aislada.
        var view=controller.View; var camera=Camera.main;
        var settings=Resources.Load<CprTrainingSettings>("RCP/TrainingSettings");
        var breathingPanel=controller.GetComponent<CprBreathingPanel>();
        Check(breathingPanel!=null,"panel de respiración creado durante inicialización");
        var breathingSession=CprStationChecks.AtStep(settings,CprPracticeStep.Breathing);
        breathingPanel.Show(breathingSession,true);
        Check(breathingPanel.Visible && breathingPanel.BreathingText.Contains("OBSERVA"),"panel solicita observar al paciente");
        var hudLocal=breathingPanel.PanelTransform.localPosition;
        var cameraPose=new Pose(camera.transform.position,camera.transform.rotation);
        camera.transform.position+=Vector3.right*.3f; camera.transform.Rotate(15,35,0);
        Check(Vector3.Distance(camera.transform.InverseTransformPoint(breathingPanel.PanelTransform.position),hudLocal)<.0001f,"panel acompaña al visor al girar y desplazarse");
        Check(hudLocal.x>0 && hudLocal.y>0 && hudLocal.z>.8f,"panel arriba a la derecha y a distancia cómoda");
        camera.transform.SetPositionAndRotation(cameraPose.position,cameraPose.rotation);
        breathingSession.Tick(.1f,default,default,Pose.identity,Pose.identity,Pose.identity,true,true,true);
        view.Show(breathingSession,0,0,false,true);
        view.Guides(breathingSession,controller.Chest,controller.Shoulder,new Pose(controller.Chest.position+Vector3.up*.035f,controller.Chest.rotation));
        breathingPanel.Show(breathingSession,true);
        Check(breathingPanel.BreathingText.Contains("NO RESPIRA NORMALMENTE"),"muestra estado del paciente virtual");
        for(int i=0;i<300 && (!view.LessonVideo.Player.isPrepared || view.LessonVideo.Player.frame<1);i++) yield return null;
        yield return null; Canvas.ForceUpdateCanvases();
        foreach(var label in breathingPanel.PanelTransform.GetComponentsInChildren<TextMeshProUGUI>())
            Check(!label.isTextOverflowing,"panel de respiración legible: "+label.name);
        var caseVoice=controller.GetComponent<CprStationAudio>(); caseVoice.enabled=true;
        for(int i=0;i<70;i++) {
            breathingSession.Tick(.02f,default,default,Pose.identity,Pose.identity,Pose.identity,true,true,true);
            caseVoice.Tick(breathingSession,.02f,true);
        }
        Check(caseVoice.CaseBeatsPlayed==2,"se reproducen exactamente dos latidos del caso");
        breathingPanel.Show(breathingSession,true); view.Show(breathingSession,0,0,false,true);
        yield return null; Canvas.ForceUpdateCanvases();
        var graph=breathingPanel.PanelTransform.GetComponentInChildren<CprCaseTrace>();
        var graphMesh=graph.canvasRenderer.GetMesh();
        Check(graphMesh!=null && graphMesh.vertexCount>200,"trazo del monitor genera geometría visible");
        Capture(camera,"Breathing-panel-view.png");
        breathingSession.TogglePause();
        for(int i=0;i<20;i++) caseVoice.Tick(breathingSession,.02f,true);
        Check(caseVoice.CaseBeatsPlayed==2,"pausa no repite latidos");
        breathingSession.TogglePause();
        for(int i=0;i<50;i++) {
            breathingSession.Tick(.02f,default,default,Pose.identity,Pose.identity,Pose.identity,true,true,true);
            caseVoice.Tick(breathingSession,.02f,true);
        }
        Check(caseVoice.CaseBeatsPlayed==2,"reanudar no reinicia secuencia de latidos");
        breathingPanel.Show(breathingSession,true);
        yield return null; Canvas.ForceUpdateCanvases(); Capture(camera,"Breathing-panel-flat.png");
        caseVoice.enabled=false;
        breathingPanel.Show(breathingSession,false); Check(!breathingPanel.Visible,"panel oculto sin foco");
        breathingSession.TogglePause(); breathingPanel.Show(breathingSession,true); Check(!breathingPanel.Visible,"panel oculto en pausa");
        breathingSession.TogglePause(); breathingPanel.Show(breathingSession,true); Check(breathingPanel.Visible,"panel vuelve al reanudar");
        breathingPanel.Show(CprStationChecks.AtStep(settings,CprPracticeStep.Help),true); Check(!breathingPanel.Visible,"panel oculto al solicitar ayuda");
        breathingPanel.Show(breathingSession,true); breathingPanel.enabled=false; Check(!breathingPanel.Visible,"panel oculto al desactivar componente");
        breathingPanel.enabled=true; breathingPanel.Show(breathingSession,true); breathingPanel.Hide();
        Check(UnityEngine.Object.FindObjectOfType<CprPatientTransport>()==null,"no se crea traslado, holograma de destino ni guías de arrastre");
        Check(UnityEngine.Object.FindObjectOfType<CprPatientRagdoll>()==null,"no se crea ragdoll ni desplome en modo fijo");
        Check(view.Panels.Length==5,"cuatro pantallas y subidor lateral");
        Check(UnityEngine.Object.FindObjectsOfType<PointableCanvasModule>().Length==1,"módulo de eventos compartido");
        Check(view.GetComponentsInChildren<RayInteractable>().Length==5,"superficie láser en cada panel");
        var patient=controller.StationRoot.Find("Hombre_Paciente");
        var patientLocal=patient.localPosition; var chestLocal=controller.StationRoot.InverseTransformPoint(controller.Chest.position);
        var rotations=view.Panels.Select(p=>p.transform.rotation).ToArray();
        var positions=view.Panels.Select(p=>p.transform.position).ToArray();
        camera.transform.position+=new Vector3(.25f,.2f,.3f); camera.transform.Rotate(0,35,0);
        view.Show(controller.Session,controller.Height,0,false,true);
        for(int i=0;i<5;i++) Check(Vector3.Distance(positions[i],view.Panels[i].transform.position)<.0001f && Quaternion.Angle(rotations[i],view.Panels[i].transform.rotation)<.01f,"panel fijo al mover cabeza "+i);
        controller.SetHeight(.55f);
        Check(Mathf.Abs(controller.Height-.55f)<.001f,"ajuste de altura");
        Check(Vector3.Distance(patient.localPosition,patientLocal)<.0001f,"paciente conserva posición respecto de estación");
        Check(Vector3.Distance(chestLocal,controller.StationRoot.InverseTransformPoint(controller.Chest.position))<.0001f,"ancla de medición acompaña altura");
        Check(Vector3.Distance(view.Panels[0].transform.position,positions[0]+Vector3.up*.55f)<.0001f,"paneles acompañan altura sin mover el rig");
        controller.SetHeight(100); Check(Mathf.Abs(controller.Height-settings.maximumStationLift)<.001f,"límite superior de altura");
        controller.SetHeight(-100); Check(Mathf.Abs(controller.Height)<.001f,"límite inferior de altura");
        camera.transform.position=controller.StationRoot.TransformPoint(new Vector3(0,1,-2.7f));
        camera.transform.LookAt(controller.StationRoot.TransformPoint(new Vector3(.15f,.38f,.12f)));
        yield return null; yield return null;

        Check(!view.Button(CprStationAction.Primary).gameObject.activeInHierarchy,"no hay siguiente ni confirmación central durante el tutorial");
        Check(!view.OptionsOpen,"opciones cerradas al entrar");
        view.ToggleOptions();
        int pointer=600;
        foreach(var action in new[]{CprStationAction.Options,CprStationAction.Pause,CprStationAction.Finish,CprStationAction.Audio,CprStationAction.Mute,CprStationAction.Menu,CprStationAction.HeightUp,CprStationAction.HeightDown,CprStationAction.AlignHeight,CprStationAction.ResetHeight})
        {
            view.Show(controller.Session,controller.Height,0,false,true); Canvas.ForceUpdateCanvases();
            var button=view.Button(action); var ray=button.GetComponentInParent<Canvas>().GetComponentInChildren<RayInteractable>();
            Check(ray.Raycast(new Ray(camera.transform.position,(button.transform.position-camera.transform.position).normalized),out var hit,6,false),"raycast en "+action);
            var pose=new Pose(hit.Point,Quaternion.identity);
            ray.PublishPointerEvent(new PointerEvent(pointer,PointerEventType.Hover,pose)); yield return null; yield return null;
            ray.PublishPointerEvent(new PointerEvent(pointer,PointerEventType.Select,pose)); yield return null; yield return null;
            Check(view.ReadAction(default,default,.02f,default,false)==-1,"mantener no confirma "+action);
            ray.PublishPointerEvent(new PointerEvent(pointer,PointerEventType.Unselect,pose)); yield return null; yield return null;
            Check(view.ReadAction(default,default,.02f,default,false)==(int)action,"clic Meta a botón "+action);
            Check(view.ReadAction(default,default,.02f,default,false)==-1,"sin repetición "+action);
            ray.PublishPointerEvent(new PointerEvent(pointer++,PointerEventType.Unhover,pose)); yield return null;
        }
        view.ToggleOptions();
        // Probar Slider mediante la misma ruta de punteros de ISDK, incluidos los eventos de arrastre.
        var slider=view.HeightSlider; var sliderRay=slider.GetComponentInParent<Canvas>().GetComponentInChildren<RayInteractable>();
        view.Show(controller.Session,controller.Height,0,false,true);
        var sliderRect=(RectTransform)slider.transform;
        var bottom=new Pose(sliderRect.TransformPoint(new Vector3(0,-115,0)),Quaternion.identity);
        sliderRay.PublishPointerEvent(new PointerEvent(pointer,PointerEventType.Hover,bottom)); yield return null; yield return null;
        sliderRay.PublishPointerEvent(new PointerEvent(pointer,PointerEventType.Select,bottom)); yield return null; yield return null;
        var top=new Pose(sliderRect.TransformPoint(new Vector3(0,90,0)),Quaternion.identity);
        for(int i=0;i<6;i++)
        {
            var drag=new Pose(Vector3.Lerp(bottom.position,top.position,(i+1)/6f),Quaternion.identity);
            sliderRay.PublishPointerEvent(new PointerEvent(pointer,PointerEventType.Move,drag)); yield return null; yield return null;
        }
        sliderRay.PublishPointerEvent(new PointerEvent(pointer,PointerEventType.Unselect,top)); yield return null; yield return null;
        sliderRay.PublishPointerEvent(new PointerEvent(pointer++,PointerEventType.Unhover,top)); yield return null;
        Check(controller.Height>.4f,"subidor responde al arrastre por láser; altura="+controller.Height+", slider="+slider.value);
        controller.SetHeight(0);
        view.Show(controller.Session,0,0,false,false);
        Check(view.Panels.SelectMany(c=>c.GetComponentsInChildren<Button>()).All(b=>!b.interactable),"controles bloqueados sin foco");
        view.Show(controller.Session,0,0,false,true); view.BlockInputs();
        Check(view.Panels.SelectMany(c=>c.GetComponentsInChildren<Button>()).All(b=>!b.interactable),"controles bloqueados durante transición");

        // Todas las pantallas deben caber en sus rectángulos fijos, incluido un panel de resultados con muchos errores.
        var states=new[]{new CprPracticeSession(settings), CprStationChecks.Prepared(settings,false)};
        // Probar las acciones exactas de la escena: evidencia de mirada y agarre/transporte del teléfono virtual.
        var actionSession=CprStationChecks.AtStep(settings,CprPracticeStep.Help);
        var actions=controller.Actions;
        var head=new Pose(controller.Chest.position+new Vector3(0,.7f,-.7f),Quaternion.identity);
        var phonePose=new Pose(actions.Phone.position,Quaternion.identity);
        var grip=new CprHandSample(phonePose,false,.9f,true);
        for(int i=0;i<20;i++) actions.Tick(actionSession,head,grip,default,.02f,true);
        Check(actions.HoldingPhone,"cerrar mano toma el teléfono");
        grip.pose.position=head.position+Vector3.right*.16f;
        CprPracticeObservation call=default;
        for(int i=0;i<55;i++) call=actions.Tick(actionSession,head,grip,default,.02f,true);
        Check(call.helpRequested,"llevar teléfono al oído completa solicitud simulada sin botón");
        actions.Tick(actionSession,head,grip,default,.02f,false);
        Check(!actions.HoldingPhone,"pérdida de foco suelta teléfono y cancela gesto");
        var cameraBeforePhone=new Pose(camera.transform.position,camera.transform.rotation);
        for(int side=0;side<2;side++)
        {
            actions.ResetPhone();
            var holding=new CprHandSample(new Pose(actions.Phone.position,Quaternion.LookRotation(Vector3.up,Vector3.forward)),false,.8f,true);
            for(int i=0;i<12;i++) actions.Tick(actionSession,head,side==0?holding:default,side==1?holding:default,.02f,true);
            Check(actions.PhoneHolder==side,"escena acepta el teléfono en ambas manos "+side);
            Check(actions.ShowingPhoneHand,"postura del teléfono visible sin sustituir las muestras "+side);
            camera.transform.position=actions.Phone.position+new Vector3(.10f,.04f,-.32f);
            camera.transform.LookAt(actions.Phone.position,Vector3.up);
            yield return null; yield return null;
            Capture(camera,side==0?"Phone-left-pose.png":"Phone-right-pose.png");
            yield return null;
        }
        camera.transform.SetPositionAndRotation(cameraBeforePhone.position,cameraBeforePhone.rotation);
        actions.Hide();
        var complete=CprStationChecks.Prepared(settings,true);
        var chest=new Pose(Vector3.zero,Quaternion.identity);
        foreach(var attempt in new[]{states[1],complete})
            for(int i=0;i<30;i++) attempt.Tick(.01f,new CprHandSample(new Pose(Vector3.up*.035f,Quaternion.identity)),new CprHandSample(chest),chest,chest,chest,false,true,true);
        for(float t=0;t<5.2f;t+=.01f)
        {
            float depth=.055f*(1-Mathf.Cos(t*Mathf.PI*110/30))*.5f;
            states[1].Tick(.01f,new CprHandSample(new Pose(Vector3.up*(.035f-depth),Quaternion.identity)),
                new CprHandSample(new Pose(Vector3.down*depth,Quaternion.identity)),chest,chest,chest,false,true,true);
        }
        for(float t=0;t<19;t+=.01f)
        {
            float depth=.075f*(1-Mathf.Cos(t*Mathf.PI*140/30))*.5f;
            var l=new CprHandSample(new Pose(Vector3.up*(.035f-depth),Quaternion.identity));
            var r=new CprHandSample(new Pose(Vector3.down*depth,Quaternion.identity));
            complete.Tick(.01f,l,r,chest,chest,chest,false,true,true);
        }
        complete.Finish();
        complete.ReportSaved(CprAttemptReport.Save(complete,Path.GetFullPath("Logs/SimulatedReportChecks"),
            "PRUEBA AUTOMATIZADA / DATOS SIMULADOS","poses sintéticas de prueba"));
        Check(complete.Completed && complete.Count==settings.practiceCompressions && complete.Excessive>0 && complete.Fast>0,"resultado completo con errores observados");
        Check(states[1].Count>=8 && states[1].Score.HasValue,"indicadores en vivo con datos medidos");
        var allSteps=System.Enum.GetValues(typeof(CprPracticeStep)).Cast<CprPracticeStep>().Where(step=>step>=CprPracticeStep.Safety && step<=CprPracticeStep.Placement).Select(step=>CprStationChecks.AtStep(settings,step));
        foreach(var s in states.Concat(allSteps).Concat(new[]{complete}))
        {
            view.Show(s,0,.86f,false,true); view.Guides(s,controller.Chest,controller.Shoulder,new Pose(controller.Chest.position+Vector3.up*.035f,controller.Chest.rotation));
            if(!s.Closed)
            {
                for(int i=0;i<300 && (!view.LessonVideo.Player.isPrepared || view.LessonVideo.Player.frame<1);i++) { view.Show(s,0,.86f,false,true); yield return null; }
                Check(view.LessonVideo.Player.isPrepared && view.LessonVideo.Player.frame>0,"demostración reproduce en "+s.Step);
                Check(!view.Button(CprStationAction.Primary).gameObject.activeInHierarchy,"ningún paso requiere siguiente: "+s.Step);
            }
            Canvas.ForceUpdateCanvases();
            foreach(var label in view.Panels.SelectMany(c=>c.GetComponentsInChildren<TextMeshProUGUI>()))
            {
                label.ForceMeshUpdate();
                Check(!label.isTextOverflowing,"texto sin recorte: "+label.name+" en "+s.Step);
            }
            string name=s.Closed?"Station-results.png":s.Step==CprPracticeStep.Compressions?"Station-compressions.png":s.Step==CprPracticeStep.Welcome?"Station-overview.png":"Station-step-"+(int)s.Step+".png";
            Capture(camera,name);
        }
        var central=view.Panels[1].transform;
        var originalCamera=new Pose(camera.transform.position,camera.transform.rotation);
        camera.transform.position=central.position-central.forward*1.1f;
        camera.transform.LookAt(central.position,central.up);
        yield return null; yield return null; Canvas.ForceUpdateCanvases();
        Capture(camera,"Station-results-points-close.png");
        view.ToggleResults(); view.Show(complete,0,0,false,true);
        yield return null; yield return null; Canvas.ForceUpdateCanvases();
        Check(view.Button(CprStationAction.Alternative).gameObject.activeInHierarchy,"botón para consultar incidencias");
        Check(central.GetComponentsInChildren<TextMeshProUGUI>().Any(t=>t.text.Contains("Separaciones entre manos")),"resultado contiene contador de separaciones");
        foreach(var label in central.GetComponentsInChildren<TextMeshProUGUI>())
        { Check(!label.isTextOverflowing,"incidencias sin recorte: "+label.name); }
        view.Button(CprStationAction.Alternative).onClick.Invoke();
        Check(view.ReadAction(default,default,.02f,new Ray(),false)==(int)CprStationAction.Alternative,"acción de alternar resultados conectada");
        yield return null; yield return null; Canvas.ForceUpdateCanvases();
        Capture(camera,"Station-results-incidents-close.png");
        view.ToggleResults(); view.Show(complete,0,0,false,true);
        Check(central.GetComponentsInChildren<TextMeshProUGUI>().Any(t=>t.text.Contains("Colocación: −2")),"regresa al desglose de puntos");
        camera.transform.SetPositionAndRotation(originalCamera.position,originalCamera.rotation);
        var feedbackSession=CprStationChecks.Prepared(settings);
        var zeroChest=new Pose(Vector3.zero,Quaternion.identity);
        feedbackSession.Detector.Tick(.01f,1,new CprHandSample(new Pose(Vector3.down*.005f,Quaternion.identity)),default,zeroChest,true,true);
        float beatInterval=60/110f;
        foreach(bool onTime in new[]{true,false})
        {
            feedbackSession.Pacing.Reset(); feedbackSession.Pacing.Tick(.01f,0);
            float pressAt=beatInterval*.8f+(onTime?.04f:-.16f);
            feedbackSession.Pacing.Tick(pressAt,pressAt); feedbackSession.Pacing.ObservePress(pressAt);
            view.Show(feedbackSession,0,feedbackSession.Pacing.Phase,false,true);
            Check(view.Panels[3].transform.Find("Medición").gameObject.activeSelf,"indicador visible durante recuperación de referencia");
            var response=view.Panels[2].transform.Find("Respuesta de pulsación").GetComponent<Graphic>().color;
            Check(onTime?response.g>response.r:response.r>response.g,"verde al acertar y rojo al adelantarse");
            camera.transform.position=controller.StationRoot.TransformPoint(new Vector3(.63f,.75f,-.55f));
            camera.transform.LookAt(view.Panels[2].transform.position+Vector3.right*.15f,Vector3.up);
            yield return null; yield return null; Canvas.ForceUpdateCanvases();
            Capture(camera,onTime?"Beat-on-time.png":"Beat-early.png");
        }
        for(int i=0;i<50;i++) feedbackSession.Detector.Tick(.01f,2+i*.01f,default,default,zeroChest,true,true);
        view.Show(feedbackSession,0,0,false,true);
        Check(view.Panels[3].transform.Find("Medición").gameObject.activeSelf,"indicador sigue visible en gris sin seguimiento");
        Check(view.Panels[3].GetComponentsInChildren<TextMeshProUGUI>().Any(t=>t.text.Contains("Última lectura")),"lectura retenida no se presenta como actual");
        camera.transform.SetPositionAndRotation(originalCamera.position,originalCamera.rotation);
        controller.Act(CprStationAction.Finish);
        Check(controller.Session.Closed,"cerrar desde controlador");
        controller.Act(CprStationAction.Primary);
        Check(controller.Session.Step==CprPracticeStep.Welcome && controller.Session.Count==0,"repetición desde controlador limpia sesión");
        view.Show(controller.Session,0,0,false,true);
        for(int i=0;i<300 && (!view.LessonVideo.Player.isPrepared || view.LessonVideo.Player.frame<1);i++)
        { view.Show(controller.Session,0,0,false,true); yield return null; }
        var video=view.LessonVideo.Player;
        Check(video.isPrepared && video.isPlaying && video.frame>0,"vídeo vuelve a reproducir después de resultados y repetición");
        view.Show(controller.Session,0,0,false,false);
        yield return null; yield return null;
        long pausedFrame=video.frame;
        float deadline=Time.realtimeSinceStartup+.25f;
        while(Time.realtimeSinceStartup<deadline) yield return null;
        Check(!video.isPlaying && video.frame==pausedFrame,"pérdida de foco congela el vídeo");
        view.Show(controller.Session,0,0,false,true);
        deadline=Time.realtimeSinceStartup+.4f;
        while(Time.realtimeSinceStartup<deadline) yield return null;
        Check(video.isPlaying && video.frame>pausedFrame,"recuperar foco continúa fotogramas");
        controller.Session.TogglePause(); view.Show(controller.Session,0,0,false,true);
        Check(!video.isPlaying,"pausa manual detiene la demostración");
        foreach(var skin in patient.GetComponentsInChildren<SkinnedMeshRenderer>())
            Check(skin.sharedMaterials.All(m=>m!=null && m.shader!=null && m.shader.name!="Hidden/InternalErrorShader"),"materiales válidos "+skin.name);
    }
    private static void Capture(Camera camera,string name)
    {
        var rt=new RenderTexture(2560,1440,24); var tex=new Texture2D(2560,1440,TextureFormat.RGB24,false);
        var old=camera.targetTexture; var active=RenderTexture.active;
        try { camera.targetTexture=rt; camera.Render(); RenderTexture.active=rt; tex.ReadPixels(new Rect(0,0,2560,1440),0,0); tex.Apply(); File.WriteAllBytes(name,tex.EncodeToPNG()); }
        finally { camera.targetTexture=old; RenderTexture.active=active; UnityEngine.Object.DestroyImmediate(tex); UnityEngine.Object.DestroyImmediate(rt); }
    }
}
