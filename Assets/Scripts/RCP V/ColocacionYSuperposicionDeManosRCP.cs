using UnityEngine;

namespace FirstAidVR.Rcp.Consulta
{
    public enum PasoRCP { Safety, MovePatient, Response, Breathing, CallForHelp, LowerHand, UpperHand, Ready }
    public enum MotivoDeLiberacionRCP { None, HandsSeparated, AwayFromChest }

    public struct MuestraDeManoRCP
    {
        public bool valid, controller, gripValid, reliable;
        public float grip;
        // Solo pose medida: origen = talón de la mano, +Y = dorso, +Z = dedos.
        public Pose pose;
        public MuestraDeManoRCP(Pose pose, bool controller = false, float grip = 0, bool gripValid = false)
        { this.pose = pose; this.controller = controller; this.grip = grip; this.gripValid = gripValid; valid = reliable = true; }
    }

    /// <summary>Las poses visuales nunca alimentan la detección. La oclusión es un estado de retención explícito.</summary>
    public sealed class ColocacionYSuperposicionDeManosRCP
    {
        public PasoRCP Step { get; private set; }
        public int LowerHand { get; private set; } = -1;
        public float Progress { get; private set; }
        public bool LeftAligned { get; private set; }
        public bool RightAligned { get; private set; }
        public bool ObservationTimedOut { get; private set; }
        public bool LowerLatched => LowerHand == 1 && (Step == PasoRCP.UpperHand || Step == PasoRCP.Ready);
        public bool StackLatched => Step == PasoRCP.Ready;
        public bool UsingOcclusion { get; private set; }
        public MotivoDeLiberacionRCP LastReleaseReason { get; private set; }
        public bool ReleasePending => topAway > 0 || lowerAway > 0 || separationEvidence > 0;
        // Las tolerancias de compresión son deliberadamente más amplias que las de colocación inicial.
        public const float CompressionReleaseHold = .18f;
        private float separationEvidence;
        private float held, elapsed, observed, observationClock, lowerAway, topAway, lowerLost, bothLost;
        private bool observationStarted;
        private readonly ConfiguracionDeCriteriosRCP settings;

        public ColocacionYSuperposicionDeManosRCP(ConfiguracionDeCriteriosRCP settings) { this.settings = settings; Restart(); }
        public void Restart() { LowerHand = -1; ObservationTimedOut = false; Enter(PasoRCP.Safety); }
        // La estación fija reutiliza solo la máquina de estados de colocación y oclusión medidas.
        public void StartPlacement() { LowerHand = -1; LastReleaseReason = MotivoDeLiberacionRCP.None; Enter(PasoRCP.LowerHand); }
        public void ClearReleaseEvidence() { lowerAway = topAway = separationEvidence = 0; }
        public void Suspend()
        {
            if (LowerLatched) ReleaseLower();
            held = Progress = 0; LeftAligned = RightAligned = UsingOcclusion = false;
        }
        public void Confirm()
        {
            if (elapsed < .75f) return;
            if (Step == PasoRCP.Safety) Enter(PasoRCP.MovePatient);
            else if (Step == PasoRCP.CallForHelp) Enter(PasoRCP.LowerHand);
        }
        public void PatientPlaced() { if (Step == PasoRCP.MovePatient) Enter(PasoRCP.Response); }
        public void BeginRelocation()
        {
            if (Step == PasoRCP.Safety || Step == PasoRCP.MovePatient) return;
            LowerHand = -1; Enter(PasoRCP.MovePatient);
        }
        private void Enter(PasoRCP step)
        {
            Step = step;
            ClearReleaseEvidence();
            held = elapsed = observed = observationClock = lowerAway = topAway = lowerLost = bothLost = Progress = 0;
            observationStarted = LeftAligned = RightAligned = UsingOcclusion = false;
        }
        private void ReleaseLower() { LowerHand = -1; Enter(PasoRCP.LowerHand); }

        public static bool Matches(MuestraDeManoRCP hand, Pose target, float radius, float palmAngle, float fingerAngle)
        {
            return hand.valid && Vector3.Distance(hand.pose.position, target.position) <= radius
                && Vector3.Angle(hand.pose.rotation * Vector3.up, target.rotation * Vector3.up) <= palmAngle
                && Vector3.Angle(hand.pose.rotation * Vector3.forward, target.rotation * Vector3.forward) <= fingerAngle;
        }

        public void Tick(float dt, MuestraDeManoRCP left, MuestraDeManoRCP right,
            Pose shoulder, Pose lower, Pose upper, bool lookingAtChest)
        {
            dt = Mathf.Clamp(dt, 0, .1f); elapsed += dt;
            if (Step == PasoRCP.Response)
            {
                LeftAligned = left.valid && Vector3.Distance(left.pose.position, shoulder.position) < .13f;
                RightAligned = right.valid && Vector3.Distance(right.pose.position, shoulder.position) < .13f;
                if (Hold(LeftAligned || RightAligned, dt, .7f)) Enter(PasoRCP.Breathing);
            }
            else if (Step == PasoRCP.Breathing)
            {
                if (lookingAtChest) observationStarted = true;
                if (!observationStarted) return;
                observationClock += dt;
                if (lookingAtChest) observed += dt;
                Progress = Mathf.Clamp01(observed / settings.breathingObservation);
                if (observed >= settings.breathingObservation || observationClock >= 10)
                {
                    ObservationTimedOut = observed < settings.breathingObservation;
                    Enter(PasoRCP.CallForHelp);
                }
            }
            else if (Step == PasoRCP.LowerHand)
            {
                RightAligned = Matches(right, lower, settings.captureRadius, settings.palmAngle, settings.fingerAngle);
                LeftAligned = false;
                if (RightAligned)
                { LowerHand = 1; Enter(PasoRCP.UpperHand); RightAligned = true; }
            }
            else if (LowerLatched) TickStack(dt, left, right, lower, upper);
        }

        public void TickCompression(float dt, MuestraDeManoRCP left, MuestraDeManoRCP right, Pose lower, Pose upper)
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
            // Los dedos ocultos pueden invertir la orientación de la palma o el orden aparente de las manos superpuestas.
            // Ninguno de estos cambios indica que el usuario haya separado las manos intencionalmente.
            topAway = seeLeft && ClearlyAway(left, upper, normal) ? topAway + dt : 0;
            lowerAway = seeRight && ClearlyAway(right, lower, normal) ? lowerAway + dt : 0;
            separationEvidence = separated ? separationEvidence + dt : 0;
            if (separationEvidence >= CompressionReleaseHold || topAway >= CompressionReleaseHold || lowerAway >= CompressionReleaseHold)
            {
                var reason = separationEvidence >= CompressionReleaseHold ? MotivoDeLiberacionRCP.HandsSeparated : MotivoDeLiberacionRCP.AwayFromChest;
                ReleaseLower(); LastReleaseReason = reason; return;
            }
            UsingOcclusion = !seeLeft || !seeRight;
            LeftAligned = RightAligned = true;
        }

        private bool ClearlyAway(MuestraDeManoRCP hand, Pose target, Vector3 normal)
        {
            var offset = hand.pose.position - target.position;
            return Vector3.ProjectOnPlane(offset, normal).magnitude > Mathf.Max(.20f, settings.releaseRadius)
                || Vector3.Dot(offset, normal) > .18f || Vector3.Dot(offset, normal) < -.20f;
        }

        private void TickStack(float dt, MuestraDeManoRCP left, MuestraDeManoRCP right, Pose lower, Pose upper)
        {
            bool locked = StackLatched;
            var topTarget = upper;
            if (left.controller) topTarget.position += upper.rotation * Vector3.up * settings.controllerStackExtra;
            bool baseOk = Matches(right, lower, settings.releaseRadius, settings.palmAngle + 15, settings.fingerAngle + 10);
            bool topOk = Matches(left, topTarget, locked ? settings.releaseRadius : settings.upperHandCaptureRadius,
                settings.palmAngle + (locked ? 15 : 10), settings.fingerAngle + 10);
            // Rechazar manos una al lado de la otra o a la misma altura dentro de esferas de captura superpuestas.
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
            // La mano superior visible sostiene la mano inferior detectada durante la superposición.
            if (!locked && !right.valid && !topOk && lowerLost >= settings.lowerHandOcclusionGrace)
            { ReleaseLower(); return; }
            if (locked && topAway >= settings.separationHold)
            {
                if (!baseOk) ReleaseLower();
                else { Enter(PasoRCP.UpperHand); RightAligned = true; }
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
                Enter(PasoRCP.Ready); LeftAligned = RightAligned = true; Progress = 1; UsingOcclusion = inferred;
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



