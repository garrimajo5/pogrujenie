using System.Collections.Generic;
using NUnit.Framework;
using Pogrujenie.Core;

namespace Pogrujenie.Tests
{
    /// <summary>Серпохвосты (initPreds/predUpdate, E.onPing, E.onZap в js/creatures.js).</summary>
    public class EcoPredTests
    {
        const float Flat = -40f;

        /// <summary>Ровное дно на −40 м, только серпохвосты; корабль далеко, тихо и без света.</summary>
        static EcoWorld Alone(out EcoPred preds)
        {
            var w = new EcoWorld { Ground = (x, z) => Flat };
            w.Ship.Pos = new EcoVec(500, -30, 500);
            preds = w.Add(new EcoPred());
            return w;
        }

        /// <summary>Первый хищник голоден и готов напасть, корабль — в dist м по +X на его высоте.</summary>
        static EcoPred.Pred Ready(EcoWorld w, EcoPred preds, float dist)
        {
            var pr = preds.All[0];
            pr.Hunger = 0.5f;
            pr.Cooldown = 0f;
            w.Ship.Pos = pr.Agent.P + new EcoVec(dist, 0, 0);
            return pr;
        }

        static List<EcoEvent> Notes(EcoWorld w, string note)
        {
            var list = new List<EcoEvent>();
            foreach (var ev in w.Bus.Pending) if (ev.SpeciesId == "pred" && ev.Note == note) list.Add(ev);
            return list;
        }

        [Test]
        public void ТриСерпохвостаНаМестахИзБраузера()
        {
            var w = new EcoWorld { Ground = Seabed.Height };
            var preds = w.Add(new EcoPred());
            Assert.AreEqual(3, preds.All.Count);
            Assert.AreEqual(3, w.Count("pred"));
            float[,] js = { { -60, 20 }, { 70, -20 }, { 10, -110 } };
            for (int i = 0; i < 3; i++)
            {
                var pr = preds.All[i];
                Assert.AreEqual(-js[i, 0], pr.Agent.P.X, 1e-4f, "x со знаком минус");
                Assert.AreEqual(js[i, 1], pr.Agent.P.Z, 1e-4f);
                float y = System.Math.Min(Seabed.Height(pr.Agent.P.X, pr.Agent.P.Z) + 8f, -6f);
                Assert.AreEqual(y, pr.Agent.P.Y, 1e-4f, "8 м над дном, не выше −6");
                Assert.AreEqual(EcoPred.Mode.Patrol, pr.Mode);
                Assert.That(pr.Hunger, Is.InRange(0.1f, 0.5f));
                Assert.AreEqual(20f, pr.Cooldown, 1e-5f, "первые 20 с на корабль не нападает");
            }
        }

        [Test]
        public void ПрожекторПриманиваетБлиже32Метров()
        {
            var w = Alone(out var preds);
            var pr = Ready(w, preds, 40f);
            w.Ship.Light = true;
            w.Step(EcoWorld.MaxStep);
            Assert.AreEqual(EcoPred.Mode.Patrol, pr.Mode, "дальше 32 м свет не видит");

            w = Alone(out preds);
            pr = Ready(w, preds, 25f);
            w.Ship.Light = true;
            w.Step(EcoWorld.MaxStep);
            Assert.AreEqual(EcoPred.Mode.Charge, pr.Mode, "свет ближе 32 м — бросок");
            Assert.AreEqual(EcoState.Hunt, pr.Agent.State);
            var ev = Notes(w, "ship");
            Assert.AreEqual(1, ev.Count);
            StringAssert.Contains("привлёк свет", ev[0].Text);
            Assert.IsTrue(ev[0].IsObservation, "заметка журнала «идёт на таран, если горит прожектор»");
        }

        [Test]
        public void БезСветаИТихоНеНападает()
        {
            var w = Alone(out var preds);
            var pr = Ready(w, preds, 15f);
            w.Run(60f);
            Assert.AreEqual(0, w.Hits, "тёмный тихий корабль хищник не трогает");
            Assert.AreNotEqual(EcoPred.Mode.Charge, pr.Mode);
            Assert.AreEqual(0, Notes(w, "ship").Count + Notes(w, "hear").Count + Notes(w, "ping").Count);
        }

        [Test]
        public void СытыйИлиПослеУкусаНеНападает()
        {
            var w = Alone(out var preds);
            var pr = Ready(w, preds, 20f);
            pr.Hunger = 0.1f;
            w.Ship.Light = true;
            w.Step(EcoWorld.MaxStep);
            Assert.AreEqual(EcoPred.Mode.Patrol, pr.Mode, "сытый (голод ≤ 0,2) не нападает");

            w = Alone(out preds);
            pr = Ready(w, preds, 20f);
            pr.Cooldown = 5f;
            w.Ship.Light = true;
            w.Step(EcoWorld.MaxStep);
            Assert.AreEqual(EcoPred.Mode.Patrol, pr.Mode, "пауза не прошла — не нападает");
        }

        [Test]
        public void СлышитДвигателиБлиже24Метров()
        {
            var w = Alone(out var preds);
            var pr = Ready(w, preds, 30f);
            w.Ship.Speed = 4f;
            w.Step(EcoWorld.MaxStep);
            Assert.AreEqual(EcoPred.Mode.Patrol, pr.Mode, "дальше 24 м двигатели не слышно");

            w = Alone(out preds);
            pr = Ready(w, preds, 20f);
            w.Ship.Speed = 2.5f;
            w.Step(EcoWorld.MaxStep);
            Assert.AreEqual(EcoPred.Mode.Patrol, pr.Mode, "малый ход (≤ 3 м/с) не слышно");

            w = Alone(out preds);
            pr = Ready(w, preds, 20f);
            w.Ship.Speed = -4f; // задний ход шумит так же
            w.Step(EcoWorld.MaxStep);
            Assert.AreEqual(EcoPred.Mode.Charge, pr.Mode);
            var ev = Notes(w, "hear");
            Assert.AreEqual(1, ev.Count);
            StringAssert.Contains("услышал двигатели", ev[0].Text);
        }

        [Test]
        public void ТаранОтдаётсяСобытиемShipHit()
        {
            var w = Alone(out var preds);
            var pr = Ready(w, preds, 25f);
            w.Ship.Light = true;
            var hits = new List<ShipHit>();
            w.ShipStruck += h => hits.Add(h);
            for (float t = 0; t < 10f && hits.Count == 0; t += EcoWorld.MaxStep) w.Step(EcoWorld.MaxStep);
            Assert.AreEqual(1, hits.Count, "за 10 с броска хищник дотянулся до корабля");
            Assert.AreEqual(1, w.Hits);
            Assert.AreEqual("pred", hits[0].SpeciesId);
            Assert.AreEqual("bite", hits[0].Kind);
            Assert.AreEqual(EcoPred.BiteDamage, hits[0].Damage);
            Assert.AreEqual(12, hits[0].Damage, "как Game.hullHit(12, 'bite') в браузере");
            Assert.Less(EcoVec.Distance(hits[0].Pos, w.Ship.Pos), EcoPred.BiteDist);
            Assert.IsTrue(w.LastHit.HasValue);
            var bite = Notes(w, "bite");
            Assert.AreEqual(1, bite.Count, "укус слышен на приборах");
            StringAssert.Contains("укусил корпус", bite[0].Text);
            Assert.AreEqual(EcoPred.Mode.Retreat, pr.Mode, "после укуса уходит");
            Assert.AreEqual(EcoState.Flee, pr.Agent.State);
            Assert.AreEqual(EcoPred.BiteCooldown, pr.Cooldown, 1e-4f);

            w.Run(14f);
            Assert.Greater(pr.Agent.DistanceTo(w.Ship.Pos), 50f, "отошёл от корабля");
            w.Run(6f);
            Assert.AreEqual(1, w.Hits, "25 с после укуса не нападает, хотя свет горит");
        }

        [Test]
        public void БросокБезУкусаКончаетсяЧерез10Секунд()
        {
            var w = Alone(out var preds);
            var pr = Ready(w, preds, 25f);
            w.Ship.Light = true;
            w.Step(EcoWorld.MaxStep);
            Assert.AreEqual(EcoPred.Mode.Charge, pr.Mode);
            w.Ship.Pos = new EcoVec(500, -30, 500); // корабль ушёл далеко
            w.Run(10.1f);
            Assert.AreEqual(EcoPred.Mode.Patrol, pr.Mode);
            Assert.AreEqual(0, w.Hits);
            Assert.Greater(pr.Cooldown, 0f, "после неудачного броска короткая пауза");
        }

        [Test]
        public void СонарЗовётПроверитьЗвук()
        {
            var w = Alone(out var preds);
            var pr = preds.All[0];
            var pos = pr.Agent.P + new EcoVec(0, 0, 100);
            preds.OnPing(w, pos);
            Assert.AreEqual(EcoPred.Mode.Investigate, pr.Mode);
            Assert.AreEqual(EcoState.Seek, pr.Agent.State);
            Assert.AreEqual(pos, pr.Agent.Goal);
            Assert.AreEqual(w.Time, w.Ship.LastPing);
            var ev = Notes(w, "ping");
            Assert.AreEqual(1, ev.Count, "ближе 110 м — экипаж слышит, что хищник идёт на звук");
            StringAssert.Contains("услышал сонар", ev[0].Text);
            Assert.AreEqual(EcoPred.Mode.Patrol, preds.All[2].Mode, "дальше 150 м сонар не слышат");
            float d0 = pr.Agent.DistanceTo(pos);
            w.Run(5f);
            Assert.Less(pr.Agent.DistanceTo(pos), d0 - 15f, "плывёт на звук");
        }

        [Test]
        public void ПослеСонараКорабльМанитБезСвета()
        {
            var w = Alone(out var preds);
            var pr = Ready(w, preds, 25f);
            w.Run(1f);
            Assert.AreEqual(EcoPred.Mode.Patrol, pr.Mode);
            w.Ship.Pos = pr.Agent.P + new EcoVec(25, 0, 0);
            w.Ship.LastPing = w.Time; // импульс с корабля, хищник дальше 150 м от других
            w.Step(EcoWorld.MaxStep);
            Assert.AreEqual(EcoPred.Mode.Charge, pr.Mode);
            StringAssert.Contains("привлёк сонар", Notes(w, "ping")[0].Text);

            w = Alone(out preds);
            pr = Ready(w, preds, 25f);
            w.Ship.LastPing = -20f; // импульс давно (больше 12 с назад)
            w.Step(EcoWorld.MaxStep);
            Assert.AreEqual(EcoPred.Mode.Patrol, pr.Mode);
        }

        [Test]
        public void РазрядОтпугивает()
        {
            var w = Alone(out var preds);
            var pr = Ready(w, preds, 15f);
            w.Ship.Light = true;
            w.Step(EcoWorld.MaxStep);
            Assert.AreEqual(EcoPred.Mode.Charge, pr.Mode);
            preds.OnZap(w);
            Assert.AreEqual(EcoPred.Mode.Flee, pr.Mode);
            Assert.AreEqual(30f, pr.Cooldown, 1e-4f);
            Assert.AreEqual(1, Notes(w, "zap").Count);
            StringAssert.Contains("Разряд отпугнул", Notes(w, "zap")[0].Text);
            w.Run(8.2f);
            Assert.AreEqual(0, w.Hits, "отпугнутый не кусает");
            Assert.Greater(pr.Agent.DistanceTo(w.Ship.Pos), 25f);
            Assert.AreEqual(EcoPred.Mode.Patrol, pr.Mode);

            w = Alone(out preds);
            pr = Ready(w, preds, 25f);
            preds.OnZap(w, 1f);
            Assert.AreEqual(EcoPred.Mode.Patrol, pr.Mode, "дальше 20 м разряд не достаёт");
            preds.OnZap(w, 1.5f);
            Assert.AreEqual(EcoPred.Mode.Flee, pr.Mode, "с большей долей реактора — дальше");
        }

        /// <summary>Мир со светляками на дне отмели; хищники, кроме первого, не голодают.</summary>
        static EcoWorld WithFish(out EcoFish fish, out EcoPred preds)
        {
            var w = new EcoWorld { Ground = Seabed.Height };
            w.Ship.Pos = new EcoVec(500, -30, 500);
            fish = w.Add(new EcoFish());
            preds = w.Add(new EcoPred(fish));
            for (int i = 1; i < preds.All.Count; i++) preds.All[i].Hunger = -10f;
            var pr = preds.All[0];
            pr.Cooldown = 100f;
            pr.Agent.P = EcoFish.Homes[0] + new EcoVec(10, 2, 0);
            pr.Agent.Goal = pr.Agent.P;
            return w;
        }

        [Test]
        public void ГолодныйЛовитСветляка()
        {
            var w = WithFish(out var fish, out var preds);
            var pr = preds.All[0];
            pr.Hunger = 0.9f;
            bool hunted = false;
            for (float t = 0; t < 60f && Notes(w, "catch").Count == 0; t += EcoWorld.MaxStep)
            {
                w.Step(EcoWorld.MaxStep);
                hunted |= pr.Mode == EcoPred.Mode.Hunt;
            }
            Assert.IsTrue(hunted, "голодный (> 0,45) охотится на светляков рядом");
            var ev = Notes(w, "catch");
            Assert.AreEqual(1, ev.Count, "поймал рыбку за минуту");
            StringAssert.Contains("поймал", ev[0].Text);
            Assert.AreEqual(EcoFish.PerShoal * EcoFish.Homes.Length - 1, w.Count("fish"), "рыбка съедена");
            Assert.AreEqual(EcoPred.Mode.Eat, pr.Mode);
            Assert.AreEqual(0f, pr.Hunger, 1e-5f, "наелся");
            Assert.IsNull(pr.Prey);
            w.Run(5.1f);
            Assert.AreEqual(EcoPred.Mode.Patrol, pr.Mode, "5 с ест, потом патрулирует");
        }

        [Test]
        public void СытыйНеОхотится()
        {
            var w = WithFish(out _, out var preds);
            var pr = preds.All[0];
            pr.Hunger = 0f;
            for (float t = 0; t < 10f; t += EcoWorld.MaxStep)
            {
                w.Step(EcoWorld.MaxStep);
                Assert.AreNotEqual(EcoPred.Mode.Hunt, pr.Mode, "за 10 с голод только 0,3");
            }
            Assert.AreEqual(0.3f, pr.Hunger, 1e-3f);
            Assert.AreEqual(0, Notes(w, "catch").Count);
        }

        [Test]
        public void ОтмельЖивётСерпохвостамиИСтаями()
        {
            var w = new EcoWorld { Ground = Seabed.Height };
            w.Ship.Pos = new EcoVec(500, -30, 500);
            var fish = w.Add(new EcoFish());
            var preds = w.Add(new EcoPred(fish));
            int catches = 0, flees = 0;
            w.Bus.Emitted += ev =>
            {
                if (ev.SpeciesId == "pred" && ev.Note == "catch") catches++;
                if (ev.SpeciesId == "fish" && ev.Note == "flee") flees++;
            };
            for (int s = 0; s < 300; s++)
            {
                w.Run(1f);
                w.Bus.Clear();
                foreach (var pr in preds.All)
                {
                    var p = pr.Agent.P;
                    Assert.GreaterOrEqual(p.Y, Seabed.Height(p.X, p.Z) + 1.8f - 1e-3f, "не под дном");
                    Assert.LessOrEqual(p.Y, -4f + 1e-4f, "не выше потолка");
                    Assert.LessOrEqual(System.Math.Abs(p.X), EcoAgent.Edge);
                    Assert.LessOrEqual(System.Math.Abs(p.Z), EcoAgent.Edge);
                    Assert.That(pr.Hunger, Is.InRange(0f, 1f));
                }
            }
            Assert.Greater(catches, 0, "за 5 минут серпохвосты кого-то поймали");
            Assert.Greater(flees, 0, "стаи уходили в кораллы от хищников");
            Assert.AreEqual(0, w.Hits, "корабль далеко — ударов нет");
        }

        [Test]
        public void ОдноЗерноОдинМир()
        {
            EcoWorld Make()
            {
                var w = new EcoWorld { Ground = Seabed.Height };
                w.Ship.Pos = new EcoVec(500, -30, 500);
                var f = w.Add(new EcoFish());
                w.Add(new EcoPred(f));
                return w;
            }
            var a = Make(); var b = Make();
            a.Run(40f); b.Run(40f);
            for (int i = 0; i < a.Agents.Count; i++) Assert.AreEqual(a.Agents[i].P, b.Agents[i].P);
            a.Reset(); a.Run(40f);
            for (int i = 0; i < a.Agents.Count; i++) Assert.AreEqual(b.Agents[i].P, a.Agents[i].P, "после сброса — то же");
        }

        [Test]
        public void СбросВозвращаетХищниковИЗабываетУдары()
        {
            var w = Alone(out var preds);
            var pr = Ready(w, preds, 25f);
            w.Ship.Light = true;
            w.Run(5f);
            Assert.AreEqual(1, w.Hits);
            w.ShipStruck += h => Assert.Fail("после сброса удар не повторяется сам");
            w.Ship.Light = false;
            w.Ship.Pos = new EcoVec(500, -30, 500);
            w.Reset();
            Assert.AreEqual(0, w.Hits);
            Assert.IsFalse(w.LastHit.HasValue);
            Assert.AreEqual(-99f, w.Ship.LastPing);
            Assert.AreEqual(3, preds.All.Count);
            Assert.AreEqual(60f, preds.All[0].Agent.P.X, 1e-4f);
            Assert.AreEqual(EcoPred.Mode.Patrol, preds.All[0].Mode);
            w.Run(1f);
        }
    }
}
