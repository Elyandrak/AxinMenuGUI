// AxinMenuGUI — Features/Teleport
// Archivo: AdminTeleportModels.cs
// Responsabilidad: modelos de datos para el sistema de TP guardados por admin.
// Solo datos. Sin lógica. Sin referencias a la API del juego.

using System.Collections.Generic;
using Newtonsoft.Json;

namespace AxinMenuGUI
{
    /// <summary>Un punto de teletransporte guardado por un admin.</summary>
    public class AdminTeleportPoint
    {
        [JsonProperty("name")]
        public string Name { get; set; } = "";

        [JsonProperty("x")]
        public double X { get; set; }

        [JsonProperty("y")]
        public double Y { get; set; }

        [JsonProperty("z")]
        public double Z { get; set; }

        [JsonProperty("createdBy")]
        public string CreatedBy { get; set; } = "";

        [JsonProperty("createdAtUtc")]
        public string CreatedAtUtc { get; set; } = "";
    }

    /// <summary>Raíz del fichero adminteleports.json.</summary>
    public class AdminTeleportStoreData
    {
        [JsonProperty("version")]
        public string Version { get; set; } = "1.0.0";

        [JsonProperty("points")]
        public List<AdminTeleportPoint> Points { get; set; } = new();
    }
}
