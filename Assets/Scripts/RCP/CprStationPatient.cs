using System.Collections.Generic;
using UnityEngine;

namespace FirstAidVR.Rcp
{
    public sealed class CprStationPatient : MonoBehaviour
    {
        [SerializeField] private Transform spine;
        [SerializeField] private Vector3 spineRest;
        [SerializeField] private Transform[] fingers;
        [SerializeField] private Quaternion[] fingerRest;
        [SerializeField] private bool prepared;
        private Transform responsePatient;
        private Vector3 responseOffset,responseOrigin;
        private readonly List<Vector2> contactHeights = new List<Vector2>();
        public Mesh ShoulderHand { get; private set; }
        public float ShoulderMinimumClearance { get; private set; }
        public float ShoulderPalmClearance { get; private set; }
        private void OnDestroy()
        {
            if(ShoulderHand!=null) { if(Application.isPlaying) Destroy(ShoulderHand); else DestroyImmediate(ShoulderHand); }
        }
        public const float MaximumVisualTravel = .014f;
        public static float VisualTravel(float measuredTravel) => Mathf.Clamp(measuredTravel * .18f, 0, MaximumVisualTravel);

        // Hornear solo durante la preparación. El contacto en ejecución sigue muestras de superficie guardadas, no una malla horneada por fotograma.
        public void PrepareContact(Transform patient, Pose chest, CprTrainingSettings settings)
        {
            contactHeights.Clear();
            var normal = chest.rotation * Vector3.up;
            var mesh = new Mesh();
            // Fijar el talón de la palma inferior, no la punta más baja de un dedo doblado de cualquiera de las manos.
            var footprint=new HashSet<Vector3>();
            if(settings.stackedRightHand!=null)
                foreach(var vertex in settings.stackedRightHand.vertices)
                    if(Mathf.Abs(vertex.x)<.032f && Mathf.Abs(vertex.z)<.022f) footprint.Add(vertex);
            if(footprint.Count==0) footprint.Add(Vector3.zero);
            try
            {
                foreach (var skin in patient.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                {
                    if (!skin.enabled || !skin.gameObject.activeInHierarchy
                        || !skin.name.Contains("Basic_T_shirts")) continue;
                    SetCompression(0, normal);
                    skin.BakeMesh(mesh);
                    var rest = mesh.vertices;
                    var triangles = mesh.triangles;
                    // BakeMesh aplica la escala del renderizador; TransformPoint la aplicaría dos veces.
                    for (int i=0;i<rest.Length;i++) rest[i]=skin.transform.position+skin.transform.rotation*rest[i];
                    SetCompression(MaximumVisualTravel / .18f, normal);
                    skin.BakeMesh(mesh);
                    var down=mesh.vertices;
                    for (int i=0;i<down.Length;i++) down[i]=skin.transform.position+skin.transform.rotation*down[i];
                    var inverse=Quaternion.Inverse(chest.rotation);
                    for(int i=0;i<rest.Length;i++)
                    { rest[i]=inverse*(rest[i]-chest.position); down[i]=inverse*(down[i]-chest.position); }
                    float restEnvelope=float.NegativeInfinity, downEnvelope=float.NegativeInfinity;
                    foreach(var point in footprint)
                    {
                        float highest=float.NegativeInfinity, compressed=0;
                        for(int t=0;t<triangles.Length;t+=3)
                        {
                            int a=triangles[t], b=triangles[t+1], c=triangles[t+2];
                            if(!SurfaceWeights(rest[a],rest[b],rest[c],point.x,point.z,out var weights)) continue;
                            float y=weights.x*rest[a].y+weights.y*rest[b].y+weights.z*rest[c].y;
                            if(y>highest) { highest=y; compressed=weights.x*down[a].y+weights.y*down[b].y+weights.z*down[c].y; }
                        }
                        if(!float.IsNegativeInfinity(highest))
                        {
                            restEnvelope=Mathf.Max(restEnvelope,highest-point.y);
                            downEnvelope=Mathf.Max(downEnvelope,compressed-point.y);
                        }
                    }
                    // Interpolar los valores máximos crea una envolvente conservadora de todos los vértices muestreados.
                    if(!float.IsNegativeInfinity(restEnvelope)) contactHeights.Add(new Vector2(restEnvelope,downEnvelope));
                }
            }
            finally { SetCompression(0,normal); if(Application.isPlaying) Destroy(mesh); else DestroyImmediate(mesh); }
        }

        public Pose AuthoredShoulderSurface(Transform patient, Transform guide, Mesh hand)
        {
            var pose = new Pose(guide.position, guide.rotation);
            if (hand == null) return pose;
            // Conservar la pose y la malla editadas. Eliminar solo el espacio en la dirección vertical del mundo;
            // la guía guardada podría dejar de hacer contacto después de mover al paciente.
            var normal = Vector3.up;
            float correction = float.NegativeInfinity;
            var probe = new GameObject("Contacto de mano manual").AddComponent<MeshCollider>();
            var baked = new Mesh();
            try
            {
                var points = hand.vertices;
                foreach (var skin in patient.GetComponentsInChildren<SkinnedMeshRenderer>())
                {
                    if (!skin.enabled || (!skin.name.Contains("Body") && !skin.name.Contains("shirts"))) continue;
                    skin.BakeMesh(baked);
                    var vertices = baked.vertices;
                    for (int i = 0; i < vertices.Length; i++)
                        vertices[i] = skin.transform.position + skin.transform.rotation * vertices[i];
                    baked.vertices = vertices; baked.RecalculateBounds();
                    probe.sharedMesh = null; probe.sharedMesh = baked;
                    foreach (var point in points)
                    {
                        var world = guide.TransformPoint(point);
                        if (probe.Raycast(new Ray(world + normal * .35f, -normal), out var hit, .7f))
                            correction = Mathf.Max(correction, Vector3.Dot(hit.point - world, normal));
                    }
                }
                if (!float.IsNegativeInfinity(correction)) pose.position += normal * (correction + .002f);
                return pose;
            }
            finally
            {
                probe.sharedMesh = null;
                if (Application.isPlaying) { Destroy(probe.gameObject); Destroy(baked); }
                else { DestroyImmediate(probe.gameObject); DestroyImmediate(baked); }
            }
        }

        public Pose ShoulderSurface(Transform patient,Pose shoulder,Mesh hand)
        {
            if(hand==null) return shoulder;
            var normal=shoulder.rotation*Vector3.up;
            float lift=float.NegativeInfinity;
            var handVertices=hand.vertices;
            var surfaces=new float[handVertices.Length];
            for(int i=0;i<surfaces.Length;i++) surfaces[i]=float.NegativeInfinity;
            var probe=new GameObject("Superficie temporal de hombro");
            var collider=probe.AddComponent<MeshCollider>(); var mesh=new Mesh();
            try
            {
                foreach(var skin in patient.GetComponentsInChildren<SkinnedMeshRenderer>())
                {
                    if(!skin.enabled || (!skin.name.Contains("Body") && !skin.name.Contains("shirts"))) continue;
                    skin.BakeMesh(mesh); var vertices=mesh.vertices;
                    for(int i=0;i<vertices.Length;i++) vertices[i]=skin.transform.position+skin.transform.rotation*vertices[i];
                    mesh.vertices=vertices; mesh.RecalculateBounds(); collider.sharedMesh=null; collider.sharedMesh=mesh;
                    for(int i=0;i<handVertices.Length;i++)
                    {
                        var point=handVertices[i];
                        var world=shoulder.position+shoulder.rotation*point;
                        if(collider.Raycast(new Ray(world+normal*.35f,-normal),out var hit,.7f))
                        {
                            float surface=Vector3.Dot(hit.point-shoulder.position,normal);
                            surfaces[i]=Mathf.Max(surfaces[i],surface);
                            if(Mathf.Abs(point.x)<.032f && Mathf.Abs(point.z)<.03f)
                                lift=Mathf.Max(lift,surface-point.y);
                        }
                    }
                }
                // Ajustar un único plano tangente a la palma. Conservar la malla de la mano y las proporciones
                // de los dedos; ajustar cada vértice por separado produciría pliegues en la piel.
                float n=0,sx=0,sz=0,sy=0,sxx=0,szz=0,sxz=0,sxy=0,szy=0;
                for(int i=0;i<handVertices.Length;i++)
                {
                    var v=handVertices[i]; if(float.IsNegativeInfinity(surfaces[i]) || Mathf.Abs(v.x)>.028f || v.z<.025f || v.z>.075f) continue;
                    float y=surfaces[i]; n++; sx+=v.x; sz+=v.z; sy+=y; sxx+=v.x*v.x; szz+=v.z*v.z; sxz+=v.x*v.z; sxy+=v.x*y; szy+=v.z*y;
                }
                float ax=0,az=0;
                if(n>3)
                {
                    float xx=sxx-sx*sx/n,zz=szz-sz*sz/n,xz=sxz-sx*sz/n,xy=sxy-sx*sy/n,zy=szy-sz*sy/n;
                    float den=xx*zz-xz*xz;
                    if(Mathf.Abs(den)>.000000001f) { ax=(xy*zz-zy*xz)/den; az=(zy*xx-xy*xz)/den; }
                }
                var tilt=Quaternion.FromToRotation(Vector3.up,new Vector3(-ax,1,-az).normalized);
                var fitted=(Vector3[])handVertices.Clone();
                float contact=float.NegativeInfinity;
                for(int i=0;i<fitted.Length;i++)
                {
                    fitted[i]=tilt*handVertices[i];
                    if(Mathf.Abs(handVertices[i].x)<.035f && Mathf.Abs(handVertices[i].z)<.045f)
                        contact=Mathf.Max(contact,(n>0?sy/n:0)+ax*(fitted[i].x-(n>0?sx/n:0))+az*(fitted[i].z-(n>0?sz/n:0))-fitted[i].y);
                }
                // Resolver el contacto con la superficie curva real después de inclinar, no con el
                // plano tangente aproximado (que podría dejar un espacio visible).
                contact=float.NegativeInfinity;
                var clearance=new float[fitted.Length];
                for(int i=0;i<clearance.Length;i++) clearance[i]=float.NegativeInfinity;
                foreach(var skin in patient.GetComponentsInChildren<SkinnedMeshRenderer>())
                {
                    if(!skin.enabled || (!skin.name.Contains("Body") && !skin.name.Contains("shirts"))) continue;
                    skin.BakeMesh(mesh); var vertices=mesh.vertices;
                    for(int i=0;i<vertices.Length;i++) vertices[i]=skin.transform.position+skin.transform.rotation*vertices[i];
                    mesh.vertices=vertices; mesh.RecalculateBounds(); collider.sharedMesh=null; collider.sharedMesh=mesh;
                    for(int i=0;i<fitted.Length;i++)
                    {
                        var world=shoulder.position+shoulder.rotation*fitted[i];
                        if(collider.Raycast(new Ray(world+normal*.35f,-normal),out var hit,.7f))
                        {
                            clearance[i]=Mathf.Max(clearance[i],Vector3.Dot(hit.point-world,normal));
                            if(Mathf.Abs(handVertices[i].x)<.028f && handVertices[i].z>=.025f && handVertices[i].z<=.075f)
                                contact=Mathf.Max(contact,clearance[i]);
                        }
                    }
                }
                if(float.IsNegativeInfinity(contact)) contact=0;
                // Envolvente suave de separación: mantener los dedos fuera de la camiseta sin elevar
                // toda la palma. Los vértices cercanos reciben la misma corrección continua.
                var corrected=(Vector3[])fitted.Clone();
                for(int i=0;i<fitted.Length;i++)
                {
                    float liftFinger=0;
                    for(int j=0;j<fitted.Length;j++)
                    {
                        float distance=new Vector2(fitted[i].x-fitted[j].x,fitted[i].z-fitted[j].z).magnitude;
                        liftFinger=Mathf.Max(liftFinger,clearance[j]-contact-distance*.7f);
                    }
                    corrected[i].y+=liftFinger;
                }
                ShoulderMinimumClearance=ShoulderPalmClearance=float.PositiveInfinity;
                for(int i=0;i<fitted.Length;i++)
                {
                    if(float.IsNegativeInfinity(clearance[i])) continue;
                    float gap=contact+.002f+corrected[i].y-fitted[i].y-clearance[i];
                    ShoulderMinimumClearance=Mathf.Min(ShoulderMinimumClearance,gap);
                    if(Mathf.Abs(handVertices[i].x)<.028f && handVertices[i].z>=.025f && handVertices[i].z<=.075f)
                        ShoulderPalmClearance=Mathf.Min(ShoulderPalmClearance,gap);
                }
                fitted=corrected;
                if(ShoulderHand!=null) { if(Application.isPlaying) Destroy(ShoulderHand); else DestroyImmediate(ShoulderHand); }
                ShoulderHand=Instantiate(hand); ShoulderHand.name="Mano apoyada sobre hombro";
                ShoulderHand.vertices=fitted; ShoulderHand.RecalculateNormals(); ShoulderHand.RecalculateBounds();
                shoulder.position+=normal*(contact+.002f);
                return shoulder;
            }
            finally
            {
                collider.sharedMesh=null; probe.SetActive(false);
                if(Application.isPlaying) { Destroy(probe); Destroy(mesh); }
                else { DestroyImmediate(probe); DestroyImmediate(mesh); }
            }
        }

        private static bool SurfaceWeights(Vector3 a,Vector3 b,Vector3 c,float x,float z,out Vector3 weights)
        {
            float den=(b.z-c.z)*(a.x-c.x)+(c.x-b.x)*(a.z-c.z);
            weights=default;
            if(Mathf.Abs(den)<.00000001f) return false;
            float u=((b.z-c.z)*(x-c.x)+(c.x-b.x)*(z-c.z))/den;
            float v=((c.z-a.z)*(x-c.x)+(a.x-c.x)*(z-c.z))/den;
            weights=new Vector3(u,v,1-u-v);
            return u>=0 && v>=0 && u+v<=1;
        }

        public Pose ContactPose(Pose chest,float measuredTravel)
        {
            // Si no se puede muestrear la camiseta, conservar la superficie en reposo para evitar hundirse en el torso.
            float height=0;
            if(contactHeights.Count>0)
            {
                height=float.NegativeInfinity;
                float amount=VisualTravel(measuredTravel)/MaximumVisualTravel;
                foreach(var pair in contactHeights) height=Mathf.Max(height,Mathf.Lerp(pair.x,pair.y,amount));
            }
            chest.position+=chest.rotation*Vector3.up*(height+.003f);
            return chest;
        }

        public void Prepare(Transform patient, CprTrainingSettings settings)
        {
            responsePatient=patient;
            foreach (var animator in patient.GetComponentsInChildren<Animator>(true)) animator.enabled = false;
            foreach (var body in patient.GetComponentsInChildren<Rigidbody>(true)) { body.isKinematic = true; body.useGravity = false; }
            var bones = patient.GetComponentsInChildren<Transform>(true);
            if (!prepared)
            {
                var found = new List<Transform>(); var rotations = new List<Quaternion>();
                foreach (var bone in bones)
                {
                    if (bone.name == "CC_Base_Spine02") { spine = bone; spineRest = bone.localPosition; }
                    if (bone.name.StartsWith("CC_Base_") && (bone.name.Contains("Index") || bone.name.Contains("Mid")
                        || bone.name.Contains("Ring") || bone.name.Contains("Pinky") || bone.name.Contains("Thumb"))
                        && (bone.name.EndsWith("1") || bone.name.EndsWith("2") || bone.name.EndsWith("3")))
                    { found.Add(bone); rotations.Add(bone.localRotation); }
                }
                fingers = found.ToArray(); fingerRest = rotations.ToArray(); prepared = true;
            }
            for (int i = 0; i < fingers.Length; i++)
            {
                var finger = fingers[i]; if (finger == null) continue;
                float curl = finger.name.Contains("Thumb") ? settings.relaxedThumbCurl : settings.relaxedFingerCurl;
                if (finger.name.EndsWith("2")) curl *= 1.25f;
                if (finger.name.EndsWith("3")) curl = finger.name.Contains("Thumb") ? settings.relaxedThumbCurl * 1.35f : settings.relaxedFingerCurlDistal;
                finger.localRotation = fingerRest[i] * Quaternion.AngleAxis(-curl, Vector3.right);
            }
            var skins = patient.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            bool shirt = false, jeans = false;
            foreach (var skin in skins)
            {
                shirt |= skin.enabled && skin.gameObject.activeInHierarchy && skin.name.Contains("Basic_T_shirts");
                jeans |= skin.enabled && skin.gameObject.activeInHierarchy && skin.name.Contains("Slim_Jeans");
            }
            if (shirt && jeans && settings.clothedPatientBody != null)
                foreach (var skin in skins) if (skin.name == "CC_Base_Body") skin.sharedMesh = settings.clothedPatientBody;
            SetCompression(0, Vector3.up);
        }

        public void SetCompression(float measuredTravel, Vector3 normal)
        {
            // Pequeña respuesta ilustrativa del pecho, compartida por la piel y la ropa. Nunca se usa como medición.
            if (spine == null) return;
            spine.localPosition = spineRest - spine.parent.InverseTransformVector(normal * VisualTravel(measuredTravel));
        }
        public void SetShoulderMotion(Vector3 offset)
        {
            var next=Vector3.ClampMagnitude(offset,.008f);
            if(responsePatient!=null)
            {
                if(responseOffset==Vector3.zero) responseOrigin=responsePatient.position;
                responsePatient.position=responseOrigin+next;
            }
            responseOffset=next;
        }
        public void RestorePose()
        {
            SetShoulderMotion(Vector3.zero);
            if(fingers!=null) for(int i=0;i<fingers.Length;i++) if(fingers[i]!=null) fingers[i].localRotation=fingerRest[i];
            if(spine!=null) spine.localPosition=spineRest;
        }
        private void OnDisable() { SetShoulderMotion(Vector3.zero); if (spine != null) spine.localPosition = spineRest; }
    }
}
