using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Media.Animation;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Xml.Linq;
using System.Xml;
using System.Threading.Tasks;
using System.Threading;
using System.Diagnostics;
using WinForms = System.Windows.Forms;

namespace RedefinedVehicleImporter
{
    internal static class PremiumMdtProgram
    {
        [STAThread]
        private static void Main()
        {
            new Application().Run(new PremiumMdtWindow());
        }
    }

    internal sealed class PremiumMdtWindow : Window
    {
        private readonly Color _ink = Color.FromRgb(9, 12, 18);
        private readonly Color _surface = Color.FromRgb(19, 25, 36);
        private readonly Brush _text = B("#EFF6FF");
        private readonly Brush _muted = B("#90A0B8");
        private readonly Brush _accent = B("#21D4C2");
        private readonly ListView _table = new ListView();
        private readonly List<PremiumVehicle> _vehicles = new List<PremiumVehicle>();
        private readonly List<DepartmentTarget> _departments = new List<DepartmentTarget>();
        private readonly List<DetectedDlcPack> _detectedDlcPacks = new List<DetectedDlcPack>();
        private readonly Dictionary<string, string> _xmlPaths = new Dictionary<string, string>();
        private string _queueFilter = string.Empty;
        private string _gtaFolder = string.Empty;
        private NativeVehiclePreview _nativeVehiclePreview;
        private CancellationTokenSource _previewLoadCancellation;
        private Grid _root;
        private Border _vehicleLibraryOverlay;
        private readonly Border _progressPanel = new Border();
        private readonly ProgressBar _progressBar = new ProgressBar();
        private readonly TextBlock _progressLabel = new TextBlock();
        private readonly List<string> _activityLog = new List<string>();
        private TextBlock _profileStatus;
        private TextBlock _deploymentTitle;
        private TextBlock _deploymentDetail;
        private TextBlock _activityHeadline;
        private TextBlock _activityDetail;
        private ListBox _currentZoneVehicleList;
        private TextBlock _currentZoneStatus;
        private DepartmentTarget _currentZoneSelection;
        private string _language = LoadLanguage();

        public PremiumMdtWindow()
        {
            Title = "Renegades XML Editor"; WindowState = WindowState.Maximized; MinWidth = 1120; MinHeight = 740; Background = new SolidColorBrush(_ink);
            var root = new Grid { Margin = new Thickness(26), AllowDrop = true }; _root = root; root.DragEnter += VehicleDragEnter; root.Drop += VehicleDrop;
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); root.RowDefinitions.Add(new RowDefinition());

            var header = new Grid { Margin = new Thickness(6, 0, 6, 25) }; header.ColumnDefinitions.Add(new ColumnDefinition()); header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            var brand = new StackPanel(); brand.Children.Add(T("RENEGADES XML EDITOR", 26, _text, FontWeights.SemiBold)); brand.Children.Add(T("Vehicle deployment & configuration workspace", 13, _muted, FontWeights.Normal, new Thickness(0, 5, 0, 0))); header.Children.Add(brand);
            var headerTools = new StackPanel { Orientation = Orientation.Horizontal }; var language = new ComboBox { Width = 190, Height = 34, Background = B("#EAF0F6"), Foreground = B("#0B1220"), BorderBrush = B("#2A3A50"), ItemsSource = SupportedLanguages, DisplayMemberPath = "Name", ToolTip = "Choose the display language" }; language.SelectedValuePath = "Code"; language.SelectedValue = _language; language.SelectionChanged += delegate { var selected = language.SelectedItem as LanguageChoice; if (selected == null || selected.Code == _language) return; _language = selected.Code; SaveLanguage(_language); ApplyLanguage(this); }; headerTools.Children.Add(language); _profileStatus = T("XML FILES NOT CONNECTED", 12, B("#F9B960"), FontWeights.SemiBold); var profile = new Border { Background = B("#15263A"), CornerRadius = new CornerRadius(18), Padding = new Thickness(14, 8, 14, 8), Margin = new Thickness(10, 0, 0, 0), Child = _profileStatus }; headerTools.Children.Add(profile); Grid.SetColumn(headerTools, 1); header.Children.Add(headerTools); root.Children.Add(header);

            var nav = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(6, 0, 6, 20) };
            var overview = Tab("Overview", true); var library = Tab("Vehicle library", false); var agencies = Tab("Agency setup", false); var dlc = Tab("Installed DLCs", false); var deployments = Tab("Assignments", false); var activity = Tab("Activity", false);
            overview.Click += delegate { SetNav(overview, library, agencies, dlc, deployments, activity); };
            library.Click += delegate { SetNav(library, overview, agencies, dlc, deployments, activity); OpenVehicleLibrary(); };
            agencies.Click += delegate { SetNav(agencies, overview, library, dlc, deployments, activity); OpenAgencySetup(); };
            dlc.Click += delegate { SetNav(dlc, overview, library, agencies, deployments, activity); OpenDlcDetection(); };
            deployments.Click += delegate { SetNav(deployments, overview, library, agencies, dlc, activity); OpenAssignments(); };
            activity.Click += delegate { SetNav(activity, overview, library, agencies, dlc, deployments); ShowActivity(); };
            nav.Children.Add(overview); nav.Children.Add(library); nav.Children.Add(agencies); nav.Children.Add(dlc); nav.Children.Add(deployments); nav.Children.Add(activity); Grid.SetRow(nav, 1); root.Children.Add(nav);

            _progressPanel.Visibility = Visibility.Collapsed; _progressPanel.Background = B("#10283B"); _progressPanel.CornerRadius = new CornerRadius(12); _progressPanel.Padding = new Thickness(16, 12, 16, 12); _progressPanel.Margin = new Thickness(6, 0, 6, 16); var progressLayout = new Grid(); progressLayout.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto }); progressLayout.ColumnDefinitions.Add(new ColumnDefinition()); _progressLabel.Foreground = _text; _progressLabel.FontSize = 13; _progressLabel.FontWeight = FontWeights.SemiBold; _progressLabel.VerticalAlignment = VerticalAlignment.Center; progressLayout.Children.Add(_progressLabel); _progressBar.Height = 8; _progressBar.Margin = new Thickness(22, 0, 0, 0); _progressBar.Foreground = _accent; _progressBar.Background = B("#1D3D54"); _progressBar.Minimum = 0; _progressBar.Maximum = 100; Grid.SetColumn(_progressBar, 1); progressLayout.Children.Add(_progressBar); _progressPanel.Child = progressLayout; Grid.SetRow(_progressPanel, 2); root.Children.Add(_progressPanel);

            var content = new Grid(); content.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.35, GridUnitType.Star) }); content.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(0.65, GridUnitType.Star) }); content.RowDefinitions.Add(new RowDefinition { Height = new GridLength(0.45, GridUnitType.Star) }); content.RowDefinitions.Add(new RowDefinition { Height = new GridLength(0.55, GridUnitType.Star) }); Grid.SetRow(content, 3); root.Children.Add(content);

            var hero = Card(); var heroStack = new StackPanel { Margin = new Thickness(26) }; heroStack.Children.Add(T("Ready to build your fleet?", 25, _text, FontWeights.SemiBold)); heroStack.Children.Add(T("Import add-on DLC packs, assign every vehicle to an agency, and deploy a reviewed configuration to Policing Redefined.", 14, _muted, FontWeights.Normal, new Thickness(0, 10, 0, 22))); var heroActions = new StackPanel { Orientation = Orientation.Horizontal }; var scan = Primary("Import vehicle pack"); scan.Click += ScanPack; var setup = Ghost("Agency setup"); setup.Click += delegate { OpenAgencySetup(); }; heroActions.Children.Add(scan); heroActions.Children.Add(setup); heroStack.Children.Add(heroActions); hero.Child = heroStack; content.Children.Add(hero);

            var status = Card(); status.Margin = new Thickness(18, 0, 0, 0); var statusStack = new StackPanel { Margin = new Thickness(24) }; statusStack.Children.Add(T("DEPLOYMENT STATUS", 11, _accent, FontWeights.SemiBold)); _deploymentTitle = T("No assignments yet", 21, _text, FontWeights.SemiBold, new Thickness(0, 14, 0, 4)); _deploymentDetail = T("Connect your Policing Redefined XML files to begin.", 13, _muted, FontWeights.Normal); statusStack.Children.Add(_deploymentTitle); statusStack.Children.Add(_deploymentDetail); status.Child = statusStack; Grid.SetColumn(status, 1); content.Children.Add(status);

            var queue = Card(); queue.Margin = new Thickness(0, 18, 18, 0); var queueGrid = new Grid { Margin = new Thickness(24) }; queueGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); queueGrid.RowDefinitions.Add(new RowDefinition()); var qHead = new Grid(); qHead.ColumnDefinitions.Add(new ColumnDefinition()); qHead.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto }); qHead.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto }); var queueTitle = new StackPanel(); queueTitle.Children.Add(T("Vehicle queue", 19, _text, FontWeights.SemiBold)); queueTitle.Children.Add(T("Your imported vehicles, ready for agency assignment", 12, _muted, FontWeights.Normal, new Thickness(0, 4, 0, 0))); qHead.Children.Add(queueTitle); var search = new TextBox { Width = 190, Height = 34, Background = B("#0D1420"), Foreground = _text, BorderBrush = B("#2A3A50"), Padding = new Thickness(10, 6, 10, 6), ToolTip = "Search vehicle model or source pack" }; search.TextChanged += delegate { _queueFilter = search.Text; RenderVehicles(); }; Grid.SetColumn(search, 1); qHead.Children.Add(search); var expand = Ghost("Open full screen"); expand.Margin = new Thickness(10, 0, 0, 0); expand.Click += delegate { OpenVehicleLibrary(); }; Grid.SetColumn(expand, 2); qHead.Children.Add(expand); queueGrid.Children.Add(qHead); ConfigureVehicleTable(_table); _table.Margin = new Thickness(0, 18, 0, 0); Grid.SetRow(_table, 1); queueGrid.Children.Add(_table); queue.Child = queueGrid; Grid.SetRow(queue, 1); content.Children.Add(queue);

            var feed = Card(); feed.Margin = new Thickness(0, 18, 0, 0); var feedStack = new StackPanel { Margin = new Thickness(24) }; feedStack.Children.Add(T("RECENT ACTIVITY", 11, _accent, FontWeights.SemiBold)); _activityHeadline = T("No activity yet", 15, _text, FontWeights.Medium, new Thickness(0, 20, 0, 3)); _activityDetail = T("Import a vehicle pack or connect your XML files to get started.", 12, _muted, FontWeights.Normal); feedStack.Children.Add(_activityHeadline); feedStack.Children.Add(_activityDetail); feed.Child = feedStack; Grid.SetRow(feed, 1); Grid.SetColumn(feed, 1); content.Children.Add(feed);
            Content = root;
            RefreshDashboard();
            Loaded += delegate { Dispatcher.BeginInvoke(new Action(ChooseGtaFolderOnStartup)); };
        }

        private void ChooseGtaFolderOnStartup()
        {
            var remembered = LoadGtaFolder();
            using (var picker = new WinForms.FolderBrowserDialog { Description = "Select your GTA V installation folder. Renegades will use it to find your installed DLC packs and Policing Redefined XML files.", SelectedPath = Directory.Exists(remembered) ? remembered : string.Empty, ShowNewFolderButton = false })
            {
                if (picker.ShowDialog() == WinForms.DialogResult.OK)
                {
                    if (!ConnectGtaFolder(picker.SelectedPath)) MessageBox.Show("That folder does not look like a GTA V installation. Select the folder containing GTA5.exe and update\\update.rpf.", "GTA V folder not recognized", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
                else if (IsValidGtaFolder(remembered)) ConnectGtaFolder(remembered);
            }
        }
        private bool ConnectGtaFolder(string path)
        {
            if (!IsValidGtaFolder(path)) return false;
            _gtaFolder = path;
            SaveGtaFolder(path);
            var found = FindPolicingRedefinedFiles(path);
            foreach (var pair in found) _xmlPaths[pair.Key] = pair.Value;
            RefreshDepartments();
            LogActivity("Selected GTA V folder and connected " + found.Count + " Policing Redefined XML file(s)");
            return true;
        }

        private Border Card() { return new Border { Background = new SolidColorBrush(_surface), CornerRadius = new CornerRadius(16), Effect = new DropShadowEffect { Color = Colors.Black, BlurRadius = 18, ShadowDepth = 5, Opacity = .25 } }; }
        private void SetNav(Button active, params Button[] inactive) { active.Background = B("#17304A"); active.Foreground = _accent; foreach (var button in inactive) { button.Background = Brushes.Transparent; button.Foreground = _muted; } }
        private void LogActivity(string message)
        {
            _activityLog.Insert(0, DateTime.Now.ToString("yyyy-MM-dd HH:mm") + "  " + message);
            if (_activityLog.Count > 100) _activityLog.RemoveAt(_activityLog.Count - 1);
            RefreshDashboard();
        }
        private void RefreshDashboard()
        {
            var connected = new[] { "DefaultRegions.xml", "SpecialUnits.xml", "CustomRegions.xml" }.Count(name => _xmlPaths.ContainsKey(name) && File.Exists(_xmlPaths[name]));
            _profileStatus.Text = connected == 3 ? "3 XML FILES CONNECTED" : connected + " OF 3 XML FILES CONNECTED";
            _profileStatus.Foreground = connected == 3 ? _accent : B("#F9B960");
            var assigned = _vehicles.Sum(vehicle => vehicle.Departments.Count);
            _deploymentTitle.Text = assigned == 0 ? "No assignments yet" : assigned + " vehicle assignment" + (assigned == 1 ? string.Empty : "s");
            _deploymentDetail.Text = connected + " of 3 XML files connected · " + _vehicles.Count + " vehicle(s) queued";
            if (_activityLog.Count == 0)
            {
                _activityHeadline.Text = "No activity yet";
                _activityDetail.Text = "Import a vehicle pack or connect your XML files to get started.";
            }
            else
            {
                _activityHeadline.Text = _activityLog[0].Substring(18);
                _activityDetail.Text = _activityLog[0].Substring(0, 16) + " · View Activity for the session log";
            }
        }
        private void ShowActivity()
        {
            var content = _activityLog.Count == 0 ? "No activity yet. Import a vehicle pack, connect XML files, or assign vehicles to record session activity." : string.Join(Environment.NewLine + Environment.NewLine, _activityLog);
            MessageBox.Show(content, "Session activity", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        private void ScanPack(object sender, RoutedEventArgs e) { OpenPackImport(); }
        private void ConnectXml(object sender, RoutedEventArgs e)
        {
            foreach (var label in new[] { "DefaultRegions.xml", "SpecialUnits.xml", "CustomRegions.xml" }) using (var open = new WinForms.OpenFileDialog { Filter = label + "|" + label + "|XML files|*.xml", Title = "Choose " + label }) { if (open.ShowDialog() == WinForms.DialogResult.OK) _xmlPaths[label] = open.FileName; }
            RefreshDepartments();
            LogActivity(_xmlPaths.Count + " Policing Redefined XML file(s) connected");
            MessageBox.Show(_xmlPaths.Count + " XML files connected.", "LSPDFR Command");
        }
        private void AutoLocateXmlFiles()
        {
            var found = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var root in GetLikelyGtaFolders())
            {
                foreach (var pair in FindPolicingRedefinedFiles(root)) found[pair.Key] = pair.Value;
                if (found.Count == 3) break;
            }
            foreach (var pair in found) _xmlPaths[pair.Key] = pair.Value;
            RefreshDepartments();
            if (found.Count > 0) LogActivity("Automatically connected " + found.Count + " Policing Redefined XML file(s)");
            if (found.Count == 3)
            {
                MessageBox.Show("Agency setup found and connected all Policing Redefined XML files automatically.", "GTA V files found");
                return;
            }
            var missing = new[] { "DefaultRegions.xml", "SpecialUnits.xml", "CustomRegions.xml" }.Where(name => !_xmlPaths.ContainsKey(name)).ToArray();
            var result = MessageBox.Show("Agency setup found " + found.Count + " of 3 XML files. Missing: " + string.Join(", ", missing) + ".\n\nWould you like to select your GTA V folder so Command can search it?", "GTA V scan complete", MessageBoxButton.YesNo, MessageBoxImage.Information);
            if (result == MessageBoxResult.Yes) BrowseForGtaFolder();
        }
        private void BrowseForGtaFolder()
        {
            using (var picker = new WinForms.FolderBrowserDialog { Description = "Select your Grand Theft Auto V installation folder" })
            {
                if (picker.ShowDialog() != WinForms.DialogResult.OK) return;
                _gtaFolder = picker.SelectedPath;
                SaveGtaFolder(_gtaFolder);
                var found = FindPolicingRedefinedFiles(picker.SelectedPath);
                foreach (var pair in found) _xmlPaths[pair.Key] = pair.Value;
                RefreshDepartments();
                var missing = new[] { "DefaultRegions.xml", "SpecialUnits.xml", "CustomRegions.xml" }.Where(name => !_xmlPaths.ContainsKey(name)).ToArray();
                if (missing.Length == 0)
                {
                    MessageBox.Show("All Policing Redefined XML files were found and connected.", "GTA V folder scanned");
                    return;
                }
                var pickFiles = MessageBox.Show("Found " + found.Count + " XML file(s) in the selected GTA V folder. Missing: " + string.Join(", ", missing) + ".\n\nChoose the remaining XML files manually?", "Some files not found", MessageBoxButton.YesNo, MessageBoxImage.Warning);
                if (pickFiles == MessageBoxResult.Yes) ConnectXml(this, null);
            }
        }
        private static IEnumerable<string> GetLikelyGtaFolders()
        {
            var candidates = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var drive in DriveInfo.GetDrives().Where(d => d.IsReady && d.DriveType == DriveType.Fixed))
            {
                var root = drive.RootDirectory.FullName;
                candidates.Add(Path.Combine(root, "Program Files", "Rockstar Games", "Grand Theft Auto V"));
                candidates.Add(Path.Combine(root, "Program Files", "Epic Games", "GTAV"));
                candidates.Add(Path.Combine(root, "Program Files (x86)", "Steam", "steamapps", "common", "Grand Theft Auto V"));
                candidates.Add(Path.Combine(root, "SteamLibrary", "steamapps", "common", "Grand Theft Auto V"));
                candidates.Add(Path.Combine(root, "Games", "Grand Theft Auto V"));
            }
            return candidates.Where(Directory.Exists);
        }
        private static DlcScanResult FindInstalledDlcPacks(string gtaFolder)
        {
            var result = new DlcScanResult();
            var modsRoot = Path.Combine(gtaFolder, "mods", "update", "x64", "dlcpacks");
            var baseGameRoot = Path.Combine(gtaFolder, "update", "x64", "dlcpacks");
            var defaultPackNames = GetDefaultDlcPackNames(baseGameRoot, result);
            if (!Directory.Exists(modsRoot)) return result;
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var pending = new Stack<string>(); pending.Push(modsRoot);
            while (pending.Count > 0)
            {
                var directory = pending.Pop();
                if (IsBlacklistedDlcPack(directory))
                {
                    var excludedName = Path.GetFileName(directory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
                    if (IsPatchdayPackName(excludedName)) result.ExcludedPatchdayFolders++;
                    continue;
                }
                var folderName = Path.GetFileName(directory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
                if (defaultPackNames.Contains(folderName)) { result.ExcludedDefaultFolders++; continue; }
                try
                {
                    foreach (var file in Directory.GetFiles(directory, "dlc.rpf", SearchOption.TopDirectoryOnly))
                    {
                        var packName = Path.GetFileName(Path.GetDirectoryName(file));
                        if (string.IsNullOrWhiteSpace(packName) || IsBlacklistedDlcPack(Path.GetDirectoryName(file)) || defaultPackNames.Contains(packName) || !seen.Add(file)) continue;
                        result.Packs.Add(new DetectedDlcPack { Name = packName, FilePath = file });
                    }
                }
                catch (UnauthorizedAccessException) { result.InaccessibleFolders++; }
                catch (IOException) { result.InaccessibleFolders++; }
                try
                {
                    foreach (var child in Directory.GetDirectories(directory))
                    {
                        var childName = Path.GetFileName(child.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
                        if (IsBlacklistedDlcPack(child))
                        {
                            if (IsPatchdayPackName(childName)) result.ExcludedPatchdayFolders++;
                            continue;
                        }
                        if (defaultPackNames.Contains(childName)) { result.ExcludedDefaultFolders++; continue; }
                        pending.Push(child);
                    }
                }
                catch (UnauthorizedAccessException) { result.InaccessibleFolders++; }
                catch (IOException) { result.InaccessibleFolders++; }
            }
            result.Packs = result.Packs.OrderBy(pack => pack.Name, StringComparer.OrdinalIgnoreCase).ThenBy(pack => pack.FilePath, StringComparer.OrdinalIgnoreCase).ToList();
            return result;
        }
        private static HashSet<string> GetDefaultDlcPackNames(string baseGameRoot, DlcScanResult result)
        {
            var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (!Directory.Exists(baseGameRoot)) return names;
            try
            {
                foreach (var directory in Directory.GetDirectories(baseGameRoot))
                {
                    var name = Path.GetFileName(directory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
                    if (IsBlacklistedDlcPack(directory)) continue;
                    try { if (File.Exists(Path.Combine(directory, "dlc.rpf"))) names.Add(name); }
                    catch (UnauthorizedAccessException) { result.InaccessibleFolders++; }
                    catch (IOException) { result.InaccessibleFolders++; }
                }
            }
            catch (UnauthorizedAccessException) { result.InaccessibleFolders++; }
            catch (IOException) { result.InaccessibleFolders++; }
            return names;
        }
        private static bool IsBlacklistedDlcPack(string directory)
        {
            if (string.IsNullOrWhiteSpace(directory)) return false;
            var name = Path.GetFileName(directory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
            return string.Equals(name, "eup", StringComparison.OrdinalIgnoreCase) || string.Equals(name, "sup", StringComparison.OrdinalIgnoreCase) || IsPatchdayPackName(name);
        }
        private static bool IsPatchdayPackName(string name) { return !string.IsNullOrWhiteSpace(name) && name.StartsWith("patchday", StringComparison.OrdinalIgnoreCase); }
        private static Dictionary<string, string> FindPolicingRedefinedFiles(string root)
        {
            var wanted = new HashSet<string>(new[] { "DefaultRegions.xml", "SpecialUnits.xml", "CustomRegions.xml" }, StringComparer.OrdinalIgnoreCase);
            var found = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var pending = new Stack<string>(); pending.Push(root);
            while (pending.Count > 0 && found.Count < wanted.Count)
            {
                var directory = pending.Pop();
                try
                {
                    foreach (var file in Directory.GetFiles(directory, "*.xml")) { var name = Path.GetFileName(file); if (wanted.Contains(name) && !found.ContainsKey(name)) found[name] = file; }
                    foreach (var child in Directory.GetDirectories(directory)) pending.Push(child);
                }
                catch (UnauthorizedAccessException) { }
                catch (IOException) { }
            }
            return found;
        }
        private void VehicleDragEnter(object sender, DragEventArgs e)
        {
            e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop) ? DragDropEffects.Copy : DragDropEffects.None;
            e.Handled = true;
        }
        private async void VehicleDrop(object sender, DragEventArgs e)
        {
            var paths = e.Data.GetData(DataFormats.FileDrop) as string[];
            if (paths != null) await AddPackFiles(paths);
        }
        private async Task AddPackFiles(IEnumerable<string> paths)
        {
            var packs = new List<string>();
            foreach (var path in paths)
            {
                if (File.Exists(path) && string.Equals(Path.GetExtension(path), ".rpf", StringComparison.OrdinalIgnoreCase) && !IsBlacklistedDlcPack(Path.GetDirectoryName(path))) packs.Add(path);
                else if (Directory.Exists(path))
                {
                    try { packs.AddRange(Directory.EnumerateFiles(path, "*.rpf", SearchOption.AllDirectories).Where(file => string.Equals(Path.GetFileName(file), "dlc.rpf", StringComparison.OrdinalIgnoreCase) && !IsBlacklistedDlcPack(Path.GetDirectoryName(file)))); }
                    catch (UnauthorizedAccessException) { }
                }
            }
            packs = packs.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            if (packs.Count == 0) { MessageBox.Show("No vehicle DLC.RPF files were found. EUP and SUP clothing packs are excluded.", "No vehicle packs found", MessageBoxButton.OK, MessageBoxImage.Information); return; }
            var added = 0; var failures = new List<string>(); SetProgress(true, "Preparing vehicle-pack scan", 0);
            for (var packIndex = 0; packIndex < packs.Count; packIndex++)
            {
                var pack = packs[packIndex]; SetProgress(true, "Scanning " + Path.GetFileName(pack) + "  ·  " + (packIndex + 1) + " of " + packs.Count, (packIndex * 100.0) / packs.Count);
                try
                {
                    var source = Path.GetFileName(Path.GetDirectoryName(pack));
                    var models = await Task.Run(() => RpfReader.ReadVehicleModels(pack));
                    foreach (var model in models)
                        if (!_vehicles.Any(v => v.Model.Equals(model, StringComparison.OrdinalIgnoreCase) && v.Source.Equals(source, StringComparison.OrdinalIgnoreCase))) { _vehicles.Add(new PremiumVehicle { Model = model, Source = source, PackPath = pack }); added++; }
                }
                catch (Exception ex) { failures.Add(Path.GetFileName(pack) + ": " + ex.Message); }
            }
            RenderVehicles(); RefreshDashboard(); LogActivity("Added " + added + " vehicle(s) from " + packs.Count + " pack(s)"); SetProgress(true, "Vehicle scan complete", 100); SetProgress(false, string.Empty, 0);
            MessageBox.Show(added + " vehicle(s) added from " + packs.Count + " pack(s)." + (failures.Count > 0 ? "\n\nCould not read:\n" + string.Join("\n", failures.Take(3)) : string.Empty), "Vehicle import complete", MessageBoxButton.OK, failures.Count > 0 ? MessageBoxImage.Warning : MessageBoxImage.Information);
        }
        private void RefreshDepartments()
        {
            _departments.Clear();
            AddDepartments("DefaultRegions.xml", "Default", doc => doc.Descendants("LocalPatrol").Elements().Select(x => new DepartmentTarget(_xmlPaths["DefaultRegions.xml"], "Default", x.Name.LocalName, "Default Regions · " + x.Name.LocalName)));
            AddDepartments("SpecialUnits.xml", "Special", doc => doc.Descendants("SpecialUnit").Select(x => new DepartmentTarget(_xmlPaths["SpecialUnits.xml"], "Special", (string)x.Attribute("name"), "Special Units · " + ((string)x.Attribute("name") ?? "Unnamed unit"))));
            AddDepartments("CustomRegions.xml", "Custom", doc => doc.Descendants("LocalPatrol").Elements("CustomRegion").Select(x => new DepartmentTarget(_xmlPaths["CustomRegions.xml"], "Custom", (string)x.Attribute("name"), "Custom Regions · " + ((string)x.Attribute("name") ?? "Unnamed region"))));
            RefreshDashboard();
        }
        private void AddDepartments(string xmlName, string kind, Func<XDocument, IEnumerable<DepartmentTarget>> reader)
        {
            if (!_xmlPaths.ContainsKey(xmlName) || !File.Exists(_xmlPaths[xmlName])) return;
            try { _departments.AddRange(reader(XDocument.Load(_xmlPaths[xmlName]))); } catch { }
        }
        private void AssignSelectedVehicles(ListView table, IEnumerable<DepartmentTarget> agencies)
        {
            var selected = table.SelectedItems.Cast<PremiumVehicle>().ToList();
            var selectedAgencies = (agencies ?? Enumerable.Empty<DepartmentTarget>()).ToList();
            if (selectedAgencies.Count == 0) { MessageBox.Show("Choose one or more patrol zones first.", "Choose patrol zones", MessageBoxButton.OK, MessageBoxImage.Information); return; }
            if (selected.Count == 0) { MessageBox.Show("Select one or more vehicles in the table first.", "Select vehicles", MessageBoxButton.OK, MessageBoxImage.Information); return; }
            foreach (var vehicle in selected) foreach (var agency in selectedAgencies) if (!vehicle.Departments.Any(d => d.FilePath == agency.FilePath && d.Id == agency.Id)) vehicle.Departments.Add(agency);
            RenderVehicles(); PopulateVehicleTable(table, string.Empty);
            LogActivity("Assigned " + selected.Count + " vehicle(s) to " + selectedAgencies.Count + " patrol zone(s)");
            MessageBox.Show(selected.Count + " vehicle(s) assigned to " + selectedAgencies.Count + " patrol zone(s).", "Assignment saved", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        private void UnassignSelectedVehicles(ListView table)
        {
            var selected = table.SelectedItems.Cast<PremiumVehicle>().ToList();
            if (selected.Count == 0) { MessageBox.Show("Select one or more vehicles to unassign.", "Select vehicles", MessageBoxButton.OK, MessageBoxImage.Information); return; }
            foreach (var vehicle in selected) vehicle.Departments.Clear();
            RenderVehicles(); PopulateVehicleTable(table, string.Empty);
            LogActivity("Unassigned " + selected.Count + " vehicle(s)");
            MessageBox.Show(selected.Count + " vehicle(s) returned to the unassigned queue.", "Vehicles unassigned", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        private async void ImportAssignments()
        {
            var assignments = _vehicles.Where(v => v.Departments.Count > 0).ToList();
            if (assignments.Count == 0) { MessageBox.Show("Assign at least one vehicle to an agency before importing.", "Nothing to import", MessageBoxButton.OK, MessageBoxImage.Information); return; }
            if (MessageBox.Show("The editor will validate every assigned agency, create a timestamped backup beside each XML file, and restore already-updated files if a later file cannot be saved.\n\nContinue and replace the vehicles in each assigned agency?", "Ready to update XML files", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return;
            try { SetProgress(true, "Preparing XML import", 10); var saved = await Task.Run(() => UpdateDepartments(assignments)); SetProgress(true, "XML import complete", 100); LogActivity("Imported " + assignments.Count + " assigned vehicle(s); automatic backups were created"); RefreshCurrentZoneVehicles(); MessageBox.Show("Import complete.\n\n" + string.Join("\n", saved), "XML files updated", MessageBoxButton.OK, MessageBoxImage.Information); }
            catch (Exception ex) { MessageBox.Show("The import could not complete.\n\n" + ex.Message, "Import error", MessageBoxButton.OK, MessageBoxImage.Error); }
            finally { SetProgress(false, string.Empty, 0); }
        }
        private void SetProgress(bool visible, string message, double value)
        {
            _progressPanel.Visibility = visible ? Visibility.Visible : Visibility.Collapsed;
            _progressLabel.Text = message; _progressBar.Value = Math.Max(0, Math.Min(100, value));
        }
        private static List<string> UpdateDepartments(IList<PremiumVehicle> assignments)
        {
            if (assignments == null) throw new ArgumentNullException("assignments");
            var plans = new List<PendingXmlUpdate>();
            var committed = new List<PendingXmlUpdate>();
            try
            {
                // Load, validate, and serialize every proposed file before touching an original.
                foreach (var file in assignments.SelectMany(v => v.Departments.Select(d => new VehicleZoneAssignment { Vehicle = v, Department = d })).GroupBy(v => v.Department.FilePath))
                {
                    var path = file.Key;
                    if (string.IsNullOrWhiteSpace(path) || !File.Exists(path)) throw new FileNotFoundException("The selected Policing Redefined XML file no longer exists.", path);
                    var originalBytes = File.ReadAllBytes(path);
                    XDocument doc;
                    using (var source = new MemoryStream(originalBytes)) doc = XDocument.Load(source);
                    foreach (var agency in file.GroupBy(v => v.Department.Id))
                    {
                        var target = agency.First().Department; var element = FindDepartment(doc, target);
                        if (element == null) throw new InvalidDataException("Could not find " + target.Display + " in " + Path.GetFileName(path) + ". The file may have changed since it was selected.");
                        var lists = element.Descendants("Vehicles").ToList();
                        if (lists.Count == 0) throw new InvalidDataException("No Vehicles list exists for " + target.Display + " in " + Path.GetFileName(path) + ". No files were changed.");
                        var models = agency.Select(v => v.Vehicle.Model).Where(x => !string.IsNullOrWhiteSpace(x)).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
                        if (models.Count == 0) throw new InvalidDataException("No valid vehicle models are assigned to " + target.Display + ". No files were changed.");
                        foreach (var list in lists)
                        {
                            list.RemoveNodes();
                            for (var index = 0; index < models.Count; index++) list.Add(new XElement("Vehicle", new XAttribute("pax", "1"), new XAttribute("pax_chance", "30"), new XAttribute("chance", index == models.Count - 1 ? 100 - (100 / models.Count) * index : 100 / models.Count), models[index]));
                        }
                    }
                    var directory = Path.GetDirectoryName(path);
                    var suffix = DateTime.Now.ToString("yyyyMMdd-HHmmss-fff") + "-" + Guid.NewGuid().ToString("N").Substring(0, 8);
                    var plan = new PendingXmlUpdate { Path = path, TempPath = Path.Combine(directory, ".renegades-" + suffix + ".tmp"), BackupPath = path + ".backup-" + suffix, OriginalBytes = originalBytes };
                    var settings = new XmlWriterSettings { Indent = true, IndentChars = "  ", NewLineChars = Environment.NewLine, NewLineHandling = NewLineHandling.Replace };
                    using (var writer = XmlWriter.Create(plan.TempPath, settings)) doc.Save(writer);
                    plans.Add(plan);
                }

                // Keep a recoverable copy of every source before replacing any of them.
                foreach (var plan in plans)
                {
                    if (!File.ReadAllBytes(plan.Path).SequenceEqual(plan.OriginalBytes)) throw new IOException(Path.GetFileName(plan.Path) + " changed during the import. Reopen Agency setup and review the file before trying again.");
                    File.WriteAllBytes(plan.BackupPath, plan.OriginalBytes);
                }

                // Replace atomically on the same volume. If a later file fails, restore files
                // already committed from their backups so the selected set stays consistent.
                foreach (var plan in plans)
                {
                    if (!File.ReadAllBytes(plan.Path).SequenceEqual(plan.OriginalBytes)) throw new IOException(Path.GetFileName(plan.Path) + " changed during the import. Reopen Agency setup and review the file before trying again.");
                    File.Replace(plan.TempPath, plan.Path, null);
                    committed.Add(plan);
                }

                return plans.Select(plan => Path.GetFileName(plan.Path) + " saved · backup: " + Path.GetFileName(plan.BackupPath)).ToList();
            }
            catch (Exception importError)
            {
                var rollbackErrors = new List<string>();
                foreach (var plan in committed.AsEnumerable().Reverse())
                {
                    try { File.Copy(plan.BackupPath, plan.Path, true); }
                    catch (Exception rollbackError) { rollbackErrors.Add(Path.GetFileName(plan.Path) + ": " + rollbackError.Message); }
                }
                if (rollbackErrors.Count > 0) throw new IOException("The XML import failed and one or more files could not be restored from their automatic backups: " + string.Join("; ", rollbackErrors) + ". Backups are beside the XML files.", importError);
                throw;
            }
            finally
            {
                foreach (var plan in plans)
                {
                    try { if (File.Exists(plan.TempPath)) File.Delete(plan.TempPath); } catch { }
                }
            }
        }
        private sealed class PendingXmlUpdate { public string Path; public string TempPath; public string BackupPath; public byte[] OriginalBytes; }
        private static XElement FindDepartment(XDocument doc, DepartmentTarget target)
        {
            if (target.Kind == "Default") return doc.Descendants("LocalPatrol").Elements(target.Name).FirstOrDefault();
            if (target.Kind == "Special") return doc.Descendants("SpecialUnit").FirstOrDefault(x => string.Equals((string)x.Attribute("name"), target.Name, StringComparison.Ordinal));
            return doc.Descendants("LocalPatrol").Elements("CustomRegion").FirstOrDefault(x => string.Equals((string)x.Attribute("name"), target.Name, StringComparison.Ordinal));
        }
        private void ConfigureVehicleTable(ListView table)
        {
            table.Background = Brushes.Transparent; table.Foreground = _text; table.BorderThickness = new Thickness(0);
            var columns = new GridView();
            columns.Columns.Add(new GridViewColumn { Header = "Vehicle model", Width = 260, DisplayMemberBinding = new System.Windows.Data.Binding("Model") });
            columns.Columns.Add(new GridViewColumn { Header = "Source pack", Width = 230, DisplayMemberBinding = new System.Windows.Data.Binding("Source") });
            columns.Columns.Add(new GridViewColumn { Header = "Assigned status", Width = 220, DisplayMemberBinding = new System.Windows.Data.Binding("AssignedStatus") });
            var headerStyle = new Style(typeof(GridViewColumnHeader));
            headerStyle.Setters.Add(new Setter(Control.BackgroundProperty, B("#15263A")));
            headerStyle.Setters.Add(new Setter(Control.ForegroundProperty, _text));
            headerStyle.Setters.Add(new Setter(Control.BorderBrushProperty, B("#2A3A50")));
            headerStyle.Setters.Add(new Setter(Control.PaddingProperty, new Thickness(10, 7, 10, 7)));
            headerStyle.Setters.Add(new Setter(Control.FontWeightProperty, FontWeights.SemiBold));
            columns.ColumnHeaderContainerStyle = headerStyle;
            table.View = columns;
        }
        private void OpenDlcDetection()
        {
            if (_vehicleLibraryOverlay != null) return;
            var overlay = CreateOverlay();
            var root = new Grid { Margin = new Thickness(38) };
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition());
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            var header = new Grid { Margin = new Thickness(0, 0, 0, 18) };
            header.ColumnDefinitions.Add(new ColumnDefinition());
            header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            var heading = new StackPanel();
            heading.Children.Add(T("INSTALLED DLC PACKS", 28, _text, FontWeights.SemiBold));
            heading.Children.Add(T("Detect vehicle DLC.RPF files from the selected GTA V installation, then choose which packs to add to your existing vehicle queue.", 13, _muted, FontWeights.Normal, new Thickness(0, 6, 18, 0)));
            header.Children.Add(heading);
            var close = Ghost("Return to dashboard"); close.Click += delegate { CloseVehicleLibrary(); };
            Grid.SetColumn(close, 1); header.Children.Add(close); root.Children.Add(header);

            var location = new Grid { Margin = new Thickness(0, 0, 0, 12) };
            location.ColumnDefinitions.Add(new ColumnDefinition());
            location.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            location.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            var folder = new TextBox { Height = 40, Text = _gtaFolder, Background = B("#0D1420"), Foreground = _text, BorderBrush = B("#2A3A50"), Padding = new Thickness(12, 8, 12, 8), ToolTip = "Select the GTA V installation folder, not the dlcpacks folder" };
            location.Children.Add(folder);
            var browse = Ghost("Choose GTA V folder"); browse.Margin = new Thickness(10, 0, 0, 0);
            browse.Click += delegate { using (var picker = new WinForms.FolderBrowserDialog { Description = "Select the GTA V installation folder" }) { if (Directory.Exists(folder.Text)) picker.SelectedPath = folder.Text; if (picker.ShowDialog() == WinForms.DialogResult.OK) folder.Text = picker.SelectedPath; } };
            Grid.SetColumn(browse, 1); location.Children.Add(browse);
            var rescan = Primary("Scan / rescan"); rescan.Margin = new Thickness(10, 0, 0, 0);
            Grid.SetColumn(rescan, 2); location.Children.Add(rescan); Grid.SetRow(location, 1); root.Children.Add(location);

            var status = T("Choose the GTA V installation folder and scan to find installed DLC packs.", 12, _muted, FontWeights.Normal, new Thickness(0, 0, 0, 8));
            Grid.SetRow(status, 2); root.Children.Add(status);
            var table = new DataGrid { AutoGenerateColumns = false, CanUserAddRows = false, CanUserDeleteRows = false, SelectionMode = DataGridSelectionMode.Extended, SelectionUnit = DataGridSelectionUnit.FullRow, Background = B("#0D1420"), Foreground = _text, BorderBrush = B("#2A3A50"), GridLinesVisibility = DataGridGridLinesVisibility.Horizontal, RowBackground = B("#101A2B"), AlternatingRowBackground = B("#15263A"), HeadersVisibility = DataGridHeadersVisibility.Column, IsReadOnly = false, Margin = new Thickness(0, 0, 0, 14) };
            var tableHeader = new Style(typeof(DataGridColumnHeader));
            tableHeader.Setters.Add(new Setter(Control.BackgroundProperty, B("#15263A")));
            tableHeader.Setters.Add(new Setter(Control.ForegroundProperty, _text));
            tableHeader.Setters.Add(new Setter(Control.BorderBrushProperty, B("#2A3A50")));
            tableHeader.Setters.Add(new Setter(Control.PaddingProperty, new Thickness(10, 8, 10, 8)));
            tableHeader.Setters.Add(new Setter(Control.FontWeightProperty, FontWeights.SemiBold));
            table.ColumnHeaderStyle = tableHeader;
            table.Columns.Add(new DataGridCheckBoxColumn { Header = "Selected", Width = 82, Binding = new System.Windows.Data.Binding("IsSelected") { Mode = System.Windows.Data.BindingMode.TwoWay, UpdateSourceTrigger = System.Windows.Data.UpdateSourceTrigger.PropertyChanged } });
            table.Columns.Add(new DataGridTextColumn { Header = "DLC pack folder", Width = 190, IsReadOnly = true, Binding = new System.Windows.Data.Binding("Name") });
            table.Columns.Add(new DataGridTextColumn { Header = "DLC.RPF path", Width = new DataGridLength(1, DataGridLengthUnitType.Star), IsReadOnly = true, Binding = new System.Windows.Data.Binding("FilePath") });
            table.Columns.Add(new DataGridTextColumn { Header = "Queue status", Width = 130, IsReadOnly = true, Binding = new System.Windows.Data.Binding("QueueStatus") });
            table.ItemsSource = _detectedDlcPacks;
            Grid.SetRow(table, 3); root.Children.Add(table);

            var footer = new Grid(); footer.ColumnDefinitions.Add(new ColumnDefinition()); footer.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto }); footer.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            var note = T("OpenIV mods only · default GTA DLCs, patchdays, EUP, and SUP are excluded.", 12, _muted, FontWeights.Normal, new Thickness(0, 10, 10, 0)); footer.Children.Add(note);
            var clear = Ghost("Deselect all"); clear.Margin = new Thickness(8, 0, 0, 0); clear.Click += delegate { foreach (var pack in _detectedDlcPacks) pack.IsSelected = false; table.Items.Refresh(); status.Text = BuildDlcStatus(_detectedDlcPacks.Count, _detectedDlcPacks.Count(x => x.IsQueued), "Selection cleared."); };
            Grid.SetColumn(clear, 1); footer.Children.Add(clear);
            var add = Primary("Add selected to vehicle queue"); add.Margin = new Thickness(10, 0, 0, 0);
            add.Click += async delegate
            {
                table.CommitEdit(DataGridEditingUnit.Cell, true); table.CommitEdit(DataGridEditingUnit.Row, true);
                var selected = _detectedDlcPacks.Where(pack => pack.IsSelected).ToList();
                if (selected.Count == 0) { status.Text = "Select at least one detected DLC pack first."; return; }
                add.IsEnabled = false;
                try
                {
                    await AddPackFiles(selected.Select(pack => pack.FilePath));
                    foreach (var pack in selected) pack.IsQueued = _vehicles.Any(vehicle => string.Equals(vehicle.PackPath, pack.FilePath, StringComparison.OrdinalIgnoreCase));
                    table.Items.Refresh();
                    status.Text = BuildDlcStatus(_detectedDlcPacks.Count, _detectedDlcPacks.Count(x => x.IsQueued), "Processed " + selected.Count + " selected pack(s). Only selected packs were read.");
                }
                finally { add.IsEnabled = true; }
            };
            Grid.SetColumn(add, 2); footer.Children.Add(add); Grid.SetRow(footer, 4); root.Children.Add(footer);

            rescan.Click += async delegate { await ScanInstalledDlcPacks(folder, table, status, rescan); };
            overlay.Child = root; ShowOverlay(overlay);
        }
        private async Task ScanInstalledDlcPacks(TextBox folder, DataGrid table, TextBlock status, Button rescan)
        {
            var gtaFolder = (folder.Text ?? string.Empty).Trim();
            if (!Directory.Exists(gtaFolder)) { status.Text = "That folder does not exist. Choose the GTA V installation folder and scan again."; status.Foreground = B("#F9B960"); return; }
            var previousSelection = new HashSet<string>(_detectedDlcPacks.Where(pack => pack.IsSelected).Select(pack => pack.FilePath), StringComparer.OrdinalIgnoreCase);
            _gtaFolder = gtaFolder; SaveGtaFolder(gtaFolder); rescan.IsEnabled = false; status.Foreground = _muted; status.Text = "Scanning OpenIV mods\\update\\x64\\dlcpacks; filtering base-game DLC names and patchdays…";
            try
            {
                var result = await Task.Run(() => FindInstalledDlcPacks(gtaFolder));
                _detectedDlcPacks.Clear();
                foreach (var pack in result.Packs)
                {
                    pack.IsSelected = previousSelection.Contains(pack.FilePath);
                    pack.IsQueued = _vehicles.Any(vehicle => string.Equals(vehicle.PackPath, pack.FilePath, StringComparison.OrdinalIgnoreCase));
                    _detectedDlcPacks.Add(pack);
                }
                table.Items.Refresh(); status.Foreground = result.Packs.Count == 0 ? B("#F9B960") : _muted;
                var exclusions = " Excluded " + result.ExcludedDefaultFolders + " default GTA DLC folder(s) and " + result.ExcludedPatchdayFolders + " patchday folder(s).";
                var detail = exclusions + (result.InaccessibleFolders > 0 ? " Skipped " + result.InaccessibleFolders + " inaccessible folder(s)." : string.Empty);
                status.Text = result.Packs.Count == 0
                    ? "No eligible DLC.RPF files were found under mods\\update\\x64\\dlcpacks. " + detail
                    : BuildDlcStatus(result.Packs.Count, result.Packs.Count(x => x.IsQueued), "Select the packs to use, then add them to the vehicle queue.") + detail;
                LogActivity("Detected " + result.Packs.Count + " installed vehicle DLC pack(s)");
            }
            catch (Exception ex) { status.Foreground = B("#F9B960"); status.Text = "DLC scan could not complete: " + ex.Message; }
            finally { rescan.IsEnabled = true; }
        }
        private static string BuildDlcStatus(int detected, int queued, string message)
        {
            return detected + " DLC pack(s) detected · " + queued + " already represented in the vehicle queue. " + message;
        }
        private void RenderVehicles() { PopulateVehicleTable(_table, _queueFilter); }
        private void PopulateVehicleTable(ListView table, string filter)
        {
            table.Items.Clear();
            var needle = (filter ?? string.Empty).Trim();
            foreach (var v in _vehicles.Where(v => needle.Length == 0 || v.Model.IndexOf(needle, StringComparison.OrdinalIgnoreCase) >= 0 || v.Source.IndexOf(needle, StringComparison.OrdinalIgnoreCase) >= 0)) table.Items.Add(v);
        }
        private void OpenVehicleLibrary()
        {
            if (_vehicleLibraryOverlay != null) return;
            var overlay = new Border { Background = new SolidColorBrush(_ink), CornerRadius = new CornerRadius(18), Opacity = 0, RenderTransformOrigin = new Point(.5, .5), RenderTransform = new ScaleTransform(.92, .92), Effect = new DropShadowEffect { Color = Colors.Black, BlurRadius = 30, ShadowDepth = 8, Opacity = .55 } };
            _vehicleLibraryOverlay = overlay;
            var root = new Grid { Margin = new Thickness(34), AllowDrop = true }; root.DragEnter += VehicleDragEnter; root.Drop += VehicleDrop; root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); root.RowDefinitions.Add(new RowDefinition());
            var fullTable = new ListView { Margin = new Thickness(22), SelectionMode = SelectionMode.Extended }; ConfigureVehicleTable(fullTable);
            var head = new Grid(); head.ColumnDefinitions.Add(new ColumnDefinition()); head.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto }); var heading = new StackPanel(); heading.Children.Add(T("VEHICLE LIBRARY", 28, _text, FontWeights.SemiBold)); heading.Children.Add(T("Expanded fleet workspace · " + _vehicles.Count + " vehicle(s) loaded", 13, _muted, FontWeights.Normal, new Thickness(0, 6, 0, 0))); head.Children.Add(heading); var close = Ghost("Return to dashboard"); close.Click += delegate { CloseVehicleLibrary(); }; Grid.SetColumn(close, 1); head.Children.Add(close); root.Children.Add(head);
            var controls = new Grid { Margin = new Thickness(0, 28, 0, 18) }; controls.ColumnDefinitions.Add(new ColumnDefinition()); controls.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto }); var filter = new TextBox { Width = 300, Height = 38, Background = B("#0D1420"), Foreground = _text, BorderBrush = B("#2A3A50"), Padding = new Thickness(12, 8, 12, 8), ToolTip = "Search vehicle model or source pack" }; controls.Children.Add(filter); var dropTip = T("Drop DLC.RPF files or pack folders anywhere here", 12, _accent, FontWeights.SemiBold, new Thickness(10, 10, 0, 0)); Grid.SetColumn(dropTip, 1); controls.Children.Add(dropTip); Grid.SetRow(controls, 1); root.Children.Add(controls);
            var frame = Card(); PopulateVehicleTable(fullTable, string.Empty); filter.TextChanged += delegate { PopulateVehicleTable(fullTable, filter.Text); }; frame.Child = fullTable; Grid.SetRow(frame, 2); root.Children.Add(frame); overlay.Child = root; Grid.SetRowSpan(overlay, 4); Panel.SetZIndex(overlay, 100); _root.Children.Add(overlay);
            var fade = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(190)); var grow = new DoubleAnimation(.92, 1, TimeSpan.FromMilliseconds(240)) { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut } }; overlay.BeginAnimation(OpacityProperty, fade); ((ScaleTransform)overlay.RenderTransform).BeginAnimation(ScaleTransform.ScaleXProperty, grow); ((ScaleTransform)overlay.RenderTransform).BeginAnimation(ScaleTransform.ScaleYProperty, grow);
        }
        private void OpenAssignments()
        {
            if (_vehicleLibraryOverlay != null) return;
            if (_departments.Count == 0) { OpenAgencySetup(); return; }
            var overlay = new Border { Background = new SolidColorBrush(_ink), CornerRadius = new CornerRadius(18), Opacity = 0, RenderTransformOrigin = new Point(.5, .5), RenderTransform = new ScaleTransform(.92, .92), Effect = new DropShadowEffect { Color = Colors.Black, BlurRadius = 30, ShadowDepth = 8, Opacity = .55 } }; _vehicleLibraryOverlay = overlay;
            var root = new Grid { Margin = new Thickness(34) }; root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); root.RowDefinitions.Add(new RowDefinition());
            var head = new Grid { Margin = new Thickness(0, 0, 0, 24) }; head.ColumnDefinitions.Add(new ColumnDefinition()); head.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto }); var heading = new StackPanel(); heading.Children.Add(T("ASSIGNMENTS", 28, _text, FontWeights.SemiBold)); heading.Children.Add(T("Select vehicles, choose an agency, then save your deployment list.", 13, _muted, FontWeights.Normal, new Thickness(0, 6, 0, 0))); head.Children.Add(heading); var close = Ghost("Return to dashboard"); close.Click += delegate { CloseVehicleLibrary(); }; Grid.SetColumn(close, 1); head.Children.Add(close); root.Children.Add(head);
            var workspace = new Grid(); workspace.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(270) }); workspace.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(20) }); workspace.ColumnDefinitions.Add(new ColumnDefinition()); workspace.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(20) }); workspace.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(420) }); Grid.SetRow(workspace, 1); root.Children.Add(workspace);
            var agencyCard = Card(); var agencyStack = new Grid { Margin = new Thickness(20) }; agencyStack.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); agencyStack.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) }); agencyStack.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); agencyStack.RowDefinitions.Add(new RowDefinition { Height = new GridLength(.8, GridUnitType.Star) }); var agencyLabel = T("1  SELECT ONE OR MORE AGENCIES / PATROL ZONES", 11, _accent, FontWeights.SemiBold, new Thickness(0, 0, 0, 12)); agencyStack.Children.Add(agencyLabel); var agencyList = new ListBox { Background = Brushes.Transparent, Foreground = _text, BorderThickness = new Thickness(0), ItemsSource = _departments, DisplayMemberPath = "Display", SelectionMode = SelectionMode.Extended, HorizontalContentAlignment = HorizontalAlignment.Stretch, MinHeight = 150 }; Grid.SetRow(agencyList, 1); agencyStack.Children.Add(agencyList); _currentZoneStatus = T("Select a zone to read its saved XML vehicles.", 12, _muted, FontWeights.Normal, new Thickness(0, 14, 0, 8)); Grid.SetRow(_currentZoneStatus, 2); agencyStack.Children.Add(_currentZoneStatus); _currentZoneVehicleList = new ListBox { Background = B("#0D1420"), Foreground = _text, BorderBrush = B("#2A3A50"), MinHeight = 100, ToolTip = "Read-only vehicles currently stored in the selected zone XML" }; Grid.SetRow(_currentZoneVehicleList, 3); agencyStack.Children.Add(_currentZoneVehicleList); agencyList.SelectionChanged += delegate { var selectedZone = agencyList.SelectedItem as DepartmentTarget; _currentZoneSelection = selectedZone; RefreshCurrentZoneVehicles(); }; agencyCard.Child = agencyStack; workspace.Children.Add(agencyCard);
            var right = new Grid(); right.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); right.RowDefinitions.Add(new RowDefinition()); Grid.SetColumn(right, 2); workspace.Children.Add(right); var actions = new Grid { Margin = new Thickness(0, 0, 0, 14) }; actions.ColumnDefinitions.Add(new ColumnDefinition()); actions.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto }); actions.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto }); actions.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto }); actions.Children.Add(T("2  SELECT ONE OR MORE VEHICLES", 11, _accent, FontWeights.SemiBold, new Thickness(0, 10, 0, 0))); var assign = Primary("Assign selected vehicles"); assign.Margin = new Thickness(10, 0, 0, 0); Grid.SetColumn(assign, 1); actions.Children.Add(assign); var unassign = Ghost("Unassign selected"); unassign.Margin = new Thickness(10, 0, 0, 0); Grid.SetColumn(unassign, 2); actions.Children.Add(unassign); var import = Ghost("Import XML changes"); import.Margin = new Thickness(10, 0, 0, 0); Grid.SetColumn(import, 3); actions.Children.Add(import); right.Children.Add(actions);
            var tableCard = Card(); var table = new ListView { Margin = new Thickness(20), SelectionMode = SelectionMode.Extended }; ConfigureVehicleTable(table); PopulateVehicleTable(table, string.Empty); tableCard.Child = table; Grid.SetRow(tableCard, 1); right.Children.Add(tableCard); assign.Click += delegate { AssignSelectedVehicles(table, agencyList.SelectedItems.Cast<DepartmentTarget>()); }; unassign.Click += delegate { UnassignSelectedVehicles(table); }; import.Click += delegate { ImportAssignments(); };
            var previewCard = Card(); var previewLayout = new Grid(); previewLayout.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); previewLayout.RowDefinitions.Add(new RowDefinition()); previewLayout.Children.Add(T("3D VEHICLE PREVIEW", 11, _accent, FontWeights.SemiBold, new Thickness(20, 20, 20, 14))); _nativeVehiclePreview = new NativeVehiclePreview { Margin = new Thickness(12, 4, 12, 12) }; Grid.SetRow(_nativeVehiclePreview, 1); previewLayout.Children.Add(_nativeVehiclePreview); var fullScreen = Ghost("Full screen"); fullScreen.HorizontalAlignment = HorizontalAlignment.Right; fullScreen.VerticalAlignment = VerticalAlignment.Top; fullScreen.Margin = new Thickness(12); fullScreen.Click += delegate { OpenFullscreenPreview(_nativeVehiclePreview, previewLayout); }; Grid.SetRow(fullScreen, 1); Panel.SetZIndex(fullScreen, 20); previewLayout.Children.Add(fullScreen); previewCard.Child = previewLayout; Grid.SetColumn(previewCard, 4); workspace.Children.Add(previewCard);
            var previewMessage = T("Drag to orbit 360° · scroll to zoom", 12, _muted, FontWeights.Normal, new Thickness(20)); previewMessage.MaxWidth = 240; previewMessage.TextWrapping = TextWrapping.Wrap; previewMessage.IsHitTestVisible = false; previewMessage.HorizontalAlignment = HorizontalAlignment.Left; previewMessage.VerticalAlignment = VerticalAlignment.Top; Grid.SetRow(previewMessage, 1); previewLayout.Children.Add(previewMessage); var previewControls = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Bottom, Margin = new Thickness(20) }; previewControls.Children.Add(BuildViewButton("Front", _nativeVehiclePreview, 0)); previewControls.Children.Add(BuildViewButton("Left", _nativeVehiclePreview, -Math.PI / 2)); previewControls.Children.Add(BuildViewButton("Right", _nativeVehiclePreview, Math.PI / 2)); previewControls.Children.Add(BuildViewButton("Rear", _nativeVehiclePreview, Math.PI)); var zoomIn = Ghost("+"); zoomIn.Width = 36; zoomIn.Margin = new Thickness(8, 0, 0, 0); zoomIn.Click += delegate { _nativeVehiclePreview.Zoom(true); }; var zoomOut = Ghost("−"); zoomOut.Width = 36; zoomOut.Margin = new Thickness(8, 0, 0, 0); zoomOut.Click += delegate { _nativeVehiclePreview.Zoom(false); }; var resetPreview = Ghost("Reset camera"); resetPreview.Margin = new Thickness(8, 0, 0, 0); resetPreview.Click += delegate { _nativeVehiclePreview.ResetCamera(); }; previewControls.Children.Add(zoomIn); previewControls.Children.Add(zoomOut); previewControls.Children.Add(resetPreview); Grid.SetRow(previewControls, 1); previewLayout.Children.Add(previewControls); table.SelectionChanged += async delegate { await LoadNativePreviewAsync(table.SelectedItem as PremiumVehicle, _nativeVehiclePreview, previewMessage); };
            overlay.Child = root; Grid.SetRowSpan(overlay, 4); Panel.SetZIndex(overlay, 100); _root.Children.Add(overlay); var fade = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(190)); var grow = new DoubleAnimation(.92, 1, TimeSpan.FromMilliseconds(240)) { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut } }; overlay.BeginAnimation(OpacityProperty, fade); ((ScaleTransform)overlay.RenderTransform).BeginAnimation(ScaleTransform.ScaleXProperty, grow); ((ScaleTransform)overlay.RenderTransform).BeginAnimation(ScaleTransform.ScaleYProperty, grow);
        }
        private void RefreshCurrentZoneVehicles()
        {
            if (_currentZoneVehicleList == null || _currentZoneStatus == null) return;
            _currentZoneVehicleList.Items.Clear();
            var target = _currentZoneSelection;
            if (target == null) { _currentZoneStatus.Text = "Select a zone to read its saved XML vehicles."; return; }
            if (string.IsNullOrWhiteSpace(target.FilePath) || !File.Exists(target.FilePath)) { _currentZoneStatus.Text = "The selected zone XML file is missing. Reconnect your GTA V files."; return; }
            try
            {
                var doc = XDocument.Load(target.FilePath);
                var zone = FindDepartment(doc, target);
                if (zone == null) { _currentZoneStatus.Text = "This zone was not found in the current XML file."; return; }
                var vehicles = zone.Descendants("Vehicles").SelectMany(list => list.Elements("Vehicle"))
                    .Select(vehicle => new { Name = (vehicle.Value ?? string.Empty).Trim(), Chance = (string)vehicle.Attribute("chance") })
                    .Where(vehicle => vehicle.Name.Length > 0).ToList();
                foreach (var vehicle in vehicles)
                    _currentZoneVehicleList.Items.Add(vehicle.Name + (string.IsNullOrWhiteSpace(vehicle.Chance) ? string.Empty : "  ·  " + vehicle.Chance + "% chance"));
                _currentZoneStatus.Text = vehicles.Count == 0
                    ? "CURRENT XML VEHICLES · " + target.Display + " · none configured"
                    : "CURRENT XML VEHICLES · " + target.Display + " · " + vehicles.Count + " vehicle(s) · read-only";
            }
            catch (Exception ex) { _currentZoneStatus.Text = "Could not read the selected XML: " + ex.Message; }
        }
        
        private Button BuildViewButton(string label, NativeVehiclePreview preview, double yaw)
        {
            var button = Ghost(label); button.Margin = new Thickness(0, 0, 6, 0); button.Click += delegate { preview.SetView(yaw, 0); }; return button;
        }

        private void OpenFullscreenPreview(NativeVehiclePreview preview, Grid originalLayout)
        {
            var originalMargin = preview.Margin;
            originalLayout.Children.Remove(preview);
            var overlay = new Border { Background = B("#060B12"), CornerRadius = new CornerRadius(18), Effect = new DropShadowEffect { Color = Colors.Black, BlurRadius = 30, ShadowDepth = 8, Opacity = .7 } };
            var page = new Grid { Margin = new Thickness(28) }; page.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); page.RowDefinitions.Add(new RowDefinition()); page.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            var header = new Grid(); header.ColumnDefinitions.Add(new ColumnDefinition()); header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto }); header.Children.Add(T("FULL 360° VEHICLE PREVIEW", 15, _accent, FontWeights.SemiBold)); var close = Ghost("Exit full screen"); close.Click += delegate { page.Children.Remove(preview); preview.Margin = originalMargin; Grid.SetRow(preview, 1); originalLayout.Children.Add(preview); _root.Children.Remove(overlay); }; Grid.SetColumn(close, 1); header.Children.Add(close); page.Children.Add(header);
            preview.Margin = new Thickness(0, 16, 0, 12); Grid.SetRow(preview, 1); page.Children.Add(preview);
            var footer = new Grid { Margin = new Thickness(0, 8, 0, 0) }; footer.ColumnDefinitions.Add(new ColumnDefinition()); footer.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto }); var hint = T("Drag the vehicle to rotate all the way around · scroll to zoom", 13, _muted, FontWeights.Normal); hint.VerticalAlignment = VerticalAlignment.Center; hint.IsHitTestVisible = false; footer.Children.Add(hint); var reset = Ghost("Reset camera"); reset.Click += delegate { preview.ResetCamera(); }; Grid.SetColumn(reset, 1); footer.Children.Add(reset); Grid.SetRow(footer, 2); page.Children.Add(footer);
            overlay.Child = page; Grid.SetRowSpan(overlay, 4); Panel.SetZIndex(overlay, 200); _root.Children.Add(overlay);
        }

        private async Task LoadNativePreviewAsync(PremiumVehicle vehicle, NativeVehiclePreview preview, TextBlock message)
        {
            if (vehicle == null) { message.Text = "Select a vehicle first."; return; }
            if (_previewLoadCancellation != null)
            {
                _previewLoadCancellation.Cancel();
                _previewLoadCancellation.Dispose();
            }
            _previewLoadCancellation = new CancellationTokenSource();
            var cancellationToken = _previewLoadCancellation.Token;
            var progress = new Progress<string>(text => { message.Text = text; Debug.WriteLine("[Vehicle preview] " + text); });
            try
            {
                message.Text = "Starting native 3D preview for " + vehicle.Model + "...";
                await preview.LoadAsync(vehicle.PackPath, vehicle.Model, cancellationToken, progress);
                if (!cancellationToken.IsCancellationRequested) message.Text = "Preview loaded: " + vehicle.Model;
            }
            catch (OperationCanceledException)
            {
                Debug.WriteLine("[Vehicle preview] Cancelled " + vehicle.Model);
            }
            catch (Exception ex)
            {
                Debug.WriteLine("[Vehicle preview] " + ex);
                message.Text = "Preview failed for " + vehicle.Model + ": " + ex.Message;
            }
        }
        private static bool IsValidGtaFolder(string path) { return !string.IsNullOrWhiteSpace(path) && File.Exists(Path.Combine(path, "GTA5.exe")) && File.Exists(Path.Combine(path, "update", "update.rpf")); }
        private static string GtaFolderSettingsPath { get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "RenegadesXmlEditor", "gta-folder.txt"); } }
        private static string LoadGtaFolder() { try { return File.Exists(GtaFolderSettingsPath) ? File.ReadAllText(GtaFolderSettingsPath).Trim() : string.Empty; } catch { return string.Empty; } }
        private static void SaveGtaFolder(string path) { try { var directory = Path.GetDirectoryName(GtaFolderSettingsPath); Directory.CreateDirectory(directory); File.WriteAllText(GtaFolderSettingsPath, path ?? string.Empty); } catch { } }
        private void OpenPackImport()
        {
            if (_vehicleLibraryOverlay != null) return;
            var overlay = CreateOverlay(); var root = new StackPanel { Margin = new Thickness(48), VerticalAlignment = VerticalAlignment.Center, MaxWidth = 760 }; root.Children.Add(T("IMPORT VEHICLE PACK", 30, _text, FontWeights.SemiBold)); root.Children.Add(T("Paste a DLC.RPF path or a pack-folder path below, browse your files, or drag files and folders onto this page.", 14, _muted, FontWeights.Normal, new Thickness(0, 8, 0, 26))); var path = new TextBox { Height = 44, Background = B("#0D1420"), Foreground = _text, BorderBrush = B("#2A3A50"), Padding = new Thickness(14, 10, 14, 10), ToolTip = "DLC.RPF or pack folder path" }; root.Children.Add(path); var result = T("Waiting for a vehicle pack.", 13, _muted, FontWeights.Normal, new Thickness(0, 14, 0, 0)); root.Children.Add(result); var actions = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 24, 0, 0) }; var start = Primary("Scan vehicle pack"); start.Click += async delegate { if (string.IsNullOrWhiteSpace(path.Text)) { result.Text = "Enter or paste a valid path first."; return; } result.Text = "Scanning pack…"; await AddPackFiles(new[] { path.Text.Trim() }); CloseVehicleLibrary(); }; var browse = Ghost("Browse in File Explorer"); browse.Margin = new Thickness(0, 0, 10, 0); browse.Click += delegate { using (var open = new WinForms.OpenFileDialog { Filter = "DLC vehicle pack|dlc.rpf|RPF archives|*.rpf", Title = "Select a DLC.RPF vehicle pack", Multiselect = false }) { if (open.ShowDialog() == WinForms.DialogResult.OK) path.Text = open.FileName; } }; var close = Ghost("Cancel"); close.Click += delegate { CloseVehicleLibrary(); }; actions.Children.Add(start); actions.Children.Add(browse); actions.Children.Add(close); root.Children.Add(actions); root.AllowDrop = true; root.DragEnter += VehicleDragEnter; root.Drop += VehicleDrop; overlay.Child = root; ShowOverlay(overlay);
        }
        private void ShowPackBrowser(Border overlay, UIElement importPage, TextBox selectedPath)
        {
            var root = new Grid { Margin = new Thickness(48) }; root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); root.RowDefinitions.Add(new RowDefinition()); root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            var header = new Grid(); header.ColumnDefinitions.Add(new ColumnDefinition()); header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto }); header.Children.Add(T("BROWSE VEHICLE PACKS", 28, _text, FontWeights.SemiBold)); var back = Ghost("Back to import"); back.Click += delegate { overlay.Child = importPage; }; Grid.SetColumn(back, 1); header.Children.Add(back); root.Children.Add(header);
            var location = new TextBox { Height = 42, Margin = new Thickness(0, 22, 0, 14), Background = B("#0D1420"), Foreground = _text, BorderBrush = B("#2A3A50"), Padding = new Thickness(14, 9, 14, 9), Text = Directory.Exists(selectedPath.Text) ? selectedPath.Text : Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments) }; Grid.SetRow(location, 1); root.Children.Add(location);
            var list = new ListBox { Background = B("#0D1420"), Foreground = _text, BorderBrush = B("#2A3A50"), DisplayMemberPath = "Name" }; Grid.SetRow(list, 2); root.Children.Add(list);
            Action refresh = delegate { try { var folder = location.Text.Trim(); var entries = new List<BrowserEntry>(); if (!Directory.Exists(folder)) throw new DirectoryNotFoundException(); entries.AddRange(Directory.GetDirectories(folder).Select(x => new BrowserEntry { Name = "[Folder]  " + Path.GetFileName(x), FullPath = x, IsFolder = true })); entries.AddRange(Directory.GetFiles(folder, "*.rpf").Select(x => new BrowserEntry { Name = Path.GetFileName(x), FullPath = x, IsFolder = false })); list.ItemsSource = entries.OrderBy(x => x.Name).ToList(); } catch { list.ItemsSource = new[] { new BrowserEntry { Name = "Folder could not be opened. Check the location and select Open folder.", FullPath = "", IsFolder = false } }; } }; refresh();
            var actions = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 14, 0, 0) }; var up = Ghost("Up one folder"); up.Click += delegate { try { var parent = Directory.GetParent(location.Text.Trim()); if (parent != null) location.Text = parent.FullName; refresh(); } catch { } }; var open = Ghost("Open folder"); open.Margin = new Thickness(10, 0, 0, 0); open.Click += delegate { var entry = list.SelectedItem as BrowserEntry; if (entry != null && entry.IsFolder) { location.Text = entry.FullPath; refresh(); } else refresh(); }; var choose = Primary("Use selected pack"); choose.Margin = new Thickness(10, 0, 0, 0); choose.Click += delegate { var entry = list.SelectedItem as BrowserEntry; if (entry == null || string.IsNullOrWhiteSpace(entry.FullPath)) return; if (entry.IsFolder) { selectedPath.Text = entry.FullPath; } else selectedPath.Text = entry.FullPath; overlay.Child = importPage; }; actions.Children.Add(up); actions.Children.Add(open); actions.Children.Add(choose); Grid.SetRow(actions, 3); root.Children.Add(actions); overlay.Child = root;
        }
        private void OpenAgencySetup()
        {
            if (_vehicleLibraryOverlay != null) return;
            var overlay = CreateOverlay(); var root = new StackPanel { Margin = new Thickness(48), VerticalAlignment = VerticalAlignment.Center, MaxWidth = 820 }; root.Children.Add(T("AGENCY SETUP", 30, _text, FontWeights.SemiBold)); root.Children.Add(T("Choose your GTA V installation folder. Command will search it and automatically connect the Policing Redefined XML files it finds.", 14, _muted, FontWeights.Normal, new Thickness(0, 8, 0, 26))); var gtaPath = new TextBox { Height = 44, Text = _gtaFolder, Background = B("#0D1420"), Foreground = _text, BorderBrush = B("#2A3A50"), Padding = new Thickness(14, 10, 14, 10), ToolTip = "Your Grand Theft Auto V folder" }; root.Children.Add(gtaPath); var result = new StackPanel { Margin = new Thickness(0, 18, 0, 0) }; result.Children.Add(T("No GTA V folder scanned yet.", 13, _muted, FontWeights.Normal)); root.Children.Add(result); var actions = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 24, 0, 0) }; var scan = Primary("Scan GTA V folder"); scan.Click += async delegate { await ScanGtaFolder(gtaPath.Text, result); }; var browse = Ghost("Browse in File Explorer"); browse.Margin = new Thickness(0, 0, 10, 0); browse.Click += async delegate { using (var folder = new WinForms.FolderBrowserDialog { Description = "Select your Grand Theft Auto V installation folder" }) { if (folder.ShowDialog() == WinForms.DialogResult.OK) { gtaPath.Text = folder.SelectedPath; await ScanGtaFolder(gtaPath.Text, result); } } }; var close = Ghost("Done"); close.Click += delegate { CloseVehicleLibrary(); }; actions.Children.Add(scan); actions.Children.Add(browse); actions.Children.Add(close); root.Children.Add(actions); overlay.Child = root; ShowOverlay(overlay);
        }
        private async Task ScanGtaFolder(string folder, StackPanel result)
        {
            if (!Directory.Exists((folder ?? string.Empty).Trim())) { result.Children.Clear(); result.Children.Add(T("That folder could not be found. Select the GTA V installation folder and try again.", 13, B("#F9B960"), FontWeights.Normal)); return; }
            try
            {
                _gtaFolder = folder.Trim(); SaveGtaFolder(_gtaFolder); result.Children.Clear(); result.Children.Add(T("Scanning selected GTA V folder", 13, _accent, FontWeights.SemiBold)); SetProgress(true, "Searching for Policing Redefined XML files", 20); var found = await Task.Run(() => FindPolicingRedefinedFiles(folder.Trim())); foreach (var item in found) _xmlPaths[item.Key] = item.Value; RefreshDepartments(); LogActivity("Scanned GTA V folder and connected " + found.Count + " XML file(s)"); result.Children.Clear(); result.Children.Add(T(found.Count + " of 3 XML files connected", 14, found.Count == 3 ? _accent : B("#F9B960"), FontWeights.SemiBold)); foreach (var expected in new[] { "DefaultRegions.xml", "SpecialUnits.xml", "CustomRegions.xml" }) result.Children.Add(T((found.ContainsKey(expected) ? "CONNECTED  " : "MISSING       ") + expected, 13, found.ContainsKey(expected) ? _text : _muted, FontWeights.Normal, new Thickness(0, 8, 0, 0)));
            }
            catch (Exception ex) { result.Children.Clear(); result.Children.Add(T("Unable to scan this folder: " + ex.Message, 13, B("#F9B960"), FontWeights.Normal)); }
            finally { SetProgress(false, string.Empty, 0); }
        }
        private Border CreateOverlay()
        {
            var overlay = new Border { Background = new SolidColorBrush(_ink), CornerRadius = new CornerRadius(18), Opacity = 0, RenderTransformOrigin = new Point(.5, .5), RenderTransform = new ScaleTransform(.92, .92), Effect = new DropShadowEffect { Color = Colors.Black, BlurRadius = 30, ShadowDepth = 8, Opacity = .55 } }; _vehicleLibraryOverlay = overlay; return overlay;
        }
        private void ShowOverlay(Border overlay)
        {
            Grid.SetRowSpan(overlay, 4); Panel.SetZIndex(overlay, 100); _root.Children.Add(overlay); var fade = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(190)); var grow = new DoubleAnimation(.92, 1, TimeSpan.FromMilliseconds(240)) { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut } }; overlay.BeginAnimation(OpacityProperty, fade); ((ScaleTransform)overlay.RenderTransform).BeginAnimation(ScaleTransform.ScaleXProperty, grow); ((ScaleTransform)overlay.RenderTransform).BeginAnimation(ScaleTransform.ScaleYProperty, grow);
        }
        private void CloseVehicleLibrary()
        {
            if (_vehicleLibraryOverlay == null) return;
            var overlay = _vehicleLibraryOverlay; var shrink = new DoubleAnimation(1, .95, TimeSpan.FromMilliseconds(150)) { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn } }; var fade = new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(150));
            fade.Completed += delegate { _root.Children.Remove(overlay); _vehicleLibraryOverlay = null; };
            overlay.BeginAnimation(OpacityProperty, fade); ((ScaleTransform)overlay.RenderTransform).BeginAnimation(ScaleTransform.ScaleXProperty, shrink); ((ScaleTransform)overlay.RenderTransform).BeginAnimation(ScaleTransform.ScaleYProperty, shrink);
        }
        private Button Tab(string label, bool active) { return new Button { Tag = label, Content = Translate(label), Margin = new Thickness(0, 0, 10, 0), Padding = new Thickness(15, 8, 15, 8), Background = active ? B("#17304A") : Brushes.Transparent, Foreground = active ? _accent : _muted, BorderThickness = new Thickness(0), FontSize = 13 }; }
        private Button Primary(string label) { return new Button { Tag = label, Content = Translate(label), Background = _accent, Foreground = B("#06141A"), BorderThickness = new Thickness(0), Padding = new Thickness(16, 10, 16, 10), Margin = new Thickness(0, 0, 10, 0), FontWeight = FontWeights.SemiBold }; }
        private Button Ghost(string label) { return new Button { Tag = label, Content = Translate(label), Background = B("#26364A"), Foreground = _text, BorderThickness = new Thickness(0), Padding = new Thickness(16, 10, 16, 10) }; }
        private TextBlock T(string text, double size, Brush color, FontWeight weight, Thickness? margin = null) { return new TextBlock { Tag = text, Text = Translate(text), FontSize = size, Foreground = color, FontWeight = weight, Margin = margin ?? new Thickness(0) }; }
        private static readonly LanguageChoice[] SupportedLanguages = new[] { new LanguageChoice("English", "English"), new LanguageChoice("Español", "Spanish"), new LanguageChoice("Français", "French"), new LanguageChoice("Deutsch", "German"), new LanguageChoice("Português", "Portuguese"), new LanguageChoice("Русский", "Russian") };
        private static string LoadLanguage() { try { var file = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "RenegadesXmlEditor", "language.txt"); return File.Exists(file) ? File.ReadAllText(file).Trim() : "English"; } catch { return "English"; } }
        private static void SaveLanguage(string language) { var folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "RenegadesXmlEditor"); Directory.CreateDirectory(folder); File.WriteAllText(Path.Combine(folder, "language.txt"), language); }
        private string Translate(string value) { string translated; return Translations.TryGetValue(_language + "|" + value, out translated) ? translated : value; }
        private void ApplyLanguage(DependencyObject root) { var text = root as TextBlock; if (text != null && text.Tag is string) text.Text = Translate((string)text.Tag); var button = root as Button; if (button != null && button.Tag is string) button.Content = Translate((string)button.Tag); for (var i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++) ApplyLanguage(VisualTreeHelper.GetChild(root, i)); }
        private static readonly Dictionary<string, string> Translations = new Dictionary<string, string>
        {
            { "Spanish|Overview", "Resumen" }, { "Spanish|Vehicle library", "Biblioteca de vehículos" }, { "Spanish|Agency setup", "Configuración de agencias" }, { "Spanish|Assignments", "Asignaciones" }, { "Spanish|Activity", "Actividad" }, { "Spanish|Import vehicle pack", "Importar paquete de vehículos" }, { "Spanish|Assign selected vehicles", "Asignar vehículos seleccionados" }, { "Spanish|Unassign selected", "Quitar asignación" }, { "Spanish|Import XML changes", "Importar cambios XML" }, { "Spanish|Full screen", "Pantalla completa" }, { "Spanish|Reset camera", "Restablecer cámara" },
            { "French|Overview", "Aperçu" }, { "French|Vehicle library", "Bibliothèque de véhicules" }, { "French|Agency setup", "Configuration des agences" }, { "French|Assignments", "Attributions" }, { "French|Activity", "Activité" }, { "French|Import vehicle pack", "Importer un pack de véhicules" }, { "French|Assign selected vehicles", "Attribuer les véhicules sélectionnés" }, { "French|Unassign selected", "Retirer l’attribution" }, { "French|Import XML changes", "Importer les modifications XML" }, { "French|Full screen", "Plein écran" },
            { "German|Overview", "Übersicht" }, { "German|Vehicle library", "Fahrzeugbibliothek" }, { "German|Agency setup", "Behördeneinrichtung" }, { "German|Assignments", "Zuweisungen" }, { "German|Activity", "Aktivität" }, { "German|Import vehicle pack", "Fahrzeugpaket importieren" }, { "German|Assign selected vehicles", "Ausgewählte Fahrzeuge zuweisen" }, { "German|Unassign selected", "Zuweisung entfernen" }, { "German|Import XML changes", "XML-Änderungen importieren" }, { "German|Full screen", "Vollbild" },
            { "Portuguese|Overview", "Visão geral" }, { "Portuguese|Vehicle library", "Biblioteca de veículos" }, { "Portuguese|Agency setup", "Configuração de agências" }, { "Portuguese|Assignments", "Atribuições" }, { "Portuguese|Import vehicle pack", "Importar pacote de veículos" }, { "Portuguese|Assign selected vehicles", "Atribuir veículos selecionados" }, { "Portuguese|Import XML changes", "Importar alterações XML" },
            { "Russian|Overview", "Обзор" }, { "Russian|Vehicle library", "Библиотека автомобилей" }, { "Russian|Agency setup", "Настройка агентств" }, { "Russian|Assignments", "Назначения" }, { "Russian|Activity", "Активность" }, { "Russian|Import vehicle pack", "Импорт пакета автомобилей" }, { "Russian|Assign selected vehicles", "Назначить выбранные автомобили" }, { "Russian|Import XML changes", "Импорт XML-изменений" }
        };
        private static Brush B(string color) { return (Brush)new BrushConverter().ConvertFromString(color); }
    }
    internal sealed class PremiumVehicle
    {
        public string Model { get; set; }
        public string Source { get; set; }
        public string PackPath { get; set; }
        public List<DepartmentTarget> Departments { get; private set; }
        public PremiumVehicle() { Departments = new List<DepartmentTarget>(); }
        public string AssignedStatus { get { return Departments.Count == 0 ? "Unassigned" : string.Join(" | ", Departments.Select(x => x.Display)); } }
    }
    internal sealed class VehicleZoneAssignment { public PremiumVehicle Vehicle; public DepartmentTarget Department; }
    internal sealed class DetectedDlcPack
    {
        public string Name { get; set; }
        public string FilePath { get; set; }
        public bool IsSelected { get; set; }
        public bool IsQueued { get; set; }
        public string QueueStatus { get { return IsQueued ? "Already queued" : "Not queued"; } }
    }
    internal sealed class DlcScanResult
    {
        public List<DetectedDlcPack> Packs = new List<DetectedDlcPack>();
        public int InaccessibleFolders;
        public int ExcludedDefaultFolders;
        public int ExcludedPatchdayFolders;
    }
    internal sealed class LanguageChoice { public string Name { get; private set; } public string Code { get; private set; } public LanguageChoice(string name, string code) { Name = name; Code = code; } }
    internal sealed class BrowserEntry
    {
        public string Name { get; set; }
        public string FullPath { get; set; }
        public bool IsFolder { get; set; }
    }
}
