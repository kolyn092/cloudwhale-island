using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

namespace CloudWhale.Editor
{
    public static class BootstrapProject
    {
        public static void Initialize()
        {
            const string scenePath = "Assets/Scenes/Main.unity";
            Directory.CreateDirectory("Assets/Scenes");
            PlayerSettings.productName = "CloudWhale Island";

            if (!File.Exists(scenePath))
            {
                var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                EditorSceneManager.SaveScene(scene, scenePath);
            }

            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(scenePath, true) };
            AssetDatabase.SaveAssets();
        }
    }
}
