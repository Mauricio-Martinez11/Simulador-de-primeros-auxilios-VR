using System;
using UnityEngine;
using FirstAidVR.Rcp;
public static class CprShoulderChecks
{
 static void Check(bool ok,string message) { if(!ok) throw new InvalidOperationException(message); }
 static CprHandSample H(float x) => new CprHandSample(new Pose(Vector3.right*x,Quaternion.identity));
 public static void Run()
 {
  var c=new CprShoulderContact();
  for(int i=0;i<100;i++) c.Tick(.02f,H(0),default,Pose.identity,true,false);
  Check(c.Latched && !c.Completed && c.Movements==0,"contacto durante audio no inicia animación");
  for(int i=0;i<36;i++) c.Tick(.02f,H(0),default,Pose.identity,true,true);
  Check(c.Movements==1 && !c.Completed,"un movimiento no completa todavía");
  for(int i=0;i<40;i++) c.Tick(.02f,H(0),default,Pose.identity,true,true);
  Check(c.Completed && c.Movements==2 && c.Animation==0,"dos movimientos con contacto completan y quedan en reposo");
  c.Tick(.02f,H(.2f),default,Pose.identity,true,true); Check(!c.Latched,"retirada libera");
  c.Tick(.02f,default,H(0),Pose.identity,true,true); Check(c.Hand==1,"recoloca con derecha");
  c.Tick(.02f,default,default,Pose.identity,false,true); Check(!c.Latched && c.Animation==0,"pausa limpia animación");
  CprObservationFeedbackChecks.RunLatest();
  Debug.Log("PASS: shoulder contact, release, reacquisition, two movements, narration gate and bounded animation");
 }
}
