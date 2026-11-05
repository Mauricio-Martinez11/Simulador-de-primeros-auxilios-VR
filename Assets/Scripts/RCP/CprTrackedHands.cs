using System.Collections.Generic;
using Oculus.Interaction;
using Oculus.Interaction.Input;
using UnityEngine;
using UnityEngine.XR;
using Hand = Oculus.Interaction.Input.Hand;
using HandFinger = Oculus.Interaction.Input.HandFinger;

namespace FirstAidVR.Rcp
{
    public sealed class CprTrackedHands : MonoBehaviour
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

        public CprHandSample Read(bool left, CprTrainingSettings settings)
        {
            if (hands == null || Time.unscaledTime >= nextDiscovery) Discover();
            sources[left ? 0 : 1] = "sin seguimiento";
            var side = left ? Handedness.Left : Handedness.Right;
            Hand syntheticFallback = null;
            foreach (var hand in hands)
            {
                if (hand == null || !hand.isActiveAndEnabled || hand.Handedness != side) continue;

                // Meta XR Simulator drives its keyboard hand through a SyntheticHand. Prefer a
                // measured hand on device, then use the synthetic source only in the editor so
                // the simulator's grab gesture can drive the same transport code.
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

            // A grip pose is only an approximation of the anatomical heel of the hand.
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
                return new CprHandSample(new Pose(worldPosition + worldRotation * settings.controllerPalmOffset,
                    worldRotation * Quaternion.Euler(settings.controllerPalmEuler)), true, grip, gripValid);
            }
            return default;
        }

        private static bool TryReadHand(Hand hand, bool left, bool allowSynthetic, out CprHandSample sample)
        {
            sample = default;
            // SyntheticHand can be marked disconnected/low-confidence while the simulator is
            // still providing valid joint poses, so those two flags are relaxed only for it.
            // Ordinary Hand components also wrap synthetic sources: HandSphereMap supplies
            // a permanently connected prefab, and LastKnownGoodHand can freeze an old pose.
            // Testing the component type or IsTrackedDataValid alone accepts both.
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
            sample = new CprHandSample(new Pose(heel, Quaternion.LookRotation(forward, normal)), false, grip, gripValid);
            sample.reliable = allowSynthetic || hand.GetData().IsHighConfidence;
            return true;
        }

        public static bool IsCurrentTrackingData(HandDataAsset data)
        {
            return data != null && data.IsDataValid && data.IsConnected && data.IsTracked
                && (data.RootPoseOrigin == PoseOrigin.RawTrackedPose
                    || data.RootPoseOrigin == PoseOrigin.FilteredTrackedPose);
        }

        public void LogPlacement(CprHandSample left, CprHandSample right, Pose chest, CprFlow placement)
        {
            if (Time.unscaledTime < nextPlacementLog) return;
            nextPlacementLog = Time.unscaledTime + 1;
            Debug.Log($"[RCP Hands live-source-v2] step={placement.Step} " +
                $"right={right.valid} source={sources[1]} distance={(right.valid ? Vector3.Distance(right.pose.position,chest.position).ToString("F3") : "-")} " +
                $"left={left.valid} source={sources[0]} distance={(left.valid ? Vector3.Distance(left.pose.position,chest.position).ToString("F3") : "-")}");
        }

        private static float ReadGrip(Hand hand, Vector3 wrist, out bool valid)
        {
            // A pinch OR a closed hand can hold an arm. Missing fingers are not an open-hand gesture.
            float curl = 0; int count = 0;
            AddCurl(hand, HandFinger.Middle, HandJointId.HandMiddle1, HandJointId.HandMiddleTip, wrist, ref curl, ref count);
            AddCurl(hand, HandFinger.Ring, HandJointId.HandRing1, HandJointId.HandRingTip, wrist, ref curl, ref count);
            AddCurl(hand, HandFinger.Pinky, HandJointId.HandPinky1, HandJointId.HandPinkyTip, wrist, ref curl, ref count);
            bool pinchValid = hand.GetFingerIsHighConfidence(HandFinger.Index);
            return CombineGrip(curl, count, hand.GetFingerPinchStrength(HandFinger.Index), pinchValid, out valid);
        }

        public static float CombineGrip(float curlSum, int fingerCount, float pinch, bool pinchValid, out bool valid)
        {
            // A partially occluded fist can still have one reliable curled finger. An observed
            // open pinch is valid input too: it must re-arm the hand after tracking was lost.
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
