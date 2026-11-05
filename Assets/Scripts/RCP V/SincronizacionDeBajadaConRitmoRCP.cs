using System;
using UnityEngine;

namespace FirstAidVR.Rcp.Consulta
{
    public enum ResultadoDePulsoRCP { Waiting, OnTime, Early, Late, ExtraPress }
    [Serializable]
    public sealed class EventoDeSincronizacionRCP
    {
        public float seconds, offsetMilliseconds;
        public string result;
    }

    /// <summary>El único reloj compartido por el audio, el marcador móvil y la respuesta a las compresiones.</summary>
    public sealed class SincronizacionDeBajadaConRitmoRCP
    {
        public const float EarlyTolerance=.055f, LateTolerance=.12f;
        private readonly float interval;
        private float lastTickTime;
        private float elapsed, lastPress=-100, visibleUntil;
        private int previousBeat=int.MinValue;
        private bool started;
        public float Phase { get; private set; }
        public int PulseVersion { get; private set; }
        public ResultadoDePulsoRCP LastResult { get; private set; }
        public float LastOffsetSeconds { get; private set; }
        public bool FeedbackVisible { get; private set; }
        public int OnTime { get; private set; }
        public int Early { get; private set; }
        public int Late { get; private set; }
        public int Extra { get; private set; }
        public event Action<EventoDeSincronizacionRCP> PressObserved;
        public SincronizacionDeBajadaConRitmoRCP(float rate) { interval=60/Mathf.Max(1,rate); }
        public void Tick(float dt,float sessionTime)
        {
            lastTickTime=sessionTime;
            FeedbackVisible=sessionTime<visibleUntil;
            if(!started) { started=true; elapsed=Phase=0; return; }
            float before=elapsed;
            elapsed+=Mathf.Max(0,dt);
            if(Mathf.FloorToInt(elapsed/interval+1-RelojDePulsoRCP.GreenStart)>Mathf.FloorToInt(before/interval+1-RelojDePulsoRCP.GreenStart))
                PulseVersion++;
            Phase=Mathf.Repeat(elapsed/interval,1);
        }
        public void ObservePress(float sessionTime)
        {
            if(!started) return;
            float observedElapsed=elapsed-Mathf.Max(0,lastTickTime-sessionTime);
            int nearest=Mathf.Max(0,Mathf.RoundToInt(observedElapsed/interval-RelojDePulsoRCP.GreenStart));
            float offset=observedElapsed-(nearest+RelojDePulsoRCP.GreenStart)*interval;
            LastOffsetSeconds=offset;
            if(nearest==previousBeat && sessionTime-lastPress<interval*.7f)
            { LastResult=ResultadoDePulsoRCP.ExtraPress; Extra++; }
            else if(offset < -EarlyTolerance) { LastResult=ResultadoDePulsoRCP.Early; Early++; }
            else if(offset > LateTolerance) { LastResult=ResultadoDePulsoRCP.Late; Late++; }
            else { LastResult=ResultadoDePulsoRCP.OnTime; OnTime++; }
            lastPress=sessionTime; previousBeat=nearest; visibleUntil=sessionTime+.38f; FeedbackVisible=true;
            PressObserved?.Invoke(new EventoDeSincronizacionRCP{seconds=sessionTime,offsetMilliseconds=offset*1000,result=LastResult.ToString()});
        }
        public void Reset()
        {
            started=false; elapsed=Phase=0; previousBeat=int.MinValue; lastPress=-100;
            FeedbackVisible=false; visibleUntil=0; LastResult=ResultadoDePulsoRCP.Waiting;
        }
    }
}
