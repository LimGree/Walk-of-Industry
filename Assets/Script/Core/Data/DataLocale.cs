using System;
using System.Collections.Generic;

/// <summary>
/// Названия и описания зданий, предметов, рецептов и исследований на языке интерфейса ([[UiLocale]]).
/// Ключ — id ассета без учёта регистра и пробелов. Пустая строка в таблице — взять текст из ассета.
/// Рецепт без своей строки берёт имя продукта (id без «recipe_»).
/// Читать через <c>data.Title</c> / <c>data.Info</c>, а не displayName/description.
/// </summary>
public static class DataLocale
{
    struct Row
    {
        public string ru, en, ruDesc, enDesc;
        public Row(string ru, string en, string ruDesc, string enDesc)
        {
            this.ru = ru;
            this.en = en;
            this.ruDesc = ruDesc;
            this.enDesc = enDesc;
        }
    }

    static Dictionary<string, Row> buildings, items, research;

    public static string BuildingName(string id, string fallback) =>
        DecorCatalog.TryName(id, false, out string decor) ? decor : Pick(Buildings(), id, fallback, false);
    public static string BuildingDesc(string id, string fallback) =>
        DecorCatalog.TryName(id, true, out string decor) ? decor : Pick(Buildings(), id, fallback, true);
    public static string ItemName(string id, string fallback) => Pick(Items(), id, fallback, false);
    public static string ItemDesc(string id, string fallback) => Pick(Items(), id, fallback, true);
    public static string ResearchName(string id, string fallback) => Pick(Research(), id, fallback, false);
    public static string ResearchDesc(string id, string fallback) => Pick(Research(), id, fallback, true);

    public static string RecipeName(string id, string fallback)
    {
        string key = Key(id);
        if (key.StartsWith("recipe_"))
            key = key.Substring(7);
        return Pick(Items(), key, fallback, false);
    }

    static string Key(string id)
    {
        return (id ?? "").Trim().ToLowerInvariant();
    }

    static string Pick(Dictionary<string, Row> table, string id, string fallback, bool desc)
    {
        if (table.TryGetValue(Key(id), out Row row))
        {
            string v = UiLocale.IsRu ? (desc ? row.ruDesc : row.ru) : (desc ? row.enDesc : row.en);
            if (!string.IsNullOrEmpty(v))
                return v;
        }

        return fallback ?? "";
    }

    static Dictionary<string, Row> Table(params (string id, string ru, string en, string ruDesc, string enDesc)[] rows)
    {
        var d = new Dictionary<string, Row>(StringComparer.Ordinal);
        foreach (var r in rows)
            d[Key(r.id)] = new Row(r.ru, r.en, r.ruDesc, r.enDesc);
        return d;
    }

    static Dictionary<string, Row> Buildings()
    {
        return buildings ??= Table(
            ("assembler", "Сборщик", "Assembler", "Собирает детали по рецепту. Один вход, один выход.", "Crafts parts from a recipe. One input, one output."),
            ("chemical_plant", "Химзавод", "Chemical Plant", "", "Chemistry: fluid by pipe and solids by belt, output to a belt."),
            ("constructor", "Конструктор", "Constructor", "Цех 2×2: два входа, один выход. Выбери рецепт.", "2×2 workshop: two inputs, one output. Pick a recipe."),
            ("conveyor", "Конвейер", "Conveyor Belt", "Везёт предметы. Прямая, поворот, T и слияния собираются сами по соседям.", "Moves items. Straight, corner, T and merges form automatically."),
            ("drone_load_station", "", "Drone Loading Station", "", "5×5. Belts feed the back, the packer fills crates, drones fly them to an unloading station."),
            ("drone_unload_station", "", "Drone Unloading Station", "", "5×5. Drones land at the back, 1000-item storage, output to belts at the front."),
            ("extractor", "Экстрактор", "Extractor", "Добывает руду, камень, дерево или песок с жилы.", "Mines ore, stone, wood or sand from a node."),
            ("fluid_storage_tank", "", "Fluid Tank", "", "1×1 fluid storage. One type, pipes only."),
            ("oil_extractor", "", "Oil Pump", "", "Pumps crude oil. Well richness is rolled on placement."),
            ("pipe", "", "Pipe", "Везёт жидкости. Слияния собираются сами по соседям.", "Carries fluids. Merges form automatically."),
            ("pipe_splitter", "", "Pipe Splitter", "", "Fluid enters at the back and is split round-robin forward, right and left."),
            ("power_generator", "", "Generator", "", "Burns coal and speeds up machines and mining nearby."),
            ("refinery", "", "Refinery", "", "Crude oil by pipe → products to a belt (or the reverse, by recipe)."),
            ("research_lab", "Лаборатория", "Research Lab", "Принимает предметы для исследований и продаёт лишнее.", "Takes items for research and sells the rest."),
            ("robotic_arm", "", "Robotic Arm", "1×1. Берёт предмет спереди и кладёт назад.", "1×1. Takes an item from the front and places it behind."),
            ("smelter", "Печь", "Smelter", "Плавит руду в слитки.", "Smelts ore into ingots."),
            ("splitter", "Сплиттер", "Splitter", "Один вход, три выхода, делит по кругу.", "One input, three outputs, round-robin."),
            ("storage_container", "", "Storage", "", "1×1 storage. One item type, two stacks."),
            ("underground_conveyor", "", "Underground Belt", "Вход и выход по прямой, до 5–9 клеток между ними.", "Entrance and exit in a line, 5–9 cells apart."),
            ("water_extractor", "", "Water Pump", "Качает воду из озера или океана. Выход — жидкость.", "Pumps water from a lake or ocean. Outputs fluid.")
        );
    }

    static Dictionary<string, Row> Items()
    {
        return items ??= Table(
            ("cooper_ingot", "Медный слиток", "Copper Ingot", "Выплавлен из медной руды.", "Smelted from copper ore."),
            ("cooper_ore", "Медная руда", "Copper Ore", "Сырая медная руда.", "Raw copper ore."),
            ("iron_ingot", "Железный слиток", "Iron Ingot", "Выплавлен из железной руды.", "Smelted from iron ore."),
            ("advanced_circuit", "Продвинутая схема", "Advanced Circuit", "", "Advanced component."),
            ("ai_module", "", "AI Module", "", "Endgame component."),
            ("battery", "", "Battery", "", "Advanced component. Chemical plant."),
            ("cable", "", "Cable", "", "Advanced component."),
            ("circuit_board", "Плата", "Circuit Board", "Основа электроники.", "The base of electronics."),
            ("coal_ore", "Уголь", "Coal", "Топливо и сырьё для стали.", "Fuel and a steel ingredient."),
            ("computer_chip", "", "Computer Chip", "", "Advanced component."),
            ("cooper_plate", "Медная пластина", "Copper Plate", "Из медных слитков.", "Made from copper ingots."),
            ("crude_oil", "", "Crude Oil", "", "Raw oil. Pipes only."),
            ("gear", "Шестерня", "Gear", "Базовая деталь. Ею же прокачиваются ленты.", "A basic part. Also used to upgrade belts."),
            ("glass", "Стекло", "Glass", "Из песка в печи.", "Made from sand in the smelter."),
            ("iron_ore", "Железная руда", "Iron Ore", "Сырая железная руда.", "Raw iron ore."),
            ("iron_plate", "Железная пластина", "Iron Plate", "Из железных слитков.", "Made from iron ingots."),
            ("iron_rod", "Железный стержень", "Iron Rod", "Из железного слитка.", "Made from an iron ingot."),
            ("log", "Бревно", "Log", "Древесина с деревьев.", "Wood from trees."),
            ("motor", "", "Motor", "", "Gears, steel rods and wire."),
            ("nano_wire", "", "Nanowire", "", "Endgame component."),
            ("plank", "Доска", "Plank", "Из брёвен.", "Made from logs."),
            ("plastic", "", "Plastic", "", "Refined oil."),
            ("quantum_core", "", "Quantum Core", "", "Endgame component."),
            ("rubber", "", "Rubber", "", "A material made from oil."),
            ("sand", "Песок", "Sand", "Для стекла и кремния.", "For glass and silicon."),
            ("silicon", "Кремний", "Silicon", "Из песка в печи.", "Made from sand in the smelter."),
            ("steel_beam", "", "Steel Beam", "", "A large steel profile."),
            ("steel_ingot", "", "Steel Ingot", "", "Iron and coal."),
            ("steel_rod", "", "Steel Rod", "", "Made from a steel ingot."),
            ("stone", "Камень", "Stone", "Для каменных блоков.", "For stone bricks."),
            ("stone_brick", "Каменный блок", "Stone Brick", "Из камня.", "Made from stone."),
            ("sulfur", "", "Sulfur", "", "Mined in the mountains by an extractor."),
            ("sulfuric_acid", "", "Sulfuric Acid", "", "Acid from sulfur. Fluid, pipes only."),
            ("water", "", "Water", "", "Fluid. Pipes only."),
            ("wire", "Провод", "Wire", "Из медного слитка.", "Made from a copper ingot.")
        );
    }

    static Dictionary<string, Row> Research()
    {
        return research ??= Table(
            ("research_basic_automation", "Базовая автоматизация", "Basic Automation", "Открывает сборщик и печь.", "Unlocks the assembler and smelter."),
            ("research_advanced_automation", "", "Advanced Automation", "", "Robotic arm: moves items between neighbours."),
            ("research_advanced_circuit", "", "Advanced Circuit", "", "Circuit in the constructor."),
            ("research_ai_module", "", "AI Module", "", "The final item."),
            ("research_ai_systems", "", "AI Systems", "", "Endgame: nanowire, quantum core and AI module."),
            ("research_assembler", "", "Assembler", "", "Unlocks the assembler."),
            ("research_assembler_2", "", "Assembler II", "Апгрейд сборщика: быстрее крафт.", "Assembler upgrade: faster crafting."),
            ("research_battery", "", "Battery", "", "Battery at the chemical plant."),
            ("research_cable", "", "Cable", "", "Cable in the constructor."),
            ("research_chemical_plant", "", "Chemical Plant", "", "Fluids and solids in one recipe."),
            ("research_circuit_board", "", "Circuit Board", "", "Circuit board in the constructor."),
            ("research_computer_chip", "", "Computer Chip", "", "Chip in the constructor."),
            ("research_computing", "", "Computing", "", "Chemical plant: fluids and solids in advanced recipes."),
            ("research_constructor", "", "Constructor", "", "A machine with two inputs."),
            ("research_cooper_ingot", "", "Copper Ingot", "", "Smelting copper ore."),
            ("research_cooper_plate", "", "Copper Plate", "", "Plates from copper ingots."),
            ("research_drone_cargo_1", "", "Cargo 100", "", "Drone crates hold 100 items instead of 50."),
            ("research_drone_cargo_2", "", "Cargo 150", "", "Drone crates hold 150 items."),
            ("research_drone_slots_2", "", "Hangar: 2 drones", "", "A second drone at the loading station (bought with rubies)."),
            ("research_drone_slots_3", "", "Hangar: 3 drones", "", "A third drone at the loading station (bought with rubies)."),
            ("research_drone_slots_4", "", "Hangar: 4 drones", "", "A fourth drone — all pads in use (bought with rubies)."),
            ("research_drone_speed_1", "", "Drone Speed I", "", "Drones fly 35% faster."),
            ("research_drone_speed_2", "", "Drone Speed II", "", "Drones fly 80% faster than base."),
            ("research_drones", "", "Drones", "", "Drone loading and unloading stations. One drone included, it flies in a straight line."),
            ("research_electronics", "", "Electronics", "Подготовка к электронике.", "Groundwork for electronics."),
            ("research_extractor_2", "", "Extractor II", "Апгрейд экстрактора: быстрее добыча.", "Extractor upgrade: faster mining."),
            ("research_fluid_tank", "", "Fluid Tank", "", "Fluid storage."),
            ("research_gear", "", "Gear", "", "Gears from rods."),
            ("research_glass", "", "Glass", "", "Glass from sand in the smelter."),
            ("research_iron_ingot", "", "Iron Ingot", "", "Smelting iron ore."),
            ("research_iron_plate", "", "Iron Plate", "", "Plates from ingots."),
            ("research_iron_rod", "", "Iron Rod", "", "Rods from an ingot."),
            ("research_mechanical_engineering", "Механика", "Mechanical Engineering", "Открывает базовые детали.", "Unlocks basic components."),
            ("research_motor", "", "Motor", "", "Motor in the constructor."),
            ("research_nano_wire", "", "Nanowire", "", "Nanowire in the constructor."),
            ("research_oil_extractor", "", "Oil Pump", "", "Oil extraction."),
            ("research_petrochemistry", "", "Petrochemistry", "", "Oil pump, water pump, fluid tank, pipes and refinery."),
            ("research_pipe", "", "Pipe", "", "Fluid logistics."),
            ("research_pipe_splitter", "", "Pipe Splitter", "", "Splits a fluid stream into three pipes."),
            ("research_plank", "", "Plank", "", "Planks from logs."),
            ("research_plastic", "", "Plastic", "", "Plastic from oil."),
            ("research_power_generator", "", "Generator", "", "Burns coal, speeds up nearby machines and mining."),
            ("research_quantum_core", "", "Quantum Core", "", "Core in the constructor."),
            ("research_refinery", "", "Refinery", "", "Oil refining."),
            ("research_reliability_1", "", "Reliability I", "", "Fewer night breakdowns: up to 3.5% of buildings per day instead of 5%."),
            ("research_reliability_2", "", "Reliability II", "", "Even fewer night breakdowns: up to 2% of buildings per day."),
            ("research_robotic_arm", "", "Robotic Arm", "", "Automatic loading of buildings."),
            ("research_rubber", "", "Rubber", "", "Rubber from oil."),
            ("research_silicon", "", "Silicon", "", "Silicon from sand in the smelter."),
            ("research_smelter", "", "Smelter", "Открывает печь.", "Unlocks the smelter."),
            ("research_splitter", "Сплиттер", "Splitter", "", "Splitting belts."),
            ("research_steel_beam", "", "Steel Beam", "", "Beam in the assembler."),
            ("research_steel_ingot", "", "Steel Ingot", "", "Steel in the smelter."),
            ("research_steel_rod", "", "Steel Rod", "", "Rod in the constructor."),
            ("research_stone_brick", "", "Stone Brick", "", "Bricks from stone."),
            ("research_storage", "", "Storage", "", "Item storage."),
            ("research_sulfuric_acid", "", "Sulfuric Acid", "", "Acid from sulfur, no water needed."),
            ("research_underground_conveyor", "", "Underground Belt", "", "Entrance and exit in a line, up to 5 cells apart."),
            ("research_underground_range_2", "", "Longer Underground", "", "Underground belt gap up to 7 cells."),
            ("research_underground_range_3", "Подземка дальняя", "Long Underground", "", "Underground belt gap up to 9 cells."),
            ("research_water_extractor", "", "Water Pump", "", "Water extraction."),
            ("research_wire", "", "Wire", "", "Wire from copper ingots.")
        );
    }
}
