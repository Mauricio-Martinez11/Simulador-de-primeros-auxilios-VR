using System;
using System.Collections.Generic;
using UnityEngine;

namespace FirstAidVR.Rcp.Consulta
{
    public enum EstadoDeCapturaRCP { Recovering, OutsideZone, Ready, Compressing }

    public readonly struct EventoDeCompresionRCP
    {
        public readonly float time, depth, rate;
        public readonly bool fullReturn, rateEvaluable, depthCorrect, rateCorrect;
        public EventoDeCompresionRCP(float time, float depth, bool fullReturn, float rate, ConfiguracionDeCriteriosRCP settings)
        {
            this.time = time; this.depth = depth; this.fullReturn = fullReturn; this.rate = rate;
            rateEvaluable = rate > 0;
            depthCorrect = depth >= settings.minimumVirtualDepth - .0001f && depth <= settings.maximumVirtualDepth + .0001f;
            rateCorrect = rateEvaluable && rate >= settings.minimumRate - .01f && rate <= settings.maximumRate + .01f;
        }
    }

    /// <summary>Solo recorrido medido de la palma. Las manos fantasma y el pecho animado nunca intervienen en este detector.</summary>
    public sealed class DeteccionYConteoDeCompresionesRCP
    {
        private enum Phase { Rest, Down, Up }
        private readonly ConfiguracionDeCriteriosRCP settings;
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
        public event Action<EventoDeCompresionRCP> Compression;
        public event Action<float> Pressed;
        public event Action PauseDetected;
        public EstadoDeCapturaRCP State { get; private set; }
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

        public DeteccionYConteoDeCompresionesRCP(ConfiguracionDeCriteriosRCP settings) { this.settings = settings; }

        public bool Calibrate(MuestraDeManoRCP left, MuestraDeManoRCP right, Pose chest)
        {
            // Ambas poses reales de las manos deben haberse observado en reposo; nunca calibrar una mano fantasma oculta.
            if (!left.valid || !right.valid) return false;
            var normal = chest.rotation * Vector3.up;
            rest[0] = Vector3.Dot(left.pose.position - chest.position, normal);
            rest[1] = Vector3.Dot(right.pose.position - chest.position, normal);
            calibrated[0] = calibrated[1] = true;
            displayInitialized=false;
            Suspend();
            return true;
        }

        // La calibración secuencial admite la oclusión real de la mano inferior al superponer las manos.
        public void CaptureRest(int hand, MuestraDeManoRCP sample, Pose chest)
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
            HasTracking = LocationCorrect = false; pauseReported = false; State = EstadoDeCapturaRCP.Recovering;
        }

        public void Tick(float dt, float time, MuestraDeManoRCP left, MuestraDeManoRCP right, Pose chest, bool participantPosition, bool stackEstablished = false)
        {
            displayAge+=Mathf.Max(0,dt);
            if (stackEstablished)
            {
                if (!left.reliable) left = default;
                if (!right.reliable) right = default;
                // La colocación controla la detección de separación. La mano superior visible determina el recorrido;
                // la oclusión de la mano inferior no debe invalidar una medición fiable.
                // Mantener una fuente observada continuamente. La alternancia breve entre manos
                // no debe reiniciar el ciclo en cada fotograma de la cámara.
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
                HasTracking=LocationCorrect=false; State=EstadoDeCapturaRCP.Recovering;
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
                Suspend(); HasTracking = true; State = EstadoDeCapturaRCP.OutsideZone;
                return;
            }
            // La mano izquierda superior permanece visible cuando Quest pierde la mano derecha inferior.
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
            { Suspend(); HasTracking = true; State = EstadoDeCapturaRCP.OutsideZone; return; }
            previousDepth = depth; haveSample = true; Depth = Mathf.Max(0, depth);
            // La visualización usa muestras medidas incluso mientras se rearma la detección de ciclos.
            // Un filtro breve de paso bajo reduce las fluctuaciones visuales sin alterar los picos de los ciclos.
            float alpha=1-Mathf.Exp(-dt/.025f);
            displayDepth=displayInitialized?Mathf.Lerp(displayDepth,Depth,alpha):Depth;
            displayInitialized=true; displayAge=0;
            if (!ready)
            {
                returnHeld = Mathf.Abs(depth) <= settings.compressionReturn ? returnHeld + dt : 0;
                State = EstadoDeCapturaRCP.Recovering;
                RecoveryReason="Vuelve al reposo para reconocer la siguiente bajada";
                // La colocación puede haber medido una mano antes de que se estabilizara. Recuperar la referencia
                // solo después de observar un ascenso y una pequeña inversión descendente.
                // Una mano inmóvil mantenida abajo no puede recalibrar ni generar un ciclo artificial.
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
                // Bastan dos o más muestras medidas en reposo. Exigir 160 ms
                // continuos en reposo puede impedir el rearme durante una RCP ininterrumpida.
                if (returnHeld >= .04f) { ready = true; continuityStart = time; State = EstadoDeCapturaRCP.Ready; }
                if(!ready) return;
            }
            State = phase == Phase.Rest ? EstadoDeCapturaRCP.Ready : EstadoDeCapturaRCP.Compressing;
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

        private bool InZone(MuestraDeManoRCP hand, Pose chest, bool stackEstablished)
        {
            if (!hand.valid) return true; // Solo se evalúa la fuente visible que ya está calibrada.
            var offset = hand.pose.position - chest.position;
            return Vector3.ProjectOnPlane(offset, chest.rotation * Vector3.up).magnitude <= settings.compressionZoneRadius
                && (stackEstablished || (Vector3.Angle(hand.pose.rotation * Vector3.up, chest.rotation * Vector3.up) <= settings.palmAngle + 15
                && Vector3.Angle(hand.pose.rotation * Vector3.forward, chest.rotation * Vector3.forward) <= settings.fingerAngle + 10));
        }

        private void Emit(float time, bool fullReturn)
        {
            // Rechazar picos muy breves. Los ciclos rápidos intencionales siguen contando y reciben información sobre el ritmo.
            if (time - cycleStart < .16f || (lastEvent >= 0 && time - lastEvent < .25f)) return;
            if (lastEvent >= 0 && time - lastEvent > settings.practicePauseSeconds) times.Clear();
            times.Add(time);
            while (times.Count > settings.rateWindow) times.RemoveAt(0);
            Rate = times.Count >= 3 ? 60 * (times.Count - 1) / (times[times.Count - 1] - times[0]) : 0;
            lastEvent = time; pauseReported = false;
            Compression?.Invoke(new EventoDeCompresionRCP(time, peak, fullReturn, Rate, settings));
        }
    }
}
