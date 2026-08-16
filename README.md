# CustomFolder

A lightweight Playnite extension for creating and opening organized per-game folders directly from your library.

## Download

Download the latest release from the [Releases page](https://github.com/MinhHoag/CustomFolder/releases/latest).

## About

CustomFolder lets you create custom per-game folders using paths relative to your Playnite directory.

It uses Playnite's `{PlayniteDir}` path, allowing the same configuration to work with both **standard and portable Playnite installations**.

The extension was originally created for personal use. I wanted somewhere to keep things such as IDM exports, downloaded media, highlights, and other game-related files without going through 200 extra steps to create a folder manually — only to forget where I put it two days later.

CustomFolder turns that process into:

**Right-click game → CustomFolder → Choose a preset → Done.**

---

# Settings

You can find the settings under:

`Add-ons → Extension settings → Generic → CustomFolder`

## Storage Location

By default, CustomFolder uses:

```text
{PlayniteDir}\CustomFolder\
```

<img width="737" height="164" alt="CustomFolder storage settings" src="https://github.com/user-attachments/assets/dc9bf46e-e0db-46e1-9262-0c9c89ea02de" />

You can add a custom parent directory after `CustomFolder` if you want additional organization.

For example:

```text
{PlayniteDir}\CustomFolder\Personal\
```

CustomFolder also supports relative navigation such as:

```text
..\
..\..\
```

This allows advanced users to place their folders outside the default `CustomFolder` directory.

> **CAUTION:** CustomFolder does not delete or overwrite existing folders. However, if the generated path has the same name as an existing folder, Windows will treat them as the same folder. Be careful when using relative paths such as `..\`.

## Live Preview

The **Preview** section shows the actual path that CustomFolder will use before you create or open anything.

Standard Playnite example:

<img width="608" height="125" alt="CustomFolder preview on standard Playnite" src="https://github.com/user-attachments/assets/9cf000cc-ff1f-4c9c-9b69-6e3354ae0dc4" />

Portable Playnite example:

<img width="490" height="105" alt="CustomFolder preview on portable Playnite" src="https://github.com/user-attachments/assets/2febe759-45d7-4573-a264-5ec9df7e4096" />

Because the path is based on `{PlayniteDir}`, CustomFolder automatically follows wherever your Playnite installation is located.

---

# Presets

Starting with **v1.1.0**, CustomFolder supports multiple folder presets.

The first installation includes three default presets:

```text
Downloads
Media
Highlights
```

These are only examples. You can rename, remove, reorder, or create your own presets.

For example:

```text
CustomFolder
├── Downloads
│   └── Game Name
├── Media
│   └── Game Name
└── Highlights
    └── Game Name
```

Or you could create presets such as:

```text
Installation
Screenshots
Mods
Save Backup
Download Links
IDM
```

Selecting a preset in Settings updates the **Preview**, so you can see exactly where that preset will lead.

> **NOTE:** Removing a preset from the settings does **not** delete any folders or files that were already created. Presets only tell CustomFolder which folder name/path to open.

Your preset names, order, storage location, and other settings are saved and preserved when updating CustomFolder.

---

# How to Use

1. Open `Add-ons → Extension settings → Generic → CustomFolder`.
2. Configure your parent directory if needed.
3. Add, rename, remove, or reorder your presets.
4. Select a game in Playnite.
5. Right-click the game.
6. Hover over **CustomFolder**.
7. Select the preset you want.

CustomFolder will automatically create the directory if it does not already exist and open it in Windows File Explorer.

For example:

```text
CustomFolder
└── Downloads
    └── Ghost of Tsushima
```

If the folder already exists, CustomFolder simply opens the existing folder.

---

# Theme Integration

CustomFolder contains support for an optional **Quick Access** button in Playnite's game details view.

However, this is **not automatically supported by every Playnite theme**.

The feature was originally created for my personal modified version of the **Mythic** desktop theme:

<img width="80" height="60" alt="CustomFolder theme button" src="https://github.com/user-attachments/assets/da4962dc-2332-4573-9800-1ff9f81bef9e" />

A theme must explicitly include CustomFolder's custom element for the button to appear.

Because this setting is useless for most users without a compatible or manually modified theme, the option is hidden under **Developer Options**.

---

# Changelog

## v1.1.0

- Added customizable **presets** for organizing different types of game-related files.
- Added preset creation, renaming, removal, and reordering.
- Added persistent settings so customized presets and their order remain after addon updates.
- Improved path safety by making `{PlayniteDir}\CustomFolder\` the default base directory.
- Improved the live path preview and relative-path warnings.
- Removing a preset no longer implies that the corresponding folder will be deleted.
- Added Developer Options for creator/personal-use features.

## v1.0.0

- Initial release.
- Added per-game custom folder creation.
- Added configurable storage location based on `{PlayniteDir}`.
- Added support for standard and portable Playnite installations.
- Added live folder-path preview.
- Added right-click game integration.

---

# Notes

CustomFolder only creates directories and opens them through Windows File Explorer.

It does **not** manage, delete, move, or modify the files stored inside those directories.
