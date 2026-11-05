using TMPro;
using UnityEngine;
namespace FirstAidVR.Rcp
{
    public sealed class CprBreathingPanel : MonoBehaviour
    {
        private Canvas panel;
        private CprRingGraphic fill;
        private TextMeshProUGUI label;
        private Camera view;
        public bool Visible => panel!=null && panel.gameObject.activeSelf;
        public string BreathingText => label!=null?label.text:"";
        public Transform PanelTransform => panel.transform;
        public void Bind(Camera camera)
        {
            view=camera;
            panel=new GameObject("Progreso de observación",typeof(RectTransform),typeof(Canvas)).GetComponent<Canvas>();
            panel.transform.SetParent(transform,false); panel.renderMode=RenderMode.WorldSpace; panel.worldCamera=camera;
            panel.transform.localScale=Vector3.one*.001f;
            ((RectTransform)panel.transform).sizeDelta=new Vector2(240,240);
            var track=Ring("Pista",new Color(.08f,.21f,.29f,.85f)); track.SetAmount(1);
            fill=Ring("Avance circular",CprStationView.Cyan); fill.SetAmount(0);
            label=new GameObject("Indicación",typeof(RectTransform),typeof(TextMeshProUGUI)).GetComponent<TextMeshProUGUI>();
            label.transform.SetParent(panel.transform,false); label.rectTransform.sizeDelta=new Vector2(170,130);
            label.fontSize=21; label.enableWordWrapping=false; label.alignment=TextAlignmentOptions.Center; label.color=Color.white; label.raycastTarget=false;
            Hide();
        }
        private CprRingGraphic Ring(string name,Color color)
        {
            var ring=new GameObject(name,typeof(RectTransform),typeof(CprRingGraphic)).GetComponent<CprRingGraphic>();
            ring.transform.SetParent(panel.transform,false); ring.rectTransform.sizeDelta=new Vector2(230,230);
            ring.color=color; ring.thickness=15; ring.raycastTarget=false; return ring;
        }
        public void SetChest(Pose chest)
        {
            panel.transform.position=chest.position+chest.rotation*Vector3.up*.16f;
            if(view!=null) panel.transform.rotation=Quaternion.LookRotation(panel.transform.position-view.transform.position,Vector3.up);
        }
        public void Show(CprPracticeSession session,bool focus,bool looking=false)
        {
            bool visible=focus && session!=null && !session.Paused && session.Step==CprPracticeStep.Breathing;
            panel.gameObject.SetActive(visible); if(!visible) return;
            fill.SetAmount(session.StepPassed?1:session.Progress);
            fill.color=session.StepPassed?CprStationView.Green:looking && !session.Explaining?CprStationView.Cyan:CprStationView.Amber;
            label.text=session.StepPassed?"REVISADO":session.Explaining?"ESCUCHA\nla guía":looking?$"OBSERVANDO\n{session.BreathingObservedSeconds:0.0} / {session.BreathingDuration:0} s":"Mira el\npecho";
        }
        public void Hide() { if(panel!=null) panel.gameObject.SetActive(false); }
        private void OnDisable() { Hide(); }
    }
}
