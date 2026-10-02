# RenderScaler

**Файл:** `Assets/Script/Core/Settings/RenderScaler.cs` (+ `RenderScaleBlit.cs`)

«Масштаб рендера» для встроенного конвейера: камера рисует в уменьшенную `RenderTexture`, вспомогательная камера `RenderScaleBlit` растягивает её на экран. UI Toolkit рисуется поверх в полном разрешении. При 100 % выключен.

[[CrosshairHud]] умеет рисовать прямо в эту текстуру.
