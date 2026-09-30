using System;
using System.Linq;
using FirstAidVR.Rcp;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class CprProjectChecks
{
    [MenuItem("Primeros auxilios/RCP/Verificar integración del proyecto")]
    public static void Run()
    {
        CprFlowChecks.Run();
        var settings = AssetDatabase.LoadAssetAtPath<CprTrainingSettings>("Assets/Resources/RCP/TrainingSettings.asset");
        if (settings == null || settings.leftHand == null || settings.rightHand == null || settings.hologramMaterial == null)
            throw new InvalidOperationException("Faltan referencias de los recursos de RCP.");
        if (settings.clothedPatientBody == null)
            throw new InvalidOperationException("Falta la malla del paciente vestido para Quest.");
        if(settings.stackedLeftHand==null || settings.stackedRightHand==null)
            throw new InvalidOperationException("Faltan las mallas de la postura entrelazada.");
        if(settings.gripLeftHand==null || settings.gripRightHand==null)
            throw new InvalidOperationException("Faltan las mallas de las manos de agarre.");
        if (ShaderUtil.ShaderHasError(settings.hologramMaterial.shader)) throw new InvalidOperationException("Error de shader RCP.");
        foreach (var mesh in new[] { settings.leftHand, settings.rightHand })
            if (mesh.bounds.size.z < .15f || mesh.bounds.size.z > .28f) throw new InvalidOperationException("Escala de mano incorrecta.");
        const string path="Assets/Scenes/2_Tutorial_RCP.unity";
        var scene=SceneManager.GetSceneByPath(path);
        bool alreadyLoaded=scene.IsValid() && scene.isLoaded;
        if(!alreadyLoaded) scene=EditorSceneManager.OpenScene(path,OpenSceneMode.Additive);
        try
        {
            var tutorials = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<CprTutorial>()).ToArray();
            if (tutorials.Length != 1 || tutorials[0].xrCorePrefab == null) throw new InvalidOperationException("Tutorial o rig sin referencia.");
            if (!scene.GetRootGameObjects().Any(root => root.name == "Hombre_Paciente")) throw new InvalidOperationException("Paciente no encontrado.");
            if (!EditorBuildSettings.scenes.Any(entry => entry.enabled && entry.path == "Assets/Scenes/2_Tutorial_RCP.unity"))
                throw new InvalidOperationException("La escena del tutorial no está incluida en Build Settings.");
        }
        finally { if(!alreadyLoaded) EditorSceneManager.CloseScene(scene,true); }
        Debug.Log("RCP: referencias de escena y recursos verificadas.");
    }
}
