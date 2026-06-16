# FarmSim

FarmSim - учебная фермерская игра на Unity. Игрок выращивает культуры, покупает животных, собирает продукцию, зарабатывает монеты и постепенно открывает новые возможности фермы.

## Что уже есть

- Авторизация и регистрация через Firebase.
- Гостевой вход без интернета.
- Локальные сохранения в JSON.
- Cloud-save через Firebase Realtime Database для авторизованных игроков.
- Отдельные сохранения для разных аккаунтов и гостя.
- Меню, настройки, обучение, прогрессия культур и животных.
- Система престижа и улучшений.
- Готовый Windows-билд в релизах GitHub.

## Гостевой режим

На экране входа есть кнопка `Войти как гость`. В этом режиме игра не требует интернет и не обращается к Firebase для сохранения прогресса.

Гостевой прогресс сохраняется локально в отдельные JSON-файлы, например:

- `save_guest.json`
- `prestige_guest.json`
- `tutorial_hints_guest.json`

Обычные аккаунты Firebase используют свои отдельные файлы, поэтому прогресс гостя не смешивается с прогрессом зарегистрированных игроков.

## Firebase

Firebase используется для:

- регистрации и входа по email/password;
- хранения cloud-save;
- профиля игрока;
- таблиц прогресса.

Для standalone-сборки включен REST fallback, чтобы авторизация и база работали стабильнее на ПК.

## Как запустить готовую игру

1. Открой раздел `Releases` на GitHub.
2. Скачай архив `FarmSim-Windows-...zip`.
3. Распакуй архив в удобную папку.
4. Запусти `FarmSim.exe`.

## Как открыть проект в Unity

1. Установи Unity `6000.3.11f1`.
2. Открой папку проекта `FarmSim/FarmSim`.
3. Дождись импорта пакетов.
4. Запусти сцену `Assets/Scenes/AuthScene.unity`.

## Сборка Windows-релиза

В проект добавлен editor-метод для сборки:

```powershell
& 'C:\Program Files\Unity\Hub\Editor\6000.3.11f1\Editor\Unity.exe' `
  -batchmode -quit `
  -projectPath 'C:\Users\shp3k\Desktop\FarmSim\FarmSim\FarmSim' `
  -executeMethod FarmSimBuild.BuildWindowsRelease `
  -outputDir 'C:\Users\shp3k\Desktop\FarmSim\Release\FarmSim-Windows' `
  -logFile 'C:\Users\shp3k\Desktop\FarmSim\FarmSim\FarmSim\Logs\UnityBuildRelease.log'
```

После сборки папку `FarmSim-Windows` можно упаковать в zip и прикрепить к GitHub Release.

## Текущая версия

`v1.0.2`
