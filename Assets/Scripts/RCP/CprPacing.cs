using System;
using UnityEngine;

namespace FirstAidVR.Rcp
{
    public enum CprBeatResult { Waiting, OnTime, Early, Late, ExtraPress }
    [Serializable]
    public sealed class CprPacingEvent
    {
        public float seconds, offsetMilliseconds;
        public string result;
    }

    /// <summary>El único reloj compartido por el audio, el marcador móvil y la respuesta a las compresiones.</summary>
    public sealed class CprPacing
    {
        public const float EarlyTolerance=.055f, LateTolerance=.12f;
        private readonly float interval;
        private float lastTickTime;
        private float elapsed, lastPress=-100, visibleUntil;
        private int previousBeat=int.MinValue;
        private bool started;
        public float Phase { get; private set; }
        public int PulseVersion { get; private set; }
        public CprBeatResult LastResult { get; private set; }
        public float LastOffsetSeconds { get; private set; }
        public bool FeedbackVisible { get; private set; }
        public int OnTime { get; private set; }
        public int Early { get; private set; }
        public int Late { get; private set; }
        public int Extra { get; private set; }
        public event Action<CprPacingEvent> PressObserved;
        public CprPacing(float rate) { interval=60/Mathf.Max(1,rate); }
        public void Tick(float dt,float sessionTime)
        {
            lastTickTime=sessionTime;
            FeedbackVisible=sessionTime<visibleUntil;
            if(!started) { started=true; elapsed=Phase=0; return; }
            float before=elapsed;
            elapsed+=Mathf.Max(0,dt);
            if(Mathf.FloorToInt(elapsed/interval+1-CprBeatClock.GreenStart)>Mathf.FloorToInt(before/interval+1-CprBeatClock.GreenStart))
                PulseVersion++;
            Phase=Mathf.Repeat(elapsed/interval,1);
        }
        public void ObservePress(float sessionTime)
        {
            if(!started) return;
            float observedElapsed=elapsed-Mathf.Max(0,lastTickTime-sessionTime);
            int nearest=Mathf.Max(0,Mathf.RoundToInt(observedElapsed/interval-CprBeatClock.GreenStart));
            float offset=observedElapsed-(nearest+CprBeatClock.GreenStart)*interval;
            LastOffsetSeconds=offset;
            if(nearest==previousBeat && sessionTime-lastPress<interval*.7f)
            { LastResult=CprBeatResult.ExtraPress; Extra++; }
            else if(offset < -EarlyTolerance) { LastResult=CprBeatResult.Early; Early++; }
            else if(offset > LateTolerance) { LastResult=CprBeatResult.Late; Late++; }
            else { LastResult=CprBeatResult.OnTime; OnTime++; }
            lastPress=sessionTime; previousBeat=nearest; visibleUntil=sessionTime+.38f; FeedbackVisible=true;
            PressObserved?.Invoke(new CprPacingEvent{seconds=sessionTime,offsetMilliseconds=offset*1000,result=LastResult.ToString()});
        }
        public void Reset()
        {
            started=false; elapsed=Phase=0; previousBeat=int.MinValue; lastPress=-100;
            FeedbackVisible=false; visibleUntil=0; LastResult=CprBeatResult.Waiting;
        }
    }
}
