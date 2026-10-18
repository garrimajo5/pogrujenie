# Прогон Unity-сборки на эмуляторе: обновить main, собрать APK, поставить, снять скриншоты и лог.
# Пишет результат в D:\AI\qa-build\run\ и печатает итог. Отчёт по результату пишет Claude.
param([switch]$Force)
$ErrorActionPreference = 'Continue'
$repo  = 'D:\AI\Pogrujenie'
$out   = 'D:\AI\qa-build'
$run   = "$out\run"
$sdk   = "$env:LOCALAPPDATA\Android\Sdk"
$adb   = "$sdk\platform-tools\adb.exe"
$unity = 'C:\Program Files\Unity\Hub\Editor\6000.6.6f1\Editor\Unity.exe'
$app   = 'com.garrimajo5.pogrujenie'

git -C $repo fetch -q origin main
$head = (git -C $repo rev-parse --short origin/main).Trim()
$last = if (Test-Path "$out\last-commit.txt") { (Get-Content "$out\last-commit.txt").Trim() } else { '' }
if ($head -eq $last -and -not $Force) { Write-Output "NO_CHANGES $head"; exit 0 }
# Клон только для тестов: локальные правки (сгенерированные .meta) не нужны
git -C $repo checkout -q -f -B main origin/main
$subject = git -C $repo log -1 --format=%s origin/main

Remove-Item -Recurse -Force $run -ErrorAction SilentlyContinue
New-Item -ItemType Directory -Force $run | Out-Null

# Эмулятор
if (-not ((& $adb devices) -match 'emulator-\d+\s+device')) {
  Start-Process "$sdk\emulator\emulator.exe" -ArgumentList '-avd','Pixel_8' -WindowStyle Minimized
  & $adb wait-for-device
  for ($i = 0; $i -lt 60; $i++) { if ((& $adb shell getprop sys.boot_completed).Trim() -eq '1') { break }; Start-Sleep 5 }
}

# Сборка
$p = Start-Process -FilePath $unity -ArgumentList '-batchmode','-nographics','-quit','-projectPath',"$repo\unity",'-buildTarget','Android','-executeMethod','Pogrujenie.EditorTools.BuildScript.BuildAndroid','-customBuildPath',"$out\Pogrujenie.apk",'-logFile',"$run\build.log" -PassThru -Wait
$buildLine = (Select-String -Path "$run\build.log" -Pattern 'Build Finished, Result' | Select -Last 1).Line
$csErrors = Select-String -Path "$run\build.log" -Pattern 'error CS\d+' | ForEach-Object Line | Sort -Unique
if ($p.ExitCode -ne 0) {
  Write-Output "BUILD_FAILED $head exit=$($p.ExitCode)"; $csErrors | Select -First 30; exit 1
}

# Установка и запуск
& $adb install -r "$out\Pogrujenie.apk" | Out-Null
& $adb shell am force-stop $app
& $adb logcat -c
& $adb shell monkey -p $app -c android.intent.category.LAUNCHER 1 | Out-Null
Start-Sleep 20
function Shot($name) { cmd /c "`"$adb`" exec-out screencap -p > `"$run\$name.png`"" }
Shot '01-start'
& $adb shell input tap 2180 810; Start-Sleep 2; Shot '02-action'
& $adb shell input swipe 402 690 402 690 3000; Shot '03-forward'
Start-Sleep 5
$appPid = (& $adb shell pidof $app).Trim()
$alive = [bool]$appPid
& $adb logcat -d | Set-Content "$run\logcat.txt" -Encoding utf8
if ($alive) { & $adb logcat -d --pid=$appPid | Set-Content "$run\app.txt" -Encoding utf8 } else { Copy-Item "$run\logcat.txt" "$run\app.txt" }
$errs = Select-String -Path "$run\app.txt" -Pattern ' E Unity|AndroidRuntime|FATAL' | ForEach-Object { ($_.Line -split ': ', 2)[1] } |
  Where-Object { $_ -and $_ -notmatch '^\s*$|GameObjectUtility\.cpp|^UnityEngine\.|^Pogrujenie\.' } |
  Group-Object | Sort Count -Descending | ForEach-Object { "$($_.Count)x $($_.Name)" }

Set-Content "$out\last-commit.txt" $head
Write-Output "OK $head $subject"
Write-Output "build: $buildLine"
Write-Output "app alive after test: $alive"
Write-Output "errors:"; $errs | Select -First 30
Write-Output "files: $run"
