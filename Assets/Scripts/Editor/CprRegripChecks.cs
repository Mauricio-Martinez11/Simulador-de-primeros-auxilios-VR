using System;
using FirstAidVR.Rcp;
using UnityEngine;
public static class CprRegripChecks
{
 public static void Preview()
 {
  CprTrainingTools.BakeStackedHands();
  UnityEditor.SceneManagement.EditorSceneManager.OpenScene("Assets/Scenes/2_Tutorial_RCP.unity");
  var config=Resources.Load<CprTrainingSettings>("RCP/TrainingSettings");
  var tutorial=UnityEngine.Object.FindObjectOfType<CprTutorial>();
  var station=tutorial.GetComponent<CprStationController>();
  station.BuildLayout(config,tutorial.patient,tutorial.chestAnchor,tutorial.shoulderAnchor);
  var patient=station.StationRoot.GetComponent<CprStationPatient>(); patient.PrepareContact(tutorial.patient,station.Chest,config);
  var camera=new GameObject("Contact preview").AddComponent<Camera>(); camera.nearClipPlane=.01f; camera.fieldOfView=45;
  var contact=patient.ContactPose(station.Chest,0);
  station.View.Bind(config,camera);
  var session=CprStationChecks.Prepared(config);
  station.View.Guides(session,station.Chest,station.Shoulder,new Pose(station.Chest.position+station.Chest.rotation*Vector3.up*config.stackedHandHeight,station.Chest.rotation),false,contact);
  camera.transform.position=contact.position+station.Chest.rotation*new Vector3(-.28f,.34f,-.36f);
  camera.transform.LookAt(contact.position+station.Chest.rotation*Vector3.up*.025f,Vector3.up);
  foreach(var skin in tutorial.patient.GetComponentsInChildren<Renderer>()) Debug.Log("PATIENT PART: "+skin.name);
  Capture(camera,"hands-contact.png");
  camera.transform.position=contact.position+station.Chest.rotation*new Vector3(0,.60f,0);
  camera.transform.LookAt(contact.position,station.Chest.rotation*Vector3.forward);
  Capture(camera,"hands-top.png");
  camera.transform.position=contact.position+station.Chest.rotation*new Vector3(-.45f,.12f,0);
  camera.transform.LookAt(contact.position+station.Chest.rotation*Vector3.up*.03f,Vector3.up);
  Capture(camera,"hands-side.png");
  var mixed=new GameObject("Hologram test").AddComponent<CprMixedReality>();
  typeof(CprMixedReality).GetProperty("Active").GetSetMethod(true).Invoke(mixed,new object[]{true});
  mixed.SetSupport(tutorial.patient,config,true);
  foreach(var skin in tutorial.patient.GetComponentsInChildren<Renderer>())
    if(CprMixedReality.InteriorPart(skin.name)) Check(!skin.enabled,"hidden internal renderer");
  var head=System.Array.Find(tutorial.patient.GetComponentsInChildren<Transform>(),t=>t.name=="CC_Base_Head");
  camera.transform.position=head.position+station.Chest.rotation*new Vector3(-.25f,.3f,-.35f);
  camera.transform.LookAt(head.position,Vector3.up);
  Capture(camera,"patient-exterior.png");
  mixed.Restore();
 }
 static void Capture(Camera camera,string name)
 {
  var target=new RenderTexture(1200,1000,24); var tex=new Texture2D(1200,1000,TextureFormat.RGB24,false);
  camera.targetTexture=target; camera.Render(); var old=RenderTexture.active; RenderTexture.active=target;
  tex.ReadPixels(new Rect(0,0,1200,1000),0,0); tex.Apply(); System.IO.File.WriteAllBytes(name,tex.EncodeToPNG());
  RenderTexture.active=old; camera.targetTexture=null; UnityEngine.Object.DestroyImmediate(tex); UnityEngine.Object.DestroyImmediate(target);
 }
 static void Check(bool ok,string text) { if(!ok) throw new InvalidOperationException(text); }
 static CprHandSample H(bool right,float x=0,float depth=0) => new CprHandSample(new Pose(new Vector3(x,(right?0:.035f)-depth,0),Quaternion.identity));
 static void Tick(CprPracticeSession s,float x=0,float d=0) { s.Tick(.02f,H(false,x,d),H(true,0,d),Pose.identity,Pose.identity,new Pose(Vector3.up*.035f,Quaternion.identity),true,true,true); }
 public static void Run()
 {
  CprReliabilityChecks.Run();
  var config=Resources.Load<CprTrainingSettings>("RCP/TrainingSettings");
  var s=CprStationChecks.Prepared(config); s.SetPresentationGate(true,true);
  for(int i=0;i<50;i++) Tick(s,.4f);
  Check(!s.Placement.StackLatched,"separación observada durante explicación");
  for(int i=0;i<140;i++) Tick(s);
  Check(s.Placement.StackLatched && s.Count==0 && s.Step==CprPracticeStep.Compressions,"recoloca durante audio sin contar ni reiniciar paso");
  for(int i=0;i<50;i++) Tick(s,.4f);
  s.SetPresentationGate(false,true);
  for(int i=0;i<140;i++) Tick(s);
  Check(s.Placement.StackLatched && s.Step==CprPracticeStep.Compressions,"recoloca también al terminar explicación");
  for(float t=0;t<3*60/110f+.1f;t+=.02f) Tick(s,0,t<3*60/110f?.055f*(1-Mathf.Cos(t*Mathf.PI*110/30))*.5f:0);
  Check(s.Count==3 && s.HandSeparationErrors==0,"tres compresiones reales tras recuperación sin penalización previa");
  for(int i=0;i<50;i++) Tick(s,.4f);
  Check(s.Step==CprPracticeStep.Compressions && s.Count==3 && s.HandSeparationErrors==1 && !s.Explaining,"soltar tras iniciar no reinicia audio, paso ni contador");
  for(int i=0;i<50;i++) Tick(s,.4f);
  Check(s.HandSeparationErrors==1 && s.Placement.LowerLatched,"incidencia única y derecha disponible durante recolocación");
  for(int i=0;i<140;i++) Tick(s);
  for(float t=0;t<3*60/110f+.1f;t+=.02f) Tick(s,0,t<3*60/110f?.055f*(1-Mathf.Cos(t*Mathf.PI*110/30))*.5f:0);
  Check(s.Count==6 && s.Step==CprPracticeStep.Compressions && !s.Explaining,"contador sigue de tres a seis tras recolocar sin explicación");
  foreach(var name in new[]{"CC_Base_Eye","CC_Base_Teeth","CC_Base_Tongue","EyeOcclusion","Tearline"}) Check(CprMixedReality.InteriorPart(name),"excluye "+name);
  Check(!CprMixedReality.InteriorPart("CC_Base_Body") && !CprMixedReality.InteriorPart("Basic_T_shirts"),"conserva superficie exterior");
  Check(Resources.Load<Shader>("RCP/PatientHologram")!=null,"shader exterior incluido");
  CprTrainingTools.BakeStackedHands();
  Debug.Log("PASS: recovery during narration, compression restart, exterior filtering and hand bake");
 }
}
