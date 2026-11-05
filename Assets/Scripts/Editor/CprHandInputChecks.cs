using System;
using System.IO;
using System.Linq;
using FirstAidVR.Rcp;
using Oculus.Interaction.Input;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class CprHandInputChecks
{
    private static int passed;

    [MenuItem("Primeros auxilios/RCP/Verificar fuentes reales de manos")]
    public static void Run()
    {
        passed = 0;
        // HandSphereMap and LastKnownGoodHand can set all these flags to true,
        // despite providing a reference pose instead of a current measurement.
        var data = new HandDataAsset {
            IsDataValid = true, IsConnected = true, IsTracked = true,
            IsHighConfidence = true, RootPoseOrigin = PoseOrigin.SyntheticPose
        };
        Check(!CprTrackedHands.IsCurrentTrackingData(data), "reject connected reference/frozen pose");
        data.RootPoseOrigin = PoseOrigin.RawTrackedPose;
        Check(CprTrackedHands.IsCurrentTrackingData(data), "accept live headset pose");
        data.IsHighConfidence = false;
        Check(CprTrackedHands.IsCurrentTrackingData(data), "accept current low confidence measurement");
        data.RootPoseOrigin = PoseOrigin.FilteredTrackedPose;
        Check(CprTrackedHands.IsCurrentTrackingData(data), "accept current filtered measurement");
        data.IsTracked = false;
        Check(!CprTrackedHands.IsCurrentTrackingData(data), "reject untracked cached pose");
        data.IsTracked = true; data.IsConnected = false;
        Check(!CprTrackedHands.IsCurrentTrackingData(data), "reject disconnected hand");
        data.IsConnected = true; data.IsDataValid = false;
        Check(!CprTrackedHands.IsCurrentTrackingData(data), "reject invalid data");
        data.IsDataValid = true; data.RootPoseOrigin = PoseOrigin.None;
        Check(!CprTrackedHands.IsCurrentTrackingData(data), "reject missing pose origin");
        Check(!CprTrackedHands.IsCurrentTrackingData(null), "reject absent data");

        // Confirm the problematic candidates actually exist in the shipped rig.
        // Keep an interactive user's current scene open.
        if (Application.isBatchMode)
        {
            EditorSceneManager.OpenScene("Assets/Scenes/Boobstrap.unity");
            var hands = UnityEngine.Object.FindObjectsOfType<Hand>(true);
            var references = hands.Where(h => h.ModifyDataFromSource is FromHandPrefabDataSource).ToArray();
            Check(references.Length >= 2, "both reference hands reproduced in actual bootstrap scene");
            Check(references.All(h => h.GetType() == typeof(Hand)),
                "reference hands use ordinary Hand, so excluding SyntheticHand alone is insufficient");
            Check(hands.Any(h => h is LastKnownGoodHand), "cached pose source reproduced in actual rig");
        }
        CprFlowChecks.Run();
        CprStationChecks.Run();
        string result = $"PASS: {passed} hand source checks; {CprFlowChecks.Passed} flow checks; {CprStationChecks.Passed} station checks. Device test pending.";
        Debug.Log(result);
        if (Application.isBatchMode)
            File.WriteAllText(Path.Combine(Application.dataPath, "../hand-input-checks.txt"), result);
    }

    private static void Check(bool ok, string label)
    {
        if (!ok) throw new InvalidOperationException("CPR hand input: " + label);
        passed++;
    }
}
