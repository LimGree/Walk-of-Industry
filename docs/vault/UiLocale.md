# UiLocale

**Файл:** `Assets/Script/Core/UI/UiLocale.cs`

Словарь русский/английский. `UiLocale.T("pause.title")` → «Пауза» или «Paused».

Язык в PlayerPrefs `UiLanguage`. Смена шлёт событие `Changed` — меню перестраиваются.

Все новые надписи нужно добавлять в таблицу внизу файла, иначе на экране вылезет сам ключ.
