using UnityEngine;

namespace FirstAidVR.Rcp.Consulta
{
    // Solo una referencia temporal: la profundidad de las manos y el número de compresiones no pueden cambiar su fase.
    public sealed class RelojDePulsoRCP
    {
        public const float GreenStart=.8f;
        public static bool AtBeat(float phase) => phase>=GreenStart;
        public static float MarkerProgress(float phase) => Mathf.Clamp01(phase);
        private float elapsed;
        private bool started;
        public float Phase { get; private set; }
        public bool Tick(float dt,float interval)
        {
            interval=Mathf.Max(.1f,interval);
            if(!started) { started=true; elapsed=Phase=0; return false; }
            float previous=elapsed;
            elapsed+=Mathf.Max(0,dt);
            // Emitir el sonido justo cuando el marcador móvil entra en la zona verde, una vez por intervalo.
            bool pulse=Mathf.FloorToInt(elapsed/interval+1-GreenStart)
                > Mathf.FloorToInt(previous/interval+1-GreenStart);
            elapsed%=interval;
            Phase=elapsed/interval;
            return pulse;
        }
        public void Reset() { started=false; elapsed=Phase=0; }
    }

    public sealed class PitidoYAudioDeLaEstacionRCP : MonoBehaviour
    {
        private CprNarrator narrator;
        private CprNarrator corrections;
        private CprCorrectionVoice correctionVoice=new CprCorrectionVoice();
        private AudioSource effects;
        private AudioClip pulse;
        private SecuenciaDePasosYEvaluacionRCP activeSession;
        private int lastStep=-1,lastPulse;
        private bool instructionDone, confirmationStarted, startAnnounced;
        public bool Muted => narrator!=null && narrator.Muted;
        public float BeatPhase { get; private set; }
        public int CaseBeatsPlayed => 0;
        public bool MissingAudio => narrator!=null && narrator.MissingAudio;
        public string CurrentText => narrator==null || narrator.Key==null ? "" : CprNarration.Get(narrator.Key).text;
        public void Initialize(ConfiguracionDeCriteriosRCP config)
        {
            narrator=gameObject.AddComponent<CprNarrator>();
            corrections=gameObject.AddComponent<CprNarrator>();
            effects=gameObject.AddComponent<AudioSource>(); effects.playOnAwake=false; effects.spatialBlend=0; effects.volume=.23f;
            const int rate=22050; var data=new float[(int)(rate*.035f)];
            for(int i=0;i<data.Length;i++) data[i]=Mathf.Sin(2*Mathf.PI*720*i/rate)*Mathf.Sin(Mathf.PI*i/data.Length)*.28f;
            pulse=AudioClip.Create("Pulso de referencia",data.Length,1,rate,false); pulse.SetData(data,0);
        }
        // Se llama antes de que la sesión procese la entrada. Controla los bloqueos de explicación y confirmación.
        public void Prepare(SecuenciaDePasosYEvaluacionRCP session,float dt,bool focus)
        {
            if(activeSession!=session)
            { activeSession=session; lastStep=-1; lastPulse=session.Pacing.PulseVersion; correctionVoice=new CprCorrectionVoice(); corrections.Stop(); }
            if(lastStep!=(int)session.Step)
            {
                if(session.Closed && lastStep>=0 && !narrator.Complete)
                {
                    // Terminar una corrección hablada pendiente antes de la narración automática de resultados.
                    narrator.Tick(dt,focus); session.SetPresentationGate(false,true); return;
                }
                lastStep=(int)session.Step; instructionDone=confirmationStarted=startAnnounced=false;
                corrections.Stop();
                narrator.Play(CprNarration.StepKey(session)); lastPulse=session.Pacing.PulseVersion;
            }
            narrator.Tick(dt,focus && !session.Paused);
            corrections.Tick(dt,focus && !session.Paused);
            effects.mute=Muted || !focus || session.Paused;
            if(!focus || session.Paused) { session.SetPresentationGate(!instructionDone,true); return; }
            if(!instructionDone && narrator.Complete)
            {
                if(session.Step==PasoDePracticaRCP.Compressions && !startAnnounced)
                { startAnnounced=true; narrator.Play("Start"); }
                else instructionDone=true;
            }
            if(startAnnounced && !narrator.Speaking) instructionDone=true;
            if(instructionDone && session.StepPassed && !session.Closed && !confirmationStarted)
            {
                confirmationStarted=true;
                narrator.Play("Confirm"+((int)session.Step).ToString("00"));
            }
            bool transitionReady=confirmationStarted && narrator.Complete;
            session.SetPresentationGate(!instructionDone,!transitionReady);
        }
        // Se llama después de la evaluación para que los sonidos del pulso y la fase visible coincidan en el mismo fotograma.
        public void Tick(SecuenciaDePasosYEvaluacionRCP session,float dt,bool focus)
        {
            if(session.Step==PasoDePracticaRCP.Compressions && !session.Explaining && !session.Paused && focus)
            {
                if(session.Pacing.PulseVersion!=lastPulse && !Muted) effects.PlayOneShot(pulse);
                BeatPhase=session.Pacing.Phase;
                // Una voz independiente nunca controla el bloqueo de explicación, el detector ni el reloj de latidos.
                string key=correctionVoice.Tick(dt,CprCorrectionVoice.Candidate(session),
                    corrections.Speaking || !narrator.Complete,!Muted);
                if(key!=null) corrections.Play(key);
            }
            else BeatPhase=0;
            lastPulse=session.Pacing.PulseVersion;
        }
        public void Repeat(PasoDePracticaRCP step)
        {
            if(narrator==null || narrator.Speaking) return;
            corrections.Stop();
            instructionDone=confirmationStarted=startAnnounced=false;
            narrator.Play(activeSession!=null?CprNarration.StepKey(activeSession):"Step"+((int)step).ToString("00"));
        }
        public void Toggle(PasoDePracticaRCP step) { narrator.ToggleMute(); corrections.ToggleMute(); }
        private void OnDisable() { if(narrator!=null) narrator.Stop(); if(corrections!=null) corrections.Stop(); if(effects!=null) effects.Stop(); }
        private void OnDestroy() { if(pulse!=null) Destroy(pulse); }
    }
}
