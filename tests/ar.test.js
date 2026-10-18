'use strict';
/* AR на телефоне: кнопка по поддержке браузера, прицел hit-test, голостол на столе */
T.describe('AR', () => {
  const AR = () => G.AR;
  const fakeXR = ok => ({ isSessionSupported: async mode => ok && mode === 'immersive-ar' });
  /* поза поверхности: стол в метре впереди и на 70 см ниже телефона */
  const tableAt = (x, y, z) => new THREE.Matrix4().makeTranslation(x, y, z).toArray();

  T.test('кнопка AR появляется только там, где браузер умеет immersive-ar', async () => {
    const b = document.getElementById('b-ar');
    T.ok(await AR().check(fakeXR(true)), 'поддержка не распознана'); T.ok(!b.hidden, 'кнопки нет');
    T.ok(!(await AR().check(fakeXR(false)))); T.ok(b.hidden, 'кнопка без поддержки');
    T.ok(!(await AR().check(null))); T.ok(b.hidden);
  });

  T.test('сцена AR: голостол с картой сонара спрятан до установки', () => {
    T.ok(AR().scene && AR().holo && AR().reticle);
    T.ok(!AR().holo.visible); T.ok(!AR().reticle.visible);
    const disc = AR().holo.children.find(m => m.material && m.material.map === G.Sonar.tex);
    T.ok(disc, 'на голостоле нет карты сонара');
    T.ok(!AR().camera.parent, 'у камеры AR есть родитель — XR поставит её не туда');
  });

  T.test('hit-test двигает прицел, касание ставит голостол на стол', () => {
    AR().placed = false;
    AR().onHit(tableAt(0, -0.7, -1));
    T.ok(AR().reticle.visible, 'прицела нет');
    T.ok(AR().onSelect(), 'касание не поставило стол');
    T.ok(AR().holo.visible && AR().placed);
    T.near(AR().holo.position.y, -0.7, 1e-6); T.near(AR().holo.position.z, -1, 1e-6);
    T.ok(!AR().reticle.visible, 'прицел остался после установки');
    AR().onHit(tableAt(2, 0, 0)); T.near(AR().holo.position.x, 0, 1e-6, 'стол уехал за новой позой');
    AR().onEnd(); T.ok(!AR().holo.visible && !AR().placed);
  });

  T.test('без поверхности касание ничего не ставит (контроль)', () => {
    AR().onEnd(); AR().onHit(null);
    T.ok(!AR().onSelect()); T.ok(!AR().holo.visible);
  });

  T.test('кадр XR берёт позу из hit-test', () => {
    AR().onEnd();
    AR().hitSource = {};
    const frame = { getHitTestResults: () => [{ getPose: () => ({ transform: { matrix: tableAt(0.3, -0.6, -0.8) } }) }] };
    AR().frame(frame, {});
    T.ok(AR().reticle.visible);
    T.near(new THREE.Vector3().setFromMatrixPosition(AR().reticle.matrix).x, 0.3, 1e-6);
    AR().frame({ getHitTestResults: () => [] }, {}); T.ok(!AR().reticle.visible, 'прицел без поверхности');
    AR().hitSource = null;
  });
});
