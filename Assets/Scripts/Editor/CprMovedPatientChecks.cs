using System;
using UnityEngine;
using FirstAidVR.Rcp;
public static class CprMovedPatientChecks
{
 [Serializable] public class AnchorData { public Vector3 chest,shoulder; public Quaternion chestRotation,shoulderRotation; }
 public static void ExportAnchors()
 {
  UnityEditor.SceneManagement.EditorSceneManager.OpenScene("Assets/Scenes/2_Tutorial_RCP.unity");
  var t=UnityEngine.Object.FindObjectOfType<CprTutorial>(); var s=t.GetComponent<CprStationController>();
  s.BuildLayout(t.settings,t.patient,t.chestAnchor,t.shoulderAnchor);
  var root=s.StationRoot;
  System.IO.File.WriteAllText("repaired-anchors.json",JsonUtility.ToJson(new AnchorData {
   chest=root.InverseTransformPoint(s.Chest.position),shoulder=root.InverseTransformPoint(s.Shoulder.position),
   chestRotation=Quaternion.Inverse(root.rotation)*s.Chest.rotation,shoulderRotation=Quaternion.Inverse(root.rotation)*s.Shoulder.rotation },true));
  Run();
 }
 public static void Run()
 {
  UnityEditor.SceneManagement.EditorSceneManager.OpenScene("Assets/Scenes/2_Tutorial_RCP.unity");
  var t=UnityEngine.Object.FindObjectOfType<CprTutorial>(); var s=t.GetComponent<CprStationController>();
  var p=t.patient; var position=p.position; var rotation=p.rotation;
  var panels=s.View.Panels; var panelPositions=new Vector3[panels.Length];
  for(int i=0;i<panels.Length;i++) panelPositions[i]=panels[i].transform.position;
  s.BuildLayout(t.settings,p,t.chestAnchor,t.shoulderAnchor);
  Check(Vector3.Distance(p.position,position)<.00001f && Quaternion.Angle(p.rotation,rotation)<.001f,"posición elegida conservada");
  panels=s.View.Panels;
  for(int i=0;i<panels.Length;i++) Check(Vector3.Distance(panelPositions[i],panels[i].transform.position)<.00001f,"pantallas conservan distribución de escena");
  var chest=s.Chest.position; var shoulder=s.Shoulder.position;
  var delta=new Vector3(-.7f,0,.4f); p.position+=delta; position=p.position;
  s.BuildLayout(t.settings,p,t.chestAnchor,t.shoulderAnchor);
  Check(Vector3.Distance(p.position,position)<.00001f,"no devuelve modelo a posición anterior");
  Check(Vector3.Distance(s.Chest.position,chest+delta)<.001f,"pecho sigue traslación del modelo");
  Check(Vector3.Distance(s.Shoulder.position,shoulder+delta)<.001f,"hombro sigue traslación del modelo");
  s.BuildLayout(t.settings,p,t.chestAnchor,t.shoulderAnchor);
  Check(Vector3.Distance(s.Chest.position,chest+delta)<.001f,"repetir ajuste no acumula desplazamiento");
  Debug.Log("PASS: moved patient preserved, chest/shoulder follow and station rebase idempotent");
  CprObservationFeedbackChecks.Preview();
 }
 static void Check(bool ok,string detail) { if(!ok) throw new InvalidOperationException(detail); }
}
