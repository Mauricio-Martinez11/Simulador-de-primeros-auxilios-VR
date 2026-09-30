using System;
using System.Collections.Generic;
using System.Linq;
using FirstAidVR.Rcp;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class CprTransportChecks
{
    public static int Passed { get; private set; }
    public static float WorstJointError { get; private set; }
    public static float DragDistance { get; private set; }
    public static float LiftError { get; private set; }
    public static float SelfPenetration { get; private set; }

    public sealed class Fixture : IDisposable
    {
        public Scene scene;
        public PhysicsScene physics;
        public GameObject host, patient, floor;
        public CprPatientTransport mover;
        public CprTrainingSettings settings;
        public Pose initial;
        public float dt = .01f;
        private Transform[] bones;
        private Vector3[] offsets, scales;
        public Fixture(bool ankles=true)
        {
            scene=SceneManager.CreateScene("Prueba ragdoll "+Guid.NewGuid(),new CreateSceneParameters(LocalPhysicsMode.Physics3D));
            physics=scene.GetPhysicsScene();
            settings=UnityEngine.Object.Instantiate(Resources.Load<CprTrainingSettings>("RCP/TrainingSettings"));
            settings.showAnkleGrips=ankles;
            patient=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Hombre/Hombre_Paciente.Fbx"));
            patient.name="Paciente prueba física";
            SceneManager.MoveGameObjectToScene(patient,scene);
            patient.transform.SetPositionAndRotation(new Vector3(0,.16f,-.877f),Quaternion.Euler(-89.948f,0,0));
            initial=new Pose(patient.transform.position,patient.transform.rotation);
            bones=patient.GetComponentsInChildren<Transform>(); offsets=bones.Select(t=>t.localPosition).ToArray(); scales=bones.Select(t=>t.localScale).ToArray();
            floor=GameObject.CreatePrimitive(PrimitiveType.Cube); floor.name="Suelo de prueba";
            SceneManager.MoveGameObjectToScene(floor,scene);
            floor.transform.position=new Vector3(0,-.05f,0); floor.transform.localScale=new Vector3(20,.1f,20);
            host=new GameObject("Traslado de prueba"); SceneManager.MoveGameObjectToScene(host,scene);
            mover=host.AddComponent<CprPatientTransport>(); mover.Initialize(settings,patient.transform);
            Physics.SyncTransforms();
        }
        public CprHandSample At(int site,float grip)
        { return new CprHandSample(mover.Guides.HandPose(site),false,grip,true); }
        public void Step(float seconds,CprHandSample left=default,CprHandSample right=default,bool enabled=true)
        {
            for(int i=0;i<Mathf.CeilToInt(seconds/dt);i++)
            {
                mover.Tick(dt,left,right,enabled);
                mover.Ragdoll.FixedTick(dt);
                physics.Simulate(dt);
                mover.Ragdoll.SyncVisuals();
                WorstJointError=Mathf.Max(WorstJointError,mover.Ragdoll.MaxJointError);
            }
        }
        public void Reset()
        { mover.ResetPatient(); Physics.SyncTransforms(); }
        public void CheckLengths()
        {
            for(int i=1;i<bones.Length;i++)
                if(Vector3.Distance(bones[i].localPosition,offsets[i])>.00001f || Vector3.Distance(bones[i].localScale,scales[i])>.00001f)
                    throw new Exception("Se ha alterado la longitud o escala del hueso "+bones[i].name);
            Check(true,"Las longitudes y escalas originales de la malla permanecen intactas");
        }
        public void Dispose()
        {
            UnityEngine.Object.DestroyImmediate(host); UnityEngine.Object.DestroyImmediate(patient); UnityEngine.Object.DestroyImmediate(floor);
            UnityEngine.Object.DestroyImmediate(settings);
            SceneManager.UnloadSceneAsync(scene);
        }
    }

    [MenuItem("Primeros auxilios/RCP/Verificar agarre y traslado")]
    public static void Run()
    {
        if (!Application.isPlaying) { RagdollValidationRunner.Run(); return; }
        Passed=0; WorstJointError=0; DragDistance=0; LiftError=0; SelfPenetration=0;
        Check(CprTrackedHands.CombineGrip(.9f,1,0,false,out var valid)>.65f && valid,"Un dedo curvado fiable mantiene el puño aunque los demás estén ocultos");
        Check(CprTrackedHands.CombineGrip(0,0,0,true,out valid)==0 && valid,"Una pinza abierta observada rearma la mano");
        CprTrackedHands.CombineGrip(0,0,0,false,out valid);
        Check(!valid,"La ausencia de datos no se interpreta como apertura");
        using(var f=new Fixture())
        {
            var rag=f.mover.Ragdoll;
            Check(rag.Parts.Count==16,"16 cuerpos anatómicos");
            Check(rag.Parts.Count(p=>p.joint!=null)==15,"Una cadena conectada sin huesos sueltos");
            Check(rag.Parts.All(p=>p.body.transform.lossyScale==Vector3.one),"Física a escala unitaria");
            Check(rag.Parts.All(p=>p.body.constraints==RigidbodyConstraints.None),"Ningún cuerpo fijado al plano");
            Check(rag.Parts.Where(p=>p.parent!=null).All(p=>p.joint.connectedBody==p.parent.body),"Cada articulación conecta al padre correcto");
            Check(rag.Parts.Where(p=>p.joint!=null).All(p=>p.joint.xMotion==ConfigurableJointMotion.Locked && p.joint.yMotion==ConfigurableJointMotion.Locked && p.joint.zMotion==ConfigurableJointMotion.Locked),"Las articulaciones no permiten elongación");
            Check(f.mover.Guides.Sites.Count==6 && f.mover.Guides.GroupCount==3,"Tres opciones con dos manos cada una");
            var visited=new HashSet<int>();
            for(int i=0;i<1000;i++)
            {
                f.mover.Guides.Tick(.01f,default,default,-1,-1,true);
                visited.Add(f.mover.Guides.ActiveGroup);
                if(f.mover.Guides.VisibleHands!=2) throw new Exception("La secuencia satura con más de una pareja o deja huecos.");
            }
            Check(visited.Count==3,"Las tres parejas se muestran por turnos");
            var near=f.At(0,0);
            for(int i=0;i<700;i++) f.mover.Guides.Tick(.01f,near,default,-1,-1,true);
            Check(f.mover.Guides.ActiveGroup==0,"Acercarse detiene el ciclo en la opción elegida");
            f.mover.Guides.Tick(.01f,default,default,-1,-1,false);
            Check(f.mover.Guides.VisibleHands==0,"Ocultar guías elimina todas las manos");
            f.Step(2);
            Check(rag.Parts.All(p=>!p.body.isKinematic),"Todo el cuerpo responde a la gravedad");
            Check(rag.Hip.position.y<.16f,"La pelvis baja por su peso, sin altura impuesta");
            Check(rag.Parts.All(p=>p.collider.bounds.min.y>-.035f),"El suelo sostiene los segmentos del cuerpo");
            f.CheckLengths();
            Check(rag.MaxJointError<.035f,"Las articulaciones permanecen unidas en reposo");

            f.Reset(); f.Step(.2f);
            var left=f.At(0,0); f.Step(.02f,left);
            Check(!f.mover.IsHeld,"La mano abierta no tira del cuerpo");
            left.pose.position+=Vector3.up; left.grip=1; f.Step(.02f,left);
            Check(!f.mover.IsHeld,"No se agarra a distancia");
            left=f.At(0,1); f.Step(.02f,left);
            Check(f.mover.IsHeld && f.mover.HoldingSite(0)==0,"Se agarra el antebrazo izquierdo");
            var before=rag.Hip.position;
            var dragStart=left;
            for(int i=0;i<500;i++)
            {
                left.pose.position=dragStart.pose.position+new Vector3(-i*.003f,.18f,-i*.001f);
                f.Step(.01f,left);
            }
            DragDistance=Vector3.ProjectOnPlane(rag.Hip.position-before,Vector3.up).magnitude;
            Check(DragDistance>.55f,"Tirar del brazo arrastra la pelvis y el tronco");
            Check(rag.MaxJointError<.04f,"El tirón de un brazo no separa la cadena física");
            f.CheckLengths();
            left.grip=0; f.Step(.16f,left);
            Check(!f.mover.IsHeld && rag.IsSimulating,"Al soltar, el cuerpo sigue teniendo peso");
            Check(!f.mover.IsSnapping,"Soltar lejos no teletransporta al destino");

            f.Reset(); f.Step(.1f);
            left=f.At(0,0); var right=f.At(1,0); f.Step(.02f,left,right);
            left=f.At(0,1); right=f.At(1,1); f.Step(.02f,left,right);
            Check(f.mover.HoldingSite(0)==0 && f.mover.HoldingSite(1)==1,"Dos manos pueden sujetar el mismo brazo");
            f.mover.Guides.Tick(.02f,left,right,0,1,true);
            Check(f.mover.Guides.VisibleHands==2,"El agarre mantiene solo su pareja visible");
            f.Step(.3f);
            Check(f.mover.IsHeld && !rag.HasGrip(0) && !rag.HasGrip(1),"La oclusión breve conserva selección sin aplicar tirones a una mano perdida");
            var previousHip=rag.Hip.position;
            left.pose.position+=Vector3.one*2; right.pose.position+=Vector3.one*2;
            f.Step(.01f,left,right);
            Check(Vector3.Distance(rag.Hip.position,previousHip)<.12f,"Reaparición lejana no lanza al paciente");
            f.Step(.7f);
            Check(!f.mover.IsHeld && !f.mover.IsSnapping,"Perder ambas manos no aprueba el destino");
            f.Step(.02f,left,right); Check(!f.mover.IsHeld,"Hay que abrir antes de volver a agarrar");

            f.Reset();
            rag.ResetPose(f.mover.Destination);
            f.Step(.02f);
            left=f.At(0,0); right=f.At(1,0); f.Step(.02f,left,right);
            left=f.At(0,1); right=f.At(1,1); f.Step(.02f,left,right);
            Check(f.mover.IsHeld && f.mover.CanSnap,"La pelvis situada y orientada en el destino habilita el snap");
            left.grip=right.grip=0; f.Step(.16f,left,right);
            Check(f.mover.IsSnapping,"Abrir ambas manos inicia la colocación");
            f.Step(.6f);
            Check(f.mover.IsPlaced && !rag.IsSimulating,"La colocación estabiliza el cuerpo para continuar RCP");
            Check(Vector3.Distance(f.patient.transform.position,f.mover.Destination.position)<.002f,"El paciente encaja en la raíz exacta del holograma");
            f.CheckLengths();
            left=f.At(0,0); f.Step(.02f,left); left=f.At(0,1); f.Step(.02f,left);
            Check(f.mover.IsHeld && rag.Parts.All(p=>!p.body.isKinematic),"Reagarrar activa TODO el ragdoll, también la pelvis");
            f.mover.Suspend();
            Check(!f.mover.IsHeld && rag.Parts.All(p=>p.body.isKinematic),"Pérdida de foco pausa el cuerpo completo");
            f.Step(.02f);
            Check(rag.Parts.All(p=>!p.body.isKinematic),"Recuperar foco no deja un solo hueso fijo");
            f.Reset();
            Check(!rag.IsSimulating && !f.mover.IsHeld && !f.mover.IsPlaced,"Reinicio limpia física e interacción");

            var landed=new List<Vector3>();
            foreach(var direction in new[]{Vector3.right,Vector3.left,Vector3.forward,Vector3.back})
            {
                rag.CollapseStanding(Vector3.zero,direction);
                float hipHeight=rag.Hip.position.y;
                for(int i=0;i<350;i++) { rag.FixedTick(.01f); f.physics.Simulate(.01f); rag.SyncVisuals(); WorstJointError=Mathf.Max(WorstJointError,rag.MaxJointError); }
                Check(rag.Hip.position.y<hipHeight-.55f,"El paciente de pie se desploma por gravedad");
                Check(rag.Parts.All(p=>p.collider.bounds.min.y>-.045f),"La caída conserva contacto con el suelo");
                Check(rag.MaxJointError<.04f,"Las articulaciones no se separan al desplomarse");
                f.CheckLengths(); landed.Add(rag.Hip.position);
            }
            Check(Vector3.Distance(landed[0],landed[1])>.3f && Vector3.Distance(landed[2],landed[3])>.3f,"La caída puede ir en distintas direcciones");
            Check(WorstJointError<.07f,"Los tirones y caídas mantienen el error físico acotado");
        }
        using(var f=new Fixture(false))
            Check(f.mover.Guides.Sites.Count==4 && f.mover.Guides.GroupCount==2,"Las guías de tobillos se pueden excluir según el caso");
        CheckTwoHandLift();
        Debug.Log($"RAGDOLL: {Passed} comprobaciones; desplazamiento pelvis {DragDistance:F3} m; máximo error articular {WorstJointError:F4} m; separación al elevar {LiftError:F4} m; penetración propia {SelfPenetration:F4} m.");
    }

    private static void CheckTwoHandLift()
    {
        foreach(float step in new[]{.02f,1f/72})
        using(var f=new Fixture())
        {
            f.dt=step; f.Step(.4f);
            var rag=f.mover.Ragdoll;
            foreach(var name in new[]{"L_Upperarm","R_Upperarm"})
            {
                var arm=rag.FindPart(name);
                Check(arm.joint.enableCollision && !Physics.GetIgnoreCollision(arm.collider,arm.parent.collider),"Hombro con colisión contra el pecho");
            }
            var right=f.At(0,1); f.Step(step,default,right);
            Check(f.mover.HoldingSite(1)==0,"La derecha puede empezar en la guía que antes estaba reservada a la izquierda");
            var left=f.At(1,1); f.Step(step,left,right);
            Check(f.mover.HoldingSite(0)==1 && f.mover.HoldingSite(1)==0,"La izquierda se añade después al mismo brazo sin soltar la derecha");
            var originL=left.pose.position; var originR=right.pose.position; var hipStart=rag.Hip.position;
            bool bothHeld=true;
            for(float t=0;t<4;t+=step)
            {
                var movement=new Vector3(.2f*t,Mathf.Min(2.1f,t*.7f),0);
                left.pose.position=originL+movement; right.pose.position=originR+movement;
                f.Step(step,left,right);
                bothHeld &= f.mover.GripCount==2;
            }
            Check(bothHeld,"Ambas manos conservan el agarre al levantar el cuerpo");
            f.Step(.5f,left,right);
            LiftError=Mathf.Max(LiftError,rag.GripError(0),rag.GripError(1));
            Debug.Log($"Elevación dt={step:F4}: izquierda={rag.GripError(0):F4} m, derecha={rag.GripError(1):F4} m");
            if(step==.02f) Capture(f,"Ragdoll-dos-manos");
            Check(LiftError<.04f,"Los agarres elevados siguen ambas manos con menos de 4 cm de separación");
            Check(rag.Hip.position.y>hipStart.y+.7f,"Al elevar el brazo también se levanta la pelvis");
            f.CheckLengths();
            right.pose.position+=Vector3.right*.08f;
            f.Step(.2f,default,right);
            Check(f.mover.GripCount==2 && !rag.HasGrip(0) && rag.HasGrip(1),"La derecha sigue tirando durante una oclusión breve de la izquierda");
            left.pose.position+=Vector3.right*.08f;
            f.Step(.5f,left,right);
            Check(f.mover.GripCount==2 && rag.GripError(0)<.04f,"La izquierda recuperada vuelve a su posición real sin guardar un desfase");
            left.grip=0; f.Step(.16f,left,right);
            Check(f.mover.HoldingSite(0)<0 && f.mover.HoldingSite(1)>=0,"Abrir una mano deja la otra agarrada");
            f.Step(.5f,left,right);
            Check(rag.GripError(1)<.06f,"Una mano sostiene el peso sin quedarse separada del brazo");
            right.grip=0; f.Step(3,left,right);
            Check(!f.mover.IsHeld && rag.IsSimulating,"Soltar desde arriba deja caer el cuerpo completo");
            f.CheckLengths();
            if(step==.02f) Capture(f,"Ragdoll-caida");
            // Force each arm across the torso and let collision contacts oppose the push.
            foreach(var side in new[]{"L_","R_"})
            {
                f.Reset(); f.Step(.4f);
                var forearm=rag.FindPart(side+"Forearm"); var chest=rag.FindPart("Spine02");
                for(float t=0;t<1.5f;t+=step)
                {
                    forearm.body.AddForce((chest.collider.bounds.center-forearm.collider.bounds.center).normalized*70);
                    f.Step(step);
                }
                for(float t=0;t<.5f;t+=step)
                {
                    f.Step(step);
                    foreach(var limbName in new[]{"Upperarm","Forearm","Hand"})
                    foreach(var torsoName in new[]{"Hip","Spine01","Spine02"})
                    {
                        var a=rag.FindPart(side+limbName).collider; var b=rag.FindPart(torsoName).collider;
                        if(Physics.ComputePenetration(a,a.transform.position,a.transform.rotation,b,b.transform.position,b.transform.rotation,out _,out float depth))
                            SelfPenetration=Mathf.Max(SelfPenetration,depth);
                    }
                }
                Check(SelfPenetration<.025f,"Los brazos chocan con abdomen y pecho sin atravesarlos");
                Check(rag.Parts.Max(p=>Vector3.Distance(p.bone.position,p.body.position))<.04f,"Los volúmenes físicos siguen los huesos visibles");
                foreach(var name in new[]{"Spine01","Spine02"})
                {
                    var part=rag.FindPart(name);
                    var bind=Quaternion.Inverse(part.parent.rootRotation)*part.rootRotation;
                    var current=Quaternion.Inverse(part.parent.body.rotation)*part.body.rotation;
                    Check(Quaternion.Angle(bind,current)<32,"La cintura y el pecho conservan una flexión limitada durante el contacto");
                }
            }
        }
    }

    private static void Capture(Fixture f,string name)
    {
        if(!Application.isBatchMode || SystemInfo.graphicsDeviceType==UnityEngine.Rendering.GraphicsDeviceType.Null) return;
        // These simulations all execute in one editor frame. Bake the current skin explicitly;
        // a second Camera.Render can otherwise reuse skinning buffers from the earlier snapshot.
        var bakedObjects=new List<GameObject>(); var bakedMeshes=new List<Mesh>();
        var skins=f.patient.GetComponentsInChildren<SkinnedMeshRenderer>().Where(s=>s.enabled).ToArray();
        var report=new List<string>();
        foreach(var skin in skins)
        {
            var mesh=new Mesh(); skin.BakeMesh(mesh); bakedMeshes.Add(mesh);
            var baked=new GameObject("Vista "+skin.name,typeof(MeshFilter),typeof(MeshRenderer));
            SceneManager.MoveGameObjectToScene(baked,f.scene);
            baked.transform.SetPositionAndRotation(skin.transform.position,skin.transform.rotation); baked.transform.localScale=skin.transform.lossyScale;
            baked.GetComponent<MeshFilter>().sharedMesh=mesh;
            baked.GetComponent<MeshRenderer>().sharedMaterials=skin.sharedMaterials;
            bakedObjects.Add(baked); skin.enabled=false;
            report.Add(skin.name+" bounds="+baked.GetComponent<MeshRenderer>().bounds+" bones="+string.Join(",",skin.bones.Select(b=>b==null?"null":b.name)));
        }
        System.IO.File.WriteAllLines(name+"-meshes.txt",report);
        var cameraObject=new GameObject("Cámara de verificación"); SceneManager.MoveGameObjectToScene(cameraObject,f.scene);
        var camera=cameraObject.AddComponent<Camera>();
        var bounds=f.mover.Ragdoll.Hip.GetComponentInChildren<Collider>().bounds;
        foreach(var part in f.mover.Ragdoll.Parts) bounds.Encapsulate(part.collider.bounds);
        camera.transform.position=bounds.center+new Vector3(2.5f,1.3f,2.5f);
        camera.transform.LookAt(bounds.center); camera.nearClipPlane=.03f;
        camera.clearFlags=CameraClearFlags.SolidColor; camera.backgroundColor=new Color(.12f,.16f,.22f);
        camera.fieldOfView=40;
        var lightObject=new GameObject("Luz de verificación"); SceneManager.MoveGameObjectToScene(lightObject,f.scene);
        var light=lightObject.AddComponent<Light>(); light.type=LightType.Directional; light.intensity=1.3f;
        lightObject.transform.rotation=Quaternion.Euler(45,-25,0);
        var fillObject=new GameObject("Luz de relleno"); SceneManager.MoveGameObjectToScene(fillObject,f.scene);
        var fill=fillObject.AddComponent<Light>(); fill.type=LightType.Directional; fill.intensity=.7f;
        fillObject.transform.rotation=Quaternion.Euler(30,155,0);
        var target=new RenderTexture(1400,1000,24); camera.targetTexture=target;
        camera.Render();
        var previous=RenderTexture.active; RenderTexture.active=target;
        var pixels=new Texture2D(target.width,target.height,TextureFormat.RGB24,false);
        pixels.ReadPixels(new Rect(0,0,target.width,target.height),0,0); pixels.Apply();
        System.IO.File.WriteAllBytes(name+".png",pixels.EncodeToPNG());
        RenderTexture.active=previous; camera.targetTexture=null;
        UnityEngine.Object.DestroyImmediate(pixels); UnityEngine.Object.DestroyImmediate(target);
        UnityEngine.Object.DestroyImmediate(cameraObject); UnityEngine.Object.DestroyImmediate(lightObject);
        UnityEngine.Object.DestroyImmediate(fillObject);
        foreach(var skin in skins) skin.enabled=true;
        foreach(var baked in bakedObjects) UnityEngine.Object.DestroyImmediate(baked);
        foreach(var mesh in bakedMeshes) UnityEngine.Object.DestroyImmediate(mesh);
    }
    private static void Check(bool condition,string message)
    {
        if(!condition) throw new InvalidOperationException("Ragdoll: "+message+" [error="+WorstJointError.ToString("F4")+" m, arrastre="+DragDistance.ToString("F3")+" m]");
        Passed++;
    }
}

