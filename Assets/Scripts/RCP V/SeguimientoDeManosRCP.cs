using System.Collections.Generic;
using Oculus.Interaction;
using Oculus.Interaction.Input;
using UnityEngine;
using UnityEngine.XR;
using Hand = Oculus.Interaction.Input.Hand;
using HandFinger = Oculus.Interaction.Input.HandFinger;

namespace FirstAidVR.Rcp.Consulta
{
    public sealed class SeguimientoDeManosRCP : MonoBehaviour
    {
        private Hand[] hands;
        private HandVisual[] visuals;
        private readonly Dictionary<HandVisual, bool> hidden = new Dictionary<HandVisual, bool>();
        private float nextDiscovery;
        private OVRCameraRig rig;
        private readonly string[] sources = new string[2];
        private float nextPlacementLog;
        public Transform Head => rig != null ? rig.centerEyeAnchor : Camera.main != null ? Camera.main.transform : null;
        public OVRCameraRig Rig => rig;

        public void Discover()
        {
            rig = FindObjectOfType<OVRCameraRig>();
            hands = FindObjectsOfType<Hand>(true);
            visuals = FindObjectsOfType<HandVisual>(true);
            nextDiscovery = Time.unscaledTime + 2;
        }

        public MuestraDeManoRCP Read(bool left, ConfiguracionDeCriteriosRCP settings)
        {
            if (hands == null || Time.unscaledTime >= nextDiscovery) Discover();
            sources[left ? 0 : 1] = "sin seguimiento";
            var side = left ? Handedness.Left : Handedness.Right;
            Hand syntheticFallback = null;
            foreach (var hand in hands)
            {
                if (hand == null || !hand.isActiveAndEnabled || hand.Handedness != side) continue;

                // Meta XR Simulator controla su mano mediante teclado a través de SyntheticHand. Priorizar una
                // mano medida en el dispositivo y usar la fuente sintética solo en el editor para que
                // el gesto de agarre del simulador controle el mismo código de transporte.
                if (hand is SyntheticHand)
                {
                    if (Application.isEditor && syntheticFallback == null) syntheticFallback = hand;
                    continue;
                }

                if (TryReadHand(hand, left, false, out var sample))
                {
                    sources[left ? 0 : 1] = hand.name + "/" + hand.GetData().RootPoseOrigin;
                    return sample;
                }
            }

            if (Application.isEditor && syntheticFallback != null
                && TryReadHand(syntheticFallback, left, true, out var simulatedSample))
            {
                sources[left ? 0 : 1] = "simulador/" + syntheticFallback.name;
                return simulatedSample;
            }

            // Una pose de agarre solo aproxima el talón anatómico de la mano.
            var device = InputDevices.GetDeviceAtXRNode(left ? XRNode.LeftHand : XRNode.RightHand);
            if (device.isValid && (device.characteristics & InputDeviceCharacteristics.Controller) != 0
                && device.TryGetFeatureValue(CommonUsages.isTracked, out bool tracked) && tracked
                && device.TryGetFeatureValue(CommonUsages.devicePosition, out Vector3 position)
                && device.TryGetFeatureValue(CommonUsages.deviceRotation, out Quaternion rotation))
            {
                sources[left ? 0 : 1] = "mando XR";
                var space = rig != null ? rig.trackingSpace : null;
                var worldPosition = space != null ? space.TransformPoint(position) : position;
                var worldRotation = space != null ? space.rotation * rotation : rotation;
                bool gripValid = device.TryGetFeatureValue(CommonUsages.grip, out float grip);
                if (!gripValid && device.TryGetFeatureValue(CommonUsages.gripButton, out bool pressed))
                { gripValid = true; grip = pressed ? 1 : 0; }
                return new MuestraDeManoRCP(new Pose(worldPosition + worldRotation * settings.controllerPalmOffset,
                    worldRotation * Quaternion.Euler(settings.controllerPalmEuler)), true, grip, gripValid);
            }
            return default;
        }

        private static bool TryReadHand(Hand hand, bool left, bool allowSynthetic, out MuestraDeManoRCP sample)
        {
            sample = default;
            // SyntheticHand puede figurar como desconectada o con baja confianza aunque el simulador
            // siga proporcionando poses articulares válidas; por eso solo para ella se flexibilizan esos dos indicadores.
            // Los componentes Hand comunes también pueden envolver fuentes sintéticas: HandSphereMap proporciona
            // un prefab siempre conectado y LastKnownGoodHand puede conservar una pose antigua.
            // Comprobar solo el tipo de componente o IsTrackedDataValid aceptaría ambos casos.
            if (!allowSynthetic && !IsCurrentTrackingData(hand.GetData())) return false;
            if (!hand.GetRootPose(out var wrist)
                || !hand.GetJointPose(HandJointId.HandMiddle1, out var middle)
                || !hand.GetJointPose(HandJointId.HandIndex1, out var index)
                || !hand.GetJointPose(HandJointId.HandPinky1, out var little)) return false;
            var forward = (middle.position - wrist.position).normalized;
            var normal = Vector3.Cross(forward, index.position - little.position).normalized * (left ? 1 : -1);
            if (normal.sqrMagnitude < .9f || forward.sqrMagnitude < .9f) return false;
            var heel = Vector3.Lerp(wrist.position, middle.position, .22f) - normal * .012f;
            float grip = ReadGrip(hand, wrist.position, out bool gripValid);
            sample = new MuestraDeManoRCP(new Pose(heel, Quaternion.LookRotation(forward, normal)), false, grip, gripValid);
            sample.reliable = allowSynthetic || hand.GetData().IsHighConfidence;
            return true;
        }

        public static bool IsCurrentTrackingData(HandDataAsset data)
        {
            return data != null && data.IsDataValid && data.IsConnected && data.IsTracked
                && (data.RootPoseOrigin == PoseOrigin.RawTrackedPose
                    || data.RootPoseOrigin == PoseOrigin.FilteredTrackedPose);
        }

        public void LogPlacement(MuestraDeManoRCP left, MuestraDeManoRCP right, Pose chest, ColocacionYSuperposicionDeManosRCP placement)
        {
            if (Time.unscaledTime < nextPlacementLog) return;
            nextPlacementLog = Time.unscaledTime + 1;
            Debug.Log($"[RCP Hands live-source-v2] step={placement.Step} " +
                $"right={right.valid} source={sources[1]} distance={(right.valid ? Vector3.Distance(right.pose.position,chest.position).ToString("F3") : "-")} " +
                $"left={left.valid} source={sources[0]} distance={(left.valid ? Vector3.Distance(left.pose.position,chest.position).ToString("F3") : "-")}");
        }

        private static float ReadGrip(Hand hand, Vector3 wrist, out bool valid)
        {
            // Una pinza o una mano cerrada pueden sujetar un brazo. La falta de dedos detectados no equivale a una mano abierta.
            float curl = 0; int count = 0;
            AddCurl(hand, HandFinger.Middle, HandJointId.HandMiddle1, HandJointId.HandMiddleTip, wrist, ref curl, ref count);
            AddCurl(hand, HandFinger.Ring, HandJointId.HandRing1, HandJointId.HandRingTip, wrist, ref curl, ref count);
            AddCurl(hand, HandFinger.Pinky, HandJointId.HandPinky1, HandJointId.HandPinkyTip, wrist, ref curl, ref count);
            bool pinchValid = hand.GetFingerIsHighConfidence(HandFinger.Index);
            return CombineGrip(curl, count, hand.GetFingerPinchStrength(HandFinger.Index), pinchValid, out valid);
        }

        public static float CombineGrip(float curlSum, int fingerCount, float pinch, bool pinchValid, out bool valid)
        {
            // Un puño parcialmente oculto aún puede tener un dedo doblado detectado de forma fiable. Una pinza abierta
            // observada también es una entrada válida: debe rearmar la mano después de perder el seguimiento.
            valid = fingerCount > 0 || pinchValid;
            return Mathf.Clamp01(Mathf.Max(fingerCount > 0 ? curlSum / fingerCount : 0, pinchValid ? pinch : 0));
        }

        private static void AddCurl(Hand hand, HandFinger finger, HandJointId proximal, HandJointId tip,
            Vector3 wrist, ref float curl, ref int count)
        {
            if (!hand.GetFingerIsHighConfidence(finger) || !hand.GetJointPose(proximal, out var root)
                || !hand.GetJointPose(tip, out var end)) return;
            float length = Vector3.Distance(wrist, root.position);
            if (length < .015f) return;
            curl += Mathf.InverseLerp(1.75f, 1.05f, Vector3.Distance(wrist, end.position) / length);
            count++;
        }

        public void Pulse(bool left, float amplitude = .35f)
        {
            var device = InputDevices.GetDeviceAtXRNode(left ? XRNode.LeftHand : XRNode.RightHand);
            if (device.isValid && device.TryGetHapticCapabilities(out var capabilities) && capabilities.supportsImpulse)
                device.SendHapticImpulse(0, amplitude, .07f);
        }

        public void HideTrackedVisual(bool left, bool hide)
        {
            if (visuals == null) return;
            foreach (var visual in visuals)
            {
                if (visual == null || visual.Hand == null || visual.Hand.Handedness != (left ? Handedness.Left : Handedness.Right)) continue;
                if (hide)
                {
                    if (!hidden.ContainsKey(visual)) hidden.Add(visual, visual.ForceOffVisibility);
                    visual.ForceOffVisibility = true;
                }
                else if (hidden.TryGetValue(visual, out bool previous))
                { visual.ForceOffVisibility = previous; hidden.Remove(visual); }
            }
        }
        public void RestoreVisuals()
        {
            foreach (var pair in hidden) if (pair.Key != null) pair.Key.ForceOffVisibility = pair.Value;
            hidden.Clear();
        }
        private void OnDisable() { RestoreVisuals(); }
    }
}
