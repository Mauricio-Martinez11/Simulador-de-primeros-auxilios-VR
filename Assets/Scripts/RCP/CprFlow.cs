using UnityEngine;

namespace FirstAidVR.Rcp
{
    public enum CprStep { Safety, MovePatient, Response, Breathing, CallForHelp, LowerHand, UpperHand, Ready }

    public struct CprHandSample
    {
        public bool valid, controller, gripValid;
        public float grip;
        // Measured pose only: origin = heel, +Y = dorsum, +Z = fingers.
        public Pose pose;
        public CprHandSample(Pose pose, bool controller = false, float grip = 0, bool gripValid = false)
        { this.pose = pose; this.controller = controller; this.grip = grip; this.gripValid = gripValid; valid = true; }
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
        public bool LowerLatched => LowerHand == 0 && (Step == CprStep.UpperHand || Step == CprStep.Ready);
        public bool StackLatched => Step == CprStep.Ready;
        public bool UsingOcclusion { get; private set; }
        private float held, elapsed, observed, observationClock, leftAway, rightAway, leftLost, bothLost;
        private bool observationStarted;
        private readonly CprTrainingSettings settings;

        public CprFlow(CprTrainingSettings settings) { this.settings = settings; Restart(); }
        public void Restart() { LowerHand = -1; ObservationTimedOut = false; Enter(CprStep.Safety); }
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
            held = elapsed = observed = observationClock = leftAway = rightAway = leftLost = bothLost = Progress = 0;
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
                LeftAligned = Matches(left, lower, settings.captureRadius, settings.palmAngle, settings.fingerAngle);
                RightAligned = false;
                if (Hold(LeftAligned, dt, settings.placementHold))
                { LowerHand = 0; Enter(CprStep.UpperHand); LeftAligned = true; }
            }
            else if (LowerLatched) TickStack(dt, left, right, lower, upper);
        }

        private void TickStack(float dt, CprHandSample left, CprHandSample right, Pose lower, Pose upper)
        {
            bool locked = StackLatched;
            var topTarget = upper;
            if (right.controller) topTarget.position += upper.rotation * Vector3.up * settings.controllerStackExtra;
            bool baseOk = Matches(left, lower, settings.releaseRadius, settings.palmAngle + 15, settings.fingerAngle + 10);
            bool topOk = Matches(right, topTarget, locked ? settings.releaseRadius : settings.captureRadius,
                settings.palmAngle + (locked ? 15 : 0), settings.fingerAngle + (locked ? 10 : 0));
            // Reject hands side by side or at the same height within overlapping capture spheres.
            var stackDelta = right.pose.position - (left.valid ? left.pose.position : lower.position);
            var normal = lower.rotation * Vector3.up;
            float height = Vector3.Dot(stackDelta, normal);
            bool ordered = height >= .012f && Vector3.ProjectOnPlane(stackDelta, normal).magnitude <= .10f;
            topOk &= ordered;
            bool separated = left.valid && right.valid &&
                (stackDelta.magnitude > settings.handSeparation + (right.controller ? settings.controllerStackExtra : 0) || height < -.015f);
            leftLost = left.valid || topOk ? 0 : leftLost + dt;
            bothLost = left.valid || right.valid ? 0 : bothLost + dt;
            leftAway = left.valid && !baseOk ? leftAway + dt : 0;
            rightAway = right.valid && (!topOk || separated) ? rightAway + dt : 0;
            if (bothLost >= settings.bothHandsLostGrace || leftAway >= settings.separationHold)
            { ReleaseLower(); return; }
            // The visible upper hand supports the captured lower hand during overlap.
            if (!left.valid && !topOk && leftLost >= settings.lowerHandOcclusionGrace)
            { ReleaseLower(); return; }
            if (locked && rightAway >= settings.separationHold)
            { Enter(CprStep.UpperHand); LeftAligned = baseOk; return; }

            bool supportedBase = baseOk || (!left.valid && (leftLost < settings.lowerHandOcclusionGrace || topOk));
            UsingOcclusion = supportedBase && (!left.valid || (locked && !right.valid));
            LeftAligned = supportedBase;
            RightAligned = locked || (topOk && ordered && supportedBase && !separated);
            if (locked) { Progress = 1; return; }
            if (Hold(supportedBase && topOk && ordered && !separated, dt, settings.placementHold))
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

