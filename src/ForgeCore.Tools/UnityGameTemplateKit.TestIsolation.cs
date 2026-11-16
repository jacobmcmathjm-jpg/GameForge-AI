namespace ForgeCore.Tools;
internal static partial class UnityGameTemplateKit
{
    private const string PlayModeIsolationSource="""
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
namespace ForgeCoreGameTemplates.Tests
{
    // Transient PlayMode state only; no scene/prefab assets are saved or changed.
    // Each test owns its physics fixture; the open demonstration must not join its combat.
    [SetUpFixture]public sealed class PlayModeBackgroundIsolation
    {
        private readonly List<GameObject> background=new List<GameObject>();
        [OneTimeSetUp]public void DisableAlreadyLoadedDemoRoots() {
            for(var i=0;i<SceneManager.sceneCount;i++)foreach(var root in SceneManager.GetSceneAt(i).GetRootGameObjects())
                if(root.activeSelf&&(root.GetComponentInChildren<SecurityCombatSession>(true)!=null||root.GetComponentInChildren<PrototypeGame>(true)!=null))
                {background.Add(root);root.SetActive(false);}
            Physics.SyncTransforms();
        }
        [OneTimeTearDown]public void RestoreTransientBackgroundRoots(){foreach(var root in background.Where(r=>r!=null))root.SetActive(true);background.Clear();}
    }
}
""";
}
