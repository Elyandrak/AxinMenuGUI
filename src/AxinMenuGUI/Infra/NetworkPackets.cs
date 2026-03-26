// AxinMenuGUI — Infra/Network
// Archivo: NetworkPackets.cs
// Responsabilidad: paquetes de red servidor ↔ cliente.
//
// NORMA N: VS usa Protobuf. Todos los tipos de paquete DEBEN tener
// [ProtoContract] y cada campo [ProtoMember(N)]. Sin esto → crash en SendPacket.
// Dictionary<K,V> no soportado — usar listas con clave explícita.

using System.Collections.Generic;
using ProtoBuf;

namespace AxinMenuGUI
{
    [ProtoContract]
    public class NetItem
    {
        [ProtoMember(1)] public string Key       { get; set; } = "";
        [ProtoMember(2)] public int    Slot      { get; set; }
        [ProtoMember(3)] public string Name      { get; set; } = "";
        [ProtoMember(4)] public string Lore      { get; set; } = "";
        [ProtoMember(5)] public string ItemCode  { get; set; } = "";
        [ProtoMember(6)] public int    Amount    { get; set; } = 1;
        [ProtoMember(7)] public string ConditionFailMessage { get; set; } = "";
    }

    [ProtoContract]
    public class NetScene
    {
        [ProtoMember(1)] public string        Key   { get; set; } = "";
        [ProtoMember(2)] public List<NetItem> Items { get; set; } = new();
    }

    [ProtoContract]
    public class NetMenu
    {
        [ProtoMember(1)] public string         Id     { get; set; } = "";
        [ProtoMember(2)] public string         Title  { get; set; } = "";
        [ProtoMember(3)] public int            Rows   { get; set; } = 3;
        [ProtoMember(4)] public List<NetScene> Scenes { get; set; } = new();
    }

    [ProtoContract]
    public class MenuOpenPacket
    {
        [ProtoMember(1)] public NetMenu Menu  { get; set; } = new();
        [ProtoMember(2)] public int     Scene { get; set; } = 0;
    }

    [ProtoContract]
    public class MenuClosePacket { }

    [ProtoContract]
    public class SlotClickPacket
    {
        [ProtoMember(1)] public string MenuId    { get; set; } = "";
        [ProtoMember(2)] public int    Scene     { get; set; } = 0;
        [ProtoMember(3)] public int    SlotIndex { get; set; } = 0;
    }

    // S→C: el servidor pide al cliente que ejecute un comando como si lo escribiera el jugador
    [ProtoContract]
    public class ExecuteCommandPacket
    {
        [ProtoMember(1)] public string Command { get; set; } = "";
    }

    public static class NetMenuMapper
    {
        public static NetMenu ToNet(MenuDefinition menu)
        {
            var net = new NetMenu
            {
                Id    = menu.Id,
                Title = menu.Title,
                Rows  = menu.Rows
            };

            foreach (var (sceneKey, scene) in menu.Scenes)
            {
                var netScene = new NetScene { Key = sceneKey };
                foreach (var (itemKey, item) in scene.Items)
                {
                    netScene.Items.Add(new NetItem
                    {
                        Key      = itemKey,
                        Slot     = item.Slot,
                        Name     = item.Name,
                        Lore     = string.Join("\n", item.Lore),
                        ItemCode = item.ItemCode,
                        Amount   = item.Amount,
                        ConditionFailMessage = item.ConditionFailMessage
                    });
                }
                net.Scenes.Add(netScene);
            }

            return net;
        }
    }
}
