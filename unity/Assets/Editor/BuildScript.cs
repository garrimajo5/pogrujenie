using System;
using System.IO;
using Pogrujenie.Game;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Pogrujenie.EditorTools
{
    /// <summary>
    /// Сборка APK для Android: и из меню «Погружение», и в CI (GameCI вызывает BuildAndroid
    /// с аргументом -customBuildPath). Сцену Main создаёт сама, если её ещё нет.
    /// </summary>
    public static class BuildScript
    {
        public const string ScenePath = "Assets/Scenes/Main.unity";
        public const string AppId = "com.garrimajo5.pogrujenie";

        [MenuItem("Погружение/Создать сцену Main")]
        public static void EnsureScene()
        {
            if (!File.Exists(ScenePath))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(ScenePath));
                var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                new GameObject("Погружение").AddComponent<Bootstrap>();
                EditorSceneManager.SaveScene(scene, ScenePath);
                AssetDatabase.Refresh();
            }
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
        }

        static void ApplyAndroidSettings()
        {
            PlayerSettings.companyName = "garrimajo5";
            PlayerSettings.productName = "Погружение";
            PlayerSettings.bundleVersion = "0.1.0";
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, AppId);
            // 64-битные телефоны без 32-битного режима запускают только ARM64, а его даёт IL2CPP
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel26;
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.LandscapeLeft;
            EditorUserBuildSettings.buildAppBundle = false;
        }

        static string Arg(string name, string fallback)
        {
            var args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
                if (args[i] == name) return args[i + 1];
            return fallback;
        }

        [MenuItem("Погружение/Собрать APK")]
        public static void BuildAndroid()
        {
            EnsureScene();
            ApplyAndroidSettings();
            string path = Arg("-customBuildPath", "Build/Android/Pogrujenie.apk");
            if (!path.EndsWith(".apk")) path = Path.Combine(path, "Pogrujenie.apk");
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path)));
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { ScenePath },
                locationPathName = path,
                target = BuildTarget.Android,
                options = BuildOptions.None,
            });
            var sum = report.summary;
            Debug.Log($"Сборка: {sum.result}, {sum.totalSize / (1024 * 1024)} МБ, ошибок {sum.totalErrors}, файл {path}");
            if (sum.result != BuildResult.Succeeded && Application.isBatchMode) EditorApplication.Exit(1);
        }
    }
}
