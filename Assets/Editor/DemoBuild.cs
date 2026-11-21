using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;
using System;
using System.IO;

public static class DemoBuild
{
 public static void BuildMac()
 {
  var target=Environment.GetEnvironmentVariable("NTO_BUILD_OUTPUT")??"/tmp/Postcript-Demo.app";
  var report=BuildPipeline.BuildPlayer(new BuildPlayerOptions{scenes=new[]{"Assets/Scenes/SampleScene.unity"},locationPathName=target,target=BuildTarget.StandaloneOSX,options=BuildOptions.None});
  File.WriteAllText(target+".result.txt",report.summary.result+" errors="+report.summary.totalErrors+" warnings="+report.summary.totalWarnings);
  if(report.summary.result!=BuildResult.Succeeded)throw new Exception("Build failed");
 }
 [MenuItem("Postcript/Prepare imported demo art")]
 public static void PrepareArt()
 {
  foreach(string id in AssetDatabase.FindAssets("t:Texture2D",new[]{"Assets/Resources/Demo"}))
  {
   string path=AssetDatabase.GUIDToAssetPath(id);var importer=AssetImporter.GetAtPath(path) as TextureImporter;
   if(importer==null)continue;
   importer.isReadable=path.Contains("/Memory/");if(path.Contains("/UI/answer-"))importer.spriteBorder=path.EndsWith("answer-short-a.png")?new Vector4(211,85,211,85):path.EndsWith("answer-short-b.png")?new Vector4(209,72,209,72):new Vector4(248,55,248,55);importer.textureType=TextureImporterType.Sprite;importer.spriteImportMode=SpriteImportMode.Single;importer.alphaIsTransparency=true;importer.maxTextureSize=path.Contains("/UI/")?4096:2048;importer.textureCompression=TextureImporterCompression.Uncompressed;importer.mipmapEnabled=false;importer.SaveAndReimport();
  }
 }
 public static void BuildAll()
 {
  PrepareArt();PlayerSettings.productName="Postcript";
  string root=Environment.GetEnvironmentVariable("NTO_BUILD_ROOT")??"/tmp/Postcript-Final";
  Directory.CreateDirectory(root+"/Mac");Directory.CreateDirectory(root+"/Windows");
  var scenes=new[]{"Assets/Scenes/SampleScene.unity"};
  foreach(var target in new[]{BuildTarget.StandaloneOSX,BuildTarget.StandaloneWindows64})
  {
   string output=root+(target==BuildTarget.StandaloneOSX?"/Mac/Postcript.app":"/Windows/Postcript.exe");
   var result=BuildPipeline.BuildPlayer(new BuildPlayerOptions{scenes=scenes,target=target,locationPathName=output,options=BuildOptions.None});
   File.WriteAllText(root+"/"+target+"-result.txt",result.summary.result+" errors="+result.summary.totalErrors+" warnings="+result.summary.totalWarnings);
   if(result.summary.result!=BuildResult.Succeeded)throw new Exception("Build failed: "+target);
  }
 }
 public static void PrepareAndBuild(){PrepareArt();BuildMac();}
}
