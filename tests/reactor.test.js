'use strict';
/* Машинное отделение: отсек за лабораторией, реактор и пульт распределения энергии */
T.describe('Машинное отделение', () => {
  const S = () => G.Ship, D = () => G.Game.debug, P = () => G.Game.player;
  const press = () => { G.Ctl.actionPressed = true; G.Ctl.actionDown = true; D().frame(1); G.Ctl.actionDown = false; D().frame(1); };
  const atConsole = () => { const p = P(); p.x = 1.4; p.z = 15.9; p.yaw = -Math.PI / 2; p.pitch = -0.3; D().frame(1); };
  const tap = (x, y) => { G.Ctl.padMove.x = x; G.Ctl.padMove.y = y; D().frame(1); G.Ctl.padMove.x = 0; G.Ctl.padMove.y = 0; D().frame(1); };

  T.test('отсек за лабораторией: где стоять и как называется', () => {
    T.ok(S().canStand(0, 15), 'у люка');
    T.ok(S().canStand(-2.2, 19.2), 'дальний угол');
    T.ok(!S().canStand(3.3, 17), 'за бортом');
    T.ok(!S().canStand(0, 17.8), 'внутри реактора');
    T.ok(!S().canStand(2.65, 15.9), 'внутри пульта');
    T.eq(S().roomAt(17), 'eng'); T.eq(S().roomAt(13), 'lab');
    T.eq(S().doors.length, 3, 'третий люк');
  });
  T.test('люк в корме лаборатории: закрытый не пускает, открывается кнопкой', () => {
    T.ok(!S().canStand(0, 14), 'люк закрыт');
    const p = P(); p.x = 0; p.z = 13.2; p.yaw = Math.PI; p.pitch = 0;
    G.Ctl.padMove.y = 1; D().frame(40);
    T.lt(p.z, 13.7, 'прошёл сквозь закрытый люк');
    G.Ctl.padMove.y = 0; D().frame(1);
    T.eq(D().actionLabel(), 'Открыть люк');
    press(); D().frame(30);
    T.gt(S().doors[2].t, 0.85, 'люк не открылся');
    G.Ctl.padMove.y = 1; D().frame(40); G.Ctl.padMove.y = 0;
    T.gt(p.z, 15, 'не прошёл в машинное отделение');
    T.eq(G.Game.room, 'eng');
    D().hud();
    T.eq(document.getElementById('s-place').textContent, 'Машинное отделение');
  });
  T.test('иллюминаторы машинного отделения', () => {
    T.ok(S().throughWindow(T.V(0, 1.65, 16), T.V(0, 0.05, 1).normalize(), 'eng'), 'кормовой иллюминатор');
    T.ok(!S().throughWindow(T.V(0, 1.65, 16), T.V(0, 1, 0), 'eng'), 'потолок');
  });
  T.test('по умолчанию реактор поделен поровну, сумма 100', () => {
    S().POWER.forEach(p => T.eq(S().power[p.key], 20, p.key));
    T.eq(S().POWER.length, 5); T.eq(S().powerUsed(), 100); T.eq(S().powerFree(), 0);
  });
  T.test('сесть за пульт и отойти кнопкой действия', () => {
    atConsole();
    T.eq(D().actionLabel(), 'Пульт реактора');
    press();
    T.eq(G.Game.station, 'power');
    T.ok(!document.getElementById('power').hidden, 'панель не показана');
    T.eq(document.querySelectorAll('#pw-rows .pw-row').length, 5);
    T.eq(D().actionLabel(), 'Отойти от пульта');
    const z = P().z; G.Ctl.padMove.y = 1; D().frame(20); G.Ctl.padMove.y = 0; D().frame(1);
    T.eq(P().z, z, 'игрок ушёл от пульта крестовиной');
    press();
    T.eq(G.Game.station, null);
    T.ok(document.getElementById('power').hidden, 'панель не спряталась');
    P().yaw = Math.PI; G.Ctl.padMove.y = 1; D().frame(10); G.Ctl.padMove.y = 0;
    T.gt(P().z, z + 0.5, 'после пульта не ходит');
  });
  T.test('крестовина выбирает систему и перекладывает энергию', () => {
    atConsole(); press();
    T.eq(S().powerSel, 0);
    tap(1, 0);
    T.eq(S().power.engine, 20, 'прибавилось сверх 100');
    T.ok(/Свободной энергии нет/.test(document.getElementById('pw-msg').textContent), 'нет подсказки');
    tap(0, -1); T.eq(S().powerSel, 1, 'вниз — следующая система');
    tap(-1, 0); T.eq(S().power.sonar, 15);
    T.eq(S().powerFree(), 5);
    T.eq(document.getElementById('pw-free').textContent, 'Свободно 5 из 100');
    tap(0, 1); T.eq(S().powerSel, 0, 'вверх — предыдущая');
    tap(1, 0); T.eq(S().power.engine, 25);
    T.eq(S().powerUsed(), 100);
    tap(0, 1); T.eq(S().powerSel, 4, 'выбор по кругу');
    T.ok(document.querySelectorAll('#pw-rows .pw-row')[4].classList.contains('sel'));
    T.eq(document.querySelectorAll('#pw-rows .pw-row b')[0].textContent, '25');
  });
  T.test('удержание повторяет шаг; меньше нуля и больше свободного не бывает', () => {
    atConsole(); press();
    G.Ctl.padMove.x = -1; D().frame(60); G.Ctl.padMove.x = 0; D().frame(1);
    T.eq(S().power.engine, 0, 'двигатели не обесточились удержанием');
    T.eq(S().powerFree(), 20);
    tap(0, -1);
    G.Ctl.padMove.x = 1; D().frame(60); G.Ctl.padMove.x = 0; D().frame(1);
    T.eq(S().power.sonar, 40, 'сонар забрал всё свободное');
    T.eq(S().powerUsed(), 100);
    S().POWER.forEach(p => { T.ok(S().power[p.key] >= 0 && S().power[p.key] % 5 === 0, p.key); });
  });
  T.test('зажатая при подходе кнопка не срабатывает сразу', () => {
    atConsole(); G.Ctl.padMove.x = 1; press(); G.Ctl.padMove.x = 0; D().frame(1);
    T.eq(S().power.engine, 20);
  });
  T.test('новое погружение возвращает равное распределение и уводит от пульта', () => {
    atConsole(); press(); tap(-1, 0);
    T.eq(S().power.engine, 15);
    G.Game.hullHit(500, 'bite');
    T.eq(G.Game.station, null, 'остался у пульта после аварии');
    document.getElementById('b-restart').click();
    T.eq(S().power.engine, 20); T.eq(S().powerFree(), 0);
  });
  T.test('ядро реактора и экран пульта не слиты со статикой', () => {
    T.ok(S().reactorCore.parent === S().group, 'ядро потерялось');
    D().step(1); const c1 = S().reactorCore.material.color.getHex();
    D().step(7); T.ok(S().reactorCore.material.color.getHex() !== c1, 'ядро не пульсирует');
  });
  T.test('бюджет отрисовки в машинном отделении', () => {
    S().doors[2].t = 1; S().doors[2].open = true;
    const p = P();
    [[0, 15, Math.PI], [1.4, 15.9, -Math.PI / 2], [0, 15, 0]].forEach(([x, z, yaw]) => {
      p.x = x; p.z = z; p.yaw = yaw; p.pitch = 0;
      const r = D().render();
      T.lt(r.calls, 260, 'вызовов отрисовки ' + yaw); T.lt(r.tris, 350000, 'треугольников ' + yaw);
    });
  });
});
