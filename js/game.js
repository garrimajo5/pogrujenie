'use strict';
/* Главный цикл, игрок внутри корабля, взаимодействия, интерфейс */
(function (G) {
  const V3 = THREE.Vector3;
  const $ = id => document.getElementById(id);
  const Game = G.Game = {
    state: 'intro', t: 0, piloting: false, room: 'bridge', done: false, shake: 0,
    player: { x: 0, z: -3.0, yaw: 0, pitch: -0.08, bob: 0 },
    scan: { key: null, prog: 0 }
  };
  let renderer, scene, camera, hemi, sun, clock;
  const tmp = { v1: new V3(), v2: new V3(), v3: new V3(), q: new THREE.Quaternion(), qy: new THREE.Quaternion(), e: new THREE.Euler(0, 0, 0, 'YXZ'), up: new V3(0, 1, 0) };
  const fogA = new THREE.Color(0x1a6676), fogB = new THREE.Color(0x041a22);

  Game.init = function () {
    renderer = new THREE.WebGLRenderer({ canvas: $('view'), antialias: true, powerPreference: 'high-performance' });
    renderer.setPixelRatio(Math.min(window.devicePixelRatio || 1, 1.25));
    scene = new THREE.Scene();
    scene.fog = new THREE.FogExp2(fogA.clone(), 0.017);
    scene.background = fogA.clone();
    camera = new THREE.PerspectiveCamera(72, 1, 0.05, 420);
    hemi = new THREE.HemisphereLight(0x9ad9e8, 0x0b1c24, 0.9); scene.add(hemi);
    sun = new THREE.DirectionalLight(0xc8f2ff, 0.55); sun.position.set(0.3, 1, 0.25); scene.add(sun);
    G.World.build(scene);
    G.Sonar.init();
    G.Ship.build(scene);
    G.Eco.init(scene);
    G.Ship.group.add(camera); camera.position.set(0, 1.65, -3);
    G.Ctl.init(); bindUI(); G.DebugPanel.init(); resize(); addEventListener('resize', resize);
    clock = new THREE.Clock();
    renderer.setAnimationLoop(loop);
    if (G.Save.load() && G.Save.hasProgress()) { const c = counts(); $('loading').textContent = `Журнал загружен: видов ${c.s} из ${c.sn}, мест ${c.p} из ${c.pn}.`; }
    else $('loading').hidden = true;
    $('b-start').hidden = false;
    updateCounts();
  };

  /* отладка: прогон симуляции без отрисовки и статистика рендера */
  Game.debug = {
    info: () => ({ calls: renderer.info.render.calls, tris: renderer.info.render.triangles, geos: renderer.info.memory.geometries }),
    step(n, dt) {
      dt = dt || 0.05;
      for (let i = 0; i < n; i++) {
        Game.t += dt; G.timeU.value = Game.t;
        G.Ctl.read(); updatePlayer(dt);
        G.Ship.update(dt, Game.t); G.World.update(dt, Game.t); G.Eco.update(dt, Game.t); G.Sonar.update(dt, Game.t);
        handleEvents(); updateDiscover();
      }
      G.Ctl.lookDX = G.Ctl.lookDY = 0;
    },
    /* полный кадр логики, как в игре: ввод, взаимодействия, сканирование, события */
    frame(n, dt) {
      dt = dt || 0.05;
      for (let i = 0; i < n; i++) {
        Game.t += dt; G.timeU.value = Game.t; G.Ctl.read();
        if (Game.state === 'play') simulate(dt);
        else if (Game.state === 'journal' && G.Ctl.journalPressed) closeJournal();
        clearOneShots();
      }
    },
    /* смотреть из текущего места на точку мира */
    lookAt(world) {
      const P = Game.player, loc = G.Ship.group.worldToLocal(world.clone());
      const eye = Game.piloting ? new V3(0, 1.62, -4.3) : new V3(P.x, 1.65, P.z);
      const d = loc.sub(eye);
      P.yaw = Math.atan2(-d.x, -d.z); P.pitch = Math.atan2(d.y, Math.hypot(d.x, d.z));
      updatePlayer(0);
    },
    /* вернуть мир и игрока в исходное состояние (для тестов) */
    paused: false,
    reset() {
      G.seed(12345); G.Save.wipe();
      G.Ship.reset(); G.Eco.reset(); G.Eco.events.length = 0;
      G.SPECIES_ORDER.forEach(k => { G.SPECIES[k].scanned = false; G.SPECIES[k].seen.clear(); });
      G.World.pois.forEach(p => { p.found = false; p.pinged = false; });
      G.Ship.doors.forEach(d => { d.open = false; d.t = 0; });
      if (Game.piloting) Game.toggleHelm();
      const P = Game.player; P.x = 0; P.z = -3; P.yaw = 0; P.pitch = -0.08;
      Object.assign(G.Ctl.padMove, { x: 0, y: 0 }); G.Ctl.btnVert = 0; G.Ctl.actionDown = false; G.Ctl.keys = {};
      Game.scan.key = null; Game.scan.prog = 0; Game.done = false;
      ['journal', 'lost', 'intro'].forEach(id => { $(id).hidden = true; }); $('hud').hidden = false;
      Game.state = 'play'; updateCounts(); updatePlayer(0);
    },
    get camera() { return camera; },
    teleport: key => G.DebugPanel.teleport(key),
    get pixelRatio() { return curPR; },
    actionLabel: () => $('act-label').textContent,
    feed: () => [...document.querySelectorAll('#feed div')].map(d => d.textContent)
  };

  function resize() {
    const w = window.innerWidth, h = window.innerHeight;
    renderer.setSize(w, h, false); camera.aspect = w / h;
    camera.fov = w < h ? 85 : 72; camera.updateProjectionMatrix();
  }

  /* адаптивное разрешение: держим плавность на слабых телефонах */
  const maxPR = Math.min(window.devicePixelRatio || 1, 1.6);
  let curPR = Math.min(maxPR, 1.25), fpsAcc = 0, fpsN = 0;
  function adapt(raw) {
    fpsAcc += raw; fpsN++;
    if (fpsAcc < 2.5) return;
    const fps = fpsN / fpsAcc; fpsAcc = 0; fpsN = 0;
    let n = curPR;
    if (fps < 42 && curPR > 0.7) n = Math.max(0.7, curPR - 0.2);
    else if (fps > 58 && curPR < maxPR) n = Math.min(maxPR, curPR + 0.15);
    if (n !== curPR) { curPR = n; renderer.setPixelRatio(curPR); resize(); }
  }

  function loop() {
    const raw = clock.getDelta();
    if (Game.state !== 'intro') adapt(raw);
    G.DebugPanel.tick(raw);
    const dt = Math.min(raw, 0.05);
    Game.t += dt; G.timeU.value = Game.t;
    const S = G.Ship, C = G.Ctl;
    C.read();
    if (Game.debug.paused) { renderer.render(scene, camera); return; }
    if (Game.state === 'play') {
      simulate(dt);
    } else if (Game.state === 'intro') {
      camera.quaternion.setFromEuler(tmp.e.set(-0.04, Math.sin(Game.t * 0.08) * 0.7, 0, 'YXZ'));
      S.update(dt, Game.t); G.World.update(dt, Game.t); G.Eco.update(dt, Game.t); G.Sonar.update(dt, Game.t);
      G.Eco.events.length = 0;
    } else if (Game.state === 'journal') {
      if (C.journalPressed) closeJournal();
    }
    clearOneShots();
    updateEnv(); updateHUD(dt);
    renderer.render(scene, camera);
  }

  /* один шаг игровой логики в режиме «play» (без рендера) */
  function simulate(dt) {
    const S = G.Ship, C = G.Ctl;
    if (C.journalPressed) Game.openJournal();
    if (C.lightPressed) Game.toggleLight();
    if (C.standPressed && Game.piloting) Game.toggleHelm();
    if (C.pingPressed) G.Sonar.ping();
    updatePlayer(dt);
    S.update(dt, Game.t); G.World.update(dt, Game.t); G.Eco.update(dt, Game.t); G.Sonar.update(dt, Game.t);
    handleEvents(); updateInteraction(dt); updateDiscover(); ambience(dt);
    G.Save.tick(dt);
  }
  function clearOneShots() {
    const C = G.Ctl;
    C.actionPressed = false; C.pingPressed = false; C.lightPressed = false; C.journalPressed = false; C.standPressed = false;
    C.lookDX = C.lookDY = 0;
  }

  /* ---------- игрок ---------- */
  function updatePlayer(dt) {
    const P = Game.player, C = G.Ctl, S = G.Ship, sens = 0.0055;
    if (C.gyro && C.gyroOK) {
      if (C.needCalib) { tmp.e.setFromQuaternion(C.gyroQ, 'YXZ'); C.yawOffset = P.yaw - tmp.e.y; C.needCalib = false; }
      C.yawOffset -= C.lookDX * sens;
      tmp.qy.setFromAxisAngle(tmp.up, C.yawOffset);
      camera.quaternion.copy(tmp.qy).multiply(C.gyroQ);
      tmp.e.setFromQuaternion(camera.quaternion, 'YXZ'); P.yaw = tmp.e.y; P.pitch = tmp.e.x;
    } else {
      P.yaw -= C.lookDX * sens;
      P.pitch = G.clamp(P.pitch - C.lookDY * sens, -1.45, 1.45);
      camera.quaternion.setFromEuler(tmp.e.set(P.pitch, P.yaw, 0, 'YXZ'));
    }
    let cx, cy, cz;
    if (Game.piloting) {
      S.input.thrust = C.move.y; S.input.turn = C.move.x; S.input.vert = C.vert;
      cx = 0; cy = 1.62; cz = -4.3;
    } else {
      S.input.thrust = S.input.turn = S.input.vert = 0;
      let mx = C.move.x, my = C.move.y;
      const len = Math.hypot(mx, my);
      if (len > 0) {
        mx /= len; my /= len;
        const sp = 2.4 * dt, fx = -Math.sin(P.yaw), fz = -Math.cos(P.yaw), rx = Math.cos(P.yaw), rz = -Math.sin(P.yaw);
        const dx = (fx * my + rx * mx) * sp, dz = (fz * my + rz * mx) * sp;
        if (S.canStand(P.x + dx, P.z)) P.x += dx;
        if (S.canStand(P.x, P.z + dz)) P.z += dz;
        P.bob += dt * 9;
      }
      Game.room = S.roomAt(P.z);
      cx = P.x; cy = 1.65 + Math.sin(P.bob) * 0.03; cz = P.z;
    }
    if (Game.piloting) Game.room = 'bridge';
    Game.shake = Math.max(0, Game.shake - dt);
    const sh = Game.shake * 0.25;
    camera.position.set(cx + G.rr(-sh, sh), cy + G.rr(-sh, sh), cz + G.rr(-sh, sh));
  }

  /* ---------- действия ---------- */
  Game.toggleHelm = function () {
    Game.piloting = !Game.piloting;
    const P = Game.player;
    if (Game.piloting) { P.yaw = 0; P.pitch = -0.08; G.Audio.beep(660); toastOnce('helm', 'Вы за штурвалом', 'Крестовина ведёт корабль, «Всплыть» и «Вниз» меняют глубину, «Сонар» показывает, что вокруг. Круглая кнопка — встать или сканировать.'); }
    else { P.x = 0; P.z = -3.2; P.yaw = 0; G.Ship.input.thrust = G.Ship.input.turn = G.Ship.input.vert = 0; G.Audio.beep(440); }
    $('pilotbtns').hidden = !Game.piloting;
    if (G.Ctl.gyro) G.Ctl.needCalib = true;
  };
  Game.toggleLight = function () {
    const S = G.Ship;
    if (!S.light && S.energy < 3) { Game.toast('Мало энергии', 'Прожектор не включить'); return; }
    S.light = !S.light; G.Audio.beep(S.light ? 760 : 380);
    $('b-light').classList.toggle('lit', S.light);
  };
  Game.zap = function () {
    const S = G.Ship;
    if (S.energy < 15) { Game.toast('Мало энергии', 'Для разряда нужно 15 единиц'); G.Audio.beep(220); return; }
    S.energy -= 15; S.flicker = 0.8; G.Audio.zap(); flash('rgba(94,242,214,.55)');
    G.Eco.onZap();
  };
  Game.hullHit = function (n, kind) {
    const S = G.Ship; if (S.hull <= 0) return;
    S.hull = Math.max(0, S.hull - n); Game.shake = 0.6; S.flicker = 0.4;
    flash('rgba(255,93,108,.5)'); G.Audio.thud();
    feed(kind === 'scrape' ? `Удар о дно: корпус −${n}%` : `Удар по корпусу: −${n}%`);
    if (S.hull <= 0) lose();
  };
  Game.onLatch = function () { G.Audio.latch(); G.Ship.flicker = 1.5; flash('rgba(191,246,255,.4)'); Game.shake = 0.3; };

  function lose() {
    Game.state = 'lost'; $('lost').hidden = false; G.Audio.alarm();
    if (Game.piloting) Game.toggleHelm();
  }
  function restart() {
    G.Ship.reset(); G.Eco.reset();
    const P = Game.player; P.x = 0; P.z = -3; P.yaw = 0; P.pitch = -0.08;
    $('lost').hidden = true; Game.state = 'play'; clock.getDelta();
  }

  /* ---------- сканирование и взаимодействие ---------- */
  function nearestInter() {
    const P = Game.player; let best = null, bd = 1e9;
    const fx = -Math.sin(P.yaw), fz = -Math.cos(P.yaw);
    for (const it of G.Ship.inter) {
      const dx = it.x - P.x, dz = it.z - P.z, d = Math.hypot(dx, dz);
      if (d > it.r) continue;
      if (d > 0.9 && (dx * fx + dz * fz) / d < 0.3) continue;
      if (d < bd) { bd = d; best = it; }
    }
    return best;
  }
  function findScanTarget() {
    const camPos = camera.getWorldPosition(tmp.v1), camDir = camera.getWorldDirection(tmp.v2);
    const ld = tmp.v3.copy(camDir).applyQuaternion(tmp.q.copy(G.Ship.group.quaternion).invert());
    if (!G.Ship.throughWindow(camera.position, ld, Game.room)) return null;
    let best = null, bs = 1;
    for (const tg of G.Eco.scanTargets()) {
      const dx = tg.pos.x - camPos.x, dy = tg.pos.y - camPos.y, dz = tg.pos.z - camPos.z;
      const dist = Math.hypot(dx, dy, dz);
      if (dist > 80 || dist < 1) continue;
      const ang = Math.acos(G.clamp((dx * camDir.x + dy * camDir.y + dz * camDir.z) / dist, -1, 1));
      const s = ang / Math.max(0.065, Math.atan(tg.r / dist));
      if (s < bs) { bs = s; best = { sp: tg.sp, dist }; }
    }
    return best;
  }
  function updateInteraction(dt) {
    const C = G.Ctl, SP = G.SPECIES;
    const tgt = findScanTarget();
    $('reticle').classList.toggle('aim', !!tgt);
    $('target').textContent = tgt ? `${SP[tgt.sp].scanned ? SP[tgt.sp].name : 'Неизвестный вид'} · ${Math.round(tgt.dist)} м` : '';
    let act = null;
    let it = Game.piloting ? null : nearestInter();
    if (it && tgt) { // если в прицеле существо, пост перехватывает кнопку только при прямом взгляде на него
      const P = Game.player, ld = camera.getWorldDirection(tmp.v2).applyQuaternion(tmp.q.copy(G.Ship.group.quaternion).invert());
      const dx = it.x - P.x, dy = 1.0 - 1.65, dz = it.z - P.z, d = Math.hypot(dx, dy, dz);
      if ((dx * ld.x + dy * ld.y + dz * ld.z) / d < 0.94) it = null;
    }
    if (it) act = { label: it.label(), fn: it.fn, dis: it.ok ? !it.ok() : false };
    else if (tgt) {
      const sp = SP[tgt.sp];
      act = sp.scanned ? { label: 'Сведения: ' + sp.name, fn: () => Game.openJournal(tgt.sp) } : { label: 'Сканировать (держать)', hold: tgt.sp };
    } else if (Game.piloting) act = { label: 'Встать', fn: () => Game.toggleHelm() };

    if (act && act.hold) {
      if (C.actionDown) {
        if (Game.scan.key !== act.hold) { Game.scan.key = act.hold; Game.scan.prog = 0; }
        Game.scan.prog += dt / 1.5; G.Audio.scanTick(Game.scan.prog);
        if (Game.scan.prog >= 1) { completeScan(act.hold); Game.scan.prog = 0; }
      } else Game.scan.prog = Math.max(0, Game.scan.prog - dt * 2);
    } else Game.scan.prog = 0;
    $('scanring').style.setProperty('--p', Game.scan.prog.toFixed(3));
    if (act && !act.hold && !act.dis && C.actionPressed) act.fn();
    const btn = $('b-action'), lbl = act ? act.label : 'Действие';
    if (btn.dataset.l !== lbl) { $('act-label').textContent = lbl; btn.dataset.l = lbl; }
    btn.classList.toggle('dis', !act || !!act.dis);
    btn.classList.toggle('scan', !!(act && act.hold));
  }
  function completeScan(key) {
    const sp = G.SPECIES[key]; if (sp.scanned) return;
    sp.scanned = true; G.Audio.scanDone(); G.Save.mark();
    Game.toast('Новый вид: ' + sp.name, sp.desc);
    feed('Вид занесён в журнал: ' + sp.name, true);
    updateCounts(); checkComplete();
  }
  function handleEvents() {
    const E = G.Eco, S = G.Ship;
    for (const ev of E.events) {
      if (ev.pos.distanceTo(S.center) > 80) continue;
      const sp = G.SPECIES[ev.sp];
      let isNew = false;
      if (ev.note && !sp.seen.has(ev.note)) { sp.seen.add(ev.note); isNew = true; G.Save.mark(); }
      if (ev.msg) feed(ev.msg(), isNew); else if (isNew) feed('Новое наблюдение: ' + G.nm(ev.sp), true);
    }
    E.events.length = 0;
  }
  function updateDiscover() {
    const S = G.Ship;
    for (const p of G.World.pois) {
      if (p.found) continue;
      if (Math.hypot(p.pos.x - S.pos.x, p.pos.z - S.pos.z) < p.rd) {
        p.found = true; G.Audio.discover(); G.Save.mark();
        Game.toast('Открытие: ' + p.name, p.desc);
        feed('Место отмечено на сонаре: ' + p.name, true);
        updateCounts(); checkComplete();
      }
    }
  }
  function counts() {
    return { s: G.SPECIES_ORDER.filter(k => G.SPECIES[k].scanned).length, p: G.World.pois.filter(p => p.found).length, sn: G.SPECIES_ORDER.length, pn: G.World.pois.length };
  }
  function updateCounts() { const c = counts(); $('j-count').textContent = `${c.s}/${c.sn} · ${c.p}/${c.pn}`; }
  function checkComplete() {
    const c = counts();
    if (!Game.done && c.s === c.sn && c.p === c.pn) {
      Game.done = true;
      setTimeout(() => Game.toast('Отмель изучена', 'Все виды описаны, все места найдены. Следующий слой — Кристальный лес — появится в новой версии.', 7000), 4200);
    }
  }

  /* ---------- атмосфера ---------- */
  let callT = 8;
  function ambience(dt) {
    const S = G.Ship, E = G.Eco;
    callT -= dt;
    if (callT <= 0) {
      callT = G.rr(14, 26);
      let nd = 1e9; E.rays.forEach(r => { nd = Math.min(nd, r.p.distanceTo(S.center)); });
      if (nd < 110) G.Audio.whale(nd); else G.Audio.creak();
    }
    G.Audio.engine(Math.abs(S.input.thrust) * 0.8 + Math.abs(S.speed) / 7 * 0.4);
    let danger = 0, warn = '';
    E.preds.forEach(p => { if (p.state === 'charge') danger = Math.max(danger, 1 - p.p.distanceTo(S.center) / 40); });
    if (danger > 0) warn = 'Хищник идёт на таран! Погасите свет, уйдите под вуаль или дайте разряд';
    if (E.latched) { warn = 'Стеклоуст на корпусе пьёт энергию. Разряд — пульт инженера у левого борта'; danger = Math.max(danger, 0.5); }
    else if (!warn && S.energy < 15) warn = 'Мало энергии: сбавьте ход и погасите прожектор';
    if (S.depthLimit > 0) { S.depthLimit -= dt; warn = 'Предельная глубина. Корпус не рассчитан на большее'; }
    if (!warn && S.hull < 25) warn = 'Корпус повреждён';
    if ($('warn').textContent !== warn) $('warn').textContent = warn;
    $('fx-danger').style.opacity = (danger * (0.6 + 0.4 * Math.sin(Game.t * 8))).toFixed(2);
  }
  function updateEnv() {
    const k = G.smooth(8, 95, -G.Ship.pos.y);
    scene.fog.color.copy(fogA).lerp(fogB, k); scene.background.copy(scene.fog.color);
    scene.fog.density = 0.015 + 0.01 * k;
    hemi.intensity = 0.95 - 0.55 * k; sun.intensity = 0.55 - 0.4 * k;
  }

  /* ---------- интерфейс ---------- */
  let hudT = 0;
  function setBar(id, v) { const el = $(id); el.style.width = v.toFixed(0) + '%'; el.className = v < 25 ? 'lo' : v < 50 ? 'mid' : ''; }
  function updateHUD(dt) {
    hudT -= dt; if (hudT > 0) return; hudT = 0.12;
    const S = G.Ship;
    $('s-depth').textContent = Math.round(-S.pos.y);
    $('s-head').textContent = String(Math.round(((-S.yaw * 180 / Math.PI) % 360 + 360) % 360)).padStart(3, '0');
    $('s-speed').textContent = Math.abs(S.speed).toFixed(1);
    setBar('s-hull', S.hull); setBar('s-energy', S.energy);
    $('s-place').textContent = Game.piloting ? 'За штурвалом' : { bridge: 'Мостик', corr: 'Коридор', lab: 'Лаборатория' }[Game.room];
    $('b-light').classList.toggle('lit', S.light);
  }
  function feed(text, isNew) {
    const box = $('feed'), d = document.createElement('div');
    d.textContent = text; if (isNew) { d.className = 'new'; d.dataset.tag = 'в журнал'; }
    box.prepend(d);
    while (box.children.length > 4) box.lastChild.remove();
    setTimeout(() => { d.style.opacity = '0'; setTimeout(() => d.remove(), 1000); }, 9000);
  }
  let toastTimer = 0;
  Game.toast = function (title, text, ms) {
    const el = $('toast');
    el.innerHTML = ''; const h = document.createElement('h3'); h.textContent = title; el.appendChild(h);
    if (text) { const p = document.createElement('p'); p.textContent = text; el.appendChild(p); }
    el.classList.add('show'); clearTimeout(toastTimer);
    toastTimer = setTimeout(() => el.classList.remove('show'), ms || 4200);
  };
  const shownOnce = new Set();
  function toastOnce(key, a, b) { if (shownOnce.has(key)) return; shownOnce.add(key); Game.toast(a, b, 6000); }
  function flash(color) { const el = $('fx-hit'); el.style.transition = 'none'; el.style.background = color; el.style.opacity = '1'; void el.offsetWidth; el.style.transition = 'opacity .6s'; el.style.opacity = '0'; }

  /* ---------- журнал ---------- */
  Game.resetProgress = function () {
    G.Save.wipe();
    G.SPECIES_ORDER.forEach(k => { G.SPECIES[k].scanned = false; G.SPECIES[k].seen.clear(); });
    G.World.pois.forEach(p => { p.found = false; p.pinged = false; });
    Game.done = false; updateCounts();
    if (Game.state === 'journal') renderJournal();
    Game.toast('Журнал очищен', 'Экспедиция начинается заново.');
  };
  Game.openJournal = function (focus) {
    if (Game.state !== 'play') return;
    Game.state = 'journal'; renderJournal(focus); $('journal').hidden = false; G.Audio.beep(520);
  };
  function closeJournal() { $('journal').hidden = true; Game.state = 'play'; clock.getDelta(); }
  function esc(s) { return String(s).replace(/[&<>"]/g, c => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;' }[c])); }
  function renderJournal(focus) {
    const SP = G.SPECIES, c = counts();
    let h = `<div class="j-head"><div><div class="tag">Журнал экспедиции · Подлёдный сад</div><h2>Отмель</h2></div><button id="j-close" class="chip">Закрыть</button></div>
      <p class="j-sub">Изучено видов ${c.s} из ${c.sn} · найдено мест ${c.p} из ${c.pn}. Наблюдения записываются, когда существа взаимодействуют рядом с кораблём.</p>
      <h3>Существа</h3><div class="j-grid">`;
    G.SPECIES_ORDER.forEach(k => {
      const s = SP[k], total = Object.keys(s.notes).length;
      const notes = [...s.seen].map(n => `<li>${esc(s.notes[n])}</li>`).join('');
      h += `<article class="j-card ${s.scanned ? '' : 'unk'}" id="j-${k}"><h4>${s.scanned ? esc(s.name) : 'Неизвестный вид'}</h4>
        <p>${s.scanned ? esc(s.desc) : 'Наведите прицел на существо через иллюминатор и удерживайте кнопку действия.'}</p>
        ${notes ? `<div class="k">Наблюдения</div><ul>${notes}</ul>` : ''}<div class="j-prog">наблюдений ${s.seen.size} из ${total}</div></article>`;
    });
    h += '</div><h3>Места</h3><div class="j-grid">';
    G.World.pois.forEach(p => {
      h += `<article class="j-card ${p.found ? '' : 'unk'}"><h4>${p.found ? esc(p.name) : '???'}</h4><p>${p.found ? esc(p.desc) : (p.pinged ? 'Сонар засёк что-то необычное: фиолетовая метка «?» на голостоле.' : 'Ещё не найдено. Импульс сонара покажет необычные места поблизости.')}</p></article>`;
    });
    h += '</div><div class="j-foot"><button id="j-reset" class="chip warn" type="button">Сбросить прогресс</button><span id="j-reset-hint"></span></div>';
    $('journal-body').innerHTML = h;
    $('j-close').onclick = closeJournal;
    const rb = $('j-reset');
    rb.onclick = () => {
      if (rb.dataset.armed === '1') { Game.resetProgress(); return; }
      rb.dataset.armed = '1'; rb.textContent = 'Стереть журнал безвозвратно';
      $('j-reset-hint').textContent = 'Нажмите ещё раз в течение 4 секунд.';
      setTimeout(() => { if (rb.isConnected) { rb.dataset.armed = ''; rb.textContent = 'Сбросить прогресс'; $('j-reset-hint').textContent = ''; } }, 4000);
    };
    if (focus) { const el = $('j-' + focus); if (el) { el.classList.add('focus'); el.scrollIntoView({ block: 'center' }); } }
  }

  function bindUI() {
    $('b-start').onclick = () => {
      G.Audio.init();
      const el = document.documentElement;
      if (matchMedia('(pointer:coarse)').matches && el.requestFullscreen) {
        el.requestFullscreen().then(() => { if (screen.orientation && screen.orientation.lock) screen.orientation.lock('landscape').catch(() => {}); }).catch(() => {});
      }
      $('intro').hidden = true; $('hud').hidden = false;
      Game.state = 'play'; Game.player.yaw = 0; G.Eco.events.length = 0; clock.getDelta();
      Game.toast('Погружение началось', 'Подойдите к штурвалу у носового иллюминатора и нажмите кнопку действия.', 6000);
    };
    $('b-journal').onclick = () => Game.openJournal();
    $('b-sound').onclick = () => { G.Audio.init(); const m = G.Audio.toggleMute(); $('b-sound').classList.toggle('off', m); $('b-sound').textContent = m ? 'Звук выкл' : 'Звук'; };
    $('b-restart').onclick = restart;
    if (G.Ctl.gyroAvailable()) {
      $('b-gyro').hidden = false;
      $('b-gyro').onclick = async () => {
        const on = await G.Ctl.toggleGyro();
        $('b-gyro').classList.toggle('on', on);
        if (on) setTimeout(() => { if (G.Ctl.gyro && !G.Ctl.gyroOK) { G.Ctl.toggleGyro(); $('b-gyro').classList.remove('on'); Game.toast('Гироскоп недоступен', 'Браузер не отдаёт данные датчиков. Откройте игру по ссылке https://'); } }, 1500);
      };
    }
  }

  window.addEventListener('load', () => {
    if (!window.THREE) { $('loading').textContent = 'Не удалось загрузить lib/three.min.js.'; return; }
    try { Game.init(); } catch (e) { $('loading').textContent = 'Ошибка запуска: ' + e.message; console.error(e); }
  });
  window.addEventListener('error', e => { const el = $('errbox'); if (el) { el.hidden = false; el.textContent = 'Ошибка: ' + e.message + ' (' + (e.filename || '').split('/').pop() + ':' + e.lineno + ')'; } });
})(window.G);
