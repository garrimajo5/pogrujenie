'use strict';
/* Игрок, кнопка действия, сканирование, открытия, журнал */
T.describe('Кнопка действия', () => {
  const D = () => G.Game.debug;
  T.test('у штурвала садимся за штурвал и встаём', () => {
    const P = G.Game.player; P.x = 0; P.z = -3.4; P.yaw = 0; P.pitch = -0.4;
    D().frame(1);
    T.eq(D().actionLabel(), 'Сесть за штурвал');
    G.Ctl.actionPressed = true; D().frame(1);
    T.ok(G.Game.piloting, 'не сели');
    G.Ctl.standPressed = true; D().frame(1);
    T.ok(!G.Game.piloting, 'не встали');
  });
  T.test('люк открывается кнопкой и пускает в лабораторию', () => {
    const P = G.Game.player; P.x = 0; P.z = 1.2; P.yaw = Math.PI; P.pitch = 0;
    D().frame(1);
    T.eq(D().actionLabel(), 'Открыть люк');
    G.Ctl.actionPressed = true; D().frame(30);
    T.gt(G.Ship.doors[0].t, 0.85);
    G.Ship.doors[1].open = true; D().frame(30);
    G.Ctl.padMove.y = 1; D().frame(160); G.Ctl.padMove.y = 0;
    T.eq(G.Game.room, 'lab'); T.gt(P.z, 8.3);
  });
  T.test('пульт инженера даёт разряд', () => {
    const P = G.Game.player; P.x = -3.8; P.z = -0.6; P.yaw = Math.PI / 2; P.pitch = 0;
    G.Ship.energy = 80; D().frame(1);
    T.eq(D().actionLabel(), 'Разряд по корпусу');
    G.Ctl.actionPressed = true; D().frame(1);
    T.lt(G.Ship.energy, 66);
  });
});

T.describe('Сканирование', () => {
  const D = () => G.Game.debug;
  function aimAtSomething() {
    for (const tg of G.Eco.scanTargets().sort((a, b) => b.r - a.r)) {
      if (tg.pos.distanceTo(G.Ship.center) > 75) continue;
      D().lookAt(tg.pos); D().frame(1);
      if (D().actionLabel().startsWith('Сканировать')) return tg.sp;
    }
    return null;
  }
  T.test('удержание кнопки заносит вид в журнал', () => {
    G.Game.toggleHelm();
    let sp = aimAtSomething();
    if (!sp) { G.Ship.pos.set(G.Eco.veils[0].p.x, G.Eco.veils[0].p.y - 6, G.Eco.veils[0].p.z + 30); D().frame(2); sp = aimAtSomething(); }
    T.ok(sp, 'в окне нет ни одного существа');
    G.Ctl.actionDown = true;
    for (let i = 0; i < 80 && !G.SPECIES[sp].scanned; i++) { D().lookAt(G.Eco.scanTargets().find(t => t.sp === sp).pos); D().frame(1); }
    G.Ctl.actionDown = false;
    T.ok(G.SPECIES[sp].scanned, 'вид не отсканирован: ' + sp);
    T.ok(document.getElementById('j-count').textContent.startsWith('1/6'));
  });
  T.test('сквозь стену не сканируется', () => {
    const P = G.Game.player; P.x = 0; P.z = 5;
    const tg = G.Eco.scanTargets()[0];
    D().lookAt(tg.pos); D().frame(1);
    T.ok(!D().actionLabel().startsWith('Сканировать'));
  });
});

T.describe('Открытия и журнал', () => {
  const D = () => G.Game.debug;
  T.test('место открывается, когда корабль рядом', () => {
    const p = G.World.pois.find(x => x.key === 'geyser');
    G.Ship.pos.set(p.pos.x + 12, -20, p.pos.z + 12);
    D().frame(1);
    T.ok(p.found);
    T.ok(document.getElementById('j-count').textContent.endsWith('1/6'));
  });
  T.test('наблюдение рядом попадает в журнал, далёкое — нет', () => {
    G.Eco.emit('grazer', 'graze', G.Ship.center, () => 'тестовое наблюдение');
    G.Eco.emit('ray', 'follow', G.Ship.center.clone().add(T.V(500, 0, 0)), () => 'далеко');
    D().frame(1);
    T.ok(G.SPECIES.grazer.seen.has('graze'));
    T.ok(!G.SPECIES.ray.seen.has('follow'));
    T.ok(D().feed().some(t => t.includes('тестовое наблюдение')));
  });
  T.test('журнал открывается и закрывается', () => {
    G.Game.openJournal();
    T.eq(G.Game.state, 'journal');
    T.eq(document.querySelectorAll('#journal-body .j-card').length, 12);
    G.Ctl.journalPressed = true; D().frame(1);
    T.eq(G.Game.state, 'play');
  });
});
