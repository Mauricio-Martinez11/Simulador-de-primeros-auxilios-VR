using UnityEngine;

namespace FirstAidVR.Rcp
{
    public enum CprStep { Safety, MovePatient, Response, Breathing, CallForHelp, LowerHand, UpperHand, Ready }
    public enum CprReleaseReason { None, HandsSeparated, AwayFromChest }

    public struct CprHandSample
    {
        public bool valid, controller, gripValid, reliable;
        public float grip;
        // Measured pose only: origin = heel, +Y = dorsum, +Z = fingers.
        public Pose pose;
        public CprHandSample(Pose pose, bool controller = false, float grip = 0, bool gripValid = false)
        { this.pose = pose; this.controller = controller; this.grip = grip; this.gripValid = gripValid; valid = reliable = true; }
    }

    /// <summary>Visual poses never feed detection. Occlusion is an explicit latch state.</summary>
    public sealed class CprFlow
    {
        public CprStep Step { get; private set; }
        public int LowerHand { get; private set; } = -1;
        public float Progress { get; private set; }
        public bool LeftAligned { get; private set; }
        public bool RightAligned { get; private set; }
        public bool ObservationTimedOut { get; private set; }
        public bool LowerLatched => LowerHand == 1 && (Step == CprStep.UpperHand || Step == CprStep.Ready);
        public bool StackLatched => Step == CprStep.Ready;
        public bool UsingOcclusion { get; private set; }
        public CprReleaseReason LastReleaseReason { get; private set; }
        public bool ReleasePending => topAway > 0 || lowerAway > 0 || separationEvidence > 0;
        // Compression tolerances are deliberately wider than initial placement tolerances.
        public const float CompressionReleaseHold = .18f;
        private float separationEvidence;
        private float held, elapsed, observed, observationClock, lowerAway, topAway, lowerLost, bothLost;
        private bool observationStarted;
        private readonly CprTrainingSettings settings;

        public CprFlow(CprTrainingSettings settings) { this.settings = settings; Restart(); }
        public void Restart() { LowerHand = -1; ObservationTimedOut = false; Enter(CprStep.Safety); }
        // The fixed station reuses only the measured placement/occlusion state machine.
        public void StartPlacement() { LowerHand = -1; LastReleaseReason = CprReleaseReason.None; Enter(CprStep.LowerHand); }
        public void ClearReleaseEvidence() { lowerAway = topAway = separationEvidence = 0; }
        public void Suspend()
        {
            if (LowerLatched) ReleaseLower();
            held = Progress = 0; LeftAligned = RightAligned = UsingOcclusion = false;
        }
        public void Confirm()
        {
            if (elapsed < .75f) return;
            if (Step == CprStep.Safety) Enter(CprStep.MovePatient);
            else if (Step == CprStep.CallForHelp) Enter(CprStep.LowerHand);
        }
        public void PatientPlaced() { if (Step == CprStep.MovePatient) Enter(CprStep.Response); }
        public void BeginRelocation()
        {
            if (Step == CprStep.Safety || Step == CprStep.MovePatient) return;
            LowerHand = -1; Enter(CprStep.MovePatient);
        }
        private void Enter(CprStep step)
        {
            Step = step;
            ClearReleaseEvidence();
            held = elapsed = observed = observationClock = lowerAway = topAway = lowerLost = bothLost = Progress = 0;
            observationStarted = LeftAligned = RightAligned = UsingOcclusion = false;
        }
        private void ReleaseLower() { LowerHand = -1; Enter(CprStep.LowerHand); }

        public static bool Matches(CprHandSample hand, Pose target, float radius, float palmAngle, float fingerAngle)
        {
            return hand.valid && Vector3.Distance(hand.pose.position, target.position) <= radius
                && Vector3.Angle(hand.pose.rotation * Vector3.up, target.rotation * Vector3.up) <= palmAngle
                && Vector3.Angle(hand.pose.rotation * Vector3.forward, target.rotation * Vector3.forward) <= fingerAngle;
        }

        public void Tick(float dt, CprHandSample left, CprHandSample right,
            Pose shoulder, Pose lower, Pose upper, bool lookingAtChest)
        {
            dt = Mathf.Clamp(dt, 0, .1f); elapsed += dt;
            if (Step == CprStep.Response)
            {
                LeftAligned = left.valid && Vector3.Distance(left.pose.position, shoulder.position) < .13f;
                RightAligned = right.valid && Vector3.Distance(right.pose.position, shoulder.position) < .13f;
                if (Hold(LeftAligned || RightAligned, dt, .7f)) Enter(CprStep.Breathing);
            }
            else if (Step == CprStep.Breathing)
            {
                if (lookingAtChest) observationStarted = true;
                if (!observationStarted) return;
                observationClock += dt;
                if (lookingAtChest) observed += dt;
                Progress = Mathf.Clamp01(observed / settings.breathingObservation);
                if (observed >= settings.breathingObservation || observationClock >= 10)
                {
                    ObservationTimedOut = observed < settings.breathingObservation;
                    Enter(CprStep.CallForHelp);
                }
            }
            else if (Step == CprStep.LowerHand)
            {
                RightAligned = Matches(right, lower, settings.captureRadius, settings.palmAngle, settings.fingerAngle);
                LeftAligned = false;
                if (RightAligned)
                { LowerHand = 1; Enter(CprStep.UpperHand); RightAligned = true; }
            }
            else if (LowerLatched) TickStack(dt, left, right, lower, upper);
        }

        public void TickCompression(float dt, CprHandSample left, CprHandSample right, Pose lower, Pose upper)
        {
            if (!StackLatched) return;
            dt = Mathf.Clamp(dt, 0, .1f);
            var normal = lower.rotation * Vector3.up;
            bool seeLeft = left.valid && left.reliable, seeRight = right.valid && right.reliable;
            bool separated = false;
            if (seeLeft && seeRight)
            {
                var delta = left.pose.position - right.pose.position;
                separated = delta.magnitude > Mathf.Max(.14f, settings.handSeparation)
                    + (left.controller ? settings.controllerStackExtra : 0);
            }
            // Occluded fingers can flip palm orientation or swap apparent stack order.
            // Neither is evidence that the user deliberately separated their hands.
            topAway = seeLeft && ClearlyAway(left, upper, normal) ? topAway + dt : 0;
            lowerAway = seeRight && ClearlyAway(right, lower, normal) ? lowerAway + dt : 0;
            separationEvidence = separated ? separationEvidence + dt : 0;
            if (separationEvidence >= CompressionReleaseHold || topAway >= CompressionReleaseHold || lowerAway >= CompressionReleaseHold)
            {
                var reason = separationEvidence >= CompressionReleaseHold ? CprReleaseReason.HandsSeparated : CprReleaseReason.AwayFromChest;
                ReleaseLower(); LastReleaseReason = reason; return;
            }
            UsingOcclusion = !seeLeft || !seeRight;
            LeftAligned = RightAligned = true;
        }

        private bool ClearlyAway(CprHandSample hand, Pose target, Vector3 normal)
        {
            var offset = hand.pose.position - target.position;
            return Vector3.ProjectOnPlane(offset, normal).magnitude > Mathf.Max(.20f, settings.releaseRadius)
                || Vector3.Dot(offset, normal) > .18f || Vector3.Dot(offset, normal) < -.20f;
        }

        private void TickStack(float dt, CprHandSample left, CprHandSample right, Pose lower, Pose upper)
        {
            bool locked = StackLatched;
            var topTarget = upper;
            if (left.controller) topTarget.position += upper.rotation * Vector3.up * settings.controllerStackExtra;
            bool baseOk = Matches(right, lower, settings.releaseRadius, settings.palmAngle + 15, settings.fingerAngle + 10);
            bool topOk = Matches(left, topTarget, locked ? settings.releaseRadius : settings.upperHandCaptureRadius,
                settings.palmAngle + (locked ? 15 : 10), settings.fingerAngle + 10);
            // Reject hands side by side or at the same height within overlapping capture spheres.
            var stackDelta = left.pose.position - (right.valid ? right.pose.position : lower.position);
            var normal = lower.rotation * Vector3.up;
            float height = Vector3.Dot(stackDelta, normal);
            bool ordered = height >= .012f && height <= settings.handSeparation + (left.controller ? settings.controllerStackExtra : 0)
                && Vector3.ProjectOnPlane(stackDelta, normal).magnitude <= .10f;
            topOk &= ordered;
            bool separated = left.valid && right.valid &&
                (stackDelta.magnitude > settings.handSeparation + (left.controller ? settings.controllerStackExtra : 0) || height < -.015f);
            lowerLost = right.valid || topOk ? 0 : lowerLost + dt;
            bothLost = left.valid || right.valid ? 0 : bothLost + dt;
            lowerAway = right.valid && !baseOk ? lowerAway + dt : 0;
            topAway = left.valid && (!topOk || separated) ? topAway + dt : 0;
            if ((!locked && bothLost >= settings.bothHandsLostGrace) || lowerAway >= settings.separationHold)
            { ReleaseLower(); return; }
            // The visible upper hand supports the captured lower hand during overlap.
            if (!locked && !right.valid && !topOk && lowerLost >= settings.lowerHandOcclusionGrace)
            { ReleaseLower(); return; }
            if (locked && topAway >= settings.separationHold)
            {
                if (!baseOk) ReleaseLower();
                else { Enter(CprStep.UpperHand); RightAligned = true; }
                return;
            }

            bool supportedBase = baseOk || (!right.valid && (locked || lowerLost < settings.lowerHandOcclusionGrace || topOk));
            UsingOcclusion = locked ? !right.valid || !left.valid : supportedBase && !right.valid;
            RightAligned = supportedBase;
            LeftAligned = topOk || locked && !left.valid;
            if (locked) { Progress = 1; return; }
            Progress = 0;
            if (supportedBase && topOk && !separated)
            {
                bool inferred = UsingOcclusion;
                Enter(CprStep.Ready); LeftAligned = RightAligned = true; Progress = 1; UsingOcclusion = inferred;
            }
        }

        private bool Hold(bool correct, float dt, float duration)
        {
            held = correct ? held + dt : 0;
            Progress = Mathf.Clamp01(held / Mathf.Max(.01f, duration));
            return held >= duration;
        }
    }
}



