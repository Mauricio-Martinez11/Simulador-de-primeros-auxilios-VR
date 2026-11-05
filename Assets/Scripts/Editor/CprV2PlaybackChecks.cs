using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using FirstAidVR.Rcp;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

[InitializeOnLoad]
public static class CprV2PlaybackChecks
{
    private const string Pending="RCP.V2.PlaybackPending", Exit="RCP.V2.PlaybackExit";
    private static IEnumerator routine;
    private static int frame,passed;
    static CprV2PlaybackChecks()
    {
        EditorApplication.playModeStateChanged+=state=>
        {
            if(state==PlayModeStateChange.EnteredPlayMode && SessionState.GetBool(Pending,false))
                EditorApplication.delayCall+=Start;
            if(state==PlayModeStateChange.EnteredEditMode && SessionState.GetInt(Exit,-1)>=0)
            {
                int code=SessionState.GetInt(Exit,-1); SessionState.EraseInt(Exit);
                if(Application.isBatchMode) EditorApplication.delayCall+=()=>EditorApplication.Exit(code);
            }
        };
    }
    public static void Run()
    {
        CprScoreImpactChecks.Run();
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
        SessionState.SetBool(Pending,true); EditorApplication.isPlaying=true;
    }
    private static void Start()
    { SessionState.SetBool(Pending,false); passed=0; frame=-1; routine=Exercise(); EditorApplication.update+=Advance; }
    private static void Advance()
    {
        if(frame==Time.frameCount) return; frame=Time.frameCount;
        int exit=0;
        try { if(routine.MoveNext()) return; }
        catch(Exception error) { Debug.LogException(error); exit=1; }
        EditorApplication.update-=Advance; SessionState.SetInt(Exit,exit); EditorApplication.isPlaying=false;
    }
    private static void Check(bool condition,string text)
    { if(!condition) throw new InvalidOperationException("V2 reproducción: "+text); passed++; }
    private static IEnumerator Exercise()
    {
        var host=new GameObject("Prueba real de audio");
        host.AddComponent<AudioListener>();
        var narrator=host.AddComponent<CprNarrator>(); narrator.Play("Confirm02");
        var source=host.GetComponent<AudioSource>();
        float deadline=Time.realtimeSinceStartup+4;
        while(source.time<.4f && Time.realtimeSinceStartup<deadline)
        { narrator.Tick(Time.unscaledDeltaTime,true); yield return null; }
        Check(source.isPlaying && narrator.Speaking && !narrator.Complete,"reproducción real iniciada");
        var samples=new float[1024]; float mixPeak=0;
        float mixDeadline=Time.realtimeSinceStartup+1;
        while(mixPeak<.0001f && Time.realtimeSinceStartup<mixDeadline)
        {
            AudioListener.GetOutputData(samples,0);
            foreach(float sample in samples) mixPeak=Mathf.Max(mixPeak,Mathf.Abs(sample));
            narrator.Tick(Time.unscaledDeltaTime,true); yield return null;
        }
        Check(mixPeak>.0001f,"voz llega al mezclador de salida, no solo a isPlaying");
        narrator.Tick(.02f,false); float pausedAt=source.time;
        deadline=Time.realtimeSinceStartup+.4f;
        while(Time.realtimeSinceStartup<deadline) { narrator.Tick(.02f,false); yield return null; }
        Check(!source.isPlaying && Math.Abs(source.time-pausedAt)<.05f && !narrator.Complete,"foco perdido pausa sin consumir locución");
        narrator.Tick(.02f,true);
        deadline=Time.realtimeSinceStartup+source.clip.length+4;
        while(narrator.Speaking && Time.realtimeSinceStartup<deadline)
        { narrator.Tick(Time.unscaledDeltaTime,true); yield return null; }
        Check(!narrator.Speaking && !narrator.Complete,"fin de voz no salta los dos segundos");
        for(int i=0;i<15;i++) narrator.Tick(.1f,true);
        Check(!narrator.Complete,"1,5 segundos todavía bloquean");
        narrator.Tick(.1f,false);
        for(int i=0;i<50;i++) narrator.Tick(.1f,false);
        Check(!narrator.Complete,"pausa también conserva silencio pendiente");
        narrator.Tick(.1f,true);
        for(int i=0;i<6;i++) narrator.Tick(.1f,true);
        Check(narrator.Complete,"audio completo más dos segundos libera avance");
        narrator.Play("Hands"); narrator.ToggleMute();
        yield return null;
        Check(source.mute && source.isPlaying && !narrator.Complete,"silenciar conserva duración y secuencia");
        float beforeReset=source.time;
        narrator.SendMessage("AudioChanged",true);
        narrator.Tick(.02f,true); yield return null; narrator.Tick(.02f,true);
        Check(source.isPlaying && source.mute && source.time>=beforeReset-.2f,"recupera voz y silencio elegido tras reinicio de audio");
        narrator.Stop();
        var camera=new GameObject("Cámara").AddComponent<Camera>();
        var intro=new GameObject("Selector").AddComponent<CprIntroPanel>(); intro.Build(camera); intro.Present("Welcome","Entorno virtual","Realidad mixta");
        Check(UnityEngine.Object.FindObjectsOfType<AudioListener>().Count(l=>l.isActiveAndEnabled)==1 && camera.GetComponent<AudioListener>().enabled,"bienvenida asegura un listener en la cámara activa");
        intro.Choose(1);
        Check(intro.Selection==1 && !intro.FinishedSpeaking,"elección temprana no corta bienvenida");
        yield return null;
        Capture(camera,"welcome.png");
        intro.Present("Align","Confirmar alineación","Continuar sin apoyo");
        Check(intro.Selection==-1,"una elección anterior no confirma alineación");
        intro.Choose(0); Check(intro.Selection==0,"alineación se confirma manualmente");
        var mixed=host.AddComponent<CprMixedReality>();
        var enabling=mixed.Enable(camera,host.transform);
        while(enabling.MoveNext()) yield return enabling.Current;
        Check(mixed.Failed && !mixed.Active,"sin visor no simula un passthrough exitoso");
        UnityEngine.Object.Destroy(intro.gameObject); UnityEngine.Object.Destroy(camera.gameObject); UnityEngine.Object.Destroy(host);
        yield return null;
        // Validar la ruta de la escena real por separado de la prueba aislada de audio.
        typeof(CprFamiliarization).GetProperty("Completed").GetSetMethod(true).Invoke(null,new object[]{true});
        SceneManager.sceneLoaded+=DesktopScene;
        SceneManager.LoadScene("1_MainMenu");
        yield return null; yield return null;
        var practice=UnityEngine.Object.FindObjectsOfType<Button>().First(b=>b.name=="B_Ajustes");
        Check(practice.GetComponentInChildren<TMPro.TMP_Text>().text=="Practicar controles","menú ofrece repetir familiarización");
        var launch=UnityEngine.Object.FindObjectsOfType<Button>().First(b=>b.name=="B_Iniciar_Tutorial");
        launch.onClick.Invoke();
        yield return null; yield return null;
        Check(SceneManager.GetActiveScene().name=="2_Tutorial_RCP","botón real carga la escena del tutorial");
        var station=UnityEngine.Object.FindObjectOfType<CprStationController>();
        deadline=Time.realtimeSinceStartup+30;
        bool welcomeChosen=false;
        while(station.Session==null && Time.realtimeSinceStartup<deadline)
        {
            var choice=UnityEngine.Object.FindObjectOfType<CprIntroPanel>();
            if(choice!=null)
            {
                if(choice.GetComponentsInChildren<Button>().Length>0 && choice.Selection<0)
                { choice.Choose(0); welcomeChosen=true; }
                foreach(var audio in choice.GetComponents<AudioSource>())
                    if(audio.isPlaying && audio.time<audio.clip.length-.1f) audio.time=audio.clip.length-.08f;
            }
            yield return null;
        }
        Check(welcomeChosen && station.Session!=null,"bienvenida de escena respeta selección y prepara intento");
        Check(station.View.Panels.All(p=>p.gameObject.activeInHierarchy),"paneles reaparecen después de elegir modalidad");
        station.Act(CprStationAction.Menu);
        yield return null; yield return null;
        Check(SceneManager.GetActiveScene().name=="1_MainMenu","opción de sesión vuelve al menú");
        Check(UnityEngine.Object.FindObjectOfType<CprStationPreparation>()==null,"se limpia preparación al salir de la estación");
        var unavailable=UnityEngine.Object.FindObjectsOfType<Button>().First(b=>b.name=="B_Otros_Escenarios");
        Check(!unavailable.interactable && unavailable.GetComponentInChildren<TMPro.TMP_Text>().text.Contains("Próximamente"),"otros escenarios explica indisponibilidad");
        UnityEngine.Object.FindObjectsOfType<Button>().First(b=>b.name=="B_Escenario_Real").onClick.Invoke();
        yield return null; yield return null;
        Check(SceneManager.GetActiveScene().name=="2_Tutorial_RCP","acceso realidad mixta carga tutorial");
        station=UnityEngine.Object.FindObjectOfType<CprStationController>();
        deadline=Time.realtimeSinceStartup+35;
        bool mixedSelected=false;
        while(station.Session==null && Time.realtimeSinceStartup<deadline)
        {
            var choice=UnityEngine.Object.FindObjectOfType<CprIntroPanel>();
            if(choice!=null)
            {
                if(choice.Selection==1) mixedSelected=true;
                if(mixedSelected && choice.Selection<0 && choice.GetComponentsInChildren<Button>().Length>0) choice.Choose(0);
                foreach(var audio in choice.GetComponents<AudioSource>())
                    if(audio.isPlaying && audio.time<audio.clip.length-.1f) audio.time=audio.clip.length-.08f;
            }
            yield return null;
        }
        Check(mixedSelected && station.Session!=null,"acceso mixto conserva elección y permite alternativa sin visor");
        Check(!SceneLoader.PreferMixedReality,"elección de menú se consume una sola vez");
        station.Act(CprStationAction.Menu);
        yield return null; yield return null;
        SceneManager.sceneLoaded-=DesktopScene;
        Directory.CreateDirectory("Logs/V2Validation");
        File.WriteAllText("Logs/V2Validation/playback.txt",$"PASS: {passed} real AudioSource playback, pause, focus, silence, selection and unavailable-passthrough checks in Unity. Physical Quest test pending.\n");
        Debug.Log($"PASS: {passed} V2 playback checks");
    }
    private static void DesktopScene(Scene scene,LoadSceneMode mode)
    {
        if(scene.name=="2_Tutorial_RCP")
        {
            var tutorial=UnityEngine.Object.FindObjectOfType<CprTutorial>();
            tutorial.desktopPreview=true; tutorial.xrCorePrefab=null;
        }
    }
    private static void Capture(Camera camera,string filename)
    {
        Directory.CreateDirectory("Logs/V2Validation");
        var target=new RenderTexture(1600,1000,24); var previous=RenderTexture.active;
        camera.targetTexture=target; camera.clearFlags=CameraClearFlags.SolidColor; camera.backgroundColor=new Color(.10f,.13f,.17f);
        Canvas.ForceUpdateCanvases(); camera.Render(); RenderTexture.active=target;
        var image=new Texture2D(1600,1000,TextureFormat.RGB24,false); image.ReadPixels(new Rect(0,0,1600,1000),0,0); image.Apply();
        File.WriteAllBytes("Logs/V2Validation/"+filename,image.EncodeToPNG());
        camera.targetTexture=null; RenderTexture.active=previous;
        UnityEngine.Object.Destroy(image); target.Release(); UnityEngine.Object.Destroy(target);
    }
}

