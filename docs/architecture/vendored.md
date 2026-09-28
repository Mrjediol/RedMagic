# Vendored third-party content — do not search or modify by default
`Assets/Brackeys/`, `Assets/Cainos/` (including its bundled `Third Party/Lucid Editor` inspector
attribute library), and `Assets/Dragon Warrior Files/` are imported asset packages (art, demo
scenes, a few utility scripts like `Chest.cs`/`Elevator.cs`/`SecondOrderDynamics.cs`). They are not
part of the game's own architecture. Scope searches to `Assets/Scripts`, `Assets/Ui`,
`Assets/Audio`, and `Assets/Scenes` unless a task specifically concerns one of these packages —
scanning all of `Assets/` pulls in hundreds of irrelevant demo/example files from them.
