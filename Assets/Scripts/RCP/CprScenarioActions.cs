using TMPro;
using UnityEngine;

namespace FirstAidVR.Rcp
{
    public sealed class CprScenarioActions : MonoBehaviour
    {
        [SerializeField] private Transform phone, phoneDock;
        private CprPracticeSession safetySession;
        private Vector3 safetyForward;
        public void ResetSafetyReference() { safetySession=null; }
        public static Pose ObservationTarget(Pose head,Vector3 forward,int side,float floorHeight=0)
        {
            if(forward.sqrMagnitude<.01f) forward=Vector3.forward;
            var direction=Quaternion.AngleAxis(side==0?-85:85,Vector3.up)*forward.normalized;
            var position=head.position+direction*1.15f; position.y=floorHeight+.015f;
            return new Pose(position,Quaternion.LookRotation(forward.normalized,Vector3.up)*Quaternion.Euler(90,0,0));
        }
        private void PlaceSafetyTargets(Pose head)
        {
            var rig=FindObjectOfType<OVRCameraRig>();
            float floor=rig!=null && rig.trackingSpace!=null?rig.trackingSpace.position.y:0;
            for(int i=0;i<2;i++)
            {
                var pose=ObservationTarget(head,safetyForward,i,floor);
                safetyZones[i].transform.SetPositionAndRotation(pose.position,pose.rotation);
                safetyZones[i].positionCount=64;
                for(int j=0;j<64;j++)
                { float a=j*Mathf.PI*2/64; safetyZones[i].SetPosition(j,new Vector3(Mathf.Cos(a)*.22f,Mathf.Sin(a)*.22f,0)); }
                safetyLabels[i].transform.localPosition=new Vector3(0,0,-.01f);
                safetyLabels[i].transform.localRotation=Quaternion.identity;
                safetyLabels[i].rectTransform.sizeDelta=new Vector2(.40f,.18f);
                safetyLabels[i].fontSize=.28f;
                safetyZones[i].widthMultiplier=.012f;
            }
        }
        public static int ObservedSide(Vector3 reference, Quaternion rotation)
        {
            var direction=Vector3.ProjectOnPlane(rotation*Vector3.forward,Vector3.up);
            if(reference.sqrMagnitude<.01f || direction.sqrMagnitude<.01f) return 0;
            float yaw=Vector3.SignedAngle(reference,direction,Vector3.up);
            return yaw<=-25 && yaw>=-110?-1:yaw>=25 && yaw<=110?1:0;
        }
        [SerializeField] private LineRenderer[] safetyZones;
        [SerializeField] private TextMeshPro[] safetyLabels;
        [SerializeField] private TextMeshPro phoneLabel;
        private int holder=-1, grabCandidate=-1;
        private float lost, grabHeld, callHeld;
        private CprPracticeStep previous;
        private MaterialPropertyBlock tint;
        private Transform[] phoneHands;
        private Material phoneBodyMaterial,phoneScreenMaterial,phoneHandMaterial;
        public bool HoldingPhone => holder>=0;
        public int PhoneHolder => holder;
        public bool ShowingPhoneHand => holder>=0 && phoneHands!=null && phoneHands[holder]!=null && phoneHands[holder].gameObject.activeSelf;
        public float CallProgress => Mathf.Clamp01(callHeld/1);
        public Transform Phone => phone;
        public Transform[] SafetyTargets => new[]{safetyZones[0].transform,safetyZones[1].transform};
        public void Build(CprTrainingSettings settings)
        {
            BuildPhoneHands();
            if(phone!=null)
            {
                phone.Find("Cuerpo").GetComponent<Renderer>().sharedMaterial=phoneBodyMaterial;
                phone.Find("Pantalla").GetComponent<Renderer>().sharedMaterial=phoneScreenMaterial;
                return;
            }
            safetyZones=new LineRenderer[2]; safetyLabels=new TextMeshPro[2];
            for(int i=0;i<2;i++)
            {
                var go=new GameObject("Observar entorno "+(i+1),typeof(LineRenderer)); go.transform.SetParent(transform,false);
                go.transform.localPosition=new Vector3(i==0?-.68f:.68f,-.15f,-.12f);
                var line=go.GetComponent<LineRenderer>(); line.sharedMaterial=settings.hologramMaterial; line.useWorldSpace=false; line.loop=true;
                line.positionCount=64; line.widthMultiplier=.005f;
                for(int j=0;j<64;j++) { float a=j*Mathf.PI*2/64; line.SetPosition(j,new Vector3(Mathf.Cos(a)*.12f,0,Mathf.Sin(a)*.12f)); }
                safetyZones[i]=line; safetyLabels[i]=Label(go.transform,"OBSERVA",new Vector3(0,.08f,0),.30f); go.SetActive(false);
            }
            phoneDock=new GameObject("Teléfono de práctica").transform; phoneDock.SetParent(transform,false); phoneDock.localPosition=new Vector3(-.43f,.12f,-.34f);
            phone=new GameObject("Teléfono").transform; phone.SetParent(phoneDock,false);
            MakeCube(phone,"Cuerpo",Vector3.zero,new Vector3(.080f,.148f,.012f),phoneBodyMaterial);
            MakeCube(phone,"Pantalla",new Vector3(0,.005f,-.008f),new Vector3(.063f,.105f,.003f),phoneScreenMaterial);
            phoneLabel=Label(phoneDock,"TOMA EL TELÉFONO",new Vector3(0,.14f,0),.25f);
            Label(phone,"AYUDA",new Vector3(0,.01f,-.012f),.16f);
            phoneDock.gameObject.SetActive(false);
        }
        private void BuildPhoneHands()
        {
            if(phoneHands!=null) return;
            var shader=Resources.Load<Shader>("RCP/PhoneHologram");
            phoneBodyMaterial=new Material(shader){color=new Color(.08f,.65f,.9f,.48f)};
            phoneScreenMaterial=new Material(shader){color=new Color(.12f,.82f,1,.32f)};
            phoneHandMaterial=new Material(shader){color=new Color(.12f,.82f,1,.62f)};
            phoneHands=new Transform[2];
            for(int i=0;i<2;i++)
            {
                var mesh=Resources.Load<Mesh>("RCP/Phone"+(i==0?"Left":"Right")+"Hand");
                if(mesh==null) continue;
                string name="Agarre del teléfono "+(i==0?"izquierdo":"derecho");
                var existing=transform.Find(name);
                var go=existing!=null?existing.gameObject:new GameObject(name,typeof(MeshFilter),typeof(MeshRenderer));
                go.transform.SetParent(transform,false);
                go.GetComponent<MeshFilter>().sharedMesh=mesh;
                go.GetComponent<MeshRenderer>().sharedMaterial=phoneHandMaterial;
                phoneHands[i]=go.transform; go.SetActive(false);
            }
        }
        public CprPracticeObservation Tick(CprPracticeSession session,Pose head,CprHandSample left,CprHandSample right,float dt,bool focus)
        {
            if(tint==null) tint=new MaterialPropertyBlock();
            bool safety=focus && !session.Paused && session.Step==CprPracticeStep.Safety;
            bool help=focus && !session.Paused && session.Step==CprPracticeStep.Help && !session.StepPassed;
            if(previous!=session.Step) { ResetPhone(); previous=session.Step; }
            var result=new CprPracticeObservation();
            if(safety && safetySession!=session)
            {
                safetySession=session;
                safetyForward=Vector3.ProjectOnPlane(head.rotation*Vector3.forward,Vector3.up).normalized;
                PlaceSafetyTargets(head);
            }
            int observed=safety?ObservedSide(safetyForward,head.rotation):0;
            for(int i=0;i<2;i++)
            {
                safetyZones[i].gameObject.SetActive(safety);
                bool seen=(session.SafetySeenMask&(1<<i))!=0;
                safetyLabels[i].text=seen?"REVISADO":i==0?"OBSERVA\nIZQUIERDA":"OBSERVA\nDERECHA";
                tint.SetColor("_Color",seen?CprStationView.Green:CprStationView.Cyan); safetyZones[i].SetPropertyBlock(tint);
                var delta=safetyZones[i].transform.position-head.position;
                bool looking=safety && (observed==(i==0?-1:1) || delta.magnitude<2.5f && Vector3.Angle(head.rotation*Vector3.forward,delta)<16);
                if(i==0) result.safetyLeft=looking; else result.safetyRight=looking;
            }
            phoneDock.gameObject.SetActive(help);
            if(!help) { ResetPhone(); return result; }
            dt=Mathf.Clamp(dt,0,.12f);
            if(holder<0)
            {
                bool l=CanGrab(left), r=CanGrab(right);
                int candidate=l && r ? (Vector3.Distance(left.pose.position,phone.position)<=Vector3.Distance(right.pose.position,phone.position)?0:1)
                    : l?0:r?1:-1;
                if(candidate!=grabCandidate) { grabCandidate=candidate; grabHeld=0; }
                grabHeld=candidate>=0?grabHeld+dt:0;
                if(grabHeld>=.12f) holder=candidate;
                phoneLabel.text="TÓMALO CON CUALQUIER MANO";
            }
            if(holder>=0)
            {
                var hand=holder==0?left:right;
                if(!hand.valid || !hand.gripValid) { lost+=dt; if(lost>.35f) ResetPhone(); return result; }
                lost=0;
                if(hand.grip<.25f) { ResetPhone(); return result; }
                var pose=PhonePose(hand,holder==0);
                phone.SetPositionAndRotation(pose.position,pose.rotation);
                if(phoneHands[holder]!=null)
                {
                    phoneHands[holder].SetPositionAndRotation(hand.pose.position,hand.pose.rotation);
                    phoneHands[holder].gameObject.SetActive(true);
                }
                bool nearEar=Vector3.Distance(hand.pose.position,head.position)<.30f && hand.pose.position.y>head.position.y-.25f;
                callHeld=nearEar?callHeld+dt:0;
                phoneLabel.text=nearEar?"SOLICITANDO AYUDA…":"ACÉRCALO AL OÍDO";
                result.helpRequested=callHeld>=1;
            }
            return result;
        }
        private bool CanGrab(CprHandSample h) => h.valid && h.gripValid && h.grip>.55f && Vector3.Distance(h.pose.position,phone.position)<.15f;
        public static Pose PhonePose(CprHandSample hand,bool left)
        {
            // Ejes de muestra: +Y dorso, +Z dedos. La parte trasera del teléfono descansa
            // contra la palma; su eje largo sigue los dedos y la pantalla mira hacia afuera.
            var offset=new Vector3(left?-.008f:.008f,-.024f,.055f);
            return new Pose(hand.pose.position+hand.pose.rotation*offset,
                hand.pose.rotation*Quaternion.LookRotation(Vector3.up,Vector3.forward));
        }
        public void ResetPhone()
        {
            if(phoneHands!=null) foreach(var hand in phoneHands) if(hand!=null) hand.gameObject.SetActive(false);
            holder=grabCandidate=-1; lost=grabHeld=callHeld=0;
            if(phone!=null) { phone.localPosition=Vector3.zero; phone.localRotation=Quaternion.identity; }
        }
        public void Hide()
        {
            if(safetyZones!=null) foreach(var zone in safetyZones) if(zone!=null) zone.gameObject.SetActive(false);
            if(phoneDock!=null) phoneDock.gameObject.SetActive(false); ResetPhone();
        }
        private void OnDisable() { Hide(); }
        private void OnDestroy()
        {
            foreach(var material in new[]{phoneBodyMaterial,phoneScreenMaterial,phoneHandMaterial})
                if(material!=null) { if(Application.isPlaying) Destroy(material); else DestroyImmediate(material); }
        }
        private static void MakeCube(Transform parent,string name,Vector3 position,Vector3 scale,Material material)
        {
            var go=GameObject.CreatePrimitive(PrimitiveType.Cube); go.name=name; go.transform.SetParent(parent,false); go.transform.localPosition=position; go.transform.localScale=scale;
            go.GetComponent<Renderer>().sharedMaterial=material;
            if(Application.isPlaying) Destroy(go.GetComponent<Collider>()); else DestroyImmediate(go.GetComponent<Collider>());
        }
        private static TextMeshPro Label(Transform parent,string text,Vector3 p,float font)
        {
            var label=new GameObject(text,typeof(TextMeshPro)).GetComponent<TextMeshPro>(); label.transform.SetParent(parent,false); label.transform.localPosition=p;
            label.rectTransform.sizeDelta=new Vector2(.55f,.12f); label.fontSize=font; label.alignment=TextAlignmentOptions.Center; label.text=text; label.color=CprStationView.Cyan; return label;
        }
    }
}
