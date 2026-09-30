using UnityEngine;

namespace FirstAidVR.Rcp
{
    [CreateAssetMenu(menuName = "Primeros auxilios/Configuración RCP")]
    public sealed class CprTrainingSettings : ScriptableObject
    {
        [Header("Manos de Meta, horneadas en postura abierta")]
        public Mesh leftHand;
        public Mesh rightHand;
        public Mesh stackedLeftHand;
        public Mesh stackedRightHand;
        public Mesh gripLeftHand;
        public Mesh gripRightHand;
        public Material hologramMaterial;
        [Tooltip("Malla del paciente con la piel cubierta por ropa omitida, horneada para Quest.")]
        public Mesh clothedPatientBody;
        [Header("Detección en metros / segundos")]
        [Range(.03f, .15f)] public float captureRadius = .085f;
        [Range(.08f, .25f)] public float releaseRadius = .14f;
        [Range(10, 75)] public float palmAngle = 45;
        [Range(10, 90)] public float fingerAngle = 65;
        [Range(.4f, 3)] public float placementHold = 1.25f;
        [Range(.5f, 3)] public float buttonDwell = 1.4f;
        [Range(3, 10)] public float breathingObservation = 5;
        [Range(.03f, .08f)] public float stackedHandHeight = .035f;
        [Tooltip("Separación adicional para evitar que choquen los mandos. Solo aproximación de postura.")]
        public float controllerStackExtra = .065f;
        [Header("Enganche de manos: izquierda debajo, derecha encima")]
        [Range(.5f, 5)] public float lowerHandOcclusionGrace = 3;
        [Range(.2f, 2)] public float bothHandsLostGrace = .8f;
        [Range(.05f, .4f)] public float separationHold = .12f;
        [Range(.08f, .25f)] public float handSeparation = .16f;
        [Header("Traslado interactivo del paciente")]
        public Vector3 patientDestinationOffset = new Vector3(-1.15f, 0, .35f);
        [Range(.07f, .25f)] public float armGrabRadius = .16f;
        [Range(.4f, .9f)] public float grabThreshold = .65f;
        [Range(.1f, .5f)] public float grabReleaseThreshold = .3f;
        [Range(.2f, 1.5f)] public float grabTrackingGrace = .6f;
        [Range(.05f, .25f)] public float grabOpenHold = .10f;
        [Range(.1f, .5f)] public float patientSnapRadius = .28f;
        [Range(5, 60)] public float patientSnapAngle = 25;
        [Range(.2f, 2)] public float patientSnapDuration = .4f;
        [Range(.3f, 3)] public float patientDragSpeed = 1.6f;
        [Header("Ragdoll y agarres físicos")]
        [Range(35, 120)] public float patientMass = 72;
        [Range(.1f, .8f)] public float patientFriction = .35f;
        [Range(3000, 60000)] public float grabSpring = 60000;
        [Range(100, 1500)] public float grabDamping = 900;
        [Range(900, 4000)] public float grabMaxForce = 3000;
        [Range(2, 10)] public float grabTargetSpeed = 6;
        [Range(.35f, 1)] public float grabMaxReach = .65f;
        [Range(1, 4)] public float grabGuideSeconds = 2.4f;
        [Tooltip("Incluye la pareja de guías en tobillos como opción de interacción del muñeco.")]
        public bool showAnkleGrips = true;
        [Header("Ajustes anatómicos desde los huesos del paciente, en metros")]
        public Vector3 chestOffset = new Vector3(0, .115f, -.08f);
        public Vector3 shoulderOffset = new Vector3(0, .081f, 0);
        [Header("Postura pasiva de las manos")]
        [Tooltip("Curvatura adicional de los dedos cuando el paciente está relajado; no cambia el agarre del usuario.")]
        [Range(0, 35)] public float relaxedFingerCurl = 18;
        [Range(0, 45)] public float relaxedFingerCurlDistal = 28;
        [Range(0, 30)] public float relaxedThumbCurl = 14;
        [Header("Mandos: punto aproximado del talón de la mano desde el grip")]
        public Vector3 controllerPalmOffset = new Vector3(0, -.035f, .04f);
        public Vector3 controllerPalmEuler = Vector3.zero;
        public string menuScene = "1_MainMenu";

        private void OnValidate()
        {
            releaseRadius = Mathf.Max(captureRadius + .02f, releaseRadius);
            breathingObservation = Mathf.Clamp(breathingObservation, 3, 10);
            grabReleaseThreshold = Mathf.Min(grabReleaseThreshold, grabThreshold - .1f);
        }
    }
}
