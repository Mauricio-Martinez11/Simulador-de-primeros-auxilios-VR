using System.Collections;
using UnityEngine;

namespace FirstAidVR.Rcp
{
    public sealed class CprStationPreparation : MonoBehaviour
    {
        public CprMixedReality Mixed { get; private set; }
        private CprIntroPanel panel;
        private CprStationView view;
        private Camera cameraView;
        private Transform patient;
        private CprTrainingSettings settings;
        private Transform station;
        private System.Func<Pose> alignmentContact;
        public IEnumerator Run(Camera camera,Transform station,Transform model,CprStationView screens,CprTrackedHands input,CprTrainingSettings config,System.Func<Pose> contactPose)
        {
            alignmentContact=contactPose;
            cameraView=camera; patient=model; view=screens; settings=config; this.station=station;
            Mixed=gameObject.AddComponent<CprMixedReality>();
            HideScreens(true); CreatePanel();
            if(!CprFamiliarization.Completed) yield return CprFamiliarization.Run(panel,camera,input,config);
            panel.Present("Welcome","Entorno virtual","Realidad mixta");
            if(SceneLoader.ConsumeMixedRealityRequest())
                while(panel.Selection<0) { panel.Choose(1); if(panel.Selection<0) yield return null; }
            while(!panel.FinishedSpeaking || panel.Selection<0) yield return null;
            bool mixed=panel.Selection==1;
            if(mixed)
            {
                panel.Hint("Preparando la vista del entorno real…");
                yield return Mixed.Enable(camera,station);
                if(Mixed.Failed)
                {
                    panel.Present("Virtual","Continuar en entorno virtual");
                    panel.Hint("Realidad mixta no está disponible en esta ejecución. Puedes continuar en el entorno virtual.");
                    while(!panel.FinishedSpeaking || panel.Selection<0) yield return null;
                }
                else
                {
                    panel.Present("Mixed","Configurar apoyo","Continuar sin apoyo");
                    while(!panel.FinishedSpeaking || panel.Selection<0) yield return null;
                    if(panel.Selection==0) yield return Align();
                }
            }
            else yield return panel.Say("Virtual");
            ClosePanel(); HideScreens(false);
        }
        public IEnumerator Realign()
        {
            if(Mixed==null || !Mixed.Active) yield break;
            HideScreens(true); CreatePanel(); yield return Align(); ClosePanel(); HideScreens(false);
        }
        private IEnumerator Align()
        {
            Mixed.SetSupport(patient,settings,true);
            RefreshAlignmentGuides();
            // The participant moves the real object. No automatic object recognition or transform handles.
            panel.Present("Align","Confirmar alineación","Continuar sin apoyo");
            while(!panel.FinishedSpeaking || panel.Selection<0)
            {
                RefreshAlignmentGuides();
                yield return null;
            }
            bool confirmed=panel.Selection==0;
            Mixed.SetSupport(patient,settings,confirmed);
            if(confirmed) yield return panel.Say("Aligned");
        }
        private void RefreshAlignmentGuides()
        {
            // The patient can be offset from the station origin. Use the same shirt
            // contact surface as practice, also after height changes or recentering.
            view.AlignmentGuides(alignmentContact());
        }
        private void CreatePanel()
        {
            panel=new GameObject("Preparación de modalidad").AddComponent<CprIntroPanel>();
            panel.transform.SetParent(transform,false); panel.Build(cameraView); panel.AnchorToStation(station);
        }
        private void ClosePanel() { if(panel!=null) { Destroy(panel.gameObject); panel=null; } }
        private void HideScreens(bool hide) { foreach(var canvas in view.Panels) canvas.gameObject.SetActive(!hide); }
        private void OnDisable() { ClosePanel(); if(Mixed!=null) Mixed.Restore(); }
    }
}
