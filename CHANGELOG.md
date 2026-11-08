# Changelog

All notable changes to this project are documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

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
