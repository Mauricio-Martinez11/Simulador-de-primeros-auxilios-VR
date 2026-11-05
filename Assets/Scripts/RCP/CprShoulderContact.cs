using UnityEngine;
namespace FirstAidVR.Rcp
{
    public sealed class CprShoulderContact
    {
        public int Hand { get; private set; }=-1;
        public bool Latched => Hand>=0;
        public bool Completed { get; private set; }
        public float Progress { get; private set; }
        public float Animation { get; private set; }
        public int Movements { get; private set; }
        private float missing,elapsed;
        public const float MovementDuration=.7f;
        public void Reset() { Hand=-1; Completed=false; missing=elapsed=Progress=Animation=0; Movements=0; }
        public void Tick(float dt,CprHandSample left,CprHandSample right,Pose shoulder,bool active,bool measure)
        {
            if(!active) { Reset(); return; }
            dt=Mathf.Clamp(dt,0,.1f);
            if(!Latched)
            {
                if(left.valid && left.reliable && Vector3.Distance(left.pose.position,shoulder.position)<.09f) Hand=0;
                else if(right.valid && right.reliable && Vector3.Distance(right.pose.position,shoulder.position)<.09f) Hand=1;
                if(!Latched) return;
            }
            var hand=Hand==0?left:right;
            if(!hand.valid || !hand.reliable)
            {
                missing+=dt;
                if(missing>.25f) Reset();
                return;
            }
            missing=0;
            if(Vector3.Distance(hand.pose.position,shoulder.position)>.14f) { Reset(); return; }
            if(!measure || Completed) { Animation=0; return; }
            // Two bounded illustrative movements after contact. No precise return gesture
            // is required, so the learner cannot get stuck waiting at the shoulder.
            elapsed=Mathf.Min(2*MovementDuration,elapsed+dt);
            Movements=Mathf.Min(2,Mathf.FloorToInt((elapsed+.00001f)/MovementDuration));
            Progress=elapsed/(2*MovementDuration);
            float phase=Mathf.Repeat(elapsed,MovementDuration)/MovementDuration;
            Animation=Movements>=2?0:Mathf.Sin(phase*2*Mathf.PI)*Mathf.Sin(phase*Mathf.PI);
            if(Movements>=2) { Completed=true; Progress=1; Animation=0; }
        }
    }
}
