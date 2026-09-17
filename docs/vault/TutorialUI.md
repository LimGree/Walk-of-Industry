# TutorialUI

**Файл:** `Assets/Script/Core/Tutorial/TutorialUI.cs`

Панель шага Welcome и Farewell, цель в углу. Кнопка «Пропустить» на модалке — всё обучение. Во время ходьбы курсор залочен: **F1** — всё, **F2** — этот шаг. Подсвечивает карточки/кнопки классом `tut-glow` (`Res_<id>` на узлах [[ResearchTree]]).

На модалке курсор свободен, пауза не открывается ([[GameManager.BlocksPause]] через [[TutorialSystem]]).

Связи: [[TutorialSystem]] · [[InventoryUI]] · [[InputHintUI]] · [[UiLocale]] · [[MachineUI]]
