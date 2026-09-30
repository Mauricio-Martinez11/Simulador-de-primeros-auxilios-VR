using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace FirstAidVR.Rcp
{
    /// <summary>
    /// Unit-scale physics rig independent of the FBX hierarchy. Only rotations are transferred
    /// to limb bones; the root follows the dynamic hip. No bone translation or scaling can stretch the skin.
    /// </summary>
    public sealed class CprPatientRagdoll : MonoBehaviour
    {
        public sealed class Part
        {
            public string name;
            public Transform bone;
            public Rigidbody body;
            public Collider collider;
            public ConfigurableJoint joint;
            public Part parent;
            public Vector3 rootPosition;
            public Quaternion rootRotation;
        }
        private sealed class Grip
        {
            public Part part;
            public Vector3 anchor, target, filtered;
            public Rigidbody handle;
            public ConfigurableJoint joint;
        }
        public IReadOnlyList<Part> Parts => parts;
        public bool IsSimulating { get; private set; }
        public Transform PhysicsRoot => physicsRoot;
        public Rigidbody Hip => parts.Count == 0 ? null : parts[0].body;
        public float MaxJointError
        {
            get
            {
                float error = 0;
                foreach (var part in parts) if (part.joint != null)
                    error = Mathf.Max(error, Vector3.Distance(part.body.transform.TransformPoint(part.joint.anchor),
                        part.parent.body.transform.TransformPoint(part.joint.connectedAnchor)));
                return error;
            }
        }

        private readonly List<Part> parts = new List<Part>();
        private readonly Dictionary<string, Part> byName = new Dictionary<string, Part>();
        private readonly Grip[] grips = new Grip[2];
        private CprTrainingSettings settings;
        private Transform patient, physicsRoot;
        private Transform[] skeleton;
        private Vector3[] localPositions, localScales;
        private Quaternion[] localRotations;
        private Animator[] animators;
        private bool[] animatorStates, rendererStates;
        private SkinnedMeshRenderer[] skins;
        private Mesh[] originalSkinMeshes;
        private Collider[] originalColliders;
        private bool[] colliderStates;
        private PhysicMaterial friction;
        private Pose initialRoot;
        private bool initialized;
        private Pose[] snapFrom;
        private bool suspendedSimulation;

        public bool Initialize(CprTrainingSettings config, Transform root)
        {
            if (initialized) return true;
            settings = config; patient = root; initialRoot = new Pose(root.position, root.rotation);
            skeleton = patient.GetComponentsInChildren<Transform>(true);
            localPositions = new Vector3[skeleton.Length]; localRotations = new Quaternion[skeleton.Length]; localScales = new Vector3[skeleton.Length];
            for (int i = 0; i < skeleton.Length; i++)
            { localPositions[i] = skeleton[i].localPosition; localRotations[i] = skeleton[i].localRotation; localScales[i] = skeleton[i].localScale; }
            string[] required = { "Hip", "Spine01", "Spine02", "Head", "L_Upperarm", "L_Forearm", "L_Hand",
                "R_Upperarm", "R_Forearm", "R_Hand", "L_Thigh", "L_Calf", "L_Foot", "R_Thigh", "R_Calf", "R_Foot" };
            foreach (string name in required) if (Bone(name) == null)
            { Debug.LogError("Ragdoll: falta CC_Base_" + name + ". No se activa un cuerpo parcialmente conectado.", this); return false; }
            if (patient.GetComponentsInChildren<Rigidbody>(true).Length > 0)
            { Debug.LogError("Ragdoll: el modelo ya tiene Rigidbody. Retira el rig anterior para evitar dos sistemas físicos superpuestos.", this); return false; }
            animators = patient.GetComponentsInChildren<Animator>(true); animatorStates = new bool[animators.Length];
            for (int i = 0; i < animators.Length; i++) { animatorStates[i] = animators[i].enabled; animators[i].enabled = false; }
            skins = patient.GetComponentsInChildren<SkinnedMeshRenderer>(true); rendererStates = new bool[skins.Length];
            originalSkinMeshes = new Mesh[skins.Length];
            for (int i = 0; i < skins.Length; i++)
            {
                rendererStates[i] = skins[i].updateWhenOffscreen;
                originalSkinMeshes[i] = skins[i].sharedMesh;
                skins[i].updateWhenOffscreen = true;
            }
            SuppressSkinUnderClothes();
            originalColliders = patient.GetComponentsInChildren<Collider>(true); colliderStates = new bool[originalColliders.Length];
            for (int i = 0; i < originalColliders.Length; i++) { colliderStates[i] = originalColliders[i].enabled; originalColliders[i].enabled = false; }
            physicsRoot = new GameObject("Paciente · articulaciones físicas").transform;
            SceneManager.MoveGameObjectToScene(physicsRoot.gameObject, patient.gameObject.scene);
            friction = new PhysicMaterial("Paciente · fricción de arrastre")
            { dynamicFriction = settings.patientFriction, staticFriction = settings.patientFriction + .05f,
                frictionCombine = PhysicMaterialCombine.Minimum, bounciness = 0, bounceCombine = PhysicMaterialCombine.Minimum };
            var hip = Add("Hip", null, 10);
            var spine = Add("Spine01", hip, 10);
            var chest = Add("Spine02", spine, 12);
            var head = Add("Head", chest, 4);
            Box(hip, hip.bone.position + patient.up * .005f + patient.forward * .012f, new Vector3(.27f,.18f,.19f));
            Box(spine, spine.bone.position + patient.up * .025f + patient.forward * .012f, new Vector3(.26f,.25f,.22f));
            Box(chest, chest.bone.position + patient.up * .13f + patient.forward * .025f, new Vector3(.32f,.29f,.235f));
            Sphere(head, head.bone.position + patient.up * .075f + patient.forward * .025f, .105f);
            Limits(spine, patient.right, -12, 16, 10, 10);
            Limits(chest, patient.right, -10, 12, 8, 10);
            Limits(head, patient.right, -40, 40, 45, 30);
            foreach (string side in new[] { "L_", "R_" })
            {
                var arm = Add(side + "Upperarm", chest, 3);
                var forearm = Add(side + "Forearm", arm, 2);
                var hand = Add(side + "Hand", forearm, 1);
                var thigh = Add(side + "Thigh", hip, 6);
                var calf = Add(side + "Calf", thigh, 4);
                var foot = Add(side + "Foot", calf, 2);
                Capsule(arm, forearm.bone.position, .052f);
                Capsule(forearm, hand.bone.position, .045f);
                Capsule(hand, hand.bone.position + (hand.bone.position-forearm.bone.position).normalized * .17f, .035f);
                Capsule(thigh, calf.bone.position, .079f);
                Capsule(calf, foot.bone.position, .058f);
                var toe = Bone(side + "ToeBase");
                Capsule(foot, toe != null ? toe.position : foot.bone.position + patient.forward * .15f, .048f);
                var upperAxis = (forearm.bone.position-arm.bone.position).normalized;
                var forearmAxis = (hand.bone.position-forearm.bone.position).normalized;
                var elbowAxis = Vector3.Cross(forearmAxis, patient.forward).normalized;
                // Shoulder X follows the humerus: limit axial twist independently of its swing.
                // A shared patient-wide axis mixed bending with twist and wrung the upper-arm skin.
                Limits(arm, upperAxis, -35, 35, 100, 75, patient.forward);
                // A small extension margin absorbs solver error on impact before hyperextension.
                Limits(forearm, elbowAxis, 5, 115, 0, 0, forearmAxis);
                // Conservative passive wrist range for this interaction, not maximum clinical ROM.
                // Keep a stable flexion hinge: an unconstrained sideways wrist swing can collapse
                // under a floor impact. Forearm/shoulder motion carries changes of direction.
                Limits(hand, elbowAxis, -20, 25, 0, 0, forearmAxis);
                PassiveResistance(arm, 3, 3, 10);
                PassiveResistance(forearm, 5, 2.5f, 10);
                forearm.joint.targetRotation=Quaternion.AngleAxis(-8,Vector3.right);
                PassiveResistance(hand, 20, 3, 6);
                hand.joint.projectionAngle=forearm.joint.projectionAngle=1;
                // A small distal collider has very low rotational inertia. Increase it without
                // changing hand/body weight so a floor impulse cannot spin the wrist in one step.
                hand.body.ResetInertiaTensor();
                hand.body.inertiaTensor*=4;
                hand.body.solverIterations=64;
                hand.body.solverVelocityIterations=16;
                foreach(var segment in new[]{arm,forearm})
                {
                    segment.body.ResetInertiaTensor(); segment.body.inertiaTensor*=2;
                    segment.body.solverIterations=64; segment.body.solverVelocityIterations=16;
                }
                Limits(thigh, patient.right, -100, 70, 45, 55);
                var kneeAxis = Vector3.Cross(foot.bone.position-calf.bone.position, -patient.forward).normalized;
                Limits(calf, kneeAxis, -5, 140, 0, 0);
                Limits(foot, kneeAxis, -40, 40, 18, 18);
            }
            foreach (var part in parts)
            {
                if (part.parent == null) continue;
                // Upper arms must collide with the chest even though the shoulder connects them.
                // Other adjacent volumes overlap at their hinge; every non-adjacent pair collides.
                bool shoulder = part.name.EndsWith("Upperarm", StringComparison.Ordinal);
                part.joint.enableCollision = shoulder;
                Physics.IgnoreCollision(part.collider, part.parent.collider, !shoulder);
            }
            // Retarget in skeletal hierarchy order, independent of left/right construction order.
            parts.Sort((a,b) => Array.IndexOf(skeleton,a.bone).CompareTo(Array.IndexOf(skeleton,b.bone)));
            initialized = true;
            return true;
        }

        public Transform Bone(string shortName)
        {
            foreach (var bone in skeleton) if (bone.name == "CC_Base_" + shortName) return bone;
            return null;
        }
        public Part FindPart(string name) => byName.TryGetValue(name, out var part) ? part : null;
        private Part Add(string name, Part parent, float mass)
        {
            var bone = Bone(name);
            var go = new GameObject("Física " + name);
            go.transform.SetParent(physicsRoot, false); go.transform.SetPositionAndRotation(bone.position, bone.rotation);
            var body = go.AddComponent<Rigidbody>();
            body.isKinematic = true; body.useGravity = true; body.mass = mass * settings.patientMass / 72;
            body.drag = .12f; body.angularDrag = .35f;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
            body.solverIterations = 32; body.solverVelocityIterations = 12;
            body.maxDepenetrationVelocity = 1.5f; body.maxAngularVelocity = 9;
            var part = new Part { name = name, bone = bone, body = body, parent = parent,
                rootPosition = patient.InverseTransformPoint(bone.position), rootRotation = Quaternion.Inverse(patient.rotation) * bone.rotation };
            parts.Add(part); byName.Add(name, part);
            return part;
        }
        private Transform ColliderFrame(Part part, Vector3 center, Quaternion rotation)
        {
            var go = new GameObject("Volumen " + part.name);
            go.transform.SetParent(part.body.transform, false); go.transform.SetPositionAndRotation(center, rotation);
            return go.transform;
        }
        private void Capsule(Part part, Vector3 end, float radius)
        {
            var delta = end - part.bone.position;
            var frame = ColliderFrame(part, part.bone.position + delta * .5f, Quaternion.FromToRotation(Vector3.up, delta.normalized));
            var shape = frame.gameObject.AddComponent<CapsuleCollider>();
            shape.radius = radius; shape.height = Mathf.Max(radius*2, delta.magnitude + radius*.5f); shape.direction = 1;
            shape.sharedMaterial = friction; part.collider = shape;
        }
        private void Box(Part part, Vector3 center, Vector3 size)
        {
            var shape = ColliderFrame(part, center, patient.rotation).gameObject.AddComponent<BoxCollider>();
            shape.size = size; shape.sharedMaterial = friction; part.collider = shape;
        }
        private void Sphere(Part part, Vector3 center, float radius)
        {
            var shape = ColliderFrame(part, center, Quaternion.identity).gameObject.AddComponent<SphereCollider>();
            shape.radius = radius; shape.sharedMaterial = friction; part.collider = shape;
        }
        private void Limits(Part part, Vector3 flexAxis, float low, float high, float twist, float swing,
            Vector3 secondaryDirection = default)
        {
            var joint = part.body.gameObject.AddComponent<ConfigurableJoint>(); part.joint = joint;
            joint.connectedBody = part.parent.body; joint.autoConfigureConnectedAnchor = false;
            joint.anchor = Vector3.zero; joint.connectedAnchor = part.parent.body.transform.InverseTransformPoint(part.body.position);
            var secondary = Vector3.ProjectOnPlane(secondaryDirection.sqrMagnitude>.5f?secondaryDirection:patient.up, flexAxis).normalized;
            if (secondary.sqrMagnitude < .5f) secondary = Vector3.ProjectOnPlane(patient.forward,flexAxis).normalized;
            joint.axis = part.body.transform.InverseTransformDirection(flexAxis);
            joint.secondaryAxis = part.body.transform.InverseTransformDirection(secondary);
            joint.xMotion = joint.yMotion = joint.zMotion = ConfigurableJointMotion.Locked;
            joint.angularXMotion = ConfigurableJointMotion.Limited;
            joint.angularYMotion = twist == 0 ? ConfigurableJointMotion.Locked : ConfigurableJointMotion.Limited;
            joint.angularZMotion = swing == 0 ? ConfigurableJointMotion.Locked : ConfigurableJointMotion.Limited;
            // Our ranges describe the child's anatomical rotation relative to its parent.
            // ConfigurableJoint's X limit frame measures the opposite relative rotation.
            // Without this conversion an elbow/knee allowed its large bend backwards.
            joint.lowAngularXLimit = new SoftJointLimit { limit = -high, contactDistance = 4 };
            joint.highAngularXLimit = new SoftJointLimit { limit = -low, contactDistance = 4 };
            joint.angularYLimit = new SoftJointLimit { limit = twist, contactDistance = 4 };
            joint.angularZLimit = new SoftJointLimit { limit = swing, contactDistance = 4 };
            joint.enablePreprocessing = false; joint.enableCollision = false;
            joint.projectionMode = JointProjectionMode.PositionAndRotation;
            joint.projectionDistance = .004f; joint.projectionAngle = 4;
        }

        private static void PassiveResistance(Part part, float spring, float damping, float maxTorque)
        {
            // Resist flopping around the neutral relative pose through the physics solver.
            // XYAndZ also works for the elbow, whose other two angular axes are locked.
            var joint=part.joint;
            joint.enablePreprocessing=true;
            joint.rotationDriveMode=RotationDriveMode.XYAndZ;
            joint.targetRotation=Quaternion.identity;
            var drive=new JointDrive { positionSpring=spring, positionDamper=damping, maximumForce=maxTorque };
            joint.angularXDrive=joint.angularYZDrive=drive;
        }

        public void Activate()
        {
            if (!initialized || IsSimulating) return;
            suspendedSimulation = false; IsSimulating = true;
            foreach (var part in parts) { part.body.isKinematic = false; part.body.WakeUp(); }
        }
        public void Freeze()
        {
            IsSimulating = false;
            foreach (var part in parts)
            {
                if (!part.body.isKinematic) { part.body.velocity = Vector3.zero; part.body.angularVelocity = Vector3.zero; }
                part.body.isKinematic = true;
            }
        }
        public void Suspend()
        {
            ReleaseGrip(0); ReleaseGrip(1);
            if(snapFrom!=null)
            {
                // A cancelled placement resumes from a coherent, fixed-length skeletal pose.
                SyncVisuals();
                foreach(var part in parts) { part.body.position=part.bone.position; part.body.rotation=part.bone.rotation; }
                snapFrom=null;
            }
            if (IsSimulating) { suspendedSimulation = true; Freeze(); }
        }
        public void Resume() { if (suspendedSimulation) Activate(); }
        public bool HasGrip(int hand) => grips[hand] != null;
        public float GripError(int hand) => grips[hand] == null ? 0 :
            Vector3.Distance(grips[hand].part.body.transform.TransformPoint(grips[hand].anchor), grips[hand].target);
        public void BeginGrip(int hand, Part part, Vector3 worldPoint)
        {
            if (part == null) return;
            ReleaseGrip(hand);
            Activate();
            var go = new GameObject(hand == 0 ? "Agarre físico izquierdo" : "Agarre físico derecho");
            go.transform.SetParent(physicsRoot, false);
            go.transform.position = worldPoint;
            var handle = go.AddComponent<Rigidbody>();
            handle.isKinematic = true; handle.useGravity = false;
            var joint = part.body.gameObject.AddComponent<ConfigurableJoint>();
            joint.connectedBody = handle; joint.autoConfigureConnectedAnchor = false;
            joint.anchor = part.body.transform.InverseTransformPoint(worldPoint);
            joint.connectedAnchor = Vector3.zero;
            joint.xMotion = joint.yMotion = joint.zMotion = ConfigurableJointMotion.Free;
            joint.angularXMotion = joint.angularYMotion = joint.angularZMotion = ConfigurableJointMotion.Free;
            var drive = new JointDrive { positionSpring = settings.grabSpring,
                positionDamper = settings.grabDamping, maximumForce = settings.grabMaxForce };
            joint.xDrive = joint.yDrive = joint.zDrive = drive;
            joint.enableCollision = false;
            grips[hand] = new Grip { part=part, anchor=joint.anchor, target=worldPoint, filtered=worldPoint,
                handle=handle, joint=joint };
        }
        public void SetGripTarget(int hand, Vector3 worldPoint) { if(grips[hand]!=null) grips[hand].target=worldPoint; }
        public void ReleaseGrip(int hand)
        {
            var grip = grips[hand];
            if (grip == null) return;
            // Destroy is deferred in Play Mode; zero the drives immediately before removing the joint.
            grip.joint.xDrive = grip.joint.yDrive = grip.joint.zDrive = new JointDrive();
            Dispose(grip.joint); Dispose(grip.handle.gameObject); grips[hand]=null;
        }
        public void FixedTick(float dt)
        {
            if (!IsSimulating) return;
            foreach (var grip in grips)
            {
                if (grip == null) continue;
                // A solver drive couples the hand to the entire articulated mass. A capped force
                // applied only to the forearm used to leave the patient far behind when lifted.
                grip.filtered = Vector3.MoveTowards(grip.filtered, grip.target, settings.grabTargetSpeed * dt);
                grip.handle.MovePosition(grip.filtered);
            }
            foreach (var part in parts) part.body.velocity = Vector3.ClampMagnitude(part.body.velocity, 6);
        }
        private void FixedUpdate() { FixedTick(Time.fixedDeltaTime); }
        private void LateUpdate() { SyncVisuals(); }

        public void SyncVisuals()
        {
            if (!initialized) return;
            // Restore bind offsets before transferring rotations. Even a stressed solver cannot elongate a bone.
            for (int i = 1; i < skeleton.Length; i++)
            { skeleton[i].localPosition=localPositions[i]; skeleton[i].localScale=localScales[i]; skeleton[i].localRotation=localRotations[i]; }
            var hip=FindPart("Hip");
            patient.rotation=hip.body.rotation*Quaternion.Inverse(hip.rootRotation);
            patient.position=hip.body.position-patient.TransformVector(hip.rootPosition);
            foreach (var part in parts) part.bone.rotation=part.body.rotation;
            ApplyRelaxedHands();
        }

        private void ApplyRelaxedHands()
        {
            // Finger bones are deliberately not physics bodies: keeping their bind offsets and
            // adding this small local curl gives a passive hand without introducing 20 extra
            // joints that could snag on clothing or the floor. The FBX finger chains use local Y
            // as their length axis, so local X is the anatomical curl axis for both sides.
            foreach (var side in new[] { "L_", "R_" })
            {
                foreach (var finger in new[] { "Index", "Mid", "Ring", "Pinky" })
                {
                    RelaxFinger(side + finger + "1", settings.relaxedFingerCurl);
                    RelaxFinger(side + finger + "2", settings.relaxedFingerCurl * 1.25f);
                    RelaxFinger(side + finger + "3", settings.relaxedFingerCurlDistal);
                }
                RelaxFinger(side + "Thumb1", settings.relaxedThumbCurl);
                RelaxFinger(side + "Thumb2", settings.relaxedThumbCurl * 1.25f);
                RelaxFinger(side + "Thumb3", settings.relaxedThumbCurl * 1.35f);
            }
        }

        private void RelaxFinger(string shortName, float degrees)
        {
            var finger = Bone(shortName);
            if (finger == null) return;
            int index = Array.IndexOf(skeleton, finger);
            if (index < 0) return;
            finger.localRotation = localRotations[index] * Quaternion.AngleAxis(-degrees, Vector3.right);
        }

        private void SuppressSkinUnderClothes()
        {
            // Use a mesh baked in the editor: the FBX is not CPU-readable in an Android player.
            // Never remove materials to hide skin; null material slots render the error shader
            // (magenta) on Quest. The baked mesh retains all material slots and bone weights.
            bool hasShirt = false, hasJeans = false;
            foreach (var skin in skins)
            {
                if (skin == null || !skin.enabled || !skin.gameObject.activeInHierarchy) continue;
                hasShirt |= skin.name.IndexOf("Basic_T_shirts", StringComparison.OrdinalIgnoreCase) >= 0;
                hasJeans |= skin.name.IndexOf("Slim_Jeans", StringComparison.OrdinalIgnoreCase) >= 0;
            }
            if (!hasShirt || !hasJeans || settings.clothedPatientBody == null) return;

            foreach (var skin in skins)
            {
                if (skin != null && skin.name == "CC_Base_Body")
                    skin.sharedMesh = settings.clothedPatientBody;
            }
        }

        public void BeginSnap()
        {
            ReleaseGrip(0); ReleaseGrip(1); Freeze();
            snapFrom = new Pose[parts.Count];
            for (int i=0;i<parts.Count;i++) snapFrom[i]=new Pose(parts[i].body.position,parts[i].body.rotation);
        }
        public void SnapTo(Pose destination,float t)
        {
            if(snapFrom==null) return;
            for(int i=0;i<parts.Count;i++)
            {
                var part=parts[i];
                var position=destination.position+destination.rotation*Vector3.Scale(patient.lossyScale,part.rootPosition);
                var rotation=destination.rotation*part.rootRotation;
                part.body.position=Vector3.Lerp(snapFrom[i].position,position,t);
                part.body.rotation=Quaternion.Slerp(snapFrom[i].rotation,rotation,t);
            }
            SyncVisuals();
            if(t>=1) snapFrom=null;
        }
        public void ResetPose(Pose rootPose)
        {
            suspendedSimulation=false; BeginSnap(); SnapTo(rootPose,1); snapFrom=null;
        }
        public void ResetPatient() { ResetPose(initialRoot); }
        public void CollapseStanding(Vector3 floorPoint, Vector3 lean)
        {
            var upright = Quaternion.LookRotation(Vector3.forward,Vector3.up);
            ResetPose(new Pose(floorPoint+Vector3.up*.08f,upright));
            Activate();
            FindPart("Spine02").body.AddForce(Vector3.ProjectOnPlane(lean,Vector3.up).normalized*35,ForceMode.Impulse);
        }

        private void OnDisable() { Suspend(); }
        private void OnDestroy()
        {
            if(animators!=null) for(int i=0;i<animators.Length;i++) if(animators[i]!=null) animators[i].enabled=animatorStates[i];
            if(skins!=null) for(int i=0;i<skins.Length;i++) if(skins[i]!=null) skins[i].updateWhenOffscreen=rendererStates[i];
            if(skins!=null && originalSkinMeshes!=null)
                for(int i=0;i<skins.Length;i++) if(skins[i]!=null) skins[i].sharedMesh=originalSkinMeshes[i];
            if(originalColliders!=null) for(int i=0;i<originalColliders.Length;i++) if(originalColliders[i]!=null) originalColliders[i].enabled=colliderStates[i];
            Dispose(physicsRoot!=null?physicsRoot.gameObject:null); Dispose(friction);
        }
        private static void Dispose(UnityEngine.Object value)
        { if(value==null)return; if(Application.isPlaying)Destroy(value); else DestroyImmediate(value); }
    }
}

