using System;
using System.IO;
using System.Collections;
using UnityEditor;

[InitializeOnLoad]
public static class RagdollValidationRunner
{
    private const string Pending = "RCP.RagdollValidation.Pending";
    private const string ExitCode = "RCP.RagdollValidation.ExitCode";
    private static IEnumerator presentation;
    private static int lastFrame;

    static RagdollValidationRunner()
    {
        EditorApplication.playModeStateChanged += state =>
        {
            if (state == PlayModeStateChange.EnteredPlayMode && SessionState.GetBool(Pending, false))
                EditorApplication.delayCall += RunWhenPlaying;
            if (state == PlayModeStateChange.EnteredEditMode && SessionState.GetInt(ExitCode, -1)>=0)
            {
                int code=SessionState.GetInt(ExitCode, -1); SessionState.EraseInt(ExitCode);
                EditorApplication.delayCall += () => EditorApplication.Exit(code);
            }
        };
    }

    // PhysicsScene.Simulate y SceneManager.CreateScene(LocalPhysicsMode) requieren el modo Play.
    // Las comprobaciones del menú siguen disponibles en el editor; este punto de entrada las ejecuta en una escena física real.
    public static void BakeAndRun() { CprPatientClothingTools.Bake(); Run(); }

    public static void Run()
    {
        if (SessionState.GetBool(Pending, false)) return;
        SessionState.SetBool(Pending, true);
        File.WriteAllText("Ragdoll-validation.txt", "RUNNING " + DateTime.UtcNow.ToString("O"));
        if (UnityEngine.Application.isPlaying) { RunWhenPlaying(); return; }
        if (UnityEngine.Application.isBatchMode)
            UnityEditor.SceneManagement.EditorSceneManager.NewScene(UnityEditor.SceneManagement.NewSceneSetup.EmptyScene);
        EditorApplication.isPlaying = true;
    }

    private static void RunWhenPlaying()
    {
        if (!UnityEngine.Application.isPlaying || !SessionState.GetBool(Pending, false)) return;
        SessionState.SetBool(Pending, false);
        presentation = CprPresentationChecks.Run();
        lastFrame = -1;
        EditorApplication.update += AdvancePresentation;
    }

    private static void AdvancePresentation()
    {
        if (lastFrame == UnityEngine.Time.frameCount) return;
        lastFrame = UnityEngine.Time.frameCount;
        int exitCode = 0;
        try
        {
            if (presentation.MoveNext()) return;
            CprFlowChecks.Run();
            CprArmPoseChecks.Run();
            CprTransportChecks.Run();
            File.WriteAllText("Ragdoll-validation.txt",
                $"PASS: {CprPresentationChecks.Passed} presentation checks; {CprFlowChecks.Passed} CPR checks; {CprTransportChecks.Passed} ragdoll checks; " +
                $"drag={CprTransportChecks.DragDistance:F3} m; peakJointError={CprTransportChecks.WorstJointError:F4} m; " +
                $"liftError={CprTransportChecks.LiftError:F4} m; selfPenetration={CprTransportChecks.SelfPenetration:F4} m; " +
                $"{CprArmPoseChecks.Passed} arm pose cases; wrist={CprArmPoseChecks.WristAngle:F1} deg; " +
                $"elbow={CprArmPoseChecks.ElbowMin:F1}..{CprArmPoseChecks.ElbowMax:F1} deg; shoulderTwist={CprArmPoseChecks.ShoulderTwist:F1} deg.");
        }
        catch (Exception exception)
        {
            UnityEngine.Debug.LogException(exception);
            File.WriteAllText("Ragdoll-validation.txt", "FAIL: " + exception);
            exitCode = 1;
        }
        EditorApplication.update -= AdvancePresentation;
        (presentation as IDisposable)?.Dispose(); presentation = null;
        if(UnityEngine.Application.isBatchMode) SessionState.SetInt(ExitCode,exitCode);
        EditorApplication.isPlaying = false;
    }
}
