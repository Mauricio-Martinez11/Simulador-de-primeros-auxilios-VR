using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
namespace FirstAidVR.Rcp
{
 public sealed class CprLiveFeedback : MonoBehaviour
 {
  private Canvas panel,overlay;
  private TextMeshProUGUI status,history;
  private readonly Queue<string> lines=new Queue<string>();
  private Material peripheral;
  private Mesh peripheralMesh;
  private Camera eye;
  private Transform peripheralQuad;
  private CprPracticeSession current;
  private int cycles,pacing,incidents;
  private float flash=-10,nextFlash;
  public void Build(Transform station,Camera camera)
  {
   panel=new GameObject("06_Incidencias en directo",typeof(RectTransform),typeof(Canvas)).GetComponent<Canvas>();
   panel.transform.SetParent(station,false); panel.renderMode=RenderMode.WorldSpace; panel.worldCamera=camera;
   panel.transform.localPosition=new Vector3(-1.52f,.72f,.29f); panel.transform.localRotation=Quaternion.Euler(10,-32,0); panel.transform.localScale=Vector3.one*.001f;
   ((RectTransform)panel.transform).sizeDelta=new Vector2(520,660);
   var bg=new GameObject("Fondo",typeof(RectTransform),typeof(CprRoundGraphic)).GetComponent<CprRoundGraphic>(); bg.transform.SetParent(panel.transform,false);
   bg.rectTransform.sizeDelta=new Vector2(520,660); bg.color=new Color(.035f,.08f,.12f,.96f); bg.radius=24; bg.raycastTarget=false;
   Label("AJUSTES EN DIRECTO",270,30);
   status=Label("",177,25); status.rectTransform.sizeDelta=new Vector2(465,120);
   history=Label("",-95,23); history.rectTransform.sizeDelta=new Vector2(465,380); history.alignment=TextAlignmentOptions.TopLeft; history.enableAutoSizing=true; history.fontSizeMin=17; history.fontSizeMax=23;
   overlay=new GameObject("Aviso periférico suave",typeof(RectTransform),typeof(Canvas)).GetComponent<Canvas>();
   overlay.renderMode=RenderMode.WorldSpace; overlay.worldCamera=camera; overlay.transform.SetParent(camera.transform,false);
   overlay.transform.localPosition=Vector3.forward*.35f; overlay.transform.localRotation=Quaternion.identity;
   var rect=(RectTransform)overlay.transform; rect.sizeDelta=new Vector2(.56f,.56f);
   eye=camera;
   var quad=new GameObject("Velo rojo periférico",typeof(MeshFilter),typeof(MeshRenderer));
   quad.transform.SetParent(camera.transform,false); peripheralQuad=quad.transform;
   peripheralMesh=new Mesh { name="Peripheral fullscreen quad" };
   peripheralMesh.vertices=new[]{new Vector3(-.5f,-.5f,0),new Vector3(.5f,-.5f,0),new Vector3(.5f,.5f,0),new Vector3(-.5f,.5f,0)};
   peripheralMesh.uv=new[]{Vector2.zero,Vector2.right,Vector2.one,Vector2.up}; peripheralMesh.triangles=new[]{0,2,1,0,3,2}; peripheralMesh.RecalculateBounds();
   quad.GetComponent<MeshFilter>().sharedMesh=peripheralMesh;
   peripheral=new Material(Resources.Load<Shader>("RCP/PeripheralError")); peripheral.color=Color.clear;
   var renderer=quad.GetComponent<MeshRenderer>(); renderer.sharedMaterial=peripheral; renderer.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off; renderer.receiveShadows=false;

  }
  TextMeshProUGUI Label(string value,float y,float size)
  {
   var t=new GameObject("Texto",typeof(RectTransform),typeof(TextMeshProUGUI)).GetComponent<TextMeshProUGUI>(); t.transform.SetParent(panel.transform,false);
   t.rectTransform.anchoredPosition=new Vector2(0,y); t.rectTransform.sizeDelta=new Vector2(465,70); t.fontSize=size; t.text=value; t.color=Color.white; t.raycastTarget=false; t.alignment=TextAlignmentOptions.Center; return t;
  }
  void Record(CprPracticeSession s,string text,bool error)
  {
   lines.Enqueue($"{(int)s.TotalSeconds/60:00}:{(int)s.TotalSeconds%60:00}  {text}"); while(lines.Count>5) lines.Dequeue();
   history.text=string.Join("\n\n",lines);
   if(error && Time.unscaledTime>=nextFlash) { flash=Time.unscaledTime; nextFlash=flash+1.5f; }
  }
  public void Tick(CprPracticeSession s,bool focus)
  {
   if(current!=s) { current=s; lines.Clear(); cycles=pacing=incidents=0; flash=-10; history.text="Las incidencias aparecerán aquí."; }
   bool active=s.Step==CprPracticeStep.Compressions && !s.Paused && focus;
   panel.gameObject.SetActive(active); overlay.gameObject.SetActive(false); peripheralQuad.gameObject.SetActive(active && !s.Explaining);
   if(!active) return;
   status.text=s.Explaining?"Escucha la indicación inicial":!s.Placement.StackLatched?"Coloca derecha e izquierda":!s.Detector.HasTracking?"Muestra tus manos al visor":!s.Detector.LocationCorrect?"Vuelve al centro del pecho":s.Detector.State==CprCaptureState.Recovering?"Sube al reposo para iniciar el conteo":$"Ciclos: {s.Count} · Recorrido: {s.Detector.DisplayDepth*100:0.0} cm";
   while(cycles<s.Events.Count)
   {
    var e=s.Events[cycles++];
    var issues=new List<string>();
    if(!e.fullReturn) issues.Add("retorno incompleto");
    if(!e.depthCorrect) issues.Add(e.depth<s.Settings.minimumVirtualDepth?"recorrido corto":"recorrido excesivo");
    if(e.rateEvaluable && !e.rateCorrect) issues.Add(e.rate> s.Settings.maximumRate?"demasiado rápido":"demasiado lento");
    if(issues.Count>0) Record(s,$"Ciclo {cycles}: "+string.Join(" · ",issues),true);
   }
   while(pacing<s.PacingEvents.Count)
   {
    var p=s.PacingEvents[pacing++];
    if(p.result!="OnTime") Record(s,p.result=="Early"?"Bajada adelantada: espera la zona verde":p.result=="Late"?"Bajada tardía: sigue el marcador":"Varias bajadas en un pulso",true);
   }
   while(incidents<s.Incidents.Count)
   {
    var entry=s.Incidents[incidents++];
    if(entry.type=="hands_away" || entry.type=="hands_separated" || entry.type=="pause") Record(s,entry.detail,true);
    else if(entry.type=="tracking_gap") Record(s,"Seguimiento perdido · sin descuento",false);
   }
   float age=Time.unscaledTime-flash;
   float alpha=age>=0 && age<.65f?Mathf.Sin(age/.65f*Mathf.PI)*.18f:0;
   peripheral.color=new Color(1,.06f,.03f,alpha);
   float distance=Mathf.Max(.2f,eye.nearClipPlane+.03f);
   float height=2*distance*Mathf.Tan(eye.fieldOfView*.5f*Mathf.Deg2Rad);
   peripheralQuad.localPosition=Vector3.forward*distance;
   peripheralQuad.localScale=new Vector3(height*eye.aspect,height,1);
  }
  void OnDestroy()
  {
   if(peripheralQuad!=null) { if(Application.isPlaying) Destroy(peripheralQuad.gameObject); else DestroyImmediate(peripheralQuad.gameObject); }
   if(peripheral!=null) { if(Application.isPlaying) Destroy(peripheral); else DestroyImmediate(peripheral); }
   if(peripheralMesh!=null) { if(Application.isPlaying) Destroy(peripheralMesh); else DestroyImmediate(peripheralMesh); }
   if(panel!=null) { if(Application.isPlaying) Destroy(panel.gameObject); else DestroyImmediate(panel.gameObject); }
   if(overlay!=null) { if(Application.isPlaying) Destroy(overlay.gameObject); else DestroyImmediate(overlay.gameObject); }
  }
 }
}
