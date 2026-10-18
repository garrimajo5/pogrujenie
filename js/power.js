'use strict';
/* Машинное отделение: реактор даёт 100 единиц, инженер делит их между пятью системами.
   Доля системы меняет её силу: тягу двигателей, дальность сонара, восстановление корпуса,
   радиус разряда и дальность луча прожектора. У нуля система обесточена.
   Пульт: крестовина вверх/вниз выбирает систему, влево/вправо снимает или добавляет 5 единиц,
   кнопка действия закрывает пульт. На телефоне работают и кнопки −/+ на панели. */
(function (G) {
  const KEYS = ['eng', 'son', 'life', 'zap', 'lamp'];
  const NAME = { eng: 'Двигатели', son: 'Сонар', life: 'Жизнеобеспечение', zap: 'Разряд по корпусу', lamp: 'Прожектор' };
  const HINT = { eng: 'тяга', son: 'дальность импульса', life: 'ремонт корпуса', zap: 'радиус разряда', lamp: 'дальность луча' };
  const DEF = { eng: 30, son: 20, life: 25, zap: 15, lamp: 10 }; // базовые доли: при них системы работают как раньше
  const TOTAL = 100, MAX = 60, STEP = 5;
  const P = G.Power = { KEYS, NAME, DEF, TOTAL, MAX, STEP, v: Object.assign({}, DEF), sel: 0, el: null, open: false, held: 0, rep: 0 };

  P.used = () => KEYS.reduce((a, k) => a + P.v[k], 0);
  P.free = () => TOTAL - P.used();
  /* множитель системы относительно базовой доли: 1 — как раньше, 0 — обесточена */
  P.k = key => P.v[key] / DEF[key];
  P.set = function (key, val) {
    if (!KEYS.includes(key)) return P.v[key];
    const others = P.used() - P.v[key];
    P.v[key] = Math.max(0, Math.min(MAX, Math.round(val / STEP) * STEP, TOTAL - others));
    P.render(); G.Ship.drawPower && G.Ship.drawPower();
    return P.v[key];
  };
  P.add = (key, d) => P.set(key, P.v[key] + d);
  P.reset = function () { P.v = Object.assign({}, DEF); P.sel = 0; if (P.open) P.close(); P.render(); G.Ship.drawPower && G.Ship.drawPower(); };

  /* ---------- пульт ---------- */
  P.isOpen = () => P.open;
  P.toggle = () => (P.open ? P.close() : P.openPanel());
  P.openPanel = function () {
    if (!P.el) return;
    P.open = true; P.el.hidden = false; P.held = 0; P.render(); G.Audio.beep(560);
    if (!P.hinted) { P.hinted = true; G.Game.toast('Пульт энергии', 'Крестовина вверх-вниз выбирает систему, влево-вправо снимает или добавляет 5 единиц. Круглая кнопка — закрыть.'); }
  };
  P.close = function () { if (!P.el) return; P.open = false; P.el.hidden = true; G.Audio.beep(420); };
  /* ввод с крестовины: срабатывает при нажатии и повторяется, пока держат */
  P.nav = function (mx, my, dt) {
    const dir = Math.abs(my) > 0.5 ? (my > 0 ? 'up' : 'down') : Math.abs(mx) > 0.5 ? (mx > 0 ? 'right' : 'left') : null;
    if (!dir) { P.held = null; return; }
    if (dir === P.held) { P.rep -= dt; if (P.rep > 0) return; P.rep = 0.18; } else { P.held = dir; P.rep = 0.4; }
    if (dir === 'up') P.sel = (P.sel + KEYS.length - 1) % KEYS.length;
    else if (dir === 'down') P.sel = (P.sel + 1) % KEYS.length;
    else { const before = P.v[KEYS[P.sel]]; if (P.add(KEYS[P.sel], dir === 'right' ? STEP : -STEP) !== before) G.Audio.beep(dir === 'right' ? 700 : 500); }
    P.render();
  };

  P.render = function () {
    if (!P.el) return;
    KEYS.forEach((k, i) => {
      const row = P.el.querySelector(`[data-k="${k}"]`); if (!row) return;
      row.classList.toggle('sel', i === P.sel);
      row.querySelector('output').textContent = P.v[k];
      row.querySelector('i').style.width = (P.v[k] / MAX * 100) + '%';
      row.querySelector('[data-d="-"]').disabled = P.v[k] <= 0;
      row.querySelector('[data-d="+"]').disabled = P.v[k] >= MAX || P.free() <= 0;
      row.classList.toggle('off', P.v[k] === 0);
    });
    P.el.querySelector('#pw-free').textContent = `Резерв: ${P.free()} из ${TOTAL}`;
  };
  P.init = function () {
    const el = P.el = document.getElementById('power'); if (!el) return;
    el.querySelector('.pw-rows').innerHTML = KEYS.map(k => `<div class="pw-row" data-k="${k}"><div class="pw-name"><b>${NAME[k]}</b><small>${HINT[k]}</small></div>
      <button class="chip" data-d="-" type="button" aria-label="Меньше">−</button><div class="pw-bar"><i></i></div><output>0</output><button class="chip" data-d="+" type="button" aria-label="Больше">+</button></div>`).join('');
    el.addEventListener('pointerdown', e => e.stopPropagation());
    el.addEventListener('click', e => {
      const b = e.target.closest('button'); if (!b) return;
      if (b.dataset.a === 'close') { P.close(); return; }
      const row = b.closest('.pw-row'); if (!row) return;
      P.sel = KEYS.indexOf(row.dataset.k); P.add(row.dataset.k, b.dataset.d === '+' ? STEP : -STEP);
    });
    P.render();
  };
})(window.G);
