using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace FirstAidVR.Rcp.Consulta
{
    public sealed class ActivacionDeRealidadMixtaRCP : MonoBehaviour
    {
        private Camera cameraView;
        private CameraClearFlags originalFlags;
        private Color originalBackground;
        private OVRManager manager;
        private bool originalPassthrough, cameraSaved;
        private OVRPassthroughLayer layer;
        private readonly List<Renderer> hidden=new List<Renderer>();
        private readonly Dictionary<Renderer,Material[]> patientMaterials=new Dictionary<Renderer,Material[]>();
        private Material ghost;
        private readonly Dictionary<Renderer,bool> supportVisibility=new Dictionary<Renderer,bool>();
        public static bool InteriorPart(string name)
        { name=name.ToLowerInvariant(); return name.Contains("teeth") || name.Contains("tongue") || name.Contains("eye") || name.Contains("tear") || name.Contains("occlusion"); }
        public bool Active { get; private set; }
        public bool UsingSupport { get; private set; }
        public bool Failed { get; private set; }

        public IEnumerator Enable(Camera camera,Transform station)
        {
            Failed=false; manager=OVRManager.instance;
            if(manager==null || !OVRManager.isHmdPresent) { Failed=true; yield break; }
            cameraView=camera; originalFlags=camera.clearFlags; originalBackground=camera.backgroundColor; cameraSaved=true;
            originalPassthrough=manager.isInsightPassthroughEnabled;
            manager.isInsightPassthroughEnabled=true;
            layer=gameObject.AddComponent<OVRPassthroughLayer>(); layer.overlayType=OVROverlay.OverlayType.Underlay;
            float wait=0;
            while(!OVRManager.IsInsightPassthroughInitialized() && wait<8)
            { wait+=Time.unscaledDeltaTime; yield return null; }
            if(!OVRManager.IsInsightPassthroughInitialized()) { Restore(); Failed=true; yield break; }
            foreach(var renderer in FindObjectsOfType<Renderer>())
            {
                if(!renderer.enabled || renderer.transform.IsChildOf(station) || renderer.GetComponentInParent<Canvas>()!=null
                    || renderer.GetComponentInParent<OVRCameraRig>()!=null || renderer.GetComponentInParent<PersistentCore>()!=null) continue;
                hidden.Add(renderer); renderer.enabled=false;
            }
            camera.clearFlags=CameraClearFlags.SolidColor; camera.backgroundColor=Color.clear;
            Active=true;
        }
        public void SetSupport(Transform patient,ConfiguracionDeCriteriosRCP settings,bool enabled)
        {
            UsingSupport=enabled && Active;
            if(!UsingSupport) { RestorePatient(); return; }
            if(ghost==null)
            {
                ghost=new Material(Resources.Load<Shader>("RCP/PatientHologram")); ghost.name="Paciente tenue de alineación";
                ghost.SetColor("_Color",new Color(.2f,.8f,.95f,.09f));
            }
            foreach(var renderer in patient.GetComponentsInChildren<Renderer>(true))
            {
                if(!supportVisibility.ContainsKey(renderer)) supportVisibility.Add(renderer,renderer.enabled);
                if(InteriorPart(renderer.name)) { renderer.enabled=false; continue; }
                if(!patientMaterials.ContainsKey(renderer)) patientMaterials.Add(renderer,renderer.sharedMaterials);
                var originals=patientMaterials[renderer]; var materials=new Material[originals.Length];
                for(int i=0;i<materials.Length;i++) materials[i]=ghost;
                renderer.sharedMaterials=materials;
            }
        }
        private void RestorePatient()
        { foreach(var pair in patientMaterials) if(pair.Key!=null) pair.Key.sharedMaterials=pair.Value; patientMaterials.Clear(); foreach(var pair in supportVisibility) if(pair.Key!=null) pair.Key.enabled=pair.Value; supportVisibility.Clear(); }
        public void Restore()
        {
            RestorePatient(); UsingSupport=Active=false;
            foreach(var renderer in hidden) if(renderer!=null) renderer.enabled=true;
            hidden.Clear();
            if(cameraSaved && cameraView!=null) { cameraView.clearFlags=originalFlags; cameraView.backgroundColor=originalBackground; }
            cameraSaved=false;
            if(layer!=null) { layer.enabled=false; Destroy(layer); layer=null; }
            if(manager!=null) manager.isInsightPassthroughEnabled=originalPassthrough;
        }
        private void OnDisable() { Restore(); }
        private void OnDestroy() { if(ghost!=null) Destroy(ghost); }
    }
}
