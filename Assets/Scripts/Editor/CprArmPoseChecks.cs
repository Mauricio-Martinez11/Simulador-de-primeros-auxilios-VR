using System;
using FirstAidVR.Rcp;
using UnityEngine;

/// <summary>Measures actual joint motion under torque, lift, sideways pull and a drop.</summary>
public static class CprArmPoseChecks
{
    public static int Passed { get; private set; }
    public static float WristAngle { get; private set; }
    public static float ElbowMin { get; private set; }
    public static float ElbowMax { get; private set; }
    public static float ShoulderTwist { get; private set; }
    public static float KneeMin { get; private set; }
    public static float KneeMax { get; private set; }
    private static string phase, wristPeak;

    public static void Run()
    {
        Passed=0; WristAngle=ElbowMin=ElbowMax=ShoulderTwist=KneeMin=KneeMax=0;
        foreach(float step in new[]{.02f,1f/72})
        {
        using(var f=new CprTransportChecks.Fixture())
        {
            f.dt=step;
            var rag=f.mover.Ragdoll;
            foreach(var side in new[]{"L_","R_"})
            foreach(var name in new[]{"Hand","Forearm","Upperarm","Calf"})
            for(int axis=0;axis<(name=="Upperarm" || name=="Calf"?1:3);axis++)
            foreach(float sign in new[]{-1f,1f})
            {
                f.Reset();
                rag.ResetPose(new Pose(f.initial.position+Vector3.up*1.5f,f.initial.rotation));
                var part=rag.FindPart(side+name);
                phase=$"torsión {side+name} eje {axis} signo {sign}";
                // Anchor only the ancestors in this isolated limit test. Descendants remain free.
                foreach(var candidate in rag.Parts)
                {
                    var ancestor=candidate;
                    while(ancestor!=null && ancestor!=part) ancestor=ancestor.parent;
                    candidate.body.isKinematic=ancestor==null;
                    candidate.body.useGravity=false;
                }
                var direction=axis==0?part.joint.axis:axis==1?part.joint.secondaryAxis:Vector3.Cross(part.joint.axis,part.joint.secondaryAxis);
                for(float t=0;t<.6f;t+=step)
                {
                    part.body.AddTorque(part.body.transform.TransformDirection(direction)*sign*30,ForceMode.Force);
                    f.physics.Simulate(step); rag.SyncVisuals(); Measure(rag);
                }
                Debug.Log($"LÍMITE {side+name} eje={axis} carga={sign}: giro final={Twist(Relative(part)):F1}°");
                Check(name+" conserva sus límites bajo torsión");
            }
        }
        // Start manipulation with a clean rig; the limit fixture intentionally pins ancestors.
        using(var f=new CprTransportChecks.Fixture())
        {
            f.dt=step; var rag=f.mover.Ragdoll;
            foreach(int site in new[]{0,2})
            {
                f.Reset(); f.Step(.3f);
                var left=f.At(site,0); var right=f.At(site+1,0); f.Step(step,left,right);
                left=f.At(site,1); right=f.At(site+1,1); f.Step(step,left,right);
                var startL=left.pose.position; var startR=right.pose.position;
                for(float t=0;t<5;t+=step)
                {
                    // Lift, then pull sideways while supported by the same forearm.
                    var move=new Vector3(Mathf.Sin(t)*.25f,Mathf.Min(2,t*.65f),Mathf.Max(0,t-3)*.25f);
                    left.pose.position=startL+move; right.pose.position=startR+move;
                    phase=$"levantar zona {site} t={t:F2}";
                    f.Step(step,left,right); Measure(rag);
                }
                if(f.mover.GripCount!=2) throw new Exception("Límites de brazos: se pierde el agarre al levantar.");
                left.grip=right.grip=0;
                for(float t=0;t<3;t+=step) { phase=$"caída zona {site} t={t:F2}"; f.Step(step,left,right); Measure(rag); }
                Check("Elevación, tirón lateral y caída mantienen límites de brazos");
            }
        }
        }
        Debug.Log($"BRAZOS: {Passed} casos; muñeca {WristAngle:F1}°; codo {ElbowMin:F1}° a {ElbowMax:F1}°; torsión hombro {ShoulderTwist:F1}°.");
    }

    private static Quaternion Relative(CprPatientRagdoll.Part part)
    {
        var bind=Quaternion.Inverse(part.parent.rootRotation)*part.rootRotation;
        var delta=Quaternion.Inverse(bind)*Quaternion.Inverse(part.parent.body.rotation)*part.body.rotation;
        var basis=Quaternion.LookRotation(Vector3.Cross(part.joint.axis,part.joint.secondaryAxis),part.joint.secondaryAxis);
        return Quaternion.Inverse(basis)*delta*basis;
    }
    private static float Twist(Quaternion q) => Mathf.DeltaAngle(0,2*Mathf.Atan2(q.x,q.w)*Mathf.Rad2Deg);
    private static void Measure(CprPatientRagdoll rag)
    {
        foreach(var side in new[]{"L_","R_"})
        {
            var wrist=Quaternion.Angle(Quaternion.identity,Relative(rag.FindPart(side+"Hand")));
            if(wrist>WristAngle) { WristAngle=wrist; wristPeak=phase+" "+side; }
            var elbow=Twist(Relative(rag.FindPart(side+"Forearm")));
            ElbowMin=Mathf.Min(ElbowMin,elbow); ElbowMax=Mathf.Max(ElbowMax,elbow);
            var knee=Twist(Relative(rag.FindPart(side+"Calf")));
            KneeMin=Mathf.Min(KneeMin,knee); KneeMax=Mathf.Max(KneeMax,knee);
            ShoulderTwist=Mathf.Max(ShoulderTwist,Mathf.Abs(Twist(Relative(rag.FindPart(side+"Upperarm")))));
        }
    }
    private static void Check(string label)
    {
        if(WristAngle>40 || ElbowMin< -5 || ElbowMax>120 || ShoulderTwist>40 || KneeMin< -10 || KneeMax>145)
            throw new InvalidOperationException($"{label}: muñeca={WristAngle:F1} ({wristPeak}), codo={ElbowMin:F1}..{ElbowMax:F1}, hombro={ShoulderTwist:F1}, rodilla={KneeMin:F1}..{KneeMax:F1}");
        Passed++;
    }
}
