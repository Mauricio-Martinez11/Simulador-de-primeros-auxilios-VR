using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace FirstAidVR.Rcp
{
    public sealed class CprStationController : MonoBehaviour
    {
        [SerializeField] private Transform stationRoot, chestReference, shoulderReference;
        [SerializeField] private CprStationView view;
        [SerializeField] private CprStationPatient patientView;
        [SerializeField] private CprScenarioActions scenarioActions;
        [SerializeField] private float originalHeight;
        private CprTrainingSettings settings;
        private CprTrackedHands input;
        private CprStationAudio audioFeedback;
        private CprLiveFeedback liveFeedback;
        private CprShoulderGuideOverride shoulderOverride;
        private CprBreathingPanel breathingPanel;
        private CprStationPreparation preparation;
        private CprPracticeSession session;
        private CprTutorial tutorial;
        private Camera viewCamera;
        private Transform movedRig;
        private Pose originalRig;
        private readonly List<Behaviour> disabledCameras = new List<Behaviour>();
        private bool started, initialized;
        private float actionCooldown, visualTravel;
        private bool observedHands, observedControllers, upperController;
        private string savedClosedAttempt;
        public CprPracticeSession Session => session;
        public Transform StationRoot => stationRoot;
        public CprStationView View => view;
        public CprScenarioActions Actions => scenarioActions;
        public Pose Chest => new Pose(chestReference.position,chestReference.rotation);
        public Pose Shoulder => new Pose(shoulderReference.position,shoulderReference.rotation);
        public float Height => stationRoot==null?0:stationRoot.position.y-originalHeight;

        public void Begin(CprTutorial owner)
        {
            if(started) return;
            started=true; tutorial=owner; StartCoroutine(Initialize());
        }
        public void DisableFixedLayout()
        {
            if(view!=null) view.HideStation();
            if(patientView!=null) patientView.RestorePose();
            if(scenarioActions!=null) scenarioActions.Hide();
            if(breathingPanel!=null) breathingPanel.Hide();
        }

        public void BuildLayout(CprTrainingSettings config,Transform patient,Transform manualChest=null,Transform manualShoulder=null)
        {
            var side=Vector3.ProjectOnPlane(patient.right,Vector3.up).normalized;
            var rotation=Quaternion.LookRotation(-side,Vector3.up);
            var spine=FindBone(patient,"CC_Base_Spine02");
            var shoulderBone=FindBone(patient,"CC_Base_R_Upperarm");
            if(spine==null && manualChest==null) throw new System.InvalidOperationException("El paciente no tiene una referencia de pecho.");
            var longitudinal=Vector3.ProjectOnPlane(patient.up,Vector3.up).normalized;
            var chestPosition=manualChest!=null?manualChest.position:spine.position+side*config.chestOffset.x+Vector3.up*config.chestOffset.y-longitudinal*config.chestOffset.z;
            shoulderOverride=GetComponentInChildren<CprShoulderGuideOverride>(true);
            if(shoulderOverride==null && stationRoot!=null) shoulderOverride=stationRoot.GetComponentInChildren<CprShoulderGuideOverride>(true);
            if(shoulderOverride!=null) manualShoulder=shoulderOverride.transform;
            var shoulderPosition=manualShoulder!=null?manualShoulder.position:shoulderBone.position+config.shoulderOffset;
            if(stationRoot!=null && view!=null)
            {
                // Keep the authored patient-to-screen layout. Only anatomical references
                // are recalculated when the patient moves within the saved station.
                chestReference.SetPositionAndRotation(chestPosition,rotation);
                shoulderReference.SetPositionAndRotation(shoulderPosition,shoulderOverride!=null?shoulderOverride.transform.rotation:rotation);
                originalHeight=stationRoot.position.y;
                view.Build(config); patientView.Prepare(patient,config); BuildActions(config);
                return;
            }
            stationRoot=new GameObject("RCP_Estacion_Fija").transform;
            stationRoot.SetPositionAndRotation(chestPosition,rotation); originalHeight=chestPosition.y;
            patient.SetParent(stationRoot,true);
            chestReference=new GameObject("Referencia de pecho (reposo)").transform; chestReference.SetParent(stationRoot,false);
            shoulderReference=new GameObject("Referencia de hombro").transform; shoulderReference.SetParent(stationRoot,false);
            shoulderReference.SetPositionAndRotation(shoulderPosition,shoulderOverride!=null?shoulderOverride.transform.rotation:rotation);
            view=stationRoot.gameObject.AddComponent<CprStationView>(); view.Build(config);
            patientView=stationRoot.gameObject.AddComponent<CprStationPatient>(); patientView.Prepare(patient,config);
            BuildActions(config);
        }
        private void BuildActions(CprTrainingSettings config)
        {
            if(scenarioActions==null) scenarioActions=stationRoot.GetComponent<CprScenarioActions>();
            if(scenarioActions==null) scenarioActions=stationRoot.gameObject.AddComponent<CprScenarioActions>();
            scenarioActions.Build(config);
        }

        private IEnumerator Initialize()
        {
            if(tutorial.settings==null || tutorial.patient==null)
            { Debug.LogError("RCP: falta la configuración o el paciente de la estación.",this); yield break; }
            // Freeze all scoring thresholds for this attempt. Inspector edits cannot alter its denominators.
            settings=Instantiate(tutorial.settings);
            BuildLayout(settings,tutorial.patient,tutorial.chestAnchor,tutorial.shoulderAnchor);
            patientView.PrepareContact(tutorial.patient,Chest,settings);
            var safeShoulder=shoulderOverride!=null
                ?patientView.AuthoredShoulderSurface(tutorial.patient,shoulderOverride.transform,shoulderOverride.GuideMesh)
                :patientView.ShoulderSurface(tutorial.patient,Shoulder,settings.leftHand);
            shoulderReference.SetPositionAndRotation(safeShoulder.position,safeShoulder.rotation);
            originalHeight=stationRoot.position.y;
            input=GetComponent<CprTrackedHands>(); if(input==null) input=gameObject.AddComponent<CprTrackedHands>(); input.Discover();
            if(!tutorial.desktopPreview && input.Rig==null && tutorial.xrCorePrefab!=null)
            { Instantiate(tutorial.xrCorePrefab); yield return null; input.Discover(); }
            if(!tutorial.desktopPreview && input.Rig!=null)
            {
                float deadline=Time.realtimeSinceStartup+2;
                while(!OVRManager.isHmdPresent && Time.realtimeSinceStartup<deadline) yield return null;
            }
            viewCamera=!tutorial.desktopPreview && input.Rig!=null?input.Rig.centerEyeAnchor.GetComponent<Camera>():Camera.main;
            if(viewCamera==null) { Debug.LogError("RCP: no hay cámara para la estación.",this); yield break; }
            if(!tutorial.desktopPreview && input.Rig!=null)
            {
                foreach(var camera in FindObjectsOfType<Camera>())
                    if(camera!=viewCamera && camera.gameObject.scene==gameObject.scene && !camera.transform.IsChildOf(input.Rig.transform))
                    {
                        if(camera.enabled) { disabledCameras.Add(camera); camera.enabled=false; }
                        var listener=camera.GetComponent<AudioListener>();
                        if(listener!=null && listener.enabled) { disabledCameras.Add(listener); listener.enabled=false; }
                    }
                movedRig=input.Rig.transform; originalRig=new Pose(movedRig.position,movedRig.rotation);
                var facing=Vector3.ProjectOnPlane(viewCamera.transform.forward,Vector3.up);
                if(facing.sqrMagnitude>.01f) movedRig.RotateAround(viewCamera.transform.position,Vector3.up,Vector3.SignedAngle(facing,stationRoot.forward,Vector3.up));
                var target=stationRoot.position-stationRoot.forward*.85f;
                var move=target-viewCamera.transform.position; move.y=0; movedRig.position+=move;
            }
            else
            {
                viewCamera.transform.position=stationRoot.TransformPoint(new Vector3(0,1.0f,-2.7f));
                viewCamera.transform.LookAt(stationRoot.TransformPoint(new Vector3(.15f,.38f,.12f)));
                viewCamera.nearClipPlane=.03f;
            }
            view.Bind(settings,viewCamera); view.ResponseHand=shoulderOverride!=null?shoulderOverride.GuideMesh:patientView.ShoulderHand; view.HeightRequested+=SetHeight;
            if(shoulderOverride!=null)
            {
                var scale=shoulderOverride.transform.lossyScale; var parentScale=view.transform.lossyScale;
                view.ResponseHandScale=new Vector3(scale.x/parentScale.x,scale.y/parentScale.y,scale.z/parentScale.z);
            }
            audioFeedback=gameObject.AddComponent<CprStationAudio>(); audioFeedback.Initialize(settings);
            breathingPanel=gameObject.AddComponent<CprBreathingPanel>(); breathingPanel.Bind(viewCamera);
            breathingPanel.PanelTransform.SetParent(stationRoot,true);
            preparation=gameObject.AddComponent<CprStationPreparation>();
            yield return preparation.Run(viewCamera,stationRoot,tutorial.patient,view,input,settings,
                () => patientView.ContactPose(Chest,0));
            session=new CprPracticeSession(settings);
            liveFeedback=gameObject.AddComponent<CprLiveFeedback>(); liveFeedback.Build(stationRoot,viewCamera);
            initialized=true;
        }

        private void OnEnable() { CprWorldRecenter.Recentered+=RecenterStation; }
        public void RecenterStation()
        {
            if(viewCamera==null || stationRoot==null) return;
            CprWorldRecenter.Align(stationRoot,stationRoot,viewCamera.transform,.85f,true);
            scenarioActions.ResetSafetyReference();
            if(session!=null && !session.Closed) session.WorldRecentered();
            view.ClearInput(); input.RestoreVisuals(); visualTravel=0;
            if(initialized && preparation.Mixed.UsingSupport && !session.Closed) StartCoroutine(Realign());
        }
        private void Update()
        {
            if(!initialized) return;
            bool focus=tutorial.desktopPreview?Application.isFocused:input.Rig!=null?OVRManager.hasVrFocus:Application.isFocused;
            var left=input.Read(true,settings); var right=input.Read(false,settings);
            if (left.valid || right.valid)
            {
                observedControllers |= left.valid && left.controller || right.valid && right.controller;
                observedHands |= left.valid && !left.controller || right.valid && !right.controller;
            }
            if(left.valid) upperController=left.controller;
            var chest=Chest; var shoulder=Shoulder;
            var upper=new Pose(chest.position+chest.rotation*Vector3.up*settings.stackedHandHeight,chest.rotation);
            var ray=new Ray(viewCamera.transform.position,viewCamera.transform.forward);
#if UNITY_EDITOR
            if(tutorial.desktopPreview && Input.mousePresent) ray=viewCamera.ScreenPointToRay(Input.mousePosition);
#endif
            bool looking=Vector3.Angle(ray.direction,chest.position-ray.origin)<22 && Vector3.Distance(ray.origin,chest.position)<2.5f;
            bool positioned=ParticipantInPosition(viewCamera.transform.position,chest);
            audioFeedback.Prepare(session,Time.unscaledDeltaTime,focus);
            var before=session.Step; bool passed=session.StepPassed;
            var observation=scenarioActions.Tick(session,new Pose(viewCamera.transform.position,viewCamera.transform.rotation),left,right,Time.unscaledDeltaTime,focus);
            session.Tick(Time.unscaledDeltaTime,left,right,shoulder,chest,upper,looking,positioned,focus,observation);
            if (session.Closed && savedClosedAttempt != session.AttemptId) SaveAttempt();
            if(focus && session.Step==CprPracticeStep.Placement)
                input.LogPlacement(left,right,chest,session.Placement);
            if(session.Step!=before || session.StepPassed!=passed) { actionCooldown=.45f; view.ClearInput(); }
            audioFeedback.Tick(session,Time.unscaledDeltaTime,focus);
            liveFeedback.Tick(session,focus);
            view.NarrationFallbackText=audioFeedback.MissingAudio?audioFeedback.CurrentText:null;
            view.Show(session,Height,audioFeedback.BeatPhase,audioFeedback.Muted,focus);
            view.Button(CprStationAction.Realign).gameObject.SetActive(preparation.Mixed.Active);
            breathingPanel.SetChest(patientView.ContactPose(chest,0));
            breathingPanel.Show(session,focus,looking);
            if(actionCooldown>0) view.BlockInputs();
            actionCooldown-=Time.unscaledDeltaTime;
            if(focus && actionCooldown<=0)
            {
                int action=view.ReadAction(left,right,Time.unscaledDeltaTime,ray,tutorial.desktopPreview);
                if(action>=0) { Act((CprStationAction)action); actionCooldown=.35f; }
            }
            bool compressing=focus && !session.Paused && session.Step==CprPracticeStep.Compressions && session.Detector.LocationCorrect;
            bool latched=session.Step==CprPracticeStep.Compressions && session.Placement.StackLatched;
            if (!latched) visualTravel=0;
            else if (compressing) visualTravel=session.Detector.Depth;
            patientView.SetCompression(visualTravel,chest.rotation*Vector3.up);
            var responseOffset=chest.rotation*Vector3.right*(session.ShoulderContact.Animation*.008f);
            patientView.SetShoulderMotion(responseOffset);
            shoulder.position+=responseOffset;
            view.Guides(session,chest,shoulder,upper,upperController,patientView.ContactPose(chest,visualTravel));
            // Contact animation never feeds the measurement reference or the detector.
            bool hide=focus && !session.Paused && !session.Explaining && (latched || session.Step==CprPracticeStep.Placement && session.Placement.LowerLatched);
            bool responseGrip=focus && !session.Paused && session.ResponseTouching;
            bool phoneGrip=focus && !session.Paused && session.Step==CprPracticeStep.Help && !session.StepPassed && scenarioActions.ShowingPhoneHand;
            input.HideTrackedVisual(true,(hide && session.Placement.StackLatched || responseGrip && session.ShoulderContact.Hand==0 || phoneGrip && scenarioActions.PhoneHolder==0) && !left.controller);
            input.HideTrackedVisual(false,(hide || responseGrip && session.ShoulderContact.Hand==1 || phoneGrip && scenarioActions.PhoneHolder==1) && !right.controller);
        }

        public static bool ParticipantInPosition(Vector3 head,Pose chest)
        {
            var local=Quaternion.Inverse(chest.rotation)*(head-chest.position);
            return local.y>.22f && local.y<1.85f && local.z<.12f && local.z> -1.3f && Mathf.Abs(local.x)<.85f;
        }

        public void SetHeight(float height)
        {
            if(!initialized || !session.CanAdjustHeight) return;
            height=Mathf.Clamp(height,0,settings.maximumStationLift);
            if(Mathf.Abs(height-Height)<.001f) return;
            var position=stationRoot.position; position.y=originalHeight+height; stationRoot.position=position;
            session.HeightChanged(); view.ClearInput(); input.RestoreVisuals();
        }

        public void Act(CprStationAction action)
        {
            if(!initialized) return;
            switch(action)
            {
                case CprStationAction.Primary:
                    if(session.Closed)
                    {
                        SaveAttempt(); session=new CprPracticeSession(settings); observedHands=observedControllers=false;
                        visualTravel=0; input.RestoreVisuals(); patientView.SetCompression(0,Vector3.up);
                    }
                    break;
                case CprStationAction.Options: view.ToggleOptions(); break;
                case CprStationAction.Alternative: if(session.Closed) view.ToggleResults(); break;
                case CprStationAction.Pause: session.TogglePause(); input.RestoreVisuals(); break;
                case CprStationAction.Finish: session.Finish(); SaveAttempt(); input.RestoreVisuals(); break;
                case CprStationAction.Menu: session.Finish(); SaveAttempt(); input.RestoreVisuals(); SceneManager.LoadScene(settings.menuScene); break;
                case CprStationAction.Realign: if(preparation.Mixed.Active && !session.Closed) StartCoroutine(Realign()); break;
                case CprStationAction.Audio: audioFeedback.Repeat(session.Step); break;
                case CprStationAction.Mute: audioFeedback.Toggle(session.Step); break;
                case CprStationAction.HeightUp: SetHeight(Height+.05f); break;
                case CprStationAction.HeightDown: SetHeight(Height-.05f); break;
                case CprStationAction.AlignHeight: SetHeight(viewCamera.transform.position.y-.72f-originalHeight); break;
                case CprStationAction.ResetHeight: SetHeight(0); break;
            }
            view.ClearInput();
        }
        private IEnumerator Realign()
        {
            initialized=false;
            if(!session.Paused) session.TogglePause();
            audioFeedback.Prepare(session,0,false);
            breathingPanel.Hide();
            input.RestoreVisuals();
            yield return preparation.Realign();
            session.HeightChanged();
            if(session.Paused) session.TogglePause();
            initialized=true;
        }
        public static Transform FindBone(Transform root,string name)
        {
            foreach(var bone in root.GetComponentsInChildren<Transform>(true)) if(bone.name==name) return bone;
            return null;
        }
        private void OnDisable()
        {
            CprWorldRecenter.Recentered-=RecenterStation;
            if(session!=null && session.TotalSeconds>0) SaveAttempt();
            if(input!=null) input.RestoreVisuals();
            if(view!=null) view.ClearInput();
            if(scenarioActions!=null) scenarioActions.Hide();
            if(patientView!=null) patientView.SetCompression(0,Vector3.up);
            if(audioFeedback!=null) audioFeedback.enabled=false;
            if(breathingPanel!=null) breathingPanel.Hide();
            if(preparation!=null) preparation.enabled=false;
            if(movedRig!=null) { movedRig.SetPositionAndRotation(originalRig.position,originalRig.rotation); movedRig=null; }
            foreach(var component in disabledCameras) if(component!=null) component.enabled=true;
            disabledCameras.Clear();
        }
        private void OnDestroy()
        {
            if(session!=null && session.TotalSeconds>0) { session.Finish(); SaveAttempt(); }
            if(view!=null) view.HeightRequested-=SetHeight;
            if(settings!=null) Destroy(settings);
        }
        private void SaveAttempt()
        {
            if (session==null || session.TotalSeconds<=0 || savedClosedAttempt==session.AttemptId) return;
            try
            {
                string mode=observedHands && observedControllers?"manos y mandos":observedHands?"manos":observedControllers?"mandos":"sin muestras";
                string directory=Application.isEditor && Application.isBatchMode
                    ? System.IO.Path.Combine(Application.dataPath,"../Logs/AutomatedPracticeReports")
                    : System.IO.Path.Combine(Application.persistentDataPath,"RegistrosRCP");
                string path=CprAttemptReport.Save(session,directory,
                    Application.isEditor?"Unity Editor / simulación":Application.platform.ToString(),mode);
                session.ReportSaved(path);
                if(session.Closed) savedClosedAttempt=session.AttemptId;
                Debug.Log("[RCP registro] "+path);
            }
            catch(System.Exception e)
            {
                session.ReportFailed(e.Message);
                // Do not retry every frame on a full or inaccessible device.
                if(session.Closed) savedClosedAttempt=session.AttemptId;
                Debug.LogError("No se pudo guardar el registro RCP: "+e.Message);
            }
        }
        private void OnApplicationPause(bool paused) { if(paused) SaveAttempt(); }
    }
}
