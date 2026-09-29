# PipeSplitter

**Файл:** `Assets/Script/Core/Buildings/PipeSplitter.cs`, префаб `prefabs/Builders/PipeSplitter.prefab`, данные `ScriptableObjects/Builders/PipeSplitter.asset` (`pipe_splitter`), модель `Resources/Models/pipe_splitter.obj`.

Трубный сплиттер 1×1. Жидкость входит **сзади** (−Z) из [[Pipe]] и выходит по кругу **вперёд, вправо, влево**. Занятый или пустой выход пропускается. Принимают трубы и здания, которые берут жидкость (бак, НПЗ, химзавод), а также другой сплиттер. Буфер 6, до 24 ед./с. Буфер пишется в сейв (`storage`).

Исследование `research_pipe_splitter` идёт после «Трубы»: пластины 250 + шестерёнки 100.
