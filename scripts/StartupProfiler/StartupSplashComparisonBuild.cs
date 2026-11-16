// Diagnostic-only Editor helper. Never installed into the template SDK or a player.
using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;
public static class StartupSplashComparisonBuild
{
    private const string Project="D:/ForgeCore/Workspace/Games/ForgeCore";
    private const string Scene="Assets/Scenes/Retro/Industrial-Retro64-v2.unity";
    private const string SettingsHash="3b45899f92669925ca58f854db6f5aae4a8fb1125818d11a601adda32ee043dd";
    public static void ExportPair()
    {
        var settings=Path.Combine(Project,"ProjectSettings/ProjectSettings.asset");
        byte[] original=null;var oldShow=false;var captured=false;var success=false;
        try
        {
            if(Path.GetFullPath(Application.dataPath).Replace('\\','/')!=Project+"/Assets")throw new IOException("Exact project required.");
            original=File.ReadAllBytes(settings);
            if(Hash(original)!=SettingsHash)throw new IOException("Project settings changed; stop before mutation.");
            if(Hash(File.ReadAllBytes(Path.Combine(Project,Scene)))!="1c5d16f360cd0e0f5979cf20ef1817a702cf389dd76ac15b9522eac11512cb9a")throw new IOException("Scene changed.");
            oldShow=PlayerSettings.SplashScreen.show;
            captured=true;
            if(!oldShow)throw new IOException("Expected splash-enabled baseline.");
            foreach(var enabled in new[]{true,false})
            {
                var label=enabled?"On":"Off";
                var folder=Path.Combine(Project,"Builds/S01-StartupFade-20261116/Splash"+label);
                if(Directory.Exists(folder))throw new IOException("Fresh pair destinations required.");
            }
            foreach(var enabled in new[]{true,false})
            {
                var label=enabled?"On":"Off";
                PlayerSettings.SplashScreen.show=enabled;
                var output=Path.Combine(Project,"Builds/S01-StartupFade-20261116/Splash"+label+"/ForgeCore-Splash"+label+".exe");
                var report=BuildPipeline.BuildPlayer(new BuildPlayerOptions{scenes=new[]{Scene},locationPathName=output,target=BuildTarget.StandaloneWindows64,options=BuildOptions.Development});
                if(report.summary.result!=BuildResult.Succeeded||report.summary.totalErrors!=0)throw new IOException("Diagnostic "+label+" export failed.");
                Debug.Log("STARTUP_SPLASH_BUILD "+label+" succeeded / "+report.summary.totalSize+" bytes");
            }
            success=true;
        }
        catch(Exception e){Debug.LogException(e);}
        finally
        {
            if(captured&&original!=null&&Hash(original)==SettingsHash)
            {
                PlayerSettings.SplashScreen.show=oldShow;
                AssetDatabase.SaveAssets();
                // Preserve exact prior bytes even if the Editor reserialized an unrelated setting.
                if(!File.ReadAllBytes(settings).SequenceEqual(original))File.WriteAllBytes(settings,original);
                if(Hash(File.ReadAllBytes(settings))!=SettingsHash)success=false;
                Debug.Log("STARTUP_SPLASH_SETTINGS_RESTORED "+Hash(File.ReadAllBytes(settings)));
            }
            EditorApplication.Exit(success?0:1);
        }
    }
    private static string Hash(byte[] bytes){using(var sha=SHA256.Create())return BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-","").ToLowerInvariant();}
}
