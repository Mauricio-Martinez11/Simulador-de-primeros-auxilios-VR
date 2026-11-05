using FirstAidVR.Rcp;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

// También protege una compilación normal con Build/Build And Run para que la corrección de Android no pierda su recurso sin aviso.
public sealed class CprQuestBuild : IPreprocessBuildWithReport
{
    public int callbackOrder => 0;
    public void OnPreprocessBuild(BuildReport report)
    {
        if (report.summary.platform != BuildTarget.Android) return;
        var settings = Resources.Load<CprTrainingSettings>("RCP/TrainingSettings");
        if (settings == null || settings.clothedPatientBody == null)
            throw new BuildFailedException("RCP: usa Primeros auxilios/RCP/Regenerar piel cubierta por ropa antes de crear el APK.");
        if (settings.hologramMaterial == null || settings.hologramMaterial.shader == null
            || ShaderUtil.ShaderHasError(settings.hologramMaterial.shader))
            throw new BuildFailedException("RCP: falta el shader de holograma o contiene errores.");
    }

}
