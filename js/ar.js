'use strict';
/* AR на телефоне (WebXR). Если браузер умеет immersive-ar, на панели появляется кнопка «AR».
   В AR камера показывает комнату, а по реальному столу бегает кольцо-прицел (hit-test).
   Касание ставит на стол голостол: живую карту сонара вокруг «Ковчега». Корабль ведут крестовиной
   как за штурвалом, пинг и свет — кнопками на экране. Нужен HTTPS (GitHub Pages). */
(function (G) {
  const AR = G.AR = { supported: false, active: false, placed: false, session: null, hitSource: null, scene: null, reticle: null, holo: null };

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
  };

  /* hit-test дал позу поверхности (массив 16 чисел) — двигаем прицел */
  AR.onHit = function (m16) {
    if (AR.placed) return;
    if (!m16) { AR.reticle.visible = false; return; }
    AR.reticle.matrix.fromArray(m16); AR.reticle.visible = true;
  };
  /* касание экрана — поставить голостол туда, где прицел */
  AR.onSelect = function () {
    if (!AR.reticle || !AR.reticle.visible) return false;
    AR.holo.position.setFromMatrixPosition(AR.reticle.matrix);
    AR.holo.visible = true; AR.placed = true; AR.reticle.visible = false;
    G.Game.toast('Голостол на месте', 'Крестовина ведёт корабль, «Сонар» показывает, что вокруг. Чтобы переставить стол, выйдите из AR и войдите снова.');
    G.Audio.beep(760);
    return true;
  };
  AR.frame = function (frame, refSpace) {
    if (!frame || !AR.hitSource || AR.placed) return;
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
  };
  AR.stop = function () { if (AR.session) AR.session.end(); };
})(window.G);
