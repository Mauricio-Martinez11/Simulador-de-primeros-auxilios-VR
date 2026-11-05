using System.Collections.Generic;
using UnityEngine;

namespace FirstAidVR.Rcp.Consulta
{
    public enum PasoDePracticaRCP { Welcome, Safety, Response, Breathing, Help, Placement, Compressions, Results }
    public enum TipoDeRespuestaRCP { Instruction, Success, Correction, Tracking }
    public struct ObservacionDePracticaRCP { public bool safetyLeft, safetyRight, helpRequested; }

    /// <summary>Estado medido del intento, utilizado por la presentación y el informe persistente.</summary>
    public sealed class SecuenciaDePasosYEvaluacionRCP
    {
        public readonly ConfiguracionDeCriteriosRCP Settings;
        public readonly ColocacionYSuperposicionDeManosRCP Placement;
        public readonly DeteccionYConteoDeCompresionesRCP Detector;
        public readonly SincronizacionDeBajadaConRitmoRCP Pacing;
        private readonly List<EventoDeSincronizacionRCP> pacingEvents=new List<EventoDeSincronizacionRCP>();
        public IReadOnlyList<EventoDeSincronizacionRCP> PacingEvents => pacingEvents;
        private readonly List<EventoDeCompresionRCP> events = new List<EventoDeCompresionRCP>();
        public IReadOnlyList<EventoDeCompresionRCP> Events => events;
        private readonly List<IncidenciaDeIntentoRCP> incidents = new List<IncidenciaDeIntentoRCP>();
        public IReadOnlyList<IncidenciaDeIntentoRCP> Incidents => incidents;
        public string AttemptId { get; } = System.Guid.NewGuid().ToString("N");
        public string StartedAt { get; } = System.DateTimeOffset.Now.ToString("o");
        public string EndedAt { get; private set; }
        public string SavedReportPath { get; private set; }
        public string SaveError { get; private set; }
        public PasoDePracticaRCP Step { get; private set; }
        public bool Explaining { get; private set; }
        public bool ResponseTouching => ShoulderContact.Latched;
        public CprShoulderContact ShoulderContact { get; } = new CprShoulderContact();
        public bool StepPassed { get; private set; }
        public bool Paused { get; private set; }
        public bool Closed => Step == PasoDePracticaRCP.Results;
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
        public TipoDeRespuestaRCP FeedbackKind { get; private set; }
        public int FeedbackVersion { get; private set; }
        public int SafetySeenMask { get; private set; }
        public bool CanAdjustHeight => !Closed && (Step != PasoDePracticaRCP.Compressions || Paused);
        public float BreathingDuration => Mathf.Clamp(Settings.breathingObservation, 3, 10);
        public bool BreathingStarted => observing;
        public float BreathingObservedSeconds => Mathf.Min(observed, BreathingDuration);
        public float BreathingElapsedSeconds => Mathf.Min(observationClock, 10);
        public bool CanChooseBreathing => Step == PasoDePracticaRCP.Breathing && !Explaining && (observed >= BreathingDuration || observationClock >= 10);
        public float MeanDepth { get { float sum = 0; foreach (var e in events) sum += e.depth; return Count > 0 ? sum / Count : 0; } }
        public float MeanRate { get { float sum = 0; foreach (var e in events) if (e.rateEvaluable) sum += e.rate; return RateSamples > 0 ? sum / RateSamples : 0; } }
        public DesgloseDePuntuacionRCP Breakdown => DesgloseDePuntuacionRCP.From(this);
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

        public SecuenciaDePasosYEvaluacionRCP(ConfiguracionDeCriteriosRCP settings)
        {
            Settings = settings; Step = PasoDePracticaRCP.Welcome;
            Placement = new ColocacionYSuperposicionDeManosRCP(settings); Placement.StartPlacement();
            Detector = new DeteccionYConteoDeCompresionesRCP(settings);
            Pacing = new SincronizacionDeBajadaConRitmoRCP((settings.minimumRate+settings.maximumRate)*.5f);
            Detector.Pressed += Pacing.ObservePress;
            Pacing.PressObserved += pacingEvents.Add;
            Detector.Compression += OnCompression;
            Detector.PauseDetected += () =>
            {
                if (Closed) return;
                PauseErrors++;
                LogIncident("pause", "Pausa prolongada con seguimiento válido.");
                Notify("Pausa prolongada. Retoma el movimiento siguiendo el pulso.", TipoDeRespuestaRCP.Correction);
            };
        }

        public void Primary()
        {
            // Los clics en la interfaz no avanzan la lección deliberadamente. Solo avanzan las acciones observadas.
        }

        public void Alternative()
        {
            // Se conserva por compatibilidad con las acciones serializadas de la estación anterior.
        }

        public void Tick(float dt, MuestraDeManoRCP left, MuestraDeManoRCP right, Pose shoulder, Pose chest, Pose upper,
            bool lookingAtChest, bool participantPosition, bool focus, ObservacionDePracticaRCP observation = default)
        {
            ShoulderContact.Tick(dt,left,right,shoulder,Step==PasoDePracticaRCP.Response && focus && !Paused && !Closed,
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
                // Seguir validando la superposición de manos mientras se escucha, sin asignar puntaje.
                // El estudiante puede relajarse o mover las manos durante una explicación larga.
                if(Placement.StackLatched && (Step==PasoDePracticaRCP.Placement || Step==PasoDePracticaRCP.Compressions))
                {
                    Placement.TickCompression(Mathf.Min(dt,.12f),left,right,chest,upper);
                    if(!Placement.StackLatched)
                    {
                        Detector.Suspend();
                        if(Step==PasoDePracticaRCP.Placement) StepPassed=false;
                    }
                }
                if((Step==PasoDePracticaRCP.Placement || Step==PasoDePracticaRCP.Compressions) && !Placement.StackLatched)
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
            if(Step==PasoDePracticaRCP.Welcome)
            {
                held=participantPosition?held+dt:0; Progress=Mathf.Clamp01(held/4);
                if(held>=4) Enter(PasoDePracticaRCP.Safety);
                return;
            }
            if(Step==PasoDePracticaRCP.Safety)
            {
                leftLook=observation.safetyLeft?leftLook+dt:0; rightLook=observation.safetyRight?rightLook+dt:0;
                if(leftLook>=.65f) SafetySeenMask|=1;
                if(rightLook>=.65f) SafetySeenMask|=2;
                Progress=SafetySeenMask==3?1:SafetySeenMask==0?0:.5f;
                if(SafetySeenMask==3) Pass("Entorno observado. Acércate y comprueba si responde.");
                else Status("Gira la cabeza a ambos lados; mantén cada mirada un instante.",TipoDeRespuestaRCP.Instruction);
                return;
            }
            if (Step == PasoDePracticaRCP.Response)
            {
                Progress=ShoulderContact.Progress;
                if(ShoulderContact.Completed) Pass("No responde. Ahora observa su respiración.");
                else Status(ResponseTouching ? "Mantén la mano apoyada · comprobando respuesta."
                    : "Acerca una mano al hombro señalado y pregunta si está bien.",TipoDeRespuestaRCP.Instruction);
            }
            else if (Step == PasoDePracticaRCP.Breathing)
            {
                if (lookingAtChest) observing = true;
                if (observing) observationClock += dt;
                if (lookingAtChest) observed += dt;
                Progress = Mathf.Clamp01(observed / BreathingDuration);
                if(CanChooseBreathing) Pass("No respira normalmente. Solicita ayuda.");
                else Status(observing ? $"Comprueba la respiración · {BreathingObservedSeconds:0.0} / {BreathingDuration:0} s · sigue el aro sobre el pecho."
                    : "Mira la cara y el pecho. Comprueba si respira normalmente, sin superar 10 segundos.", TipoDeRespuestaRCP.Instruction);
            }
            else if(Step==PasoDePracticaRCP.Help)
            {
                if(observation.helpRequested) Pass("Ayuda solicitada en la simulación. Prepara tus manos.");
                else Status("Toma el teléfono y acércalo a tu oído. Pide ayuda.",TipoDeRespuestaRCP.Instruction);
            }
            else if (Step == PasoDePracticaRCP.Placement)
            {
                ObservePlacement(dt,left,right,shoulder,chest,upper);
                if (Placement.StackLatched && Detector.Calibrated)
                {
                    if(presentationGates) Pass("Manos reconocidas. Mantén la postura y escucha la explicación.");
                    else Enter(PasoDePracticaRCP.Compressions);
                    StepsDone = 5;
                }
                else Status(!Placement.LowerLatched && !right.valid ? "No se detecta la mano derecha. Muéstrala abierta delante del visor."
                    : Placement.LowerLatched ? "Derecha colocada. Acerca la izquierda encima, alineada con la guía."
                    : "Acerca la mano derecha al centro del pecho, palma abajo y dedos en la dirección de la guía.",
                    !left.valid && !right.valid ? TipoDeRespuestaRCP.Tracking : TipoDeRespuestaRCP.Instruction);
            }
            else if (Step == PasoDePracticaRCP.Compressions)
            {
                Pacing.Tick(dt,TotalSeconds);
                bool wasStacked = Placement.StackLatched;
                Placement.TickCompression(dt,left,right,chest,upper);
                if (!Placement.StackLatched)
                {
                    // Recuperarse dentro de este paso: cambiar de paso reinicia la narración y su bloqueo de entrada.
                    if (wasStacked && measurementStarted)
                    {
                        if (Placement.LastReleaseReason == MotivoDeLiberacionRCP.HandsSeparated)
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
                        : "Vuelve a colocar las manos y continúa; se conserva tu avance.", TipoDeRespuestaRCP.Instruction);
                    return;
                }
                PracticeSeconds += Mathf.Min(dt, .12f);
                // Nunca penalizar un salto sospechoso mientras aún se reúne evidencia de separación.
                // La dirección o posición de la cabeza no es una medición de las manos durante la RCP.
                Detector.Tick(dt, TotalSeconds, Placement.ReleasePending ? default : left,
                    Placement.ReleasePending ? default : right, chest, true, true);
                if (Closed) return;
                if (Detector.State == EstadoDeCapturaRCP.Ready || Detector.State == EstadoDeCapturaRCP.Compressing) measurementStarted = true;
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
                bool outside = Detector.State == EstadoDeCapturaRCP.OutsideZone;
                badLocationHeld = outside ? badLocationHeld + dt : 0;
                if (!outside && !lost) locationReported = false;
                if (measurementStarted && badLocationHeld > .45f && !locationReported)
                {
                    LocationErrors++; locationReported = true;
                    LogIncident("hands_away", "Manos fuera del centro del pecho durante más de 0,45 s.");
                }
                if (Placement.ReleasePending) Notify("Comprobando la posición · unión conservada.", TipoDeRespuestaRCP.Tracking, 0);
                else if (lost) Notify("Unión conservada · esperando seguimiento fiable, sin descuento.", TipoDeRespuestaRCP.Tracking, 0);
                else if (outside) Notify("Vuelve al centro del pecho con las manos superpuestas.", TipoDeRespuestaRCP.Correction, 0);
                else if (Detector.State == EstadoDeCapturaRCP.Recovering) Notify("Regresa al reposo y mantén un instante.", TipoDeRespuestaRCP.Tracking, 0);
                else if (Detector.Depth > Settings.maximumVirtualDepth) Status("Recorrido virtual excesivo: reduce la bajada hasta la franja verde.", TipoDeRespuestaRCP.Correction);
                else Status("Sigue el pulso y permite el retorno completo.", TipoDeRespuestaRCP.Instruction);
                Progress = (float)Count / Settings.practiceCompressions;
            }
        }

        private void ObservePlacement(float dt,MuestraDeManoRCP left,MuestraDeManoRCP right,Pose shoulder,Pose chest,Pose upper)
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
                : "Práctica reanudada. Recupera la posición de reposo de las manos.", TipoDeRespuestaRCP.Instruction, 0);
        }

        public void WorldRecentered()
        {
            Detector.Suspend(); Pacing.Reset(); Placement.ClearReleaseEvidence();
            if(Closed) return;
            if(Step==PasoDePracticaRCP.Placement || Step==PasoDePracticaRCP.Compressions)
            {
                Placement.StartPlacement();
                if (Step != PasoDePracticaRCP.Compressions) Enter(PasoDePracticaRCP.Placement,false);
                Notify("Estación recolocada. Coloca de nuevo las manos; se conserva tu avance.",TipoDeRespuestaRCP.Instruction);
            }
        }
        public void HeightChanged()
        {
            if (!CanAdjustHeight) return;
            Detector.Suspend();
            if (Step == PasoDePracticaRCP.Placement || Step == PasoDePracticaRCP.Compressions)
            {
                Paused = false; Placement.StartPlacement();
                if (Step != PasoDePracticaRCP.Compressions) Enter(PasoDePracticaRCP.Placement, false);
                Notify("Altura ajustada. Coloca otra vez las dos manos para renovar la referencia; se conserva tu avance.", TipoDeRespuestaRCP.Instruction);
            }
        }

        public void Finish()
        {
            if (Closed) return;
            Completed = Count >= Settings.practiceCompressions;
            EndedAt = System.DateTimeOffset.Now.ToString("o");
            if (Completed) StepsDone = 6;
            Step = PasoDePracticaRCP.Results; Paused = Explaining = false; StepPassed = true;
            Progress = Completed ? 1 : Mathf.Clamp01((float)Count / Settings.practiceCompressions);
            Detector.Suspend();
            Notify(Completed ? "Bloque virtual completado. Revisa cada criterio y las recomendaciones."
                : "Intento interrumpido. Los resultados corresponden solo a lo observado.", TipoDeRespuestaRCP.Instruction, 0);
            LogIncident("finished", Completed ? "Bloque completado." : "Intento interrumpido.");
        }

        private void OnCompression(EventoDeCompresionRCP e)
        {
            if (Closed || Paused || Step != PasoDePracticaRCP.Compressions) return;
            events.Add(e);
            if (e.depthCorrect) DepthHits++;
            else if (e.depth < Settings.minimumVirtualDepth) Shallow++;
            else Excessive++;
            if (e.fullReturn) RecoilHits++;
            if (e.rateEvaluable) { RateSamples++; if (e.rateCorrect) RateHits++; else if (e.rate < Settings.minimumRate) Slow++; else Fast++; }
            if (!e.fullReturn) Notify("Deja subir las manos hasta el reposo antes de volver a bajar.", TipoDeRespuestaRCP.Correction);
            else if (!e.depthCorrect) Notify(e.depth < Settings.minimumVirtualDepth
                ? "Recorrido virtual corto: baja un poco más hasta la franja verde." : "Recorrido virtual excesivo: reduce la bajada.", TipoDeRespuestaRCP.Correction);
            else if (e.rateEvaluable && !e.rateCorrect) Notify(e.rate < Settings.minimumRate
                ? "Ritmo lento: acelera siguiendo el pulso." : "Ritmo rápido: baja la velocidad siguiendo el pulso.", TipoDeRespuestaRCP.Correction);
            else Notify(e.rateEvaluable ? "Bien: ritmo, recorrido virtual y retorno dentro de la referencia."
                : "Ciclo reconocido. Faltan eventos para calcular el ritmo.", TipoDeRespuestaRCP.Success, .65f);
            if (Count >= Settings.practiceCompressions) Finish();
        }

        private void Enter(PasoDePracticaRCP step, bool explanation = false)
        {
            Step = step; Explaining = explanation; StepPassed = false;
            Pacing.Reset();
            held = observed = observationClock = Progress = stepClock = transitionClock = 0; observing = false;
            if (step == PasoDePracticaRCP.Placement)
            {
                Placement.StartPlacement(); measurementStarted = false;
                badLocationHeld = 0; locationReported = wasTrackingLost = false;
            }
            Notify(CprPracticeContent.ActionHint(step), TipoDeRespuestaRCP.Instruction, 0);
        }
        private void Pass(string message)
        {
            if (StepPassed) return;
            StepPassed = true; StepsDone++; Progress = 1; transitionClock=0;
            Notify(message, TipoDeRespuestaRCP.Success, 0);
        }
        private void Status(string text, TipoDeRespuestaRCP kind) { if (TotalSeconds >= noticeUntil) Notify(text, kind, 0); }
        private void LogIncident(string type, string detail)
        { incidents.Add(new IncidenciaDeIntentoRCP { seconds = TotalSeconds, type = type, detail = detail }); }
        private void Notify(string text, TipoDeRespuestaRCP kind, float duration = 1.7f)
        {
            if (Feedback != text || FeedbackKind != kind) FeedbackVersion++;
            Feedback = text; FeedbackKind = kind; noticeUntil = TotalSeconds + duration;
        }
    }
}

