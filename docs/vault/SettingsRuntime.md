# SettingsRuntime

**Файл:** `Assets/Script/Core/Settings/SettingsRuntime.cs`

Живёт всю игру (создаётся сам, `DontDestroyOnLoad`). На каждое изменение [[GameSettings]] и смену `Camera.main` применяет:

- [[PostFx]] и [[RenderScaler]] на камеру;
- [[UiLook]] (акцент, анимации, контраст, дальтонизм, прозрачность HUD);
- стрелки [[SocketArrow]];
- фокус окна: FPS в фоне, «Звук в фоне» (`AudioMuted`), «Пауза при сворачивании»;
- выбор монитора (`Screen.MoveMainWindowTo`).

Рисует счётчик FPS и хост [[SoundCaptions]].
