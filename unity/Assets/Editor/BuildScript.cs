using System;
using System.IO;
using Pogrujenie.Game;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

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
            PrepareScene(ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
        }

        /// <summary>
        /// Сцена готова к сборке: есть, в ней ровно один Bootstrap и нет компонентов без скрипта.
        /// Иначе сцена собирается заново. Сцена, созданная до постоянных GUID в .meta (#61), ссылалась
        /// на старый GUID Bootstrap — в APK «The referenced script … ('Погружение') is missing!».
        /// Возвращает true, если сцена (пере)создана.
        /// </summary>
        public static bool PrepareScene(string path)
        {
            string problem = SceneProblem(path);
            if (problem == null) return false;
            if (File.Exists(path)) Debug.Log($"Сцена {path} собирается заново: {problem}");
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            new GameObject("Погружение").AddComponent<Bootstrap>();
            EditorSceneManager.SaveScene(scene, path);
            AssetDatabase.Refresh();
            return true;
        }

        /// <summary>Что не так со сценой по пути path; null — всё в порядке.</summary>
        public static string SceneProblem(string path)
        {
            if (!File.Exists(path)) return "сцены нет";
            var scene = SceneManager.GetSceneByPath(path);
            bool opened = false;
            if (!scene.isLoaded)
            {
                scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
                opened = true;
            }
            int missing = 0, boots = 0;
            foreach (var root in scene.GetRootGameObjects())
                foreach (var t in root.GetComponentsInChildren<Transform>(true))
                {
                    missing += GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject);
                    boots += t.GetComponents<Bootstrap>().Length;
                }
            if (opened) EditorSceneManager.CloseScene(scene, true);
            if (missing > 0) return $"компонентов без скрипта: {missing}";
            if (boots != 1) return $"Bootstrap в сцене: {boots}";
            return null;
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
