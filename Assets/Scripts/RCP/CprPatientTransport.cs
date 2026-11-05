using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Rendering;

namespace FirstAidVR.Rcp
{
    /// <summary>Los objetivos de agarre con seguimiento aplican fuerzas limitadas a un ragdoll dinámico completamente conectado.</summary>
    public sealed class CprPatientTransport : MonoBehaviour
    {
        public UnityEvent onGrab = new UnityEvent();
        public UnityEvent onPlaced = new UnityEvent();
        public bool IsHeld => heldSites[0] >= 0 || heldSites[1] >= 0;
        public bool IsSnapping { get; private set; }
        public bool IsPlaced { get; private set; }
        public bool CanSnap { get; private set; }
        public bool TrackingInterrupted { get; private set; }
        public CprPatientRagdoll Ragdoll { get; private set; }
        public CprGrabGuides Guides { get; private set; }
        public Pose Destination => destination;
        public Transform Hologram => hologram;
        public int HoldingSite(int hand) => heldSites[hand];
        public int HoldingArm(int hand) => heldSites[hand] < 0 || Guides.Sites[heldSites[hand]].group>1 ? -1 : Guides.Sites[heldSites[hand]].group;
        public int GripCount => (heldSites[0]>=0?1:0)+(heldSites[1]>=0?1:0);
        public string GuideLabel => Guides != null ? Guides.ActiveLabel : "";
        public int GuideOption => Guides != null ? Guides.ActiveGroup + 1 : 0;
        public int GuideOptions => Guides != null ? Guides.GroupCount : 0;
        private Vector3 TargetHip => destination.position+destination.rotation*Vector3.Scale(patient.lossyScale,Ragdoll.FindPart("Hip").rootPosition);
        public float DistanceToTarget => !initialized ? 0 : Vector3.ProjectOnPlane(Ragdoll.Hip.position-TargetHip,Vector3.up).magnitude;
        public float AngleToTarget
        {
            get
            {
                if(!initialized) return 0;
                var direction=Vector3.ProjectOnPlane(Ragdoll.FindPart("Spine02").body.position-Ragdoll.Hip.position,Vector3.up);
                var target=Vector3.ProjectOnPlane(destination.rotation*(Ragdoll.FindPart("Spine02").rootPosition-Ragdoll.FindPart("Hip").rootPosition),Vector3.up);
                return direction.sqrMagnitude<.001f ? 180 : Vector3.Angle(direction,target);
            }
        }

        private CprTrainingSettings settings;
        private Transform patient, hologram;
        private readonly int[] heldSites = { -1, -1 };
        private readonly bool[] armed = { true, true }, previousValid = new bool[2];
        private readonly float[] lost = new float[2], openTime = new float[2], strainTime = new float[2];
        private readonly Vector3[] previous = new Vector3[2], gripTargets = new Vector3[2];
        private readonly CprHandSample[] samples = new CprHandSample[2];
        private readonly List<Mesh> ownedMeshes = new List<Mesh>();
        private readonly List<Renderer> ghostRenderers = new List<Renderer>();
        private MaterialPropertyBlock properties;
        private Pose initial, destination;
        private float snapTime, placedTime, floorHeight;
        private bool initialized;

        public void Initialize(CprTrainingSettings config, Transform body, Transform target = null,
            Transform leftArm = null, Transform rightArm = null)
        {
            settings = config; patient = body;
            initial = new Pose(patient.position, patient.rotation);
            var side = Vector3.ProjectOnPlane(patient.right, Vector3.up).normalized;
            var length = Vector3.ProjectOnPlane(patient.up, Vector3.up).normalized;
            if (length.sqrMagnitude < .1f) length = Vector3.Cross(side, Vector3.up);
            destination = target != null ? new Pose(target.position, target.rotation)
                : new Pose(initial.position + side*settings.patientDestinationOffset.x + length*settings.patientDestinationOffset.z, initial.rotation);
            destination.position.y = initial.position.y;
            floorHeight=Physics.Raycast(body.position+Vector3.up*2,Vector3.down,out var floor,4,~0,QueryTriggerInteraction.Ignore)
                ? floor.point.y : initial.position.y-.16f;
            properties=new MaterialPropertyBlock();
            // Hornear antes de que la física cambie el esqueleto para que el objetivo conserve la pose acostada prevista.
            BuildHologram();
            Ragdoll=gameObject.AddComponent<CprPatientRagdoll>();
            if(!Ragdoll.Initialize(settings,patient)) { enabled=false; return; }
            var guideObject=new GameObject("Agarres · manos por turnos");
            guideObject.transform.SetParent(transform,false);
            Guides=guideObject.AddComponent<CprGrabGuides>();
            Guides.Build(settings,Ragdoll,patient,leftArm,rightArm);
            initialized=true; UpdateVisuals(false);
        }
        public Vector3 ArmPosition(int index)
        {
            if(!initialized) return patient.position;
            var part=Ragdoll.FindPart(index==0?"L_Forearm":"R_Forearm");
            var hand=Ragdoll.FindPart(index==0?"L_Hand":"R_Hand");
            return Vector3.Lerp(part.body.position,hand.body.position,.58f);
        }
        private static Vector3 GripPoint(CprHandSample hand) => hand.pose.position+hand.pose.rotation*Vector3.forward*.035f;

        public void Tick(float dt,CprHandSample left,CprHandSample right,bool allowInteraction,bool showGuides=true)
        {
            if(!initialized) return;
            dt=Mathf.Clamp(dt,0,.1f); samples[0]=left; samples[1]=right; TrackingInterrupted=false;
            if(!allowInteraction) { Suspend(); UpdateVisuals(false); return; }
            if(IsSnapping)
            {
                snapTime+=dt;
                float t=Mathf.SmoothStep(0,1,snapTime/Mathf.Max(.01f,settings.patientSnapDuration));
                Ragdoll.SnapTo(destination,t);
                if(t>=1) { IsSnapping=false; IsPlaced=true; placedTime=0; onPlaced.Invoke(); }
                Guides.SetVisible(false); UpdateVisuals(true); return;
            }
            if(!IsPlaced) Ragdoll.Activate();
            Ragdoll.SyncVisuals();
            placedTime+=dt;
            bool hadGrip=IsHeld, gestureRelease=false, trackingRelease=false;
            for(int hand=0;hand<2;hand++)
            {
                var sample=samples[hand];
                bool usable=sample.valid && sample.gripValid;
                if(heldSites[hand]>=0)
                {
                    lost[hand]=usable?0:lost[hand]+dt;
                    if(!usable) TrackingInterrupted=true;
                    openTime[hand]=usable && sample.grip<=settings.grabReleaseThreshold?openTime[hand]+dt:0;
                    bool opened=openTime[hand]>=settings.grabOpenHold;
                    if(opened || lost[hand]>=settings.grabTrackingGrace)
                    {
                        Release(hand); gestureRelease|=opened; trackingRelease|=!opened; continue;
                    }
                    if(!usable)
                    {
                        // Mantener unida la representación de la mano mientras el cuerpo completo se estabiliza.
                        Ragdoll.ReleaseGrip(hand); previousValid[hand]=false; continue;
                    }
                    var point=GripPoint(sample);
                    strainTime[hand]=Vector3.Distance(point,Guides.Contact(heldSites[hand]))>settings.grabMaxReach?strainTime[hand]+dt:0;
                    if(strainTime[hand]>.15f || (!previousValid[hand] && Vector3.Distance(point,Guides.Contact(heldSites[hand]))>settings.grabMaxReach))
                    {
                        // Una mano recuperada lejos del paciente debe volver a agarrarlo. Nunca guardar un desplazamiento permanente.
                        Release(hand); trackingRelease=true; continue;
                    }
                    if(!previousValid[hand] || Vector3.Distance(point,previous[hand])>.3f)
                    {
                        int site=heldSites[hand];
                        gripTargets[hand]=Guides.Contact(site);
                        Ragdoll.BeginGrip(hand,Guides.Sites[site].part,gripTargets[hand]);
                    }
                    gripTargets[hand]=point;
                    Ragdoll.SetGripTarget(hand,gripTargets[hand]);
                    previous[hand]=point; previousValid[hand]=true;
                }
                else
                {
                    if(usable && sample.grip<=settings.grabReleaseThreshold) armed[hand]=true;
                    if(!armed[hand] || !usable || sample.grip<settings.grabThreshold) continue;
                    int site=Guides.Nearest(sample,hand,heldSites[1-hand],settings.armGrabRadius);
                    if(site<0) continue;
                    heldSites[hand]=site; armed[hand]=false; previousValid[hand]=true; lost[hand]=openTime[hand]=strainTime[hand]=0;
                    Guides.Capture(site,hand,sample);
                    previous[hand]=GripPoint(sample); gripTargets[hand]=Guides.Contact(site);
                    Ragdoll.BeginGrip(hand,Guides.Sites[site].part,gripTargets[hand]);
                    Ragdoll.SetGripTarget(hand,previous[hand]);
                    IsPlaced=false; onGrab.Invoke();
                }
            }
            CanSnap=DistanceToTarget<=settings.patientSnapRadius && AngleToTarget<=settings.patientSnapAngle
                && Mathf.Abs(Ragdoll.Hip.position.y-TargetHip.y)<.30f;
            if(hadGrip && !IsHeld && gestureRelease && !trackingRelease && CanSnap)
            { Ragdoll.BeginSnap(); snapTime=0; IsSnapping=true; }
            Guides.Tick(dt,left,right,heldSites[0],heldSites[1],!IsSnapping && (showGuides || IsHeld));
            UpdateVisuals(true);
        }
        private void Release(int hand)
        { Guides.Release(heldSites[hand]); heldSites[hand]=-1; previousValid[hand]=false; Ragdoll.ReleaseGrip(hand); }
        public void CancelGrips()
        {
            for(int hand=0;hand<2;hand++)
            {
                if(Ragdoll!=null) Ragdoll.ReleaseGrip(hand);
                if(Guides!=null) Guides.Release(heldSites[hand]);
                heldSites[hand]=-1; lost[hand]=openTime[hand]=strainTime[hand]=0; previousValid[hand]=false; armed[hand]=false;
            }
            TrackingInterrupted=false;
        }
        public void Suspend()
        {
            CancelGrips(); IsSnapping=false; CanSnap=false;
            if(Ragdoll!=null) Ragdoll.Suspend();
            if(Guides!=null) Guides.SetVisible(false);
        }
        public void ResetPatient()
        {
            if(!initialized) return;
            Suspend(); IsPlaced=false; Ragdoll.ResetPatient(); UpdateVisuals(false);
        }
        [ContextMenu("Demostrar desplome del paciente")]
        public void DemonstrateCollapse()
        {
            if(!initialized) return;
            CancelGrips(); IsPlaced=IsSnapping=CanSnap=false;
            var lean=Random.insideUnitCircle;
            if(lean.sqrMagnitude<.1f) lean=Vector2.right;
            Ragdoll.CollapseStanding(new Vector3(initial.position.x,floorHeight,initial.position.z),new Vector3(lean.x,0,lean.y));
            onGrab.Invoke();
        }

        private void BuildHologram()
        {
            hologram=new GameObject("Destino · holograma del paciente").transform;
            hologram.SetParent(transform,false); hologram.SetPositionAndRotation(destination.position,destination.rotation);
            hologram.localScale=patient.lossyScale;
            foreach(var skin in patient.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                if(!skin.enabled || !skin.gameObject.activeInHierarchy || skin.sharedMesh==null) continue;
                var mesh=new Mesh { name="Holograma "+skin.name }; skin.BakeMesh(mesh); AddGhostMesh(mesh,skin.transform);
            }
            foreach(var filter in patient.GetComponentsInChildren<MeshFilter>(true))
            {
                var renderer=filter.GetComponent<MeshRenderer>();
                if(renderer==null || !renderer.enabled || !filter.gameObject.activeInHierarchy || filter.sharedMesh==null) continue;
                AddGhostMesh(Instantiate(filter.sharedMesh),filter.transform);
            }
        }
        private void AddGhostMesh(Mesh mesh,Transform source)
        {
            var matrix=patient.worldToLocalMatrix*source.localToWorldMatrix;
            var vertices=mesh.vertices;
            for(int i=0;i<vertices.Length;i++) vertices[i]=matrix.MultiplyPoint3x4(vertices[i]);
            mesh.vertices=vertices; mesh.RecalculateNormals(); mesh.RecalculateBounds(); ownedMeshes.Add(mesh);
            var ghost=new GameObject(mesh.name,typeof(MeshFilter),typeof(MeshRenderer));
            ghost.transform.SetParent(hologram,false); ghost.GetComponent<MeshFilter>().sharedMesh=mesh;
            var renderer=ghost.GetComponent<MeshRenderer>();
            var materials=new Material[mesh.subMeshCount];
            for(int i=0;i<materials.Length;i++) materials[i]=settings.hologramMaterial;
            renderer.sharedMaterials=materials; renderer.shadowCastingMode=ShadowCastingMode.Off; renderer.receiveShadows=false;
            ghostRenderers.Add(renderer);
        }
        private void UpdateVisuals(bool visible)
        {
            if(hologram==null) return;
            hologram.gameObject.SetActive(visible && (!IsPlaced || placedTime<.7f));
            var color=CanSnap || IsSnapping || IsPlaced?CprTutorialView.Green:CprTutorialView.Cyan;
            color.a=IsPlaced?Mathf.Lerp(.35f,0,placedTime/.7f):.16f;
            properties.SetColor("_Color",color);
            foreach(var renderer in ghostRenderers) renderer.SetPropertyBlock(properties);
        }
        private void OnDisable()
        {
            Suspend(); if(initialized) UpdateVisuals(false);
            if(Ragdoll!=null) Ragdoll.enabled=false;
        }
        private void OnDestroy()
        {
            foreach(var mesh in ownedMeshes) if(mesh!=null)
            { if(Application.isPlaying) Destroy(mesh); else DestroyImmediate(mesh); }
        }
    }
}

