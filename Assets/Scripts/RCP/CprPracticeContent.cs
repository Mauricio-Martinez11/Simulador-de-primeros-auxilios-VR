namespace FirstAidVR.Rcp
{
    public static class CprPracticeContent
    {
        public static readonly string[] Titles={"Aprende haciendo", "Revisa el entorno", "Comprueba si responde", "Comprueba la respiración",
            "Solicita ayuda", "Coloca tus manos", "Sigue el ritmo", "Tu resultado"};
        public static readonly string[] Explanations={
            "Sitúate al costado. Sigue la demostración y las guías.",
            "Observa las dos zonas iluminadas. Comprueba que sea seguro acercarte.",
            "Apoya la mano durante los dos movimientos suaves. Pregunta: ¿se encuentra bien?",
            "Comprueba si respira normalmente. Máximo 10 s. Sigue el aro sobre el pecho.",
            "Toma el teléfono holográfico con cualquier mano y acércalo al oído. Pide ayuda.",
            "Derecha en el centro del pecho. Izquierda encima y dedos entrelazados.",
            "Baja al llegar a verde y sonar el pitido. Vuelve arriba antes del siguiente.",
            "Revisa lo observado y repite para practicar. Esta puntuación es virtual."
        };
        public static string ActionHint(CprPracticeStep step) => Explanations[(int)step];
        public static string PrimaryLabel(CprPracticeSession s) => s.Closed?"Repetir práctica":"";
        public static string Recommendation(CprPracticeSession s)
        {
            if(s.Count==0) return "No registraste ciclos. Sigue las guías y vuelve al reposo tras cada bajada.";
            if(!s.Score.HasValue) return "Datos insuficientes para una valoración del intento. Repite y completa más ciclos con seguimiento disponible.";
            var tips=new System.Collections.Generic.List<string>();
            if(s.LocationErrors>0) tips.Add("Mantén las manos centradas.");
            if(s.HandSeparationErrors>0) tips.Add("Mantén las manos juntas.");
            if(s.Shallow>0) tips.Add("Aumenta el recorrido hasta verde.");
            if(s.Excessive>0) tips.Add("Reduce el recorrido excesivo.");
            if(s.Fast>0 || s.Slow>0) tips.Add("Estabiliza el ritmo con el pulso.");
            if(s.RecoilHits<s.Count) tips.Add("Permite volver al reposo.");
            if(s.PauseErrors>0) tips.Add("Evita pausas dentro del bloque.");
            if(tips.Count==0) return "Buen trabajo. Los criterios observados están dentro de la referencia.";
            return string.Join(" ",tips.GetRange(0,System.Math.Min(2,tips.Count)));
        }
    }
}
