using System.IO;
using System.Linq;
using FirstAidVR.Rcp;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class CprTrainingTools
{
    [MenuItem("Primeros auxilios/RCP/Abrir tutorial")]
    public static void OpenTutorial()
    {
        if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            EditorSceneManager.OpenScene("Assets/Scenes/2_Tutorial_RCP.unity");
    }

    [MenuItem("Primeros auxilios/RCP/Seleccionar configuración")]
    public static void SelectSettings() { Selection.activeObject = Resources.Load<CprTrainingSettings>("RCP/TrainingSettings"); }

    [MenuItem("Primeros auxilios/RCP/Regenerar modelos holográficos")]
    public static void BakeHands()
    {
        BakeHandSet(false);
        BakeStackedHands();
        BakeGripHands();
    }

    [MenuItem("Primeros auxilios/RCP/Regenerar postura entrelazada")]
    public static void BakeStackedHands()
    {
        BakeHandSet(true);
        var settings=Resources.Load<CprTrainingSettings>("RCP/TrainingSettings");
        settings.stackedLeftHand=AssetDatabase.LoadAssetAtPath<Mesh>("Assets/Resources/RCP/StackedLeftHand.asset");
        settings.stackedRightHand=AssetDatabase.LoadAssetAtPath<Mesh>("Assets/Resources/RCP/StackedRightHand.asset");
        EditorUtility.SetDirty(settings); AssetDatabase.SaveAssets();
    }

    [MenuItem("Primeros auxilios/RCP/Regenerar manos de agarre")]
    public static void BakeGripHands()
    {
        BakeHandSet(false,true);
        var settings=Resources.Load<CprTrainingSettings>("RCP/TrainingSettings");
        settings.gripLeftHand=AssetDatabase.LoadAssetAtPath<Mesh>("Assets/Resources/RCP/GripLeftHand.asset");
        settings.gripRightHand=AssetDatabase.LoadAssetAtPath<Mesh>("Assets/Resources/RCP/GripRightHand.asset");
        EditorUtility.SetDirty(settings); AssetDatabase.SaveAssets();
    }

    private static void BakeHandSet(bool stacked,bool gripping=false)
    {
        const string folder = "Assets/Resources/RCP";
        Directory.CreateDirectory(folder); AssetDatabase.Refresh();
        foreach (var side in new[] { "Left", "Right" })
        {
            var source = AssetDatabase.LoadAssetAtPath<GameObject>("Packages/com.meta.xr.sdk.interaction/Runtime/Meshes/OpenXR" + side + "Hand.fbx");
            if (source == null) throw new System.InvalidOperationException("No se encuentra el modelo de mano de Meta: " + side);
            var hand = Object.Instantiate(source);
            try
            {
                var bones = hand.GetComponentsInChildren<Transform>();
                var skin = hand.GetComponentInChildren<SkinnedMeshRenderer>();
                var wrist = bones.First(t => t.name == "XRHand_Wrist");
                var middle = bones.First(t => t.name == "XRHand_MiddleProximal");
                var index = bones.First(t => t.name == "XRHand_IndexProximal");
                var little = bones.First(t => t.name == "XRHand_LittleProximal");
                var forward = (middle.position - wrist.position).normalized;
                var up = Vector3.Cross(forward,index.position-little.position).normalized * (side=="Left" ? 1 : -1);
                var inverse = Quaternion.Inverse(Quaternion.LookRotation(forward,up));
                var heel = Vector3.Lerp(wrist.position,middle.position,.22f);
                if(stacked || gripping)
                {
                    // Lower fingers lift off the chest. Upper fingers wrap over the lower hand.
                    // Rotate the actual phalanges before baking; preserve the anatomical hand mesh.
                    var flexAxis=Vector3.Cross(up,forward).normalized;
                    foreach(var finger in new[]{"Index","Middle","Ring","Little"})
                    {
                        var proximal=bones.First(t=>t.name=="XRHand_"+finger+"Proximal");
                        var intermediate=bones.First(t=>t.name=="XRHand_"+finger+"Intermediate");
                        var distal=bones.First(t=>t.name=="XRHand_"+finger+"Distal");
                        if(stacked && !gripping)
                        {
                            // Bring the spread MCP directions together before flexing each chain.
                            var direction=Vector3.ProjectOnPlane(intermediate.position-proximal.position,up);
                            float spread=Vector3.SignedAngle(direction,forward,up);
                            float fan=side=="Right"?(finger=="Index"?-17:finger=="Middle"?-5:finger=="Ring"?10:24)
                                :(finger=="Index"?17:finger=="Middle"?5:finger=="Ring"?-10:-24);
                            proximal.rotation=Quaternion.AngleAxis(spread+fan,up)*proximal.rotation;
                        }
                        proximal.rotation=Quaternion.AngleAxis(gripping?42:side=="Right"?-6:8,flexAxis)*proximal.rotation;
                        intermediate.rotation=Quaternion.AngleAxis(gripping?62:side=="Right"?3:43,flexAxis)*intermediate.rotation;
                        distal.rotation=Quaternion.AngleAxis(gripping?28:side=="Right"?2:18,flexAxis)*distal.rotation;
                    }
                    if(gripping || stacked)
                    {
                        var thumb=bones.First(t=>t.name=="XRHand_ThumbProximal");
                        var thumbTip=bones.First(t=>t.name=="XRHand_ThumbDistal");
                        var thumbAxis=Vector3.Cross(up,thumbTip.position-thumb.position).normalized;
                        thumb.rotation=Quaternion.AngleAxis(gripping?35:side=="Right"?-5:20,thumbAxis)*thumb.rotation;
                        thumbTip.rotation=Quaternion.AngleAxis(gripping?30:side=="Right"?0:20,thumbAxis)*thumbTip.rotation;
                    }
                }
                var mesh = new Mesh(); skin.BakeMesh(mesh);
                // BakeMesh already applies the FBX skin scale. Applying TransformPoint here scales it twice.
                // Meta's source FBX is authored in centimetres; normalize using its 9.56 cm wrist-to-MCP length.
                float toMeters = .0956f / Vector3.Distance(wrist.position,middle.position);
                var vertices = mesh.vertices.Select(v => inverse*(skin.transform.position+skin.transform.rotation*v-heel)*toMeters).ToArray();
                float contactY = vertices.Where(v => Mathf.Abs(v.z)<.018f && (!gripping || Mathf.Abs(v.x)<.02f)).Min(v => v.y);
                for (int i=0;i<vertices.Length;i++)
                {
                    // Slightly lift the extended fingers so that the heel indicates the contact surface.
                    vertices[i].y -= contactY;
                    if(stacked && side=="Left")
                    {
                        // Offset the upper fingers into the gaps of the lower hand;
                        // keep the palm heel centered and in contact with its dorsum.
                        vertices[i].x += .008f*Mathf.SmoothStep(0,1,Mathf.InverseLerp(.02f,.085f,vertices[i].z));
                        vertices[i].y -= .008f;
                    }
                    if(!stacked && !gripping) vertices[i].y += Mathf.SmoothStep(0,.024f,Mathf.InverseLerp(.05f,.14f,vertices[i].z));
                }
                string prefix=gripping?"Grip":stacked?"Stacked":"";
                mesh.vertices=vertices; mesh.RecalculateNormals(); mesh.RecalculateBounds(); mesh.name="CPR_"+prefix+side+"Hand";
                float minimumDepth=(stacked || gripping) ? .09f : .15f;
                if(mesh.bounds.size.z<minimumDepth || mesh.bounds.size.z>.28f)
                    throw new System.InvalidOperationException("La escala del modelo de mano no es anatómica: "+mesh.bounds);
                string path=folder+"/"+prefix+side+"Hand.asset";
                var existing=AssetDatabase.LoadAssetAtPath<Mesh>(path);
                if(existing!=null) { EditorUtility.CopySerialized(mesh,existing); Object.DestroyImmediate(mesh); }
                else AssetDatabase.CreateAsset(mesh,path);
            }
            finally { Object.DestroyImmediate(hand); }
        }
        AssetDatabase.SaveAssets();
    }
}
