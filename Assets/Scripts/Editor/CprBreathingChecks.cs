using System;
using System.IO;
using FirstAidVR.Rcp;
using UnityEngine;

public static class CprBreathingChecks
{
    private static int passed;
    private static void Check(bool value, string detail)
    { if (!value) throw new InvalidOperationException("Respiración: " + detail); passed++; }
    private static void Tick(CprPracticeSession s, bool looking, bool focus = true)
    { s.Tick(.02f, default, default, Pose.identity, Pose.identity, Pose.identity, looking, true, focus); }
    public static void Run()
    {
        passed = 0;
        var settings = ScriptableObject.CreateInstance<CprTrainingSettings>();
        try
        {
            var s = CprStationChecks.AtStep(settings, CprPracticeStep.Breathing);
            for (int i = 0; i < 600; i++) Tick(s, false);
            Check(!s.BreathingStarted && !s.StepPassed, "no avanza sin observar al paciente");
            for (int i = 0; i < 50; i++) Tick(s, true);
            Check(s.BreathingStarted && Mathf.Abs(s.BreathingObservedSeconds - 1) < .001f, "inicia con dirección del visor");
            float observed = s.BreathingObservedSeconds, elapsed = s.BreathingElapsedSeconds;
            for (int i = 0; i < 600; i++) Tick(s, true, false);
            Check(s.BreathingObservedSeconds == observed && s.BreathingElapsedSeconds == elapsed, "sin foco no consume tiempo");
            s.TogglePause();
            for (int i = 0; i < 600; i++) Tick(s, true);
            Check(s.BreathingObservedSeconds == observed && !s.StepPassed, "pausa no evalúa");
            s.TogglePause();
            for (int i = 0; i < 210 && !s.StepPassed; i++) Tick(s, true);
            Check(s.StepPassed && s.Feedback.Contains("No respira normalmente"), "caso simulado conduce a ayuda");
            for (int i = 0; i < 70; i++) Tick(s, true);
            Check(s.Step == CprPracticeStep.Help && !s.StepPassed, "debe solicitar ayuda antes de colocar manos");
            s = CprStationChecks.AtStep(settings, CprPracticeStep.Breathing);
            Tick(s, true);
            for (int i = 0; i < 499; i++) Tick(s, false);
            Tick(s, false); // floating-point tolerance around 10 seconds
            Check(s.StepPassed && s.BreathingElapsedSeconds <= 10, "observación iniciada limitada a 10 s aunque cambie la mirada");
            Check(s.BreathingObservedSeconds < .1f, "el reloj límite no inventa tiempo de mirada");
            settings.breathingObservation = 100;
            s = CprStationChecks.AtStep(settings, CprPracticeStep.Breathing);
            Check(s.BreathingDuration == 10, "configuración inválida no prolonga evaluación");
            for (int i = 0; i < 501 && !s.StepPassed; i++) Tick(s, true);
            Check(s.StepPassed, "límite se aplica aunque el Inspector exceda 10 s");
            Check(!CprPracticeContent.Explanations[3].Contains("pulso") && !CprPracticeContent.Explanations[3].Contains("latidos"),
                "instrucción para alumnos centrada en respiración");
            Check(CprCaseMonitor.BeatCount(0)==0 && CprCaseMonitor.BeatCount(.4f)==1 && CprCaseMonitor.BeatCount(1.3f)==2,
                "dos latidos ilustrativos en instantes definidos");
            Check(CprCaseMonitor.BeatCount(60)==2,"el monitor no repite latidos al terminar");
            Check(CprCaseMonitor.SignalAt(.37f)>.7f && CprCaseMonitor.SignalAt(1.27f)>.7f,"dos picos ilustrativos");
            Check(CprCaseMonitor.SignalAt(3)==0 && CprCaseMonitor.SignalAt(30)==0,"línea estable tras la animación");
            File.WriteAllText("breathing-checks.txt", $"PASS: {passed} response and breathing checks; controlled inputs, no clinical validation.\n");
        }
        finally { UnityEngine.Object.DestroyImmediate(settings); }
    }
}
