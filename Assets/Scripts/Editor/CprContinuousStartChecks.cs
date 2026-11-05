using System;
using UnityEngine;
using FirstAidVR.Rcp;
public static class CprContinuousStartChecks
{
 public static void Run()
 {
  var config=Resources.Load<CprTrainingSettings>("RCP/TrainingSettings");
  foreach(float rate in new[]{110f,150f,190f})
  {
   var d=new CprCompressionDetector(config); int count=0,fast=0;
   d.Compression+=e=>{ count++; if(e.rateEvaluable && !e.rateCorrect && e.rate>120) fast++; };
   var low=new CprHandSample(Pose.identity); var high=new CprHandSample(new Pose(Vector3.up*.035f,Quaternion.identity)); d.Calibrate(high,low,Pose.identity);
   float t=0;
   for(int i=0;i<900;i++)
   {
    t+=1/90f; float depth=.055f*(1-Mathf.Cos(t*Mathf.PI*rate/30))*.5f;
    var l=new CprHandSample(new Pose(Vector3.up*(.035f-depth),Quaternion.identity));
    d.Tick(1/90f,t,l,default,Pose.identity,true,true);
   }
   if(count<8 || rate>120 && fast<3) throw new InvalidOperationException("Arranque continuo/ritmo rápido: "+rate+" count="+count);
   Debug.Log("PASS: continuous start "+rate+" cpm, cycles="+count+", fast="+fast);
  }
  foreach(float offset in new[]{-.025f,.025f})
  {
   var d=new CprCompressionDetector(config); int count=0;
   d.Compression+=e=>count++;
   d.Calibrate(new CprHandSample(new Pose(Vector3.up*.035f,Quaternion.identity)),new CprHandSample(Pose.identity),Pose.identity);
   for(int i=0;i<720;i++)
   {
    float t=i/90f; float depth=offset+.055f*(1-Mathf.Cos(t*Mathf.PI*110/30))*.5f;
    d.Tick(1/90f,t,new CprHandSample(new Pose(Vector3.up*(.035f-depth),Quaternion.identity)),default,Pose.identity,true,true);
   }
   if(count<10) throw new InvalidOperationException("Altura de reposo desplazada: "+offset+" ciclos="+count);
   Debug.Log("PASS: settled hand offset "+offset+" cycles="+count);
  }
  CprRegripChecks.Run();
  var go=new GameObject("Feedback checks"); var cam=new GameObject("Camera").AddComponent<Camera>();
  var feedback=go.AddComponent<CprLiveFeedback>(); feedback.Build(go.transform,cam);
  var session=CprStationChecks.Prepared(config);
  for(int i=0;i<700 && !session.Closed;i++)
  {
   float depth=i<40?0:.03f*(1-Mathf.Cos((i-40)/90f*Mathf.PI*150/30))*.5f;
   var l=new CprHandSample(new Pose(Vector3.up*(.035f-depth),Quaternion.identity));
   var r=new CprHandSample(new Pose(Vector3.down*depth,Quaternion.identity));
   session.Tick(1/90f,l,r,Pose.identity,Pose.identity,new Pose(Vector3.up*.035f,Quaternion.identity),true,true,true);
   feedback.Tick(session,true);
  }
  bool hasError=false; foreach(var label in go.GetComponentsInChildren<TMPro.TMP_Text>(true)) hasError|=label.text.Contains("rápido") || label.text.Contains("recorrido corto");
  if(!hasError || session.Count<5) throw new InvalidOperationException("El historial debe mostrar incidencias de ciclos contados");
  feedback.Tick(session,false);
  if(go.transform.Find("06_Incidencias en directo").gameObject.activeSelf) throw new InvalidOperationException("Panel debe ocultarse al perder foco");
  UnityEngine.Object.DestroyImmediate(go); UnityEngine.Object.DestroyImmediate(cam.gameObject);
  Debug.Log("PASS: live feedback records cycle errors, count continues, focus hides panel");
 }
}
