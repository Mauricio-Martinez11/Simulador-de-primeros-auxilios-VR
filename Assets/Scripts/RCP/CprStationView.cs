using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace FirstAidVR.Rcp
{
    public enum CprStationAction { Primary, Alternative, Pause, Finish, Menu, Audio, Mute, HeightUp, HeightDown, AlignHeight, ResetHeight, Options, Realign }

    public sealed partial class CprStationView : MonoBehaviour
    {
        public static readonly Color Cyan = new Color(.22f,.82f,.98f), Green = new Color(.24f,.89f,.60f), Amber = new Color(1,.72f,.25f), Red = new Color(1,.39f,.35f);
        private static readonly Color White = new Color(.92f,.96f,1), Muted = new Color(.60f,.72f,.81f), Dark = new Color(.025f,.055f,.09f,.98f);
        [SerializeField] private Canvas[] panels;
        [SerializeField] private TextMeshProUGUI stepTitle, explanation, feedback, phase, timeText, score, scoreCaption, checklist, countText, rateText, depthText, metricsHint, resultsDetail, heightText;
        [SerializeField] private TextMeshProUGUI[] buttonLabels;
        [SerializeField] private Button[] buttons;
        [SerializeField] private Slider heightSlider;
        [SerializeField] private RectTransform progressFill, depthNeedle, beatNeedle;
        [SerializeField] private UnityEngine.UI.Graphic feedbackAccent, beatFlash;
        [SerializeField] private GameObject liveMeters;
        [SerializeField] private Transform[] hands;
        [SerializeField] private LineRenderer marker;
        private CprTrainingSettings settings;
        private MaterialPropertyBlock properties;
        private int pending = -1, lastTouch = -1, firedTouch = -1;
        private float touchTime;
        private bool bound;
        [SerializeField] private int layoutVersion;
        private const int CurrentLayoutVersion=8;
        public string NarrationFallbackText { get; set; }
        private CprHeightControl heightDrag;
        public event Action<float> HeightRequested;
        public Canvas[] Panels => panels;
        public Slider HeightSlider => heightSlider;
        public Button Button(CprStationAction action) => buttons[(int)action];
#if UNITY_EDITOR
        public void Rebuild(CprTrainingSettings config)
        {
            if(panels!=null) foreach(var canvas in panels) if(canvas!=null) DestroyImmediate(canvas.gameObject);
            if(hands!=null) foreach(var hand in hands) if(hand!=null) DestroyImmediate(hand.gameObject);
            if(marker!=null) DestroyImmediate(marker.gameObject);
            panels=null; Build(config);
        }
#endif

        public void Build(CprTrainingSettings config)
        {
            settings=config;
            if(layoutVersion==CurrentLayoutVersion && panels!=null && panels.Length==5 && panels[0]!=null) return;
            if(panels!=null) foreach(var panel in panels) if(panel!=null) RemoveObject(panel.gameObject);
            if(hands!=null) foreach(var hand in hands) if(hand!=null) RemoveObject(hand.gameObject);
            if(marker!=null) RemoveObject(marker.gameObject);
            bound=false;
            buttons=new Button[13]; buttonLabels=new TextMeshProUGUI[13];
            BuildScreens(config); BuildGuides(config); layoutVersion=CurrentLayoutVersion; Show(new CprPracticeSession(config),0,0,false,true);
        }
        private static void RemoveObject(GameObject obj) { if(Application.isPlaying) Destroy(obj); else DestroyImmediate(obj); }

        public void Bind(CprTrainingSettings config, Camera camera)
        {
            settings = config; properties = new MaterialPropertyBlock();
            foreach (var panel in panels) { panel.worldCamera = camera; CprCanvasInteraction.Attach(panel); }
            if(lessonVideo!=null) lessonVideo.Bind(config);
            if (bound) return;
            for (int i = 0; i < buttons.Length; i++)
            {
                int action = i; var button = buttons[i]; if(button==null) continue;
                button.onClick.AddListener(() => { if (button.interactable && pending < 0) pending = action; });
            }
            heightDrag=heightSlider.GetComponent<CprHeightControl>();
            if(heightDrag==null) heightDrag=heightSlider.gameObject.AddComponent<CprHeightControl>();
            heightSlider.onValueChanged.AddListener(value => { if(!heightDrag.Dragging) HeightRequested?.Invoke(value); });
            heightDrag.Released+=()=>HeightRequested?.Invoke(heightSlider.value);
            bound = true;
        }

        public void Show(CprPracticeSession s,float height,float beatPhase,bool muted,bool focus)
        { ShowScreens(s,height,beatPhase,muted,focus); }

        public int ReadAction(CprHandSample left, CprHandSample right, float dt, Ray desktopRay, bool desktopPreview)
        {
            if (pending >= 0) { int action = pending; pending = -1; return action; }
            int touched = -1;
            for (int i = 0; i < buttons.Length; i++)
            {
                if (buttons[i]==null || !buttons[i].IsActive() || !buttons[i].interactable) continue;
                var rect = (RectTransform)buttons[i].transform;
#if UNITY_EDITOR
                if (desktopPreview && Input.GetMouseButtonDown(0) && Hit(rect,desktopRay)) return i;
#endif
                if (Touch(rect,left) || Touch(rect,right)) { touched = i; break; }
            }
            if (touched != lastTouch) { touchTime = 0; lastTouch = touched; firedTouch = -1; }
            if (touched < 0 || touched == firedTouch) return -1;
            touchTime += dt;
            if (touchTime < .55f) return -1;
            firedTouch = touched; return touched;
        }
        public void ClearInput() { pending = lastTouch = firedTouch = -1; touchTime = 0; if(heightDrag!=null) heightDrag.Cancel(); }
        public void HideStation()
        {
            foreach(var canvas in panels) if(canvas!=null) canvas.gameObject.SetActive(false);
            foreach(var hand in hands) if(hand!=null) hand.gameObject.SetActive(false);
            if(marker!=null) marker.gameObject.SetActive(false);
            ClearInput();
        }
        public void BlockInputs()
        {
            foreach (var button in buttons) if(button!=null) button.interactable = false;
            heightSlider.interactable = false;
            ClearInput();
        }

        public Mesh ResponseHand { get; set; }
        public Vector3 ResponseHandScale { get; set; } = Vector3.one;
        public void Guides(CprPracticeSession s, Pose chest, Pose shoulder, Pose upper, bool leftController = false, Pose? contact = null)
        {
            if (properties == null) properties = new MaterialPropertyBlock();
            bool response = s.Step == CprPracticeStep.Response;
            bool placement = s.Step == CprPracticeStep.Placement;
            bool compress = s.Step == CprPracticeStep.Compressions && !s.Paused;
            Pose surface=contact ?? chest;
            Pose lowerPose = response ? shoulder : surface;
            upper.position += surface.position-chest.position;
            if (leftController) upper.position += chest.rotation * Vector3.up * settings.controllerStackExtra;
            for (int i = 0; i < 2; i++)
            {
                bool show = !s.Closed && (i==0 ? response || (compress || placement) && s.Placement.LowerLatched : placement || compress);
                hands[i].gameObject.SetActive(show); if (!show) continue;
                bool green = response && s.ResponseTouching || (compress || placement) && (i==0 ? s.Placement.StackLatched : s.Placement.LowerLatched);
                var pose = i==0 ? response ? lowerPose : upper : lowerPose; hands[i].SetPositionAndRotation(pose.position,pose.rotation);
                hands[i].localScale=response?ResponseHandScale:Vector3.one;
                var mesh = i==0 ? settings.stackedLeftHand : settings.stackedRightHand;
                hands[i].GetComponent<MeshFilter>().sharedMesh = response ? (ResponseHand!=null?ResponseHand:settings.leftHand) : mesh;
                var color = green ? Green : Cyan; color.a = green ? .70f : .38f;
                properties.SetColor("_Color",color); hands[i].GetComponent<MeshRenderer>().SetPropertyBlock(properties);
            }
            marker.gameObject.SetActive(!s.Closed && (placement || compress || s.Step==CprPracticeStep.Breathing));
            var markerPose = response ? shoulder : chest;
            marker.transform.SetPositionAndRotation(markerPose.position,markerPose.rotation);
            properties.SetColor("_Color",Cyan); marker.SetPropertyBlock(properties);
        }

        public void AlignmentGuides(Pose chest)
        {
            if(properties==null) properties=new MaterialPropertyBlock();
            for(int i=0;i<hands.Length;i++)
            {
                hands[i].gameObject.SetActive(true);
                hands[i].SetPositionAndRotation(chest.position+chest.rotation*Vector3.up*(i==0?settings.stackedHandHeight:0),chest.rotation);
                hands[i].GetComponent<MeshFilter>().sharedMesh=i==0?settings.stackedLeftHand:settings.stackedRightHand;
                properties.SetColor("_Color",new Color(.2f,.82f,.98f,.38f)); hands[i].GetComponent<Renderer>().SetPropertyBlock(properties);
            }
            marker.gameObject.SetActive(true); marker.transform.SetPositionAndRotation(chest.position,chest.rotation);
        }

        private void BuildGuides(CprTrainingSettings config)
        {
            hands = new Transform[2];
            for (int i=0;i<2;i++)
            {
                var hand=new GameObject(i==0?"Guía izquierda RCP":"Guía derecha RCP",typeof(MeshFilter),typeof(MeshRenderer)); hand.transform.SetParent(transform,false);
                hand.GetComponent<MeshFilter>().sharedMesh=i==0?config.stackedLeftHand:config.stackedRightHand;
                hand.GetComponent<MeshRenderer>().sharedMaterial=config.hologramMaterial;
                hand.GetComponent<MeshRenderer>().shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;
                hands[i]=hand.transform; hand.SetActive(false);
            }
            marker=new GameObject("Zona de reposo",typeof(LineRenderer)).GetComponent<LineRenderer>(); marker.transform.SetParent(transform,false);
            marker.sharedMaterial=config.hologramMaterial; marker.useWorldSpace=false; marker.loop=true; marker.positionCount=64; marker.widthMultiplier=.003f;
            for(int i=0;i<64;i++) { float a=i*Mathf.PI*2/64; marker.SetPosition(i,new Vector3(Mathf.Cos(a)*.075f,.002f,Mathf.Sin(a)*.075f)); }
            marker.gameObject.SetActive(false);
        }
        private Canvas Panel(string name, Vector3 position, Vector2 size, float yaw)
        {
            var canvas=new GameObject(name,typeof(RectTransform),typeof(Canvas)).GetComponent<Canvas>(); canvas.renderMode=RenderMode.WorldSpace;
            canvas.transform.SetParent(transform,false); canvas.transform.localPosition=position; canvas.transform.localRotation=Quaternion.Euler(10,yaw,0); canvas.transform.localScale=Vector3.one*.001f;
            ((RectTransform)canvas.transform).sizeDelta=size;
            Box(canvas.transform,"Borde",Vector2.zero,size+Vector2.one*4,new Color(.18f,.52f,.68f));
            Box(canvas.transform,"Fondo",Vector2.zero,size,Dark);
            Box(canvas.transform,"Acento",new Vector2(0,size.y/2-5),new Vector2(size.x-48,3),Cyan);
            return canvas;
        }
        private void AddButton(Transform parent,CprStationAction action,string label,Vector2 pos,Vector2 size,bool main=false)
        {
            var image=Box(parent,action.ToString(),pos,size,main?new Color(.06f,.36f,.30f):new Color(.075f,.18f,.25f)); image.raycastTarget=true;
            var button=image.gameObject.AddComponent<Button>(); button.targetGraphic=image; button.navigation=new Navigation { mode=Navigation.Mode.None };
            var colors=button.colors; colors.highlightedColor=new Color(.65f,1,1); colors.pressedColor=Green; colors.disabledColor=new Color(.35f,.45f,.50f,.6f); button.colors=colors;
            buttons[(int)action]=button;
            buttonLabels[(int)action]=Label(image.transform,"Texto",Vector2.zero,size-new Vector2(16,4),size.x<240?20:24,White,label,TextAlignmentOptions.Center);
        }
        private static TextMeshProUGUI Label(Transform parent,string name,Vector2 pos,Vector2 size,float fontSize,Color color,string text,TextAlignmentOptions align=TextAlignmentOptions.MidlineLeft)
        {
            var label=new GameObject(name,typeof(RectTransform),typeof(TextMeshProUGUI)).GetComponent<TextMeshProUGUI>(); label.transform.SetParent(parent,false);
            label.rectTransform.anchoredPosition=pos; label.rectTransform.sizeDelta=size; label.fontSize=fontSize; label.color=color; label.text=text;
            label.alignment=align; label.enableWordWrapping=true; label.raycastTarget=false; label.margin=Vector4.zero; return label;
        }
        private static CprRoundGraphic Box(Transform parent,string name,Vector2 pos,Vector2 size,Color color)
        {
            var image=new GameObject(name,typeof(RectTransform),typeof(CprRoundGraphic)).GetComponent<CprRoundGraphic>(); image.transform.SetParent(parent,false);
            image.rectTransform.anchoredPosition=pos; image.rectTransform.sizeDelta=size; image.color=color; image.raycastTarget=false; return image;
        }
        private static bool Touch(RectTransform rect,CprHandSample hand)
        {
            if(!hand.valid) return false;
            var point=hand.pose.position+hand.pose.rotation*Vector3.forward*.08f;
            return Mathf.Abs(Vector3.Dot(point-rect.position,rect.forward))<.035f && rect.rect.Contains(rect.InverseTransformPoint(point));
        }
        private static bool Hit(RectTransform rect,Ray ray)
        {
            return new Plane(rect.forward,rect.position).Raycast(ray,out float distance) && distance<5 && rect.rect.Contains(rect.InverseTransformPoint(ray.GetPoint(distance)));
        }
    }
}
