using System.IO;
using NUnit.Framework;
using Pogrujenie.EditorTools;
using Pogrujenie.Game;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Pogrujenie.Tests
{
    /// <summary>
    /// Сцена Main перед сборкой (BuildScript.PrepareScene). По отчёту qa/2026-10-19-1315-bridge-models-status,
    /// замечание 1: сцена тестера была создана до постоянных GUID в .meta, и в APK на «Погружение»
    /// висел компонент без скрипта. Такая сцена должна собираться заново, исправная — оставаться как есть.
    /// </summary>
    public class MainSceneTests
    {
        const string Dir = "Assets/TestScenesTmp";
        const string Tmp = Dir + "/Main.unity";

        [SetUp]
        public void Clean() => Remove();

        [TearDown]
        public void Remove()
        {
            // отпустить временную сцену, если она открыта, и удалить её вместе с папкой
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            if (AssetDatabase.IsValidFolder(Dir)) AssetDatabase.DeleteAsset(Dir);
            if (Directory.Exists(Dir)) Directory.Delete(Dir, true);
            if (File.Exists(Dir + ".meta")) File.Delete(Dir + ".meta");
            AssetDatabase.Refresh();
        }

        static string BootGuid()
        {
            var probe = new GameObject("проба").AddComponent<Bootstrap>();
            string guid = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(MonoScript.FromMonoBehaviour(probe)));
            Object.DestroyImmediate(probe.gameObject);
            Assert.IsFalse(string.IsNullOrEmpty(guid));
            return guid;
        }

        /// <summary>Сохранить сцену из объектов, которые делает fill, по пути Tmp.</summary>
        static void Save(System.Action<Scene> fill)
        {
            Directory.CreateDirectory(Dir);
            AssetDatabase.Refresh();
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            fill(scene);
            Assert.IsTrue(EditorSceneManager.SaveScene(scene, Tmp));
            // закрыть: проверка открывает сцену с диска, как при сборке
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        }

        static GameObject Make(Scene scene, string name)
        {
            var go = new GameObject(name);
            SceneManager.MoveGameObjectToScene(go, scene);
            return go;
        }

        [Test]
        public void СценыНетСоздаётсяСBootstrap()
        {
            Assert.AreEqual("сцены нет", BuildScript.SceneProblem(Tmp));
            Assert.IsTrue(BuildScript.PrepareScene(Tmp));
            Assert.IsNull(BuildScript.SceneProblem(Tmp));
            StringAssert.Contains(BootGuid(), File.ReadAllText(Tmp));
        }

        [Test]
        public void СтарыйGuidBootstrapСценаПересобирается()
        {
            // открытая сцена с потерянным скриптом может предупредить в лог — это и проверяем
            UnityEngine.TestTools.LogAssert.ignoreFailingMessages = true;
            string guid = BootGuid();
            Save(s => Make(s, "Погружение").AddComponent<Bootstrap>());
            // как у тестера: в сцене GUID скрипта, которого в проекте больше нет
            const string old = "0123456789abcdef0123456789abcdef";
            string text = File.ReadAllText(Tmp);
            StringAssert.Contains(guid, text);
            File.WriteAllText(Tmp, text.Replace(guid, old));
            AssetDatabase.ImportAsset(Tmp, ImportAssetOptions.ForceUpdate);
            StringAssert.StartsWith("компонентов без скрипта", BuildScript.SceneProblem(Tmp));

            Assert.IsTrue(BuildScript.PrepareScene(Tmp), "битую сцену надо собрать заново");
            Assert.IsNull(BuildScript.SceneProblem(Tmp));
            text = File.ReadAllText(Tmp);
            StringAssert.Contains(guid, text);
            StringAssert.DoesNotContain(old, text);
        }

        [Test]
        public void СценаБезBootstrapПересобирается()
        {
            Save(s => Make(s, "Погружение"));
            Assert.AreEqual("Bootstrap в сцене: 0", BuildScript.SceneProblem(Tmp));
            Assert.IsTrue(BuildScript.PrepareScene(Tmp));
            Assert.IsNull(BuildScript.SceneProblem(Tmp));
        }

        [Test]
        public void ИсправнаяСценаНеТрогается()
        {
            Save(s =>
            {
                Make(s, "Погружение").AddComponent<Bootstrap>();
                Make(s, "Метка");
            });
            string before = File.ReadAllText(Tmp);
            Assert.IsNull(BuildScript.SceneProblem(Tmp));
            Assert.IsFalse(BuildScript.PrepareScene(Tmp));
            Assert.AreEqual(before, File.ReadAllText(Tmp), "исправная сцена не пересохраняется");
            StringAssert.Contains("Метка", File.ReadAllText(Tmp));
        }
    }
}
