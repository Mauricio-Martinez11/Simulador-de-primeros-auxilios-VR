using System;
using FirstAidVR.Rcp;
using UnityEditor;
using UnityEngine;
using UnityEngine.Video;

// Import the reviewed illustrated sequences assembled by Tools/GenerateCprLessons.py.
public static class CprLessonVideoTools
{
    public static readonly string[] Names={"01_Seguridad","02_Respuesta","03_Respiracion","04_Ayuda","05_Manos","06_Compresiones"};
    public static void Assign(CprTrainingSettings settings)
    {
        settings.practiceVideos=new VideoClip[6]; settings.practiceVideoPosters=new Texture2D[6];
        for(int i=0;i<6;i++)
        {
            string path="Assets/Videos/RCP/"+Names[i];
            var importer=AssetImporter.GetAtPath(path+".png") as TextureImporter;
            if(importer!=null && (importer.mipmapEnabled || importer.textureShape!=TextureImporterShape.Texture2D))
            {
                importer.textureType=TextureImporterType.Default; importer.textureShape=TextureImporterShape.Texture2D;
                importer.mipmapEnabled=false; importer.maxTextureSize=1024; importer.SaveAndReimport();
            }
            settings.practiceVideos[i]=AssetDatabase.LoadAssetAtPath<VideoClip>(path+".mp4");
            settings.practiceVideoPosters[i]=AssetDatabase.LoadAssetAtPath<Texture2D>(path+".png");
            if(settings.practiceVideos[i]==null || settings.practiceVideoPosters[i]==null)
                throw new InvalidOperationException("Falta la demostración ilustrada: "+path+"; video="+settings.practiceVideos[i]+"; poster="+settings.practiceVideoPosters[i]+"; main PNG="+AssetDatabase.GetMainAssetTypeAtPath(path+".png")+"; main MP4="+AssetDatabase.GetMainAssetTypeAtPath(path+".mp4"));
        }
    }
}
