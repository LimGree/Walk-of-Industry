# ResearchLab

**Файл:** `Assets/Script/Core/Buildings/ResearchLab.cs`  
**Предок:** [[BuildingBase]] + [[IInteractable]]

Лаборатория. Принимает предметы с ленты и отдаёт их в [[ResearchSystem.SubmitItem]]:

1. если идёт исследование и предмет нужен — засчитывается в прогресс **и** продаётся за [[Economy.SellValue]];
2. иначе если это шестерёнка — копится в [[BeltSpeedSystem]] (без монет);
3. иначе продаётся за монеты [[Economy.SellValue]].

Открывает [[MachineUI]] с вкладками: исследования ([[ResearchTree]]), рецепты, дерево лент, статистика. То же окно — клавиша T ([[ResearchUI]]).

Сама лаборатория дорогая (`buildCost` в данных). Без неё экономика и исследования не крутятся.
