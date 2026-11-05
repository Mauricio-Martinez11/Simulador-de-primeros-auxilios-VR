using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace FirstAidVR.Rcp
{
    // Enabled only by the main-menu familiarization, never by the evaluated station.
    public sealed class CprIntroCompanion : MonoBehaviour
    {
        [Serializable] public class Word { public float time; public int start,length; }
        [Serializable] public class Timings { public Word[] words; }
        private CprIntroPanel panel;
        private CprNarrator narrator;
        private TextMeshProUGUI subtitle,clock,stepLabel;
        private Image progress;
        private Button previous,next;
        private Canvas canvas;
        private string key;
        private Timings timings;
        public void Build(Canvas host,CprIntroPanel owner,CprNarrator voice)
        {
            canvas=host; panel=owner; narrator=voice;
            ((RectTransform)host.transform).sizeDelta=new Vector2(2100,720);
            var clipper=host.GetComponentInChildren<Oculus.Interaction.Surfaces.BoundsClipper>();
            if(clipper!=null) clipper.Size=new Vector3(2100,720,.01f);
            var left=Card(host.transform,"Diálogo de la guía",-810);
            Text(left,"ESCUCHA Y SIGUE",new Vector2(0,292),new Vector2(400,44),24,true);
            subtitle=Text(left,"",new Vector2(0,48),new Vector2(400,380),30,false);
            subtitle.alignment=TextAlignmentOptions.TopLeft;
            var track=Box(left,"Progreso del audio",new Vector2(0,-224),new Vector2(390,10),new Color(.73f,.83f,.88f));
            progress=new GameObject("Audio reproducido",typeof(RectTransform),typeof(Image)).GetComponent<Image>();
            progress.transform.SetParent(track.transform,false); progress.rectTransform.anchorMin=new Vector2(0,0); progress.rectTransform.anchorMax=new Vector2(0,1);
            progress.rectTransform.pivot=new Vector2(0,.5f); progress.rectTransform.sizeDelta=new Vector2(0,0); progress.color=new Color(.03f,.48f,.65f); progress.raycastTarget=false;
            clock=Text(left,"",new Vector2(0,-273),new Vector2(400,45),23,false);
            var right=Card(host.transform,"Opciones de la guía",810);
            Text(right,"CONTROLES DE LA GUÍA",new Vector2(0,292),new Vector2(400,44),24,true);
            stepLabel=Text(right,"",new Vector2(0,235),new Vector2(400,45),23,false);
            ActionButton(right,"Repetir indicación",140,()=>panel.Replay());
            previous=ActionButton(right,"Paso anterior",35,()=>panel.Navigate(-1));
            next=ActionButton(right,"Siguiente paso",-70,()=>panel.Navigate(1));
            ActionButton(right,"Ya sé usar VR\nIr al menú",-220,()=>panel.Skip());
        }
        public void SetStep(int step) { previous.interactable=step>0; next.interactable=step>=0; stepLabel.text=step<0?"Antes de comenzar":$"Familiarización · {step+1} de 4"; }
        private void Update()
        {
            if(narrator==null) return;
            if(key!=narrator.Key)
            {
                key=narrator.Key; var asset=key==null?null:Resources.Load<TextAsset>("RCP/Narration/"+key+"Timing");
                timings=asset!=null?JsonUtility.FromJson<Timings>(asset.text):null;
            }
            if(key==null) return;
            string full=CprNarration.Get(key).text;
            subtitle.text=Caption(full,timings,narrator.PlaybackSeconds,narrator.MissingAudio);
            progress.rectTransform.sizeDelta=new Vector2(390*narrator.PlaybackProgress,0);
            clock.text=narrator.MissingAudio?"Lee la indicación":$"{narrator.PlaybackSeconds:0} s / {narrator.Duration:0} s";
#if UNITY_EDITOR
            if(panel.Focus && Input.GetMouseButtonDown(0) && canvas.worldCamera!=null)
            {
                var ray=canvas.worldCamera.ScreenPointToRay(Input.mousePosition);
                foreach(var button in GetComponentsInChildren<Button>())
                {
                    // Buttons are children of the canvas, not this component's transform hierarchy root.
                    var rect=(RectTransform)button.transform;
                    if(button.interactable && button.gameObject.activeInHierarchy && new Plane(rect.forward,rect.position).Raycast(ray,out float d)
                        && rect.rect.Contains(rect.InverseTransformPoint(ray.GetPoint(d)))) button.onClick.Invoke();
                }
            }
#endif
        }
        public static string Caption(string full,Timings timing,float seconds,bool fallback)
        {
            if(fallback || timing==null || timing.words==null || timing.words.Length==0) return full;
            var words=timing.words; int current=-1;
            for(int i=0;i<words.Length && words[i].time<=seconds;i++) current=i;
            if(current<0) return "Escucha la guía…";
            int first=current/12*12;
            int start=Mathf.Clamp(words[first].start,0,full.Length);
            int end=Mathf.Clamp(words[current].start+words[current].length,start,full.Length);
            return full.Substring(start,end-start);
        }
        private static Transform Card(Transform parent,string name,float x)
        { return Box(parent,name,new Vector2(x,0),new Vector2(480,720),new Color(.94f,.97f,.99f)).transform; }
        private static CprRoundGraphic Box(Transform parent,string name,Vector2 pos,Vector2 size,Color color)
        {
            var graphic=new GameObject(name,typeof(RectTransform),typeof(CprRoundGraphic)).GetComponent<CprRoundGraphic>();
            graphic.transform.SetParent(parent,false); graphic.rectTransform.anchoredPosition=pos; graphic.rectTransform.sizeDelta=size;
            graphic.color=color; graphic.radius=24; graphic.raycastTarget=false; return graphic;
        }
        private static TextMeshProUGUI Text(Transform parent,string value,Vector2 pos,Vector2 size,float font,bool bold)
        {
            var text=new GameObject("Texto",typeof(RectTransform),typeof(TextMeshProUGUI)).GetComponent<TextMeshProUGUI>();
            text.transform.SetParent(parent,false); text.rectTransform.anchoredPosition=pos; text.rectTransform.sizeDelta=size;
            text.text=value; text.fontSize=font; text.color=new Color(.055f,.16f,.23f); text.alignment=TextAlignmentOptions.Center; text.raycastTarget=false;
            if(bold) text.fontStyle=FontStyles.Bold; return text;
        }
        private static Button ActionButton(Transform parent,string caption,float y,UnityEngine.Events.UnityAction action)
        {
            var image=Box(parent,caption,new Vector2(0,y),new Vector2(410,88),new Color(.025f,.37f,.51f)); image.raycastTarget=true;
            var button=image.gameObject.AddComponent<Button>(); button.targetGraphic=image;
            var colors=button.colors; colors.highlightedColor=new Color(.6f,1,1); colors.pressedColor=CprStationView.Green; button.colors=colors;
            Text(image.transform,caption,Vector2.zero,new Vector2(380,80),26,false).color=Color.white;
            button.onClick.AddListener(action); return button;
        }
    }
}
