using System;
using System.Collections.Generic;
using UnityEngine;

namespace FirstAidVR.Rcp
{
    public enum CprCaptureState { Recovering, OutsideZone, Ready, Compressing }

    public readonly struct CprCompressionEvent
    {
        public readonly float time, depth, rate;
        public readonly bool fullReturn, rateEvaluable, depthCorrect, rateCorrect;
        public CprCompressionEvent(float time, float depth, bool fullReturn, float rate, CprTrainingSettings settings)
        {
            this.time = time; this.depth = depth; this.fullReturn = fullReturn; this.rate = rate;
            rateEvaluable = rate > 0;
            depthCorrect = depth >= settings.minimumVirtualDepth - .0001f && depth <= settings.maximumVirtualDepth + .0001f;
            rateCorrect = rateEvaluable && rate >= settings.minimumRate - .01f && rate <= settings.maximumRate + .01f;
        }
    }

    /// <summary>Measured palm travel only. Ghosts and the animated chest never enter this detector.</summary>
    public sealed class CprCompressionDetector
    {
        private enum Phase { Rest, Down, Up }
        private readonly CprTrainingSettings settings;
        private readonly List<float> times = new List<float>();
        private readonly float[] rest = new float[2];
        private readonly bool[] calibrated = new bool[2];
        private int source = -1;
        private Phase phase;
        private bool haveSample, ready, pauseReported;
        private float previousDepth, peak, trough, cycleStart, returnHeld, lastEvent = -1, continuityStart;
        private float gapSeconds, displayDepth, displayAge=float.PositiveInfinity;
        private bool displayInitialized;
        private float recoveryTop,recoveryBottom,recoveryLastDepth;
        private float descentStarted=-1;
        private bool recoveryObserved;
        public string RecoveryReason { get; private set; } = "Coloca las manos";
        public event Action<CprCompressionEvent> Compression;
        public event Action<float> Pressed;
        public event Action PauseDetected;
        public CprCaptureState State { get; private set; }
        public float Depth { get; private set; }
        public float Rate { get; private set; }
        public bool RateEvaluable => Rate > 0;
        public bool HasTracking { get; private set; }
        public bool LocationCorrect { get; private set; }
        public bool UsingSingleHand { get; private set; }
        public bool Calibrated => calibrated[0] && calibrated[1];
        public float DisplayDepth => displayDepth;
        public bool DisplayAvailable => displayInitialized;
        public bool DisplayFresh => displayInitialized && HasTracking && LocationCorrect && displayAge<=.15f;
        public const float BriefTrackingGap=.10f;

        public CprCompressionDetector(CprTrainingSettings settings) { this.settings = settings; }

        public bool Calibrate(CprHandSample left, CprHandSample right, Pose chest)
        {
            // Both actual hand poses must have been observed at rest; never calibrate a hidden ghost.
            if (!left.valid || !right.valid) return false;
            var normal = chest.rotation * Vector3.up;
            rest[0] = Vector3.Dot(left.pose.position - chest.position, normal);
            rest[1] = Vector3.Dot(right.pose.position - chest.position, normal);
            calibrated[0] = calibrated[1] = true;
            displayInitialized=false;
            Suspend();
            return true;
        }

        // Sequential calibration accommodates the real lower-hand occlusion during stacking.
        public void CaptureRest(int hand, CprHandSample sample, Pose chest)
        {
            if (!sample.valid || hand < 0 || hand > 1) return;
            rest[hand] = Vector3.Dot(sample.pose.position - chest.position, chest.rotation * Vector3.up);
            calibrated[hand] = true;
            displayInitialized=false;
        }

        public void Suspend()
        {
            phase = Phase.Rest; ready = haveSample = false; source = -1; descentStarted=-1;
            returnHeld = Depth = Rate = 0; times.Clear(); lastEvent = -1;
            gapSeconds=0; recoveryObserved=false;
            HasTracking = LocationCorrect = false; pauseReported = false; State = CprCaptureState.Recovering;
        }

        public void Tick(float dt, float time, CprHandSample left, CprHandSample right, Pose chest, bool participantPosition, bool stackEstablished = false)
        {
            displayAge+=Mathf.Max(0,dt);
            if (stackEstablished)
            {
                if (!left.reliable) left = default;
                if (!right.reliable) right = default;
                // Placement owns separation detection. The visible top hand drives travel;
                // the occluded lower hand must not invalidate a sound measurement.
                // Keep a continuously observed source. Brief alternation between hands
                // must not reset the cycle at every camera frame.
                if (source==1 && right.valid) left=default;
                else if (left.valid) right = default;
            }
            HasTracking = left.valid || right.valid;
            UsingSingleHand = left.valid != right.valid;
            if (!Calibrated || dt <= 0 || dt > .12f)
            { Suspend(); return; }
            if(!HasTracking)
            {
                gapSeconds+=dt;
                if(gapSeconds>BriefTrackingGap) Suspend();
                HasTracking=LocationCorrect=false; State=CprCaptureState.Recovering;
                return;
            }
            LocationCorrect = participantPosition && InZone(left, chest, stackEstablished) && InZone(right, chest, stackEstablished);
            if (left.valid && right.valid)
            {
                var delta = left.pose.position - right.pose.position;
                float height = Vector3.Dot(delta, chest.rotation * Vector3.up);
                LocationCorrect &= height > .008f && height < settings.handSeparation + (left.controller ? settings.controllerStackExtra : 0)
                    && Vector3.ProjectOnPlane(delta, chest.rotation * Vector3.up).magnitude < .10f;
            }
            if (!LocationCorrect)
            {
                Suspend(); HasTracking = true; State = CprCaptureState.OutsideZone;
                return;
            }
            // The upper left hand stays visible when Quest loses the lower right hand.
            int nextSource = left.valid ? 0 : 1;
            var sample = nextSource == 0 ? left : right;
            float depth = rest[nextSource] - Vector3.Dot(sample.pose.position - chest.position, chest.rotation * Vector3.up);
            bool compatibleSwitch=haveSample && Mathf.Abs(depth-previousDepth)<=.012f;
            bool discontinuity = source != nextSource && !compatibleSwitch
                || haveSample && Mathf.Abs(depth-previousDepth)>Mathf.Max(.025f,(dt+gapSeconds)*1.5f);
            gapSeconds=0;
            if (discontinuity)
            {
                Suspend(); source = nextSource; continuityStart = time;
                HasTracking = LocationCorrect = true; UsingSingleHand = left.valid != right.valid;
            }
            source=nextSource;
            if (depth < -.06f || depth > .16f)
            { Suspend(); HasTracking = true; State = CprCaptureState.OutsideZone; return; }
            previousDepth = depth; haveSample = true; Depth = Mathf.Max(0, depth);
            // Display uses measured samples even while cycle detection is re-arming.
            // A short low-pass filter reduces visual jitter without changing cycle peaks.
            float alpha=1-Mathf.Exp(-dt/.025f);
            displayDepth=displayInitialized?Mathf.Lerp(displayDepth,Depth,alpha):Depth;
            displayInitialized=true; displayAge=0;
            if (!ready)
            {
                returnHeld = Mathf.Abs(depth) <= settings.compressionReturn ? returnHeld + dt : 0;
                State = CprCaptureState.Recovering;
                RecoveryReason="Vuelve al reposo para reconocer la siguiente bajada";
                // Placement may have sampled a hand before it settled. Recover the baseline
                // only after an observed upward excursion and a small downward reversal.
                // A stationary hand held down cannot recalibrate or manufacture a cycle.
                if(!recoveryObserved) { recoveryLastDepth=recoveryBottom=recoveryTop=depth; recoveryObserved=true; }
                bool turningDown=depth>recoveryLastDepth+.0005f; recoveryLastDepth=depth;
                recoveryBottom=Mathf.Max(recoveryBottom,depth);
                recoveryTop=Mathf.Min(recoveryTop,depth);
                if(turningDown && recoveryBottom-recoveryTop>=settings.compressionActivation && depth-recoveryTop>=.004f
                    && recoveryTop>=-.035f && recoveryTop<=.035f && depth<recoveryBottom-.006f)
                {
                    rest[nextSource]-=recoveryTop;
                    depth-=recoveryTop; previousDepth=depth; Depth=Mathf.Max(0,depth);
                    ready=true; phase=Phase.Rest; continuityStart=time;
                    RecoveryReason="Referencia recuperada";
                }
                // Two or more measured rest samples are enough. Requiring 160 ms
                // continuously at rest can prevent arming during uninterrupted CPR.
                if (returnHeld >= .04f) { ready = true; continuityStart = time; State = CprCaptureState.Ready; }
                if(!ready) return;
            }
            State = phase == Phase.Rest ? CprCaptureState.Ready : CprCaptureState.Compressing;
            float reference = lastEvent >= 0 ? lastEvent : continuityStart;
            if (lastEvent >= 0 && time - reference > settings.practicePauseSeconds && !pauseReported)
            {
                pauseReported = true; times.Clear(); Rate = 0;
                PauseDetected?.Invoke();
            }
            if (phase == Phase.Rest)
            {
                if(depth<=.003f) descentStarted=-1;
                else if(descentStarted<0) descentStarted=time;
                if (depth >= settings.compressionActivation)
                { phase = Phase.Down; peak = depth; cycleStart = time; Pressed?.Invoke(descentStarted>=0?descentStarted:time); }
                return;
            }
            if (phase == Phase.Down)
            {
                peak = Mathf.Max(peak, depth);
                if (peak - depth >= .006f) { phase = Phase.Up; trough = depth; }
            }
            if (phase == Phase.Up)
            {
                trough = Mathf.Min(trough, depth);
                if (depth <= settings.compressionReturn)
                { Emit(time, true); phase = Phase.Rest; }
                else if (depth - trough >= .012f)
                { Emit(time, false); phase = Phase.Down; peak = depth; cycleStart = time; Pressed?.Invoke(time); }
            }
        }

        private bool InZone(CprHandSample hand, Pose chest, bool stackEstablished)
        {
            if (!hand.valid) return true; // Only the visible, already calibrated source is evaluated.
            var offset = hand.pose.position - chest.position;
            return Vector3.ProjectOnPlane(offset, chest.rotation * Vector3.up).magnitude <= settings.compressionZoneRadius
                && (stackEstablished || (Vector3.Angle(hand.pose.rotation * Vector3.up, chest.rotation * Vector3.up) <= settings.palmAngle + 15
                && Vector3.Angle(hand.pose.rotation * Vector3.forward, chest.rotation * Vector3.forward) <= settings.fingerAngle + 10));
        }

        private void Emit(float time, bool fullReturn)
        {
            // Reject very short spikes. Fast deliberate cycles still count and receive rhythm feedback.
            if (time - cycleStart < .16f || (lastEvent >= 0 && time - lastEvent < .25f)) return;
            if (lastEvent >= 0 && time - lastEvent > settings.practicePauseSeconds) times.Clear();
            times.Add(time);
            while (times.Count > settings.rateWindow) times.RemoveAt(0);
            Rate = times.Count >= 3 ? 60 * (times.Count - 1) / (times[times.Count - 1] - times[0]) : 0;
            lastEvent = time; pauseReported = false;
            Compression?.Invoke(new CprCompressionEvent(time, peak, fullReturn, Rate, settings));
        }
    }
}
