# PostFx

**Файл:** `Assets/Script/Core/Settings/PostFx.cs`

Постобработка на пакете **Post Processing v2** (`com.unity.postprocessing` в `Packages/manifest.json`). `PostProcessLayer` вешается на `Camera.main` из кода, глобальный volume `PostFxVolume` живёт между сценами.

Настройки: сглаживание FXAA/SMAA (MSAA ×2/×4 — через `QualitySettings.antiAliasing`), Bloom, виньетка, размытие в движении, гамма (Color Grading, LDR). Всё выключено → слой отключён.

Ресурсы пакета берутся из `Resources/PostFxResources.asset` ([[PostFxResources]] ссылается на `PostProcessResources` пакета по GUID) — иначе в билде слой не заработает.
