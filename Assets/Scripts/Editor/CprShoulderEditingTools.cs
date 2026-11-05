using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using FirstAidVR.Rcp;
public static class CprShoulderEditingTools
{
 public static void FinalizePlacement()
 {
  EditorSceneManager.OpenScene("Assets/Scenes/2_Tutorial_RCP.unity");
  var t=Object.FindObjectOfType<CprTutorial>(); var s=t.GetComponent<CprStationController>();
  var g=Object.FindObjectOfType<CprShoulderGuideOverride>(); var chosen=new Pose(g.transform.position,g.transform.rotation);
  s.BuildLayout(t.settings,t.patient,t.chestAnchor,t.shoulderAnchor);
  if(Vector3.Distance(s.Shoulder.position,chosen.position)>.0001f) throw new System.Exception("La pose elegida debe conservarse");
  var hand=g.GuideMesh; var points=hand.vertices; var corrections=new float[points.Length]; var normal=s.Chest.rotation*Vector3.up;
  var probe=new GameObject("Clearance probe").AddComponent<MeshCollider>(); var body=new Mesh();
  float maximum=0;
  foreach(var skin in t.patient.GetComponentsInChildren<SkinnedMeshRenderer>())
  {
   if(!skin.enabled || (!skin.name.Contains("Body") && !skin.name.Contains("shirts"))) continue;
   skin.BakeMesh(body); var vertices=body.vertices;
   for(int i=0;i<vertices.Length;i++) vertices[i]=skin.transform.position+skin.transform.rotation*vertices[i];
   body.vertices=vertices; body.RecalculateBounds(); probe.sharedMesh=null; probe.sharedMesh=body;
   for(int i=0;i<points.Length;i++)
   {
    var world=g.transform.TransformPoint(points[i]);
    if(probe.Raycast(new Ray(world+normal*.35f,-normal),out var hit,.7f))
    { corrections[i]=Mathf.Max(corrections[i],Vector3.Dot(hit.point-world,normal)+.001f); maximum=Mathf.Max(maximum,corrections[i]); }
   }
  }
  var adjusted=(Vector3[])points.Clone();
  for(int i=0;i<points.Length;i++)
  {
   float lift=corrections[i];
   for(int j=0;j<points.Length;j++) lift=Mathf.Max(lift,corrections[j]-Vector3.Distance(points[i],points[j])*.7f);
   adjusted[i]+=g.transform.InverseTransformVector(normal*lift);
  }
  hand.vertices=adjusted; hand.RecalculateNormals(); hand.RecalculateBounds(); EditorUtility.SetDirty(hand); AssetDatabase.SaveAssets();
  probe.sharedMesh=null; Object.DestroyImmediate(probe.gameObject); Object.DestroyImmediate(body);
  var patient=s.StationRoot.GetComponent<CprStationPatient>(); var bodyStart=t.patient.position;
  for(int i=0;i<140;i++)
  {
   var offset=normal*(.008f*Mathf.Sin(i*.1f)); patient.SetShoulderMotion(offset);
   if(Vector3.Distance(t.patient.position-bodyStart,offset)>.00001f) throw new System.Exception("Movimiento coherente del paciente: "+(t.patient.position-bodyStart)+" / "+offset);
   var visual=chosen.position+offset;
   if(Vector3.Distance(visual-t.patient.position,chosen.position-bodyStart)>.00001f) throw new System.Exception("Contacto debe conservarse durante movimiento");
  }
  patient.SetShoulderMotion(Vector3.zero);
  var session=CprStationChecks.AtStep(t.settings,CprPracticeStep.Response);
  s.View.Guides(session,s.Chest,s.Shoulder,Pose.identity);
  if(s.StationRoot.Find("Zona de reposo").gameObject.activeSelf) throw new System.Exception("Círculo del hombro debe ocultarse");
  Debug.Log("PASS: authored pose retained, shoulder ring hidden, rigid contact during animation; max clearance correction="+maximum);
 }
 public static void Verify()
 {
  EditorSceneManager.OpenScene("Assets/Scenes/2_Tutorial_RCP.unity");
  var guide=Object.FindObjectOfType<CprShoulderGuideOverride>();
  if(guide==null || !guide.GetComponent<Renderer>().enabled || guide.GuideMesh==null) throw new System.Exception("Falta mano editable visible");
  var t=Object.FindObjectOfType<CprTutorial>(); var s=t.GetComponent<CprStationController>();
  guide.transform.position+=Vector3.right*.03f;
  var position=guide.transform.position;
  s.BuildLayout(t.settings,t.patient,t.chestAnchor,t.shoulderAnchor);
  if(Vector3.Distance(s.Shoulder.position,position)>.0001f) throw new System.Exception("No respeta ajuste manual");
  var camera=new GameObject("Aviso camera").AddComponent<Camera>(); camera.transform.position=s.StationRoot.position+new Vector3(0,1,-2);
  camera.transform.LookAt(s.StationRoot.position+Vector3.up*.4f); camera.nearClipPlane=.01f;
  var feedback=new GameObject("Live preview").AddComponent<CprLiveFeedback>(); feedback.Build(s.StationRoot,camera);
  var session=CprStationChecks.Prepared(t.settings); feedback.Tick(session,true);
  var field=typeof(CprLiveFeedback).GetField("peripheral",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance);
  ((Material)field.GetValue(feedback)).color=new Color(1,.06f,.03f,.18f);
  var rt=new RenderTexture(1200,800,24); camera.targetTexture=rt; camera.Render(); RenderTexture.active=rt;
  var image=new Texture2D(1200,800,TextureFormat.RGB24,false); image.ReadPixels(new Rect(0,0,1200,800),0,0); image.Apply();
  System.IO.File.WriteAllBytes("peripheral-error-preview.png",image.EncodeToPNG()); camera.targetTexture=null; RenderTexture.active=null;
  Object.DestroyImmediate(image); Object.DestroyImmediate(rt);
  Debug.Log("PASS: visible editable shoulder respected; peripheral red preview rendered");
 }
 public static void Prepare()
 {
  var scene=EditorSceneManager.OpenScene("Assets/Scenes/2_Tutorial_RCP.unity");
  var tutorial=Object.FindObjectOfType<CprTutorial>(); var controller=tutorial.GetComponent<CprStationController>();
  controller.BuildLayout(tutorial.settings,tutorial.patient,tutorial.chestAnchor,tutorial.shoulderAnchor);
  var patient=controller.StationRoot.GetComponent<CprStationPatient>();
  var pose=patient.ShoulderSurface(tutorial.patient,controller.Shoulder,tutorial.settings.leftHand);
  const string path="Assets/Resources/RCP/ShoulderManualGuide.asset";
  var mesh=Object.Instantiate(patient.ShoulderHand); AssetDatabase.CreateAsset(mesh,path);
  var go=new GameObject("AJUSTAR_HOLOGRAMA_HOMBRO",typeof(MeshFilter),typeof(MeshRenderer),typeof(CprShoulderGuideOverride));
  go.transform.SetParent(controller.StationRoot,false); go.transform.SetPositionAndRotation(pose.position,pose.rotation);
  go.GetComponent<MeshFilter>().sharedMesh=mesh; go.GetComponent<MeshRenderer>().sharedMaterial=tutorial.settings.hologramMaterial;
  go.GetComponent<MeshRenderer>().shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;
  EditorSceneManager.SaveScene(scene,"Assets/Scenes/ShoulderEditingExport.unity"); AssetDatabase.SaveAssets();
  Debug.Log("PASS: editable shoulder guide exported; patient and panels kept in place");
 }
}
