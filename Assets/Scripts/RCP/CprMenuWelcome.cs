using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace FirstAidVR.Rcp
{
    public sealed class CprMenuWelcome : MonoBehaviour
    {
        private bool busy;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Install() { SceneManager.sceneLoaded-=Loaded; SceneManager.sceneLoaded+=Loaded; }
        private static void Loaded(Scene scene,LoadSceneMode mode)
        {
            if(scene.name=="1_MainMenu" && FindObjectOfType<CprMenuWelcome>()==null)
                new GameObject("Preparación antes del menú").AddComponent<CprMenuWelcome>();
        }
        private void Start()
        {
            foreach(var button in FindObjectsOfType<Button>(true))
            {
                if(button.name=="B_Otros_Escenarios")
                {
                    button.interactable=false;
                    var label=button.GetComponentInChildren<TMP_Text>();
                    if(label!=null) { label.text="Otros escenarios · Próximamente"; label.enableAutoSizing=true; label.fontSizeMin=18; label.fontSizeMax=30; }
                }
                else if(button.name=="B_Escenario_Real")
                {
                    var label=button.GetComponentInChildren<TMP_Text>();
                    if(label!=null) label.text="Realidad mixta";
                }
                else if(button.name=="B_Ajustes")
                {
                    var label=button.GetComponentInChildren<TMP_Text>(); if(label!=null) label.text="Practicar controles";
                    button.onClick.RemoveAllListeners(); button.onClick.AddListener(()=>{ if(!busy) StartCoroutine(Practice()); });
                }
            }
            if(!CprFamiliarization.Completed) StartCoroutine(Practice());
        }
        private IEnumerator Practice()
        {
            busy=true;
            var panels=FindObjectsOfType<Canvas>();
            foreach(var panel in panels) panel.gameObject.SetActive(false);
            Camera camera=null;
            while(camera==null)
            {
                var rig=FindObjectOfType<OVRCameraRig>();
                camera=rig!=null?rig.centerEyeAnchor.GetComponent<Camera>():Camera.main;
                yield return null;
            }
            var input=gameObject.AddComponent<CprTrackedHands>(); input.Discover();
            var intro=new GameObject("Ensayo de controles").AddComponent<CprIntroPanel>(); intro.Build(camera); intro.EnableNavigation();
            intro.transform.SetParent(transform,true);
            intro.Present("MenuWelcome","Escuchar instrucciones","Ir al menú");
            intro.SetNavigationStep(-1);
            while(!intro.SkipRequested && intro.Selection!=1 && (!intro.FinishedSpeaking || intro.Selection!=0)) yield return null;
            if(intro.Selection==1) intro.Skip();
            yield return CprFamiliarization.Run(intro,camera,input,Resources.Load<CprTrainingSettings>("RCP/TrainingSettings"));
            Destroy(intro.gameObject); Destroy(input);
            foreach(var panel in panels) if(panel!=null) panel.gameObject.SetActive(true);
            foreach(var menu in FindObjectsOfType<CanvasBillboard>()) menu.PlaceInFront();
            busy=false;
        }
    }
}
