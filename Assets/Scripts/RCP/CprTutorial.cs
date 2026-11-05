using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.XR;

namespace FirstAidVR.Rcp
{
    public sealed class CprTutorial : MonoBehaviour
    {
        public CprTrainingSettings settings;
        public Transform patient;
        public GameObject xrCorePrefab;
        [Tooltip("Solo para ver el panel con ratón, sin visor ni Meta XR Simulator. Desactivar para probar XR.")]
        public bool desktopPreview;
        [Tooltip("Opcional: anclas manuales para otros modelos de paciente.")]
        public Transform chestAnchor, shoulderAnchor;
        [Tooltip("Destino y agarres opcionales. Si están vacíos se usan el desplazamiento de configuración y los huesos del paciente.")]
        public Transform patientDestination, leftArmAnchor, rightArmAnchor;
        private CprTrackedHands input;
        private CprTutorialView view;
        private CprFlow flow;
        private CprPatientTransport transport;
        private Transform chestBone, shoulderBone, headBone;
        private Vector3 side, rigPosition;
        private Quaternion rigRotation;
        private Transform movedRig;
        private Camera cameraForView;
        private bool initialized;
        private float buttonCooldown;
        private float panelHeight;
        private Pose chest, shoulder, upper;
        private readonly List<Behaviour> temporarilyDisabled = new List<Behaviour>();
        public CprFlow Flow => flow;
        public Pose ChestPose => chest;
        public Pose ShoulderPose => shoulder;
        public Pose UpperPose => upper;
        public CprPatientTransport Transport => transport;

        private IEnumerator Start()
        {
            if (settings == null) settings = Resources.Load<CprTrainingSettings>("RCP/TrainingSettings");
            if (patient == null) patient = GameObject.Find("Hombre_Paciente")?.transform;
            if (settings != null && settings.stationaryPractice)
            {
                var station = GetComponent<CprStationController>();
                if (station == null) station = gameObject.AddComponent<CprStationController>();
                station.Begin(this);
                yield break;
            }
            GetComponent<CprStationController>()?.DisableFixedLayout();
            if (settings == null || patient == null || settings.leftHand == null || settings.rightHand == null || settings.hologramMaterial == null)
            { Debug.LogError("RCP: faltan paciente o recursos en Resources/RCP/TrainingSettings.",this); enabled=false; yield break; }
            input = gameObject.AddComponent<CprTrackedHands>(); input.Discover();
            // OpenXR can start its session after Start(). The rig must exist before the first tracked frame.
            // XRSettings.isDeviceActive at this point used to incorrectly select the static desktop camera.
            if (!desktopPreview && input.Rig == null && xrCorePrefab != null)
            { Instantiate(xrCorePrefab); yield return null; input.Discover(); }
            yield return null;
            if (!desktopPreview && input.Rig != null)
            {
                float deadline = Time.realtimeSinceStartup + 2;
                while (!OVRManager.isHmdPresent && Time.realtimeSinceStartup < deadline) yield return null;
                yield return null;
            }
            chestBone = Bone("CC_Base_Spine02"); headBone = Bone("CC_Base_Head");
            if (chestAnchor == null && (chestBone == null || headBone == null))
            { Debug.LogError("RCP: asigna Chest Anchor y Shoulder Anchor para este paciente.",this); enabled=false; yield break; }
            side = patient.right;
            shoulderBone = Bone("CC_Base_R_Upperarm");
            UpdateAnchors();
            cameraForView = input.Rig != null ? input.Rig.centerEyeAnchor.GetComponent<Camera>() : Camera.main;
            if (cameraForView == null) { Debug.LogError("RCP: no hay cámara disponible.",this); enabled=false; yield break; }
            if (input.Rig != null)
            {
                foreach (var camera in FindObjectsOfType<Camera>())
                    if (camera.gameObject.scene == gameObject.scene && camera != cameraForView && !camera.transform.IsChildOf(input.Rig.transform))
                    {
                        if(camera.enabled) { temporarilyDisabled.Add(camera); camera.enabled=false; }
                        var listener=camera.GetComponent<AudioListener>();
                        if(listener!=null && listener.enabled) { temporarilyDisabled.Add(listener); listener.enabled=false; }
                    }
                movedRig=input.Rig.transform; rigPosition=movedRig.position; rigRotation=movedRig.rotation;
                var facing = Vector3.ProjectOnPlane(cameraForView.transform.forward,Vector3.up);
                if (facing.sqrMagnitude>.01f)
                    movedRig.RotateAround(cameraForView.transform.position,Vector3.up,Vector3.SignedAngle(facing,-side,Vector3.up));
                var translation = chest.position+side*.72f-cameraForView.transform.position; translation.y=0;
                movedRig.position+=translation;
            }
            else
            {
                cameraForView.transform.position=chest.position+side*1.25f+Vector3.up*.9f;
                cameraForView.transform.LookAt(chest.position+Vector3.up*.3f);
                cameraForView.nearClipPlane=.03f;
            }
            view=gameObject.AddComponent<CprTutorialView>(); view.Build(settings,cameraForView);
            view.PanelPosition=chest.position-side*.50f+Vector3.up*.86f;
            panelHeight=view.PanelPosition.y;
            flow=new CprFlow(settings);
            transport=gameObject.AddComponent<CprPatientTransport>();
            transport.Initialize(settings,patient,patientDestination,leftArmAnchor,rightArmAnchor);
            transport.onGrab.AddListener(OnPatientGrabbed);
            transport.onPlaced.AddListener(OnPatientPlaced);
            initialized=true;
        }

        private Transform Bone(string name)
        {
            foreach(var bone in patient.GetComponentsInChildren<Transform>()) if(bone.name==name) return bone;
            return null;
        }
        private void UpdateAnchors()
        {
            var floorSide=Vector3.ProjectOnPlane(patient.right,Vector3.up);
            if(floorSide.sqrMagnitude>.01f) side=floorSide.normalized;
            var orientation=Quaternion.LookRotation(-side,Vector3.up);
            // Offset is in floor/world axes, with X/Z following the patient's longitudinal direction.
            var forward=Vector3.ProjectOnPlane(patient.up,Vector3.up).normalized;
            var offset = side*settings.chestOffset.x + Vector3.up*settings.chestOffset.y - forward*settings.chestOffset.z;
            chest=chestAnchor!=null ? new Pose(chestAnchor.position,chestAnchor.rotation) : new Pose(chestBone.position+offset,orientation);
            shoulder=shoulderAnchor!=null ? new Pose(shoulderAnchor.position,shoulderAnchor.rotation)
                : new Pose((shoulderBone!=null?shoulderBone.position:chest.position)+settings.shoulderOffset,orientation);
            upper=new Pose(chest.position+chest.rotation*Vector3.up*settings.stackedHandHeight,chest.rotation);
        }

        private void Update()
        {
            if (!initialized) return;
            var left=input.Read(true,settings); var right=input.Read(false,settings);
            bool hasInputFocus = input.Rig != null ? OVRManager.hasVrFocus : Application.isFocused;
            if (hasInputFocus) transport.Tick(Time.unscaledDeltaTime,left,right,flow.Step!=CprStep.Safety,flow.Step==CprStep.MovePatient);
            else { transport.Suspend(); flow.Suspend(); view.ResetButtons(); left=right=default; }
            UpdateAnchors();
            var panelTarget=chest.position-side*.50f; panelTarget.y=panelHeight;
            view.PanelPosition=Vector3.Lerp(view.PanelPosition,panelTarget,
                1-Mathf.Exp(-5*Time.unscaledDeltaTime));
            var gaze=new Ray(cameraForView.transform.position,cameraForView.transform.forward);
#if UNITY_EDITOR
            // Mouse points the observation ray in the desktop preview; it never synthesizes tracked hands.
            if (input.Rig == null && Input.mousePresent) gaze=cameraForView.ScreenPointToRay(Input.mousePosition);
#endif
            bool looking=Vector3.Angle(gaze.direction,chest.position-gaze.origin)<22 && Vector3.Distance(gaze.origin,chest.position)<2.5f;
            var before=flow.Step;
            // The standalone simulator owns keyboard focus while Unity renders in the background.
            if (hasInputFocus) flow.Tick(Time.unscaledDeltaTime,left,right,shoulder,chest,upper,looking);
            if (before!=flow.Step)
            {
                buttonCooldown=.85f;
                if((before==CprStep.LowerHand && flow.Step==CprStep.UpperHand) || flow.Step==CprStep.Ready)
                { input.Pulse(flow.Step==CprStep.Ready); view.PlayFeedback(flow.Step==CprStep.Ready); }
            }
            buttonCooldown-=Time.unscaledDeltaTime;
            view.Show(flow,Status(left,right,looking));
            view.SetButtonsEnabled(buttonCooldown<=0 && hasInputFocus);
            if(buttonCooldown<=0 && hasInputFocus)
            {
                int button=view.UpdateButtons(gaze,Time.unscaledDeltaTime,settings.buttonDwell,left,right,desktopPreview);
                if(button==0)
                {
                    input.RestoreVisuals();
                    if(flow.Step==CprStep.Ready) RestartPractice();
                    else if(flow.Step==CprStep.MovePatient) transport.DemonstrateCollapse();
                    else flow.Confirm();
                    buttonCooldown=.85f;
                }
                else if(button==1) { SceneManager.LoadScene(settings.menuScene); return; }
            }
            RenderHands(left,right);
        }

        private void RenderHands(CprHandSample left,CprHandSample right)
        {
            if(flow.Step==CprStep.MovePatient)
            {
                view.Hand(0,chest,left,false,false,settings.captureRadius);
                view.Hand(1,upper,right,false,false,settings.captureRadius);
                view.Marker(chest,false,false);
                input.HideTrackedVisual(true,transport.HoldingSite(0)>=0 && !left.controller);
                input.HideTrackedVisual(false,transport.HoldingSite(1)>=0 && !right.controller);
                return;
            }
            bool touch=flow.Step==CprStep.Response;
            bool placement=flow.Step>=CprStep.LowerHand;
            bool stacking=flow.Step>=CprStep.UpperHand;
            bool showLeft=touch || stacking;
            bool showRight=placement;
            Pose l=touch?shoulder:upper;
            Pose r=chest;
            if (stacking && left.controller) l.position += chest.rotation * Vector3.up * settings.controllerStackExtra;
            // Retain a clearly visible target when no tracked hand is available.
            view.Hand(0,l,left,showLeft,flow.StackLatched || (flow.LeftAligned && left.valid),settings.upperHandCaptureRadius,stacking);
            view.Hand(1,r,right,showRight,flow.LowerLatched || (flow.RightAligned && right.valid),settings.captureRadius,stacking);
            view.Marker(touch?shoulder:chest,touch||placement||flow.Step==CprStep.Breathing,flow.LeftAligned||flow.RightAligned);
            input.HideTrackedVisual(true,showLeft && (flow.StackLatched || flow.LeftAligned) && !left.controller);
            input.HideTrackedVisual(false,showRight && (flow.LowerLatched || flow.RightAligned) && !right.controller);
        }

        private string Status(CprHandSample left,CprHandSample right,bool looking)
        {
            if(flow.Step==CprStep.Safety) return "Apunta con el láser y confirma con gatillo o pinza. También puedes tocar el botón.";
            if(flow.Step==CprStep.MovePatient)
            {
                if(transport.IsSnapping) return "Colocando al paciente en el holograma…";
                if(transport.TrackingInterrupted) return "Vuelve a mostrar la mano. El cuerpo conserva su peso y se apoya en el suelo.";
                if(transport.IsHeld && transport.CanSnap) return "Destino en verde · abre las manos para colocar al paciente.";
                if(transport.IsHeld && transport.DistanceToTarget<=settings.patientSnapRadius)
                    return "Gira con ambos brazos hasta alinear al paciente con el holograma.";
                if(transport.IsHeld) return $"{(transport.GripCount==2?"Ambas manos agarradas":transport.HoldingSite(0)>=0?"Izquierda agarrada · puedes añadir la derecha":"Derecha agarrada · puedes añadir la izquierda")} · {transport.DistanceToTarget:0.0} m al destino";
                return $"Opción {transport.GuideOption}/{transport.GuideOptions}: {transport.GuideLabel}. Acércate y cierra la mano.";
            }
            if(flow.Step==CprStep.Breathing) return looking ? $"Observando el pecho… {flow.Progress*settings.breathingObservation:0.0} s" : "Dirige la mirada al pecho marcado. Máximo: 10 s.";
            if(flow.Step==CprStep.CallForHelp) return flow.ObservationTimedOut ? "Han pasado 10 s. En este caso: no respira normalmente." : "Caso simulado: no respira normalmente. Solicita ayuda.";
            if(flow.Step==CprStep.Ready) return flow.UsingOcclusion
                ? "Manos enganchadas · postura conservada durante la oclusión. Separa para soltar."
                : "Manos enganchadas · derecha abajo, izquierda encima. Separa para soltar.";
            if(!left.valid && !right.valid) return "Muestra las manos a las cámaras del visor o activa los mandos.";
            if(flow.Step==CprStep.Response) return "Toca suavemente el hombro indicado. Puedes usar cualquiera de las manos.";
            if(left.controller||right.controller) return "Modo mandos · orientación aproximada; deja espacio entre ellos.";
            if(flow.Step==CprStep.UpperHand) return "Derecha colocada · acerca la IZQUIERDA encima, siguiendo la guía.";
            return "Acerca primero la DERECHA: palma abajo y dedos siguiendo la guía.";
        }

        private void OnPatientGrabbed()
        {
            flow.BeginRelocation(); input.RestoreVisuals();
            input.Pulse(true); input.Pulse(false); view.PlayFeedback(false);
        }
        private void OnPatientPlaced()
        {
            flow.PatientPlaced(); input.Pulse(true,.5f); input.Pulse(false,.5f); view.PlayFeedback(true);
            buttonCooldown=.85f;
        }
        public void RestartPractice()
        {
            if(flow==null) return;
            flow.Restart(); transport.ResetPatient(); input.RestoreVisuals(); buttonCooldown=.85f;
        }
        private void OnDisable()
        {
            if(input!=null) input.RestoreVisuals();
            if(transport!=null) { transport.Suspend(); transport.enabled=false; }
            if(view!=null) view.enabled=false;
            if(movedRig!=null) { movedRig.SetPositionAndRotation(rigPosition,rigRotation); movedRig=null; }
            foreach(var component in temporarilyDisabled) if(component!=null) component.enabled=true;
            temporarilyDisabled.Clear();
        }
        private void OnDrawGizmosSelected()
        {
            if(chestAnchor!=null) { Gizmos.color=Color.cyan; Gizmos.DrawWireSphere(chestAnchor.position,.085f); }
            if(shoulderAnchor!=null) { Gizmos.color=Color.yellow; Gizmos.DrawWireSphere(shoulderAnchor.position,.13f); }
        }
    }
}
