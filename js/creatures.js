'use strict';
/* Живая экосистема отмели.
   Пищевые связи: серпохвосты охотятся на светляков; вуали жалят рыб, хищники их обходят;
   скребуны пасутся на мху; стеклоусты подстерегают скребунов и рыб; за ледокрылами ходят стаи;
   огнёвки держат станции чистки на огнецвете: там серпохвосты не охотятся и избавляются от пиявников;
   хватни ловят светляков в темноте, пестрянки подманивают к ним стаи и доедают остатки. */
(function (G) {
  const V3 = THREE.Vector3;
  const E = G.Eco = { fish: [], shoals: [], preds: [], stations: [], anemones: [], shipSheltered: null, veils: [], grazers: [], eels: [], rays: [], events: [], shipHidden: false, latched: null, lastPing: -99 };
  const tv = new V3(), tv2 = new V3(), tv3 = new V3(), dummy = new THREE.Object3D();
  const H = (x, z) => G.World.height(x, z);
  const cap = G.cap;

  const SP = G.SPECIES = {
    fish: { name: 'Светляк-полосник', acc: 'светляка-полосника', unom: 'мелкая рыба', uacc: 'мелкую рыбу',
      desc: 'Мелкая рыба с полосами живого света. Держится стаями у кораллов и светится ярче, когда стая напугана.',
      notes: { flee: 'Завидев хищника, стая ныряет в заросли кораллов.', follow: 'Стаи сопровождают ледокрылов и подбирают всё, что те взбаламутили.' } },
    pred: { name: 'Серпохвост', acc: 'серпохвоста', unom: 'крупный хищник', uacc: 'крупного хищника',
      desc: 'Хищник рифа длиной около шести метров. Глаза светятся красным, по бокам тянется цепочка огней. Хвост-серп глушит добычу.',
      notes: { catch: 'Охотится на светляков, вырывая рыб из стаи.', avoid: 'Обходит фонарные вуали стороной.', ping: 'Слышит сонар и плывёт проверить источник звука.', ship: 'Идёт на таран, если у корабля горит прожектор.', zap: 'Разряд по корпусу отпугивает его.' } },
    veil: { name: 'Фонарная вуаль', acc: 'фонарную вуаль', unom: 'колония медуз', uacc: 'колонию медуз',
      desc: 'Колония медуз размером с автобус. Дрейфует по течению, свет мерцает в общем ритме.',
      notes: { sting: 'Жалит рыб, которые подплывают к щупальцам.', hide: 'Под её куполом хищники теряют корабль из виду.' } },
    grazer: { name: 'Скребун', acc: 'скребуна', unom: 'донный панцирник', uacc: 'донного панцирника',
      desc: 'Медлительный донный панцирник на шести лапах. Пасётся на пятнах светящегося мха.',
      notes: { graze: 'Объедает светящийся мох. Съеденные пятна гаснут и потом отрастают.' } },
    eel: { name: 'Стеклоуст', acc: 'стеклоуста', unom: 'прозрачный угорь', uacc: 'прозрачного угря',
      desc: 'Прозрачный угорь-засадчик. Прячется в каменных расщелинах, снаружи виден только красный глаз.',
      notes: { eelcatch: 'Бросается из укрытия на скребунов и рыб.', latch: 'Присасывается к корпусу и пьёт энергию.', zap: 'Разряд по корпусу сбрасывает его.' } },
    ray: { name: 'Ледокрыл', acc: 'ледокрыла', unom: 'гигантский скат', uacc: 'гигантского ската',
      desc: 'Огромный скат с размахом крыльев около 14 метров. Скользит под самым льдом и поёт низким голосом.',
      notes: { follow: 'За ним держатся стаи светляков.' } },
    ognevka: { name: 'Огнёвка', acc: 'огнёвку', unom: 'рачки-чистильщики', uacc: 'рачков-чистильщиков',
      desc: 'Светящиеся креветки длиной около 8 см. Держат «станции чистки» на вершинах огнецвета и снимают паразитов с крупных рыб.',
      notes: { clean: 'Снимают пиявников с серпохвостов, которые приплывают на станцию.', truce: 'У станции чистки серпохвост не охотится.', shelter: 'Корабль может переждать у станции: хищник отворачивает.' } },
    leech: { name: 'Пиявник', acc: 'пиявника', unom: 'паразит', uacc: 'паразита',
      desc: 'Плоский червь около 20 см. Присасывается к жабрам серпохвостов.',
      notes: { infest: 'Присасывается к жабрам серпохвоста.', angry: 'Заражённый хищник медленнее, но злее: замечает корабль издалека.', cleaned: 'Огнёвки снимают пиявников на станциях чистки.' } },
    hvaten: { name: 'Хватень', acc: 'хватня', gen: 'хватня', unom: 'донный анемон', uacc: 'донный анемон', ugen: 'донного анемона',
      desc: 'Анемон-хищник высотой около метра с сотней стрекающих щупалец. Сидит на дне рядом со стаями светляков.',
      notes: { catch: 'Ловит светляков, подплывших к щупальцам в темноте.', hide: 'Прячет щупальца от луча прожектора и раскрывает их снова только в темноте.' } },
    pestr: { name: 'Пестрянка', acc: 'пестрянку', unom: 'пёстрая рыбка', uacc: 'пёструю рыбку',
      desc: 'Рыбка длиной около 12 см в оранжево-белую полоску. Живёт среди щупалец хватня и не боится его стрекал.',
      notes: { lure: 'Выплывает к стае светляков и заманивает рыб в щупальца хозяина.', leftovers: 'Доедает остатки добычи хватня.', immune: 'Прячется в сжатых щупальцах: стрекала её не трогают.' } }
  };
  G.SPECIES_ORDER = ['fish', 'pred', 'veil', 'grazer', 'eel', 'ray', 'ognevka', 'leech', 'hvaten', 'pestr'];
  Object.values(SP).forEach(s => { s.scanned = false; s.seen = new Set(); });
  G.nm = (sp, cs) => { const s = SP[sp]; return s.scanned ? (cs === 'gen' ? s.gen || s.acc : cs === 'acc' ? s.acc : s.name.toLowerCase()) : (cs === 'gen' ? s.ugen || s.uacc : cs === 'acc' ? s.uacc : s.unom); };

  E.emit = (sp, note, pos, msg) => { E.events.push({ sp, note, pos: pos.clone(), msg }); };
  function killFish(f) { f.alive = false; f.t = G.rr(25, 40); }
  function clampArea(p) { p.x = G.clamp(p.x, -290, 290); p.z = G.clamp(p.z, -290, 290); }

  /* ======================= СВЕТЛЯКИ ======================= */
  function fishGeo() {
    const body = new THREE.SphereGeometry(1, 8, 6); body.scale(0.07, 0.1, 0.28);
    const tail = new THREE.BufferGeometry();
    tail.setAttribute('position', new THREE.Float32BufferAttribute([0, 0, -0.22, 0, 0.12, -0.42, 0, -0.12, -0.42], 3));
    tail.computeVertexNormals();
    return G.merge([{ g: body }, { g: tail, c: new THREE.Color(0.7, 0.7, 0.7) }]);
  }
  function initFish(scene) {
    const spots = G.World.coralSpots.slice().sort((a, b) => Math.hypot(a.x, a.z - 55) - Math.hypot(b.x, b.z - 55));
    const homes = [];
    for (const s of spots) { if (homes.every(h => Math.hypot(h.x - s.x, h.z - s.z) > 35)) homes.push(s); if (homes.length >= 6) break; }
    const PER = 22, N = homes.length * PER;
    const im = E.fishIM = new THREE.InstancedMesh(fishGeo(), new THREE.MeshBasicMaterial({ vertexColors: true, side: THREE.DoubleSide }), Math.max(1, N));
    im.frustumCulled = false; scene.add(im);
    const cols = [0x5ef2d6, 0xffd36b, 0x8fd0ff, 0xff8fd8, 0xb6ff7a, 0x7fe7ff];
    homes.forEach((h, si) => {
      const sh = { i: si, home: h.clone().add(new V3(0, 1.5, 0)), goal: h.clone(), c: h.clone(), v: new V3(), alarm: 0, follow: null, followT: 0, gt: 0, fleeCd: 0, n: 0, threat: null, members: [] };
      E.shoals.push(sh);
      const col = new THREE.Color(cols[si % cols.length]);
      for (let k = 0; k < PER; k++) {
        const f = { s: sh, p: sh.home.clone().add(new V3(G.rr(-3, 3), G.rr(0, 3), G.rr(-3, 3))), v: new V3(G.rr(-1, 1), 0, G.rr(-1, 1)), alive: true, t: 0, i: E.fish.length, ph: G.rr(0, 6) };
        E.fish.push(f); sh.members.push(f);
        im.setColorAt(f.i, col.clone().multiplyScalar(G.rr(0.75, 1.15)));
      }
    });
    if (im.instanceColor) im.instanceColor.needsUpdate = true;
  }
  function nearestFish(p, R) {
    let best = null, bd = R;
    for (const f of E.fish) { if (!f.alive || nearStation(f.p)) continue; const d = f.p.distanceTo(p); if (d < bd) { bd = d; best = f; } }
    return best;
  }
  function updateFish(dt, t) {
    const S = G.Ship;
    for (const sh of E.shoals) {
      sh.c.set(0, 0, 0); sh.v.set(0, 0, 0); sh.n = 0;
      for (const f of sh.members) if (f.alive) { sh.c.add(f.p); sh.v.add(f.v); sh.n++; }
      if (sh.n) { sh.c.divideScalar(sh.n); sh.v.divideScalar(sh.n); } else sh.c.copy(sh.home);
      sh.alarm = Math.max(0, sh.alarm - dt); sh.fleeCd -= dt; sh.gt -= dt; sh.followT -= dt;
      if (sh.followT <= 0) sh.follow = null;
      if (sh.follow) { sh.goal.copy(sh.follow.p).addScaledVector(sh.follow.dir, -12); sh.goal.y -= 3; }
      else if (sh.gt <= 0) {
        sh.gt = G.rr(6, 12);
        sh.goal.set(sh.home.x + G.rr(-14, 14), 0, sh.home.z + G.rr(-14, 14));
        sh.goal.y = Math.min(H(sh.goal.x, sh.goal.z) + G.rr(2, 7), -4);
      }
      sh.threat = null; let best = 16;
      for (const p of E.preds) { const d = p.p.distanceTo(sh.c); if (d < best) { best = d; sh.threat = p.p; } }
      if (sh.threat) {
        if (sh.alarm <= 0 && sh.fleeCd <= 0) { sh.fleeCd = 30; E.emit('fish', 'flee', sh.c, () => `${cap(G.nm('fish'))}: стая уходит в кораллы — рядом хищник`); }
        sh.alarm = 3;
      } else if (S.center.distanceTo(sh.c) < 11 && Math.abs(S.speed) > 1.2) { sh.threat = S.center; sh.alarm = Math.max(sh.alarm, 1.5); }
    }

    const im = E.fishIM;
    for (const f of E.fish) {
      if (!f.alive) {
        f.t -= dt;
        if (f.t > 0) { dummy.position.set(0, -999, 0); dummy.scale.setScalar(0.001); dummy.updateMatrix(); im.setMatrixAt(f.i, dummy.matrix); continue; }
        f.alive = true; f.p.copy(f.s.home).add(tv.set(G.rr(-2, 2), G.rr(0, 2), G.rr(-2, 2))); f.v.set(0, 0.2, 0);
      }
      const sh = f.s, acc = tv.set(0, 0, 0);
      acc.addScaledVector(tv2.copy(sh.c).sub(f.p), 0.5);
      acc.addScaledVector(tv2.copy(sh.v).sub(f.v), 0.6);
      const goal = sh.alarm > 0 && sh.threat !== S.center ? sh.home : sh.goal;
      acc.addScaledVector(tv2.copy(goal).sub(f.p).clampLength(0, 8), sh.alarm > 0 ? 1.2 : 0.35);
      for (const o of sh.members) {
        if (o === f || !o.alive) continue;
        const dx = f.p.x - o.p.x, dy = f.p.y - o.p.y, dz = f.p.z - o.p.z, d2 = dx * dx + dy * dy + dz * dz;
        if (d2 < 0.6 && d2 > 1e-5) { const k = 1.8 / (d2 + 0.05); acc.x += dx * k; acc.y += dy * k; acc.z += dz * k; }
      }
      if (sh.threat) { tv2.copy(f.p).sub(sh.threat); const d = tv2.length(); if (d < 14 && d > 0.01) acc.addScaledVector(tv2.normalize(), 10 * (1 - d / 14) + 2); }
      for (const vl of E.veils) {
        const dx = f.p.x - vl.p.x, dz = f.p.z - vl.p.z, dh = Math.hypot(dx, dz) + 0.01;
        if (f.p.y < vl.p.y + 2 && f.p.y > vl.p.y - vl.len && dh < vl.r + 3) {
          acc.x += dx / dh * 2.2; acc.z += dz / dh * 2.2;
          if (dh < vl.r * 0.85 && f.p.y < vl.p.y - 0.5 && G.rng() < dt * 0.22) {
            killFish(f); E.emit('veil', 'sting', f.p, () => `${cap(G.nm('veil'))} ужалила ${G.nm('fish', 'acc')}`); break;
          }
        }
      }
      if (!f.alive) continue;
      acc.x += Math.sin(t * 1.7 + f.ph) * 0.6; acc.y += Math.sin(t * 1.3 + f.ph * 2) * 0.3; acc.z += Math.cos(t * 1.5 + f.ph) * 0.6;
      const h = H(f.p.x, f.p.z);
      if (f.p.y < h + 1.2) acc.y += (h + 1.2 - f.p.y) * 6;
      if (f.p.y > -3) acc.y -= 4;
      f.v.addScaledVector(acc, dt);
      const hide = sh.alarm > 0 && f.p.distanceTo(sh.home) < 4;
      const maxS = hide ? 1.0 : (sh.alarm > 0 ? 6.5 : 2.4);
      const sp = f.v.length();
      if (sp > maxS) f.v.multiplyScalar(maxS / sp); else if (sp < 0.5) f.v.multiplyScalar(0.5 / (sp + 1e-3));
      f.p.addScaledVector(f.v, dt);
      if (f.p.y < h + 0.4) f.p.y = h + 0.4;
      clampArea(f.p);
      dummy.position.copy(f.p); dummy.lookAt(tv2.copy(f.p).add(f.v)); dummy.rotateY(Math.sin(t * 12 + f.ph) * 0.25);
      dummy.scale.setScalar(1); dummy.updateMatrix(); im.setMatrixAt(f.i, dummy.matrix);
    }
    im.instanceMatrix.needsUpdate = true;
  }

  /* ======================= СЕРПОХВОСТЫ ======================= */
  const PSEG = 11;
  function initPreds(scene) {
    const N = 3;
    E.predSegIM = new THREE.InstancedMesh(new THREE.SphereGeometry(1, 10, 8), new THREE.MeshLambertMaterial({ color: 0x31445e, emissive: 0x070b12 }), N * (PSEG - 1));
    E.predDotIM = new THREE.InstancedMesh(new THREE.SphereGeometry(0.07, 6, 4), new THREE.MeshBasicMaterial({ color: 0xff9a3c }), N * (PSEG - 1) * 2);
    [E.predSegIM, E.predDotIM].forEach(m => { m.frustumCulled = false; scene.add(m); });
    const starts = [[-60, 20], [70, -20], [10, -110]];
    for (let i = 0; i < N; i++) {
      const p = new V3(starts[i][0], 0, starts[i][1]); p.y = Math.min(H(p.x, p.z) + 8, -6);
      const head = new THREE.Group();
      const skull = new THREE.Mesh(new THREE.SphereGeometry(1, 12, 8), new THREE.MeshLambertMaterial({ color: 0x3a5272 })); skull.scale.set(0.55, 0.5, 1.3); head.add(skull);
      const jaw = new THREE.Mesh(new THREE.ConeGeometry(0.35, 1.1, 8), new THREE.MeshLambertMaterial({ color: 0x1d2838 })); jaw.rotation.x = Math.PI / 2; jaw.position.set(0, -0.22, 0.95); head.add(jaw);
      const eyeM = new THREE.MeshBasicMaterial({ color: 0xff3b4e });
      [-1, 1].forEach(s => { const e = new THREE.Mesh(new THREE.SphereGeometry(0.1, 8, 6), eyeM); e.position.set(0.33 * s, 0.16, 0.72); head.add(e); });
      const tailG = new THREE.Group();
      const tail = new THREE.Mesh(new THREE.TorusGeometry(1.1, 0.08, 5, 18, Math.PI * 0.95), new THREE.MeshLambertMaterial({ color: 0x5a7394, emissive: 0x140c1c }));
      tail.rotation.set(0, Math.PI / 2, -Math.PI / 2); tail.position.z = -0.9; tailG.add(tail);
      const leechM = new THREE.Mesh(leechGeo(), new THREE.MeshLambertMaterial({ color: 0x8a2f3c, emissive: 0x3a0a12 }));
      leechM.position.set(0.5, -0.05, 0.25); leechM.rotation.set(0, 0.4, -0.5); head.add(leechM);
      scene.add(head); scene.add(tailG);
      const pr = { p, v: new V3(0, 0, 1), state: 'patrol', hunger: G.rr(0.1, 0.5), timer: 0, target: null, goal: p.clone(), segs: [], head, tailG, cd: 20, avoidCd: 0, i,
        leech: false, leechCd: 0, angryCd: 0, truceCd: 0, station: null, leechM, leechP: new V3() };
      setLeech(pr, i === 0); pr.leechCd = G.rr(90, 160);
      for (let k = 0; k < PSEG; k++) pr.segs.push(p.clone().add(new V3(0, 0, -k * 0.6)));
      E.preds.push(pr);
    }
  }
  function leechGeo() { const g = new THREE.CapsuleGeometry(0.09, 0.32, 3, 6); g.rotateX(Math.PI / 2); g.scale(1.4, 0.45, 1); return g; }
  function setLeech(pr, on) { pr.leech = on; pr.leechM.visible = on; if (!on) pr.station = null; }
  function startCharge(pr) { pr.state = 'charge'; pr.timer = 10; E.emit('pred', 'ship', pr.p, () => `${cap(G.nm('pred'))} разворачивается к кораблю — его привлёк свет`); }
  function predUpdate(pr, dt) {
    const S = G.Ship;
    pr.hunger = Math.min(1, pr.hunger + dt * (pr.state === 'clean' ? 0.01 : pr.leech ? 0.05 : 0.03)); pr.timer -= dt; pr.cd -= dt; pr.avoidCd -= dt; pr.truceCd -= dt; pr.angryCd -= dt;
    const dShip = pr.p.distanceTo(S.center), atStation = nearStation(pr.p);
    const reach = pr.leech ? 48 : 32; // заражённый пиявником замечает корабль издалека
    const lured = dShip < reach && (S.light || G.Game.t - E.lastPing < 12) && !E.shipHidden && S.hull > 0;
    const canAttack = pr.cd <= 0 && pr.hunger > (pr.leech ? 0.1 : 0.2) && lured && !E.shipSheltered && !atStation;
    if (pr.leech && canAttack && dShip > 32 && pr.angryCd <= 0) { pr.angryCd = 60; E.emit('leech', 'angry', pr.p, () => `Заражённый ${G.nm('pred')} злее обычного и замечает корабль издалека`); }
    if (lured && E.shipSheltered && pr.cd <= 0 && pr.truceCd <= 0 && (pr.state === 'patrol' || pr.state === 'investigate')) {
      pr.truceCd = 40; pr.cd = Math.max(pr.cd, 8);
      E.emit('ognevka', 'shelter', pr.p, () => `${cap(G.nm('pred'))} не трогает корабль у станции чистки`);
    }
    /* заражение пиявником и поиск станции чистки */
    if (!pr.leech) {
      pr.leechCd -= dt;
      if (pr.leechCd <= 0 && !atStation) { setLeech(pr, true); E.emit('leech', 'infest', pr.p, () => `${cap(G.nm('leech'))} присосался к жабрам ${G.nm('pred', 'acc')}`); }
    } else if (!pr.station && pr.hunger < 0.45 && (pr.state === 'patrol' || pr.state === 'eat') && E.stations.length) {
      pr.station = nearestStation(pr.p); pr.state = 'clean'; pr.timer = 60; pr.goal.copy(pr.station.top);
    }
    let speed = 3;
    switch (pr.state) {
      case 'patrol':
        if (pr.timer <= 0 || pr.p.distanceTo(pr.goal) < 4) {
          pr.timer = G.rr(10, 20);
          if (G.rng() < 0.6 && E.shoals.length) { const h = G.pick(E.shoals).home; pr.goal.set(h.x + G.rr(-25, 25), 0, h.z + G.rr(-25, 25)); }
          else pr.goal.set(G.rr(-170, 150), 0, G.rr(-170, 150));
          pr.goal.y = Math.min(H(pr.goal.x, pr.goal.z) + G.rr(4, 14), -6);
        }
        if (pr.hunger > 0.45 && !atStation) { const f = nearestFish(pr.p, 50); if (f) { pr.state = 'hunt'; pr.target = f; pr.timer = 12; } }
        if (canAttack) startCharge(pr);
        break;
      case 'clean': {
        const st = pr.station;
        if (!st || !pr.leech) { pr.state = 'patrol'; pr.timer = 0; break; }
        const d = pr.p.distanceTo(st.top);
        speed = d < 4 ? 0.6 : 4;
        pr.goal.copy(st.top);
        if (d < 4) { st.busy = pr; st.work += dt; }
        if (st.work >= 6) {
          st.work = 0; st.busy = null; setLeech(pr, false); pr.leechCd = G.rr(150, 260); pr.state = 'patrol'; pr.timer = G.rr(4, 8); pr.hunger = Math.min(pr.hunger, 0.3);
          E.emit('ognevka', 'clean', st.top, () => `${cap(G.nm('pred'))} на станции чистки: его очистили от ${G.nm('leech', 'acc')}`);
          E.emit('leech', 'cleaned', st.top, null);
        } else if (pr.timer <= 0 || pr.hunger > 0.9) { pr.state = 'patrol'; pr.station = null; pr.timer = 0; }
        if (canAttack && pr.hunger > 0.6) startCharge(pr);
        break;
      }
      case 'hunt':
        speed = 7.6;
        if (!pr.target || !pr.target.alive || pr.timer <= 0) { pr.state = 'patrol'; pr.timer = 0; pr.target = null; break; }
        if (atStation || nearStation(pr.target.p)) { // у станции чистки перемирие
          pr.state = 'patrol'; pr.timer = G.rr(4, 8); pr.target = null;
          if (pr.truceCd <= 0) { pr.truceCd = 40; E.emit('ognevka', 'truce', pr.p, () => `${cap(G.nm('pred'))} бросил охоту у станции чистки`); }
          break;
        }
        pr.goal.copy(pr.target.v).multiplyScalar(0.4).add(pr.target.p);
        if (pr.p.distanceTo(pr.target.p) < 1.6) {
          killFish(pr.target); pr.hunger = 0; pr.state = 'eat'; pr.timer = 5; pr.target = null;
          E.emit('pred', 'catch', pr.p, () => `${cap(G.nm('pred'))} поймал ${G.nm('fish', 'acc')}`);
        } else if (canAttack && pr.hunger > 0.6) startCharge(pr);
        break;
      case 'eat': speed = 1; if (pr.timer <= 0) { pr.state = 'patrol'; pr.timer = 0; } break;
      case 'investigate':
        speed = 5.5;
        if (pr.p.distanceTo(pr.goal) < 10 || pr.timer <= 0) { pr.state = 'patrol'; pr.timer = 0; }
        if (canAttack) startCharge(pr);
        break;
      case 'charge':
        speed = 9.5; pr.goal.copy(S.center);
        if (E.shipSheltered) {
          pr.state = 'retreat'; pr.timer = 8; pr.cd = Math.max(pr.cd, 15); pr.goal.copy(pr.p).add(tv.set(G.rr(-40, 40), 4, G.rr(-40, 40))); pr.goal.y = Math.min(pr.goal.y, -6);
          if (pr.truceCd <= 0) { pr.truceCd = 40; E.emit('ognevka', 'shelter', pr.p, () => `${cap(G.nm('pred'))} отвернул: корабль у станции чистки`); }
          break;
        }
        if (E.shipHidden) {
          pr.state = 'retreat'; pr.timer = 10; pr.goal.copy(pr.p).add(tv.set(G.rr(-40, 40), 5, G.rr(-40, 40))); pr.goal.y = Math.min(pr.goal.y, -6);
          E.emit('veil', 'hide', pr.p, () => `Корабль под куполом: ${G.nm('pred')} потерял его из виду`);
          break;
        }
        if (dShip < 8.5) {
          G.Game.hullHit(12, 'bite');
          pr.state = 'retreat'; pr.timer = 14; pr.cd = 25;
          tv.copy(pr.p).sub(S.center).setY(0); if (tv.lengthSq() < 0.01) tv.set(1, 0, 0);
          pr.goal.copy(pr.p).add(tv.normalize().multiplyScalar(60)); pr.goal.y = Math.min(H(pr.goal.x, pr.goal.z) + 8, -6);
        } else if (pr.timer <= 0) { pr.state = 'patrol'; pr.cd = 10; }
        break;
      case 'retreat': case 'flee':
        speed = pr.state === 'flee' ? 9 : 6;
        if (pr.timer <= 0) { pr.state = 'patrol'; pr.timer = 0; }
        break;
    }
    const steer = tv2.copy(pr.goal).sub(pr.p);
    if (pr.leech && pr.state !== 'clean') speed *= 0.75; // пиявник замедляет хищника
    if (steer.lengthSq() > 1e-4) steer.normalize().multiplyScalar(speed);
    for (const vl of E.veils) {
      const dx = pr.p.x - vl.p.x, dz = pr.p.z - vl.p.z, dh = Math.hypot(dx, dz) + 0.01;
      if (dh < vl.r + 9 && pr.p.y < vl.p.y + 4 && pr.p.y > vl.p.y - vl.len - 2) {
        steer.x += dx / dh * 8; steer.z += dz / dh * 8;
        if (pr.avoidCd <= 0) { pr.avoidCd = 40; E.emit('pred', 'avoid', pr.p, () => `${cap(G.nm('pred'))} обходит ${G.nm('veil', 'acc')} стороной`); }
      }
    }
    pr.v.lerp(steer, Math.min(1, dt * (pr.state === 'hunt' || pr.state === 'charge' ? 1.6 : 1.1)));
    pr.p.addScaledVector(pr.v, dt);
    const h = H(pr.p.x, pr.p.z);
    if (pr.p.y < h + 1.8) { pr.p.y = h + 1.8; if (pr.v.y < 0) pr.v.y *= -0.3; }
    if (pr.p.y > -4) pr.p.y = -4;
    clampArea(pr.p);
  }
  function predRender(pr, idx, t) {
    const segs = pr.segs; segs[0].copy(pr.p);
    for (let k = 1; k < segs.length; k++) { tv.copy(segs[k]).sub(segs[k - 1]); const L = tv.length(); if (L > 0.62) segs[k].copy(segs[k - 1]).addScaledVector(tv, 0.62 / L); }
    pr.head.position.copy(pr.p);
    pr.head.lookAt(tv.copy(pr.p).add(pr.v.lengthSq() > 0.01 ? tv3.copy(pr.v) : tv3.set(0, 0, 1)));
    const freq = 3 + pr.v.length() * 0.6;
    const base = idx * (PSEG - 1);
    let lastPos = null;
    for (let k = 1; k < segs.length; k++) {
      tv.copy(segs[k - 1]).sub(segs[k]); if (tv.lengthSq() < 1e-6) tv.set(0, 0, 1); tv.normalize();
      tv2.set(tv.z, 0, -tv.x); if (tv2.lengthSq() < 1e-6) tv2.set(1, 0, 0); tv2.normalize();
      const w = Math.sin(t * freq - k * 0.75) * 0.08 * k;
      dummy.position.copy(segs[k]).addScaledVector(tv2, w);
      dummy.lookAt(tv3.copy(dummy.position).add(tv));
      const r = 0.55 * (1 - (k - 1) / segs.length * 0.8);
      dummy.scale.set(r * 0.9, r, r * 1.5); dummy.updateMatrix();
      E.predSegIM.setMatrixAt(base + k - 1, dummy.matrix);
      [-1, 1].forEach((s, j) => {
        tv3.set(0.95 * s, 0.25, 0).applyMatrix4(dummy.matrix);
        const m = new THREE.Matrix4().makeTranslation(tv3.x, tv3.y, tv3.z);
        E.predDotIM.setMatrixAt((base + k - 1) * 2 + j, m);
      });
      lastPos = dummy.position;
    }
    pr.head.updateMatrixWorld(); pr.leechM.getWorldPosition(pr.leechP);
    pr.tailG.position.copy(lastPos);
    pr.tailG.lookAt(segs[segs.length - 2]);
  }

  /* ======================= ФОНАРНЫЕ ВУАЛИ ======================= */
  function initVeils(scene) {
    [[-28, 42], [85, 25]].forEach(([x, z], i) => {
      const top = Math.min(H(x, z) + 21, -9);
      const vl = { p: new V3(x, top, z), home: new V3(x, top, z), r: 6, len: 15, ph: i * 2.1, group: new THREE.Group(), scanP: new V3() };
      const bell = new THREE.Mesh(new THREE.SphereGeometry(vl.r, 28, 12, 0, Math.PI * 2, 0, Math.PI / 2), new THREE.MeshBasicMaterial({ color: 0x7ff5ff, transparent: true, opacity: 0.22, side: THREE.DoubleSide, depthWrite: false }));
      const inner = new THREE.Mesh(new THREE.SphereGeometry(vl.r * 0.6, 20, 10, 0, Math.PI * 2, 0, Math.PI / 2), new THREE.MeshBasicMaterial({ color: 0xff8ae0, transparent: true, opacity: 0.28, side: THREE.DoubleSide, depthWrite: false }));
      vl.group.add(bell, inner); vl.bell = bell; vl.inner = inner;
      const rp = []; for (let k = 0; k < 48; k++) { const a = k / 48 * 6.283; rp.push(Math.cos(a) * vl.r, 0, Math.sin(a) * vl.r); }
      const rg = new THREE.BufferGeometry(); rg.setAttribute('position', new THREE.Float32BufferAttribute(rp, 3));
      vl.rim = new THREE.Points(rg, new THREE.PointsMaterial({ color: 0xcffcff, size: 0.5, transparent: true, opacity: 0.9 }));
      vl.group.add(vl.rim);
      const smallM = new THREE.MeshBasicMaterial({ color: 0xbff8ff, transparent: true, opacity: 0.35, depthWrite: false, side: THREE.DoubleSide });
      vl.small = [];
      for (let k = 0; k < 9; k++) { const s = new THREE.Mesh(new THREE.SphereGeometry(G.rr(0.5, 1), 12, 6, 0, Math.PI * 2, 0, Math.PI / 2), smallM); const a = k / 9 * 6.283; s.position.set(Math.cos(a) * (vl.r + 1.2), G.rr(-3, -1), Math.sin(a) * (vl.r + 1.2)); s.userData.ph = G.rr(0, 6); vl.group.add(s); vl.small.push(s); }
      const T = 40, SG = 10, arr = new Float32Array(T * SG * 2 * 3);
      const tg = new THREE.BufferGeometry(); tg.setAttribute('position', new THREE.BufferAttribute(arr, 3));
      vl.lines = new THREE.LineSegments(tg, new THREE.LineBasicMaterial({ color: 0xff9ae6, transparent: true, opacity: 0.6 }));
      vl.lines.frustumCulled = false; vl.group.add(vl.lines);
      vl.tent = { arr, tg, T, SG, base: Array.from({ length: T }, (_, k) => ({ a: k / T * 6.283 + G.rr(-0.05, 0.05), rr: vl.r * G.rr(0.25, 0.9), len: vl.len * G.rr(0.6, 1) })) };
      scene.add(vl.group); E.veils.push(vl);
    });
  }
  function veilUpdate(vl, dt, t) {
    vl.p.x += Math.sin(t * 0.031 + vl.ph) * 0.6 * dt + (vl.home.x - vl.p.x) * 0.004 * dt;
    vl.p.z += Math.cos(t * 0.027 + vl.ph * 1.7) * 0.6 * dt + (vl.home.z - vl.p.z) * 0.004 * dt;
    const floor = H(vl.p.x, vl.p.z) + vl.len + 3;
    vl.p.y = Math.min(Math.max(vl.home.y, floor), -7) + Math.sin(t * 0.3 + vl.ph) * 1.2;
    vl.group.position.copy(vl.p); vl.scanP.copy(vl.p).y -= 3;
    const s = 1 + Math.sin(t * 1.3 + vl.ph) * 0.07, s2 = 1 + Math.sin(t * 1.3 + vl.ph - 0.6) * 0.09;
    vl.bell.scale.set(s, 1 / s, s); vl.inner.scale.set(s2, 1 / s2, s2); vl.rim.scale.set(s, 1, s);
    vl.rim.material.opacity = 0.6 + 0.4 * Math.sin(t * 2.6 + vl.ph);
    vl.small.forEach(m => { const q = 1 + Math.sin(t * 2 + m.userData.ph) * 0.15; m.scale.set(q, 1 / q, q); });
    const { arr, T, SG, base } = vl.tent;
    let o = 0;
    for (let k = 0; k < T; k++) {
      const b = base[k], x0 = Math.cos(b.a) * b.rr * s, z0 = Math.sin(b.a) * b.rr * s, step = b.len / SG;
      let px = x0, py = -0.1, pz = z0;
      for (let j = 0; j < SG; j++) {
        const nx = x0 + Math.sin(t * 0.9 + j * 0.45 + k) * (j + 1) * 0.12;
        const nz = z0 + Math.cos(t * 0.7 + j * 0.4 + k * 1.3) * (j + 1) * 0.12;
        const ny = -(j + 1) * step;
        arr[o++] = px; arr[o++] = py; arr[o++] = pz; arr[o++] = nx; arr[o++] = ny; arr[o++] = nz;
        px = nx; py = ny; pz = nz;
      }
    }
    vl.tent.tg.attributes.position.needsUpdate = true;
  }

  /* ======================= СКРЕБУНЫ ======================= */
  function makeGrazer() {
    const g = new THREE.Group();
    const shell = new THREE.Mesh(new THREE.SphereGeometry(0.8, 14, 8, 0, Math.PI * 2, 0, Math.PI / 2), new THREE.MeshLambertMaterial({ color: 0x7b6a8c, emissive: 0x120a18 }));
    shell.scale.set(1, 0.6, 1.3); g.add(shell);
    const dp = [];
    for (let k = 0; k < 10; k++) {
      const a = G.rr(0, 6.28), b = G.rr(0.25, 1.25);
      dp.push({ g: new THREE.SphereGeometry(0.07, 6, 4), m: new THREE.Matrix4().makeTranslation(Math.cos(a) * Math.sin(b) * 0.8, Math.cos(b) * 0.48, Math.sin(a) * Math.sin(b) * 1.04) });
    }
    g.add(new THREE.Mesh(G.merge(dp), new THREE.MeshBasicMaterial({ color: 0xffb259 })));
    const lp = [];
    [-1, 1].forEach(s => { for (let k = 0; k < 3; k++) { const geo = new THREE.CylinderGeometry(0.035, 0.03, 0.6, 4); geo.translate(0, -0.3, 0); lp.push({ g: geo, m: new THREE.Matrix4().makeTranslation(s * 0.6, 0.15, (k - 1) * 0.45).multiply(new THREE.Matrix4().makeRotationZ(s * 0.9)) }); } });
    const legs = new THREE.Mesh(G.merge(lp), new THREE.MeshLambertMaterial({ color: 0x3a2f45 }));
    g.add(legs); g.userData.legs = legs; g.userData.shell = shell;
    return g;
  }
  function pickPatch(gz) {
    let best = null, bs = -1;
    for (const pt of G.World.patches) {
      if (pt === gz.patch) continue;
      const s = pt.food / (1 + Math.hypot(pt.x - gz.p.x, pt.z - gz.p.z) / 25) * G.rr(0.7, 1.3);
      if (s > bs) { bs = s; best = pt; }
    }
    return best;
  }
  function initGrazers(scene) {
    const P = G.World.patches; if (!P.length) return;
    for (let i = 0; i < 10; i++) {
      const pt = P[(i * 2) % P.length];
      const gz = { p: new V3(pt.x + G.rr(-3, 3), pt.y, pt.z + G.rr(-3, 3)), dir: G.rr(0, 6.28), state: 'walk', patch: null, timer: 0, alive: true, t: 0, gcd: G.rr(0, 20), mesh: makeGrazer() };
      gz.patch = pickPatch(gz); scene.add(gz.mesh); E.grazers.push(gz);
    }
  }
  function grazerUpdate(gz, dt, t) {
    if (!gz.alive) {
      gz.t -= dt;
      if (gz.t <= 0) { const pt = G.pick(G.World.patches); gz.p.set(pt.x + G.rr(-3, 3), 0, pt.z + G.rr(-3, 3)); gz.patch = pickPatch(gz); gz.state = 'walk'; gz.alive = true; gz.mesh.visible = true; }
      else return;
    }
    gz.gcd -= dt;
    const legs = gz.mesh.userData.legs;
    if (gz.state === 'walk') {
      const tp = gz.patch; if (!tp) { gz.patch = pickPatch(gz); return; }
      const dx = tp.x - gz.p.x, dz = tp.z - gz.p.z, d = Math.hypot(dx, dz);
      if (d < 1.6) {
        gz.state = 'graze'; gz.timer = G.rr(8, 14);
        if (gz.gcd <= 0) { gz.gcd = 45; E.emit('grazer', 'graze', gz.p, () => `${cap(G.nm('grazer'))} объедает светящийся мох`); }
      } else {
        let da = Math.atan2(dx, dz) - gz.dir; da = Math.atan2(Math.sin(da), Math.cos(da));
        gz.dir += da * Math.min(1, dt * 1.5);
        gz.p.x += Math.sin(gz.dir) * 0.9 * dt; gz.p.z += Math.cos(gz.dir) * 0.9 * dt;
      }
      legs.rotation.x = Math.sin(t * 7 + gz.dir) * 0.18;
    } else {
      gz.patch.food = Math.max(0, gz.patch.food - dt * 0.05); gz.timer -= dt;
      gz.mesh.userData.shell.rotation.x = Math.sin(t * 3) * 0.05;
      if (gz.timer <= 0 || gz.patch.food < 0.08) { gz.patch = pickPatch(gz); gz.state = 'walk'; }
    }
    gz.p.y = H(gz.p.x, gz.p.z) + 0.1;
    gz.mesh.position.copy(gz.p); gz.mesh.rotation.y = gz.dir;
  }

  /* ======================= СТЕКЛОУСТЫ ======================= */
  const ESEG = 14;
  function initEels(scene) {
    const L = G.World.lairs, N = L.length;
    E.eelIM = new THREE.InstancedMesh(new THREE.SphereGeometry(1, 10, 8), new THREE.MeshLambertMaterial({ color: 0xc8f8ff, emissive: 0x2a6f7d, transparent: true, opacity: 0.4, depthWrite: false }), Math.max(1, N * (ESEG - 1)));
    E.eelDotIM = new THREE.InstancedMesh(new THREE.SphereGeometry(0.06, 6, 4), new THREE.MeshBasicMaterial({ color: 0xfff0a0 }), Math.max(1, N * (ESEG - 1)));
    [E.eelIM, E.eelDotIM].forEach(m => { m.frustumCulled = false; scene.add(m); });
    L.forEach((lr, i) => {
      const head = new THREE.Group();
      const hs = new THREE.Mesh(new THREE.SphereGeometry(0.3, 12, 8), new THREE.MeshLambertMaterial({ color: 0xd8fbff, emissive: 0x2a6f7d, transparent: true, opacity: 0.55, depthWrite: false }));
      hs.scale.set(1, 0.8, 1.6); head.add(hs);
      const eye = new THREE.Mesh(new THREE.SphereGeometry(0.09, 8, 6), new THREE.MeshBasicMaterial({ color: 0xff3b4e })); eye.position.set(0, 0.13, 0.3); head.add(eye);
      const mouth = new THREE.Mesh(new THREE.TorusGeometry(0.17, 0.03, 6, 16), new THREE.MeshBasicMaterial({ color: 0xffe2b8 })); mouth.position.z = 0.48; head.add(mouth);
      scene.add(head);
      const el = { lair: lr.mouth.clone(), out: lr.out.clone(), state: 'hide', p: lr.mouth.clone(), target: null, kind: null, timer: 0, cd: G.rr(10, 25), segs: [], head, i };
      for (let k = 0; k < ESEG; k++) el.segs.push(lr.mouth.clone().addScaledVector(lr.out, -k * 0.45));
      E.eels.push(el);
    });
  }
  function eelUpdate(el, dt, t) {
    const S = G.Ship;
    el.timer -= dt; el.cd -= dt;
    let speed = 0, goal = null;
    switch (el.state) {
      case 'hide':
        el.p.copy(el.lair).addScaledVector(el.out, 0.3 + Math.sin(t * 0.8 + el.i) * 0.15);
        if (el.cd <= 0) {
          let prey = null, kind = null;
          for (const gz of E.grazers) if (gz.alive && gz.p.distanceTo(el.lair) < 9) { prey = gz; kind = 'grazer'; break; }
          if (!prey) for (const f of E.fish) if (f.alive && f.p.distanceTo(el.lair) < 6) { prey = f; kind = 'fish'; break; }
          if (prey && G.rng() < dt * 0.6) { el.state = 'strike'; el.target = prey; el.kind = kind; el.timer = 3; }
          else if (!E.latched && S.hull > 0 && S.front.distanceTo(el.lair) < 28 && G.rng() < dt * 0.5) { el.state = 'toShip'; el.timer = 6; }
        }
        break;
      case 'strike':
        speed = 11; goal = el.target.p;
        if (!el.target.alive || el.timer <= 0) { el.state = 'return'; el.cd = 8; break; }
        if (el.p.distanceTo(goal) < 1.3) {
          if (el.kind === 'grazer') {
            el.target.alive = false; el.target.t = G.rr(40, 60); el.target.mesh.visible = false;
            E.emit('eel', 'eelcatch', el.p, () => `${cap(G.nm('eel'))} утащил ${G.nm('grazer', 'acc')} в расщелину`);
          } else {
            killFish(el.target);
            E.emit('eel', 'eelcatch', el.p, () => `${cap(G.nm('eel'))} схватил ${G.nm('fish', 'acc')}`);
          }
          el.state = 'return'; el.cd = 25;
        }
        break;
      case 'return': case 'flee':
        speed = el.state === 'flee' ? 12 : 6; goal = el.lair;
        if (el.p.distanceTo(el.lair) < 0.8) el.state = 'hide';
        break;
      case 'toShip':
        speed = 10; goal = S.front;
        if (E.latched || el.timer <= 0) { el.state = 'return'; el.cd = 15; break; }
        if (el.p.distanceTo(S.front) < 2) {
          el.state = 'latched'; E.latched = el;
          E.emit('eel', 'latch', el.p, () => `${cap(G.nm('eel'))} присосался к корпусу!`);
          G.Game.onLatch();
        }
        break;
    }
    if (goal) { tv.copy(goal).sub(el.p); const d = tv.length(); if (d > 0.01) el.p.addScaledVector(tv, Math.min(1, speed * dt / d)); }
    const segs = el.segs;
    if (el.state === 'latched') {
      for (let k = 0; k < ESEG; k++) S.group.localToWorld(segs[k].set(-3.4 + k * 0.5, 1.8 + Math.sin(k * 0.8 + t * 2.2) * 0.45, -6.36));
      el.p.copy(segs[0]);
    } else if (el.state === 'hide') {
      for (let k = 0; k < ESEG; k++) segs[k].copy(el.p).addScaledVector(el.out, -(k + 0.5) * 0.45);
    } else {
      segs[0].copy(el.p);
      for (let k = 1; k < ESEG; k++) { tv.copy(segs[k]).sub(segs[k - 1]); const L = tv.length(); if (L > 0.42) segs[k].copy(segs[k - 1]).addScaledVector(tv, 0.42 / L); }
    }
  }
  function eelRender(el, idx, t) {
    const segs = el.segs, base = idx * (ESEG - 1), moving = el.state !== 'hide' && el.state !== 'latched';
    for (let k = 1; k < ESEG; k++) {
      tv.copy(segs[k - 1]).sub(segs[k]); if (tv.lengthSq() < 1e-6) tv.set(0, 0, 1); tv.normalize();
      tv2.set(tv.z, 0, -tv.x); if (tv2.lengthSq() < 1e-6) tv2.set(1, 0, 0); tv2.normalize();
      const w = moving ? Math.sin(t * 10 - k * 0.9) * 0.05 * k : 0;
      dummy.position.copy(segs[k]).addScaledVector(tv2, w);
      dummy.lookAt(tv3.copy(dummy.position).add(tv));
      const r = 0.28 * (1 - k / ESEG * 0.7);
      dummy.scale.set(r, r * 0.85, r * 2.6); dummy.updateMatrix();
      E.eelIM.setMatrixAt(base + k - 1, dummy.matrix);
      E.eelDotIM.setMatrixAt(base + k - 1, new THREE.Matrix4().makeTranslation(dummy.position.x, dummy.position.y, dummy.position.z));
    }
    el.head.position.copy(segs[0]);
    el.head.lookAt(tv.copy(segs[0]).multiplyScalar(2).sub(segs[1]));
  }

  /* ======================= ЛЕДОКРЫЛЫ ======================= */
  function rayGeo() {
    const g = new THREE.PlaneGeometry(14, 9, 18, 8); g.rotateX(-Math.PI / 2);
    const p = g.attributes.position, col = new Float32Array(p.count * 3);
    for (let i = 0; i < p.count; i++) {
      const x = p.getX(i), z = p.getZ(i);
      const f = Math.max(0.05, 1 - Math.pow(Math.abs(z + 0.6) / 5.1, 1.6));
      const nx = x * f; p.setX(i, nx);
      p.setY(i, Math.max(0, 1 - Math.abs(nx) / 2.2) * 0.5 * Math.max(0, 1 - Math.abs(z) / 4.5));
      const edge = Math.abs(x) / 7, c = 0.4 + edge * 0.6;
      col[i * 3] = c * 0.75; col[i * 3 + 1] = c * 1.0; col[i * 3 + 2] = c * 1.15;
    }
    g.setAttribute('color', new THREE.BufferAttribute(col, 3));
    g.computeVertexNormals();
    return g;
  }
  function initRays(scene) {
    [{ c: new V3(0, -9, 10), R: 85, sp: 4.2 }, { c: new V3(-140, -11, -90), R: 55, sp: 3.6 }].forEach((o, i) => {
      const geo = rayGeo();
      const mesh = new THREE.Mesh(geo, new THREE.MeshLambertMaterial({ color: 0xa8cfe0, emissive: 0x123848, side: THREE.DoubleSide, vertexColors: true }));
      const tail = new THREE.Mesh(new THREE.CylinderGeometry(0.03, 0.12, 9, 5), mesh.material); tail.rotation.x = Math.PI / 2; tail.position.z = -8.5; mesh.add(tail);
      const spots = []; for (let k = 0; k < 14; k++) { const s = k % 2 ? 1 : -1; spots.push({ g: new THREE.SphereGeometry(0.12, 6, 4), m: new THREE.Matrix4().makeTranslation(s * (0.8 + (k >> 1) * 0.35), 0.45, 1 - (k >> 1) * 0.4) }); }
      mesh.add(new THREE.Mesh(G.merge(spots), new THREE.MeshBasicMaterial({ color: 0x9ffcff })));
      mesh.frustumCulled = false; scene.add(mesh);
      E.rays.push({ c: o.c, R: o.R, sp: o.sp, ang: G.rr(0, 6.28), p: new V3(), dir: new V3(), mesh, geo, base: Float32Array.from(geo.attributes.position.array), fcd: G.rr(5, 15), i, init: false, nT: 0 });
    });
  }
  function rayUpdate(ry, dt, t) {
    ry.ang += ry.sp / ry.R * dt;
    const x = ry.c.x + Math.cos(ry.ang) * ry.R, z = ry.c.z + Math.sin(ry.ang) * ry.R;
    const ty = Math.min(-5.5, Math.max(ry.c.y + Math.sin(t * 0.2 + ry.i) * 1.5, H(x, z) + 7));
    ry.p.set(x, ry.init ? G.lerp(ry.p.y, ty, Math.min(1, dt * 0.5)) : ty, z); ry.init = true;
    ry.dir.set(-Math.sin(ry.ang), 0, Math.cos(ry.ang));
    ry.mesh.position.copy(ry.p); ry.mesh.lookAt(tv.copy(ry.p).add(ry.dir)); ry.mesh.rotateZ(-0.22);
    const pa = ry.geo.attributes.position.array, b = ry.base;
    for (let i = 0; i < pa.length; i += 3) { const ax = Math.abs(b[i]); pa[i + 1] = b[i + 1] + Math.sin(t * 1.3 + ry.i - ax * 0.32) * Math.pow(ax / 7, 1.4) * 1.8; }
    ry.geo.attributes.position.needsUpdate = true;
    if (++ry.nT % 3 === 0) ry.geo.computeVertexNormals();
    ry.fcd -= dt;
    if (ry.fcd <= 0) {
      ry.fcd = G.rr(14, 22);
      let best = null, bd = 45;
      for (const sh of E.shoals) { const d = sh.c.distanceTo(ry.p); if (d < bd && !sh.follow) { bd = d; best = sh; } }
      if (best) {
        best.follow = ry; best.followT = 18;
        E.emit('ray', 'follow', ry.p, () => `${cap(G.nm('ray'))} ведёт за собой стаю рыб`);
        E.emit('fish', 'follow', ry.p, null);
      }
    }
  }


  /* ======================= ОГНЁВКИ И СТАНЦИИ ЧИСТКИ ======================= */
  const ST_R = 14, SHELTER_R = 18, SHRIMP = 10;
  function nearStation(p) { for (const st of E.stations) if (p.distanceTo(st.top) < ST_R) return st; return null; }
  function nearestStation(p) { let best = null, bd = 1e9; for (const st of E.stations) { const d = st.top.distanceTo(p); if (d < bd) { bd = d; best = st; } } return best; }
  E.nearStation = nearStation;
  function fireCoralGeo() { // огнецвет: веер толстых ветвей с огненными кончиками
    const parts = [], wood = new THREE.Color(0.75, 0.32, 0.16), tip = new THREE.Color(1, 0.75, 0.35);
    const trunk = new THREE.CylinderGeometry(0.28, 0.45, 2.2, 7); trunk.translate(0, 1.1, 0);
    parts.push({ g: trunk, c: wood });
    for (let k = 0; k < 6; k++) {
      const a = k / 6 * 6.283 + 0.3, tilt = 0.45 + (k % 2) * 0.25, L = 1.6 + (k % 3) * 0.35;
      const br = new THREE.CylinderGeometry(0.1, 0.2, L, 5); br.translate(0, L / 2, 0);
      const m = new THREE.Matrix4().makeTranslation(0, 1.9, 0).multiply(new THREE.Matrix4().makeRotationAxis(new V3(Math.sin(a), 0, -Math.cos(a)), tilt));
      parts.push({ g: br, m, c: wood });
      const tp = new THREE.SphereGeometry(0.22, 6, 4); tp.translate(0, L, 0);
      parts.push({ g: tp, m: m.clone(), c: tip });
    }
    return G.merge(parts);
  }
  function initStations(scene) {
    const spots = G.World.coralSpots;
    if (!spots.length) return;
    const geo = fireCoralGeo(), mat = new THREE.MeshLambertMaterial({ vertexColors: true, emissive: 0x5a1a04 });
    [[-45, 35], [38, 72], [22, -42]].forEach(([x, z]) => {
      let best = null, bd = 1e9;
      for (const sp of spots) { const d = Math.hypot(sp.x - x, sp.z - z); if (d < bd && E.stations.every(st => Math.hypot(st.base.x - sp.x, st.base.z - sp.z) > 40)) { bd = d; best = sp; } }
      if (!best) return;
      const base = best.clone(); base.y = H(base.x, base.z);
      const m = new THREE.Mesh(geo, mat); m.position.copy(base); m.rotation.y = G.rr(0, 6.28); m.scale.setScalar(1.3); scene.add(m);
      E.stations.push({ i: E.stations.length, base, top: base.clone().add(new V3(0, 4.2, 0)), busy: null, work: 0, mesh: m });
    });
    const sg = new THREE.CapsuleGeometry(0.06, 0.2, 2, 5); sg.rotateX(Math.PI / 2);
    E.shrimpIM = new THREE.InstancedMesh(sg, new THREE.MeshBasicMaterial({ color: 0xffa040 }), Math.max(1, E.stations.length * SHRIMP));
    E.shrimpIM.frustumCulled = false; scene.add(E.shrimpIM);
    E.shrimp = [];
    E.stations.forEach(st => { for (let k = 0; k < SHRIMP; k++) E.shrimp.push({ st, ph: G.rr(0, 6.28), r: G.rr(0.6, 1.6), h: G.rr(-0.6, 0.8), p: st.top.clone() }); });
  }
  function stationsUpdate(dt, t) {
    const S = G.Ship;
    E.shipSheltered = null;
    for (const st of E.stations) {
      if (st.busy && (st.busy.state !== 'clean' || st.busy.p.distanceTo(st.top) > 5)) st.busy = null;
      if (!st.busy) st.work = Math.max(0, st.work - dt * 0.5);
      if (S.center.distanceTo(st.top) < SHELTER_R) E.shipSheltered = st;
    }
    if (!E.shrimpIM) return;
    E.shrimp.forEach((sh, k) => {
      const st = sh.st, a = t * 0.8 + sh.ph;
      if (st.busy) tv.copy(st.busy.p).add(tv2.set(Math.cos(a * 2) * sh.r * 0.5, 0.4 + sh.h * 0.4, Math.sin(a * 2) * sh.r * 0.5));
      else tv.copy(st.top).add(tv2.set(Math.cos(a) * sh.r, sh.h + Math.sin(t * 3 + sh.ph) * 0.15, Math.sin(a) * sh.r));
      tv3.copy(sh.p); sh.p.lerp(tv, Math.min(1, dt * 3));
      tv2.subVectors(sh.p, tv3); if (tv2.lengthSq() < 1e-8) tv2.set(Math.cos(a + 1.57), 0, Math.sin(a + 1.57));
      dummy.position.copy(sh.p); dummy.lookAt(tv3.copy(sh.p).add(tv2));
      dummy.scale.setScalar(1); dummy.updateMatrix(); E.shrimpIM.setMatrixAt(k, dummy.matrix);
    });
    E.shrimpIM.instanceMatrix.needsUpdate = true;
  }

  /* ======================= ХВАТНИ И ПЕСТРЯНКИ ======================= */
  /* Хватень — анемон-засадчик у стай светляков. В темноте раскрывает щупальца,
     пестрянки подманивают стаю, хватень ловит неосторожную рыбу, пестрянки доедают остатки.
     В луче прожектора щупальца быстро прячутся. */
  const HV_TENT = 22, HV_PESTR = 3, HV_REACH = 1.9, BEAM_R = 45, BEAM_ANG = 0.5;
  const beamO = new V3(), beamD = new V3();
  function inBeam(p, fresh) {
    const S = G.Ship;
    if (fresh) updateBeam();
    if (!S.light || S.energy <= 0.5) return false;
    tv.copy(p).sub(beamO); const d = tv.length();
    if (d > BEAM_R || d < 0.01) return false;
    return tv.dot(beamD) / d > Math.cos(BEAM_ANG);
  }
  E.inBeam = p => inBeam(p, true);
  function updateBeam() {
    const g = G.Ship.group;
    beamO.set(0, 0.8, -6.8); g.localToWorld(beamO);
    beamD.set(0, -10.8, -33.2).normalize().applyQuaternion(g.quaternion);
  }
  function tentGeo() {
    const g = new THREE.CylinderGeometry(0.015, 0.05, 1, 4, 3); g.translate(0, 0.5, 0);
    const p = g.attributes.position, col = new Float32Array(p.count * 3);
    for (let i = 0; i < p.count; i++) { const k = p.getY(i); col[i * 3] = 0.55 + k * 0.45; col[i * 3 + 1] = 0.25 + k * 0.6; col[i * 3 + 2] = 0.6 + k * 0.35; }
    g.setAttribute('color', new THREE.BufferAttribute(col, 3));
    return g;
  }
  function hvBodyGeo() {
    const col = new THREE.CylinderGeometry(0.32, 0.45, 0.8, 9); col.translate(0, 0.4, 0);
    const disc = new THREE.CylinderGeometry(0.4, 0.32, 0.08, 9); disc.translate(0, 0.82, 0);
    return G.merge([{ g: col, c: new THREE.Color(0.48, 0.22, 0.42) }, { g: disc, c: new THREE.Color(0.95, 0.55, 0.75) }]);
  }
  function initAnemones(scene) {
    const N = Math.min(4, E.shoals.length);
    if (!N) return;
    E.hvBodyIM = new THREE.InstancedMesh(hvBodyGeo(), new THREE.MeshLambertMaterial({ vertexColors: true, emissive: 0x2a0a24 }), N);
    E.hvTentIM = new THREE.InstancedMesh(tentGeo(), new THREE.MeshBasicMaterial({ vertexColors: true }), N * HV_TENT);
    const pg = new THREE.SphereGeometry(1, 6, 4); pg.scale(0.035, 0.05, 0.12);
    E.pestrIM = new THREE.InstancedMesh(pg, new THREE.MeshBasicMaterial({ color: 0xffffff }), N * HV_PESTR);
    [E.hvBodyIM, E.hvTentIM, E.pestrIM].forEach(m => { m.frustumCulled = false; scene.add(m); });
    const pc = [new THREE.Color(0xff7a2a), new THREE.Color(0xfff1d0), new THREE.Color(0xff9a4a)];
    for (let i = 0; i < N; i++) {
      const sh = E.shoals[i], a = i * 2.1 + 0.7;
      const base = new V3(sh.home.x + Math.cos(a) * 5, 0, sh.home.z + Math.sin(a) * 5); base.y = H(base.x, base.z);
      const top = base.clone().add(new V3(0, 0.85, 0));
      const hv = { i, base, top, mouth: top.clone().add(new V3(0, 0.7, 0)), open: 1, lit: false, digest: 0, lure: null, lureT: 0, lureCd: G.rr(4, 12), leftT: 0, hideCd: 0, msgCd: 0,
        tent: Array.from({ length: HV_TENT }, (_, k) => ({ a: k / HV_TENT * 6.283 + G.rr(-0.1, 0.1), tilt: G.rr(0.35, 1.0), len: G.rr(0.9, 1.4), ph: G.rr(0, 6) })),
        pestr: Array.from({ length: HV_PESTR }, (_, k) => ({ ph: k / HV_PESTR * 6.283, r: G.rr(0.3, 0.6), p: top.clone(), c: new V3() })), pc: new V3() };
      dummy.position.copy(base); dummy.rotation.set(0, a, 0); dummy.scale.setScalar(1); dummy.updateMatrix(); E.hvBodyIM.setMatrixAt(i, dummy.matrix);
      hv.pestr.forEach((p, k) => E.pestrIM.setColorAt(i * HV_PESTR + k, pc[k % pc.length]));
      E.anemones.push(hv);
    }
    E.hvBodyIM.instanceMatrix.needsUpdate = true;
    if (E.pestrIM.instanceColor) E.pestrIM.instanceColor.needsUpdate = true;
  }
  function anemoneUpdate(hv, dt, t) {
    hv.digest -= dt; hv.lureCd -= dt; hv.hideCd -= dt; hv.msgCd -= dt;
    const say = fn => (hv.msgCd > 0 ? null : fn); // не засорять ленту: у одного хватня — одна история за раз
    const wasLit = hv.lit; hv.lit = inBeam(hv.top);
    /* щупальца: в луче быстро прячутся, в темноте медленно раскрываются */
    const want = hv.lit ? 0 : hv.digest > 0 ? 0.5 : 1;
    hv.open = want < hv.open ? Math.max(want, hv.open - dt * 2.5) : Math.min(want, hv.open + dt * 0.35);
    if (hv.lit && !wasLit && hv.open > 0.5 && hv.hideCd <= 0) {
      hv.hideCd = 30;
      E.emit('hvaten', 'hide', hv.top, () => `${cap(G.nm('hvaten'))} прячет щупальца от прожектора`);
      E.emit('pestr', 'immune', hv.top, () => `${cap(G.nm('pestr'))} ныряет в сжатые щупальца ${G.nm('hvaten', 'gen')}`);
    }
    if (hv.lit) { if (hv.lure) hv.lure = null; }
    /* пестрянки подманивают ближайшую стаю */
    if (!hv.lure && hv.open > 0.85 && hv.digest <= 0 && hv.lureCd <= 0) {
      let best = null, bd = 30;
      for (const sh of E.shoals) { const d = sh.c.distanceTo(hv.top); if (sh.n >= 3 && !sh.follow && sh.alarm <= 0 && d < bd) { bd = d; best = sh; } }
      hv.lureCd = G.rr(25, 45);
      if (best) {
        hv.lure = best; hv.lureT = 14;
        E.emit('pestr', 'lure', hv.top, say(() => `${cap(G.nm('pestr'))} выманивает ${G.nm('fish', 'acc')} к щупальцам`));
      }
    }
    if (hv.lure) {
      const sh = hv.lure; hv.lureT -= dt;
      if (hv.lureT <= 0 || sh.follow || sh.alarm > 0) hv.lure = null;
      else { sh.goal.set(hv.top.x, hv.top.y + 1.1, hv.top.z); sh.gt = Math.max(sh.gt, 0.5); }
    }
    /* ловля: только раскрытые щупальца и только в темноте */
    if (hv.open > 0.7 && hv.digest <= 0) {
      for (const f of E.fish) {
        if (!f.alive) continue;
        const d = f.p.distanceTo(hv.mouth);
        if (d < HV_REACH * hv.open && G.rng() < dt * 1.5) {
          killFish(f); hv.digest = 35; hv.lure = null; hv.leftT = 3;
          E.emit('hvaten', 'catch', hv.mouth, say(() => `${cap(G.nm('hvaten'))} поймал ${G.nm('fish', 'acc')} щупальцами`));
          break;
        }
      }
    }
    if (hv.leftT > 0) { hv.leftT -= dt; if (hv.leftT <= 0) { E.emit('pestr', 'leftovers', hv.mouth, say(() => `${cap(G.nm('pestr'))} доедает остатки добычи ${G.nm('hvaten', 'gen')}`)); hv.msgCd = 90; } }
    /* отрисовка щупалец */
    const o = hv.open, base = hv.i * HV_TENT;
    hv.tent.forEach((tn, k) => {
      const sway = Math.sin(t * 1.4 + tn.ph) * 0.18 * o;
      dummy.position.set(hv.top.x + Math.cos(tn.a) * 0.3, hv.top.y, hv.top.z + Math.sin(tn.a) * 0.3);
      dummy.rotation.set(0, -tn.a, 0, 'YXZ');
      dummy.rotateZ(-(tn.tilt * (0.25 + 0.75 * o) + sway));
      const L = tn.len * (0.12 + 0.88 * o);
      dummy.scale.set(1 + (1 - o), L, 1 + (1 - o)); dummy.updateMatrix();
      E.hvTentIM.setMatrixAt(base + k, dummy.matrix);
    });
    /* пестрянки: среди щупалец, при подманивании выплывают к стае */
    hv.pc.set(0, 0, 0);
    hv.pestr.forEach((p, k) => {
      const a = t * (hv.lure ? 1.6 : 0.9) + p.ph;
      if (hv.lure) tv.copy(hv.top).lerp(hv.lure.c, 0.45 + Math.sin(t * 2 + p.ph) * 0.2).add(tv2.set(Math.cos(a) * 0.6, 0.3, Math.sin(a) * 0.6));
      else tv.set(hv.top.x + Math.cos(a) * p.r * (0.4 + 0.6 * o), hv.top.y + 0.25 + 0.35 * o + Math.sin(t * 2.3 + p.ph) * 0.08, hv.top.z + Math.sin(a) * p.r * (0.4 + 0.6 * o));
      tv3.copy(p.p); p.p.lerp(tv, Math.min(1, dt * 2.5)); hv.pc.add(p.p);
      tv2.subVectors(p.p, tv3); if (tv2.lengthSq() < 1e-8) tv2.set(-Math.sin(a), 0, Math.cos(a));
      dummy.position.copy(p.p); dummy.lookAt(tv3.copy(p.p).add(tv2)); dummy.rotation.z += Math.sin(t * 9 + p.ph) * 0.2;
      dummy.scale.setScalar(1); dummy.updateMatrix(); E.pestrIM.setMatrixAt(hv.i * HV_PESTR + k, dummy.matrix);
    });
    hv.pc.divideScalar(HV_PESTR);
  }

  /* ======================= ОБЩЕЕ ======================= */
  E.init = function (scene) { initVeils(scene); initFish(scene); initStations(scene); initAnemones(scene); initPreds(scene); initGrazers(scene); initEels(scene); initRays(scene); };

  E.update = function (dt, t) {
    const S = G.Ship;
    E.veils.forEach(v => veilUpdate(v, dt, t));
    E.shipHidden = E.veils.some(vl => Math.hypot(S.center.x - vl.p.x, S.center.z - vl.p.z) < vl.r + 6 && S.center.y < vl.p.y + 1 && S.center.y > vl.p.y - vl.len - 6);
    stationsUpdate(dt, t);
    updateBeam(); E.anemones.forEach(hv => anemoneUpdate(hv, dt, t));
    if (E.hvTentIM) { E.hvTentIM.instanceMatrix.needsUpdate = true; E.pestrIM.instanceMatrix.needsUpdate = true; }
    updateFish(dt, t);
    E.preds.forEach((p, i) => { predUpdate(p, dt); predRender(p, i, t); });
    E.predSegIM.instanceMatrix.needsUpdate = true; E.predDotIM.instanceMatrix.needsUpdate = true;
    E.grazers.forEach(gz => grazerUpdate(gz, dt, t));
    E.eels.forEach((el, i) => { eelUpdate(el, dt, t); eelRender(el, i, t); });
    E.eelIM.instanceMatrix.needsUpdate = true; E.eelDotIM.instanceMatrix.needsUpdate = true;
    E.rays.forEach(r => rayUpdate(r, dt, t));
  };

  E.scanTargets = function () {
    const L = [];
    E.shoals.forEach(sh => { if (sh.n >= 3) L.push({ sp: 'fish', pos: sh.c, r: 2.5 }); });
    E.preds.forEach(p => L.push({ sp: 'pred', pos: p.p, r: 2.5 }));
    E.veils.forEach(v => L.push({ sp: 'veil', pos: v.scanP, r: 7 }));
    E.grazers.forEach(gz => { if (gz.alive) L.push({ sp: 'grazer', pos: gz.p, r: 1.4 }); });
    E.eels.forEach(el => L.push({ sp: 'eel', pos: el.p, r: 1.3 }));
    E.rays.forEach(r => L.push({ sp: 'ray', pos: r.p, r: 7 }));
    E.stations.forEach(st => L.push({ sp: 'ognevka', pos: st.top, r: 1.6 }));
    E.preds.forEach(p => { if (p.leech) L.push({ sp: 'leech', pos: p.leechP, r: 0.7 }); });
    E.anemones.forEach(hv => { L.push({ sp: 'hvaten', pos: hv.top, r: 1.2 }); L.push({ sp: 'pestr', pos: hv.pc, r: 0.6 }); });
    return L;
  };

  E.onPing = function (pos) {
    E.lastPing = G.Game.t; let heard = false;
    E.preds.forEach(pr => {
      if (pr.state === 'charge' || pr.state === 'retreat' || pr.state === 'flee') return;
      const d = pr.p.distanceTo(pos);
      if (d < 150) { pr.state = 'investigate'; pr.goal.copy(pos); pr.timer = 25; if (d < 110) heard = true; }
    });
    if (heard) E.emit('pred', 'ping', pos, () => `${cap(G.nm('pred'))} услышал сонар и плывёт на звук`);
  };

  E.onZap = function () {
    const S = G.Ship;
    if (E.latched) {
      const el = E.latched; el.state = 'flee'; el.cd = 40; E.latched = null;
      E.emit('eel', 'zap', S.front, () => `Разряд сбросил ${G.nm('eel', 'acc')} с корпуса`);
    }
    E.preds.forEach(pr => {
      if (pr.p.distanceTo(S.center) < 20) {
        pr.state = 'flee'; pr.timer = 8; pr.cd = 30;
        tv.copy(pr.p).sub(S.center); if (tv.lengthSq() < 0.01) tv.set(1, 0, 0);
        pr.goal.copy(pr.p).add(tv.normalize().multiplyScalar(50)); pr.goal.y = Math.min(pr.goal.y, -6);
        E.emit('pred', 'zap', pr.p, () => `Разряд отпугнул ${G.nm('pred', 'acc')}`);
      }
    });
  };

  E.reset = function () {
    if (E.latched) { E.latched.state = 'flee'; E.latched.cd = 40; E.latched = null; }
    E.preds.forEach((pr, i) => { pr.state = 'patrol'; pr.timer = 0; pr.cd = 30; pr.target = null; setLeech(pr, i === 0); pr.leechCd = G.rr(90, 160); pr.truceCd = 0; pr.angryCd = 0; });
    E.stations.forEach(st => { st.busy = null; st.work = 0; });
    E.anemones.forEach(hv => { hv.open = 1; hv.lit = false; hv.digest = 0; hv.lure = null; hv.lureT = 0; hv.lureCd = G.rr(4, 12); hv.leftT = 0; hv.hideCd = 0; hv.msgCd = 0; });
  };
})(window.G);
