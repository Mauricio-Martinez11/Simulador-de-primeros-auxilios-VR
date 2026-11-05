using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEngine;

namespace FirstAidVR.Rcp
{
    [Serializable]
    public sealed class CprScoreImpact
    {
        public string label;
        public int count;
        public float points;
        public CprScoreImpact(string label,int count,float points) { this.label=label; this.count=count; this.points=points; }
        public static CprScoreImpact[] From(CprPracticeSession s)
        {
            var b=s.Breakdown;
            int placementCount=s.LocationErrors+s.HandSeparationErrors;
            // Share a saturated placement cap proportionally; never claim both types lost the full cap.
            float placementUnit=placementCount>0?(20*b.completion-b.placement)/placementCount:0;
            float travelUnit=s.Count>0?25f*b.completion/s.Count:0;
            float rhythmUnit=s.RateSamples>0?25f*b.completion/s.RateSamples:0;
            float returnUnit=s.Count>0?10f*b.completion/s.Count:0;
            return new[]{
                new CprScoreImpact("Fuera del pecho",s.LocationErrors,placementUnit*s.LocationErrors),
                new CprScoreImpact("Manos separadas",s.HandSeparationErrors,placementUnit*s.HandSeparationErrors),
                new CprScoreImpact("Recorrido corto",s.Shallow,travelUnit*s.Shallow),
                new CprScoreImpact("Recorrido excesivo",s.Excessive,travelUnit*s.Excessive),
                new CprScoreImpact("Ritmo lento",s.Slow,rhythmUnit*s.Slow),
                new CprScoreImpact("Ritmo rápido",s.Fast,rhythmUnit*s.Fast),
                new CprScoreImpact("Retorno incompleto",s.Count-s.RecoilHits,returnUnit*(s.Count-s.RecoilHits)),
                new CprScoreImpact("Pausas prolongadas",s.PauseErrors,b.pauseDeduction)
            };
        }
        public static string Summary(CprPracticeSession s)
        {
            var text=new StringBuilder();
            foreach(var row in From(s)) text.AppendLine(string.Format(CultureInfo.InvariantCulture,"{0}: {1} → −{2:0.00} pt",row.label,row.count,row.points));
            text.AppendLine($"Seguimiento: {s.TrackingInterruptions} cortes → 0 pt");
            text.AppendLine(string.Format(CultureInfo.InvariantCulture,"Ciclos pendientes: {0} → {1:0.00} pt sin obtener",Mathf.Max(0,s.Settings.practiceCompressions-s.Count),80*(1-s.Breakdown.completion)));
            text.AppendLine("Topes: colocación 20 pt compartidos; pausas 10 pt.");
            text.Append(s.Score.HasValue?"Valores antes de redondear y limitar la nota.":"Sin nota global: faltan datos evaluables.");
            return text.ToString();
        }
    }
}
