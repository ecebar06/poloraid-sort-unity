# Polaroid Sort

Polaroid Sort is a mobile word-sorting puzzle game developed with Unity. Players organize word and image cards into the correct category holders while managing a limited number of moves.

## Features

- Drag-and-drop word and image cards
- Category-based sorting mechanics
- 50 playable levels
- 500+ words and 380+ custom icons
- Category transformation mechanics
- Limited-move gameplay
- Animated category completion and progress system
- Win and out-of-moves screens
- Sound, music and vibration settings
- Persistent progress and coin system
- Responsive portrait-oriented mobile interface
- Custom splash screen and polished UI animations

## Gameplay

Drag each card into the holder that matches its category. Correct cards snap into available slots, while incorrect placements consume moves. Complete every category before running out of moves to finish the level.

Some levels include transformation mechanics. Completing a category can generate a new card that must be used to solve another category.

## Built With

- Unity
- C#
- TextMesh Pro
- DOTween
- Unity UI
- JSON-based level and word data

## Project Structure

- `Assets/_Game/Scripts` — Gameplay, UI, save system and editor tools
- `Assets/_Game/Scenes` — Splash and gameplay scenes
- `Assets/_Game/Resources/Data` — Level, word and icon-library data
- `Assets/_Game/Resources/Icons` — Word and category icons
- `Assets/_Game/Art` — UI and visual assets

## Controls

- Drag a card to move it.
- Drop it onto a category holder to place it.
- Correct placements flash green.
- Incorrect placements flash red and consume a move.

## Screenshots

Add gameplay screenshots here:

```text
Screenshots/
├── gameplay.png
├── settings.png
├── win-screen.png
└── out-of-moves.png
