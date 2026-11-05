using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace FirstAidVR.Rcp
{
    public sealed class CprGrabGuides : MonoBehaviour
    {
        public sealed class Site
        {
            public int group, hand;
            public string label;
            public CprPatientRagdoll.Part part;
            public Pose localHand;
            public Vector3 localContact;
            public bool held;
            public int heldHand;
            public Pose heldLocalHand;
            public Vector3 heldLocalContact;
        }
        public IReadOnlyList<Site> Sites => sites;
        public int ActiveGroup { get; private set; }
        public string ActiveLabel => labels[ActiveGroup];
        public int GroupCount => labels.Count;
        public int VisibleHands { get; private set; }
        private readonly List<Site> sites = new List<Site>();
        private readonly List<string> labels = new List<string>();
        private readonly List<Transform> ghosts = new List<Transform>();
        private readonly List<MeshRenderer> renderers = new List<MeshRenderer>();
        private MaterialPropertyBlock properties;
        private CprTrainingSettings settings;
        private float clock, hoverGrace;
        private int hovered = -1;

        public void Build(CprTrainingSettings config, CprPatientRagdoll ragdoll, Transform patient,
            Transform leftAnchor = null, Transform rightAnchor = null)
        {
            settings = config; properties = new MaterialPropertyBlock();
            for(int side=0;side<2;side++)
            {
                int group=labels.Count; labels.Add(side==0?"Antebrazo izquierdo · agarre del muñeco":"Antebrazo derecho · agarre del muñeco");
                var arm=ragdoll.FindPart(side==0?"L_Forearm":"R_Forearm");
                var end=ragdoll.FindPart(side==0?"L_Hand":"R_Hand").bone.position;
                var axis=(end-arm.bone.position).normalized;
                var overrideAnchor=side==0?leftAnchor:rightAnchor;
                for(int hand=0;hand<2;hand++)
                {
                    var center=Vector3.Lerp(arm.bone.position,end,hand==0?.38f:.78f);
                    if(overrideAnchor!=null) center=overrideAnchor.position+axis*(hand==0?-.04f:.04f);
                    var forward=Vector3.Cross(axis,patient.forward).normalized*(hand==0?1:-1);
                    Add(group,hand,arm,center+patient.forward*.04f,Quaternion.LookRotation(forward,patient.forward));
                }
            }
            if(settings.showAnkleGrips)
            {
                int group=labels.Count; labels.Add("Piernas / tobillos · solo sin sospecha de lesión cervical");
                for(int hand=0;hand<2;hand++)
                {
                    var calf=ragdoll.FindPart(hand==0?"L_Calf":"R_Calf");
                    var foot=ragdoll.FindPart(hand==0?"L_Foot":"R_Foot");
                    var axis=(foot.bone.position-calf.bone.position).normalized;
                    var forward=Vector3.Cross(axis,patient.forward).normalized*(hand==0?1:-1);
                    Add(group,hand,calf,Vector3.Lerp(calf.bone.position,foot.bone.position,.88f)+patient.forward*.048f,
                        Quaternion.LookRotation(forward,patient.forward));
                }
            }
            SetVisible(false);
        }

        private void Add(int group,int hand,CprPatientRagdoll.Part part,Vector3 palmContact,Quaternion rotation)
        {
            // La entrada mide el talón de la palma; la fuerza se aplica en el centro de la superficie agarrada.
            var heel=palmContact-rotation*Vector3.forward*.035f;
            var site=new Site { group=group,hand=hand,label=labels[group],part=part,
                localHand=new Pose(part.body.transform.InverseTransformPoint(heel),Quaternion.Inverse(part.body.rotation)*rotation),
                localContact=part.body.transform.InverseTransformPoint(palmContact) };
            sites.Add(site);
            var ghost=new GameObject("Guía "+labels[group]+(hand==0?" · izquierda":" · derecha"),typeof(MeshFilter),typeof(MeshRenderer));
            ghost.transform.SetParent(transform,false);
            ghost.GetComponent<MeshFilter>().sharedMesh=hand==0?settings.gripLeftHand:settings.gripRightHand;
            var renderer=ghost.GetComponent<MeshRenderer>(); renderer.sharedMaterial=settings.hologramMaterial;
            renderer.shadowCastingMode=ShadowCastingMode.Off; renderer.receiveShadows=false;
            ghosts.Add(ghost.transform); renderers.Add(renderer);
        }
        public Pose HandPose(int site)
        {
            var target=sites[site];
            var local=target.held?target.heldLocalHand:target.localHand;
            return new Pose(target.part.body.transform.TransformPoint(local.position),target.part.body.rotation*local.rotation);
        }
        public Vector3 Contact(int site)
        { var target=sites[site]; return target.part.body.transform.TransformPoint(target.held?target.heldLocalContact:target.localContact); }
        public void Capture(int site, int hand, CprHandSample sample)
        {
            var target=sites[site];
            var point=sample.pose.position+sample.pose.rotation*Vector3.forward*.035f;
            var contact=target.part.collider.ClosestPoint(point);
            target.held=true; target.heldHand=hand;
            target.heldLocalContact=target.part.body.transform.InverseTransformPoint(contact);
            target.heldLocalHand=new Pose(target.part.body.transform.InverseTransformPoint(contact-sample.pose.rotation*Vector3.forward*.035f),
                Quaternion.Inverse(target.part.body.rotation)*sample.pose.rotation);
        }
        public void Release(int site) { if(site>=0) sites[site].held=false; }
        public int Nearest(CprHandSample sample,int hand,int otherHeld,float radius)
        {
            if(!sample.valid) return -1;
            int nearest=-1; float distance=radius;
            var point=sample.pose.position+sample.pose.rotation*Vector3.forward*.035f;
            for(int i=0;i<sites.Count;i++)
            {
                if(i==otherHeld) continue;
                float d=Vector3.Distance(point,Contact(i));
                if(d<distance) { nearest=i; distance=d; }
            }
            return nearest;
        }
        public void Tick(float dt,CprHandSample left,CprHandSample right,int heldLeft,int heldRight,bool visible)
        {
            if(!visible) { SetVisible(false); return; }
            int nearLeft=Nearest(left,0,heldRight,settings.armGrabRadius*1.5f);
            int nearRight=Nearest(right,1,heldLeft,settings.armGrabRadius*1.5f);
            bool held=heldLeft>=0 || heldRight>=0;
            if(held)
            {
                ActiveGroup=sites[heldLeft>=0?heldLeft:heldRight].group;
                hovered=ActiveGroup; hoverGrace=.35f;
            }
            else if(nearLeft>=0 || nearRight>=0)
            {
                int near=nearLeft>=0?nearLeft:nearRight;
                ActiveGroup=sites[near].group; hovered=ActiveGroup; hoverGrace=.35f;
            }
            else
            {
                hoverGrace-=dt;
                if(hoverGrace<=0) { hovered=-1; clock+=dt; ActiveGroup=(int)(clock/Mathf.Max(.5f,settings.grabGuideSeconds))%labels.Count; }
            }
            float phase=(clock%Mathf.Max(.5f,settings.grabGuideSeconds))/Mathf.Max(.5f,settings.grabGuideSeconds);
            float pulse=hovered>=0?1:Mathf.SmoothStep(.18f,1,Mathf.Sin(phase*Mathf.PI));
            VisibleHands=0;
            for(int i=0;i<sites.Count;i++)
            {
                bool attached=i==heldLeft || i==heldRight;
                bool show=attached || sites[i].group==ActiveGroup;
                // Al sujetar zonas diferentes, mostrar solo las dos manos realmente ocupadas.
                if(heldLeft>=0 && heldRight>=0 && sites[heldLeft].group!=sites[heldRight].group) show=attached;
                ghosts[i].gameObject.SetActive(show);
                if(!show) continue;
                VisibleHands++;
                int meshHand=attached?sites[i].heldHand:sites[i].hand;
                if(!attached && held) meshHand=heldLeft>=0?1:0;
                // La posición está disponible para cualquiera de las manos; su malla sigue a la mano que la ocupó.
                ghosts[i].GetComponent<MeshFilter>().sharedMesh=meshHand==0?settings.gripLeftHand:settings.gripRightHand;
                var pose=HandPose(i); ghosts[i].SetPositionAndRotation(pose.position,pose.rotation);
                var color=attached?CprTutorialView.Green:CprTutorialView.Cyan;
                color.a=attached?.94f:Mathf.Lerp(.12f,.60f,pulse);
                properties.SetColor("_Color",color); renderers[i].SetPropertyBlock(properties);
            }
        }
        public void SetVisible(bool visible)
        {
            VisibleHands=0;
            for(int i=0;i<ghosts.Count;i++)
            {
                bool show=visible && sites[i].group==ActiveGroup;
                ghosts[i].gameObject.SetActive(show); if(show) VisibleHands++;
            }
        }
        private void OnDisable() { SetVisible(false); }
    }
}

