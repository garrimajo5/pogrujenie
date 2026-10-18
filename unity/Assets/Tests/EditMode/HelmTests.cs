using System;
using NUnit.Framework;
using Pogrujenie.Core;

namespace Pogrujenie.Tests
{
    /// <summary>Ход корабля и течение: перенос js/ship.js (S.update) и течения из js/creatures.js.</summary>
    public class HelmTests
    {
        static void Run(ShipPhysics s, float seconds, float k = 1f, float cx = 0f, float cz = 0f)
        {
            int n = (int)Math.Round(seconds / 0.05f);
            for (int i = 0; i < n; i++) s.Update(0.05f, k, cx, cz);
        }

        [Test]
        public void БезВводаИТеченияКорабльСтоит()
        {
            var s = new ShipPhysics();
            Run(s, 5);
            Assert.AreEqual(ShipPhysics.StartX, s.X, 1e-4f);
            Assert.AreEqual(ShipPhysics.StartY, s.Y, 1e-4f);
            Assert.AreEqual(ShipPhysics.StartZ, s.Z, 1e-4f);
        }

        [Test]
        public void ПолныйХодВпередКНосу()
        {
            var s = new ShipPhysics { InThrust = 1 };
            Run(s, 1);
            Assert.Less(s.Z, ShipPhysics.StartZ - 1f, "за секунду разогнались к носу (−Z)");
            Run(s, 20);
            Assert.AreEqual(ShipPhysics.Forward / ShipPhysics.Drag, s.Speed, 0.05f, "полный ход ≈ 6,7 м/с");
            Assert.AreEqual(ShipPhysics.StartX, s.X, 1e-3f, "без штурвала идём прямо");
        }

        [Test]
        public void ЗаднийХодМедленнее()
        {
            var s = new ShipPhysics { InThrust = -1 };
            Run(s, 25);
            Assert.AreEqual(-ShipPhysics.Reverse / ShipPhysics.Drag, s.Speed, 0.05f, "задний ход ≈ 3,6 м/с");
            Assert.Greater(s.Z, ShipPhysics.StartZ + 30f, "пятимся к корме (+Z)");
        }

        [Test]
        public void ТягаЗависитОтДолиДвигателей()
        {
            var r = new Reactor();
            var base30 = new ShipPhysics { InThrust = 1 };
            Run(base30, 10, r.K(ShipSystem.Engines));

            r.Set(ShipSystem.Life, 0);
            r.Set(ShipSystem.Zap, 0);
            Assert.AreEqual(60, r.Set(ShipSystem.Engines, 60));
            var full = new ShipPhysics { InThrust = 1 };
            Run(full, 10, r.K(ShipSystem.Engines));
            Assert.AreEqual(base30.Speed * 2f, full.Speed, 0.05f, "двойная доля — вдвое быстрее");

            r.Set(ShipSystem.Engines, 0);
            var off = new ShipPhysics { InThrust = 1 };
            Run(off, 10, r.K(ShipSystem.Engines));
            Assert.AreEqual(0f, off.Speed, 1e-4f, "двигатели обесточены — хода нет");
            Assert.AreEqual(ShipPhysics.StartZ, off.Z, 1e-4f);
        }

        [Test]
        public void ПриАварийномСбросеКорабльТеряетХод()
        {
            var s = new ShipPhysics { InThrust = 1 };
            Run(s, 15);
            float v = s.Speed;
            Run(s, 6, 0f);
            Assert.Less(s.Speed, v * 0.1f, "без двигателей сопротивление воды гасит ход");
        }

        [Test]
        public void ШтурвалВправоПоворачиваетКПравомуБорту()
        {
            var s = new ShipPhysics { InThrust = 1, InTurn = 1 };
            Run(s, 3);
            Assert.Greater(s.Yaw, 0.5f, "курс растёт");
            Assert.Less(s.NoseX, -0.4f, "нос ушёл к правому борту (−X)");
            Assert.Less(s.X, ShipPhysics.StartX - 1f, "и корабль идёт туда же");
        }

        [Test]
        public void ШтурвалОтзываетсяПлавно()
        {
            var s = new ShipPhysics { InTurn = 1 };
            s.Update(0.05f, 1f, 0, 0);
            Assert.Less(s.Turn, 0.2f, "штурвал не прыгает в крайнее положение");
            Run(s, 3);
            Assert.AreEqual(1f, s.Turn, 0.01f);
            s.InTurn = 0;
            Run(s, 3);
            Assert.AreEqual(0f, s.Turn, 0.01f, "отпустили — штурвал вернулся");
        }

        [Test]
        public void ВсплытиеУпираетсяВЛёд()
        {
            var s = new ShipPhysics { InVert = 1 };
            Run(s, 2);
            Assert.Greater(s.Y, ShipPhysics.StartY + 2f, "всплываем");
            Run(s, 30);
            Assert.AreEqual(ShipPhysics.Ceiling, s.Y, 1e-4f, "выше 5 м подо льдом не подняться");
            Assert.AreEqual(0f, s.Vy, 1e-4f);
        }

        [Test]
        public void ПогружениеДоДна()
        {
            var s = new ShipPhysics { InVert = -1, Ground = (x, z) => -32f };
            Run(s, 30);
            Assert.AreEqual(-32f + ShipPhysics.Clearance, s.Y, 1e-4f, "корабль держится над дном");
            var deep = new ShipPhysics { InVert = -1 };
            Run(deep, 60);
            Assert.AreEqual(ShipPhysics.Deep, deep.Y, 1e-4f, "без дна — не глубже 96 м");
        }

        [Test]
        public void ТечениеСноситКорабль()
        {
            var c = new SeaCurrent();
            var s = new ShipPhysics();
            for (int i = 0; i < 200; i++) { c.Update(0.05f); s.Update(0.05f, 1f, c.X, c.Z); }
            float dx = s.X - ShipPhysics.StartX, dz = s.Z - ShipPhysics.StartZ;
            Assert.AreEqual(SeaCurrent.Speed * 10f, (float)Math.Sqrt(dx * dx + dz * dz), 0.01f, "0,3 м/с: за 10 с — 3 м");
            SeaCurrent.Vector(SeaCurrent.Dirs[0], out float ex, out float ez);
            Assert.AreEqual(ex * 10f, dx, 0.01f, "по направлению течения");
            Assert.AreEqual(ez * 10f, dz, 0.01f);
            Assert.Less(dx, 0f, "угол 0,7 в браузере — +X, в Unity x меняет знак");
        }

        [Test]
        public void ТечениеСноситДажеБезЭнергии()
        {
            var s = new ShipPhysics { InThrust = 1 };
            Run(s, 10, 0f, 0.3f, 0f);
            Assert.AreEqual(ShipPhysics.StartX + 3f, s.X, 0.01f);
        }

        [Test]
        public void ТечениеРазВТриМинутыМеняетНаправление()
        {
            var c = new SeaCurrent();
            int changes = 0, last = -1;
            c.Changed += i => { changes++; last = i; };
            for (int i = 0; i < 179 * 20; i++) c.Update(0.05f);
            Assert.AreEqual(0, changes, "первые три минуты течение прежнее");
            Assert.AreEqual(SeaCurrent.Dirs[0], c.Angle, 1e-4f);
            for (int i = 0; i < 2 * 20; i++) c.Update(0.05f);
            Assert.AreEqual(1, changes);
            Assert.AreEqual(1, last);
            Assert.AreEqual(SeaCurrent.Dirs[1], c.Target, 1e-5f);
            float a0 = c.Angle;
            for (int i = 0; i < 20; i++) c.Update(0.05f);
            Assert.AreNotEqual(a0, c.Angle, "поворачивает плавно");
            Assert.Greater(Math.Abs(c.Angle - SeaCurrent.Dirs[1]), 1f, "а не скачком");
            for (int i = 0; i < 170 * 20; i++) c.Update(0.05f);
            Assert.AreEqual(SeaCurrent.Dirs[1], c.Angle, 0.01f, "к концу трёх минут течение развернулось");
            float speed = (float)Math.Sqrt(c.X * c.X + c.Z * c.Z);
            Assert.AreEqual(SeaCurrent.Speed, speed, 1e-4f, "скорость течения всегда 0,3 м/с");
        }

        [Test]
        public void ТечениеИдётПоКругуИзШести()
        {
            var c = new SeaCurrent();
            for (int i = 0; i < 6 * 180 * 10 + 5; i++) c.Update(0.1f);
            Assert.AreEqual(0, c.Index, "после шести смен — снова первое направление");
        }

        [Test]
        public void КорабльНеУплываетЗаКрайКарты()
        {
            var s = new ShipPhysics { InThrust = 1 };
            Run(s, 120);
            Assert.AreEqual(-ShipPhysics.Edge, s.Z, 1e-3f);
        }

        [Test]
        public void СбросВозвращаетНаСтарт()
        {
            var s = new ShipPhysics { InThrust = 1, InTurn = 1 };
            Run(s, 5);
            s.Reset();
            Assert.AreEqual(ShipPhysics.StartZ, s.Z);
            Assert.AreEqual(0f, s.Yaw);
            Assert.AreEqual(0f, s.Speed);
            Assert.AreEqual(0f, s.InThrust);
        }
    }
}
