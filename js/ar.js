'use strict';
/* AR на телефоне (WebXR). Если браузер умеет immersive-ar, на панели появляется кнопка «AR».
   В AR камера показывает комнату, а по реальному столу бегает кольцо-прицел (hit-test).
   Касание ставит на стол голостол: живую карту сонара вокруг «Ковчега». Корабль ведут крестовиной
   как за штурвалом, пинг и свет — кнопками на экране. Нужен HTTPS (GitHub Pages). */
(function (G) {
  /* Иллюминаторы: после голостола касание по реальной стене вешает на неё иллюминатор, за ним — океан
     вокруг «Ковчега». Стол задаёт оси корабля: от стола вперёд — нос. Если телефон не находит стен,
     через несколько секунд иллюминаторы сами встают вокруг стола. Океан рисуется в текстуру
     с камеры на мостике, по одному иллюминатору за кадр. */
  const WIN_MAX = 4, WIN_R = 0.35, WIN_AUTO = 8, RT = 256;
  const AR = G.AR = { supported: false, active: false, placed: false, session: null, hitSource: null, scene: null, reticle: null, holo: null,
    windows: [], autoT: 0, winIdx: 0, viewer: new THREE.Vector3() };

  /* поддерживает ли браузер AR — показать кнопку */
  AR.check = async function (xr) {
    xr = xr || navigator.xr;
    let ok = false;
    try { ok = !!(xr && xr.isSessionSupported && await xr.isSessionSupported('immersive-ar')); } catch (_) { ok = false; }
    AR.supported = ok;
    const b = document.getElementById('b-ar'); if (b) b.hidden = !ok;
    return ok;
  };

  /* сцена AR: прицел и голостол с картой сонара */
  AR.build = function () {
    const sc = AR.scene = new THREE.Scene();
    AR.camera = new THREE.PerspectiveCamera(70, 1, 0.01, 50); // своя камера без родителя: XR ставит её по позе телефона в комнате
    sc.add(new THREE.HemisphereLight(0xffffff, 0x334455, 1.0));
    const ret = AR.reticle = new THREE.Mesh(new THREE.RingGeometry(0.05, 0.065, 32).rotateX(-Math.PI / 2), new THREE.MeshBasicMaterial({ color: 0x5ef2d6 }));
    ret.matrixAutoUpdate = false; ret.visible = false; sc.add(ret);
    const h = AR.holo = new THREE.Group();
    const base = new THREE.Mesh(new THREE.CylinderGeometry(0.27, 0.3, 0.04, 32), new THREE.MeshLambertMaterial({ color: 0x1f2b30 })); base.position.y = 0.02; h.add(base);
    const disc = new THREE.Mesh(new THREE.CircleGeometry(0.25, 48).rotateX(-Math.PI / 2), new THREE.MeshBasicMaterial({ map: G.Sonar.tex, transparent: true, blending: THREE.AdditiveBlending, depthWrite: false }));
    disc.position.y = 0.045; h.add(disc);
    const rim = new THREE.Mesh(new THREE.TorusGeometry(0.255, 0.006, 6, 48).rotateX(Math.PI / 2), new THREE.MeshBasicMaterial({ color: 0x5ef2d6 })); rim.position.y = 0.045; h.add(rim);
    const col = new THREE.Mesh(new THREE.CylinderGeometry(0.25, 0.25, 0.12, 32, 1, true), new THREE.MeshBasicMaterial({ color: 0x5ef2d6, transparent: true, opacity: 0.08, side: THREE.DoubleSide, depthWrite: false, blending: THREE.AdditiveBlending }));
    col.position.y = 0.1; h.add(col);
    h.visible = false; sc.add(h);
    AR.winCam = new THREE.PerspectiveCamera(70, 1, 0.05, 420);
    AR.frameGeo = new THREE.TorusGeometry(WIN_R + 0.02, 0.04, 8, 36); AR.glassGeo = new THREE.CircleGeometry(WIN_R, 40);
    AR.frameMat = new THREE.MeshLambertMaterial({ color: 0x8aa0a8 });
  };
  /* повесить иллюминатор: позиция и нормаль стены в комнате */
  AR.addWindow = function (pos, normal) {
    if (AR.windows.length >= WIN_MAX) return null;
    const rt = new THREE.WebGLRenderTarget(RT, RT);
    const g = new THREE.Group();
    const glass = new THREE.Mesh(AR.glassGeo, new THREE.MeshBasicMaterial({ map: rt.texture })); g.add(glass);
    g.add(new THREE.Mesh(AR.frameGeo, AR.frameMat));
    g.position.copy(pos).addScaledVector(normal, 0.01);
    g.quaternion.setFromUnitVectors(new THREE.Vector3(0, 0, 1), normal.clone().normalize()); // стекло смотрит в комнату
    AR.scene.add(g);
    const w = { g, rt, pos: g.position.clone(), n: normal.clone().normalize() };
    AR.windows.push(w);
    return w;
  };
  /* если стен не нашлось — три иллюминатора вокруг стола на уровне глаз */
  AR.autoWindows = function () {
    const c = AR.holo.position;
    [[-1, 0], [1, 0], [0, -1]].forEach(([x, z]) => { const p = new THREE.Vector3(c.x + x * 1.8, c.y + 0.6, c.z + z * 1.8); AR.addWindow(p, new THREE.Vector3(-x, 0, -z)); });
    G.Game.toast('Иллюминаторы', 'Стен не нашлось — иллюминаторы встали вокруг стола. Подойдите и загляните в них.');
  };
  /* куда смотреть с мостика: от глаз игрока к иллюминатору, в осях корабля (оси комнаты = оси стола) */
  AR.windowDir = function (w, viewer, out) {
    out.copy(w.pos).sub(viewer); out.y *= 0.6; if (out.lengthSq() < 1e-6) out.copy(w.n).negate();
    return out.normalize().applyQuaternion(G.Ship.group.quaternion);
  };
  const tdir = new THREE.Vector3(), tpos = new THREE.Vector3();
  /* нарисовать океан в иллюминаторы: по одному за кадр, XR на это время выключаем */
  AR.renderWindows = function (renderer, scene) {
    if (!AR.windows.length) return 0;
    const w = AR.windows[AR.winIdx++ % AR.windows.length];
    G.Ship.group.localToWorld(tpos.set(0, 1.65, -3));
    AR.winCam.position.copy(tpos); AR.winCam.lookAt(tpos.add(AR.windowDir(w, AR.viewer, tdir)));
    const xrOn = renderer.xr.enabled, prev = renderer.getRenderTarget();
    renderer.xr.enabled = false; renderer.setRenderTarget(w.rt); renderer.render(scene, AR.winCam);
    renderer.setRenderTarget(prev); renderer.xr.enabled = xrOn;
    return 1;
  };
  AR.clearWindows = function () { AR.windows.forEach(w => { AR.scene.remove(w.g); w.rt.dispose(); w.g.children[0].material.dispose(); }); AR.windows.length = 0; AR.autoT = 0; };

  /* hit-test дал позу поверхности (массив 16 чисел) — двигаем прицел */
  AR.onHit = function (m16) {
    if (!m16) { AR.reticle.visible = false; return; }
    AR.reticle.matrix.fromArray(m16);
    AR.reticle.visible = !AR.placed || (AR.isWall() && AR.windows.length < WIN_MAX); // после стола прицел ищет стены
  };
  /* поверхность под прицелом вертикальная: ось Y позы hit-test — нормаль поверхности */
  AR.isWall = function () { const e = AR.reticle.matrix.elements; return Math.abs(e[5]) < 0.5; };
  /* касание экрана — поставить голостол туда, где прицел */
  AR.onSelect = function () {
    if (!AR.reticle || !AR.reticle.visible) return false;
    if (AR.placed) { // стол уже стоит — вешаем иллюминатор на стену
      if (!AR.isWall()) return false;
      const e = AR.reticle.matrix.elements, n = new THREE.Vector3(e[4], e[5], e[6]);
      const w = AR.addWindow(new THREE.Vector3().setFromMatrixPosition(AR.reticle.matrix), n);
      if (w) { G.Audio.beep(820); G.Game.toast('Иллюминатор', 'За ним — океан вокруг «Ковчега».'); }
      return !!w;
    }
    AR.holo.position.setFromMatrixPosition(AR.reticle.matrix);
    AR.holo.visible = true; AR.placed = true; AR.reticle.visible = false;
    G.Game.toast('Голостол на месте', 'Крестовина ведёт корабль, «Сонар» показывает, что вокруг. Чтобы переставить стол, выйдите из AR и войдите снова.');
    G.Audio.beep(760);
    return true;
  };
  AR.frame = function (frame, refSpace, dt, camera) {
    if (camera) camera.getWorldPosition(AR.viewer); // где глаза игрока в комнате
    if (AR.placed && !AR.windows.length) { AR.autoT += dt || 0; if (AR.autoT > WIN_AUTO) AR.autoWindows(); }
    if (!frame || !AR.hitSource) return;
    const res = frame.getHitTestResults(AR.hitSource);
    const pose = res.length ? res[0].getPose(refSpace) : null;
    AR.onHit(pose ? pose.transform.matrix : null);
  };

  AR.start = async function (renderer) {
    if (AR.active || !navigator.xr) return false;
    let s;
    try {
      s = await navigator.xr.requestSession('immersive-ar', { requiredFeatures: ['hit-test'], optionalFeatures: ['dom-overlay'], domOverlay: { root: document.getElementById('hud') } });
    } catch (e) { G.Game.toast('AR недоступен', 'Браузер не дал включить AR: ' + (e && e.message || e)); return false; }
    AR.session = s; renderer.xr.setReferenceSpaceType('local');
    await renderer.xr.setSession(s);
    const viewer = await s.requestReferenceSpace('viewer');
    AR.hitSource = await s.requestHitTestSource({ space: viewer });
    s.addEventListener('select', AR.onSelect);
    s.addEventListener('end', AR.onEnd);
    AR.active = true; AR.placed = false; AR.holo.visible = false;
    if (!G.Game.piloting) G.Game.toggleHelm();
    G.Game.toast('AR: найдите стол', 'Медленно ведите телефон над столом, пока не появится кольцо, и коснитесь экрана.');
    return true;
  };
  AR.onEnd = function () {
    AR.active = false; AR.placed = false; AR.session = null;
    if (AR.hitSource) { try { AR.hitSource.cancel(); } catch (_) {} AR.hitSource = null; }
    if (AR.holo) AR.holo.visible = false; if (AR.reticle) AR.reticle.visible = false;
    if (AR.scene) AR.clearWindows();
  };
  AR.stop = function () { if (AR.session) AR.session.end(); };
})(window.G);
