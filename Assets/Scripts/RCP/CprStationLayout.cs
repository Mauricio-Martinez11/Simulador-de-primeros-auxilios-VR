using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace FirstAidVR.Rcp
{
    public sealed partial class CprStationView
    {
        [SerializeField] private TextMeshProUGUI[] stepNames,stepNumbers;
        [SerializeField] private Graphic[] stepCards;
        [SerializeField] private TextMeshProUGUI rateStatus,depthStatus,returnStatus,videoTag;
        [SerializeField] private CprRingGraphic scoreRing;
        [SerializeField] private CprLessonVideo lessonVideo;
        [SerializeField] private GameObject optionsGroup,videoFrame;
        [SerializeField] private RectTransform beatTrack;
        [SerializeField] private RectTransform depthFill, depthLastPeak;
        [SerializeField] private Graphic pressFeedback;
        private bool optionsOpen, showIncidents;
        public CprLessonVideo LessonVideo => lessonVideo;
        public bool OptionsOpen => optionsOpen;
        public void ToggleResults() { showIncidents=!showIncidents; ClearInput(); }

        public void ToggleOptions() { optionsOpen=!optionsOpen; optionsGroup.SetActive(optionsOpen); ClearInput(); }
        private void BuildScreens(CprTrainingSettings config)
        {
            panels=new[]{
                Panel("01_Progreso",new Vector3(-.94f,.72f,.53f),new Vector2(540,800),-22),
                Panel("02_Demostracion",new Vector3(-.20f,.76f,.68f),new Vector2(860,800),-3),
                Panel("03_Ritmo",new Vector3(.46f,.72f,.62f),new Vector2(350,800),17),
                Panel("04_Recorrido",new Vector3(.84f,.69f,.45f),new Vector2(350,800),26),
                Panel("05_Altura",new Vector3(1.15f,.60f,.26f),new Vector2(190,720),32)
            };
            Transform left=panels[0].transform, center=panels[1].transform, rhythm=panels[2].transform, depth=panels[3].transform, lift=panels[4].transform;
            Label(left,"Marca",new Vector2(0,345),new Vector2(462,46),29,White,"PRÁCTICA RCP · PASOS");
            timeText=Label(left,"Tiempo",new Vector2(0,294),new Vector2(462,38),22,Muted,"TIEMPO  00:00");
            stepNames=new TextMeshProUGUI[6]; stepNumbers=new TextMeshProUGUI[6]; stepCards=new Graphic[6];
            string[] names={"Revisar el entorno","Comprobar respuesta","Observar respiración","Solicitar ayuda","Colocar las manos","Realizar compresiones"};
            for(int i=0;i<6;i++)
            {
                var row=Box(left,"Paso "+(i+1),new Vector2(0,224-i*64),new Vector2(472,55),new Color(.06f,.12f,.19f)); stepCards[i]=row;
                var dot=Box(row.transform,"Número",new Vector2(-205,0),new Vector2(38,38),new Color(.12f,.25f,.33f)); dot.radius=19;
                stepNumbers[i]=Label(dot.transform,"Número",Vector2.zero,new Vector2(38,36),21,White,(i+1).ToString(),TextAlignmentOptions.Center);
                stepNames[i]=Label(row.transform,"Nombre",new Vector2(27,0),new Vector2(394,50),25,Muted,names[i]);
            }
            Ring(left,"Fondo de puntuación",new Vector2(-130,-227),new Vector2(130,130),new Color(.12f,.24f,.31f));
            scoreRing=Ring(left,"Puntos",new Vector2(-130,-227),new Vector2(130,130),Cyan);
            score=Label(left,"Nota",new Vector2(-130,-224),new Vector2(116,68),38,White,"—",TextAlignmentOptions.Center);
            Label(left,"Escala",new Vector2(-130,-261),new Vector2(90,23),17,Muted,"/ 100",TextAlignmentOptions.Center);
            countText=Label(left,"Ciclos",new Vector2(95,-208),new Vector2(210,60),43,White,"0 / 30");
            scoreCaption=Label(left,"Estado",new Vector2(95,-265),new Vector2(220,58),21,Muted,"Ciclos observados");
            AddButton(left,CprStationAction.Options,"Opciones de práctica",new Vector2(0,-351),new Vector2(462,49));
            optionsGroup=new GameObject("Controles opcionales",typeof(RectTransform)); optionsGroup.transform.SetParent(left,false);
            Box(optionsGroup.transform,"Fondo",new Vector2(0,0),new Vector2(484,610),new Color(.055f,.105f,.16f));
            Label(optionsGroup.transform,"Título",new Vector2(0,260),new Vector2(420,50),30,White,"CONTROLES OPCIONALES");
            var actions=new[]{CprStationAction.Pause,CprStationAction.Audio,CprStationAction.Mute,CprStationAction.Finish,CprStationAction.Menu,CprStationAction.Realign};
            string[] labels={"Pausar","Repetir voz","Silenciar","Terminar intento","Volver al menú","Volver a alinear apoyo"};
            for(int i=0;i<actions.Length;i++) AddButton(optionsGroup.transform,actions[i],labels[i],new Vector2(0,160-i*79),new Vector2(420,62));
            optionsGroup.SetActive(false); optionsOpen=false;

            phase=Label(center,"Paso actual",new Vector2(-100,347),new Vector2(580,38),24,Cyan,"TUTORIAL GUIADO");
            videoTag=Label(center,"Modo",new Vector2(287,347),new Vector2(205,38),19,Green,"APRENDE HACIENDO",TextAlignmentOptions.MidlineRight);
            stepTitle=Label(center,"Título",new Vector2(0,289),new Vector2(784,66),39,White,""); stepTitle.fontStyle=FontStyles.Bold;
            explanation=Label(center,"Indicación",new Vector2(0,208),new Vector2(784,82),29,White,"");
            videoFrame=new GameObject("Demostración animada",typeof(RectTransform)); videoFrame.transform.SetParent(center,false);
            var backdrop=Box(videoFrame.transform,"Marco",new Vector2(0,-57),new Vector2(786,444),new Color(.17f,.35f,.46f));
            var screen=new GameObject("Vídeo",typeof(RectTransform),typeof(RawImage)).GetComponent<RawImage>(); screen.transform.SetParent(videoFrame.transform,false);
            screen.rectTransform.anchoredPosition=new Vector2(0,-57); screen.rectTransform.sizeDelta=new Vector2(780,438.75f); screen.raycastTarget=false;
            if(config.practiceVideoPosters!=null && config.practiceVideoPosters.Length>0) screen.texture=config.practiceVideoPosters[0];
            lessonVideo=screen.gameObject.AddComponent<CprLessonVideo>(); lessonVideo.Setup(screen);
            resultsDetail=Label(center,"Criterios del resultado",new Vector2(0,-44),new Vector2(744,432),27,White,""); resultsDetail.gameObject.SetActive(false);
            var feed=Box(center,"Confirmación",new Vector2(0,-326),new Vector2(784,66),new Color(.06f,.17f,.21f));
            feedbackAccent=Box(feed.transform,"Acento",new Vector2(-378,0),new Vector2(5,43),Cyan);
            feedback=Label(feed.transform,"Mensaje",Vector2.zero,new Vector2(736,59),24,Cyan,"");
            var progress=Box(center,"Progreso",new Vector2(0,-377),new Vector2(784,6),new Color(.12f,.23f,.31f));
            progressFill=Box(progress.transform,"Avance",new Vector2(-392,0),new Vector2(0,6),Green).rectTransform; progressFill.pivot=new Vector2(0,.5f);
            AddButton(center,CprStationAction.Primary,"Repetir práctica",new Vector2(-200,-326),new Vector2(384,66),true);
            AddButton(center,CprStationAction.Alternative,"Ver incidencias",new Vector2(200,-326),new Vector2(384,66));
            Button(CprStationAction.Primary).gameObject.SetActive(false);
            Button(CprStationAction.Alternative).gameObject.SetActive(false);

            Label(rhythm,"Título",new Vector2(0,340),new Vector2(290,54),28,Cyan,"RITMO",TextAlignmentOptions.Center);
            rateText=Label(rhythm,"Ritmo",new Vector2(0,236),new Vector2(296,102),82,White,"—",TextAlignmentOptions.Center);
            Label(rhythm,"Unidad",new Vector2(0,159),new Vector2(290,42),24,Muted,"por minuto",TextAlignmentOptions.Center);
            Pill(rhythm,"Referencia",new Vector2(0,93),new Vector2(290,53),$"{config.minimumRate:0}–{config.maximumRate:0} / min");
            Label(rhythm,"Pulso",new Vector2(0,15),new Vector2(288,60),22,White,"AL LLEGAR A VERDE,\nBAJA CON EL PITIDO",TextAlignmentOptions.Center);
            beatTrack=Box(rhythm,"Pista de ritmo",new Vector2(0,-75),new Vector2(274,35),new Color(.12f,.24f,.31f)).rectTransform;
            beatFlash=Box(beatTrack,"Zona verde",new Vector2(109.6f,0),new Vector2(54.8f,35),Green);
            beatNeedle=Box(beatTrack,"Pulso móvil",new Vector2(-137,0),new Vector2(8,60),White).rectTransform;
            Label(rhythm,"Retorno",new Vector2(0,-139),new Vector2(288,54),20,Muted,"Vuelve arriba antes\ndel siguiente pitido.",TextAlignmentOptions.Center);
            pressFeedback=Box(rhythm,"Respuesta de pulsación",new Vector2(0,-238),new Vector2(300,96),new Color(.055f,.11f,.16f));
            rateStatus=Label(rhythm,"Estado del ritmo",new Vector2(0,-238),new Vector2(288,90),24,White,"Esperando\ncompresiones",TextAlignmentOptions.Center);
            Label(rhythm,"Nota",new Vector2(0,-337),new Vector2(286,71),18,Muted,$"Guía fija: {(config.minimumRate+config.maximumRate)*.5f:0} / min.\nEl número muestra tu ritmo.",TextAlignmentOptions.Center);

            Label(depth,"Título",new Vector2(0,340),new Vector2(290,64),25,Cyan,"RECORRIDO\nVIRTUAL",TextAlignmentOptions.Center);
            depthText=Label(depth,"Recorrido",new Vector2(0,236),new Vector2(296,102),78,White,"—",TextAlignmentOptions.Center);
            Label(depth,"Unidad",new Vector2(0,159),new Vector2(290,42),24,Muted,"cm virtuales",TextAlignmentOptions.Center);
            Pill(depth,"Referencia",new Vector2(0,93),new Vector2(290,53),$"Verde: {config.minimumVirtualDepth*100:0}–{config.maximumVirtualDepth*100:0} cm");
            for(int i=0;i<16;i++)
            {
                float d=(i+.5f)*.005f; Color c=d<config.minimumVirtualDepth?new Color(.72f,.49f,.18f):d<=config.maximumVirtualDepth?Green:new Color(.75f,.25f,.23f);
                Box(depth,"Tramo "+i,new Vector2(-64,-216+i*15.2f),new Vector2(56,12),c);
            }
            foreach(float value in new[]{0f,.05f,.06f,.08f})
                Label(depth,"Escala "+value,new Vector2(15,-224+243*value/.08f),new Vector2(100,25),20,Muted,$"{value*100:0} cm");
            Box(depth,"Pista de recorrido",new Vector2(-111,-102.5f),new Vector2(14,243),new Color(.12f,.24f,.31f));
            depthFill=Box(depth,"Recorrido actual",new Vector2(-111,-224),new Vector2(14,0),Cyan).rectTransform;
            depthFill.pivot=new Vector2(.5f,0);
            depthLastPeak=Box(depth,"Último ciclo",new Vector2(-64,-224),new Vector2(76,4),Amber).rectTransform;
            var cursor=Box(depth,"Medición",new Vector2(-64,-224),new Vector2(104,17),Dark);
            depthNeedle=cursor.rectTransform;
            Box(cursor.transform,"Marca visible",Vector2.zero,new Vector2(100,9),White);
            depthStatus=Label(depth,"Estado",new Vector2(0,-273),new Vector2(294,56),22,White,"Vuelve al reposo",TextAlignmentOptions.Center);
            returnStatus=Label(depth,"Alcance",new Vector2(0,-344),new Vector2(290,62),18,Muted,"Recorrido de manos.\nNo es profundidad física.",TextAlignmentOptions.Center);

            Label(lift,"Título",new Vector2(0,304),new Vector2(174,64),22,Cyan,"AJUSTAR\nALTURA",TextAlignmentOptions.Center);
            AddButton(lift,CprStationAction.HeightUp,"+",new Vector2(0,232),new Vector2(142,52));
            var sliderGo=new GameObject("Subidor",typeof(RectTransform),typeof(Slider)); var sr=(RectTransform)sliderGo.transform; sr.SetParent(lift,false); sr.anchoredPosition=new Vector2(0,73); sr.sizeDelta=new Vector2(100,245);
            Box(sr,"Pista",Vector2.zero,new Vector2(11,245),new Color(.15f,.31f,.40f)).raycastTarget=true;
            var area=new GameObject("Área",typeof(RectTransform)).GetComponent<RectTransform>(); area.SetParent(sr,false); area.sizeDelta=new Vector2(100,245);
            var handle=Box(area,"Manija",Vector2.zero,new Vector2(79,30),Cyan); handle.raycastTarget=true;
            heightSlider=sliderGo.GetComponent<Slider>(); heightSlider.direction=Slider.Direction.BottomToTop; heightSlider.handleRect=handle.rectTransform; heightSlider.targetGraphic=handle;
            heightSlider.minValue=0; heightSlider.maxValue=config.maximumStationLift; heightSlider.navigation=new Navigation{mode=Navigation.Mode.None};
            AddButton(lift,CprStationAction.HeightDown,"−",new Vector2(0,-86),new Vector2(142,52));
            heightText=Label(lift,"Altura",new Vector2(0,-142),new Vector2(174,54),21,White,"+0 cm",TextAlignmentOptions.Center);
            AddButton(lift,CprStationAction.AlignHeight,"A mi altura",new Vector2(0,-215),new Vector2(170,55));
            AddButton(lift,CprStationAction.ResetHeight,"Restablecer",new Vector2(0,-289),new Vector2(170,50));
        }

        private void ShowScreens(CprPracticeSession s,float height,float beatPhase,bool muted,bool focus)
        {
            phase.text=s.Closed?(s.Completed?"PRÁCTICA COMPLETADA":"INTENTO INTERRUMPIDO"):s.Step==CprPracticeStep.Welcome?"TUTORIAL GUIADO":$"PASO {(int)s.Step} DE 6";
            videoTag.text=s.Closed?"RESUMEN":s.Paused?"EN PAUSA":s.Explaining?"ESCUCHA LA GUÍA":s.StepPassed?"PASO RECONOCIDO":"AHORA PRACTICA";
            stepTitle.text=CprPracticeContent.Titles[(int)s.Step]; explanation.text=s.Closed?CprPracticeContent.Recommendation(s):CprPracticeContent.Explanations[(int)s.Step];
            timeText.text=$"TIEMPO  {Mathf.FloorToInt(s.TotalSeconds/60):00}:{Mathf.FloorToInt(s.TotalSeconds%60):00}";
            for(int i=0;i<6;i++)
            {
                bool done=i<s.StepsDone, active=(int)s.Step==i+1 && !s.Closed;
                stepCards[i].color=done?new Color(.06f,.25f,.22f):active?new Color(.08f,.30f,.43f):new Color(.055f,.10f,.16f);
                stepNames[i].color=done?Green:active?White:Muted; stepNumbers[i].text=done?"OK":(i+1).ToString(); stepNumbers[i].fontSize=done?16:21;
            }
            score.text=s.Score.HasValue?s.Score.Value.ToString():"—"; scoreRing.SetAmount(s.Score.HasValue?s.Score.Value/100f:0);
            scoreRing.color=!s.Closed?Cyan:s.Score>=85?Green:s.Score>=65?Amber:Red;
            countText.text=$"{s.Count} / {settings.practiceCompressions}";
            scoreCaption.text=!s.Closed?"Ciclos observados":!s.Score.HasValue?"Sin datos suficientes":s.Score>=85?"Objetivo logrado":s.Score>=65?"En progreso":"Sigue practicando";
            var color=s.FeedbackKind==CprFeedbackKind.Correction?Amber:s.FeedbackKind==CprFeedbackKind.Success?Green:Cyan;
            feedback.text=!focus?"Registro pausado mientras el visor no tiene foco.":s.Feedback; feedback.color=feedbackAccent.color=color;
            feedback.transform.parent.gameObject.SetActive(!s.Closed);
            progressFill.sizeDelta=new Vector2(784*Mathf.Clamp01(s.Progress),6);
            videoFrame.SetActive(!s.Closed); resultsDetail.gameObject.SetActive(s.Closed);
            bool fallback=!s.Closed && !string.IsNullOrEmpty(NarrationFallbackText);
            explanation.rectTransform.anchoredPosition=fallback?new Vector2(0,-10):new Vector2(0,208);
            explanation.rectTransform.sizeDelta=fallback?new Vector2(784,540):new Vector2(784,82);
            explanation.enableAutoSizing=fallback; explanation.fontSizeMin=20; explanation.fontSizeMax=29;
            if(fallback) { explanation.text=NarrationFallbackText; videoFrame.SetActive(false); }
            if(Application.isPlaying && lessonVideo.Player!=null && !s.Closed) lessonVideo.Show(s.Step,!s.Paused && focus);
            if(s.Closed)
            {
                var b=s.Breakdown;
                string placement=s.LocationSeconds>0 && s.Count>0?$"{b.placement:0.0} / 20 puntos":"No evaluable: sin ciclos en zona";
                string travelPoints=s.Count>0?$"{b.travel:0.0} / 25 puntos":"No evaluable: sin ciclos";
                string rhythm=s.RateSamples>0?$"{b.rhythm:0.0} / 25 puntos":"No evaluable: faltan intervalos";
                string recoil=s.Count>0?$"{b.recoil:0.0} / 10 puntos":"No evaluable: sin ciclos";
                string saved=s.SaveError!=null?"<color=#FFB84D>No se pudo guardar el registro.</color>"
                    :s.SavedReportPath!=null?"Registro guardado en este dispositivo.":"Guardando registro…";
                resultsDetail.text=showIncidents
                    ? CprScoreImpact.Summary(s)
                    : $"Preparación: {b.preparation:0.0} / 20 puntos\nColocación: {placement}\nRecorrido: {travelPoints}\nRitmo: {rhythm}\nRetorno: {recoil}\nPausas: −{b.pauseDeduction:0.0} puntos\n\nEjecución observada: {s.Count}/{s.Settings.practiceCompressions} ciclos.\nColocación: −2 por alejamiento o separación.\n<color=#85B7CF>Intentos cortos: ejecución proporcional.</color>\n{saved}";
                resultsDetail.fontSize=showIncidents?24:27;
                buttonLabels[(int)CprStationAction.Alternative].text=showIncidents?"Ver puntos":"Ver incidencias";
            }
            else showIncidents=false;
            bool measuring=!s.Closed && s.Step==CprPracticeStep.Compressions && !s.Explaining && !s.Paused && focus;
            bool valid=measuring && (s.Detector.State==CprCaptureState.Ready || s.Detector.State==CprCaptureState.Compressing);
            float rate=s.Closed?s.MeanRate:s.Detector.Rate;
            bool hasRate=s.Closed?s.RateSamples>0:valid && s.Detector.RateEvaluable;
            rateText.text=hasRate?$"{rate:0}":"—"; rateText.color=!hasRate?Muted:rate>=settings.minimumRate && rate<=settings.maximumRate?Green:Amber;
            rateStatus.text=s.Closed?$"Lentos: {s.Slow}\nRápidos: {s.Fast}":!measuring?"Esperando\ncompresiones":!hasRate?"Observando\ntus movimientos":rate<settings.minimumRate?"Acelera un poco":rate>settings.maximumRate?"Más despacio":"Buen ritmo";
            bool showPress=measuring && s.Pacing.FeedbackVisible;
            pressFeedback.color=showPress?(s.Pacing.LastResult==CprBeatResult.OnTime?new Color(.06f,.35f,.22f):new Color(.42f,.07f,.08f)):new Color(.055f,.11f,.16f);
            rateStatus.color=showPress?(s.Pacing.LastResult==CprBeatResult.OnTime?Green:Red):White;
            if(showPress)
                rateStatus.text=s.Pacing.LastResult==CprBeatResult.OnTime?"A TIEMPO\nBien, sigue así":s.Pacing.LastResult==CprBeatResult.Early?"TE ADELANTASTE\nEspera el pitido":s.Pacing.LastResult==CprBeatResult.Late?"TARDE\nSigue el pitido":"MUY SEGUIDO\nUn gesto por pitido";
            bool atBeat=CprBeatClock.AtBeat(beatPhase);
            beatFlash.color=measuring && atBeat?Green:new Color(.13f,.37f,.28f);
            beatNeedle.gameObject.SetActive(measuring);
            beatNeedle.GetComponent<Graphic>().color=showPress?(s.Pacing.LastResult==CprBeatResult.OnTime?Green:Red):White;
            beatNeedle.anchoredPosition=new Vector2(Mathf.Lerp(-137,137,CprBeatClock.MarkerProgress(beatPhase)),0);
            float travel=s.Closed?s.MeanDepth:s.Detector.DisplayDepth;
            bool hasDepth=s.Closed?s.Count>0:s.Detector.DisplayAvailable;
            bool freshDepth=s.Closed || measuring && s.Detector.DisplayFresh;
            if(!hasDepth) travel=0;
            depthText.text=hasDepth?$"{travel*100:0.0}":"—";
            // Mantener visible un cursor de referencia en cero incluso antes de la primera muestra y durante la recuperación.
            // Conservar la última posición medida si se pierde el seguimiento; nunca animar una compresión inventada.
            depthNeedle.gameObject.SetActive(true);
            depthNeedle.anchoredPosition=new Vector2(-64,-224+243*Mathf.Clamp01(travel/.08f));
            depthNeedle.Find("Marca visible").GetComponent<Graphic>().color=hasDepth && freshDepth?White:Muted;
            depthFill.sizeDelta=new Vector2(14,243*Mathf.Clamp01(travel/.08f));
            depthFill.GetComponent<Graphic>().color=freshDepth?Cyan:Muted;
            depthLastPeak.gameObject.SetActive(s.Count>0 && !s.Closed);
            if(s.Count>0) depthLastPeak.anchoredPosition=new Vector2(-64,-224+243*Mathf.Clamp01(s.Events[s.Count-1].depth/.08f));
            depthText.color=!hasDepth || !freshDepth?Muted:travel>settings.maximumVirtualDepth?Red:travel>=settings.minimumVirtualDepth?Green:White;
            depthStatus.text=s.Closed?$"Cortos: {s.Shallow}\nExcesivos: {s.Excessive}":s.Paused?"Pausado\nLectura retenida":!focus?"Práctica sin foco":!measuring?"Esperando\ncompresiones":!hasDepth?"Muestra las manos\nen la guía del pecho":!freshDepth?s.Detector.State==CprCaptureState.OutsideZone?"Vuelve al centro\ndel pecho":"Lectura retenida\nMuestra las manos":travel>settings.maximumVirtualDepth?"Demasiado: vuelve arriba":travel>=settings.minimumVirtualDepth?"Bien: vuelve arriba":travel<=settings.compressionReturn?"Listo para otra bajada":"Baja hasta verde y vuelve";
            returnStatus.text=s.Count>0?$"<color=#FFB840>Último ciclo: {s.Events[s.Count-1].depth*100:0.0} cm</color>\nBlanco: recorrido actual.":"Blanco: recorrido actual.\nBaja y vuelve para contar.";
            Button(CprStationAction.Primary).gameObject.SetActive(s.Closed); Button(CprStationAction.Primary).interactable=s.Closed && focus;
            Button(CprStationAction.Alternative).gameObject.SetActive(s.Closed); Button(CprStationAction.Alternative).interactable=s.Closed && focus;
            foreach(var action in new[]{CprStationAction.Menu,CprStationAction.Audio,CprStationAction.Mute,CprStationAction.Options}) Button(action).interactable=focus;
            Button(CprStationAction.Pause).interactable=Button(CprStationAction.Finish).interactable=focus && !s.Closed;
            Button(CprStationAction.Realign).interactable=focus && !s.Closed;
            buttonLabels[(int)CprStationAction.Pause].text=s.Paused?"Reanudar":"Pausar";
            buttonLabels[(int)CprStationAction.Mute].text=muted?"Activar audio":"Silenciar";
            buttonLabels[(int)CprStationAction.Options].text=optionsOpen?"Cerrar opciones":"Opciones de práctica";
            bool adjust=focus && s.CanAdjustHeight;
            heightSlider.interactable=adjust;
            if(heightDrag==null || !heightDrag.Dragging) heightSlider.SetValueWithoutNotify(height);
            foreach(var action in new[]{CprStationAction.HeightUp,CprStationAction.HeightDown,CprStationAction.AlignHeight,CprStationAction.ResetHeight}) Button(action).interactable=adjust;
            heightText.text=adjust?$"+{(heightDrag!=null && heightDrag.Dragging?heightSlider.value:height)*100:0} cm":"Pausa para\najustar";
            if(!focus) ClearInput();
        }
        private static CprRingGraphic Ring(Transform parent,string name,Vector2 position,Vector2 size,Color color)
        {
            var ring=new GameObject(name,typeof(RectTransform),typeof(CprRingGraphic)).GetComponent<CprRingGraphic>(); ring.transform.SetParent(parent,false);
            ring.rectTransform.anchoredPosition=position; ring.rectTransform.sizeDelta=size; ring.color=color; ring.raycastTarget=false; return ring;
        }
        private static void Pill(Transform parent,string name,Vector2 p,Vector2 size,string text)
        { var plate=Box(parent,name,p,size,new Color(.07f,.31f,.25f)); Label(plate.transform,"Texto",Vector2.zero,size-new Vector2(12,4),24,Green,text,TextAlignmentOptions.Center); }
    }
}
