'use strict';
/* Грузовой отсек: все добытые образцы лежат на полках за машинным отделением и сохраняются */
T.describe('Грузовой отсек', () => {
  const D = () => G.Game.debug, S = () => G.Ship, M = () => G.Manip, C = () => G.Ctl;
  const stored = () => { const r = localStorage.getItem(G.Save.KEY); return r ? JSON.parse(r) : null; };
  /* рука донесла образец до шлюза — так же, как в конце удержания кнопки манипулятора */
  const grab = i => { const s = M().samples[i]; s.taken = true; M().place(); M().stow(s); return s; };
  const slot = i => { const m = new THREE.Matrix4(), p = new THREE.Vector3(), q = new THREE.Quaternion(), sc = new THREE.Vector3(); S().shelfIm.getMatrixAt(i, m); m.decompose(p, q, sc); return { p, sc }; };
  const onShelf = i => { const { p, sc } = slot(i); return p.y > 0 && sc.x > 0.5; };
  const toastText = () => document.getElementById('toast').textContent;

  T.test('люк из машинного отделения ведёт в грузовой отсек', () => {
    const pl = G.Game.player; pl.x = 1.9; pl.z = 18.3; pl.yaw = Math.PI; pl.pitch = 0;
    D().frame(1);
    T.eq(G.Game.room, 'eng');
    T.eq(D().actionLabel(), 'Открыть люк');
    C().padMove.y = 1; D().frame(40); C().padMove.y = 0;
    T.lt(pl.z, 19.2, 'прошли сквозь закрытый люк');
    C().actionPressed = true; D().frame(30);
    T.gt(S().doors[3].t, 0.85, 'люк не открылся');
    T.lt(S().doors[3].mesh.position.x, 0.5, 'створка не ушла в переборку');
    C().padMove.y = 1; D().frame(80); C().padMove.y = 0;
    T.eq(G.Game.room, 'hold'); T.gt(pl.z, 20);
    T.eq(S().roomAt(19), 'eng'); T.eq(S().roomAt(21), 'hold');
    T.ok(S().canStand(1.9, 19.5), 'в проёме люка не пройти');
    T.ok(!S().canStand(0, 19.5), 'проход сквозь переборку мимо люка');
  });

  T.test('добытые образцы ложатся на полки, экран и осмотр считают их', () => {
    for (let i = 0; i < S().SHELF; i++) T.ok(!onShelf(i), 'полка не пуста в начале');
    const pl = G.Game.player; pl.x = 0; pl.z = 22.2; pl.yaw = Math.PI; pl.pitch = -0.1;
    D().frame(1);
    T.eq(D().actionLabel(), 'Осмотреть полки');
    C().actionPressed = true; D().frame(1);
    T.ok(toastText().includes('Полки пусты'), toastText());
    grab(0); grab(8); grab(13);
    T.eq(S().cargo.length, 3);
    [0, 1, 2].forEach(i => T.ok(onShelf(i), 'образца нет на полке ' + i));
    T.ok(!onShelf(3), 'лишний образец на полке');
    const c = new THREE.Color(); S().shelfIm.getColorAt(1, c);
    T.eq(c.getHex(), M().KIND[M().samples[8].kind].color, 'цвет образца на полке');
    const n = S().cargoCounts(); T.eq(n.moss, 1); T.eq(n.shell, 1); T.eq(n.spine, 1); T.eq(n.coral, 0);
    C().actionPressed = true; D().frame(1);
    T.ok(toastText().includes('На полках 3 из ' + S().SHELF), toastText());
    T.ok(toastText().includes('ракушка моллюска — 1'), toastText());
  });

  T.test('украденный скальником образец на полку не попадает (контроль)', () => {
    const s = M().samples[13]; s.taken = true; M().place();
    M().state = 'back'; M().target = s; M().t = 1;
    T.ok(M().steal() === s);
    T.eq(S().cargo.length, 0);
    T.ok(!onShelf(0), 'полка не пуста после кражи');
  });

  T.test('груз сохраняется: после перезапуска образцы на полках, а на дне их нет', () => {
    grab(2); grab(9);
    D().frame(25);
    const s = stored();
    T.ok(s && Array.isArray(s.cargo), 'груза нет в сохранении');
    T.eq(s.cargo.length, 2); T.eq(s.cargo[0].kind, 'moss'); T.eq(s.cargo[1].i, 9);
    // «перезапуск»: мир снова свежий, образцы на дне, полки пусты
    S().cargo.length = 0; M().reset(); S().showTubes();
    T.ok(!M().samples[9].taken);
    T.ok(G.Save.load(), 'не загрузилось');
    T.eq(S().cargo.length, 2);
    T.ok(M().samples[2].taken && M().samples[9].taken, 'поднятые образцы снова на дне');
    T.ok(!M().samples[0].taken, 'лишний образец пропал со дна');
    T.ok(onShelf(0) && onShelf(1), 'полки пусты после загрузки');
    T.ok(S().tubeOrbs[1].visible, 'колбы лаборатории пусты после загрузки');
    T.ok(G.Save.hasProgress(), 'груз не считается прогрессом');
  });

  T.test('битый груз в сохранении не ломает игру', () => {
    localStorage.setItem(G.Save.KEY, JSON.stringify({ v: 1, species: {}, pois: [],
      cargo: [{ kind: 'moss', i: 0 }, { kind: 'moss', i: 0 }, { kind: 'shell', i: 0 }, { kind: 'нет-такого', i: 3 }, { kind: 'coral', i: 999 }, null, { kind: 'spine' }] }));
    T.ok(G.Save.load());
    T.eq(S().cargo.length, 2, 'должны остаться мох с индексом 0 и игла без индекса');
    T.eq(S().cargo[0].kind, 'moss'); T.eq(S().cargo[1].kind, 'spine');
    T.ok(M().samples[0].taken);
    // старое сохранение без груза — полки пусты
    localStorage.setItem(G.Save.KEY, JSON.stringify({ v: 1, species: {}, pois: [] }));
    S().cargo.length = 0; M().reset();
    T.ok(G.Save.load()); T.eq(S().cargo.length, 0);
  });

  T.test('сброс прогресса очищает полки и возвращает образцы на дно', () => {
    grab(1); grab(4);
    G.Game.resetProgress();
    T.eq(S().cargo.length, 0);
    T.ok(!M().samples[1].taken && !M().samples[4].taken, 'образцы не вернулись на дно');
    T.ok(!onShelf(0), 'полка не опустела');
    T.ok(!S().tubeOrbs[0].visible, 'колба не опустела');
  });

  T.test('после гибели корабля груз остаётся на полках', () => {
    grab(3);
    S().hull = 1; G.Game.hullHit(50, 'bite');
    T.eq(G.Game.state, 'lost');
    document.getElementById('b-restart').click();
    T.eq(G.Game.state, 'play');
    T.eq(S().hull, 100);
    T.eq(S().cargo.length, 1); T.ok(onShelf(0), 'груз пропал после аварии');
  });

  T.test('бюджет отрисовки в грузовом отсеке', () => {
    for (let i = 0; i < 6; i++) grab(i);
    const pl = G.Game.player; pl.x = 0; pl.z = 20.4;
    [0, 1.57, 3.14, 4.71].forEach(y => {
      pl.yaw = y; const r = D().render();
      T.lt(r.calls, 260, 'вызовов отрисовки ' + y); T.lt(r.tris, 350000, 'треугольников ' + y);
    });
  });
});
