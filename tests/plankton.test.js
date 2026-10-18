'use strict';
/* Мерцающий планктон: светящийся след за быстрым кораблём выдаёт его серпохвостам,
   ледокрылы процеживают облака, и след становится тусклым */
T.describe('Мерцающий планктон', () => {
  const E = () => G.Eco, S = () => G.Ship, D = () => G.Game.debug;
  const eco = (n) => { for (let i = 0; i < n; i++) { G.Game.t += 0.05; E().update(0.05, G.Game.t); } };
  /* хищники далеко, ледокрылы уведены от облаков, прожектор погашен: след выдаёт корабль и без света */
  const quiet = (keep) => {
    S().light = false;
    E().preds.forEach(p => { p.state = 'eat'; p.timer = 999; p.cd = 999; p.p.set(250, -20, 250); });
    E().rays.forEach(r => { r.fcd = 999; if (r === keep) return; const c = r.c.clone(); r.c.set(600, -9, 600); T._restore.push(() => r.c.copy(c)); });
  };
  /* корабль в облаке: центр корабля в точке облака */
  const shipIn = (cl, speed) => {
    S().yaw = 0; S().pitch = S().roll = 0; S().vy = 0;
    S().pos.set(cl.c.x, cl.c.y - 1.5, cl.c.z); S().applyTransform();
    S().pos.add(T.V(cl.c.x - S().center.x, cl.c.y - S().center.y, cl.c.z - S().center.z)); S().applyTransform();
    S().speed = speed;
  };
  const shipAway = (speed) => { S().pos.set(-250, -20, 250); S().speed = speed; S().applyTransform(); };
  const alive = () => E().trailPts.filter(q => q.life > 0);
  const predAt = (dist) => {
    const pr = E().preds[1]; // без пиявника: не уплывёт на станцию чистки
    T.ok(!pr.leech);
    pr.state = 'patrol'; pr.timer = 999; pr.leechCd = 999; pr.cd = 0; pr.hunger = 0.3; pr.target = null; pr.trailCd = 0;
    pr.p.copy(S().center).add(T.V(0, 0, dist)); pr.goal.copy(pr.p); pr.v.set(0, 0, 0);
    return pr;
  };

  T.test('три облака в толще воды, через два проходят ледокрылы', () => {
    T.eq(E().clouds.length, 3);
    E().clouds.forEach(cl => {
      T.gt(cl.c.y - G.World.height(cl.c.x, cl.c.z), 15, 'облако лежит на дне');
      T.lt(cl.c.y, -8, 'облако у самого льда');
      T.eq(cl.density, 1);
    });
    T.eq(E().scanTargets().filter(x => x.sp === 'plankton').length, 3);
    /* ледокрыл по своему кругу проходит через облако (по горизонтали) */
    const crosses = (ry, cl) => { for (let a = 0; a < 6.283; a += 0.01) { if (Math.hypot(ry.c.x + Math.cos(a) * ry.R - cl.c.x, ry.c.z + Math.sin(a) * ry.R - cl.c.z) < 10) return true; } return false; };
    T.ok(crosses(E().rays[0], E().clouds[0]), 'первый ледокрыл не проходит через облако');
    T.ok(crosses(E().rays[1], E().clouds[2]), 'второй ледокрыл не проходит через облако');
    T.ok(!E().rays.some(r => crosses(r, E().clouds[1])), 'среднее облако должно быть нетронутым');
    T.eq(G.nm('plankton', 'gen'), 'светящейся взвеси');
    G.SPECIES.plankton.scanned = true; T.eq(G.nm('plankton', 'gen'), 'мерцающего планктона');
  });

  T.test('быстрый корабль в облаке оставляет светящийся след', () => {
    const ev = T.captureEvents();
    quiet();
    const cl = E().clouds[1];
    shipIn(cl, 6); eco(20);
    T.ok(E().inCloud(S().center) === cl, 'корабль не в облаке');
    T.gt(E().trail, 0.9, 'след не разгорелся');
    T.gt(alive().length, 25, 'мало светящихся точек следа');
    alive().forEach(q => T.lt(q.p.distanceTo(S().center), 20, 'точка следа далеко от корабля'));
    T.ok(ev.some(e => e.sp === 'plankton' && e.note === 'trail'), 'нет наблюдения «след»');
    /* фоновые точки облака у корабля вспыхнули */
    const pc = E().plAmb.geometry.attributes.color.array; let mx = 0;
    for (let k = 0; k < 110; k++) mx = Math.max(mx, pc[(110 + k) * 3 + 1]);
    T.gt(mx, 0.5, 'облако у корабля не вспыхнуло');
  });

  T.test('на малом ходу и вне облака следа нет (контроль)', () => {
    const ev = T.captureEvents();
    quiet();
    shipIn(E().clouds[1], 2.5); eco(40);
    T.eq(E().trail, 0, 'след на малом ходу');
    T.eq(alive().length, 0);
    shipAway(6); eco(40);
    T.eq(E().inCloud(S().center), null);
    T.eq(E().trail, 0, 'след без планктона');
    T.eq(alive().length, 0);
    T.ok(!ev.some(e => e.sp === 'plankton'), 'событие без следа');
  });

  T.test('сбросишь ход — след гаснет', () => {
    quiet();
    shipIn(E().clouds[1], 6); eco(20);
    T.gt(E().trail, 0.9);
    S().speed = 1; eco(20);
    T.lt(E().trail, 0.75, 'след не тускнеет');
    eco(100);
    T.eq(E().trail, 0, 'след не погас');
    T.eq(alive().length, 0, 'точки следа не погасли');
  });

  T.test('след выдаёт корабль дальнему серпохвосту', () => {
    const ev = T.captureEvents();
    quiet();
    shipIn(E().clouds[1], 6); eco(20);
    const pr = predAt(60);
    eco(2);
    T.ok(!S().light && E().flare === 0, 'корабль выдал свет, а не след');
    T.eq(pr.state, 'investigate', 'хищник не заметил след');
    T.lt(pr.goal.distanceTo(S().center), 3, 'хищник плывёт не к кораблю');
    T.ok(ev.some(e => e.sp === 'plankton' && e.note === 'betray'), 'нет наблюдения «выдаёт корабль»');
    const d0 = pr.p.distanceTo(S().center);
    eco(60);
    T.lt(pr.p.distanceTo(S().center), d0 - 10, 'хищник не приблизился');
  });

  T.test('без следа и слишком далеко серпохвост корабль не замечает (контроль)', () => {
    quiet();
    shipIn(E().clouds[1], 2.5); eco(10);
    const pr = predAt(60);
    eco(40);
    T.eq(pr.state, 'patrol', 'хищник пошёл к кораблю без следа: ' + E().flare + ' ' + E().trail);
    shipIn(E().clouds[1], 6); eco(20); T.gt(E().trail, 0.9);
    const far = predAt(100);
    eco(5);
    T.eq(far.state, 'patrol', 'след видно слишком далеко');
  });

  T.test('ледокрыл процеживает облако: после него след тусклый и не выдаёт', () => {
    const ev = T.captureEvents();
    const ry = E().rays[0], cl = E().clouds[0];
    quiet(ry);
    /* ледокрыл на подходе к облаку */
    const c0 = ry.c.clone(), a0 = ry.ang; T._restore.push(() => { ry.c.copy(c0); ry.ang = a0; });
    ry.ang = Math.atan2(cl.c.z - ry.c.z, cl.c.x - ry.c.x) - 22 / ry.R;
    eco(1);
    T.ok(!E().clouds.some(c => c.density < 1), 'облако тронуто заранее');
    eco(240); // 12 с: ледокрыл проходит облако насквозь
    T.lt(cl.density, 0.4, 'планктон не поредел');
    T.ok(ev.some(e => e.sp === 'plankton' && e.note === 'feed'), 'нет наблюдения «ледокрыл процеживает»');
    T.ok(ev.some(e => e.sp === 'ray' && e.note === 'feed'), 'нет наблюдения у ледокрыла');
    T.eq(E().clouds[1].density, 1, 'другое облако поредело');
    /* быстрый корабль в поредевшем облаке: след тусклый, хищник не замечает */
    ry.c.set(600, -9, 600); eco(1);
    shipIn(cl, 6); eco(20);
    T.gt(E().trail, 0.1, 'следа нет совсем');
    T.lt(E().trail, 0.5, 'след слишком яркий');
    const pr = predAt(60);
    eco(20);
    T.eq(pr.state, 'patrol', 'тусклый след выдал корабль');
    /* без ледокрыла планктон отрастает */
    const d = cl.density; eco(400);
    T.gt(cl.density, d + 0.05, 'планктон не отрастает');
  });

  T.test('полный ход со штурвала оставляет след, сброс сохраняет плотность', () => {
    quiet();
    T.ok(D().teleport('plankton1'));
    T.ok(E().inCloud(S().center) === E().clouds[1], 'телепорт не в облако');
    G.Game.toggleHelm(); D().frame(1);
    G.Ctl.padMove.y = 1; D().frame(60); G.Ctl.padMove.y = 0;
    T.gt(Math.abs(S().speed), 3.5, 'корабль не разогнался');
    T.gt(E().trail, 0.5, 'нет следа за кораблём');
    const r = D().render();
    T.lt(r.calls, 260, 'вызовов отрисовки'); T.lt(r.tris, 350000, 'треугольников');
    T.ok(G.SPECIES.plankton.seen.has('trail'), 'наблюдение не попало в журнал');
    G.Save.save(); G.SPECIES.plankton.seen.clear(); G.Save.load();
    T.ok(G.SPECIES.plankton.seen.has('trail'), 'наблюдение не сохранилось');
    E().clouds[1].density = 0.3; D().reset();
    T.eq(E().clouds[1].density, 1); T.eq(E().trail, 0); T.eq(alive().length, 0);
  });

  T.test('планктон сканируют с мостика', () => {
    quiet();
    const cl = E().clouds[1];
    S().pos.set(cl.c.x, cl.c.y - 1.5, cl.c.z + 45); S().yaw = 0; S().speed = 0; S().applyTransform();
    G.Game.toggleHelm(); D().frame(1);
    G.Ctl.actionDown = true;
    for (let i = 0; i < 80 && !G.SPECIES.plankton.scanned; i++) { D().lookAt(cl.c); D().frame(1); }
    G.Ctl.actionDown = false; D().frame(1);
    T.ok(G.SPECIES.plankton.scanned, 'планктон не отсканирован');
  });
});
