using System.Collections.Generic;
using UnityEngine;

namespace FirstAidVR.Rcp
{
    public enum CprPracticeStep { Welcome, Safety, Response, Breathing, Help, Placement, Compressions, Results }
    public enum CprFeedbackKind { Instruction, Success, Correction, Tracking }
    public struct CprPracticeObservation { public bool safetyLeft, safetyRight, helpRequested; }

    /// <summary>Measured attempt state, consumed by presentation and the persistent report.</summary>
    public sealed class CprPracticeSession
    {
        public readonly CprTrainingSettings Settings;
        public readonly CprFlow Placement;
        public readonly CprCompressionDetector Detector;
        public readonly CprPacing Pacing;
        private readonly List<CprPacingEvent> pacingEvents=new List<CprPacingEvent>();
        public IReadOnlyList<CprPacingEvent> PacingEvents => pacingEvents;
        private readonly List<CprCompressionEvent> events = new List<CprCompressionEvent>();
        public IReadOnlyList<CprCompressionEvent> Events => events;
        private readonly List<CprAttemptIncident> incidents = new List<CprAttemptIncident>();
        public IReadOnlyList<CprAttemptIncident> Incidents => incidents;
        public string AttemptId { get; } = System.Guid.NewGuid().ToString("N");
        public string StartedAt { get; } = System.DateTimeOffset.Now.ToString("o");
        public string EndedAt { get; private set; }
        public string SavedReportPath { get; private set; }
        public string SaveError { get; private set; }
        public CprPracticeStep Step { get; private set; }
        public bool Explaining { get; private set; }
        public bool ResponseTouching => ShoulderContact.Latched;
        public CprShoulderContact ShoulderContact { get; } = new CprShoulderContact();
        public bool StepPassed { get; private set; }
        public bool Paused { get; private set; }
        public bool Closed => Step == CprPracticeStep.Results;
        public bool Completed { get; private set; }
        public float TotalSeconds { get; private set; }
        public float PracticeSeconds { get; private set; }
        public float UntrackedSeconds { get; private set; }
        public float LocationSeconds { get; private set; }
        public float GoodLocationSeconds { get; private set; }
        public int SequenceErrors { get; private set; }
        public int LocationErrors { get; private set; }
        public int HandSeparationErrors { get; private set; }
        public int PauseErrors { get; private set; }
        public int TrackingInterruptions { get; private set; }
        public int DepthHits { get; private set; }
        public int RateHits { get; private set; }
        public int RateSamples { get; private set; }
        public int RecoilHits { get; private set; }
        public int Shallow { get; private set; }
        public int Excessive { get; private set; }
        public int Slow { get; private set; }
        public int Fast { get; private set; }
        public int StepsDone { get; private set; }
        public int Count => events.Count;
        public float Progress { get; private set; }
        public string Feedback { get; private set; } = "Acércate al costado del paciente. La guía empieza sola.";
        public CprFeedbackKind FeedbackKind { get; private set; }
        public int FeedbackVersion { get; private set; }
        public int SafetySeenMask { get; private set; }
        public bool CanAdjustHeight => !Closed && (Step != CprPracticeStep.Compressions || Paused);
        public float BreathingDuration => Mathf.Clamp(Settings.breathingObservation, 3, 10);
        public bool BreathingStarted => observing;
        public float BreathingObservedSeconds => Mathf.Min(observed, BreathingDuration);
        public float BreathingElapsedSeconds => Mathf.Min(observationClock, 10);
        public bool CanChooseBreathing => Step == CprPracticeStep.Breathing && !Explaining && (observed >= BreathingDuration || observationClock >= 10);
        public float MeanDepth { get { float sum = 0; foreach (var e in events) sum += e.depth; return Count > 0 ? sum / Count : 0; } }
        public float MeanRate { get { float sum = 0; foreach (var e in events) if (e.rateEvaluable) sum += e.rate; return RateSamples > 0 ? sum / RateSamples : 0; } }
        public CprScoreBreakdown Breakdown => CprScoreBreakdown.From(this);
        public int? Score { get { var b = Breakdown; return b.evaluable ? b.total : (int?)null; } }
        public void ReportSaved(string path) { SavedReportPath = path; SaveError = null; }
        public void ReportFailed(string error) { SaveError = error; }
        private float held, observed, observationClock, noticeUntil, badLocationHeld, stepClock, transitionClock, leftLook, rightLook;
        private bool observing, wasTrackingLost, locationReported, clockWasInterrupted, measurementStarted;
        private bool presentationGates, transitionBlocked;
        public void SetPresentationGate(bool explaining, bool blockTransition)
        {
            presentationGates=true;
            if(explaining && !Explaining) { Detector.Suspend(); Pacing.Reset(); }
            Explaining=explaining; transitionBlocked=blockTransition;
        }

        public CprPracticeSession(CprTrainingSettings settings)
        {
            Settings = settings; Step = CprPracticeStep.Welcome;
            Placement = new CprFlow(settings); Placement.StartPlacement();
            Detector = new CprCompressionDetector(settings);
            Pacing = new CprPacing((settings.minimumRate+settings.maximumRate)*.5f);
            Detector.Pressed += Pacing.ObservePress;
            Pacing.PressObserved += pacingEvents.Add;
            Detector.Compression += OnCompression;
            Detector.PauseDetected += () =>
            {
                if (Closed) return;
                PauseErrors++;
                LogIncident("pause", "Pausa prolongada con seguimiento válido.");
                Notify("Pausa prolongada. Retoma el movimiento siguiendo el pulso.", CprFeedbackKind.Correction);
            };
        }

        public void Primary()
        {
            // Deliberately no progression through UI clicks. Only observed actions advance the lesson.
        }

        public void Alternative()
        {
            // Retained for compatibility with serialized actions from the previous station.
        }

        public void Tick(float dt, CprHandSample left, CprHandSample right, Pose shoulder, Pose chest, Pose upper,
            bool lookingAtChest, bool participantPosition, bool focus, CprPracticeObservation observation = default)
        {
            ShoulderContact.Tick(dt,left,right,shoulder,Step==CprPracticeStep.Response && focus && !Paused && !Closed,
                !Explaining && !StepPassed);
            if (Closed || dt <= 0) return;
            if (!focus || Paused)
            {
                if (!clockWasInterrupted)
                {
                    Detector.Suspend(); Pacing.Reset(); Placement.ClearReleaseEvidence(); held=leftLook=rightLook=0; clockWasInterrupted = true;
                    LogIncident(Paused ? "manual_pause" : "focus_lost", "Medición suspendida sin penalización; se conserva la unión.");
                }
                return;
            }
            clockWasInterrupted = false;
            if(Explaining || StepPassed && transitionBlocked)
            {
                // Keep validating the established stack while listening, without scoring.
                // A pupil may relax or move their hands during a long explanation.
                if(Placement.StackLatched && (Step==CprPracticeStep.Placement || Step==CprPracticeStep.Compressions))
                {
                    Placement.TickCompression(Mathf.Min(dt,.12f),left,right,chest,upper);
                    if(!Placement.StackLatched)
                    {
                        Detector.Suspend();
                        if(Step==CprPracticeStep.Placement) StepPassed=false;
                    }
                }
                if((Step==CprPracticeStep.Placement || Step==CprPracticeStep.Compressions) && !Placement.StackLatched)
                    ObservePlacement(Mathf.Min(dt,.12f),left,right,shoulder,chest,upper);
                return;
            }
            dt=Mathf.Min(dt,.12f); TotalSeconds += dt; stepClock+=dt;
            if (StepPassed)
            {
                transitionClock+=dt;
                if(transitionClock>=(presentationGates?0:1.25f)) Enter(Step+1);
                return;
            }
            if(Step==CprPracticeStep.Welcome)
            {
                held=participantPosition?held+dt:0; Progress=Mathf.Clamp01(held/4);
                if(held>=4) Enter(CprPracticeStep.Safety);
                return;
            }
            if(Step==CprPracticeStep.Safety)
            {
                leftLook=observation.safetyLeft?leftLook+dt:0; rightLook=observation.safetyRight?rightLook+dt:0;
                if(leftLook>=.65f) SafetySeenMask|=1;
                if(rightLook>=.65f) SafetySeenMask|=2;
                Progress=SafetySeenMask==3?1:SafetySeenMask==0?0:.5f;
                if(SafetySeenMask==3) Pass("Entorno observado. Acércate y comprueba si responde.");
                else Status("Gira la cabeza a ambos lados; mantén cada mirada un instante.",CprFeedbackKind.Instruction);
                return;
            }
            if (Step == CprPracticeStep.Response)
            {
                Progress=ShoulderContact.Progress;
                if(ShoulderContact.Completed) Pass("No responde. Ahora observa su respiración.");
                else Status(ResponseTouching ? "Mantén la mano apoyada · comprobando respuesta."
                    : "Acerca una mano al hombro señalado y pregunta si está bien.",CprFeedbackKind.Instruction);
            }
            else if (Step == CprPracticeStep.Breathing)
            {
                if (lookingAtChest) observing = true;
                if (observing) observationClock += dt;
                if (lookingAtChest) observed += dt;
                Progress = Mathf.Clamp01(observed / BreathingDuration);
                if(CanChooseBreathing) Pass("No respira normalmente. Solicita ayuda.");
                else Status(observing ? $"Comprueba la respiración · {BreathingObservedSeconds:0.0} / {BreathingDuration:0} s · sigue el aro sobre el pecho."
                    : "Mira la cara y el pecho. Comprueba si respira normalmente, sin superar 10 segundos.", CprFeedbackKind.Instruction);
            }
            else if(Step==CprPracticeStep.Help)
            {
                if(observation.helpRequested) Pass("Ayuda solicitada en la simulación. Prepara tus manos.");
                else Status("Toma el teléfono y acércalo a tu oído. Pide ayuda.",CprFeedbackKind.Instruction);
            }
            else if (Step == CprPracticeStep.Placement)
            {
                ObservePlacement(dt,left,right,shoulder,chest,upper);
                if (Placement.StackLatched && Detector.Calibrated)
                {
                    if(presentationGates) Pass("Manos reconocidas. Mantén la postura y escucha la explicación.");
                    else Enter(CprPracticeStep.Compressions);
                    StepsDone = 5;
                }
                else Status(!Placement.LowerLatched && !right.valid ? "No se detecta la mano derecha. Muéstrala abierta delante del visor."
                    : Placement.LowerLatched ? "Derecha colocada. Acerca la izquierda encima, alineada con la guía."
                    : "Acerca la mano derecha al centro del pecho, palma abajo y dedos en la dirección de la guía.",
                    !left.valid && !right.valid ? CprFeedbackKind.Tracking : CprFeedbackKind.Instruction);
            }
            else if (Step == CprPracticeStep.Compressions)
            {
                Pacing.Tick(dt,TotalSeconds);
                bool wasStacked = Placement.StackLatched;
                Placement.TickCompression(dt,left,right,chest,upper);
                if (!Placement.StackLatched)
                {
                    // Recover inside this step: changing steps restarts narration and its input gate.
                    if (wasStacked && measurementStarted)
                    {
                        if (Placement.LastReleaseReason == CprReleaseReason.HandsSeparated)
                        {
                            HandSeparationErrors++;
                            LogIncident("hands_separated", "Separación entre manos observada de forma sostenida.");
                        }
                        else if (!locationReported)
                        {
                            LocationErrors++;
                            LogIncident("hands_away", "Mano observada lejos del pecho de forma sostenida.");
                        }
                    }
                    Detector.Suspend();
                    ObservePlacement(dt,left,right,shoulder,chest,upper);
                    Progress = (float)Count / Settings.practiceCompressions;
                    Status(Placement.LowerLatched ? "Derecha colocada. Añade la izquierda y continúa."
                        : "Vuelve a colocar las manos y continúa; se conserva tu avance.", CprFeedbackKind.Instruction);
                    return;
                }
                PracticeSeconds += Mathf.Min(dt, .12f);
                // Never score a suspected jump while release evidence is still being gathered.
                // Head direction/position is not a CPR hand measurement.
                Detector.Tick(dt, TotalSeconds, Placement.ReleasePending ? default : left,
                    Placement.ReleasePending ? default : right, chest, true, true);
                if (Closed) return;
                if (Detector.State == CprCaptureState.Ready || Detector.State == CprCaptureState.Compressing) measurementStarted = true;
                bool lost = !Detector.HasTracking;
                bool technicalLoss = lost && !Placement.ReleasePending;
                if (technicalLoss)
                {
                    UntrackedSeconds += Mathf.Min(dt, .12f);
                    if (!wasTrackingLost)
                    {
                        TrackingInterruptions++;
                        LogIncident("tracking_gap", "Datos ausentes o inestables. Sin descuento ni compresiones estimadas.");
                    }
                }
                else if (!lost && measurementStarted)
                {
                    LocationSeconds += Mathf.Min(dt, .12f);
                    if (Detector.LocationCorrect) GoodLocationSeconds += Mathf.Min(dt, .12f);
                }
                if (!technicalLoss && wasTrackingLost) LogIncident("tracking_resumed", "Lectura recuperada; se requiere volver al reposo.");
                wasTrackingLost = technicalLoss;
                bool outside = Detector.State == CprCaptureState.OutsideZone;
                badLocationHeld = outside ? badLocationHeld + dt : 0;
                if (!outside && !lost) locationReported = false;
                if (measurementStarted && badLocationHeld > .45f && !locationReported)
                {
                    LocationErrors++; locationReported = true;
                    LogIncident("hands_away", "Manos fuera del centro del pecho durante más de 0,45 s.");
                }
                if (Placement.ReleasePending) Notify("Comprobando la posición · unión conservada.", CprFeedbackKind.Tracking, 0);
                else if (lost) Notify("Unión conservada · esperando seguimiento fiable, sin descuento.", CprFeedbackKind.Tracking, 0);
                else if (outside) Notify("Vuelve al centro del pecho con las manos superpuestas.", CprFeedbackKind.Correction, 0);
                else if (Detector.State == CprCaptureState.Recovering) Notify("Regresa al reposo y mantén un instante.", CprFeedbackKind.Tracking, 0);
                else if (Detector.Depth > Settings.maximumVirtualDepth) Status("Recorrido virtual excesivo: reduce la bajada hasta la franja verde.", CprFeedbackKind.Correction);
                else Status("Sigue el pulso y permite el retorno completo.", CprFeedbackKind.Instruction);
                Progress = (float)Count / Settings.practiceCompressions;
            }
        }

        private void ObservePlacement(float dt,CprHandSample left,CprHandSample right,Pose shoulder,Pose chest,Pose upper)
        {
            bool lowerBefore=Placement.LowerLatched, stackBefore=Placement.StackLatched;
            Placement.Tick(dt,left,right,shoulder,chest,upper,false);
            Progress=Placement.Progress;
            if(!lowerBefore && Placement.LowerLatched) Detector.CaptureRest(1,right,chest);
            if(!stackBefore && Placement.StackLatched && left.valid) Detector.CaptureRest(0,left,chest);
        }

        public void TogglePause()
        {
            if (Closed) return;
            Paused = !Paused; Detector.Suspend(); Pacing.Reset(); Placement.ClearReleaseEvidence();
            Notify(Paused ? "Práctica pausada. Puedes ajustar la altura o consultar la explicación."
                : "Práctica reanudada. Recupera la posición de reposo de las manos.", CprFeedbackKind.Instruction, 0);
        }

        public void WorldRecentered()
        {
            Detector.Suspend(); Pacing.Reset(); Placement.ClearReleaseEvidence();
            if(Closed) return;
            if(Step==CprPracticeStep.Placement || Step==CprPracticeStep.Compressions)
            {
                Placement.StartPlacement();
                if (Step != CprPracticeStep.Compressions) Enter(CprPracticeStep.Placement,false);
                Notify("Estación recolocada. Coloca de nuevo las manos; se conserva tu avance.",CprFeedbackKind.Instruction);
            }
        }
        public void HeightChanged()
        {
            if (!CanAdjustHeight) return;
            Detector.Suspend();
            if (Step == CprPracticeStep.Placement || Step == CprPracticeStep.Compressions)
            {
                Paused = false; Placement.StartPlacement();
                if (Step != CprPracticeStep.Compressions) Enter(CprPracticeStep.Placement, false);
                Notify("Altura ajustada. Coloca otra vez las dos manos para renovar la referencia; se conserva tu avance.", CprFeedbackKind.Instruction);
            }
        }

        public void Finish()
        {
            if (Closed) return;
            Completed = Count >= Settings.practiceCompressions;
            EndedAt = System.DateTimeOffset.Now.ToString("o");
            if (Completed) StepsDone = 6;
            Step = CprPracticeStep.Results; Paused = Explaining = false; StepPassed = true;
            Progress = Completed ? 1 : Mathf.Clamp01((float)Count / Settings.practiceCompressions);
            Detector.Suspend();
            Notify(Completed ? "Bloque virtual completado. Revisa cada criterio y las recomendaciones."
                : "Intento interrumpido. Los resultados corresponden solo a lo observado.", CprFeedbackKind.Instruction, 0);
            LogIncident("finished", Completed ? "Bloque completado." : "Intento interrumpido.");
        }

        private void OnCompression(CprCompressionEvent e)
        {
            if (Closed || Paused || Step != CprPracticeStep.Compressions) return;
            events.Add(e);
            if (e.depthCorrect) DepthHits++;
            else if (e.depth < Settings.minimumVirtualDepth) Shallow++;
            else Excessive++;
            if (e.fullReturn) RecoilHits++;
            if (e.rateEvaluable) { RateSamples++; if (e.rateCorrect) RateHits++; else if (e.rate < Settings.minimumRate) Slow++; else Fast++; }
            if (!e.fullReturn) Notify("Deja subir las manos hasta el reposo antes de volver a bajar.", CprFeedbackKind.Correction);
            else if (!e.depthCorrect) Notify(e.depth < Settings.minimumVirtualDepth
                ? "Recorrido virtual corto: baja un poco más hasta la franja verde." : "Recorrido virtual excesivo: reduce la bajada.", CprFeedbackKind.Correction);
            else if (e.rateEvaluable && !e.rateCorrect) Notify(e.rate < Settings.minimumRate
                ? "Ritmo lento: acelera siguiendo el pulso." : "Ritmo rápido: baja la velocidad siguiendo el pulso.", CprFeedbackKind.Correction);
            else Notify(e.rateEvaluable ? "Bien: ritmo, recorrido virtual y retorno dentro de la referencia."
                : "Ciclo reconocido. Faltan eventos para calcular el ritmo.", CprFeedbackKind.Success, .65f);
            if (Count >= Settings.practiceCompressions) Finish();
        }

        private void Enter(CprPracticeStep step, bool explanation = false)
        {
            Step = step; Explaining = explanation; StepPassed = false;
            Pacing.Reset();
            held = observed = observationClock = Progress = stepClock = transitionClock = 0; observing = false;
            if (step == CprPracticeStep.Placement)
            {
                Placement.StartPlacement(); measurementStarted = false;
                badLocationHeld = 0; locationReported = wasTrackingLost = false;
            }
            Notify(CprPracticeContent.ActionHint(step), CprFeedbackKind.Instruction, 0);
        }
        private void Pass(string message)
        {
            if (StepPassed) return;
            StepPassed = true; StepsDone++; Progress = 1; transitionClock=0;
            Notify(message, CprFeedbackKind.Success, 0);
        }
        private void Status(string text, CprFeedbackKind kind) { if (TotalSeconds >= noticeUntil) Notify(text, kind, 0); }
        private void LogIncident(string type, string detail)
        { incidents.Add(new CprAttemptIncident { seconds = TotalSeconds, type = type, detail = detail }); }
        private void Notify(string text, CprFeedbackKind kind, float duration = 1.7f)
        {
            if (Feedback != text || FeedbackKind != kind) FeedbackVersion++;
            Feedback = text; FeedbackKind = kind; noticeUntil = TotalSeconds + duration;
        }
    }
}

