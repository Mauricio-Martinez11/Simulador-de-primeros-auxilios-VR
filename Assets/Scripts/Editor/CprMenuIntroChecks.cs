using System;
using FirstAidVR.Rcp;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
public static class CprMenuIntroChecks
{
 static void Check(bool ok,string message) { if(!ok) throw new InvalidOperationException(message); }
 public static void Run()
 {
  CprObservationFeedbackChecks.Run();
  var go=new GameObject("Menu introduction test"); var camera=go.AddComponent<Camera>();
  var panel=go.AddComponent<CprIntroPanel>(); panel.Build(camera); panel.EnableNavigation();
  panel.Present("MenuWelcome","Escuchar instrucciones","Ir al menú"); panel.SetNavigationStep(-1);
  Check(Resources.Load<AudioClip>("RCP/Narration/MenuWelcome")!=null,"audio de bienvenida incluido");
  foreach(string key in new[]{"MenuWelcome","Look","Hands","SelectHands","SelectControllers","ControlsDone"})
  {
   var timing=JsonUtility.FromJson<CprIntroCompanion.Timings>(Resources.Load<TextAsset>("RCP/Narration/"+key+"Timing").text);
   var full=CprNarration.Get(key).text; float last=-1;
   foreach(var w in timing.words) { Check(w.time>=last && w.start+w.length<=full.Length,"tiempos y posiciones válidos "+key); last=w.time; }
   Check(last<Resources.Load<AudioClip>("RCP/Narration/"+key).length,"tiempos dentro del audio "+key);
   var first=timing.words[0]; Check(CprIntroCompanion.Caption(full,timing,first.time,false)==full.Substring(first.start,first.length),"primera palabra al tiempo de voz");
  }
  var canvas=go.GetComponentInChildren<Canvas>();
  Check(canvas.GetComponentInChildren<Oculus.Interaction.Surfaces.BoundsClipper>().Size.x>=2100,"láser alcanza paneles laterales");
  panel.SetNavigationStep(1); panel.Navigate(-1); Check(panel.Navigation==-1,"anterior disponible");
  panel.Present("Hands"); Check(panel.Navigation==0,"nuevo paso consume navegación");
  panel.Replay(); Check(panel.GetComponent<CprNarrator>().Key=="Hands","repetir conserva indicación");
  var input=go.AddComponent<CprTrackedHands>();
  var walkthrough=CprFamiliarization.Run(panel,camera,input,Resources.Load<CprTrainingSettings>("RCP/TrainingSettings"));
  Check(walkthrough.MoveNext() && panel.GetComponent<CprNarrator>().Key=="Look","recorrido empieza después de elegir instrucciones");
  panel.Navigate(1); Check(walkthrough.MoveNext() && panel.GetComponent<CprNarrator>().Key=="Hands","siguiente cambia indicación sin esperar acción anterior");
  panel.Navigate(-1); Check(walkthrough.MoveNext() && panel.GetComponent<CprNarrator>().Key=="Look","anterior vuelve y reproduce indicación correspondiente");
  panel.Skip(); Check(panel.SkipRequested && panel.GetComponent<CprNarrator>().Key==null,"saltar detiene audio");
  var routine=CprFamiliarization.Run(panel,camera,input,Resources.Load<CprTrainingSettings>("RCP/TrainingSettings"));
  Check(!routine.MoveNext() && CprFamiliarization.Completed,"saltar evita acciones pendientes de familiarización");
  UnityEngine.Object.DestroyImmediate(go);
  Debug.Log("PASS: menu welcome audio, word timestamps, ray bounds, navigation, replay and skip");
 }
 public static void Preview()
 {
  UnityEditor.SceneManagement.EditorSceneManager.NewScene(UnityEditor.SceneManagement.NewSceneSetup.EmptyScene);
  var camera=new GameObject("Preview camera").AddComponent<Camera>(); camera.fieldOfView=76; camera.backgroundColor=new Color(.055f,.11f,.15f); camera.clearFlags=CameraClearFlags.SolidColor;
  var panel=new GameObject("Main introduction").AddComponent<CprIntroPanel>(); panel.Build(camera); panel.EnableNavigation(); panel.Present("MenuWelcome","Escuchar instrucciones","Ir al menú"); panel.SetNavigationStep(-1);
  var companion=panel.GetComponent<CprIntroCompanion>();
  typeof(CprIntroCompanion).GetMethod("Update",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).Invoke(companion,null);
  Canvas.ForceUpdateCanvases();
  var rt=new RenderTexture(1800,1000,24); var image=new Texture2D(1800,1000,TextureFormat.RGB24,false); camera.targetTexture=rt; camera.Render(); RenderTexture.active=rt;
  image.ReadPixels(new Rect(0,0,1800,1000),0,0); image.Apply(); System.IO.File.WriteAllBytes("main-intro.png",image.EncodeToPNG());
  camera.targetTexture=null; RenderTexture.active=null; UnityEngine.Object.DestroyImmediate(image); UnityEngine.Object.DestroyImmediate(rt);
  CprObservationFeedbackChecks.Preview();
 }
}
