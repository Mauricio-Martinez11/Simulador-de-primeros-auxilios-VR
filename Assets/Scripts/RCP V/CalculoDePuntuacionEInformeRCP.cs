using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;

namespace FirstAidVR.Rcp.Consulta
{
    [Serializable]
    public sealed class IncidenciaDeIntentoRCP
    {
        public float seconds;
        public string type, detail;
    }

    [Serializable]
    public struct DesgloseDePuntuacionRCP
    {
        public bool evaluable;
        public int total;
        public float preparation, placement, travel, rhythm, recoil, pauseDeduction, completion;
        public static DesgloseDePuntuacionRCP From(SecuenciaDePasosYEvaluacionRCP s)
        {
            var b = new DesgloseDePuntuacionRCP();
            b.evaluable = s.Count >= 3 && s.RateSamples > 0 && s.LocationSeconds > 0;
            b.completion = Mathf.Clamp01((float)s.Count / s.Settings.practiceCompressions);
            b.preparation = 20 * Mathf.Clamp01(Mathf.Min(s.StepsDone, 5) / 5f) * Mathf.Clamp01(1 - s.SequenceErrors * .1f);
            // Un incidente continuo se cuenta una sola vez. Las pérdidas técnicas nunca entran en estos contadores.
            b.placement = Mathf.Max(0, 20 - 2 * (s.LocationErrors + s.HandSeparationErrors)) * b.completion;
            b.travel = s.Count > 0 ? 25f * s.DepthHits / s.Count * b.completion : 0;
            b.rhythm = s.RateSamples > 0 ? 25f * s.RateHits / s.RateSamples * b.completion : 0;
            b.recoil = s.Count > 0 ? 10f * s.RecoilHits / s.Count * b.completion : 0;
            b.pauseDeduction = Mathf.Min(10, 2 * s.PauseErrors);
            b.total = Mathf.Clamp(Mathf.RoundToInt(b.preparation + b.placement + b.travel + b.rhythm + b.recoil - b.pauseDeduction), 0, 100);
            return b;
        }
    }

    [Serializable]
    public sealed class CompresionRegistradaRCP
    {
        public int number;
        public float seconds, virtualTravelCm, ratePerMinute;
        public bool travelCorrect, rateEvaluable, rateCorrect, fullReturn;
    }

    [Serializable]
    public sealed class CalculoDePuntuacionEInformeRCP
    {
        public string schema = "rcp-attempt-v2";
        public string scoring = "placement-incidents-v1";
        public string attemptId, startedAt, endedAt, applicationVersion, environment, deviceModel, inputMode, status;
        public string scope = "Práctica virtual: no mide fuerza, profundidad física ni extensión de codos. No certifica competencia clínica.";
        public int targetCycles, observedCycles, preparationSteps, travelHits, rateHits, rateSamples, returnHits;
        public int handsAway, handsSeparated, prolongedPauses, trackingInterruptions, shallow, excessive, slow, fast;
        public float elapsedSeconds, trackingGapSeconds, minimumVirtualCm, maximumVirtualCm, minimumRate, maximumRate;
        public DesgloseDePuntuacionRCP score;
        public CompresionRegistradaRCP[] compressions;
        public IncidenciaDeIntentoRCP[] incidents;
        public CprScoreImpact[] scoreImpacts;
        public string impactSummary;
        public EventoDeSincronizacionRCP[] pacing;
        public int onTimePresses, earlyPresses, latePresses, extraPresses;

        public static CalculoDePuntuacionEInformeRCP From(SecuenciaDePasosYEvaluacionRCP s, string environment, string inputMode)
        {
            return new CalculoDePuntuacionEInformeRCP {
                attemptId = s.AttemptId, startedAt = s.StartedAt, endedAt = s.EndedAt,
                applicationVersion = Application.version, environment = environment,
                deviceModel = SystemInfo.deviceModel, inputMode = inputMode,
                status = !s.Closed ? "en_curso" : s.Completed ? "completado" : "interrumpido",
                targetCycles = s.Settings.practiceCompressions, observedCycles = s.Count,
                preparationSteps = Mathf.Min(s.StepsDone, 5), travelHits = s.DepthHits,
                rateHits = s.RateHits, rateSamples = s.RateSamples, returnHits = s.RecoilHits,
                handsAway = s.LocationErrors, handsSeparated = s.HandSeparationErrors,
                prolongedPauses = s.PauseErrors, trackingInterruptions = s.TrackingInterruptions,
                shallow = s.Shallow, excessive = s.Excessive, slow = s.Slow, fast = s.Fast,
                elapsedSeconds = s.TotalSeconds, trackingGapSeconds = s.UntrackedSeconds,
                minimumVirtualCm = s.Settings.minimumVirtualDepth * 100, maximumVirtualCm = s.Settings.maximumVirtualDepth * 100,
                minimumRate = s.Settings.minimumRate, maximumRate = s.Settings.maximumRate, score = s.Breakdown,
                incidents = s.Incidents.ToArray(),
                scoreImpacts=CprScoreImpact.From(s), impactSummary=CprScoreImpact.Summary(s),
                pacing=s.PacingEvents.ToArray(), onTimePresses=s.Pacing.OnTime, earlyPresses=s.Pacing.Early,
                latePresses=s.Pacing.Late, extraPresses=s.Pacing.Extra,
                compressions = s.Events.Select((e, i) => new CompresionRegistradaRCP {
                    number = i + 1, seconds = e.time, virtualTravelCm = e.depth * 100, ratePerMinute = e.rate,
                    travelCorrect = e.depthCorrect, rateEvaluable = e.rateEvaluable,
                    rateCorrect = e.rateCorrect, fullReturn = e.fullReturn
                }).ToArray()
            };
        }

        public string ToReadableText()
        {
            var text = new StringBuilder();
            text.AppendLine("REGISTRO DE PRÁCTICA VIRTUAL DE RCP");
            text.AppendLine($"Intento: {attemptId}\nInicio: {startedAt}\nFin: {endedAt ?? "en curso"}");
            text.AppendLine($"Estado: {status}\nEntorno: {environment}\nDispositivo: {deviceModel}\nEntrada observada: {inputMode}\nVersión: {applicationVersion}");
            text.AppendLine(scope);
            text.AppendLine($"\nCALIFICACIÓN: {(score.evaluable ? score.total + " / 100" : "SIN DATOS SUFICIENTES")}");
            text.AppendLine($"Ciclos: {observedCycles}/{targetCycles}. Etapas preparatorias: {preparationSteps}/5.");
            text.AppendLine(FormattableString.Invariant(
                $"Preparación: {score.preparation:0.00}/20\nColocación: {score.placement:0.00}/20\nRecorrido: {score.travel:0.00}/25\nRitmo: {score.rhythm:0.00}/25\nRetorno: {score.recoil:0.00}/10\nDescuento por pausas: -{score.pauseDeduction:0.00}"));
            text.AppendLine("FÓRMULA: preparación + colocación + recorrido + ritmo + retorno - pausas; redondeo al entero más cercano, limitado a 0..100.");
            text.AppendLine("Preparación = 20 x etapas/5 (secuencia guiada). Colocación = máximo(0, 20 - 2 x (alejamientos + separaciones)).");
            text.AppendLine("Recorrido = 25 x aciertos/ciclos; ritmo = 25 x aciertos/ciclos con ritmo evaluable; retorno = 10 x retornos completos/ciclos.");
            text.AppendLine(FormattableString.Invariant($"Los cuatro criterios de ejecución se multiplican por ciclos/objetivo = {score.completion:0.0000}. Pausas: 2 puntos por episodio, máximo 10."));
            text.AppendLine("Los incidentes continuos se cuentan una sola vez. Las pérdidas técnicas y las pausas manuales no descuentan puntos.");
            text.AppendLine($"Si faltan 3 ciclos, una lectura de ritmo o datos de ubicación, la nota no es evaluable.");
            text.AppendLine($"\nINCIDENCIAS\nManos alejadas/fuera de zona: {handsAway}\nManos separadas: {handsSeparated}\nPausas prolongadas: {prolongedPauses}");
            text.AppendLine(FormattableString.Invariant($"Pérdidas o inestabilidad del seguimiento: {trackingInterruptions} episodios / {trackingGapSeconds:0.00} s, sin descuento."));
            text.AppendLine($"Recorridos cortos: {shallow}; excesivos: {excessive}; correctos: {travelHits}/{observedCycles}.");
            text.AppendLine($"Ritmo lento: {slow}; rápido: {fast}; correcto: {rateHits}/{rateSamples} evaluables.");
            text.AppendLine($"Retornos completos: {returnHits}/{observedCycles}; incompletos: {observedCycles - returnHits}.");
            text.AppendLine($"Pulsaciones respecto del metrónomo: a tiempo {onTimePresses}; adelantadas {earlyPresses}; tardías {latePresses}; adicionales {extraPresses}.");
            text.AppendLine("La coincidencia con el pitido orienta la práctica; los 25 puntos de ritmo evalúan la frecuencia medida configurada.");
            text.AppendLine(FormattableString.Invariant($"Referencias configuradas: {minimumVirtualCm:0.0}–{maximumVirtualCm:0.0} cm virtuales; {minimumRate:0}–{maximumRate:0}/min."));
            text.AppendLine("\nEFECTO POR TIPO DE INCIDENCIA\n"+impactSummary);
            text.AppendLine("Colocación: límite compartido de 20 puntos, proporcional a ciclos completados. Si se agota, se reparte entre los tipos según su cantidad. Pausas: límite de 10 puntos. Los demás efectos son puntos no obtenidos en su criterio. No se descuentan de nuevo.");
            text.AppendLine("\nCRONOLOGÍA (segundos desde el inicio activo)");
            foreach (var e in incidents) text.AppendLine(FormattableString.Invariant($"{e.seconds:0.00} s | {e.type} | {e.detail}"));
            text.AppendLine("\nRESPUESTA AL PULSO");
            foreach(var e in pacing) text.AppendLine(FormattableString.Invariant($"{e.seconds:0.00} s | {e.result} | diferencia {e.offsetMilliseconds:0.0} ms"));
            text.AppendLine("\nCICLOS OBSERVADOS");
            foreach (var e in compressions)
                text.AppendLine(string.Format(CultureInfo.InvariantCulture,
                    "{0}: t={1:0.00}s; recorrido={2:0.00}cm ({3}); ritmo={4}; retorno={5}",
                    e.number, e.seconds, e.virtualTravelCm, e.travelCorrect ? "correcto" : "fuera de referencia",
                    e.rateEvaluable ? e.ratePerMinute.ToString("0.0", CultureInfo.InvariantCulture) + "/min " + (e.rateCorrect ? "correcto" : "fuera de referencia") : "aún no evaluable",
                    e.fullReturn ? "completo" : "incompleto"));
            return text.ToString();
        }

        public static string Save(SecuenciaDePasosYEvaluacionRCP session, string directory, string environment, string inputMode)
        {
            var report = From(session, environment, inputMode);
            Directory.CreateDirectory(directory);
            string filename = "RCP_" + session.AttemptId;
            string jsonPath = Path.Combine(directory, filename + ".json");
            string textPath = Path.Combine(directory, filename + ".txt");
            WriteFile(jsonPath, JsonUtility.ToJson(report, true));
            WriteFile(textPath, report.ToReadableText());
            return textPath;
        }

        private static void WriteFile(string path, string content)
        {
            string temp = path + ".tmp";
            File.WriteAllText(temp, content, new UTF8Encoding(false));
            if (File.Exists(path)) File.Copy(temp, path, true);
            else File.Move(temp, path);
            if (File.Exists(temp)) File.Delete(temp);
        }
    }
}
