using System.IO;
using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
public static class InspectPatientRig {
 public static void Run() {
 EditorSceneManager.OpenScene("Assets/Scenes/2_Tutorial_RCP.unity");
 var patient=GameObject.Find("Hombre_Paciente").transform;
 File.WriteAllLines("patient-rig.txt",patient.GetComponentsInChildren<Transform>().Where(t=>t.name.StartsWith("CC_Base") || t==patient).Select(t=>$"{t.name}|parent={t.parent?.name}|pos={t.position:F4}|rot={t.eulerAngles:F2}|local={t.localPosition:F4}|scale={t.lossyScale:F4}"));
 File.AppendAllLines("patient-rig.txt",Object.FindObjectsOfType<Collider>().Select(c=>$"Collider {c.name} type={c.GetType().Name} bounds={c.bounds}"));
 EditorApplication.Exit(0);
 }
}
