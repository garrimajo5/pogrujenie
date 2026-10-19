using System.Collections;
using System.Linq;
using NUnit.Framework;
using Pogrujenie.Core;
using Pogrujenie.Game;
using UnityEngine;
using UnityEngine.TestTools;

namespace Pogrujenie.Tests
{
    /// <summary>
    /// Сохранение в сцене: поднятый образец через секунду пишется в PlayerPrefs, при новом запуске
    /// трюм и дно восстанавливаются. В тестах свой ключ, чтобы не задеть другие тесты.
    /// </summary>
    public class SaveSceneTests
    {
        const string Key = "pogrujenie.save.test";
        Bootstrap boot;

        [SetUp]
        public void SetUp()
        {
            PlayerPrefs.DeleteKey(Key);
            Bootstrap.TestSaveKey = Key;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (boot != null) Object.Destroy(boot.gameObject);
            Bootstrap.TestSaveKey = null;
            PlayerPrefs.DeleteKey(Key);
            yield return null;
        }

        Bootstrap Start()
        {
            if (boot != null) Object.DestroyImmediate(boot.gameObject);
            boot = new GameObject("Погружение").AddComponent<Bootstrap>();
            return boot;
        }

        [UnityTest]
        public IEnumerator ПоднятыйОбразецСохраняетсяИВосстанавливается()
        {
            var b = Start();
            yield return null;
            Assert.AreEqual(0, b.Samples.Cargo.Count, "сохранения нет — трюм пуст");
            var s = b.Samples.All[0];
            for (int i = 0; i < 40 && b.Samples.State != GrabState.Back; i++) { b.Samples.Hold(s, 0.05f); b.Step(0.05f); }
            for (int i = 0; i < 40 && b.Samples.State != GrabState.Idle; i++) b.Step(0.05f);
            Assert.AreEqual(1, b.Samples.Cargo.Count);
            Assert.IsTrue(b.SaveDirty);
            Assert.IsFalse(PlayerPrefs.HasKey(Key), "пишем не сразу, а через секунду");
            for (int i = 0; i < 22; i++) b.Step(0.05f);
            Assert.IsFalse(b.SaveDirty);
            Assert.AreEqual("{\"v\":1,\"cargo\":[{\"kind\":\"moss\",\"i\":0}],\"got\":[0]}", PlayerPrefs.GetString(Key));

            // перезапуск игры
            b = Start();
            yield return null;
            yield return null;
            Assert.AreEqual(1, b.Samples.Cargo.Count, "груз трюма на месте");
            Assert.IsTrue(b.Samples.All[0].Taken, "поднятый образец не вернулся на дно");
            Assert.AreEqual(13, b.SampleView.Shown, "на дне рисуются 13 образцов");
            Assert.IsFalse(b.SaveDirty, "после чтения перезаписывать нечего");
        }

        [UnityTest]
        public IEnumerator БитоеСохранение_ЧистыйТрюм()
        {
            PlayerPrefs.SetString(Key, "{\"v\":1,\"cargo\":[");
            var b = Start();
            yield return null;
            Assert.AreEqual(0, b.Samples.Cargo.Count);
            Assert.AreEqual(14, b.Samples.All.Count(x => !x.Taken));
            Assert.AreEqual(14, b.SampleView.Shown);
        }

        [UnityTest]
        public IEnumerator СворачиваниеПишетСразу()
        {
            var b = Start();
            yield return null;
            b.Samples.Restore(new[] { 2 }, new[] { new CargoItem(SampleKind.Moss, 2) });
            b.Samples.Reset(); // изменение: сброс трюма
            Assert.IsTrue(b.SaveDirty);
            b.SendMessage("OnApplicationPause", true);
            Assert.IsFalse(b.SaveDirty);
            Assert.AreEqual("{\"v\":1,\"cargo\":[],\"got\":[]}", PlayerPrefs.GetString(Key));
        }

        [UnityTest]
        public IEnumerator ВТестовойСценеБезКлючаНеСохраняет()
        {
            Bootstrap.TestSaveKey = null;
            var b = Start();
            yield return null;
            Assert.IsNull(Bootstrap.SaveKey);
            b.Samples.Reset();
            Assert.IsFalse(b.Save());
        }
    }
}
