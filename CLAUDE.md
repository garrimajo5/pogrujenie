# Погружение — правила для разработки

Кооперативная игра про батискаф «Ковчег-7» в подлёдном океане. Сейчас это одиночный прототип в браузере на three.js r149 (лежит в `lib/`, без сборки и без CDN).
Контекст и решения: `docs/context.txt`, бестиарий на 100 видов: `docs/bestiary.md`, концепт-арт: `docs/concept-art/`, план: `ROADMAP.md`.

## Как устроен код
- `index.html` — интерфейс, обычные `<script>` без модулей, всё в глобальном `window.G`.
- `js/util.js` шум и геометрия · `js/world.js` рельеф, флора, места · `js/ship.js` отсеки, посты, физика корабля
- `js/creatures.js` существа и экосистема (`G.Eco`, `G.SPECIES`) · `js/sonar.js` голостол · `js/controls.js` крестовина/свайп/гироскоп/клавиатура
- `js/audio.js` процедурный звук · `js/game.js` цикл, игрок, взаимодействия, журнал, `G.Game.debug.step(n)` для прогона без рендера
- Координаты корабля: нос в −Z, пол y=0. Мостик z∈[−6,2], коридор [2,8], лаборатория [8,14].

## Правила
- Интерфейс и тексты — на русском, коротко и по делу.
- Игра должна работать на Android-телефоне в Chrome: управление крестовиной + кнопка действия, без клавиатуры.
- Бюджет производительности: draw calls < 260, треугольников < 350 тыс. (смотреть `G.Game.debug.info()`).
- Новые существа: запись в `G.SPECIES` (имя, падежи, описание, notes), поведение и взаимодействия через `E.emit(...)`, цель сканирования в `E.scanTargets()`.
- Не подключать внешние библиотеки по сети. Если нужна библиотека — положить файл в `lib/`.

## Тесты
- Логические тесты лежат в `tests/*.test.js` и выполняются внутри настоящей игры (`tools/test.py`). Фреймворк: `tests/framework.js` (`T.describe`, `T.test`, `T.ok/eq/near/gt/lt`, `T.captureEvents()`).
- Перед каждым тестом мир сбрасывается `G.Game.debug.reset()`. Время двигают `G.Game.debug.frame(n)` (полный кадр логики с вводом и взаимодействиями) или `G.Game.debug.step(n)` (только симуляция).
- Ввод в тестах: `G.Ctl.padMove`, `G.Ctl.btnVert`, `G.Ctl.actionPressed/actionDown`, прицел: `G.Game.debug.lookAt(точка)`.
- **Каждая новая фича или исправление приходит с тестами**: поведение существ и их связи, реакции на корабль, кнопки и посты, сохранение. Исправленный баг закрепляется тестом, который без исправления падает.

## Проверка перед PR
1. `node --check js/*.js`
2. `python3 tools/test.py` — все тесты зелёные.
3. `python3 tools/smoke.py --shots shots` — без ошибок, посмотреть скриншоты `shots/desktop.png` и `shots/phone.png`.

## Процесс
- Ветка `feat/<кратко>` или `fix/<кратко>` от свежего `main`.
- Одна задача из `ROADMAP.md` = одна PR. В PR: что сделано, как проверено.
- После проверки PR сливается в `main` (squash). Владелец разрешил сливать самостоятельно.
- В той же PR отметить задачу `[x]` в `ROADMAP.md` и добавить строку в `CHANGELOG.md`.

## Unity (этап 5)
- Проект в `unity/`, Unity 6000.6.6f1 (Personal), цель — Android (ARM64, IL2CPP, minSdk 26, альбомная ориентация). iPhone позже.
- Сборки: `Pogrujenie.Core` — чистый C# без UnityEngine (`noEngineReferences`), сюда идёт логика из `js/` (реактор, экосистема, правила), её тестируют EditMode-тесты. `Pogrujenie.Game` — MonoBehaviour и сцена. `Pogrujenie.Editor` — меню и `BuildScript.BuildAndroid`.
- Сцена собирается из кода (`Bootstrap`). Сцена `Assets/Scenes/Main.unity` создаётся сборкой или меню «Погружение → Создать сцену Main»; в репозиторий её не кладём, пока не понадобятся ручные правки.
- Оси в Unity: нос в −Z, пол y=0, как в браузере, но система координат левая. Лицом к носу правый борт — это −X (в браузере +X), поэтому x из `js/` при переносе меняет знак. Курс игрока: вперёд = (sin yaw, cos yaw), yaw = π — лицом к носу.
- Отсеки: `Core/ShipLayout.cs` — бруски стен (`Boxes`), люки, препятствия, `CanStand`, посты кнопки действия (`Inter`, `Nearest`). Новый пост — `Layout.Inter.Add(new Interactable(x, z, r, label, act))`. Рисует отсеки `Game/ShipView.cs`.
- Ввод: `TouchInput` (крестовина, кнопка действия, свайп). В тестах `Controls.PadOverride`, `Controls.InjectLook`, `Controls.PressAction()`. Логика касаний в `Core/TouchControls.cs`.
- Тесты: `unity/Assets/Tests/EditMode` (NUnit, логика из Core) и `unity/Assets/Tests/PlayMode` (сцена, `[UnityTest]`). Новая фича — с тестами, как и в браузерной версии.
- Редактора Unity в облачной сессии нет: тесты и сборка идут только в GitHub Actions, проверка «Unity» (`.github/workflows/unity.yml`, GameCI, секреты `UNITY_LICENSE`, `UNITY_EMAIL`, `UNITY_PASSWORD`). Перед PR внимательно перечитать C#: компилятор увидим только в CI. PR сливается, когда зелёные и «Тесты», и «Unity».
- APK — артефакт `pogrujenie-apk` у запуска «Unity».
