using System;
using System.Collections.Generic;
using NUnit.Framework;
using Pogrujenie.Core;

namespace Pogrujenie.Tests
{
    /// <summary>Как рисуется серпохвост (initPreds/predRender в js/creatures.js): тело, огни, цепочка сегментов.</summary>
    public class EcoPredLookTests
    {
        static EcoPred.Pred Pred(out EcoWorld w)
        {
            w = new EcoWorld { Ground = (x, z) => -40f };
            w.Ship.Pos = new EcoVec(500, -30, 500);
            var preds = w.Add(new EcoPred());
            return preds.All[0];
        }

        static float Dot(EcoVec a, EcoVec b) => a.X * b.X + a.Y * b.Y + a.Z * b.Z;

        /// <summary>Каждое ребро идёт один раз в одну сторону и один раз в обратную; объём каждой части > 0.</summary>
        static void Closed(EcoVec[] v, int[] tris, int[] part, int parts, string what)
        {
            Assert.AreEqual(0, tris.Length % 3);
            var edges = new Dictionary<long, int>();
            for (int i = 0; i < tris.Length; i += 3)
                for (int e = 0; e < 3; e++)
                {
                    long key = (long)tris[i + e] * 100000 + tris[i + (e + 1) % 3];
                    edges.TryGetValue(key, out int n);
                    edges[key] = n + 1;
                }
            foreach (var kv in edges)
            {
                Assert.AreEqual(1, kv.Value, what + ": ребро повторяется");
                long back = (kv.Key % 100000) * 100000 + kv.Key / 100000;
                Assert.IsTrue(edges.ContainsKey(back), what + ": дыра или вывернутый треугольник");
            }
            var vol = new double[parts];
            for (int i = 0; i < tris.Length; i += 3)
            {
                var a = v[tris[i]]; var b = v[tris[i + 1]]; var c = v[tris[i + 2]];
                Assert.AreEqual(part[tris[i]], part[tris[i + 1]], what + ": треугольник на двух частях");
                vol[part[tris[i]]] += Dot(a, EcoPredLook.Cross(b, c)) / 6.0;
            }
            for (int p = 0; p < parts; p++) Assert.Greater(vol[p], 0.0, what + ": часть " + p + " смотрит внутрь");
        }

        [Test]
        public void ТелоИОгниЗамкнутыИСмотрятНаружу()
        {
            Assert.AreEqual(EcoPredLook.BodyVerts, EcoPredLook.BodyPaint.Length);
            Assert.AreEqual(EcoPredLook.BodyVerts, EcoPredLook.BodyFrame.Length);
            Assert.AreEqual(EcoPredLook.LightVerts, EcoPredLook.LightPart.Length);
            Closed(EcoPredLook.BodyRest, EcoPredLook.BodyTris, EcoPredLook.BodyPart, EcoPredLook.TailPart + 1, "тело");
            Closed(EcoPredLook.LightRest, EcoPredLook.LightTris, EcoPredLook.LightPart, 2 + 2 * (EcoPredLook.Segs - 1), "огни");
            int tris = (EcoPredLook.BodyTris.Length + EcoPredLook.LightTris.Length) / 3 * EcoPred.Starts.GetLength(0);
            Assert.Less(tris, 3000, "три серпохвоста — меньше 3 тыс. треугольников");
            Assert.Less(EcoPredLook.BodyVerts * 3, 65535);
        }

        [Test]
        public void ГоловаГлазаОгниИХвостКакВБраузере()
        {
            int eyes = 0, dots = 0;
            for (int i = 0; i < EcoPredLook.LightVerts; i++)
            {
                uint c = EcoPredLook.LightPaint[i];
                if (EcoPredLook.LightPart[i] < 2) { Assert.AreEqual(EcoPredLook.Eye, c, "глаза красные"); eyes++; }
                else { Assert.AreEqual(EcoPredLook.Dot, c, "огни по бокам оранжевые"); dots++; }
            }
            Assert.AreEqual(2 * 6, eyes, "два глаза");
            Assert.AreEqual(2 * 10 * 6, dots, "по два огня на каждый из десяти сегментов");
            Assert.AreEqual(0xFF3B4Eu, EcoPredLook.Eye);
            Assert.AreEqual(0xFF9A3Cu, EcoPredLook.Dot);

            var seen = new HashSet<int>();
            for (int i = 0; i < EcoPredLook.BodyVerts; i++)
            {
                int p = EcoPredLook.BodyPart[i];
                seen.Add(p);
                uint c = EcoPredLook.BodyPaint[i];
                if (p == EcoPredLook.HeadPart) Assert.AreEqual(EcoPredLook.Head, c);
                else if (p == EcoPredLook.JawPart) { Assert.AreEqual(EcoPredLook.Jaw, c); Assert.LessOrEqual(EcoPredLook.BodyRest[i].Z, 1.5f + 1e-4f); }
                else if (p == EcoPredLook.TailPart)
                {
                    Assert.AreEqual(EcoPredLook.Tail, c);
                    Assert.Less(EcoPredLook.BodyRest[i].Z, -0.6f, "серп позади последнего сегмента");
                }
                else Assert.AreEqual(EcoPredLook.Seg, c);
            }
            Assert.AreEqual(13, seen.Count, "голова, челюсть, десять сегментов, хвост");
            Assert.AreEqual(0.55f, EcoPredLook.SegRadius(1), 1e-5f);
            Assert.Less(EcoPredLook.SegRadius(10), EcoPredLook.SegRadius(1) * 0.4f, "к хвосту тоньше");
        }

        [Test]
        public void ЦепочкаТянетсяЗаГоловой()
        {
            var pr = Pred(out _);
            pr.Agent.P = new EcoVec(0, -30, 0);
            pr.Agent.V = new EcoVec(5, 0, 0);
            var c = new EcoPredLook.Chain();
            EcoPredLook.Follow(c, pr);
            Assert.IsTrue(c.Ready);
            for (int k = 0; k < EcoPredLook.Segs; k++)
            {
                Assert.AreEqual(-k * EcoPredLook.Spacing, c.P[k].X, 1e-4f, "первый раз — прямо за головой");
                Assert.AreEqual(0f, c.P[k].Z, 1e-4f);
            }
            // голова плывёт по кругу 30 с: звенья не длиннее 0,62 м
            for (int s = 0; s < 600; s++)
            {
                float a = s * 0.02f;
                pr.Agent.P = new EcoVec(20f * (float)Math.Cos(a), -30f, 20f * (float)Math.Sin(a));
                EcoPredLook.Follow(c, pr);
                Assert.AreEqual(0f, EcoVec.Distance(c.P[0], pr.Agent.P), 1e-5f, "голова — в месте хищника");
                for (int k = 1; k < EcoPredLook.Segs; k++)
                    Assert.LessOrEqual(EcoVec.Distance(c.P[k], c.P[k - 1]), EcoPredLook.Link + 1e-4f);
            }
            // долго по прямой — цепочка вытягивается в линию за головой
            var start = pr.Agent.P;
            for (int s = 1; s <= 200; s++)
            {
                pr.Agent.P = start + new EcoVec(0, 0, -0.3f * s);
                EcoPredLook.Follow(c, pr);
            }
            var tail = c.P[EcoPredLook.Segs - 1];
            Assert.AreEqual(pr.Agent.P.Z + 10 * EcoPredLook.Link, tail.Z, 0.05f, "хвост в 6,2 м позади");
            Assert.AreEqual(pr.Agent.P.X, tail.X, 0.05f);
        }

        [Test]
        public void НарисованПоКурсуИИзвивается()
        {
            var pr = Pred(out _);
            pr.Agent.P = new EcoVec(10, -30, 10);
            pr.Agent.V = new EcoVec(6, 0, 0);
            var c = new EcoPredLook.Chain();
            EcoPredLook.Follow(c, pr);
            var body = new EcoVec[EcoPredLook.BodyVerts];
            var lights = new EcoVec[EcoPredLook.LightVerts];
            EcoPredLook.Place(pr, c, 1.3f, body, 0, lights, 0);
            var p = pr.Agent.P;
            var fw = new EcoVec(1, 0, 0);

            var head = EcoVec.Zero; int n = 0;
            float minX = float.MaxValue, maxX = float.MinValue;
            for (int i = 0; i < body.Length; i++)
            {
                minX = Math.Min(minX, body[i].X); maxX = Math.Max(maxX, body[i].X);
                if (EcoPredLook.BodyPart[i] == EcoPredLook.HeadPart) { head = head + body[i]; n++; }
                if (EcoPredLook.BodyPart[i] == EcoPredLook.TailPart)
                    Assert.Less(body[i].X, c.P[EcoPredLook.Segs - 1].X - 0.3f, "серп за последним сегментом");
            }
            head = head * (1f / n);
            Assert.Less(EcoVec.Distance(head, p), 1e-3f, "голова — в месте хищника");
            Assert.AreEqual(p.X + 1.5f, maxX, 1e-3f, "впереди — кончик челюсти");
            Assert.That(maxX - minX, Is.InRange(8f, 11f), "вытянутый серпохвост от челюсти до серпа");

            // глаза впереди и выше середины головы, по разные стороны
            var eyeL = EcoVec.Zero; var eyeR = EcoVec.Zero;
            for (int i = 0; i < 6; i++) { eyeL = eyeL + lights[i] * (1f / 6); eyeR = eyeR + lights[6 + i] * (1f / 6); }
            Assert.AreEqual(0.72f, Dot(eyeL - p, fw), 1e-3f);
            Assert.AreEqual(0.16f, eyeL.Y - p.Y, 1e-3f);
            Assert.AreEqual(0.66f, EcoVec.Distance(eyeL, eyeR), 1e-3f);

            // огни сегмента по бокам: оба на одной высоте, по разные стороны от оси
            for (int k = 1; k < EcoPredLook.Segs; k++)
            {
                var a = EcoVec.Zero; var b = EcoVec.Zero;
                for (int i = 0; i < 6; i++)
                {
                    a = a + lights[(2 + (k - 1) * 2) * 6 + i] * (1f / 6);
                    b = b + lights[(3 + (k - 1) * 2) * 6 + i] * (1f / 6);
                }
                float r = EcoPredLook.SegRadius(k);
                Assert.AreEqual(2f * 0.95f * r * 0.9f, EcoVec.Distance(a, b), 1e-3f, "огни сегмента " + k);
                Assert.AreEqual(a.Y, b.Y, 1e-3f);
                float side = (a.Z + b.Z) * 0.5f - p.Z;
                Assert.LessOrEqual(Math.Abs(side), EcoPredLook.WaveAmp * k + 1e-3f, "волна не шире 0,08 м на звено");
            }

            // во времени тело извивается, на ходу — чаще
            var later = new EcoVec[EcoPredLook.BodyVerts];
            EcoPredLook.Place(pr, c, 1.6f, later, 0, lights, 0);
            float moved = 0f;
            for (int i = 0; i < body.Length; i++) moved = Math.Max(moved, EcoVec.Distance(body[i], later[i]));
            Assert.Greater(moved, 0.05f, "сегменты ходят волной");
            float fast = EcoPredLook.WaveFreq(pr);
            pr.Agent.V = EcoVec.Zero;
            Assert.AreEqual(EcoPredLook.WaveRate, EcoPredLook.WaveFreq(pr), 1e-5f);
            Assert.AreEqual(EcoPredLook.WaveRate + 6f * EcoPredLook.WaveRatePerSpeed, fast, 1e-4f);
            Assert.AreEqual(0f, EcoPredLook.Forward(pr).X, 1e-5f, "стоит — смотрит на +z, как lookAt в браузере");
            Assert.AreEqual(1f, EcoPredLook.Forward(pr).Z, 1e-5f);
        }

        [Test]
        public void ОдноВремяОдинРисунок()
        {
            var pr = Pred(out var w);
            var c1 = new EcoPredLook.Chain(); var c2 = new EcoPredLook.Chain();
            var b1 = new EcoVec[EcoPredLook.BodyVerts]; var b2 = new EcoVec[EcoPredLook.BodyVerts];
            var l = new EcoVec[EcoPredLook.LightVerts];
            for (int s = 0; s < 100; s++)
            {
                w.Step(EcoWorld.MaxStep);
                EcoPredLook.Follow(c1, pr); EcoPredLook.Follow(c2, pr);
            }
            EcoPredLook.Place(pr, c1, w.Time, b1, 0, l, 0);
            EcoPredLook.Place(pr, c2, w.Time, b2, 0, l, 0);
            for (int i = 0; i < b1.Length; i++) Assert.AreEqual(b1[i], b2[i]);
        }
    }
}
