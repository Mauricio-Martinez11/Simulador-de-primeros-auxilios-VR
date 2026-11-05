using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class CprPatientRestPoseTools
{
    private const string ScenePath = "Assets/Scenes/2_Tutorial_RCP.unity";

    [MenuItem("Primeros auxilios/RCP/Colocar brazos del paciente junto al cuerpo")]
    public static void Apply()
    {
        var scene = EditorSceneManager.OpenScene(ScenePath);
        var patient = GameObject.Find("Hombre_Paciente");
        if (patient == null) throw new System.InvalidOperationException("No se encontró Hombre_Paciente.");

        var report = new System.Text.StringBuilder();
        PoseArm(patient.transform, "L", -1f, report);
        PoseArm(patient.transform, "R", 1f, report);
        var patientView = Object.FindObjectOfType<FirstAidVR.Rcp.CprStationPatient>();
        if (patientView != null)
        {
            var serialized = new SerializedObject(patientView);
            var fingers = serialized.FindProperty("fingers");
            var rest = serialized.FindProperty("fingerRest");
            if (fingers.arraySize != rest.arraySize) throw new System.InvalidOperationException("La pose de dedos no coincide con los huesos.");
            for (int i = 0; i < fingers.arraySize; i++)
            {
                var bone = fingers.GetArrayElementAtIndex(i).objectReferenceValue as Transform;
                if (bone == null) continue;
                bone.localRotation = rest.GetArrayElementAtIndex(i).quaternionValue;
                PrefabUtility.RecordPrefabInstancePropertyModifications(bone);
            }
        }
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        File.WriteAllText("patient-rest-pose.txt", report.ToString());
        Debug.Log("RCP: brazos del paciente colocados junto al cuerpo. " + report);
    }

    public static void ApplyAndCapture()
    {
        Apply();
        Capture();
        CaptureWrist();
    }

    public static void CaptureWrist()
    {
        EditorSceneManager.OpenScene(ScenePath);
        var cameraObject = new GameObject("Vista temporal de muñeca");
        var camera = cameraObject.AddComponent<Camera>();
        camera.transform.position = new Vector3(-.55f, .85f, -1.82f);
        camera.transform.LookAt(new Vector3(-.28f, .10f, -1.91f));
        camera.fieldOfView = 38f;
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(.72f, .78f, .84f);
        var target = new RenderTexture(1000, 1000, 24);
        var pixels = new Texture2D(1000, 1000, TextureFormat.RGB24, false);
        var previous = RenderTexture.active;
        try
        {
            camera.targetTexture = target;
            camera.Render();
            RenderTexture.active = target;
            pixels.ReadPixels(new Rect(0, 0, 1000, 1000), 0, 0);
            pixels.Apply();
            File.WriteAllBytes("Patient-wrist.png", pixels.EncodeToPNG());
        }
        finally
        {
            RenderTexture.active = previous;
            Object.DestroyImmediate(pixels);
            Object.DestroyImmediate(target);
            Object.DestroyImmediate(cameraObject);
        }
    }

    [MenuItem("Primeros auxilios/RCP/Capturar pose de reposo")]
    public static void Capture()
    {
        EditorSceneManager.OpenScene(ScenePath);
        var cameraObject = new GameObject("Vista temporal de pose");
        var camera = cameraObject.AddComponent<Camera>();
        camera.transform.position = new Vector3(0f, 2.7f, -1.72f);
        camera.transform.LookAt(new Vector3(0f, .08f, -1.72f));
        camera.fieldOfView = 42f;
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(.72f, .78f, .84f);
        var target = new RenderTexture(1600, 1000, 24);
        var pixels = new Texture2D(1600, 1000, TextureFormat.RGB24, false);
        var previous = RenderTexture.active;
        try
        {
            camera.targetTexture = target;
            camera.Render();
            RenderTexture.active = target;
            pixels.ReadPixels(new Rect(0, 0, 1600, 1000), 0, 0);
            pixels.Apply();
            File.WriteAllBytes("Patient-rest-pose.png", pixels.EncodeToPNG());
        }
        finally
        {
            RenderTexture.active = previous;
            Object.DestroyImmediate(pixels);
            Object.DestroyImmediate(target);
            Object.DestroyImmediate(cameraObject);
        }
    }

    private static void PoseArm(Transform patient, string side, float sign, System.Text.StringBuilder report)
    {
        var upper = Find(patient, "CC_Base_" + side + "_Upperarm");
        var forearm = Find(patient, "CC_Base_" + side + "_Forearm");
        var hand = Find(patient, "CC_Base_" + side + "_Hand");
        if (upper == null || forearm == null || hand == null)
            throw new System.InvalidOperationException("Falta un hueso del brazo " + side);

        // Partir siempre de la pose importada evita acumular giros en codos y muñecas.
        foreach (var bone in new[] { upper, forearm, hand })
        {
            var source = PrefabUtility.GetCorrespondingObjectFromSource(bone) as Transform;
            if (source == null) throw new System.InvalidOperationException("No se encontró la pose original de " + bone.name);
            bone.localRotation = source.localRotation;
        }

        // Los brazos quedan ligeramente separados del torso y orientados hacia los pies.
        Aim(upper, forearm, new Vector3(sign * .30f, -.025f, .953f));
        Aim(forearm, hand, new Vector3(sign * .08f, -.015f, .997f));
        var middleFinger = Find(patient, "CC_Base_" + side + "_Mid1");
        forearm.rotation = Quaternion.AngleAxis(sign * 45f, hand.position - forearm.position) * forearm.rotation;
        hand.rotation = Quaternion.AngleAxis(sign * 45f, middleFinger.position - hand.position) * hand.rotation;
        var fingerDirection = Vector3.ProjectOnPlane(middleFinger.position - hand.position, Vector3.up);
        hand.rotation = Quaternion.FromToRotation(fingerDirection, Vector3.forward) * hand.rotation;

        foreach (var bone in new[] { upper, forearm, hand })
        {
            var source = PrefabUtility.GetCorrespondingObjectFromSource(bone);
            AssetDatabase.TryGetGUIDAndLocalFileIdentifier(source, out string guid, out long id);
            var q = bone.localRotation;
            report.AppendLine($"{bone.name}|sourceId={id}|rotation={q.x:R},{q.y:R},{q.z:R},{q.w:R}|position={bone.position:F4}");
        }
        report.AppendLine($"{side} fingertips start={middleFinger.position:F4}");
    }

    private static void Aim(Transform pivot, Transform end, Vector3 targetDirection)
    {
        pivot.rotation = Quaternion.FromToRotation(end.position - pivot.position, targetDirection.normalized) * pivot.rotation;
    }

    private static Transform Find(Transform root, string name)
    {
        return root.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == name);
    }
}
