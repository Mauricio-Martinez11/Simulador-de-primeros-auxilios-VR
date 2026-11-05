using UnityEditor;

public static class CprSceneViewTools
{
    [MenuItem("Primeros auxilios/RCP/Vista de escena ligera")]
    public static void LightSceneView()
    {
        foreach (SceneView view in SceneView.sceneViews)
        {
            view.sceneLighting = false;
            view.drawGizmos = false;
            view.Repaint();
        }
    }

    [MenuItem("Primeros auxilios/RCP/Restaurar vista de escena")]
    public static void RestoreSceneView()
    {
        foreach (SceneView view in SceneView.sceneViews)
        {
            view.sceneLighting = true;
            view.drawGizmos = true;
            view.Repaint();
        }
    }
}
