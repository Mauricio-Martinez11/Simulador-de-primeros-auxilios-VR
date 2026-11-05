using System.IO;
using System.Linq;
using FirstAidVR.Rcp;
using UnityEditor;
using UnityEngine;

public static class CprPhonePoseTools
{
    public static void BakeAndCheck()
    {
        foreach (bool left in new[] {true,false}) Bake(left);
        AssetDatabase.SaveAssets();
        CprInteractionFeedbackChecks.RunAll();
    }

    private static void Bake(bool left)
    {
        string side=left?"Left":"Right";
        var prefab=AssetDatabase.LoadAssetAtPath<GameObject>("Packages/com.meta.xr.sdk.interaction/Runtime/Meshes/OpenXR"+side+"Hand.fbx");
        var hand=Object.Instantiate(prefab);
        try
        {
            var bones=hand.GetComponentsInChildren<Transform>();
            var skin=hand.GetComponentInChildren<SkinnedMeshRenderer>();
            var wrist=bones.First(t=>t.name=="XRHand_Wrist");
            var middle=bones.First(t=>t.name=="XRHand_MiddleProximal");
            var index=bones.First(t=>t.name=="XRHand_IndexProximal");
            var little=bones.First(t=>t.name=="XRHand_LittleProximal");
            var forward=(middle.position-wrist.position).normalized;
            var up=Vector3.Cross(forward,index.position-little.position).normalized*(left?1:-1);
            var basis=Quaternion.LookRotation(forward,up);
            var inverse=Quaternion.Inverse(basis);
            var heel=Vector3.Lerp(wrist.position,middle.position,.22f);
            float scale=.0956f/Vector3.Distance(wrist.position,middle.position);
            // El mismo sistema de referencia medido del talón de la mano que en CprTrackedHands, con la piel
            // sobre la superficie de la palma. Este agarre es solo visual; nunca reemplaza la entrada.
            Vector3 Local(Vector3 world)=>inverse*(world-heel)*scale+Vector3.up*.012f;
            Vector3 World(Vector3 local)=>heel+basis*(local-Vector3.up*.012f)/scale;
            float sign=left?-1:1;
            string[] names={"Index","Middle","Ring","Little"};
            float[] heights={.112f,.096f,.080f,.064f};
            for(int i=0;i<4;i++)
            {
                var chain=new[]{"Proximal","Intermediate","Distal"}.Select(s=>bones.First(t=>t.name=="XRHand_"+names[i]+s)).ToArray();
                var tip=bones.First(t=>t.name=="XRHand_"+names[i]+"Tip");
                // Iniciar la curvatura alrededor del borde exterior en lugar de cerrar los dedos
                // hacia el centro de la pantalla como un puño.
                var aim=World(new Vector3(sign*.048f,.001f,heights[i]));
                chain[0].rotation=Quaternion.FromToRotation(chain[1].position-chain[0].position,aim-chain[0].position)*chain[0].rotation;
                var flex=basis*Vector3.right;
                chain[1].rotation=Quaternion.AngleAxis(45,flex)*chain[1].rotation;
                chain[2].rotation=Quaternion.AngleAxis(35,flex)*chain[2].rotation;
                Fit(chain,tip,World(new Vector3(sign*.042f,-.039f,heights[i])));
            }
            var thumbChain=new[]{"Metacarpal","Proximal","Distal"}.Select(s=>bones.First(t=>t.name=="XRHand_Thumb"+s)).ToArray();
            Fit(thumbChain,bones.First(t=>t.name=="XRHand_ThumbTip"),World(new Vector3(-sign*.040f,-.029f,.095f)));
            var mesh=new Mesh(); skin.BakeMesh(mesh);
            mesh.vertices=mesh.vertices.Select(v=>Local(skin.transform.position+skin.transform.rotation*v)).ToArray();
            mesh.RecalculateNormals(); mesh.RecalculateBounds(); mesh.name="CPR_Phone"+side+"Hand";
            if(mesh.bounds.size.z<.10f || mesh.bounds.size.z>.30f) throw new System.InvalidOperationException("Escala de agarre incorrecta");
            string path="Assets/Resources/RCP/Phone"+side+"Hand.asset";
            var existing=AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if(existing!=null) { EditorUtility.CopySerialized(mesh,existing); Object.DestroyImmediate(mesh); }
            else AssetDatabase.CreateAsset(mesh,path);
            File.WriteAllText("Phone-"+side+"-pose.txt",string.Join("\n",bones.Where(t=>t.name.EndsWith("Tip")).Select(t=>t.name+" "+Local(t.position))));
        }
        finally { Object.DestroyImmediate(hand); }
    }

    private static void Fit(Transform[] joints,Transform tip,Vector3 target)
    {
        for(int pass=0;pass<28;pass++)
        {
            for(int i=joints.Length-1;i>=0;i--)
            {
                var joint=joints[i];
                var delta=Quaternion.FromToRotation(tip.position-joint.position,target-joint.position);
                joint.rotation=Quaternion.Slerp(Quaternion.identity,delta,.65f)*joint.rotation;
            }
            if(Vector3.Distance(tip.position,target)<.0001f) break;
        }
    }
}
