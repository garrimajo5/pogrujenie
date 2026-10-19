using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Pogrujenie.Core;

namespace Pogrujenie.Tests
{
    /// <summary>
    /// Образцы на дне и подбор лебёдкой через открытый нижний шлюз (перенос js/manip.js):
    /// места как в браузере, захват удержанием 1,4 с, подъём 1,2 с, образец — в груз трюма.
    /// </summary>
    public class SamplesTests
    {
        const float Stern = 0f; // лицом к корме: вперёд = +Z

        /// <summary>Образцы из браузерной версии (G.Manip.samples): вид, x, y, z в осях браузера.</summary>
        static readonly (SampleKind k, float x, float y, float z)[] Browser =
        {
            (SampleKind.Moss, 31.63f, -37.41f, 94.44f), (SampleKind.Moss, -24.62f, -34.79f, -79.71f),
            (SampleKind.Moss, -111.67f, -41.52f, 74.62f), (SampleKind.Moss, -42.46f, -43.73f, 34.77f),
            (SampleKind.Moss, -24.08f, -50.75f, 173.59f), (SampleKind.Moss, 110.62f, -34.69f, -20.3f),
            (SampleKind.Moss, 19.51f, -44.82f, 22.68f), (SampleKind.Moss, 117.75f, -39.64f, -47.64f),
            (SampleKind.Shell, 13.33f, -35.5f, 76.45f), (SampleKind.Shell, -67.48f, -38.79f, 83.59f),
            (SampleKind.Coral, -55.35f, -40.8f, 23.24f), (SampleKind.Coral, 34.9f, -42.98f, 57.2f),
            (SampleKind.Coral, 56.48f, -39.37f, -68.29f), (SampleKind.Spine, -52.04f, -42.69f, 84.95f),
        };

        /// <summary>Корабль кормой к образцу: шлюз (Airlock.Z = 22 м за центром) на высоте above над ним.</summary>
        static ShipPhysics Over(Sample s, float above, float yaw = 0f)
        {
            var ship = new ShipPhysics { Yaw = yaw };
            ship.X = s.X - (float)System.Math.Sin(yaw) * Airlock.Z;
            ship.Z = s.Z - (float)System.Math.Cos(yaw) * Airlock.Z;
            ship.Y = s.Y + above - Airlock.Y;
            return ship;
        }

        static Airlock OpenLock()
        {
            var a = new Airlock();
            a.Toggle();
            for (int i = 0; i < 50; i++) a.Update(0.05f);
            Assert.IsTrue(a.FullyOpen);
            return a;
        }

        /// <summary>Держать кнопку seconds секунд шагами по 0,05 с (удержание и шаг каждый кадр).</summary>
        static void Hold(Samples m, Sample s, float seconds)
        {
            int n = (int)System.Math.Round(seconds / 0.05f);
            for (int i = 0; i < n; i++) { m.Hold(s, 0.05f); m.Update(0.05f); }
        }

        static void Wait(Samples m, float seconds)
        {
            int n = (int)System.Math.Round(seconds / 0.05f);
            for (int i = 0; i < n; i++) m.Update(0.05f);
        }

        [Test]
        public void ЧетырнадцатьОбразцовТамЖеЧтоВБраузере()
        {
            var m = new Samples();
            Assert.AreEqual(Browser.Length, m.All.Count);
            Assert.AreEqual(8, m.All.Count(s => s.Kind == SampleKind.Moss));
            Assert.AreEqual(2, m.All.Count(s => s.Kind == SampleKind.Shell));
            Assert.AreEqual(3, m.All.Count(s => s.Kind == SampleKind.Coral));
            Assert.AreEqual(1, m.All.Count(s => s.Kind == SampleKind.Spine));
            for (int i = 0; i < Browser.Length; i++)
            {
                var b = Browser[i]; var s = m.All[i];
                Assert.AreEqual(i, s.I);
                Assert.AreEqual(b.k, s.Kind, $"образец {i}");
                Assert.AreEqual(-b.x, s.X, 1e-4f, "x меняет знак: правый борт в Unity — −X");
                Assert.AreEqual(b.z, s.Z, 1e-4f);
                Assert.AreEqual(b.y, s.Y, 0.03f, $"образец {i} лежит на дне, как в браузере");
                Assert.Greater(s.Y, Seabed.Height(s.X, s.Z), "над дном, не в нём");
                Assert.IsFalse(s.Taken);
            }
            Assert.AreEqual(0, m.Cargo.Count);
            Assert.AreEqual(GrabState.Idle, m.State);
        }

        [Test]
        public void ИменаИЦветаПоВидам()
        {
            Assert.AreEqual("Светящийся мох", Samples.Name(SampleKind.Moss));
            Assert.AreEqual("Ракушка моллюска", Samples.Name(SampleKind.Shell));
            Assert.AreEqual("Ветка огнецвета", Samples.Name(SampleKind.Coral));
            Assert.AreEqual("Игла шипоежа", Samples.Name(SampleKind.Spine));
            Assert.AreEqual(0x9dffd0, Samples.Color(SampleKind.Moss));
            Assert.AreEqual(0xff8a3c, Samples.Color(SampleKind.Coral));
        }

        [Test]
        public void ШлюзПодКормойПротивНоса()
        {
            foreach (float yaw in new[] { 0f, 0.7f, 1.5707963f, -2.4f, 3.1f })
            {
                var ship = new ShipPhysics { X = 10f, Y = -20f, Z = 50f, Yaw = yaw };
                Samples.LockPoint(ship, out float x, out float y, out float z);
                Assert.AreEqual(-20f + Airlock.Y, y, 1e-4f);
                // шлюз в Airlock.Z за центром, в сторону, противоположную носу
                Assert.AreEqual(10f - ship.NoseX * Airlock.Z, x, 1e-3f, $"yaw {yaw}");
                Assert.AreEqual(50f - ship.NoseZ * Airlock.Z, z, 1e-3f, $"yaw {yaw}");
            }
            var s0 = new ShipPhysics { X = 0f, Y = -20f, Z = 95f, Yaw = 0f };
            Samples.LockPoint(s0, out float lx, out float ly, out float lz);
            Assert.AreEqual(0f, lx, 1e-4f);
            Assert.AreEqual(95f + Airlock.Z, lz, 1e-4f);
        }

        [Test]
        public void ДотягиваетсяТолькоВниз14Метров()
        {
            var m = new Samples();
            var s = m.All[0];
            Assert.AreSame(s, m.Find(s.X, s.Y + 4f, s.Z), "4 м над образцом");
            Assert.AreSame(s, m.Find(s.X + 3f, s.Y + 13f, s.Z), "почти на пределе");
            Assert.IsNull(m.Find(s.X, s.Y + 15f, s.Z), "дальше 14 м не дотянуться");
            Assert.IsNull(m.Find(s.X, s.Y - 1f, s.Z), "шлюз ниже образца — рука вверх не тянется");
            Assert.IsNull(m.Find(0f, -21.15f, 117f), "у места старта образцов нет");
            s.Taken = true;
            Assert.IsNull(m.Find(s.X, s.Y + 4f, s.Z), "поднятый не находится");
        }

        [Test]
        public void ВыбираетБлижайший_ПоднятыйПропускает()
        {
            var m = new Samples();
            // ракушка 9 и игла 13 лежат в 16 м друг от друга: шлюз над ними ближе к ракушке
            var a = m.All[9]; var b = m.All[13];
            float x = a.X + (b.X - a.X) * 0.4f, z = a.Z + (b.Z - a.Z) * 0.4f, y = a.Y + 3f;
            Assert.Less(m.DistanceTo(a, x, y, z), m.DistanceTo(b, x, y, z));
            Assert.Less(m.DistanceTo(b, x, y, z), Samples.Reach, "дотянуться можно до обоих");
            Assert.AreSame(a, m.Find(x, y, z));
            a.Taken = true;
            Assert.AreSame(b, m.Find(x, y, z), "ракушку подняли — следующая игла");
        }

        [Test]
        public void УдержаниеЗахватПодъёмИГрузТрюма()
        {
            var m = new Samples();
            var msgs = new List<string>();
            m.Message += msgs.Add;
            var s = m.All[0];
            int v0 = m.Version;

            Hold(m, s, 0.7f);
            Assert.AreEqual(GrabState.Reach, m.State);
            Assert.AreSame(s, m.Target);
            Assert.AreEqual(0.5f, m.Progress, 0.01f, "половина захвата за 0,7 с");
            Assert.IsFalse(s.Taken);

            Hold(m, s, 0.75f);
            Assert.AreEqual(GrabState.Back, m.State, "захват за 1,4 с удержания");
            Assert.IsTrue(s.Taken, "образца больше нет на дне");
            Assert.AreNotEqual(v0, m.Version);
            Assert.IsTrue(m.Carrying);
            Assert.Contains("Образец захвачен", msgs);
            Assert.AreEqual(0, m.Cargo.Count, "ещё поднимается");

            Wait(m, 0.55f);
            Assert.AreEqual(0.5f, m.Lift, 0.06f, "полпути к шлюзу");
            Wait(m, 0.7f);
            Assert.AreEqual(GrabState.Idle, m.State);
            Assert.IsNull(m.Target);
            Assert.AreEqual(1, m.Cargo.Count);
            Assert.AreEqual(SampleKind.Moss, m.Cargo[0].Kind);
            Assert.AreEqual(0, m.Cargo[0].I);
            Assert.IsTrue(m.Got.Contains(0));
            Assert.AreEqual("В трюме: светящийся мох (образцов 1)", msgs.Last());
        }

        [Test]
        public void ОтпустилРаньше_ЗахватСбрасывается()
        {
            var m = new Samples();
            var s = m.All[0];
            Hold(m, s, 1.0f);
            Assert.AreEqual(GrabState.Reach, m.State);
            Wait(m, 0.2f);
            Assert.Less(m.Progress, 0.5f, "без удержания захват уходит назад");
            Assert.AreEqual(GrabState.Reach, m.State);
            Wait(m, 0.3f);
            Assert.AreEqual(GrabState.Idle, m.State);
            Assert.IsNull(m.Target);
            Assert.IsFalse(s.Taken);
            Assert.AreEqual(0, m.Cargo.Count);
        }

        [Test]
        public void ВоВремяПодъёмаВторойНеЗахватить()
        {
            var m = new Samples();
            Hold(m, m.All[0], 1.5f);
            Assert.AreEqual(GrabState.Back, m.State);
            m.Hold(m.All[1], 0.05f);
            Assert.AreSame(m.All[0], m.Target, "рука занята");
            Assert.IsFalse(m.All[1].Taken);
            m.Hold(null, 0.05f); // пустой захват ничего не ломает
        }

        [Test]
        public void ПостЛебёдкиРаботаетТолькоПриОткрытомШлюзеНадОбразцом()
        {
            var m = new Samples();
            var s = m.All[0];
            var ship = Over(s, 4f);
            var closed = new Airlock();
            var post = m.MakePost(closed, ship);
            Assert.AreEqual(0f, post.X);
            Assert.AreEqual(Samples.PostZ, post.Z);
            Assert.IsNotNull(post.Hold, "пост удержания");
            Assert.IsFalse(post.On(), "шлюз закрыт");

            var open = OpenLock();
            post = m.MakePost(open, ship);
            Assert.IsTrue(post.On(), "шлюз открыт, внизу мох");
            Assert.AreEqual("Достать образец", post.Label());

            var far = new ShipPhysics(); // у места старта образцов нет
            Assert.IsFalse(m.MakePost(open, far).On());
            Assert.IsFalse(m.MakePost(open, Over(s, 16f)).On(), "высоко");

            post.Do(); // короткое нажатие ничего не делает
            Assert.AreEqual(GrabState.Idle, m.State);
            for (int i = 0; i < 14; i++) { post.Hold(0.05f); m.Update(0.05f); }
            Assert.AreEqual(GrabState.Reach, m.State);
            Assert.AreEqual("Захват 50%", post.Label());
            for (int i = 0; i < 16; i++) { post.Hold(0.05f); m.Update(0.05f); }
            Assert.AreEqual(GrabState.Back, m.State);
            Assert.AreEqual("Поднимаю…", post.Label());
            Assert.IsTrue(post.On(), "пока поднимается, пост остаётся");
            for (int i = 0; i < 30; i++) { post.Hold(0.05f); m.Update(0.05f); }
            Assert.AreEqual(1, m.Cargo.Count);
            Assert.IsFalse(post.On(), "больше над шлюзом ничего нет");
        }

        [Test]
        public void ПостЛебёдкиНеПерекрываетПостШлюза()
        {
            var layout = new ShipLayout();
            var m = new Samples();
            var s = m.All[0];
            var ship = Over(s, 4f);
            var winch = m.MakePost(layout.Lock, ship);
            layout.Inter.Add(winch);
            layout.Lock.Toggle();
            for (int i = 0; i < 50; i++) layout.Update(0.05f);
            Assert.IsTrue(layout.Lock.FullyOpen);

            Assert.IsTrue(layout.CanStand(0f, Samples.PostZ + 0.1f), "к лебёдке можно подойти");
            Assert.AreSame(winch, layout.Nearest(0f, Samples.PostZ + 0.1f, Stern));
            var lockPost = layout.Nearest(0f, 21f, Stern);
            Assert.IsNotNull(lockPost);
            Assert.AreEqual("Закрыть шлюз", lockPost.Label(), "над крышкой — пост шлюза, его можно закрыть");
        }

        /// <summary>Мировая точка → оси корабля (обратно Samples.ToWorld).</summary>
        static void ToShip(ShipPhysics s, float x, float y, float z, out float lx, out float ly, out float lz)
        {
            float dx = x - s.X, dz = z - s.Z, sin = (float)System.Math.Sin(s.Yaw), cos = (float)System.Math.Cos(s.Yaw);
            lx = cos * dx - sin * dz; ly = y - s.Y; lz = sin * dx + cos * dz;
        }

        [Test]
        public void ВОсиКорабляКакLockPoint()
        {
            foreach (float yaw in new[] { 0f, 0.7f, -2.4f, 3.1f })
            {
                var ship = new ShipPhysics { X = 10f, Y = -20f, Z = 50f, Yaw = yaw };
                Samples.LockPoint(ship, out float ax, out float ay, out float az);
                Samples.ToWorld(ship, 0f, Airlock.Y, Airlock.Z, out float bx, out float by, out float bz);
                Assert.AreEqual(ax, bx, 1e-3f); Assert.AreEqual(ay, by, 1e-4f); Assert.AreEqual(az, bz, 1e-3f);
                // правый борт (+x корабля в Unity — левый лицом к носу) и обратно
                Samples.ToWorld(ship, 1.5f, 0.3f, 4f, out float wx, out float wy, out float wz);
                ToShip(ship, wx, wy, wz, out float lx, out float ly, out float lz);
                Assert.AreEqual(1.5f, lx, 1e-3f); Assert.AreEqual(0.3f, ly, 1e-4f); Assert.AreEqual(4f, lz, 1e-3f);
            }
            // Yaw = π/2: +x корабля смотрит в −Z мира, как Quaternion.Euler(0, 90, 0) * (1, 0, 0)
            var q = new ShipPhysics { Yaw = (float)(System.Math.PI / 2) };
            Samples.ToWorld(q, 1f, 0f, 0f, out float qx, out _, out float qz);
            Assert.AreEqual(0f, qx, 1e-4f); Assert.AreEqual(-1f, qz, 1e-4f);
        }

        [Test]
        public void ЗахватВШахтеПриЗакрытойКрышкеИПодДнищемПриОткрытой()
        {
            var m = new Samples();
            var ship = new ShipPhysics { X = 3f, Y = -20f, Z = 40f, Yaw = 0.9f };
            // шкив под решёткой, внутри корпуса, над шахтой
            Samples.PulleyPoint(ship, out float px, out float py, out float pz);
            ToShip(ship, px, py, pz, out float plx, out float ply, out float plz);
            Assert.AreEqual(0f, plx, 1e-3f); Assert.AreEqual(Airlock.Z, plz, 1e-3f);
            Assert.Less(ply, 0f, "под полом трюма");
            Assert.Greater(ply, -ShipLayout.ShaftDepth);
            Assert.IsTrue(HullPlan.Inside(plx, ply, plz));

            // закрыто: захват в шахте над крышкой, внутри корпуса
            m.HookPoint(ship, 0f, out float hx, out float hy, out float hz);
            ToShip(ship, hx, hy, hz, out float lx, out float ly, out float lz);
            Assert.AreEqual(0f, lx, 1e-3f); Assert.AreEqual(Airlock.Z, lz, 1e-3f);
            Assert.Less(ly, ply, "ниже шкива");
            Assert.Greater(ly, Airlock.Y + Airlock.Thick, "выше закрытой крышки");
            Assert.IsTrue(HullPlan.Inside(lx, ly, lz));
            // пока крышка открыта наполовину — захват ещё в шахте
            m.HookPoint(ship, 0.5f, out _, out float halfY, out _);
            Assert.AreEqual(hy, halfY, 1e-4f);
            // открыто: под днищем, снаружи корпуса
            m.HookPoint(ship, 1f, out hx, out hy, out hz);
            ToShip(ship, hx, hy, hz, out lx, out ly, out lz);
            Assert.Less(ly, Airlock.Y);
            Assert.IsFalse(HullPlan.Inside(lx, ly, lz), "вышел из шлюза в воду");
            Assert.AreEqual(Samples.RestOpenY, ly, 1e-4f);
            // опускается плавно, без прыжков
            float prev = Samples.RestHeight(0f);
            for (float t = 0.05f; t <= 1.001f; t += 0.05f)
            {
                float h = Samples.RestHeight(t);
                Assert.LessOrEqual(h, prev + 1e-5f);
                Assert.Less(prev - h, 0.2f, $"t = {t}");
                prev = h;
            }
        }

        [Test]
        public void ЗахватТянетсяКОбразцуИНесётЕгоКШлюзу()
        {
            var m = new Samples();
            var s = m.All[5];
            var ship = Over(s, 6f);
            m.HookPoint(ship, 1f, out float rx, out float ry, out float rz);
            float full = m.DistanceTo(s, rx, ry, rz);
            Assert.Greater(full, 4f);

            // половина захвата — захват на полпути от покоя к образцу
            Hold(m, s, 0.7f);
            Assert.AreEqual(GrabState.Reach, m.State);
            m.HookPoint(ship, 1f, out float x, out float y, out float z);
            Assert.AreEqual(full * (1f - m.Progress), m.DistanceTo(s, x, y, z), 0.01f);
            Assert.Less(m.DistanceTo(s, x, y, z), full * 0.6f);

            // полный захват — захват у образца
            Hold(m, s, 0.75f);
            Assert.AreEqual(GrabState.Back, m.State);
            m.HookPoint(ship, 1f, out x, out y, out z);
            Assert.Less(m.DistanceTo(s, x, y, z), full * 0.1f, "захват на образце");
            // подъём: обратно к шлюзу
            Wait(m, 0.6f);
            m.HookPoint(ship, 1f, out x, out y, out z);
            float back = m.DistanceTo(s, x, y, z);
            Assert.AreEqual(full * m.Lift, back, 0.05f);
            Assert.Greater(back, full * 0.3f);
            // в трюме — захват снова в покое под шлюзом
            Wait(m, 0.8f);
            Assert.AreEqual(GrabState.Idle, m.State);
            m.HookPoint(ship, 1f, out x, out y, out z);
            Assert.AreEqual(rx, x, 1e-4f); Assert.AreEqual(ry, y, 1e-4f); Assert.AreEqual(rz, z, 1e-4f);
        }

        [Test]
        public void ОтпустилКнопку_ЗахватВозвращается()
        {
            var m = new Samples();
            var s = m.All[2];
            var ship = Over(s, 5f);
            m.HookPoint(ship, 1f, out float rx, out float ry, out float rz);
            Hold(m, s, 1.0f);
            m.HookPoint(ship, 1f, out float x, out float y, out float z);
            Assert.Greater(System.Math.Abs(y - ry), 1f, "захват ушёл вниз");
            Wait(m, 1f);
            m.HookPoint(ship, 1f, out x, out y, out z);
            Assert.AreEqual(ry, y, 1e-4f, "вернулся под шлюз");
        }

        [Test]
        public void СбросВозвращаетОбразцыНаДно()
        {
            var m = new Samples();
            Hold(m, m.All[3], 1.5f);
            Wait(m, 1.5f);
            Assert.AreEqual(1, m.Cargo.Count);
            int v = m.Version;
            m.Reset();
            Assert.AreEqual(0, m.Cargo.Count);
            Assert.AreEqual(0, m.Got.Count);
            Assert.IsTrue(m.All.All(s => !s.Taken));
            Assert.AreEqual(GrabState.Idle, m.State);
            Assert.AreNotEqual(v, m.Version);
        }
    }
}
