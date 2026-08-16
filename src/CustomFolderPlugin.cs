using Playnite.SDK;
using Playnite.SDK.Plugins;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Shapes;

using IOPath = System.IO.Path;
using ShapePath = System.Windows.Shapes.Path;

namespace CustomFolder
{
    public class CustomFolderPlugin : GenericPlugin
    {
        private const string DeveloperUnlockCode = "MinhHoang";
        private const string PlayniteToken = "{PlayniteDir}";

        public override Guid Id => Guid.Parse("7b4a1b34-6c57-4ef6-9d35-7e4e64b7c0a1");
        public CustomFolderSettings Settings { get; private set; }

        public CustomFolderPlugin(IPlayniteAPI api) : base(api)
        {
            Properties = new GenericPluginProperties { HasSettings = true };
            Settings = new CustomFolderSettings(this);

            AddCustomElementSupport(new AddCustomElementSupportArgs
            {
                ElementList = new List<string> { "PluginButton" },
                SourceName = "CustomFolder"
            });

            AddSettingsSupport(new AddSettingsSupportArgs
            {
                SourceName = "CustomFolder",
                SettingsRoot = "Settings"
            });
        }

        public override ISettings GetSettings(bool firstRunSettings) => Settings;

        public override UserControl GetSettingsView(bool firstRunSettings)
            => new CustomFolderSettingsView(this);

        public override IEnumerable<GameMenuItem> GetGameMenuItems(GetGameMenuItemsArgs args)
        {
            foreach (var preset in (Settings.Presets ?? new List<string>())
                .Where(p => !string.IsNullOrWhiteSpace(p))
                .Distinct(StringComparer.OrdinalIgnoreCase))
            {
                var capturedPreset = preset;

                yield return new GameMenuItem
                {
                    MenuSection = "CustomFolder",
                    Description = capturedPreset,
                    Action = actionArgs =>
                    {
                        var game = actionArgs.Games?.FirstOrDefault();
                        if (game != null)
                        {
                            OpenPresetFolder(game.Name, capturedPreset);
                        }
                    }
                };
            }

            yield return new GameMenuItem
            {
                MenuSection = "CustomFolder",
                Description = "Open CustomFolder Root",
                Action = actionArgs => OpenResolvedRoot()
            };
        }

        public override Control GetGameViewControl(GetGameViewControlArgs args)
        {
            if (!string.Equals(args.Name, "PluginButton", StringComparison.Ordinal))
            {
                return null;
            }

            var icon = new ShapePath
            {
                Width = 20,
                Height = 20,
                Stretch = Stretch.Uniform,
                Data = Geometry.Parse(
                    "M2,4 L8,4 L10,6 L22,6 C23.1,6 24,6.9 24,8 L24,19 " +
                    "C24,20.1 23.1,21 22,21 L2,21 C0.9,21 0,20.1 0,19 L0,6 " +
                    "C0,4.9 0.9,4 2,4 Z"),
                Fill = Brushes.Gray,
                IsHitTestVisible = false
            };

            var button = new Button
            {
                Content = icon,
                ToolTip = "CustomFolder",
                Width = 40,
                Height = 40,
                Padding = new Thickness(9),
                Focusable = false,
                HorizontalContentAlignment = HorizontalAlignment.Center,
                VerticalContentAlignment = VerticalAlignment.Center
            };

            icon.SetBinding(Shape.FillProperty, new Binding("Foreground")
            {
                Source = button,
                Mode = BindingMode.OneWay,
                FallbackValue = Brushes.Gray
            });

            button.Click += (s, e) =>
            {
                var game = PlayniteApi.MainView.SelectedGames?.FirstOrDefault();
                if (game == null) return;

                var preset = GetValidQuickAccessPreset();

                if (preset == null)
                {
                    OpenResolvedRoot();
                }
                else
                {
                    OpenPresetFolder(game.Name, preset);
                }
            };

            return button;
        }

        internal string GetValidQuickAccessPreset()
        {
            var presets = (Settings.Presets ?? new List<string>())
                .Where(p => !string.IsNullOrWhiteSpace(p))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (presets.Count == 0) return null;

            return presets.FirstOrDefault(p =>
                string.Equals(p, Settings.QuickAccessPreset, StringComparison.OrdinalIgnoreCase))
                ?? presets[0];
        }

        private void OpenPresetFolder(string gameName, string preset)
        {
            try
            {
                var folder = IOPath.Combine(
                    ResolveRoot(Settings.ParentDirectory),
                    MakeSafeFolderName(preset),
                    MakeSafeFolderName(gameName));

                Directory.CreateDirectory(folder);
                OpenExplorer(folder);
            }
            catch (Exception ex)
            {
                PlayniteApi.Dialogs.ShowErrorMessage(
                    "Could not create or open the custom folder.\n\n" + ex.Message,
                    "CustomFolder");
            }
        }

        private void OpenResolvedRoot()
        {
            try
            {
                var folder = ResolveRoot(Settings.ParentDirectory);
                Directory.CreateDirectory(folder);
                OpenExplorer(folder);
            }
            catch (Exception ex)
            {
                PlayniteApi.Dialogs.ShowErrorMessage(
                    "Could not create or open the CustomFolder root.\n\n" + ex.Message,
                    "CustomFolder");
            }
        }

        private static void OpenExplorer(string folder)
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = "explorer.exe",
                Arguments = "\"" + folder + "\"",
                UseShellExecute = true
            });
        }

        // Base layout:
        // {PlayniteDir}\CustomFolder\{Relative Root}\{Preset}\{GameName}
        //
        // Relative Root = ""       -> CustomFolder\Preset\Game
        // Relative Root = Personal -> CustomFolder\Personal\Preset\Game
        // Relative Root = ..\      -> PlayniteDir\Preset\Game
        // Relative Root = ..\..\   -> parent-of-PlayniteDir\Preset\Game
        internal string ResolveRoot(string configuredRoot)
        {
            var baseFolder = IOPath.Combine(GetPlayniteDirectory(), "CustomFolder");
            var relative = GetRootPart(configuredRoot);

            if (string.IsNullOrWhiteSpace(relative))
            {
                return IOPath.GetFullPath(baseFolder);
            }

            return IOPath.GetFullPath(IOPath.Combine(baseFolder, relative));
        }

        internal string ResolveExamplePath(string configuredRoot, string preset)
        {
            try
            {
                return IOPath.Combine(
                    ResolveRoot(configuredRoot),
                    MakeSafeFolderName(string.IsNullOrWhiteSpace(preset) ? "Downloads" : preset),
                    "Ghost of Tsushima");
            }
            catch
            {
                return "Invalid path";
            }
        }

        internal string GetRootPart(string configuredRoot)
        {
            var value = (configuredRoot ?? string.Empty).Trim();
            value = value.Replace('/', '\\');

            // Backward compatibility with older saved settings.
            if (value.StartsWith(PlayniteToken, StringComparison.OrdinalIgnoreCase))
            {
                value = value.Substring(PlayniteToken.Length).TrimStart('\\');

                if (string.IsNullOrWhiteSpace(value))
                {
                    return string.Empty;
                }

                // If an older setting included CustomFolder explicitly, strip it.
                if (value.StartsWith("CustomFolder\\", StringComparison.OrdinalIgnoreCase))
                {
                    value = value.Substring("CustomFolder\\".Length);
                }
                else if (string.Equals(value, "CustomFolder", StringComparison.OrdinalIgnoreCase))
                {
                    return string.Empty;
                }
            }

            while (value.Contains("\\\\"))
            {
                value = value.Replace("\\\\", "\\");
            }

            value = value.Trim('\\');

            if (value.Length >= 2 && char.IsLetter(value[0]) && value[1] == ':')
            {
                throw new ArgumentException("Use a relative path, not a drive path.");
            }

            var parts = value.Split(new[] { '\\' }, StringSplitOptions.RemoveEmptyEntries);

            foreach (var part in parts)
            {
                if (part == "." || part == "..")
                {
                    continue;
                }

                if (part.IndexOfAny(IOPath.GetInvalidFileNameChars()) >= 0)
                {
                    throw new ArgumentException("Invalid folder name: " + part);
                }
            }

            return string.Join("\\", parts);
        }

        internal string NormalizeParentDirectoryForDisplay(string configuredRoot)
        {
            var value = (configuredRoot ?? string.Empty).Trim();
            value = value.Replace('/', '\\');

            // Migrate the old full-token style into the new suffix-only style.
            if (value.StartsWith(PlayniteToken, StringComparison.OrdinalIgnoreCase))
            {
                value = value.Substring(PlayniteToken.Length).TrimStart('\\');

                if (value.StartsWith("CustomFolder\\", StringComparison.OrdinalIgnoreCase))
                {
                    value = value.Substring("CustomFolder\\".Length);
                }
                else if (string.Equals(value, "CustomFolder", StringComparison.OrdinalIgnoreCase))
                {
                    value = string.Empty;
                }
            }

            while (value.Contains("\\\\"))
            {
                value = value.Replace("\\\\", "\\");
            }

            // A trailing slash isn't needed because Path.Combine inserts one.
            return value.Trim('\\');
        }

        internal string GetPlayniteDirectory()
        {
            return PlayniteApi.Paths.ApplicationPath.TrimEnd(
                IOPath.DirectorySeparatorChar,
                IOPath.AltDirectorySeparatorChar);
        }

        internal string GetPlayniteDirectoryDisplay()
        {
            return GetPlayniteDirectory() + "\\";
        }

        internal string GetCustomFolderBaseDisplay()
        {
            return PlayniteToken + "\\CustomFolder\\";
        }

        internal int GetTraversalDepth(string configuredRoot)
        {
            try
            {
                var relative = GetRootPart(configuredRoot);

                if (string.IsNullOrWhiteSpace(relative))
                {
                    return 0;
                }

                var parts = relative.Split(
                    new[] { '\\' },
                    StringSplitOptions.RemoveEmptyEntries);

                var depth = 0;

                foreach (var part in parts)
                {
                    if (part == "..")
                    {
                        depth++;
                    }
                    else if (part == ".")
                    {
                        continue;
                    }
                    else
                    {
                        break;
                    }
                }

                return depth;
            }
            catch
            {
                return 0;
            }
        }

        internal string GetSafetyText(string configuredRoot)
        {
            try
            {
                var resolved = ResolveRoot(configuredRoot).TrimEnd('\\') + "\\";
                var playnite = IOPath.GetFullPath(GetPlayniteDirectory()).TrimEnd('\\') + "\\";
                var custom = IOPath.GetFullPath(
                    IOPath.Combine(GetPlayniteDirectory(), "CustomFolder"))
                    .TrimEnd('\\') + "\\";

                var depth = GetTraversalDepth(configuredRoot);

                if (resolved.StartsWith(custom, StringComparison.OrdinalIgnoreCase))
                {
                    return string.Empty;
                }

                if (resolved.StartsWith(playnite, StringComparison.OrdinalIgnoreCase))
                {
                    return depth > 0
                        ? "Warning: goes back " + depth + " folder level" + (depth == 1 ? "" : "s") + " and leaves CustomFolder."
                        : "Warning: resolved path is outside CustomFolder.";
                }

                return depth > 0
                    ? "Warning: goes back " + depth + " folder levels and leaves the Playnite directory."
                    : "Warning: resolved path is outside the Playnite directory.";
            }
            catch
            {
                return "Invalid path.";
            }
        }

        internal int GetSafetyLevel(string configuredRoot)
        {
            try
            {
                var resolved = ResolveRoot(configuredRoot).TrimEnd('\\') + "\\";
                var playnite = IOPath.GetFullPath(GetPlayniteDirectory()).TrimEnd('\\') + "\\";
                var custom = IOPath.GetFullPath(
                    IOPath.Combine(GetPlayniteDirectory(), "CustomFolder"))
                    .TrimEnd('\\') + "\\";

                if (resolved.StartsWith(custom, StringComparison.OrdinalIgnoreCase))
                {
                    return 0; // safe
                }

                if (resolved.StartsWith(playnite, StringComparison.OrdinalIgnoreCase))
                {
                    return 1; // outside CustomFolder
                }

                return 2; // outside Playnite
            }
            catch
            {
                return 3; // invalid
            }
        }

        internal static string MakeSafeFolderName(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return "Unnamed";

            var invalid = IOPath.GetInvalidFileNameChars();
            var cleaned = new string(name.Select(c => invalid.Contains(c) ? ' ' : c).ToArray());

            while (cleaned.Contains("  "))
            {
                cleaned = cleaned.Replace("  ", " ");
            }

            cleaned = cleaned.Trim().TrimEnd('.');
            return string.IsNullOrWhiteSpace(cleaned) ? "Unnamed" : cleaned;
        }

        internal bool CheckDeveloperCode(string value)
        {
            return string.Equals(
                value ?? string.Empty,
                DeveloperUnlockCode,
                StringComparison.Ordinal);
        }
    }

    public class CustomFolderSettings : ObservableObject, ISettings
    {
        private readonly CustomFolderPlugin plugin;

        private string parentDirectory = string.Empty;
        private List<string> presets = new List<string>
        {
            "Downloads",
            "Media",
            "Highlights"
        };

        private bool enableQuickAccessButton = false;
        private string quickAccessPreset = "Downloads";

        private string backupParentDirectory;
        private List<string> backupPresets;
        private bool backupQuickAccess;
        private string backupQuickAccessPreset;

        public string ParentDirectory
        {
            get => parentDirectory;
            set => SetValue(ref parentDirectory, value);
        }

        public List<string> Presets
        {
            get => presets;
            set => SetValue(ref presets, value ?? new List<string>());
        }

        public bool EnableQuickAccessButton
        {
            get => enableQuickAccessButton;
            set
            {
                if (enableQuickAccessButton == value)
                {
                    return;
                }

                SetValue(ref enableQuickAccessButton, value);

                // Keep old theme snippets working too.
                OnPropertyChanged(nameof(EnableIntegrationButton));
            }
        }

        // Backwards-compatible alias for older Mythic/theme snippets that
        // referenced EnableIntegrationButton.
        public bool EnableIntegrationButton
        {
            get => EnableQuickAccessButton;
            set => EnableQuickAccessButton = value;
        }

        public string QuickAccessPreset
        {
            get => quickAccessPreset;
            set => SetValue(ref quickAccessPreset, value);
        }

        public CustomFolderSettings() { }

        public CustomFolderSettings(CustomFolderPlugin plugin)
        {
            this.plugin = plugin;

            var saved = plugin.LoadPluginSettings<CustomFolderSettings>();

            if (saved != null)
            {
                if (!string.IsNullOrWhiteSpace(saved.ParentDirectory))
                    ParentDirectory = saved.ParentDirectory;

                if (saved.Presets != null && saved.Presets.Count > 0)
                {
                    Presets = NormalizePresetList(saved.Presets);
                }

                EnableQuickAccessButton = saved.EnableQuickAccessButton;

                if (!string.IsNullOrWhiteSpace(saved.QuickAccessPreset))
                    QuickAccessPreset = saved.QuickAccessPreset;
            }
        }

        public void BeginEdit()
        {
            backupParentDirectory = ParentDirectory;
            backupPresets = Presets.ToList();
            backupQuickAccess = EnableQuickAccessButton;
            backupQuickAccessPreset = QuickAccessPreset;
        }

        public void CancelEdit()
        {
            ParentDirectory = backupParentDirectory;
            Presets = backupPresets?.ToList() ?? new List<string>();
            EnableQuickAccessButton = backupQuickAccess;
            QuickAccessPreset = backupQuickAccessPreset;
        }

        public void EndEdit()
        {
            ParentDirectory = plugin.NormalizeParentDirectoryForDisplay(ParentDirectory);
            NormalizePresets();

            if (Presets.Count > 0 &&
                !Presets.Any(p =>
                    string.Equals(p, QuickAccessPreset, StringComparison.OrdinalIgnoreCase)))
            {
                QuickAccessPreset = Presets[0];
            }

            plugin.SavePluginSettings(this);
        }

        public bool VerifySettings(out List<string> errors)
        {
            errors = new List<string>();

            try
            {
                ParentDirectory =
                    plugin.NormalizeParentDirectoryForDisplay(ParentDirectory);

                plugin.ResolveRoot(ParentDirectory);
            }
            catch (Exception ex)
            {
                errors.Add("Parent Directory is invalid: " + ex.Message);
            }

            NormalizePresets();

            if (Presets.Count == 0)
            {
                errors.Add("Create at least one preset.");
            }

            var duplicate = Presets
                .GroupBy(p => p, StringComparer.OrdinalIgnoreCase)
                .FirstOrDefault(g => g.Count() > 1);

            if (duplicate != null)
            {
                errors.Add("Preset names must be unique. Duplicate: " + duplicate.Key);
            }

            return errors.Count == 0;
        }

        private void NormalizePresets()
        {
            Presets = NormalizePresetList(Presets);
        }

        private static List<string> NormalizePresetList(IEnumerable<string> source)
        {
            var result = new List<string>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var raw in source ?? Enumerable.Empty<string>())
            {
                var value = (raw ?? string.Empty).Trim();

                if (string.IsNullOrWhiteSpace(value))
                {
                    continue;
                }

                if (seen.Add(value))
                {
                    result.Add(value);
                }
            }

            return result;
        }
    }

    public class CustomFolderSettingsView : UserControl
    {
        private readonly CustomFolderPlugin plugin;

        private TextBox parentBox;
        private Button parentEditButton;
        private TextBlock previewText;
        private TextBlock safetyText;

        private ListBox presetList;
        private TextBox presetNameBox;

        private StackPanel developerPanel;
        private ComboBox quickAccessCombo;

        private bool developerUnlocked;

        private CustomFolderSettings CurrentSettings =>
            DataContext as CustomFolderSettings;

        public CustomFolderSettingsView(CustomFolderPlugin plugin)
        {
            this.plugin = plugin;
            BuildUi();
        }

        private void BuildUi()
        {
            var root = new StackPanel
            {
                Margin = new Thickness(18, 14, 24, 24),
                MaxWidth = 900,
                HorizontalAlignment = HorizontalAlignment.Left
            };

            root.Children.Add(new TextBlock
            {
                Text = "CustomFolder",
                FontSize = 24,
                FontWeight = FontWeights.SemiBold
            });

            root.Children.Add(new TextBlock
            {
                Text = "Create organized per-game folders using configurable presets.",
                Opacity = 0.72,
                Margin = new Thickness(0, 4, 0, 12)
            });

            // Preview kept directly above Root with a smaller gap.
            var previewGroup = new GroupBox
            {
                Header = "Preview",
                Padding = new Thickness(12),
                Margin = new Thickness(0, 0, 0, 6)
            };

            previewText = new TextBlock
            {
                FontFamily = new FontFamily("Consolas"),
                TextWrapping = TextWrapping.Wrap
            };

            previewGroup.Content = previewText;
            root.Children.Add(previewGroup);

            var storageGroup = new GroupBox
            {
                Header = "Root",
                Padding = new Thickness(14),
                Margin = new Thickness(0, 0, 0, 14)
            };

            var storage = new StackPanel();

            // Parent Directory + dynamic warning on the same line.
            var labelRow = new DockPanel
            {
                LastChildFill = true,
                Margin = new Thickness(0, 0, 0, 7)
            };

            var parentLabel = new TextBlock
            {
                Text = "Parent Directory",
                FontWeight = FontWeights.SemiBold
            };

            DockPanel.SetDock(parentLabel, Dock.Left);
            labelRow.Children.Add(parentLabel);

            safetyText = new TextBlock
            {
                Margin = new Thickness(12, 0, 0, 0),
                FontWeight = FontWeights.SemiBold,
                TextWrapping = TextWrapping.Wrap
            };

            labelRow.Children.Add(safetyText);
            storage.Children.Add(labelRow);

            // Fixed base + editable suffix + Edit button.
            var pathRow = new Grid();

            pathRow.ColumnDefinitions.Add(new ColumnDefinition
            {
                Width = GridLength.Auto
            });

            pathRow.ColumnDefinitions.Add(new ColumnDefinition
            {
                Width = new GridLength(1, GridUnitType.Star)
            });

            pathRow.ColumnDefinitions.Add(new ColumnDefinition
            {
                Width = GridLength.Auto
            });

            var basePath = new Border
            {
                Padding = new Thickness(8, 5, 8, 5),
                Margin = new Thickness(0, 0, 6, 0),
                VerticalAlignment = VerticalAlignment.Center,
                Child = new TextBlock
                {
                    Text = "{PlayniteDir}\\CustomFolder\\",
                    FontFamily = new FontFamily("Consolas"),
                    VerticalAlignment = VerticalAlignment.Center
                }
            };

            Grid.SetColumn(basePath, 0);
            pathRow.Children.Add(basePath);

            parentBox = new TextBox
            {
                Height = 30,
                MinWidth = 280,
                IsReadOnly = true,
                Opacity = 0.55,
                VerticalContentAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 8, 0),
                ToolTip = "Optional relative folder inserted after CustomFolder."
            };

            parentBox.SetBinding(TextBox.TextProperty, new Binding("ParentDirectory")
            {
                Mode = BindingMode.TwoWay,
                UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged
            });

            parentBox.TextChanged += (s, e) => UpdatePreview();

            parentBox.KeyDown += (s, e) =>
            {
                if (e.Key == System.Windows.Input.Key.Enter)
                {
                    SetParentEditMode(false);
                    e.Handled = true;
                }
                else if (e.Key == System.Windows.Input.Key.Escape)
                {
                    SetParentEditMode(false);
                    e.Handled = true;
                }
            };

            Grid.SetColumn(parentBox, 1);
            pathRow.Children.Add(parentBox);

            parentEditButton = new Button
            {
                Content = "Edit",
                MinWidth = 70,
                Height = 30
            };

            parentEditButton.Click += (s, e) =>
            {
                SetParentEditMode(parentBox.IsReadOnly);
            };

            Grid.SetColumn(parentEditButton, 2);
            pathRow.Children.Add(parentEditButton);

            storage.Children.Add(pathRow);

            // Friendly notes with clearer spacing.
            var notePanel = new StackPanel
            {
                Margin = new Thickness(0, 12, 0, 0)
            };

            notePanel.Children.Add(new TextBlock
            {
                Text = "How this path works",
                FontWeight = FontWeights.SemiBold,
                Margin = new Thickness(0, 0, 0, 6)
            });

            notePanel.Children.Add(new TextBlock
            {
                Text = "• {PlayniteDir} means your current Playnite folder: [" + plugin.GetPlayniteDirectoryDisplay() + "]",
                Opacity = 0.9,
                Margin = new Thickness(0, 2, 0, 7),
                TextWrapping = TextWrapping.Wrap
            });

            notePanel.Children.Add(new TextBlock
            {
                Text = "• CustomFolder is always added automatically. The Edit field only controls what comes after CustomFolder and before the preset.",
                Opacity = 0.78,
                Margin = new Thickness(0, 0, 0, 7),
                TextWrapping = TextWrapping.Wrap
            });

            notePanel.Children.Add(new TextBlock
            {
                Text = "• Leave the Edit field empty to use the base folder: {PlayniteDir}\\CustomFolder\\",
                Opacity = 0.78,
                Margin = new Thickness(0, 0, 0, 7),
                TextWrapping = TextWrapping.Wrap
            });

            notePanel.Children.Add(new TextBlock
            {
                Text = "• Relative navigation is supported: ..\\ goes back 1 folder, ..\\..\\ goes back 2 folders, and so on. A folder name after it is optional.",
                Opacity = 0.78,
                TextWrapping = TextWrapping.Wrap
            });

            storage.Children.Add(notePanel);

            storageGroup.Content = storage;
            root.Children.Add(storageGroup);

            // Presets
            var presetGroup = new GroupBox
            {
                Header = "Presets",
                Padding = new Thickness(14),
                Margin = new Thickness(0, 0, 0, 14)
            };

            var presetPanel = new StackPanel();

            presetPanel.Children.Add(new TextBlock
            {
                Text = "Presets are created after the Root and before the game name.",
                Opacity = 0.72,
                Margin = new Thickness(0, 0, 0, 9),
                TextWrapping = TextWrapping.Wrap
            });

            presetList = new ListBox
            {
                MinHeight = 120,
                MaxHeight = 220,
                Width = 500,
                HorizontalAlignment = HorizontalAlignment.Left,
                Margin = new Thickness(0, 0, 0, 10)
            };

            ScrollViewer.SetHorizontalScrollBarVisibility(
                presetList,
                ScrollBarVisibility.Auto);

            ScrollViewer.SetVerticalScrollBarVisibility(
                presetList,
                ScrollBarVisibility.Auto);

            presetList.SelectionChanged += (s, e) =>
            {
                if (presetList.SelectedItem != null)
                {
                    presetNameBox.Text = presetList.SelectedItem.ToString();
                }

                UpdatePreview();
            };

            presetPanel.Children.Add(presetList);

            var editRow = new StackPanel
            {
                Orientation = Orientation.Horizontal
            };

            presetNameBox = new TextBox
            {
                Width = 360,
                Height = 30,
                Margin = new Thickness(0, 0, 8, 0)
            };

            var add = new Button
            {
                Content = "Add",
                MinWidth = 66,
                Margin = new Thickness(0, 0, 8, 0)
            };
            add.Click += AddPreset_Click;

            var rename = new Button
            {
                Content = "Rename",
                MinWidth = 76
            };
            rename.Click += RenamePreset_Click;

            editRow.Children.Add(presetNameBox);
            editRow.Children.Add(add);
            editRow.Children.Add(rename);
            presetPanel.Children.Add(editRow);

            var actions = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Margin = new Thickness(0, 8, 0, 0)
            };

            var remove = new Button
            {
                Content = "Remove",
                MinWidth = 76,
                Margin = new Thickness(0, 0, 8, 0)
            };
            remove.Click += RemovePreset_Click;

            var up = new Button
            {
                Content = "Move Up",
                MinWidth = 76,
                Margin = new Thickness(0, 0, 8, 0)
            };
            up.Click += (s, e) => MovePreset(-1);

            var down = new Button
            {
                Content = "Move Down",
                MinWidth = 86
            };
            down.Click += (s, e) => MovePreset(1);

            actions.Children.Add(remove);
            actions.Children.Add(up);
            actions.Children.Add(down);
            presetPanel.Children.Add(actions);

            presetPanel.Children.Add(new TextBlock
            {
                Text =
                    "Removing a preset does not delete any folders or files already created. " +
                    "A preset only defines the folder name CustomFolder uses. " +
                    "Select a preset above to see its resolved directory in Preview.",
                Foreground = Brushes.Orange,
                Opacity = 0.9,
                TextWrapping = TextWrapping.Wrap,
                MaxWidth = 620,
                HorizontalAlignment = HorizontalAlignment.Left,
                Margin = new Thickness(0, 10, 0, 0)
            });

            presetGroup.Content = presetPanel;
            root.Children.Add(presetGroup);

            // Developer options
            var developerGroup = new GroupBox
            {
                Header = "Developer",
                Padding = new Thickness(14)
            };

            var developerRoot = new StackPanel();

            var unlock = new Button
            {
                Content = "Unlock Developer Options",
                HorizontalAlignment = HorizontalAlignment.Left
            };
            unlock.Click += UnlockDeveloper_Click;

            developerRoot.Children.Add(unlock);

            developerPanel = new StackPanel
            {
                Visibility = Visibility.Collapsed,
                Margin = new Thickness(0, 12, 0, 0)
            };

            developerPanel.Children.Add(new TextBlock
            {
                Text = "Theme integration requires the active theme to include CustomFolder_PluginButton.",
                Opacity = 0.72,
                Margin = new Thickness(0, 0, 0, 9),
                TextWrapping = TextWrapping.Wrap
            });

            var quick = new CheckBox
            {
                Content = "Show Quick Access button",
                Margin = new Thickness(0, 0, 0, 10)
            };

            quick.SetBinding(CheckBox.IsCheckedProperty, new Binding("EnableQuickAccessButton")
            {
                Mode = BindingMode.TwoWay,
                UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged
            });

            developerPanel.Children.Add(quick);
            developerPanel.Children.Add(new TextBlock
            {
                Text = "Quick Access preset",
                FontWeight = FontWeights.SemiBold,
                Margin = new Thickness(0, 0, 0, 5)
            });

            quickAccessCombo = new ComboBox
            {
                Width = 250,
                HorizontalAlignment = HorizontalAlignment.Left
            };

            quickAccessCombo.SetBinding(ComboBox.SelectedItemProperty, new Binding("QuickAccessPreset")
            {
                Mode = BindingMode.TwoWay,
                UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged
            });

            developerPanel.Children.Add(quickAccessCombo);
            developerRoot.Children.Add(developerPanel);
            developerGroup.Content = developerRoot;
            root.Children.Add(developerGroup);

            Content = new ScrollViewer
            {
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                Content = root
            };

            Loaded += (s, e) =>
            {
                RefreshPresetList();
                RefreshQuickAccessPresets();
                NormalizeParentBox();
                SetParentEditMode(false);
                UpdatePreview();
            };
        }

        private void SetParentEditMode(bool editing)
        {
            if (parentBox == null)
            {
                return;
            }

            if (editing)
            {
                parentBox.IsReadOnly = false;
                parentBox.Opacity = 1.0;

                if (parentEditButton != null)
                {
                    parentEditButton.Content = "Done";
                }

                parentBox.Focus();
                parentBox.CaretIndex = parentBox.Text.Length;
            }
            else
            {
                NormalizeParentBox();

                parentBox.IsReadOnly = true;
                parentBox.Opacity = 0.55;

                if (parentEditButton != null)
                {
                    parentEditButton.Content = "Edit";
                }

                UpdatePreview();
            }
        }

        private void NormalizeParentBox()
        {
            try
            {
                var normalized =
                    plugin.NormalizeParentDirectoryForDisplay(parentBox.Text);

                if (parentBox.Text != normalized)
                {
                    parentBox.Text = normalized;
                    parentBox.CaretIndex = parentBox.Text.Length;
                }
            }
            catch
            {
                // Final validation happens when settings are saved.
            }
        }

        private void UpdatePreview()
        {
            if (parentBox == null || previewText == null)
            {
                return;
            }

            var preset =
                presetList?.SelectedItem?.ToString()
                ?? CurrentSettings?.Presets?.FirstOrDefault()
                ?? "Downloads";

            previewText.Text =
                plugin.ResolveExamplePath(parentBox.Text, preset);

            if (safetyText != null)
            {
                var level = plugin.GetSafetyLevel(parentBox.Text);
                safetyText.Text = plugin.GetSafetyText(parentBox.Text);

                if (string.IsNullOrWhiteSpace(safetyText.Text))
                {
                    safetyText.Foreground = Brushes.Transparent;
                }
                else if (level == 1)
                {
                    safetyText.Foreground = Brushes.Orange;
                }
                else
                {
                    safetyText.Foreground = Brushes.Red;
                }
            }
        }

        private void AddPreset_Click(object sender, RoutedEventArgs e)
        {
            var name = GetPresetInput();
            if (name == null) return;

            if (CurrentSettings.Presets.Any(p =>
                string.Equals(p, name, StringComparison.OrdinalIgnoreCase)))
            {
                plugin.PlayniteApi.Dialogs.ShowMessage(
                    "A preset with that name already exists.",
                    "CustomFolder");
                return;
            }

            CurrentSettings.Presets.Add(name);
            presetNameBox.Clear();
            RefreshPresetList(name);
            RefreshQuickAccessPresets();
        }

        private void RenamePreset_Click(object sender, RoutedEventArgs e)
        {
            if (presetList.SelectedIndex < 0) return;

            var name = GetPresetInput();
            if (name == null) return;

            var index = presetList.SelectedIndex;
            var oldName = CurrentSettings.Presets[index];

            if (CurrentSettings.Presets
                .Where((p, i) => i != index)
                .Any(p => string.Equals(
                    p,
                    name,
                    StringComparison.OrdinalIgnoreCase)))
            {
                plugin.PlayniteApi.Dialogs.ShowMessage(
                    "A preset with that name already exists.",
                    "CustomFolder");
                return;
            }

            CurrentSettings.Presets[index] = name;

            if (string.Equals(
                CurrentSettings.QuickAccessPreset,
                oldName,
                StringComparison.OrdinalIgnoreCase))
            {
                CurrentSettings.QuickAccessPreset = name;
            }

            RefreshPresetList(name);
            RefreshQuickAccessPresets();
        }

        private void RemovePreset_Click(object sender, RoutedEventArgs e)
        {
            if (presetList.SelectedIndex < 0) return;

            if (CurrentSettings.Presets.Count <= 1)
            {
                plugin.PlayniteApi.Dialogs.ShowMessage(
                    "CustomFolder must have at least one preset.",
                    "CustomFolder");
                return;
            }

            var index = presetList.SelectedIndex;
            var removed = CurrentSettings.Presets[index];

            CurrentSettings.Presets.RemoveAt(index);

            if (string.Equals(
                CurrentSettings.QuickAccessPreset,
                removed,
                StringComparison.OrdinalIgnoreCase))
            {
                CurrentSettings.QuickAccessPreset =
                    CurrentSettings.Presets[0];
            }

            RefreshPresetList(
                CurrentSettings.Presets[
                    Math.Min(index, CurrentSettings.Presets.Count - 1)]);

            RefreshQuickAccessPresets();
        }

        private void MovePreset(int direction)
        {
            if (presetList.SelectedIndex < 0) return;

            var oldIndex = presetList.SelectedIndex;
            var newIndex = oldIndex + direction;

            if (newIndex < 0 ||
                newIndex >= CurrentSettings.Presets.Count)
            {
                return;
            }

            var item = CurrentSettings.Presets[oldIndex];

            CurrentSettings.Presets.RemoveAt(oldIndex);
            CurrentSettings.Presets.Insert(newIndex, item);

            RefreshPresetList(item);
            RefreshQuickAccessPresets();
        }

        private string GetPresetInput()
        {
            var value =
                (presetNameBox.Text ?? string.Empty).Trim();

            if (string.IsNullOrWhiteSpace(value))
            {
                plugin.PlayniteApi.Dialogs.ShowMessage(
                    "Preset name cannot be empty.",
                    "CustomFolder");
                return null;
            }

            if (CustomFolderPlugin.MakeSafeFolderName(value) != value)
            {
                plugin.PlayniteApi.Dialogs.ShowMessage(
                    "Preset contains invalid Windows folder characters.",
                    "CustomFolder");
                return null;
            }

            return value;
        }

        private void RefreshPresetList(string selected = null)
        {
            var wanted =
                selected ?? presetList.SelectedItem?.ToString();

            presetList.ItemsSource = null;
            presetList.ItemsSource =
                CurrentSettings.Presets.ToList();

            if (wanted != null)
            {
                presetList.SelectedItem =
                    CurrentSettings.Presets.FirstOrDefault(p =>
                        string.Equals(
                            p,
                            wanted,
                            StringComparison.OrdinalIgnoreCase));
            }

            if (presetList.SelectedIndex < 0 &&
                CurrentSettings.Presets.Count > 0)
            {
                presetList.SelectedIndex = 0;
            }

            UpdatePreview();
        }

        private void RefreshQuickAccessPresets()
        {
            if (quickAccessCombo == null) return;

            quickAccessCombo.ItemsSource = null;
            quickAccessCombo.ItemsSource =
                CurrentSettings.Presets.ToList();

            var match =
                CurrentSettings.Presets.FirstOrDefault(p =>
                    string.Equals(
                        p,
                        CurrentSettings.QuickAccessPreset,
                        StringComparison.OrdinalIgnoreCase));

            quickAccessCombo.SelectedItem =
                match ?? CurrentSettings.Presets.FirstOrDefault();
        }

        private void UnlockDeveloper_Click(object sender, RoutedEventArgs e)
        {
            if (developerUnlocked)
            {
                developerPanel.Visibility =
                    developerPanel.Visibility == Visibility.Visible
                    ? Visibility.Collapsed
                    : Visibility.Visible;

                return;
            }

            var result =
                plugin.PlayniteApi.Dialogs.SelectString(
                    "Enter developer code:",
                    "CustomFolder Developer Options",
                    string.Empty);

            if (result == null || !result.Result)
            {
                return;
            }

            if (!plugin.CheckDeveloperCode(result.SelectedString))
            {
                plugin.PlayniteApi.Dialogs.ShowMessage(
                    "Incorrect developer code.",
                    "CustomFolder");
                return;
            }

            developerUnlocked = true;
            developerPanel.Visibility = Visibility.Visible;
            RefreshQuickAccessPresets();
        }
    }
}
