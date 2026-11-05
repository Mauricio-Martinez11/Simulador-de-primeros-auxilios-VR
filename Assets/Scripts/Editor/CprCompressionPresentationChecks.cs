using System;
using System.IO;
using System.Linq;
using FirstAidVR.Rcp;
using TMPro;
using UnityEngine;

public static class CprCompressionPresentationChecks
{
    private static int passed;
    public static void Run()
    {
        passed=0;
        CprHandInputChecks.Run();
        var config=Resources.Load<CprTrainingSettings>("RCP/TrainingSettings");
        var beat=new CprBeatClock();
        float interval=60/110f;
        Check(!beat.Tick(.02f,interval) && beat.Phase==0,"marca inicia antes del primer pitido");
        Check(!beat.Tick(interval*.79f,interval),"sin sonido antes de entrar a verde");
        Check(beat.Tick(interval*.02f,interval) && CprBeatClock.AtBeat(beat.Phase),"pitido al entrar a verde");
        int pulses=0;
        for(int i=0;i<1100;i++) if(beat.Tick(interval/100,interval)) pulses++;
        Check(pulses>=10 && pulses<=11,"pulso constante independiente del movimiento");
        beat.Reset();
        Check(!beat.Tick(.02f,interval) && beat.Phase==0,"reanudar prepara nuevo pulso sin ráfaga");

        var detector=new CprCompressionDetector(config);
        var chest=new Pose(Vector3.zero,Quaternion.identity);
        Func<float,bool,CprHandSample> sample=(d,right)=>new CprHandSample(new Pose(Vector3.up*((right?0:.035f)-d),Quaternion.identity));
        detector.Calibrate(sample(0,false),sample(0,true),chest);
        int count=0; detector.Compression+=e=>count++;
        float clock=0;
        for(int i=0;i<40;i++) detector.Tick(.01f,clock+=.01f,sample(0,false),sample(0,true),chest,true);
        for(int i=1;i<=20;i++) detector.Tick(.01f,clock+=.01f,sample(.055f*i/20,false),sample(.055f*i/20,true),chest,true);
        for(int i=0;i<100;i++) detector.Tick(.01f,clock+=.01f,sample(.055f,false),sample(.055f,true),chest,true);
        Check(count==0,"mantener abajo no cuenta como pulsaciones repetidas");
        for(int i=19;i>=0;i--) detector.Tick(.01f,clock+=.01f,sample(.055f*i/20,false),sample(.055f*i/20,true),chest,true);
        Check(count==1,"una bajada con retorno completo cuenta una vez");
        for(int i=0;i<40;i++) detector.Tick(.01f,clock+=.01f,sample(0,false),sample(0,true),chest,true);
        Check(count==1,"permanecer arriba no suma");

        CprStationTools.BuildScene();
        var station=UnityEngine.Object.FindObjectOfType<CprStationController>();
        var patient=UnityEngine.Object.FindObjectOfType<CprTutorial>().patient;
        var chestView=station.StationRoot.GetComponent<CprStationPatient>();
        chestView.PrepareContact(patient,station.Chest,config);
        var session=CprStationChecks.Prepared(config,false);
        var cameraObject=new GameObject("Compression validation camera");
        var camera=cameraObject.AddComponent<Camera>();
        camera.nearClipPlane=.01f; camera.fieldOfView=42;
        camera.clearFlags=CameraClearFlags.SolidColor; camera.backgroundColor=new Color(.76f,.82f,.90f);
        station.View.Bind(config,camera);
        var rest=chestView.ContactPose(station.Chest,0);
        var originalReference=station.Chest;
        var normal=station.Chest.rotation*Vector3.up;
        var guide=station.View.transform.Find("Guía derecha RCP");
        var topGuide=station.View.transform.Find("Guía izquierda RCP");
        var shirt=patient.GetComponentsInChildren<SkinnedMeshRenderer>().First(s=>s.name.Contains("Basic_T_shirts"));
        for(int i=0;i<4;i++)
        {
            float depth=new[]{0,.055f,.12f,0}[i];
            chestView.SetCompression(depth,normal);
            var contact=chestView.ContactPose(station.Chest,depth);
            Check(Vector3.Distance(rest.position,contact.position)<=CprStationPatient.MaximumVisualTravel+.001f,"recorrido visual limitado "+i);
            station.View.Guides(session,station.Chest,station.Shoulder,
                new Pose(station.Chest.position+normal*config.stackedHandHeight,station.Chest.rotation),false,contact);
            Check(Vector3.Distance(guide.position,contact.position)<.0001f,"guía sigue superficie "+i);
            Check(Mathf.Abs(Vector3.Dot(topGuide.position-guide.position,normal)-config.stackedHandHeight)<.0001f,"manos mantienen separación "+i);
            Check(Vector3.Distance(station.Chest.position,originalReference.position)<.0001f,"animación no mueve ancla de medición "+i);
            Check(AboveShirt(shirt,contact,config),"talón inferior sobre superficie deformada "+i);
            camera.transform.position=contact.position+station.Chest.rotation*new Vector3(-.34f,.22f,-.42f);
            camera.transform.LookAt(contact.position+normal*.015f,normal);
            Capture(camera,"Compression-contact-"+i+".png");
        }
        Check(Vector3.Distance(rest.position,chestView.ContactPose(station.Chest,0).position)<.0001f,"vuelve al contacto inicial");
        foreach(float phase in new[]{.86f,.4f})
        {
            station.View.Show(session,0,phase,false,true); Canvas.ForceUpdateCanvases();
            var needle=(RectTransform)station.View.Panels[2].transform.Find("Pista de ritmo/Pulso móvil");
            Check(phase>=.8f?needle.anchoredPosition.x>=82.2f:needle.anchoredPosition.x<82.2f,
                "marca en verde al pitido; avanza hacia el siguiente en el intervalo");
            foreach(var text in station.View.Panels.SelectMany(p=>p.GetComponentsInChildren<TextMeshProUGUI>()))
            { text.ForceMeshUpdate(); Check(!text.isTextOverflowing,"texto sin recorte: "+text.name); }
            camera.transform.position=station.StationRoot.TransformPoint(new Vector3(.63f,.75f,-.55f));
            camera.transform.LookAt(station.View.Panels[2].transform.position+Vector3.right*.15f,Vector3.up);
            Capture(camera,phase>=.8f?"Compression-meters-beat.png":"Compression-meters-between.png");
        }
        chestView.SetCompression(0,normal);
        UnityEngine.Object.DestroyImmediate(cameraObject);
        File.WriteAllText(Path.Combine(Application.dataPath,"../compression-presentation-checks.txt"),$"PASS: {passed} presentation checks. Live hand-source, flow and station checks also passed. Quest verification pending.");
        Debug.Log($"PASS: {passed} compression presentation checks");
    }
    private static bool AboveShirt(SkinnedMeshRenderer skin,Pose contact,CprTrainingSettings config)
    {
        var mesh=new Mesh(); skin.BakeMesh(mesh);
        var vertices=mesh.vertices;
        for(int i=0;i<vertices.Length;i++) vertices[i]=skin.transform.position+skin.transform.rotation*vertices[i];
        mesh.vertices=vertices; mesh.RecalculateBounds();
        var go=new GameObject("Contact validation surface");
        var collider=go.AddComponent<MeshCollider>(); collider.sharedMesh=mesh;
        var normal=contact.rotation*Vector3.up;
        bool above=true; int hits=0;
        foreach(var hand in new[]{config.stackedRightHand})
        {
            var handVertices=hand.vertices;
            foreach(var vertex in handVertices)
            {
                if(Mathf.Abs(vertex.x)>=.032f || Mathf.Abs(vertex.z)>=.022f) continue;
                var local=vertex+(hand==config.stackedLeftHand?Vector3.up*config.stackedHandHeight:Vector3.zero);
                var position=contact.position+contact.rotation*local;
                if(collider.Raycast(new Ray(position+normal*.3f,-normal),out var point,1))
                { hits++; above &= Vector3.Dot(position-point.point,normal)>=.0015f; }
            }
        }
        UnityEngine.Object.DestroyImmediate(go); UnityEngine.Object.DestroyImmediate(mesh);
        return above && hits>0;
    }
    private static void Check(bool ok,string label)
    { if(!ok) throw new InvalidOperationException(label); passed++; }
    private static void Capture(Camera camera,string name)
    {
        var target=new RenderTexture(1400,1000,24); var image=new Texture2D(1400,1000,TextureFormat.RGB24,false);
        var active=RenderTexture.active;
        try {
            camera.targetTexture=target; camera.Render(); RenderTexture.active=target;
            image.ReadPixels(new Rect(0,0,1400,1000),0,0); image.Apply();
            File.WriteAllBytes(Path.Combine(Application.dataPath,"../"+name),image.EncodeToPNG());
        }
        finally { camera.targetTexture=null; RenderTexture.active=active; UnityEngine.Object.DestroyImmediate(image); UnityEngine.Object.DestroyImmediate(target); }
    }
}
