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
                    ResolveCurrentRoot(Settings),
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
                var folder = ResolveCurrentRoot(Settings);
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

        // The user selects the PARENT directory. CustomFolder is always appended.
        // Relative: {PlayniteDir}\{ParentDirectory}\CustomFolder
        // Absolute: {AbsoluteParentDirectory}\CustomFolder
        internal string ResolveRoot(string configuredRoot)
        {
            var relative = GetRootPart(configuredRoot);
            var parent = string.IsNullOrWhiteSpace(relative)
                ? GetPlayniteDirectory()
                : IOPath.GetFullPath(IOPath.Combine(GetPlayniteDirectory(), relative));

            return IOPath.GetFullPath(IOPath.Combine(parent, "CustomFolder"));
        }

        internal string ResolveAbsoluteRoot(string absoluteParent)
        {
            var value = (absoluteParent ?? string.Empty).Trim().Trim('"');
            value = value.Replace('/', '\\');

            if (string.IsNullOrWhiteSpace(value))
                throw new ArgumentException("Choose an absolute parent directory.");

            if (!IOPath.IsPathRooted(value))
                throw new ArgumentException("Absolute path must include a drive path.");

            if (value.Contains("\\\\"))
                throw new ArgumentException("Directory cannot contain double backslashes.");

            var root = IOPath.GetPathRoot(value);
            if (string.IsNullOrWhiteSpace(root))
                throw new ArgumentException("Absolute path has an invalid root.");

            var remainder = value.Substring(root.Length).TrimEnd('\\');

            if (!string.IsNullOrEmpty(remainder))
            {
                var parts = remainder.Split(new[] { '\\' }, StringSplitOptions.None);

                foreach (var part in parts)
                {
                    if (part.Length == 0)
                        throw new ArgumentException("Directory contains an empty folder segment.");

                    ValidateFolderSegment(part);
                }
            }

            return IOPath.GetFullPath(IOPath.Combine(value.TrimEnd('\\'), "CustomFolder"));
        }

        internal string ResolveCurrentRoot(CustomFolderSettings settings)
        {
            return settings != null && settings.UseAbsolutePath
                ? ResolveAbsoluteRoot(settings.AbsoluteParentDirectory)
                : ResolveRoot(settings?.ParentDirectory);
        }

        internal string ResolveExamplePath(CustomFolderSettings settings, string preset)
        {
            try
            {
                return IOPath.Combine(
                    ResolveCurrentRoot(settings),
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

            if (value.StartsWith(PlayniteToken, StringComparison.OrdinalIgnoreCase))
            {
                value = value.Substring(PlayniteToken.Length).TrimStart('\\');

                if (string.IsNullOrWhiteSpace(value))
                    return string.Empty;

                if (value.StartsWith("CustomFolder\\", StringComparison.OrdinalIgnoreCase))
                    value = value.Substring("CustomFolder\\".Length);
                else if (string.Equals(value, "CustomFolder", StringComparison.OrdinalIgnoreCase))
                    return string.Empty;
            }

            if (value.Contains("\\\\"))
                throw new ArgumentException("Directory cannot contain double backslashes.");

            value = value.TrimEnd('\\');

            if (string.IsNullOrWhiteSpace(value))
                return string.Empty;

            if (value.Length >= 2 && char.IsLetter(value[0]) && value[1] == ':')
                throw new ArgumentException("Use Absolute path mode for drive paths.");

            var parts = value.Split(new[] { '\\' }, StringSplitOptions.None);

            foreach (var part in parts)
            {
                if (part.Length == 0)
                    throw new ArgumentException("Directory contains an empty folder segment.");

                if (part == "." || part == "..")
                    continue;

                ValidateFolderSegment(part);
            }

            return string.Join("\\", parts);
        }

        internal static void ValidateFolderSegment(string part)
        {
            if (string.IsNullOrEmpty(part))
                throw new ArgumentException("Folder name cannot be empty.");

            if (char.IsWhiteSpace(part[0]))
                throw new ArgumentException("Folder names cannot start with a space.");

            if (part.EndsWith(" ", StringComparison.Ordinal))
                throw new ArgumentException("Folder names cannot end with a space.");

            if (part.EndsWith(".", StringComparison.Ordinal))
                throw new ArgumentException("Folder names cannot end with a period.");

            if (part.IndexOfAny(IOPath.GetInvalidFileNameChars()) >= 0)
                throw new ArgumentException("Folder name contains invalid Windows characters: " + part);

            var baseName = part.Split('.')[0];
            var reserved = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "CON", "PRN", "AUX", "NUL",
                "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9",
                "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9"
            };

            if (reserved.Contains(baseName))
                throw new ArgumentException("Folder name is reserved by Windows: " + part);
        }

        internal string NormalizeParentDirectoryForDisplay(string configuredRoot)
        {
            var value = (configuredRoot ?? string.Empty).Trim();
            value = value.Replace('/', '\\');

            if (value.StartsWith(PlayniteToken, StringComparison.OrdinalIgnoreCase))
            {
                value = value.Substring(PlayniteToken.Length).TrimStart('\\');

                if (value.StartsWith("CustomFolder\\", StringComparison.OrdinalIgnoreCase))
                    value = value.Substring("CustomFolder\\".Length);
                else if (string.Equals(value, "CustomFolder", StringComparison.OrdinalIgnoreCase))
                    value = string.Empty;
            }

            var displayValue = value.TrimStart('\\');

            // Validate without changing whether the user typed the final slash.
            GetRootPart(displayValue);
            return displayValue;
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

        internal bool IsSamePath(string first, string second)
        {
            if (string.IsNullOrWhiteSpace(first) || string.IsNullOrWhiteSpace(second))
                return false;

            var a = IOPath.GetFullPath(first).TrimEnd('\\', '/');
            var b = IOPath.GetFullPath(second).TrimEnd('\\', '/');
            return string.Equals(a, b, StringComparison.OrdinalIgnoreCase);
        }

        internal bool IsInsidePlayniteDirectory(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) return false;

            var playnite = IOPath.GetFullPath(GetPlayniteDirectory()).TrimEnd('\\', '/');
            var target = IOPath.GetFullPath(path).TrimEnd('\\', '/');

            return string.Equals(playnite, target, StringComparison.OrdinalIgnoreCase) ||
                   target.StartsWith(playnite + "\\", StringComparison.OrdinalIgnoreCase);
        }

        // User may select either CustomFolder itself or its parent.
        internal string DetectCustomFolder(string selectedPath)
        {
            if (string.IsNullOrWhiteSpace(selectedPath)) return null;

            var selected = IOPath.GetFullPath(selectedPath).TrimEnd('\\', '/');

            if (Directory.Exists(selected) &&
                string.Equals(IOPath.GetFileName(selected), "CustomFolder",
                    StringComparison.OrdinalIgnoreCase))
                return selected;

            var child = IOPath.Combine(selected, "CustomFolder");
            return Directory.Exists(child) ? IOPath.GetFullPath(child) : null;
        }

        internal MigrationResult MigrateFolder(string source, string destination)
        {
            source = IOPath.GetFullPath(source);
            destination = IOPath.GetFullPath(destination);

            if (!Directory.Exists(source))
                return new MigrationResult { SourceFound = false };

            if (IsSamePath(source, destination))
                return new MigrationResult { SourceFound = true, SameLocation = true, Destination = destination };

            var result = new MigrationResult { SourceFound = true, Destination = destination };
            Directory.CreateDirectory(destination);
            MergeDirectory(source, destination, result);

            if (Directory.Exists(source) && !Directory.EnumerateFileSystemEntries(source).Any())
                Directory.Delete(source);

            return result;
        }

        private static void MergeDirectory(string source, string destination, MigrationResult result)
        {
            Directory.CreateDirectory(destination);

            foreach (var file in Directory.GetFiles(source))
            {
                var target = IOPath.Combine(destination, IOPath.GetFileName(file));

                if (File.Exists(target))
                {
                    result.SkippedFiles++;
                    continue;
                }

                try
                {
                    File.Move(file, target);
                }
                catch (IOException)
                {
                    File.Copy(file, target, false);
                    File.Delete(file);
                }

                result.MovedFiles++;
            }

            foreach (var directory in Directory.GetDirectories(source))
            {
                var target = IOPath.Combine(destination, IOPath.GetFileName(directory));
                MergeDirectory(directory, target, result);

                if (!Directory.EnumerateFileSystemEntries(directory).Any())
                    Directory.Delete(directory);
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

    public class MigrationResult
    {
        public bool SourceFound { get; set; }
        public bool SameLocation { get; set; }
        public int MovedFiles { get; set; }
        public int SkippedFiles { get; set; }
        public string Destination { get; set; }
    }

    public class CustomFolderSettings : ObservableObject, ISettings
    {
        private readonly CustomFolderPlugin plugin;

        private const int CurrentPathSettingsVersion = 2;

        private int pathSettingsVersion = CurrentPathSettingsVersion;
        private string parentDirectory = "..\\";
        private bool useAbsolutePath = false;
        private string absoluteParentDirectory = string.Empty;
        private string previousRootPath = string.Empty;
        private List<string> recentRootPaths = new List<string>();
        private string sessionOriginalRoot = string.Empty;
        private List<string> presets = new List<string>
        {
            "Downloads",
            "Media",
            "Highlights"
        };

        private bool enableQuickAccessButton = false;
        private string quickAccessPreset = "Downloads";

        private string backupParentDirectory;
        private bool backupUseAbsolutePath;
        private string backupAbsoluteParentDirectory;
        private string backupPreviousRootPath;
        private List<string> backupRecentRootPaths;
        private string backupResolvedRoot;
        private List<string> backupPresets;
        private bool backupQuickAccess;
        private string backupQuickAccessPreset;

        public string ParentDirectory
        {
            get => parentDirectory;
            set => SetValue(ref parentDirectory, value);
        }

        public int PathSettingsVersion
        {
            get => pathSettingsVersion;
            set => SetValue(ref pathSettingsVersion, value);
        }

        public bool UseAbsolutePath
        {
            get => useAbsolutePath;
            set => SetValue(ref useAbsolutePath, value);
        }

        public string AbsoluteParentDirectory
        {
            get => absoluteParentDirectory;
            set => SetValue(ref absoluteParentDirectory, value);
        }

        public string PreviousRootPath
        {
            get => previousRootPath;
            set => SetValue(ref previousRootPath, value);
        }

        public List<string> RecentRootPaths
        {
            get => recentRootPaths;
            set => SetValue(ref recentRootPaths, value ?? new List<string>());
        }

        internal string SessionOriginalRoot => sessionOriginalRoot;

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
                var savedParentDirectory = saved.ParentDirectory ?? string.Empty;
                var savedVersion = saved.PathSettingsVersion;

                UseAbsolutePath = saved.UseAbsolutePath;
                AbsoluteParentDirectory = saved.AbsoluteParentDirectory ?? string.Empty;
                PreviousRootPath = saved.PreviousRootPath ?? string.Empty;
                RecentRootPaths = saved.RecentRootPaths?.ToList() ?? new List<string>();

                if (savedVersion < CurrentPathSettingsVersion && !UseAbsolutePath)
                {
                    // Old layout:
                    //   {PlayniteDir}\CustomFolder\{ParentDirectory}
                    //
                    // New layout:
                    //   {PlayniteDir}\{ParentDirectory}\CustomFolder
                    //
                    // Preserve the OLD resolved folder as Migration's source,
                    // then convert the setting when the old resolved location
                    // already ended with "CustomFolder".
                    try
                    {
                        var oldBase = IOPath.Combine(plugin.GetPlayniteDirectory(), "CustomFolder");
                        var oldRelative = plugin.GetRootPart(savedParentDirectory);
                        var oldResolved = string.IsNullOrWhiteSpace(oldRelative)
                            ? IOPath.GetFullPath(oldBase)
                            : IOPath.GetFullPath(IOPath.Combine(oldBase, oldRelative));

                        if (string.IsNullOrWhiteSpace(PreviousRootPath))
                            PreviousRootPath = oldResolved;

                        if (string.Equals(
                            IOPath.GetFileName(oldResolved.TrimEnd('\\', '/')),
                            "CustomFolder",
                            StringComparison.OrdinalIgnoreCase))
                        {
                            var newParent = IOPath.GetDirectoryName(
                                oldResolved.TrimEnd('\\', '/'));

                            ParentDirectory = MakeRelativeToPlaynite(
                                plugin.GetPlayniteDirectory(),
                                newParent);
                        }
                        else
                        {
                            // The old custom root did not itself end in CustomFolder,
                            // so there is no exact representation in the new model.
                            // Start at the new recommended location and let Migration
                            // use PreviousRootPath as the old source.
                            ParentDirectory = "..\\";
                        }
                    }
                    catch
                    {
                        ParentDirectory = "..\\";
                    }

                    PathSettingsVersion = CurrentPathSettingsVersion;
                }
                else
                {
                    ParentDirectory = savedParentDirectory;
                    PathSettingsVersion = CurrentPathSettingsVersion;
                }

                if (saved.Presets != null && saved.Presets.Count > 0)
                {
                    Presets = NormalizePresetList(saved.Presets);
                }

                EnableQuickAccessButton = saved.EnableQuickAccessButton;

                if (!string.IsNullOrWhiteSpace(saved.QuickAccessPreset))
                    QuickAccessPreset = saved.QuickAccessPreset;
            }

            try
            {
                // This is only the currently SAVED root when the addon loads.
                // Do not write it into PreviousRootPath here. Path history is
                // committed only from Playnite's built-in Save -> EndEdit().
                sessionOriginalRoot = plugin.ResolveCurrentRoot(this);
            }
            catch
            {
                sessionOriginalRoot = string.Empty;
            }
        }

        internal void RecordSuccessfulRootChange(string oldRoot, string newRoot)
        {
            if (string.IsNullOrWhiteSpace(oldRoot) ||
                string.IsNullOrWhiteSpace(newRoot) ||
                plugin.IsSamePath(oldRoot, newRoot))
                return;

            PreviousRootPath = oldRoot;

            var history = (RecentRootPaths ?? new List<string>())
                .Where(p => !string.IsNullOrWhiteSpace(p))
                .Where(p => !plugin.IsSamePath(p, oldRoot))
                .ToList();

            history.Insert(0, oldRoot);
            RecentRootPaths = history.Take(3).ToList();
        }

        private static string MakeRelativeToPlaynite(string playniteDirectory, string targetDirectory)
        {
            if (string.IsNullOrWhiteSpace(targetDirectory))
                return string.Empty;

            var playnite = IOPath.GetFullPath(playniteDirectory).TrimEnd('\\') + "\\";
            var target = IOPath.GetFullPath(targetDirectory).TrimEnd('\\') + "\\";

            var relative = Uri.UnescapeDataString(
                new Uri(playnite).MakeRelativeUri(new Uri(target)).ToString())
                .Replace('/', '\\');

            if (relative == "." || relative == ".\\")
                return string.Empty;

            return relative.EndsWith("\\", StringComparison.Ordinal)
                ? relative
                : relative + "\\";
        }

        public void BeginEdit()
        {
            backupParentDirectory = ParentDirectory;
            backupUseAbsolutePath = UseAbsolutePath;
            backupAbsoluteParentDirectory = AbsoluteParentDirectory;
            backupPreviousRootPath = PreviousRootPath;
            backupRecentRootPaths = RecentRootPaths?.ToList() ?? new List<string>();
            backupPresets = Presets.ToList();
            backupQuickAccess = EnableQuickAccessButton;
            backupQuickAccessPreset = QuickAccessPreset;

            try
            {
                // Playnite calls BeginEdit when the settings session starts.
                // This is the root that was actually saved before the user edits it.
                backupResolvedRoot = plugin.ResolveCurrentRoot(this);
                sessionOriginalRoot = backupResolvedRoot;
            }
            catch
            {
                backupResolvedRoot = string.Empty;
            }
        }

        public void CancelEdit()
        {
            ParentDirectory = backupParentDirectory;
            UseAbsolutePath = backupUseAbsolutePath;
            AbsoluteParentDirectory = backupAbsoluteParentDirectory;
            PreviousRootPath = backupPreviousRootPath;
            RecentRootPaths = backupRecentRootPaths?.ToList() ?? new List<string>();
            sessionOriginalRoot = backupResolvedRoot;
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

            try
            {
                var newRoot = plugin.ResolveCurrentRoot(this);

                // IMPORTANT:
                // Playnite's built-in Save calls EndEdit().
                // Only a successful Save commits the previous path/history.
                if (!string.IsNullOrWhiteSpace(backupResolvedRoot) &&
                    !plugin.IsSamePath(backupResolvedRoot, newRoot))
                {
                    RecordSuccessfulRootChange(backupResolvedRoot, newRoot);
                }

                // The just-saved root becomes the next settings session baseline.
                sessionOriginalRoot = newRoot;
                backupResolvedRoot = newRoot;
            }
            catch
            {
                // VerifySettings reports invalid paths before EndEdit is accepted.
            }

            PathSettingsVersion = CurrentPathSettingsVersion;
            plugin.SavePluginSettings(this);
        }

        public bool VerifySettings(out List<string> errors)
        {
            errors = new List<string>();

            try
            {
                ParentDirectory =
                    plugin.NormalizeParentDirectoryForDisplay(ParentDirectory);

                if (UseAbsolutePath)
                    plugin.ResolveAbsoluteRoot(AbsoluteParentDirectory);
                else
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
        private TextBox absoluteParentBox;
        private Button parentEditButton;
        private Button resetRelativeButton;
        private Button browseAbsoluteButton;
        private Button relativeModeButton;
        private Button absoluteModeButton;
        private TextBlock relativeModeTitle;
        private TextBlock relativeModeNote;
        private TextBlock absoluteModeTitle;
        private TextBlock previewText;
        private TextBlock safetyText;
        private TextBlock changedPathWarning;
        private TextBlock migrationSourceText;
        private TextBlock migrationSourceWarning;
        private string migrationSourceOverride;

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
                Margin = new Thickness(0, 4, 0, 4)
            });

            root.Children.Add(new TextBlock
            {
                Text = "Disclaimer: CustomFolder is not a backup. Files stored here can still be deleted, lost, or corrupted.",
                Foreground = Brushes.Red,
                Opacity = 0.82,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 0, 0, 12)
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

            storage.Children.Add(new TextBlock
            {
                Text = "Path type",
                FontWeight = FontWeights.SemiBold,
                Margin = new Thickness(0, 0, 0, 8)
            });

            relativeModeTitle = new TextBlock
            {
                Text = "Relative to Playnite directory",
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Left
            };

            relativeModeNote = new TextBlock
            {
                Text = "Recommended for portability",
                Opacity = 0.48,
                FontSize = 11,
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Right
            };

            var relativeModeContent = new Grid();
            relativeModeContent.ColumnDefinitions.Add(
                new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            relativeModeContent.ColumnDefinitions.Add(
                new ColumnDefinition { Width = GridLength.Auto });

            Grid.SetColumn(relativeModeTitle, 0);
            Grid.SetColumn(relativeModeNote, 1);

            relativeModeContent.Children.Add(relativeModeTitle);
            relativeModeContent.Children.Add(relativeModeNote);

            relativeModeButton = new Button
            {
                Content = relativeModeContent,
                MinHeight = 34,
                HorizontalContentAlignment = HorizontalAlignment.Stretch,
                Padding = new Thickness(10, 5, 10, 5),
                Margin = new Thickness(0, 0, 0, 6)
            };
            relativeModeButton.Click += (s, e) =>
            {
                if (CurrentSettings != null)
                    CurrentSettings.UseAbsolutePath = false;

                UpdatePathModeUi();
                UpdatePreview();
            };
            storage.Children.Add(relativeModeButton);

            var relativeRow = new Grid
            {
                Margin = new Thickness(20, 0, 0, 14)
            };

            relativeRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            relativeRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            relativeRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            relativeRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            relativeRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var prefix = new TextBlock
            {
                Text = "{PlayniteDir}\\",
                FontFamily = new FontFamily("Consolas"),
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 6, 0)
            };
            Grid.SetColumn(prefix, 0);
            relativeRow.Children.Add(prefix);

            parentBox = new TextBox
            {
                Height = 30,
                MinWidth = 260,
                VerticalContentAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 6, 0),
                ToolTip = "Parent path relative to the Playnite directory."
            };
            parentBox.SetBinding(TextBox.TextProperty, new Binding("ParentDirectory")
            {
                Mode = BindingMode.TwoWay,
                UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged
            });
            parentBox.TextChanged += (s, e) => UpdatePreview();
            Grid.SetColumn(parentBox, 1);
            relativeRow.Children.Add(parentBox);

            var relativeSuffix = new TextBlock
            {
                Text = "\\CustomFolder\\",
                FontFamily = new FontFamily("Consolas"),
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 8, 0)
            };
            Grid.SetColumn(relativeSuffix, 2);
            relativeRow.Children.Add(relativeSuffix);

            parentEditButton = new Button
            {
                Content = "Browse",
                MinWidth = 70,
                Height = 30,
                Margin = new Thickness(0, 0, 6, 0)
            };
            parentEditButton.Click += BrowseRelative_Click;
            Grid.SetColumn(parentEditButton, 3);
            relativeRow.Children.Add(parentEditButton);

            resetRelativeButton = new Button
            {
                Content = "Reset",
                MinWidth = 70,
                Height = 30,
                ToolTip = "Reset to {PlayniteDir}\\..\\CustomFolder\\"
            };
            resetRelativeButton.Click += ResetRelative_Click;
            Grid.SetColumn(resetRelativeButton, 4);
            relativeRow.Children.Add(resetRelativeButton);

            storage.Children.Add(relativeRow);

            // Absolute option
            absoluteModeTitle = new TextBlock
            {
                Text = "Absolute path",
                VerticalAlignment = VerticalAlignment.Center
            };

            absoluteModeButton = new Button
            {
                Content = absoluteModeTitle,
                MinHeight = 34,
                HorizontalContentAlignment = HorizontalAlignment.Left,
                Padding = new Thickness(10, 5, 10, 5),
                Margin = new Thickness(0, 0, 0, 6)
            };
            absoluteModeButton.Click += (s, e) =>
            {
                if (CurrentSettings != null)
                    CurrentSettings.UseAbsolutePath = true;

                UpdatePathModeUi();
                UpdatePreview();
            };
            storage.Children.Add(absoluteModeButton);

            var absoluteRow = new Grid
            {
                Margin = new Thickness(20, 0, 0, 10)
            };

            absoluteRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            absoluteRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            absoluteRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            absoluteParentBox = new TextBox
            {
                Height = 30,
                MinWidth = 420,
                VerticalContentAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 6, 0),
                ToolTip = "Absolute parent directory. CustomFolder is appended automatically."
            };
            absoluteParentBox.SetBinding(TextBox.TextProperty, new Binding("AbsoluteParentDirectory")
            {
                Mode = BindingMode.TwoWay,
                UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged
            });
            absoluteParentBox.TextChanged += (s, e) => UpdatePreview();
            Grid.SetColumn(absoluteParentBox, 0);
            absoluteRow.Children.Add(absoluteParentBox);

            var absoluteSuffix = new TextBlock
            {
                Text = "\\CustomFolder\\",
                FontFamily = new FontFamily("Consolas"),
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 8, 0)
            };
            Grid.SetColumn(absoluteSuffix, 1);
            absoluteRow.Children.Add(absoluteSuffix);

            browseAbsoluteButton = new Button
            {
                Content = "Browse",
                MinWidth = 70,
                Height = 30
            };
            browseAbsoluteButton.Click += BrowseAbsolute_Click;
            Grid.SetColumn(browseAbsoluteButton, 2);
            absoluteRow.Children.Add(browseAbsoluteButton);

            storage.Children.Add(absoluteRow);

            changedPathWarning = new TextBlock
            {
                Foreground = Brushes.Orange,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 5, 0, 6),
                Visibility = Visibility.Collapsed
            };
            storage.Children.Add(changedPathWarning);

            safetyText = new TextBlock
            {
                Foreground = Brushes.Red,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 2, 0, 6)
            };
            storage.Children.Add(safetyText);

            var migrationPanel = new StackPanel { Margin = new Thickness(0, 10, 0, 0) };
            migrationPanel.Children.Add(new Separator { Margin = new Thickness(0, 0, 0, 10) });
            migrationPanel.Children.Add(new TextBlock
            {
                Text = "Migrate Existing Files",
                FontWeight = FontWeights.SemiBold,
                Margin = new Thickness(0, 0, 0, 6)
            });

            migrationSourceText = new TextBlock
            {
                FontFamily = new FontFamily("Consolas"),
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 0, 0, 4)
            };
            migrationPanel.Children.Add(migrationSourceText);

            migrationSourceWarning = new TextBlock
            {
                Foreground = Brushes.Red,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 0, 0, 8)
            };
            migrationPanel.Children.Add(migrationSourceWarning);

            var migrationButtons = new StackPanel { Orientation = Orientation.Horizontal };

            var browseSource = new Button
            {
                Content = "Browse Source...",
                MinWidth = 110,
                Margin = new Thickness(0, 0, 8, 0)
            };
            browseSource.Click += BrowseMigrationSource_Click;

            var migrate = new Button { Content = "Migrate", MinWidth = 90 };
            migrate.Click += Migrate_Click;

            migrationButtons.Children.Add(browseSource);
            migrationButtons.Children.Add(migrate);
            migrationPanel.Children.Add(migrationButtons);
            storage.Children.Add(migrationPanel);

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
                UpdatePathModeUi();
                UpdatePreview();
            };
        }

        private void UpdatePathModeUi()
        {
            if (CurrentSettings == null) return;

            var absolute = CurrentSettings.UseAbsolutePath;

            if (relativeModeButton != null)
            {
                relativeModeButton.Opacity = absolute ? 0.52 : 1.0;
            }

            if (relativeModeTitle != null)
            {
                relativeModeTitle.FontWeight =
                    absolute ? FontWeights.Normal : FontWeights.SemiBold;
            }

            if (relativeModeNote != null)
            {
                // Keep the recommendation visually secondary even when selected.
                relativeModeNote.Opacity = absolute ? 0.30 : 0.48;
            }

            if (absoluteModeButton != null)
            {
                absoluteModeButton.Opacity = absolute ? 1.0 : 0.52;
            }

            if (absoluteModeTitle != null)
            {
                absoluteModeTitle.FontWeight =
                    absolute ? FontWeights.SemiBold : FontWeights.Normal;
            }

            if (parentBox != null) parentBox.IsEnabled = !absolute;
            if (parentEditButton != null) parentEditButton.IsEnabled = !absolute;
            if (resetRelativeButton != null) resetRelativeButton.IsEnabled = !absolute;

            if (absoluteParentBox != null) absoluteParentBox.IsEnabled = absolute;
            if (browseAbsoluteButton != null) browseAbsoluteButton.IsEnabled = absolute;
        }

        private void ResetRelative_Click(object sender, RoutedEventArgs e)
        {
            if (CurrentSettings == null) return;

            CurrentSettings.ParentDirectory = "..\\";

            if (parentBox != null)
                parentBox.Text = "..\\";

            UpdatePreview();
        }

        private void BrowseRelative_Click(object sender, RoutedEventArgs e)
        {
            var selected = plugin.PlayniteApi.Dialogs.SelectFolder();
            if (string.IsNullOrWhiteSpace(selected)) return;

            try
            {
                var playnite = IOPath.GetFullPath(plugin.GetPlayniteDirectory()).TrimEnd('\\') + "\\";
                var chosen = IOPath.GetFullPath(selected).TrimEnd('\\') + "\\";
                var relative = Uri.UnescapeDataString(
                    new Uri(playnite).MakeRelativeUri(new Uri(chosen)).ToString())
                    .Replace('/', '\\');

                if (relative == "." || relative == ".\\")
                {
                    CurrentSettings.ParentDirectory = string.Empty;
                }
                else
                {
                    CurrentSettings.ParentDirectory =
                        relative.EndsWith("\\", StringComparison.Ordinal)
                            ? relative
                            : relative + "\\";
                }

                parentBox.Text = CurrentSettings.ParentDirectory;
            }
            catch (Exception ex)
            {
                plugin.PlayniteApi.Dialogs.ShowErrorMessage(
                    "Could not create a Playnite-relative path.\n\n" + ex.Message,
                    "CustomFolder");
            }
        }

        private void BrowseAbsolute_Click(object sender, RoutedEventArgs e)
        {
            var selected = plugin.PlayniteApi.Dialogs.SelectFolder();
            if (string.IsNullOrWhiteSpace(selected)) return;

            CurrentSettings.AbsoluteParentDirectory = selected;
            absoluteParentBox.Text = selected;
        }

        private string GetRecordedMigrationCandidate()
        {
            // A real previous path committed by Playnite Save has first priority.
            if (!string.IsNullOrWhiteSpace(CurrentSettings?.PreviousRootPath) &&
                Directory.Exists(CurrentSettings.PreviousRootPath))
            {
                return CurrentSettings.PreviousRootPath;
            }

            // If an older/broken build left a stale PreviousRootPath, use the
            // path that was actually saved when this settings session opened.
            if (!string.IsNullOrWhiteSpace(CurrentSettings?.SessionOriginalRoot))
                return CurrentSettings.SessionOriginalRoot;

            // Keep the stale value only as diagnostic information if nothing
            // better is available.
            if (!string.IsNullOrWhiteSpace(CurrentSettings?.PreviousRootPath))
                return CurrentSettings.PreviousRootPath;

            return null;
        }

        private string GetMigrationSource()
        {
            // A manually selected source is only trusted while it still exists.
            if (!string.IsNullOrWhiteSpace(migrationSourceOverride) &&
                Directory.Exists(migrationSourceOverride))
            {
                return migrationSourceOverride;
            }

            var candidate = GetRecordedMigrationCandidate();

            // Never present an invented/stale path as a valid migration source.
            if (!string.IsNullOrWhiteSpace(candidate) &&
                Directory.Exists(candidate))
            {
                return candidate;
            }

            // Check recent recorded roots as a small fallback history.
            foreach (var recent in CurrentSettings?.RecentRootPaths ?? new List<string>())
            {
                if (!string.IsNullOrWhiteSpace(recent) &&
                    Directory.Exists(recent))
                {
                    return recent;
                }
            }

            return null;
        }

        private void BrowseMigrationSource_Click(object sender, RoutedEventArgs e)
        {
            var selected = plugin.PlayniteApi.Dialogs.SelectFolder();
            if (string.IsNullOrWhiteSpace(selected)) return;

            string detected;
            try
            {
                detected = plugin.DetectCustomFolder(selected);
            }
            catch (Exception ex)
            {
                plugin.PlayniteApi.Dialogs.ShowErrorMessage(
                    "Could not inspect that location.\n\n" + ex.Message,
                    "CustomFolder Migration");
                return;
            }

            if (string.IsNullOrWhiteSpace(detected))
            {
                plugin.PlayniteApi.Dialogs.ShowMessage(
                    "No CustomFolder was detected there.\n\n" +
                    "Select either the CustomFolder itself or the folder containing it.",
                    "CustomFolder Migration");
                return;
            }

            migrationSourceOverride = detected;
            UpdatePreview();
        }

        private void Migrate_Click(object sender, RoutedEventArgs e)
        {
            var source = GetMigrationSource();
            string destination;

            try
            {
                destination = plugin.ResolveCurrentRoot(CurrentSettings);
            }
            catch (Exception ex)
            {
                plugin.PlayniteApi.Dialogs.ShowErrorMessage(
                    "Destination path is invalid.\n\n" + ex.Message,
                    "CustomFolder Migration");
                return;
            }

            if (string.IsNullOrWhiteSpace(source) || !Directory.Exists(source))
            {
                plugin.PlayniteApi.Dialogs.ShowMessage(
                    "No existing migration source was detected.\n\n" +
                    "The saved old path does not exist, or is no longer available. " +
                    "Use Browse Source to choose the correct old CustomFolder location.",
                    "CustomFolder Migration");
                return;
            }

            if (plugin.IsSamePath(source, destination))
            {
                plugin.PlayniteApi.Dialogs.ShowMessage(
                    "Source and destination are the same. Nothing needs to be migrated.",
                    "CustomFolder Migration");
                return;
            }

            var crossDrive = !string.Equals(
                IOPath.GetPathRoot(source),
                IOPath.GetPathRoot(destination),
                StringComparison.OrdinalIgnoreCase);

            var message =
                "Migrate existing files?\n\n" +
                "Source:\n" + source + "\n\n" +
                "Destination:\n" + destination + "\n\n" +
                "Existing destination files will NOT be overwritten.";

            if (crossDrive)
                message += "\n\nThis crosses drives, so files will be copied and then removed from the old location.";

            var confirm = MessageBox.Show(
                message,
                "CustomFolder Migration",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (confirm != MessageBoxResult.Yes) return;

            try
            {
                var result = plugin.MigrateFolder(source, destination);

                CurrentSettings.RecordSuccessfulRootChange(source, destination);
                migrationSourceOverride = null;

                plugin.PlayniteApi.Dialogs.ShowMessage(
                    "Migration complete.\n\n" +
                    "Moved files: " + result.MovedFiles + "\n" +
                    "Skipped existing files: " + result.SkippedFiles +
                    (result.SkippedFiles > 0
                        ? "\n\nSkipped files remain in the old location."
                        : string.Empty),
                    "CustomFolder Migration");

                UpdatePreview();
            }
            catch (Exception ex)
            {
                plugin.PlayniteApi.Dialogs.ShowErrorMessage(
                    "Migration failed.\n\n" + ex.Message,
                    "CustomFolder Migration");
            }
        }

        private void UpdatePreview()
        {
            if (previewText == null || CurrentSettings == null) return;

            var preset =
                presetList?.SelectedItem?.ToString()
                ?? CurrentSettings.Presets?.FirstOrDefault()
                ?? "Downloads";

            string currentRoot = null;

            try
            {
                currentRoot = plugin.ResolveCurrentRoot(CurrentSettings);
                previewText.Text = IOPath.Combine(
                    currentRoot,
                    CustomFolderPlugin.MakeSafeFolderName(preset),
                    "Ghost of Tsushima");

                safetyText.Text = plugin.IsInsidePlayniteDirectory(currentRoot)
                    ? "Playnite 11 warning: this CustomFolder is inside the Playnite directory. Move it outside Playnite before updating to avoid possible data loss."
                    : string.Empty;
            }
            catch (Exception ex)
            {
                previewText.Text = "Invalid path";
                safetyText.Text = "Invalid directory: " + ex.Message;
            }

            var oldRoot = GetMigrationSource();
            var recordedCandidate = GetRecordedMigrationCandidate();

            if (!string.IsNullOrWhiteSpace(currentRoot) &&
                !string.IsNullOrWhiteSpace(recordedCandidate) &&
                !plugin.IsSamePath(currentRoot, recordedCandidate))
            {
                changedPathWarning.Visibility = Visibility.Visible;

                if (Directory.Exists(recordedCandidate))
                {
                    changedPathWarning.Text =
                        "You changed the CustomFolder path. Use Migrate to avoid leaving files behind.\n" +
                        "Old path: " + recordedCandidate;
                }
                else
                {
                    changedPathWarning.Text =
                        "The saved old CustomFolder path could not be found. " +
                        "Use Browse Source in Migrate if your files are somewhere else.\n" +
                        "Recorded old path: " + recordedCandidate;
                }
            }
            else
            {
                changedPathWarning.Visibility = Visibility.Collapsed;
            }

            if (!string.IsNullOrWhiteSpace(oldRoot))
            {
                migrationSourceText.Text =
                    "Source: " + oldRoot +
                    "\nDestination: " +
                    (string.IsNullOrWhiteSpace(currentRoot) ? "Invalid path" : currentRoot);
            }
            else
            {
                migrationSourceText.Text =
                    "Source: Not detected" +
                    (!string.IsNullOrWhiteSpace(recordedCandidate)
                        ? "\nRecorded old path (not found): " + recordedCandidate
                        : string.Empty) +
                    "\nDestination: " +
                    (string.IsNullOrWhiteSpace(currentRoot) ? "Invalid path" : currentRoot);
            }

            migrationSourceWarning.Text =
                !string.IsNullOrWhiteSpace(oldRoot) && plugin.IsInsidePlayniteDirectory(oldRoot)
                ? "Playnite 11 warning: this migration source is inside the Playnite directory."
                : string.Empty;

            UpdatePathModeUi();
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