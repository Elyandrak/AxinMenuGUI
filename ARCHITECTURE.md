# AxinMenuGUI — ARCHITECTURE.md
Versión: 0.1.0 | AXIN M=4

## Qué hace este mod
Sistema de menús GUI 100% configurables por JSON para servidores VintageStory.
Los administradores crean menús en `ModConfig/AxinMenuGUI/menus/*.json` sin escribir código.
Inspirado en GUIPlus (Minecraft/Spigot).

## Estructura de carpetas

```
src/AxinMenuGUI/
├── Core/
│   └── AxinMenuGuiMod.cs       ← punto de entrada, coordina subsistemas
├── Data/
│   └── MenuModels.cs           ← clases de modelo JSON (solo datos)
├── Features/
│   ├── Commands/
│   │   └── CommandHandler.cs   ← /amenu y subcomandos
│   ├── Engine/
│   │   └── MenuEngine.cs       ← evalúa condiciones + ejecuta click events
│   ├── PlayerData/
│   │   └── PlayerDataStore.cs  ← persistencia de variables por jugador
│   ├── Registry/
│   │   └── MenuRegistry.cs     ← carga y acceso a menús JSON
│   └── Tokens/
│       └── PlaceholderResolver.cs ← resuelve {player}, {var:campo}, etc.
└── UI/
    └── GuiMenuManager.cs       ← gestión del GUI en el cliente (stub hasta Bloque 1.3)
```

## Evento → Handler

| Evento VS              | Handler                          |
|------------------------|----------------------------------|
| StartServerSide        | AxinMenuGuiMod → init subsistemas |
| ModsAndConfigReady     | CommandHandler → RegisterCommands |
| StartClientSide        | AxinMenuGuiMod → GuiMenuManager   |

## Dependencias one-way

```
Core → Features/* → Data
UI   → Features/Engine
Features/Engine → Features/Registry, Features/PlayerData, Features/Tokens
```

## Zonas NO TOCAR sin checkpoint previo
- `MenuModels.cs` — cambiar nombres de campos JSON rompe deserialización de configs existentes
- `CommandHandler.RegisterCommands()` — cambiar el árbol de comandos requiere prueba de RUN

## Cómo hacer cambios seguros
1. Identificar el fichero responsable (ver tabla arriba)
2. Cambio mínimo — un solo objetivo
3. Compilar → 0 errores
4. RUN y verificar
5. Commit + Push

## Estado por bloque (Fase 0 / Bloque 0.2)

| Fichero                  | Estado           |
|--------------------------|------------------|
| modinfo.json             | ✅ Listo         |
| MenuModels.cs            | ✅ Listo         |
| MenuRegistry.cs          | ✅ Listo         |
| PlayerDataStore.cs       | ✅ Listo         |
| PlaceholderResolver.cs   | ✅ Listo         |
| MenuEngine.cs            | ✅ Listo (stub parcial — giveItem/takeItem/teleport pendientes) |
| CommandHandler.cs        | ✅ Listo         |
| GuiMenuManager.cs        | 🔲 STUB — Bloque 1.3 pendiente |
