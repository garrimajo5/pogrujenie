'use strict';
/* Забег. Брифинг на голостоле: экипаж выбирает маршрут по местам отмели. Маршрут рисуется на сонаре,
   на приборах видно следующую точку и расстояние до неё. Когда все точки пройдены — маршрут закрыт,
   пора решать, глубже или наверх. Брифинг: крестовина вверх/вниз — выбор, вправо — взять маршрут,
   круглая кнопка закрывает. */
(function (G) {
  const ROUTES = [
    { id: 'reef', name: 'Рифовый сад', pois: ['flower', 'bones', 'well'], desc: 'Спокойный путь между рифами. Много жизни — и много серпохвостов.' },
    { id: 'vents', name: 'Тёплые трубы', pois: ['geyser', 'shell'], desc: 'Короткий путь к Раковине древних мимо чёрного курильщика: реактор будет греться.' },
    { id: 'edge', name: 'Край отмели', pois: ['well', 'drop'], desc: 'К обрыву над Кристальным лесом. Оползни поднимают ил — готовьтесь к сонарной тьме.' }
  ];
  const R = G.Run = { ROUTES, route: null, visited: new Set(), done: false, sel: 0, open: false, el: null, hudT: 0 };
  const poi = key => G.World.pois.find(p => p.key === key);
  R.next = () => (R.route ? R.route.pois.map(poi).find(p => p && !R.visited.has(p.key)) || null : null);
  R.choose = function (id) {
    const r = ROUTES.find(x => x.id === id); if (!r) return false;
    R.route = r; R.visited.clear(); R.done = false;
    const n = R.next();
    G.Game.toast('Маршрут: ' + r.name, `${r.desc} Первая точка — ${n ? n.name : '—'}. Она отмечена на голостоле.`, 5000);
    G.Game.feedMsg && G.Game.feedMsg(`Экипаж выбрал маршрут «${r.name}»`, true);
    G.Audio.discover(); R.render(); R.hud(true);
    return true;
  };
  R.reset = function () { R.route = null; R.visited.clear(); R.done = false; R.sel = 0; if (R.open) R.close(); R.hud(true); };
  R.update = function (dt) {
    if (R.route && !R.done) {
      const S = G.Ship;
      for (const key of R.route.pois) {
        const p = poi(key); if (!p || R.visited.has(key)) continue;
        if (Math.hypot(p.pos.x - S.pos.x, p.pos.z - S.pos.z) < p.rd) {
          R.visited.add(key);
          const n = R.next();
          if (n) { G.Game.feedMsg && G.Game.feedMsg(`Точка маршрута пройдена: ${p.name}. Дальше — ${n.name}`, true); G.Audio.beep(880); }
          else {
            R.done = true;
            G.Game.toast('Маршрут пройден', `«${R.route.name}» закрыт. Пора решать: глубже или наверх.`, 5000);
            G.Game.feedMsg && G.Game.feedMsg(`Маршрут «${R.route.name}» пройден`, true);
            G.Audio.discover();
          }
        }
      }
    }
    R.hudT -= dt; if (R.hudT <= 0) R.hud();
  };
  /* строка маршрута на приборах */
  R.hud = function (force) {
    R.hudT = 0.3;
    const el = document.getElementById('s-route'); if (!el) return;
    el.hidden = !R.route;
    if (!R.route) return;
    const n = R.next(), S = G.Ship;
    el.textContent = R.done ? `Маршрут «${R.route.name}» пройден` : `${R.route.name} · ${R.visited.size}/${R.route.pois.length} · ${n.name} ${Math.round(Math.hypot(n.pos.x - S.pos.x, n.pos.z - S.pos.z))} м`;
  };

  /* ---------- панель брифинга ---------- */
  R.isOpen = () => R.open;
  R.toggle = () => (R.open ? R.close() : R.openPanel());
  R.openPanel = function () { if (!R.el) return; R.open = true; R.el.hidden = false; R.held = null; R.render(); G.Audio.beep(560); };
  R.close = function () { if (!R.el) return; R.open = false; R.el.hidden = true; G.Audio.beep(420); };
  R.nav = function (mx, my) {
    const dir = Math.abs(my) > 0.5 ? (my > 0 ? 'up' : 'down') : mx > 0.5 ? 'right' : null;
    if (!dir) { R.held = null; return; }
    if (dir === R.held) return; R.held = dir;
    if (dir === 'up') R.sel = (R.sel + ROUTES.length - 1) % ROUTES.length;
    else if (dir === 'down') R.sel = (R.sel + 1) % ROUTES.length;
    else { R.choose(ROUTES[R.sel].id); R.close(); }
    R.render();
  };
  R.render = function () {
    if (!R.el) return;
    R.el.querySelectorAll('.cg-row').forEach((row, i) => {
      row.classList.toggle('sel', i === R.sel);
      row.classList.toggle('own', !!R.route && R.route.id === ROUTES[i].id);
      row.querySelector('button').textContent = R.route && R.route.id === ROUTES[i].id ? 'Выбран' : 'Выбрать';
    });
  };
  R.init = function () {
    const el = R.el = document.getElementById('brief'); if (!el) return;
    el.querySelector('.cg-rows').innerHTML = ROUTES.map(r => `<div class="cg-row" data-r="${r.id}"><div class="pw-name"><b>${r.name}</b><small>${r.pois.map(k => (poi(k) || {}).name).join(' → ')} · ${r.desc}</small></div><button class="chip" type="button">Выбрать</button></div>`).join('');
    el.addEventListener('pointerdown', e => e.stopPropagation());
    el.addEventListener('click', e => {
      const b = e.target.closest('button'); if (!b) return;
      if (b.dataset.a === 'close') { R.close(); return; }
      const row = b.closest('.cg-row'); if (!row) return;
      R.sel = ROUTES.findIndex(r => r.id === row.dataset.r); R.choose(row.dataset.r); R.close();
    });
    R.render(); R.hud(true);
  };
})(window.G);
