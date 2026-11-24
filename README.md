# PCSoundBooster

PCSoundBooster — утилита для усиления звука приложений в Windows из области уведомлений. Диапазон от 100 % до 500 % соответствует усилению до +13,98 дБ. Громкий исходный сигнал при усилении может клиппировать.

[Скачать PCSoundBooster.exe для Windows x64](https://github.com/TruerDev/PCSoundBooster/releases/latest/download/PCSoundBooster.exe)

## Запуск

1. Откройте PCSoundBooster на Windows x64 и нажмите «Установить аудиодвижок». Установщик [Equalizer APO 1.4.2](https://sourceforge.net/projects/equalizerapo/files/1.4.2/) встроен в EXE.
2. Подтвердите запрос администратора и выберите колонки или наушники в Device Selector. Если установщик предложит перезагрузить Windows, перезагрузите её.
3. PCSoundBooster подключит усиление после завершения установки. Если Equalizer APO уже установлен, нажмите «Подключить Equalizer APO».
4. Включите усиление и выберите уровень. Windows продолжает управлять обычной громкостью.

Один щелчок по значку в области уведомлений открывает панель. Escape или щелчок снаружи её закрывает. Под шестерёнкой доступны сброс до 100 %, автозапуск с Windows и выход. Автозапуск открывает приложение в трее и использует путь к текущему EXE. Если переместили файл, выключите и снова включите автозапуск из нового расположения.

Команда «Выход» сбрасывает усиление до 100 % и завершает приложение. После аварийного завершения последнее записанное усиление действует до следующего изменения или штатного выхода.

При подключении приложение добавляет строку `Include: PCSoundBooster.txt` в `config.txt` и сохраняет рядом резервную копию `config.pcsoundbooster-*.bak`. Чтобы отключить интеграцию, выйдите из приложения и удалите добавленную строку из `config.txt`. Усиление действует на выходах с включённым Equalizer APO.

## Сборка

Для сборки из клона нужен .NET 8 SDK на Windows. Установщик Equalizer APO уже находится в репозитории. Опубликованный EXE включает среду .NET Desktop Runtime для Windows x64.

```powershell
dotnet build -c Release
dotnet run -c Release -- --self-test
dotnet publish -c Release -r win-x64 --self-contained true -p:RuntimeFrameworkVersion=8.0.34 -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFile=true -p:DebugType=none -o artifacts
```

Готовый файл находится в `artifacts/PCSoundBooster.exe`.

## Equalizer APO

Оригинальный установщик и соответствующий архив исходников Equalizer APO 1.4.2 лежат в `third-party`. Установщик запускается отдельным процессом. При распространении EXE предоставляйте рядом `EqualizerAPO-src-1.4.2.zip` с лицензией GNU GPL v2. [Официальный выпуск](https://sourceforge.net/projects/equalizerapo/files/1.4.2/)

Штатный установщик Equalizer APO 1.4.2 устанавливает параметр Windows `DisableProtectedAudioDG` в `1`. Его программа удаления убирает этот параметр. [Документация Microsoft по APO](https://learn.microsoft.com/windows-hardware/drivers/audio/implementing-audio-processing-objects)

## Лицензия

Код PCSoundBooster предоставляется по [лицензии MIT](LICENSE). Equalizer APO распространяется на условиях своей лицензии в [архиве исходников](third-party/EqualizerAPO-src-1.4.2.zip).
