'use strict';
/* Каменщик: ряды трубчатых червей у огнецвета прячут венчики от вибрации */
T.describe('Каменщик', () => {
  const E = () => G.Eco, S = () => G.Ship, D = () => G.Game.debug;
  const eco = (n, dt = 0.05) => { for (let i = 0; i < n; i++) { G.Game.t += dt; E().update(dt, G.Game.t); } };
  const quiet = () => {
    E().preds.forEach(p => { p.state = 'eat'; p.timer = 999; p.cd = 999; p.p.set(250, -20, 250); });
    E().rays.forEach(r => { r.fcd = 999; });
    if (E().grot) { E().grot.shy = 999; E().grot.p.set(-200, 0, 200); }
  };
  const shipAway = () => { S().pos.set(-250, -20, -250); S().speed = S().vy = 0; S().applyTransform(); };
  const km = () => E().kamen[0];
  const openN = k => k.crowns.filter(c => c.open > 0.9).length;
  /* корабль над рядом, на высоте h, со скоростью v */
  const shipOver = (k, v, h = 6) => { S().yaw = 0; S().pitch = S().roll = 0; S().vy = 0; S().speed = v; S().pos.copy(k.c).add(T.V(0, h, 3)); S().applyTransform(); };
  /* венчики ряда по порядку вдоль ряда: от одного края к другому */
  const along = k => k.crowns.slice().sort((a, b) => a.p.clone().sub(k.c).dot(k.dir) - b.p.clone().sub(k.c).dot(k.dir));

  T.test('ряды каменщиков стоят у каждой станции огнецвета, венчики раскрыты', () => {
    T.eq(E().kamen.length, E().stations.length);
    T.gt(E().kamen.length, 0, 'нет ни одного ряда');
    E().kamen.forEach(k => {
      T.eq(k.crowns.length, 12);
      T.lt(Math.hypot(k.c.x - k.st.base.x, k.c.z - k.st.base.z), 8, 'ряд далеко от огнецвета');
      k.crowns.forEach(c => { const h = G.World.height(c.p.x, c.p.z); T.ok(c.p.y > h + 0.1 && c.p.y < h + 1.1, 'венчик висит в воде или в грунте'); });
      T.eq(openN(k), 12);
    });
    T.eq(E().scanTargets().filter(x => x.sp === 'kamen').length, E().kamen.length);
    T.eq(G.nm('kamen'), 'трубчатые черви');
  });

  T.test('идущий корабль заставляет спрятать венчики, у стоящего они раскрываются', () => {
    const ev = T.captureEvents();
    quiet();
    for (let i = 0; i < 40; i++) { shipOver(km(), 3); eco(1); }
    T.lt(openN(km()), 1, 'венчики не спрятались');
    T.gt(km().shut, 0.9);
    T.ok(ev.some(e => e.sp === 'kamen' && e.note === 'ship'), 'нет наблюдения «вибрация двигателей»');
    shipOver(km(), 0); eco(20);
    T.lt(openN(km()), 1, 'раскрылись сразу, без паузы');
    eco(120);
    T.eq(openN(km()), 12, 'не раскрылись у стоящего корабля');
    T.ok(ev.some(e => e.sp === 'kamen' && e.note === 'arch'), 'нет наблюдения «раскрыли венчики»');
  });

  T.test('прожектор и стоящий рядом корабль венчики не тревожат', () => {
    quiet(); S().light = true;
    shipOver(km(), 0, 3); eco(200);
    T.eq(openN(km()), 12);
    T.eq(km().shut, 0);
  });

  T.test('хищник у края ряда: прячется ближний край, дальний остаётся раскрытым', () => {
    const ev = T.captureEvents();
    quiet(); shipAway();
    const k = km(), row = along(k), end = row[0].p;
    const pr = E().preds[0];
    for (let i = 0; i < 30; i++) { pr.state = 'eat'; pr.timer = 999; pr.p.copy(end).addScaledVector(k.dir, -2.5); pr.p.y += 1; eco(1); }
    T.lt(row[0].open, 0.1, 'ближний к хищнику венчик раскрыт');
    T.lt(row[1].open, 0.1);
    T.gt(row[row.length - 1].open, 0.9, 'дальний край тоже спрятался');
    T.gt(row[row.length - 2].open, 0.9);
    T.ok(ev.some(e => e.sp === 'kamen' && e.note === 'warn'), 'нет наблюдения «кто-то крупный»');
    /* с другой стороны — наоборот */
    pr.p.set(250, -20, 250); eco(200);
    T.eq(openN(k), 12, 'не раскрылись после ухода хищника');
    const far = row[row.length - 1].p;
    for (let i = 0; i < 30; i++) { pr.p.copy(far).addScaledVector(k.dir, 2.5); pr.p.y += 1; eco(1); }
    T.gt(row[0].open, 0.9); T.lt(row[row.length - 1].open, 0.1);
  });

  T.test('бредущий гротовик пугает каменщиков, замерший — нет', () => {
    const ev = T.captureEvents();
    quiet(); shipAway();
    const k = km(), gr = E().grot;
    T.ok(gr, 'гротовика нет');
    gr.shy = 0; gr.shyCd = 999;
    for (let i = 0; i < 20; i++) { gr.p.set(k.c.x + k.dir.z * 4, 0, k.c.z - k.dir.x * 4); eco(1); }
    T.gt(k.shut, 0.5, 'гротовик прошёл, а венчики раскрыты');
    T.ok(ev.some(e => e.sp === 'kamen' && e.note === 'warn'));
    gr.shy = 999; // гротовик замер
    for (let i = 0; i < 200; i++) { gr.p.set(k.c.x + k.dir.z * 4, 0, k.c.z - k.dir.x * 4); eco(1); }
    T.eq(openN(k), 12, 'замерший гротовик всё ещё пугает');
  });

  T.test('сброс мира раскрывает все венчики', () => {
    quiet();
    for (let i = 0; i < 40; i++) { shipOver(km(), 3); eco(1); }
    T.lt(openN(km()), 1);
    D().reset();
    E().kamen.forEach(k => T.eq(openN(k), 12));
  });

  T.test('ряд можно отсканировать из рубки, вид попадает в сохранение', () => {
    quiet();
    const k = km();
    S().yaw = 0; S().pitch = S().roll = 0; S().speed = S().vy = 0; S().light = true;
    S().pos.copy(k.c).add(T.V(0, 5, 22)); S().applyTransform();
    G.Game.toggleHelm(); D().frame(1);
    G.Ctl.actionDown = true;
    for (let i = 0; i < 80 && !G.SPECIES.kamen.scanned; i++) { D().lookAt(k.c); D().frame(1); }
    G.Ctl.actionDown = false;
    T.ok(G.SPECIES.kamen.scanned, 'не отсканирован');
    T.eq(G.nm('kamen'), 'каменщик');
    T.ok(G.Save.snapshot().species.kamen.scanned, 'не попал в сохранение');
  });

  T.test('телепорт к каменщикам в отладочной панели', () => {
    T.ok(D().teleport('kamen0'));
    T.lt(Math.hypot(S().pos.x - km().c.x, S().pos.z - km().c.z), 30);
    T.ok(!D().teleport('kamen9'));
  });
});
