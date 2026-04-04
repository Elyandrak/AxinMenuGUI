// AxinMenuGUI — Data/Models
// Archivo: MenuModels.cs
// Responsabilidad: clases de modelo que representan la estructura JSON de un menú.
// Solo datos. Sin lógica. Sin referencias a la API del juego.
// Patrón 5 AXIN: todos los campos opcionales tienen valor por defecto.

using System.Collections.Generic;
using Newtonsoft.Json;

namespace AxinMenuGUI
{
    // ═══════════════════════════════════════════════════════════
    // MENÚ RAÍZ
    // ═══════════════════════════════════════════════════════════

    public class MenuDefinition
    {
        [JsonProperty("id")]
        public string Id { get; set; } = "";

        [JsonProperty("title")]
        public string Title { get; set; } = "Menú";

        [JsonProperty("rows")]
        public int Rows { get; set; } = 3;

        [JsonProperty("commandAlias")]
        public string CommandAlias { get; set; } = "";

        [JsonProperty("commandAliasTarget")]
        public string CommandAliasTarget { get; set; } = "OPTIONAL";

        [JsonProperty("permission")]
        public string Permission { get; set; } = "";

        [JsonProperty("scenes")]
        public Dictionary<string, SceneDefinition> Scenes { get; set; } = new();

        [JsonProperty("openTriggers")]
        public List<OpenTriggerDefinition> OpenTriggers { get; set; } = new();

        /// <summary>
        /// Tema visual del menú. Valores válidos: default, dark-red, dark-blue,
        /// dark-green, parchment, stone, night.
        /// Si está vacío o ausente, usa "default" (aspecto estándar de VS).
        /// </summary>
        [JsonProperty("theme")]
        public string Theme { get; set; } = "default";
    }

    // ═══════════════════════════════════════════════════════════
    // ESCENA
    // ═══════════════════════════════════════════════════════════

    public class SceneDefinition
    {
        [JsonProperty("delay")]
        public int DelayMs { get; set; } = 0;

        /// <summary>
        /// Tema visual de esta escena. Sobreescribe el theme del menú.
        /// Si está vacío o ausente, hereda el theme del menú.
        /// </summary>
        [JsonProperty("theme")]
        public string Theme { get; set; } = "";

        [JsonProperty("items")]
        public Dictionary<string, ItemDefinition> Items { get; set; } = new();
    }

    // ═══════════════════════════════════════════════════════════
    // ÍTEM (botón en el grid)
    // ═══════════════════════════════════════════════════════════

    public class ItemDefinition
    {
        [JsonProperty("slot")]
        public int Slot { get; set; } = 0;

        [JsonProperty("item")]
        public string ItemCode { get; set; } = "game:stone";

        [JsonProperty("amount")]
        public int Amount { get; set; } = 1;

        [JsonProperty("name")]
        public string Name { get; set; } = "";

        [JsonProperty("lore")]
        public List<string> Lore { get; set; } = new();

        [JsonProperty("clickEvents")]
        public Dictionary<string, ClickEventDefinition> ClickEvents { get; set; } = new();

        [JsonProperty("conditions")]
        public Dictionary<string, ConditionDefinition> Conditions { get; set; } = new();

        /// <summary>
        /// Si true: el botón no se envía al cliente si falla alguna condición.
        /// Si false (default): el botón se muestra pero el clic queda bloqueado con conditionFailMessage.
        /// </summary>
        [JsonProperty("hideOnFail")]
        public bool HideOnFail { get; set; } = false;

        [JsonProperty("conditionFailMessage")]
        public string ConditionFailMessage { get; set; } = "";

        /// <summary>
        /// Prioridad para slot groups (varios ítems en el mismo slot).
        /// Menor número = evaluado primero. Default 0.
        /// </summary>
        [JsonProperty("priority")]
        public int Priority { get; set; } = 0;

        /// <summary>
        /// Número máximo de veces que este botón puede usarse por jugador. 0 = infinito.
        /// El check y el incremento ocurren una vez por clic, antes de ejecutar los clickEvents.
        /// </summary>
        [JsonProperty("maxUses")]
        public int MaxUses { get; set; } = 0;

        /// <summary>Mensaje al jugador cuando se alcanza el límite. Vacío = mensaje genérico.</summary>
        [JsonProperty("maxUsesMessage")]
        public string MaxUsesMessage { get; set; } = "";
    }

    // ═══════════════════════════════════════════════════════════
    // ÍTEM DE INTERCAMBIO (buyItem / sellItem)
    // ═══════════════════════════════════════════════════════════

    public class ItemStackDefinition
    {
        [JsonProperty("item")]
        public string Item { get; set; } = "";

        [JsonProperty("amount")]
        public int Amount { get; set; } = 1;
    }

    public class ExchangeLimits
    {
        public class PerPlayerLimits
        {
            /// <summary>Máximo de veces que el jugador puede hacer esta acción en total. 0 = sin límite.</summary>
            [JsonProperty("maxTotal")]
            public int MaxTotal { get; set; } = 0;

            /// <summary>Cooldown entre usos. Formatos: 30m, 12h, 7d, 1mo</summary>
            [JsonProperty("cooldown")]
            public string Cooldown { get; set; } = "";
        }

        public class GlobalLimits
        {
            /// <summary>Stock global total. 0 = sin límite.</summary>
            [JsonProperty("maxTotal")]
            public int MaxTotal { get; set; } = 0;
        }

        [JsonProperty("perPlayer")]
        public PerPlayerLimits PerPlayer { get; set; } = new();

        [JsonProperty("global")]
        public GlobalLimits Global { get; set; } = new();
    }

    // ═══════════════════════════════════════════════════════════
    // CLICK EVENT
    // ═══════════════════════════════════════════════════════════

    public class ClickEventDefinition
    {
        [JsonProperty("type")]
        public string Type { get; set; } = "";

        [JsonProperty("clickType")]
        public string ClickType { get; set; } = "ANY";

        [JsonProperty("delay")]
        public int DelayMs { get; set; } = 0;

        // ── message ─────────────────────────────────────────────
        [JsonProperty("message")]
        public string Message { get; set; } = "";

        // ── consoleCommand / playerCommand ───────────────────────
        [JsonProperty("commands")]
        public List<string> Commands { get; set; } = new();

        // ── giveItem / takeItem ──────────────────────────────────
        [JsonProperty("item")]
        public string ItemCode { get; set; } = "";

        [JsonProperty("amount")]
        public int Amount { get; set; } = 1;

        // ── buyItem / sellItem ───────────────────────────────────
        [JsonProperty("cost")]
        public List<ItemStackDefinition> Cost { get; set; } = new();

        [JsonProperty("give")]
        public List<ItemStackDefinition> Give { get; set; } = new();

        [JsonProperty("consume")]
        public bool Consume { get; set; } = true;

        [JsonProperty("limits")]
        public ExchangeLimits Limits { get; set; } = new();

        [JsonProperty("conditionFailMessage")]
        public string ConditionFailMessage { get; set; } = "";

        // ── teleport ─────────────────────────────────────────────
        // { "type": "teleport", "location": "x,y,z" }
        [JsonProperty("location")]
        public string Location { get; set; } = "";

        // ── teleportSaved ─────────────────────────────────────────
        // { "type": "teleportSaved", "target": "nombre_punto" }
        [JsonProperty("target")]
        public string Target { get; set; } = "";

        // ── randomTeleport ────────────────────────────────────────
        // { "type": "randomTeleport", "radiusMin": 5000, "radiusMax": 10000 }
        // 0 = usar defaults de config.json → randomTeleport.defaultMin/Max
        [JsonProperty("radiusMin")]
        public int RadiusMin { get; set; } = 0;

        [JsonProperty("radiusMax")]
        public int RadiusMax { get; set; } = 0;

        // ── openGui ──────────────────────────────────────────────
        [JsonProperty("guiId")]
        public string GuiId { get; set; } = "";

        // ── setVariable ──────────────────────────────────────────
        [JsonProperty("variable")]
        public string Variable { get; set; } = "";

        [JsonProperty("value")]
        public string Value { get; set; } = "";

        [JsonProperty("operation")]
        public string Operation { get; set; } = "set"; // set | add | subtract | multiply | divide
    }

    // ═══════════════════════════════════════════════════════════
    // CONDICIÓN
    // ═══════════════════════════════════════════════════════════

    public class ConditionDefinition
    {
        [JsonProperty("type")]
        public string Type { get; set; } = "";

        [JsonProperty("inverted")]
        public bool Inverted { get; set; } = false;

        // ── hasPrivilege ─────────────────────────────────────────
        [JsonProperty("privilege")]
        public string Privilege { get; set; } = "";

        // ── hasRole ──────────────────────────────────────────────
        [JsonProperty("roleCode")]
        public string RoleCode { get; set; } = "";

        // ── hasPrivilegeLevel ────────────────────────────────────
        [JsonProperty("minLevel")]
        public int MinLevel { get; set; } = 0;

        // ── hasItem ──────────────────────────────────────────────
        [JsonProperty("item")]
        public string ItemCode { get; set; } = "";

        [JsonProperty("amount")]
        public int Amount { get; set; } = 1;

        // ── playerDataCompare ────────────────────────────────────
        [JsonProperty("field")]
        public string Field { get; set; } = "";

        [JsonProperty("operator")]
        public string Operator { get; set; } = "eq"; // eq | neq | gt | gte | lt | lte

        [JsonProperty("compareValue")]
        public string CompareValue { get; set; } = "";

        // ── cooldownActive ───────────────────────────────────────
        [JsonProperty("cooldownKey")]
        public string CooldownKey { get; set; } = "";
    }

    // ═══════════════════════════════════════════════════════════
    // TRIGGER DE APERTURA
    // ═══════════════════════════════════════════════════════════

    public class OpenTriggerDefinition
    {
        [JsonProperty("type")]
        public string Type { get; set; } = "";

        [JsonProperty("itemCode")]
        public string ItemCode { get; set; } = "";

        [JsonProperty("hotkey")]
        public string Hotkey { get; set; } = "";

        [JsonProperty("claimFlag")]
        public string ClaimFlag { get; set; } = "";
    }
}
