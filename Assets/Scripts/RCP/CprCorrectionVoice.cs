using System.Collections.Generic;
using UnityEngine;

namespace FirstAidVR.Rcp
{
    // No queue: only the current, still relevant problem can be spoken.
    public sealed class CprCorrectionVoice
    {
        public const float QuietSeconds=8, RepeatSeconds=22;
        private readonly Dictionary<string,float> last=new Dictionary<string,float>();
        private float clock,quiet=QuietSeconds,held;
        private string previous;
        public string Tick(float dt,string candidate,bool speaking,bool active)
        {
            if(!active) { previous=null; held=0; return null; }
            dt=Mathf.Clamp(dt,0,.12f); clock+=dt;
            if(speaking) quiet=0; else quiet+=dt;
            if(candidate!=previous) { previous=candidate; held=0; }
            held+=dt;
            if(speaking || candidate==null || held<.65f || quiet<QuietSeconds) return null;
            if(last.TryGetValue(candidate,out float when) && clock-when<RepeatSeconds) return null;
            last[candidate]=clock; quiet=0; return candidate;
        }
        public static string Candidate(CprPracticeSession s)
        {
            if(s.Closed || s.Step!=CprPracticeStep.Compressions || s.Explaining || s.Paused) return null;
            if(!s.Placement.StackLatched)
                return s.Placement.LastReleaseReason==CprReleaseReason.HandsSeparated ? "Separate"
                    : s.Placement.LastReleaseReason==CprReleaseReason.AwayFromChest ? "Away" : null;
            if(s.Placement.ReleasePending) return null;
            if(!s.Detector.HasTracking) return "Lost";
            if(!s.Detector.LocationCorrect) return "Away";
            // Completed-cycle metrics take precedence over phase guidance.
            if(s.Events.Count>0)
            {
                var e=s.Events[s.Events.Count-1];
                if(s.TotalSeconds-e.time<2.5f)
                {
                    if(e.rateEvaluable && !e.rateCorrect) return e.rate>s.Settings.maximumRate?"Fast":"Slow";
                    if(!e.fullReturn) return "Return";
                    if(!e.depthCorrect) return e.depth<s.Settings.minimumVirtualDepth?"Shallow":"Deep";
                }
            }
            if(s.PacingEvents.Count>0)
            {
                var e=s.PacingEvents[s.PacingEvents.Count-1];
                if(s.TotalSeconds-e.seconds<1.5f && e.result!="OnTime")
                    return e.result=="ExtraPress"?"Fast":"Timing";
            }
            return null;
        }
    }
}
