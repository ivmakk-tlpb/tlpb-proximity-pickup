# Changelog

All notable changes to this project are documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

## [1.1.0] - 2026-11-13

### Added

- One action of collecting clay, mud, or sand by hand takes the same kind of ground within `PickupRadius`, with one message for the total.
- A gather of a ready resource that you gather in a moment (mushrooms, herbs, Dandelions, sticks, small stones, Clay Deposits) also gathers the same kind on its label, or within `PickupRadius` with the labels off. Each resource gives the yield and the XP of one normal gather, and one message shows the total.
- A group gather stops before a resource whose drops do not fit in the bag, and that resource stays in the world.
- Labels of ready resources of the same kind merge into one label with their number, such as "Spotted Red Mushroom (5)".
- With the labels off, the hover name of a resource shows how many resources a click gathers.
- A middle-click picks up one item or gathers one resource, also from a merged label. A hold or a drag of the middle mouse button still turns the camera.
- The line of actions above the hotbar shows the middle mouse button with "Pick up one" or "Gather one" for a merged label.

### Fixed

- No empty label box shows above your character while you hold an item such as a bottle.

## [1.0.0] - 2026-11-08

### Added

- Item labels of the same kind within `PickupRadius` merge into one label with the total count, such as "Stick (11)".
- A click on a label, or on an item with a label, picks all the items on that label.
- A click on an item with the labels off also picks the items of the same kind within `PickupRadius`, and the hover name shows the total.
- Labels that overlap on the screen move up or down until each has its own space.
- A dark box behind each label, which catches the mouse.
- A hover that keeps the label in place, with a darker box and a thin border in the color of the label.
- The actions of the hovered thing in one line above the hotbar.
- A progress fill in the label box while you hold an action.
- The settings `PickMode` and `PickupRadius`.
