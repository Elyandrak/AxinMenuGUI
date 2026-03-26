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

        /// <summary>OPTIONAL | REQUIRED | DISABLED</summary>
        [JsonProperty("commandAliasTarget")]
        public string CommandAliasTarget { get; set; } = "OPTIONAL";

        /// <summary>Permiso necesario para abrir este menú. Vacío = todos.</summary>
        [JsonProperty("permission")]
        public string Permission { get; set; } = "";

        [JsonProperty("scenes")]
        public Dictionary<string, SceneDefinition> Scenes { get; set; } = new();

        [JsonProperty("openTriggers")]
        public List<OpenTriggerDefinition> OpenTriggers { get; set; } = new();
    }

    // ═══════════════════════════════════════════════════════════
    // ESCENA (página dentro de un menú)
    // ═══════════════════════════════════════════════════════════

    public class SceneDefinition
    {
        [JsonProperty("delay")]
        public int DelayMs { get; set; } = 0;

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

        [JsonProperty("conditionFailMessage")]
        public string ConditionFailMessage { get; set; } = "";
    }

    // ═══════════════════════════════════════════════════════════
    // CLICK EVENT
    // ═══════════════════════════════════════════════════════════

    public class ClickEventDefinition
    {
        /// <summary>
        /// Tipos v1: message | consoleCommand | playerCommand |
        ///           giveItem | takeItem | teleport |
        ///           closeGui | openGui | nextScene | previousScene | back
        /// </summary>
        [JsonProperty("type")]
        public string Type { get; set; } = "";

        /// <summary>ANY | LEFT | RIGHT</summary>
        [JsonProperty("clickType")]
        public string ClickType { get; set; } = "ANY";

        [JsonProperty("delay")]
        public int DelayMs { get; set; } = 0;

        // ── Campos por tipo ──────────────────────────────────────

        /// <summary>[message] Texto a enviar al jugador.</summary>
        [JsonProperty("message")]
        public string Message { get; set; } = "";

        /// <summary>[consoleCommand | playerCommand] Lista de comandos.</summary>
        [JsonProperty("commands")]
        public List<string> Commands { get; set; } = new();

        /// <summary>[giveItem | takeItem] Código de ítem VS.</summary>
        [JsonProperty("item")]
        public string ItemCode { get; set; } = "";

        /// <summary>[giveItem | takeItem] Cantidad.</summary>
        [JsonProperty("amount")]
        public int Amount { get; set; } = 1;

        /// <summary>[teleport] Formato: "x,y,z" o "x,y,z,yaw,pitch"</summary>
        [JsonProperty("location")]
        public string Location { get; set; } = "";

        /// <summary>[openGui] ID del menú destino.</summary>
        [JsonProperty("guiId")]
        public string GuiId { get; set; } = "";
    }

    // ═══════════════════════════════════════════════════════════
    // CONDICIÓN
    // ═══════════════════════════════════════════════════════════

    public class ConditionDefinition
    {
        /// <summary>
        /// Tipos v1: hasPrivilege | hasItem
        /// Tipos v2: playerDataCompare | cooldown | isGameMode
        /// </summary>
        [JsonProperty("type")]
        public string Type { get; set; } = "";

        /// <summary>Si true, la condición se evalúa al revés.</summary>
        [JsonProperty("inverted")]
        public bool Inverted { get; set; } = false;

        // ── Campos por tipo ──────────────────────────────────────

        /// <summary>[hasPrivilege] Nombre del privilegio VS.</summary>
        [JsonProperty("privilege")]
        public string Privilege { get; set; } = "";

        /// <summary>[hasItem] Código de ítem VS.</summary>
        [JsonProperty("item")]
        public string ItemCode { get; set; } = "";

        /// <summary>[hasItem] Cantidad mínima requerida.</summary>
        [JsonProperty("amount")]
        public int Amount { get; set; } = 1;
    }

    // ═══════════════════════════════════════════════════════════
    // TRIGGER DE APERTURA
    // ═══════════════════════════════════════════════════════════

    public class OpenTriggerDefinition
    {
        /// <summary>command | item | onJoin | hotkey | claimZone</summary>
        [JsonProperty("type")]
        public string Type { get; set; } = "";

        /// <summary>[item] Código del ítem que abre el menú al hacer clic derecho.</summary>
        [JsonProperty("itemCode")]
        public string ItemCode { get; set; } = "";

        /// <summary>[hotkey] Tecla de apertura (ClientSide).</summary>
        [JsonProperty("hotkey")]
        public string Hotkey { get; set; } = "";

        /// <summary>[claimZone] Flag del claim que dispara la apertura.</summary>
        [JsonProperty("claimFlag")]
        public string ClaimFlag { get; set; } = "";
    }
}
