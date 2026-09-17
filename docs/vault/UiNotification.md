# UiNotification

**Файл:** `Assets/Script/Core/UI/UiNotification.cs`

Всплывающие тосты. Хост по умолчанию — под балансом [[WalletHud]]; если HUD ещё нет — правый нижний угол.

Исследование зовёт `Push` из [[ResearchSystem.CompleteResearch]] (только живое завершение, не загрузка сейва). Звук — `UiAudio.PlayNotify`.
