# TutorialSystem

**Файл:** `Assets/Script/Core/Tutorial/TutorialSystem.cs`

Мозг обучения. Шаги из [[Обучение]]. Вешается на [[GameManager]].

Новый мир (нет `save.json`) → шаг 0, хотбар пустой. Старые сейвы v8 и ниже считаются пройденными. Пропуск и финал пишутся в сейв мира, не в PlayerPrefs.

**F1** — пропустить всё. **F2** — этот шаг. **F3** — повторить шаг. В HUD: повтор / до лаборатории / до лент. `Restart` не ломается.

Первая глава дерева в обучении: `research_smelter`, затем отдельно `research_iron_ingot` и `research_cooper_ingot`.

Пока идёт обучение, [[PlayerInventory]] не кладёт новые здания на хотбар сам.

Связи: [[TutorialUI]] · [[TutorialFx]] · [[SaveSystem]] · [[ResearchSystem]] · [[BuildSelectionController]]
