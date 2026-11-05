using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace FirstAidVR.Rcp
{
    public sealed class CprIntroPanel : MonoBehaviour
    {
        private Canvas canvas;
        private TextMeshProUGUI title, body, state, section;
        private Button[] choices;
        private CprNarrator narrator;
        private OVRCameraRig rig;
        private CprIntroCompanion companion;
        private bool stationAnchored;
        public void AnchorToStation(Transform station)
        {
            stationAnchored=true;
            transform.SetParent(station,false);
            canvas.transform.localPosition=new Vector3(0,.76f,.68f);
            canvas.transform.localRotation=Quaternion.Euler(10,0,0);
        }
        public int Navigation { get; private set; }
        public bool SkipRequested { get; private set; }
        public void EnableNavigation() { companion=gameObject.AddComponent<CprIntroCompanion>(); companion.Build(canvas,this,narrator); }
        public void Navigate(int direction) { if(Focus) Navigation=direction; }
        public void Skip() { if(Focus) { SkipRequested=true; narrator.Stop(); } }
        public void Replay() { if(Focus && narrator.Key!=null) narrator.Play(narrator.Key); }
        public void SetNavigationStep(int step) { if(companion!=null) companion.SetStep(step); }

        private bool fallbackShown;
        public int Selection { get; private set; } = -1;
        public bool FinishedSpeaking => narrator!=null && narrator.Complete;
        public bool Focus => Application.isBatchMode || (rig!=null ? OVRManager.hasVrFocus : Application.isFocused);
        public void Build(Camera camera)
        {
            CprAudioOutput.Bind(camera);
            narrator=gameObject.AddComponent<CprNarrator>();
            rig=FindObjectOfType<OVRCameraRig>();
            canvas=new GameObject("Bienvenida y preparación",typeof(RectTransform),typeof(Canvas)).GetComponent<Canvas>();
            canvas.transform.SetParent(transform,false); canvas.renderMode=RenderMode.WorldSpace; canvas.worldCamera=camera;
            Vector3 forward=Vector3.ProjectOnPlane(camera.transform.forward,Vector3.up).normalized;
            if(forward.sqrMagnitude<.1f) forward=Vector3.forward;
            canvas.transform.SetPositionAndRotation(camera.transform.position+forward*1.5f,Quaternion.LookRotation(forward));
            canvas.transform.localScale=Vector3.one*.001f;
            ((RectTransform)canvas.transform).sizeDelta=new Vector2(1080,720);
            var bg=Box(canvas.transform,"Fondo",Vector2.zero,new Vector2(1080,720),new Color(.94f,.97f,.99f));
            bg.raycastTarget=false;
            Box(canvas.transform,"Acento",new Vector2(0,281),new Vector2(980,6),new Color(.03f,.48f,.65f));
            section=Label(canvas.transform,new Vector2(0,315),new Vector2(980,42),22);
            section.color=new Color(.04f,.37f,.49f);
            title=Label(canvas.transform,new Vector2(0,206),new Vector2(970,110),42);
            title.fontStyle=FontStyles.Bold; title.enableAutoSizing=true; title.fontSizeMin=28; title.fontSizeMax=42;
            body=Label(canvas.transform,new Vector2(0,20),new Vector2(950,250),36);
            Box(canvas.transform,"Estado de la guía",new Vector2(0,-157),new Vector2(980,78),new Color(.83f,.92f,.96f));
            state=Label(canvas.transform,new Vector2(0,-157),new Vector2(930,68),23);
            state.enableAutoSizing=true; state.fontSizeMin=18; state.fontSizeMax=23;
            choices=new Button[2];
            for(int i=0;i<2;i++)
            {
                var image=Box(canvas.transform,"Opción "+i,new Vector2(i==0?-250:250,-266),new Vector2(470,100),new Color(.025f,.37f,.51f));
                image.raycastTarget=true; var button=image.gameObject.AddComponent<Button>(); button.targetGraphic=image;
                var colors=button.colors; colors.highlightedColor=new Color(.6f,1,1); colors.pressedColor=CprStationView.Green; button.colors=colors;
                var caption=Label(button.transform,Vector2.zero,new Vector2(430,82),28); caption.color=Color.white; caption.enableAutoSizing=true; caption.fontSizeMin=22; caption.fontSizeMax=28;
                int index=i; button.onClick.AddListener(()=>Choose(index)); choices[i]=button;
            }
            CprCanvasInteraction.Attach(canvas);
        }
        private void OnEnable() { CprWorldRecenter.Recentered+=Recenter; }
        private void OnDisable() { CprWorldRecenter.Recentered-=Recenter; }
        private void Recenter()
        {
            var camera=CprWorldRecenter.ViewCamera();
            if(canvas!=null && camera!=null)
            {
                canvas.worldCamera=camera;
                if(!stationAnchored) CprWorldRecenter.Align(canvas.transform,canvas.transform,camera.transform,1.5f,false);
            }
        }
        public void Choose(int index)
        {
            if(Focus && index>=0 && index<choices.Length && choices[index].gameObject.activeInHierarchy && choices[index].interactable)
            { Selection=index; state.text="Seleccionado: "+choices[index].GetComponentInChildren<TextMeshProUGUI>().text+" · escucha la explicación"; }
        }
        public void Present(string key,params string[] options)
        {
            Navigation=0; Selection=-1; var dialogue=CprNarration.Get(key); title.text=dialogue.title; body.text=dialogue.hint;
            section.text=key=="Look"?"PRIMEROS PASOS · 1 DE 3":key=="Hands"?"PRIMEROS PASOS · 2 DE 3":key.StartsWith("Select")?"PRIMEROS PASOS · 3 DE 3":"PRIMEROS AUXILIOS · RCP";
            fallbackShown=false; body.enableAutoSizing=false; body.fontSize=36;
            for(int i=0;i<choices.Length;i++)
            {
                choices[i].gameObject.SetActive(i<options.Length);
                ((RectTransform)choices[i].transform).anchoredPosition=new Vector2(options.Length==1?0:i==0?-250:250,-266);
                if(i<options.Length) choices[i].GetComponentInChildren<TextMeshProUGUI>().text=options[i];
            }
            narrator.Play(key); state.text="Escucha la explicación";
            ShowFallback();
        }
        private void ShowFallback()
        {
            if(!narrator.MissingAudio || fallbackShown) return;
            fallbackShown=true; body.text=CprNarration.Get(narrator.Key).text;
            body.enableAutoSizing=true; body.fontSizeMin=18;
            state.text="Audio no disponible · lee la explicación";
        }
        public void Hint(string text) { if(!fallbackShown) body.text=text; }
        public IEnumerator Say(string key)
        { Present(key); while(!FinishedSpeaking) yield return null; }
        private void Update()
        {
            if(narrator==null) return;
            narrator.Tick(Time.unscaledDeltaTime,Focus);
            ShowFallback();
            foreach(var button in choices) button.interactable=Focus;
            if(!Focus) state.text="En pausa · vuelve al visor para continuar";
            else if(FinishedSpeaking) state.text=Selection>=0?"Selección confirmada":"Realiza la acción indicada cuando estés listo";
#if UNITY_EDITOR
            if(Focus && Input.GetMouseButtonDown(0) && canvas.worldCamera!=null)
            {
                var ray=canvas.worldCamera.ScreenPointToRay(Input.mousePosition);
                for(int i=0;i<choices.Length;i++)
                {
                    var rect=(RectTransform)choices[i].transform;
                    if(choices[i].gameObject.activeInHierarchy && new Plane(rect.forward,rect.position).Raycast(ray,out float distance)
                        && rect.rect.Contains(rect.InverseTransformPoint(ray.GetPoint(distance)))) Choose(i);
                }
            }
#endif
        }
        private static CprRoundGraphic Box(Transform parent,string name,Vector2 p,Vector2 size,Color color)
        {
            var image=new GameObject(name,typeof(RectTransform),typeof(CprRoundGraphic)).GetComponent<CprRoundGraphic>();
            image.transform.SetParent(parent,false); image.rectTransform.anchoredPosition=p; image.rectTransform.sizeDelta=size; image.color=color; image.radius=24; image.raycastTarget=false; return image;
        }
        private static TextMeshProUGUI Label(Transform parent,Vector2 p,Vector2 size,float font)
        {
            var label=new GameObject("Texto",typeof(RectTransform),typeof(TextMeshProUGUI)).GetComponent<TextMeshProUGUI>();
            label.transform.SetParent(parent,false); label.rectTransform.anchoredPosition=p; label.rectTransform.sizeDelta=size;
            label.fontSize=font; label.color=new Color(.055f,.16f,.23f); label.alignment=TextAlignmentOptions.Center; label.raycastTarget=false; return label;
        }
    }
}
