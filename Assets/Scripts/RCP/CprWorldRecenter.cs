using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR;
namespace FirstAidVR.Rcp
{
    // Agrupar las notificaciones de Meta/OpenXR y esperar los anclajes oculares actualizados.
    // Es una colocación única, nunca un panel que siga el movimiento habitual de la cabeza.
    public sealed class CprWorldRecenter : MonoBehaviour
    {
        public static event Action Recentered;
        private OVRDisplay display;
        private OVRCameraRig rig;
        private readonly List<XRInputSubsystem> inputs=new List<XRInputSubsystem>();
        private readonly HashSet<XRInputSubsystem> subscribed=new HashSet<XRInputSubsystem>();
        private float discoverAt,requestedAt=-1,stableSince;
        private int requestedFrame;
        private Pose previous;
        private bool havePose;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetState() { Recentered=null; }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Install()
        { var go=new GameObject("Recolocación de la experiencia"); DontDestroyOnLoad(go); go.AddComponent<CprWorldRecenter>(); }
        void OnEnable() { OVRManager.VrFocusAcquired+=Requested; }
        void Update()
        {
            if(display!=OVRManager.display)
            {
                if(display!=null) display.RecenteredPose-=Requested;
                display=OVRManager.display;
                if(display!=null) display.RecenteredPose+=Requested;
            }
            if(Time.unscaledTime>=discoverAt)
            {
                discoverAt=Time.unscaledTime+.5f;
                var camera=ViewCamera(); var activeRig=camera!=null?camera.GetComponentInParent<OVRCameraRig>():null;
                if(rig!=activeRig)
                {
                    if(rig!=null) rig.UpdatedAnchors-=AnchorsUpdated;
                    rig=activeRig;
                    if(rig!=null) rig.UpdatedAnchors+=AnchorsUpdated;
                }
                SubsystemManager.GetInstances(inputs);
                foreach(var input in inputs) if(subscribed.Add(input)) input.trackingOriginUpdated+=OriginUpdated;
            }
        }
        void OriginUpdated(XRInputSubsystem input) { Requested(); }
        void OnApplicationFocus(bool focused) { if(focused) Requested(); }
        void OnApplicationPause(bool paused) { if(!paused) Requested(); }
        public void Requested()
        {
            requestedAt=Time.unscaledTime; requestedFrame=Time.frameCount;
            stableSince=requestedAt; havePose=false;
        }
        void AnchorsUpdated(OVRCameraRig updated) { ApplyWhenReady(); }
        void LateUpdate() { if(rig==null) ApplyWhenReady(); }
        public static bool PoseSettled(float elapsed,float stableFor,int frames)
        { return frames>=3 && elapsed>=.35f && (stableFor>=.12f || elapsed>=1.5f); }
        private void ApplyWhenReady()
        {
            if(requestedAt<0) return;
            if(rig!=null && !Application.isBatchMode && !OVRManager.hasVrFocus) return;
            var camera=ViewCamera(); if(camera==null) return;
            var current=new Pose(camera.transform.position,camera.transform.rotation);
            if(!havePose || Vector3.Distance(current.position,previous.position)>.015f || Quaternion.Angle(current.rotation,previous.rotation)>1.5f)
                stableSince=Time.unscaledTime;
            previous=current; havePose=true;
            if(!PoseSettled(Time.unscaledTime-requestedAt,Time.unscaledTime-stableSince,Time.frameCount-requestedFrame)) return;
            requestedAt=-1; Recentered?.Invoke();
        }
        void OnDisable()
        {
            OVRManager.VrFocusAcquired-=Requested;
            if(display!=null) display.RecenteredPose-=Requested; display=null;
            if(rig!=null) rig.UpdatedAnchors-=AnchorsUpdated; rig=null;
            foreach(var input in subscribed) input.trackingOriginUpdated-=OriginUpdated;
            subscribed.Clear(); requestedAt=-1;
        }
        public static Camera ViewCamera()
        {
            foreach(var activeRig in FindObjectsOfType<OVRCameraRig>())
            {
                if(!activeRig.isActiveAndEnabled || activeRig.centerEyeAnchor==null) continue;
                var camera=activeRig.centerEyeAnchor.GetComponent<Camera>();
                if(camera!=null && camera.isActiveAndEnabled) return camera;
            }
            return Camera.main;
        }
        public static Vector3 Forward(Transform head)
        { var v=Vector3.ProjectOnPlane(head.forward,Vector3.up); return v.sqrMagnitude>.01f?v.normalized:Vector3.forward; }
        public static void Align(Transform root,Transform anchor,Transform head,float distance,bool preserveHeight)
        {
            var forward=Forward(head); var target=head.position+forward*distance;
            if(preserveHeight) target.y=anchor.position.y;
            root.rotation=Quaternion.LookRotation(forward,Vector3.up);
            root.position+=target-anchor.position;
        }
    }
}
