using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using Oculus.Interaction;
using Oculus.Interaction.Surfaces;

namespace FirstAidVR.Rcp
{
    public sealed class CprTutorialView : MonoBehaviour
    {
        public static readonly Color Cyan = new Color(.16f, .84f, 1);
        public static readonly Color Green = new Color(.24f, 1, .68f);
        private TextMeshProUGUI count, title, instruction, feedback, footer, primaryText;
        private RectTransform primary, secondary, fill;
        private Image primaryImage, secondaryImage;
        private Button primaryButton, secondaryButton;
        private int clickedButton = -1;
        private bool buttonsEnabled;
        private Transform[] ghosts = new Transform[2];
        private MeshRenderer[] ghostRenderers = new MeshRenderer[2];
        private MaterialPropertyBlock properties;
        private LineRenderer ring;
        private Transform panel;
        private Camera viewCamera;
        private float dwell;
        private int hover = -1;
        private int pressedButton = -1;
        private CprStep shownStep = (CprStep)(-1);
        private CprTrainingSettings settings;
        private AudioSource feedbackAudio;
        private AudioClip touchSound, successSound;
        public Vector3 PanelPosition { get => panel.position; set => panel.position = value; }

        public void Build(CprTrainingSettings settings, Camera camera)
        {
            this.settings = settings;
            viewCamera = camera; properties = new MaterialPropertyBlock();
            feedbackAudio = gameObject.AddComponent<AudioSource>(); feedbackAudio.playOnAwake = false;
            feedbackAudio.spatialBlend = 0; feedbackAudio.volume = .18f;
            touchSound = Tone(620, .07f); successSound = Tone(880, .15f);
            var canvas = new GameObject("Guía RCP", typeof(RectTransform), typeof(Canvas)).GetComponent<Canvas>();
            canvas.transform.SetParent(transform, false); canvas.renderMode = RenderMode.WorldSpace;
            canvas.worldCamera = camera;
            panel = canvas.transform; panel.localScale = Vector3.one * .0011f;
            ((RectTransform)panel).sizeDelta = new Vector2(860, 620);
            Box("Panel", panel, Vector2.zero, new Vector2(860,620), new Color(.018f,.04f,.065f,.97f));
            Box("Acento", panel, new Vector2(-413,0), new Vector2(6,568), Cyan);
            count = Label("Sección", new Vector2(0,260), new Vector2(768,36), 23, Cyan);
            title = Label("Título", new Vector2(0,195), new Vector2(768,76), 42, Color.white);
            instruction = Label("Instrucción", new Vector2(0,67), new Vector2(768,160), 29, new Color(.85f,.92f,.98f));
            feedback = Label("Estado", new Vector2(0,-61), new Vector2(768,76), 25, Green);
            var track = Box("Progreso", panel, new Vector2(0,-119), new Vector2(768,5), new Color(.13f,.22f,.3f));
            fill = Box("Avance", track.transform, new Vector2(-384,0), new Vector2(0,5), Cyan).rectTransform;
            fill.pivot = new Vector2(0,.5f);
            primaryImage = Box("Continuar", panel, new Vector2(0,-182), new Vector2(768,68), new Color(.07f,.25f,.33f));
            primary = primaryImage.rectTransform;
            primaryText = ChildLabel(primary, "Continuar", 26);
            secondaryImage = Box("Menú", panel, new Vector2(222,-182), new Vector2(324,68), new Color(.08f,.18f,.24f));
            secondary = secondaryImage.rectTransform;
            ChildLabel(secondary, "Volver al menú", 24);
            primaryButton = MakeButton(primaryImage, 0);
            secondaryButton = MakeButton(secondaryImage, 1);
            BuildRayInteraction(canvas);
            footer = Label("Nota", new Vector2(0,-265), new Vector2(768,48), 19, new Color(.51f,.65f,.76f));
            footer.text = "RCP EN ADULTOS · SOLO MANOS\nGuía de secuencia y colocación · AHA 2025 / Cruz Roja";

            for (int i = 0; i < 2; i++)
            {
                var ghost = new GameObject(i == 0 ? "Holograma mano izquierda" : "Holograma mano derecha", typeof(MeshFilter), typeof(MeshRenderer));
                ghost.transform.SetParent(transform, false);
                ghost.GetComponent<MeshFilter>().sharedMesh = i == 0 ? settings.leftHand : settings.rightHand;
                var renderer = ghost.GetComponent<MeshRenderer>(); renderer.sharedMaterial = settings.hologramMaterial;
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; renderer.receiveShadows = false;
                ghosts[i] = ghost.transform; ghostRenderers[i] = renderer;
            }
            ring = new GameObject("Zona de contacto", typeof(LineRenderer)).GetComponent<LineRenderer>();
            ring.transform.SetParent(transform, false); ring.sharedMaterial = settings.hologramMaterial;
            ring.useWorldSpace = false; ring.loop = true; ring.positionCount = 64; ring.widthMultiplier = .0035f;
            ring.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            for (int i = 0; i < 64; i++)
            { float a = i * Mathf.PI * 2 / 64; ring.SetPosition(i, new Vector3(Mathf.Cos(a)*.075f,.002f,Mathf.Sin(a)*.075f)); }
        }

        public void Show(CprFlow flow, string status)
        {
            if (shownStep != flow.Step)
            {
                shownStep = flow.Step; ResetButtons();
                count.text = flow.Step == CprStep.Ready ? "POSTURA ENGANCHADA · SEPARA PARA SOLTAR" : $"PASO {(int)flow.Step+1:00} / 07     ·     PRÁCTICA GUIADA";
                string[] titles = { "Comprueba el entorno", "Traslada al holograma", "Comprueba si responde", "Observa la respiración", "Pide ayuda", "Primero, mano derecha", "Izquierda sobre derecha", "Manos preparadas para RCP" };
                string[] texts = {
                    "Comprueba que el lugar sea seguro para ambos.\n\nApunta al botón y pulsa el gatillo del mando o haz pinza con la mano.",
                    "Escenario: traslado por peligro inmediato. Las manos muestran cada agarre por turnos. Acércate a una opción y cierra los dedos o haz pinza.\nArrastra el cuerpo; abre las manos en el destino verde.",
                    "Pregunta en voz alta: «¿Se encuentra bien?». Toca suavemente el hombro marcado con una mano.\n\nAcércala al holograma y mantenla un instante.",
                    "La persona no responde. Mira el pecho para comprobar si respira normalmente, durante un máximo de 10 segundos.\n\nLos jadeos aislados NO son respiración normal.",
                    "No responde y no respira normalmente. Pide a alguien que llame al servicio local de emergencias.\nSi estás solo, llama con el altavoz activado.",
                    "Arrodíllate al costado. Acerca el TALÓN de la DERECHA al centro del pecho, sobre la mitad inferior del esternón.\nPalma abajo y dedos alineados; la guía cambia al reconocer la postura.",
                    "La derecha queda en la guía. Acerca el talón de la IZQUIERDA encima y sigue la postura indicada.\nMantén las manos juntas durante la práctica.",
                    "La postura se mantiene aunque una mano oculte a la otra. Al separarlas, se libera el enganche y puedes volver a colocarlas.\nEsta práctica no mide compresiones, fuerza ni profundidad."
                };
                title.text = titles[(int)flow.Step]; instruction.text = texts[(int)flow.Step];
                bool button = flow.Step == CprStep.Safety || flow.Step == CprStep.MovePatient || flow.Step == CprStep.CallForHelp || flow.Step == CprStep.Ready;
                primary.gameObject.SetActive(button); secondary.gameObject.SetActive(true);
                primary.sizeDelta = new Vector2(420, 68);
                primary.anchoredPosition = new Vector2(-174, -182);
                secondary.anchoredPosition = new Vector2(button ? 222 : 0, -182);
                primaryText.text = flow.Step == CprStep.Ready ? "Repetir práctica" : flow.Step == CprStep.MovePatient ? "Ver desplome" : flow.Step == CprStep.Safety ? "El entorno es seguro" : "Ayuda solicitada · simulación";
            }
            feedback.text = status;
            fill.sizeDelta = new Vector2(768 * flow.Progress, 5);
            if (panel != null && viewCamera != null)
                panel.rotation = Quaternion.LookRotation(panel.position-viewCamera.transform.position);
        }

        private Button MakeButton(Image graphic, int index)
        {
            graphic.raycastTarget = true;
            var button = graphic.gameObject.AddComponent<Button>();
            button.targetGraphic = graphic;
            button.navigation = new Navigation { mode = Navigation.Mode.None };
            var colors = button.colors;
            colors.highlightedColor = new Color(.6f, 1, 1);
            colors.pressedColor = Green;
            colors.selectedColor = Color.white;
            colors.disabledColor = new Color(.5f, .5f, .5f, .7f);
            button.colors = colors;
            button.interactable = false;
            button.onClick.AddListener(() =>
            {
                if (buttonsEnabled && clickedButton < 0 && button.IsActive() && button.IsInteractable())
                    clickedButton = index;
            });
            return button;
        }

        private static void BuildRayInteraction(Canvas canvas)
        {
            // Same ISDK pipeline as 1_MainMenu: existing hand/controller rays target this
            // bounded surface, then PointableCanvas forwards their selection to Unity Buttons.
            // This surface is invisible and remains attached when the panel moves or turns.
            if (FindObjectOfType<PointableCanvasModule>() == null)
            {
                var events = EventSystem.current;
                if (events == null) events = new GameObject("EventSystem RCP", typeof(EventSystem)).GetComponent<EventSystem>();
                events.gameObject.AddComponent<PointableCanvasModule>().ExclusiveMode = true;
            }
            canvas.gameObject.AddComponent<GraphicRaycaster>();
            var surfaceObject = new GameObject("ISDK_RayCanvasInteraction", typeof(RectTransform));
            surfaceObject.SetActive(false);
            var rect = (RectTransform)surfaceObject.transform;
            rect.SetParent(canvas.transform, false);
            rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            var pointable = surfaceObject.AddComponent<PointableCanvas>();
            pointable.InjectAllPointableCanvas(canvas);
            var plane = surfaceObject.AddComponent<PlaneSurface>();
            var clipper = surfaceObject.AddComponent<BoundsClipper>();
            var canvasRect = ((RectTransform)canvas.transform).rect;
            clipper.Position = canvasRect.center;
            clipper.Size = new Vector3(canvasRect.width, canvasRect.height, .01f);
            var surface = surfaceObject.AddComponent<ClippedPlaneSurface>();
            surface.InjectAllClippedPlaneSurface(plane, new IBoundsClipper[] { clipper });
            var interactable = surfaceObject.AddComponent<RayInteractable>();
            interactable.InjectAllRayInteractable(surface);
            interactable.InjectOptionalSelectSurface(plane);
            interactable.InjectOptionalPointableElement(pointable);
            surfaceObject.SetActive(true);
        }

        public void SetButtonsEnabled(bool value)
        {
            buttonsEnabled = value;
            primaryButton.interactable = secondaryButton.interactable = value;
            if (!value) ResetButtons();
        }

        public void ResetButtons() { dwell=0; hover=-1; pressedButton=-1; clickedButton=-1; }
        public int UpdateButtons(Ray gaze, float dt, float duration, CprHandSample left = default, CprHandSample right = default, bool desktopMouse = false)
        {
            if (!buttonsEnabled) return -1;
            if (clickedButton >= 0)
            {
                int click = clickedButton; clickedButton = -1;
                PlayFeedback(false);
                return click;
            }
#if UNITY_EDITOR
            if (desktopMouse && Input.GetMouseButtonDown(0))
                return Inside(primary, gaze) ? 0 : Inside(secondary, gaze) ? 1 : -1;
#endif
            // Keep near-touch as an alternative. Looking at a button never activates it.
            int touch = Touches(primary,left,right) ? 0 : Touches(secondary,left,right) ? 1 : -1;
            int current = touch;
            if(current!=pressedButton) pressedButton=-1;
            else if(current>=0) return -1;
            if (current != hover) { dwell = 0; hover = current; }
            if (current < 0) { dwell = 0; return -1; }
            if(touch >= 0) duration = .55f;
            dwell += dt;
            fill.sizeDelta = new Vector2(768 * Mathf.Clamp01(dwell/duration), 5);
            if (dwell < duration) return -1;
            dwell = 0; pressedButton=current; return current;
        }

        private static bool Touches(RectTransform rect, CprHandSample left, CprHandSample right)
        {
            return rect.gameObject.activeInHierarchy && (Touches(rect,left) || Touches(rect,right));
        }
        private static bool Touches(RectTransform rect, CprHandSample hand)
        {
            if(!hand.valid) return false;
            var point = hand.pose.position + hand.pose.rotation * Vector3.forward * .08f;
            return Mathf.Abs(Vector3.Dot(point-rect.position,rect.forward)) < .035f
                && rect.rect.Contains(rect.InverseTransformPoint(point));
        }

        private static bool Inside(RectTransform rect, Ray ray)
        {
            if (!rect.gameObject.activeInHierarchy) return false;
            var plane = new Plane(rect.forward, rect.position);
            return plane.Raycast(ray, out float distance) && distance < 4 && rect.rect.Contains(rect.InverseTransformPoint(ray.GetPoint(distance)));
        }

        public void Hand(int index, Pose target, CprHandSample raw, bool show, bool aligned, float radius, bool stacked = false)
        {
            ghosts[index].gameObject.SetActive(show);
            if (!show) return;
            var stackMesh = index == 0 ? settings.stackedLeftHand : settings.stackedRightHand;
            ghosts[index].GetComponent<MeshFilter>().sharedMesh = stacked && stackMesh != null ? stackMesh
                : index == 0 ? settings.leftHand : settings.rightHand;
            // Only a visual attraction, maximum displacement bounded by the capture radius.
            float proximity = raw.valid ? 1-Mathf.Clamp01(Vector3.Distance(raw.pose.position,target.position)/radius) : 0;
            bool orientated = raw.valid && Vector3.Angle(raw.pose.rotation*Vector3.up,target.rotation*Vector3.up) < 65;
            float blend = aligned ? 1 : Mathf.SmoothStep(0,1,proximity);
            if (aligned) ghosts[index].SetPositionAndRotation(target.position,target.rotation);
            else if (proximity > 0 && orientated)
                ghosts[index].SetPositionAndRotation(Vector3.Lerp(raw.pose.position,target.position,blend), Quaternion.Slerp(raw.pose.rotation,target.rotation,blend));
            else ghosts[index].SetPositionAndRotation(target.position,target.rotation);
            var color = aligned ? (index == 0 && stacked ? new Color(.16f,.72f,.5f) : Green) : Cyan;
            color.a = aligned ? .94f : .36f;
            properties.SetColor("_Color",color); ghostRenderers[index].SetPropertyBlock(properties);
        }

        public void Marker(Pose pose, bool visible, bool correct)
        {
            ring.gameObject.SetActive(visible); ring.transform.SetPositionAndRotation(pose.position,pose.rotation);
            var color = correct ? Green : Cyan; color.a = .8f;
            properties.SetColor("_Color",color); ring.SetPropertyBlock(properties);
        }
        public void PlayFeedback(bool success) { if(feedbackAudio!=null) feedbackAudio.PlayOneShot(success?successSound:touchSound); }
        private static AudioClip Tone(float frequency,float duration)
        {
            const int rate=22050;
            var data=new float[Mathf.CeilToInt(rate*duration)];
            for(int i=0;i<data.Length;i++) data[i]=Mathf.Sin(2*Mathf.PI*frequency*i/rate)*Mathf.Sin(Mathf.PI*i/data.Length)*.3f;
            var clip=AudioClip.Create("Señal de interacción",data.Length,1,rate,false); clip.SetData(data,0); return clip;
        }
        private void OnDisable()
        {
            ResetButtons();
            if(panel!=null) panel.gameObject.SetActive(false);
            foreach(var ghost in ghosts) if(ghost!=null) ghost.gameObject.SetActive(false);
            if(ring!=null) ring.gameObject.SetActive(false);
        }
        private void OnDestroy()
        {
            if(touchSound!=null) { if(Application.isPlaying) Destroy(touchSound); else DestroyImmediate(touchSound); }
            if(successSound!=null) { if(Application.isPlaying) Destroy(successSound); else DestroyImmediate(successSound); }
        }
        private TextMeshProUGUI Label(string name, Vector2 position, Vector2 size, float fontSize, Color color)
        {
            var text = new GameObject(name,typeof(RectTransform),typeof(TextMeshProUGUI)).GetComponent<TextMeshProUGUI>();
            text.transform.SetParent(panel,false); text.rectTransform.anchoredPosition=position; text.rectTransform.sizeDelta=size;
            text.fontSize=fontSize; text.color=color; text.raycastTarget=false; text.enableWordWrapping=true;
            text.alignment=TextAlignmentOptions.MidlineLeft; text.margin=Vector4.zero;
            return text;
        }
        private static TextMeshProUGUI ChildLabel(Transform parent, string value, float size)
        {
            var text = new GameObject("Texto",typeof(RectTransform),typeof(TextMeshProUGUI)).GetComponent<TextMeshProUGUI>();
            text.transform.SetParent(parent,false); text.rectTransform.anchorMin=Vector2.zero; text.rectTransform.anchorMax=Vector2.one;
            text.rectTransform.offsetMin=new Vector2(10,0); text.rectTransform.offsetMax=new Vector2(-10,0);
            text.fontSize=size; text.text=value; text.alignment=TextAlignmentOptions.Center; text.raycastTarget=false; return text;
        }
        private static Image Box(string name, Transform parent, Vector2 position, Vector2 size, Color color)
        {
            var result = new GameObject(name,typeof(RectTransform),typeof(Image)).GetComponent<Image>();
            result.transform.SetParent(parent,false); result.rectTransform.anchoredPosition=position; result.rectTransform.sizeDelta=size;
            result.color=color; result.raycastTarget=false; return result;
        }
    }
}
