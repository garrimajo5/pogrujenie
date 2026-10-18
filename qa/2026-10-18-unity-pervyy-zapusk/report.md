# Первый запуск Unity-сборки на эмуляторе

- Коммит: `549038f` (main, «Unity: отмель…», #57)
- Сборка: локально, Unity 6000.6.6f1, `BuildScript.BuildAndroid` в batchmode. Результат Succeeded, 0 ошибок, APK 16 МБ.
- Устройство: эмулятор Pixel 8, Android 17 (API 37), x86_64 с трансляцией arm64-v8a, 2400×1080.

## Что работает
- APK ставится и запускается, не падает.
- Интерфейс на месте: заголовок, строка состояния, крестовина, кнопка действия.
- «Сесть за штурвал» сажает за штурвал, появляются кнопки «Свет», «Всплыть», «Вниз», «Встать».
- Крестовина вверх 3 с за штурвалом даёт ход 4.8 м/с, корабль движется (скриншот 03).
- Рельеф дна (шейдер `Pogrujenie/Seabed`) и туман рисуются.

## Ошибки

### 1. Почти всё, кроме дна, розовое (нет шейдера) — критично
Корпус и отсеки «Ковчега», светлячки и рамка вокруг вида розовые (скриншоты 01–03).
Похоже, в сборку не попал шейдер материалов примитивов (`ShipView`: `baseMat` берётся с `CreatePrimitive`, `Bootstrap`: `r.material.color`). Шейдер `Standard` (fileID 46) нет в `m_AlwaysIncludedShaders` в `GraphicsSettings.asset`. Дно работает, потому что его шейдер лежит в `Resources`.
Предположение: добавить нужный шейдер в Always Included Shaders или держать свой материал или шейдер в `Resources` и брать его оттуда. В редакторе и в тестах это не видно, только в сборке.

### 2. В логе 84 ошибки «class '…Collider' doesn't exist»
`Can't add component because class 'BoxCollider' doesn't exist!` 56 раз, `SphereCollider` 24 раза, `CapsuleCollider` 4 раза. Стек `GameObject:CreatePrimitive` ← `Bootstrap:Build()`.
Коллайдеры и так удаляются сразу после создания, поэтому на игру это не влияет. Но физический модуль вырезан стриппингом (`stripEngineCode: 1`), и если коллайдеры понадобятся, они не заработают. Выдержка в `log-excerpt.txt`.

### 3. Мелочи
- `ClassNotFoundException: com.google.android.play.core.assetpacks.AssetPackManager`, один раз при старте. Безвредно, Play Asset Delivery не используется.
- В репозитории нет `.meta`-файлов для `Assets/Editor`, `Assets/Resources`, `Assets/Scripts` и других. Unity создаёт их заново при каждом импорте, GUID каждый раз новые. Ссылки на ассеты по GUID будут ломаться между машинами. Стоит закоммитить `.meta`.
- Unity при открытии дописывает `m_EditorVersionWithRevision` в `ProjectVersion.txt`.

## Как воспроизвести
1. Собрать APK, поставить: `adb install -r Pogrujenie.apk`.
2. Запустить, посмотреть на мостик (01), нажать «Сесть за штурвал» (02), держать крестовину вверх (03).
3. `adb logcat -d -s Unity:E`
