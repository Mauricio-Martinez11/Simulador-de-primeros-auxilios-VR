using System;
using FirstAidVR.Rcp;
using UnityEngine;
using TMPro;
public static class CprObservationFeedbackChecks
{
 public static void Preview()
 {
  UnityEditor.SceneManagement.EditorSceneManager.OpenScene("Assets/Scenes/2_Tutorial_RCP.unity");
  var config=Resources.Load<CprTrainingSettings>("RCP/TrainingSettings"); var tutorial=UnityEngine.Object.FindObjectOfType<CprTutorial>();
  var station=tutorial.GetComponent<CprStationController>(); station.BuildLayout(config,tutorial.patient,tutorial.chestAnchor,tutorial.shoulderAnchor);
  var patient=station.StationRoot.GetComponent<CprStationPatient>();
  var shoulder=patient.ShoulderSurface(tutorial.patient,station.Shoulder,config.leftHand);
  Check(patient.ShoulderMinimumClearance>=.0019f,"dedos y palma fuera de piel y ropa");
  Check(patient.ShoulderPalmClearance<=.004f,"palma apoyada con margen máximo de cuatro milímetros");
  Debug.Log("PASS: shoulder mesh outside body, palm clearance="+patient.ShoulderPalmClearance);
  Debug.Log("Shoulder surface adjustment: "+Vector3.Distance(shoulder.position,station.Shoulder.position));
  var camera=new GameObject("Preview camera").AddComponent<Camera>(); camera.nearClipPlane=.01f; camera.fieldOfView=48;
  station.View.Bind(config,camera); station.View.ResponseHand=patient.ShoulderHand;
  var s=CprStationChecks.AtStep(config,CprPracticeStep.Response);
  s.Tick(.02f,new CprHandSample(shoulder),default,shoulder,station.Chest,Pose.identity,false,true,true);
  station.View.Guides(s,station.Chest,shoulder,Pose.identity);
  camera.transform.position=shoulder.position+station.Chest.rotation*new Vector3(-.25f,.4f,-.3f);
  camera.transform.LookAt(shoulder.position,Vector3.up);
  Capture(camera,"shoulder-feedback.png");
  camera.transform.position=shoulder.position+station.Chest.rotation*new Vector3(-.45f,.09f,-.12f);
  camera.transform.LookAt(shoulder.position,Vector3.up); Capture(camera,"shoulder-contact-side.png");
  camera.transform.position=shoulder.position+station.Chest.rotation*new Vector3(0,.55f,0);
  camera.transform.LookAt(shoulder.position,station.Chest.rotation*Vector3.forward); Capture(camera,"shoulder-contact-top.png");
  var safety=CprStationChecks.AtStep(config,CprPracticeStep.Safety);
  station.View.Guides(safety,station.Chest,shoulder,Pose.identity);
  var actions=station.StationRoot.GetComponent<CprScenarioActions>();
  var head=new Pose(station.Chest.position-station.StationRoot.forward*.85f+Vector3.up*1.5f,station.StationRoot.rotation);
  actions.Tick(safety,head,default,default,.02f,true);
  camera.transform.position=head.position+Vector3.up*.7f-station.StationRoot.forward*.5f;
  camera.transform.LookAt(station.Chest.position,Vector3.up); camera.fieldOfView=85;
  Capture(camera,"floor-observation.png"); actions.Hide(); camera.fieldOfView=48;
  s=CprStationChecks.AtStep(config,CprPracticeStep.Breathing); station.View.Guides(s,station.Chest,shoulder,Pose.identity);
  for(int i=0;i<100;i++) s.Tick(.02f,default,default,shoulder,station.Chest,Pose.identity,true,true,true);
  var panel=new GameObject("Breathing progress").AddComponent<CprBreathingPanel>(); panel.Bind(camera); panel.SetChest(station.Chest); panel.Show(s,true,true);
  camera.transform.position=station.Chest.position+station.Chest.rotation*new Vector3(0,.5f,-.65f);
  camera.transform.LookAt(station.Chest.position+Vector3.up*.1f,Vector3.up); panel.SetChest(station.Chest);
  Capture(camera,"breathing-progress.png");
 }
 static void Capture(Camera camera,string name)
 {
  Canvas.ForceUpdateCanvases(); var rt=new RenderTexture(1200,1000,24); var image=new Texture2D(1200,1000,TextureFormat.RGB24,false); var previous=RenderTexture.active;
  camera.targetTexture=rt; camera.Render(); RenderTexture.active=rt; image.ReadPixels(new Rect(0,0,1200,1000),0,0); image.Apply();
  System.IO.File.WriteAllBytes(name,image.EncodeToPNG()); RenderTexture.active=previous; camera.targetTexture=null;
  UnityEngine.Object.DestroyImmediate(image); UnityEngine.Object.DestroyImmediate(rt);
 }
 static int passed;
 static void Check(bool ok,string detail) { if(!ok) throw new InvalidOperationException(detail); passed++; }
 public static void RunLatest()
 {
  CprRecenterChecks.Run(); CprMenuIntroChecks.Run();
  foreach(var key in new[]{"Step03","Confirm03","Confirm01"})
   Check(!CprNarration.Get(key).text.Contains("simulado"),"sin coletillas repetidas: "+key);
  Check(!CprNarration.Get("Welcome").text.StartsWith("Hola"),"selector sin repetir bienvenida");
  Debug.Log("PASS: floor markers and direct narration regressions");
 }
 public static void Run()
 {
  CprV2Checks.Run();
  var settings=Resources.Load<CprTrainingSettings>("RCP/TrainingSettings");
  var go=new GameObject("Observation checks"); var camera=go.AddComponent<Camera>();
  var panel=go.AddComponent<CprBreathingPanel>(); panel.Bind(camera); panel.SetChest(Pose.identity);
  var s=CprStationChecks.AtStep(settings,CprPracticeStep.Breathing);
  for(int i=0;i<50;i++) s.Tick(.02f,default,default,Pose.identity,Pose.identity,Pose.identity,true,true,true);
  panel.Show(s,true,true);
  Check(panel.BreathingText.Contains("OBSERVANDO"),"mirada ofrece respuesta inmediata");
  var fill=panel.PanelTransform.Find("Avance circular").GetComponent<CprRingGraphic>();
  Check(fill.amount>0 && fill.amount<1,"aro avanza con tiempo observado");
  float progress=fill.amount;
  s.Tick(.02f,default,default,Pose.identity,Pose.identity,Pose.identity,false,true,true); panel.Show(s,true,false);
  Check(fill.amount==progress && panel.BreathingText.Contains("Mira"),"apartar mirada conserva avance y orienta");
  Check(panel.PanelTransform.childCount==3,"sin tarjeta ni respuesta extra");
  s=CprStationChecks.AtStep(settings,CprPracticeStep.Response); s.SetPresentationGate(true,true);
  s.Tick(.02f,new CprHandSample(Pose.identity),default,Pose.identity,Pose.identity,Pose.identity,false,true,true);
  Check(s.ResponseTouching && !s.StepPassed,"contacto reconoce mano durante voz sin saltar paso");
  var view=go.AddComponent<CprStationView>(); view.Build(settings); view.Guides(s,Pose.identity,Pose.identity,Pose.identity);
  var hand=view.transform.Find("Guía izquierda RCP").GetComponent<Renderer>(); var block=new MaterialPropertyBlock(); hand.GetPropertyBlock(block);
  var color=block.GetColor("_Color"); Check(color.g>.8f && color.r<.3f,"hombro cambia a verde");
  s.Tick(.02f,new CprHandSample(new Pose(Vector3.right,Quaternion.identity)),default,Pose.identity,Pose.identity,Pose.identity,false,true,true);
  Check(!s.ResponseTouching,"retirar mano elimina contacto");
  Check(!CprNarration.Get("Step04").text.Contains("llamada real"),"frase retirada del audio cuatro");
  UnityEngine.Object.DestroyImmediate(go);
  Debug.Log($"PASS: {passed} observation feedback checks");
 }
}
