using UnityEngine;

namespace FirstAidVR.Rcp
{
    // Usa el fin real de la reproducción para avanzar la lección narrada, no una duración estimada.
    // El reloj de reserva solo se usa si un recurso no se cargó y permanece visible como texto.
    public sealed class CprNarrator : MonoBehaviour
    {
        private AudioSource source;
        private float fallback, quiet, startupWait;
        private bool running, suspended, heardFrame, started, deviceChanged;
        private int resumeSample;
        private float retryWait;
        public string Key { get; private set; }
        public bool Complete => !running && quiet>=2;
        public bool Speaking => running;
        public float Duration => source!=null && source.clip!=null ? source.clip.length : 0;
        public float PlaybackSeconds => Duration<=0 ? 0 : !running ? Duration : Mathf.Clamp((float)resumeSample/source.clip.frequency,0,Duration);
        public float PlaybackProgress => Duration>0 ? PlaybackSeconds/Duration : 0;
        public bool MissingAudio { get; private set; }
        public bool Muted { get; private set; }
        private void OnEnable() { AudioSettings.OnAudioConfigurationChanged+=AudioChanged; }
        private void AudioChanged(bool deviceWasChanged)
        {
            if(running && !MissingAudio) { deviceChanged=true; started=false; }
        }
        private void TryStart()
        {
            if(source.clip==null || source.clip.loadState!=AudioDataLoadState.Loaded || !CprAudioOutput.Ready()) return;
            source.Stop();
            source.timeSamples=Mathf.Clamp(resumeSample,0,source.clip.samples-1);
            source.Play(); started=true; retryWait=0;
        }
        public void Play(string key)
        {
            if(source==null) { source=gameObject.AddComponent<AudioSource>(); source.playOnAwake=false; source.spatialBlend=0; source.volume=1; source.priority=0; source.spatialize=false; source.bypassEffects=true; source.bypassReverbZones=true; }
            source.Stop(); started=deviceChanged=false; resumeSample=0; retryWait=0; Key=key; source.clip=CprNarration.Clip(key); source.mute=Muted;
            MissingAudio=source.clip==null; running=true; heardFrame=false; suspended=false; quiet=startupWait=0;
            fallback=Mathf.Max(3,CprNarration.Get(key).text.Split(' ').Length/2.3f);
            if(!MissingAudio)
            {
                if(source.clip.loadState==AudioDataLoadState.Unloaded) source.clip.LoadAudioData();
                TryStart();
            }
        }
        public void Tick(float dt,bool active)
        {
            if(source==null) return;
            if(!active) { if(!suspended) source.Pause(); suspended=true; return; }
            if(suspended) { if(started && !deviceChanged) source.UnPause(); suspended=false; }
            if(deviceChanged) { source.Stop(); deviceChanged=false; heardFrame=false; started=false; }
            dt=Mathf.Clamp(dt,0,.12f);
            if(running)
            {
                if(MissingAudio) { fallback-=dt; running=fallback>0; }
                else if(source.isPlaying) { heardFrame=true; resumeSample=source.timeSamples; }
                else if(heardFrame && started) running=false;
                else
                {
                    startupWait+=dt; retryWait+=dt;
                    if(!started || retryWait>=.5f) TryStart();
                    if(source.clip.loadState==AudioDataLoadState.Failed || startupWait>=8)
                    {
                        MissingAudio=true;
                        Debug.LogWarning("RCP audio unavailable: "+Key+"; clip="+source.clip.loadState+"; output="+CprAudioOutput.Ready());
                    }
                }
                return;
            }
            quiet+=dt;
        }
        public void ToggleMute() { Muted=!Muted; if(source!=null) source.mute=Muted; }
        public void Stop() { if(source!=null) source.Stop(); running=false; quiet=0; Key=null; }
        private void OnDisable() { AudioSettings.OnAudioConfigurationChanged-=AudioChanged; Stop(); }
    }
}
