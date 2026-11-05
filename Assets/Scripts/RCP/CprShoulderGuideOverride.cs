using UnityEngine;
namespace FirstAidVR.Rcp
{
 [ExecuteAlways]
 public sealed class CprShoulderGuideOverride : MonoBehaviour
 {
  public bool showInEditor=false;
  public Mesh GuideMesh => GetComponent<MeshFilter>().sharedMesh;
  void OnValidate() { OnEnable(); }
  void OnEnable() { var renderer=GetComponent<Renderer>(); if(renderer!=null) renderer.enabled=!Application.isPlaying && showInEditor; }
 }
}
