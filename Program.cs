using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Xml;
using System.Xml.Linq;

namespace RedefinedVehicleImporter
{
    internal static class Program
    {
        [STAThread]
        private static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Ui.Language = LanguageSettings.LoadOrChoose();
            Application.Run(new MainForm());
        }
    }

    internal sealed class MainForm : Form
    {
        private readonly TextBox _defaultRegions = FileBox("Choose DefaultRegions.xml");
        private readonly TextBox _specialUnits = FileBox("Choose SpecialUnits.xml");
        private readonly TextBox _customRegions = FileBox("Choose CustomRegions.xml");
        private readonly ListBox _vehicles = new ListBox();
        private readonly ComboBox _department = new ComboBox();
        private readonly Button _assign = new Button { Text = "Assign selected vehicles", FlatStyle = FlatStyle.Flat };
        private readonly Button _scanRpf = new Button { Text = "Scan DLC.RPF", FlatStyle = FlatStyle.Flat };
        private readonly Button _findGtaFiles = new Button { Text = "Find XMLs in GTA Folder", FlatStyle = FlatStyle.Flat };
        private readonly Button _loadAssignments = new Button { Text = "Load XML vehicles", FlatStyle = FlatStyle.Flat };
        private readonly Button _removeAssignment = new Button { Text = "Remove selected", FlatStyle = FlatStyle.Flat };
        private readonly Label _assignmentStatus = new Label { AutoSize = true };
        private readonly HashSet<string> _loadedDepartments = new HashSet<string>();
        private readonly TabControl _stages = new TabControl();
        private readonly TabPage _welcomePage = new TabPage("Welcome");
        private FlowLayoutPanel _targetBox;
        private readonly Button _back = new Button { Text = "← Back", FlatStyle = FlatStyle.Flat };
        private readonly Button _next = new Button { Text = "Next →", FlatStyle = FlatStyle.Flat };
        private readonly Label _pageIndicator = new Label { AutoSize = true };
        private Button _languageButton;
        private readonly List<string> _history = new List<string>();
        private Label _heading;
        private Label _explanation;
        private readonly TabPage _previewPage = new TabPage("4  Review & Preview");
        private readonly RichTextBox _preview = new RichTextBox();
        private readonly Button _showPreview = new Button { Text = "Review XML changes  →", FlatStyle = FlatStyle.Flat };
        private readonly ProgressBar _progress = new ProgressBar { Style = ProgressBarStyle.Marquee, MarqueeAnimationSpeed = 28, Visible = false, Width = 150 };
        private readonly Label _progressText = new Label { Text = "Working…", AutoSize = true, Visible = false };
        private readonly List<DepartmentTarget> _departments = new List<DepartmentTarget>();
        private readonly CheckBox _useDefault = new CheckBox { Text = "Update Default Regions", Checked = true, AutoSize = true };
        private readonly CheckBox _useSpecial = new CheckBox { Text = "Update Special Units", Checked = true, AutoSize = true };
        private readonly CheckBox _useCustom = new CheckBox { Text = "Update Custom Regions", Checked = true, AutoSize = true };
        private readonly Label _dropHint = new Label { Text = "DROP VEHICLE PACKS HERE\n\nFolders and .yft files are supported", AutoSize = false, TextAlign = ContentAlignment.MiddleCenter, BackColor = Color.FromArgb(28, 39, 62), ForeColor = Color.FromArgb(205, 221, 244) };

        public MainForm()
        {
            Text = Ui.T("Renegade's Vehicle Importer | MDT");
            Size = new Size(1080, 760);
            MinimumSize = new Size(720, 580);
            StartPosition = FormStartPosition.CenterScreen;
            WindowState = FormWindowState.Maximized;
            Font = new Font("Segoe UI", 10F);
            BackColor = Color.FromArgb(10, 17, 30);
            AllowDrop = true;

            BuildModernShell();
            return;

            var sidebar = new Panel { Dock = DockStyle.Left, Width = 224, BackColor = Color.FromArgb(248, 250, 252), Padding = new Padding(20) };
            sidebar.Controls.Add(new Label { Text = "SAFE IMPORT\n\nYou’ll be reminded to back up files before changes are saved.", ForeColor = Color.FromArgb(71, 85, 105), Font = new Font("Segoe UI", 9.5F), Dock = DockStyle.Bottom, Height = 100 });
            sidebar.Controls.Add(new Label { Text = "Created by\nRenegade9532", ForeColor = Color.FromArgb(37, 99, 235), Font = new Font("Segoe UI", 10F, FontStyle.Bold), Dock = DockStyle.Bottom, Height = 52 });
            _languageButton = new Button { Text = "LANGUAGE: " + Ui.Language.ToUpperInvariant() + "  ▾", ForeColor = Color.White, BackColor = Color.FromArgb(40, 56, 75), Font = new Font("Consolas", 8.5F, FontStyle.Bold), Dock = DockStyle.Bottom, Height = 34, FlatStyle = FlatStyle.Flat };
            _languageButton.FlatAppearance.BorderSize = 0; _languageButton.Click += delegate { Ui.Language = LanguageSettings.ChooseLanguage(); ApplyLanguage(); };
            sidebar.Controls.Add(_languageButton);
            var helpButton = new Button { Text = "HELP & TUTORIAL", ForeColor = Color.White, BackColor = Color.FromArgb(40, 56, 75), Font = new Font("Consolas", 8.5F, FontStyle.Bold), Dock = DockStyle.Bottom, Height = 34, FlatStyle = FlatStyle.Flat };
            helpButton.FlatAppearance.BorderSize = 0; helpButton.Click += delegate { ShowHelp(); }; sidebar.Controls.Add(helpButton);
            var historyButton = new Button { Text = "ASSIGNMENT HISTORY", ForeColor = Color.White, BackColor = Color.FromArgb(40, 56, 75), Font = new Font("Consolas", 8.5F, FontStyle.Bold), Dock = DockStyle.Bottom, Height = 34, FlatStyle = FlatStyle.Flat };
            historyButton.FlatAppearance.BorderSize = 0; historyButton.Click += delegate { ShowHistory(); }; sidebar.Controls.Add(historyButton);
            sidebar.Controls.Add(new Label { Text = "Need help?\nDiscord: renegade9532", ForeColor = Color.FromArgb(205, 221, 244), Font = new Font("Segoe UI", 9F), Dock = DockStyle.Bottom, Height = 50 });
            sidebar.Controls.Add(new Label { Text = "RENEGADE’S\nVEHICLE IMPORTER", ForeColor = Color.FromArgb(15, 23, 42), Font = new Font("Segoe UI", 17F, FontStyle.Bold), Dock = DockStyle.Top, Height = 70 });
            sidebar.Controls.Add(new Label { Text = "POLICING REDEFINED TOOLS", ForeColor = Color.FromArgb(100, 116, 139), Font = new Font("Segoe UI", 8F, FontStyle.Bold), Dock = DockStyle.Top, Height = 28 });

            _heading = new Label { Text = "Vehicle workspace", ForeColor = Color.FromArgb(15, 23, 42), Font = new Font("Segoe UI", 24F, FontStyle.Bold), AutoSize = true };
            _explanation = new Label { Text = "Add vehicle packs, assign departments, and review changes before importing.", ForeColor = Color.FromArgb(71, 85, 105), AutoSize = false, Height = 42, Font = new Font("Segoe UI", 10F) };
            ConfigureStages();

            _dropHint.Height = 118;
            _dropHint.Dock = DockStyle.Fill;
            _dropHint.AllowDrop = true; _vehicles.AllowDrop = true;
            _dropHint.DragEnter += DragEnterFiles; _dropHint.DragDrop += DragDropFiles;
            _vehicles.DragEnter += DragEnterFiles; _vehicles.DragDrop += DragDropFiles;
            DragEnter += DragEnterFiles; DragDrop += DragDropFiles;
            _vehicles.SelectionMode = SelectionMode.MultiExtended; _vehicles.BackColor = Color.FromArgb(20, 32, 52); _vehicles.ForeColor = Color.FromArgb(230, 237, 250); _vehicles.BorderStyle = BorderStyle.FixedSingle; _vehicles.Font = new Font("Segoe UI", 11F); _vehicles.ItemHeight = 30;
            _vehicles.Dock = DockStyle.Fill;
            _vehicles.IntegralHeight = false;

            _targetBox = new FlowLayoutPanel { AutoSize = false, Height = 140, Dock = DockStyle.Top, Padding = new Padding(18, 16, 18, 12), BackColor = Color.FromArgb(20, 32, 52), WrapContents = true };
            _targetBox.Controls.Add(new Label { Text = "SELECT MULTIPLE MODELS (CTRL / SHIFT), THEN ASSIGN TO", ForeColor = Color.FromArgb(150, 175, 210), Font = new Font("Segoe UI", 8F, FontStyle.Bold), AutoSize = true, Padding = new Padding(0, 7, 12, 0) });
            _department.Width = 300; _department.DropDownStyle = ComboBoxStyle.DropDownList; _department.BackColor = Color.FromArgb(32, 46, 72); _department.ForeColor = Color.White;
            _assign.ForeColor = Color.White; _assign.BackColor = Color.FromArgb(34, 139, 230); _assign.FlatAppearance.BorderSize = 0; _assign.Click += delegate { AssignDepartment(); };
            _scanRpf.ForeColor = Color.White; _scanRpf.BackColor = Color.FromArgb(45, 66, 101); _scanRpf.FlatAppearance.BorderSize = 0; _scanRpf.Click += ScanRpf;
            _findGtaFiles.ForeColor = Color.White; _findGtaFiles.BackColor = Color.FromArgb(45, 66, 101); _findGtaFiles.FlatAppearance.BorderSize = 0; _findGtaFiles.Click += FindGtaFiles;
            _loadAssignments.ForeColor = Color.White; _loadAssignments.BackColor = Color.FromArgb(45, 66, 101); _loadAssignments.FlatAppearance.BorderSize = 0; _loadAssignments.Click += delegate { LoadXmlAssignments(); };
            _removeAssignment.ForeColor = Color.White; _removeAssignment.BackColor = Color.FromArgb(139, 62, 62); _removeAssignment.FlatAppearance.BorderSize = 0; _removeAssignment.Click += delegate { RemoveSelectedAssignments(); };
            _showPreview.ForeColor = Color.White; _showPreview.BackColor = Color.FromArgb(69, 91, 132); _showPreview.FlatAppearance.BorderSize = 0; _showPreview.Click += delegate { ShowPreview(); };
            _back.ForeColor = Color.FromArgb(205, 221, 244); _back.BackColor = Color.FromArgb(45, 66, 101); _back.FlatAppearance.BorderSize = 0; _back.Click += delegate { ChangePage(-1); };
            _next.ForeColor = Color.White; _next.BackColor = Color.FromArgb(34, 139, 230); _next.FlatAppearance.BorderSize = 0; _next.Click += delegate { ChangePage(1); };
            _pageIndicator.ForeColor = Color.FromArgb(150, 175, 210); _pageIndicator.Padding = new Padding(8, 7, 8, 0);
            _assign.Text = Ui.T("Assign selected vehicles"); _scanRpf.Text = Ui.T("Scan DLC.RPF"); _findGtaFiles.Text = Ui.T("Find XMLs in GTA Folder"); _showPreview.Text = Ui.T("Review XML changes"); _back.Text = Ui.T("← Back"); _next.Text = Ui.T("Next →");
            _assignmentStatus.ForeColor = Color.FromArgb(116, 211, 255); _assignmentStatus.Padding = new Padding(8, 7, 8, 0); UpdateAssignmentStatus();
            _progressText.ForeColor = Color.FromArgb(116, 211, 255); _progressText.Padding = new Padding(8, 7, 0, 0);
            _targetBox.Controls.Add(_department); _targetBox.Controls.Add(_assign); _targetBox.Controls.Add(_removeAssignment); _targetBox.Controls.Add(_loadAssignments); _targetBox.Controls.Add(_scanRpf); _targetBox.Controls.Add(_findGtaFiles); _targetBox.Controls.Add(_showPreview); _targetBox.Controls.Add(_back); _targetBox.Controls.Add(_next); _targetBox.Controls.Add(_pageIndicator); _targetBox.Controls.Add(_assignmentStatus); _targetBox.Controls.Add(_progress); _targetBox.Controls.Add(_progressText);
            _targetBox.SetFlowBreak(_assign, true);
            _targetBox.SetFlowBreak(_showPreview, true);
            var import = new Button { Text = "IMPORT SELECTED VEHICLES  →", Height = 52, Dock = DockStyle.Bottom, BackColor = Color.FromArgb(34, 139, 230), ForeColor = Color.White, FlatStyle = FlatStyle.Flat, Font = new Font("Segoe UI", 10F, FontStyle.Bold) };
            import.FlatAppearance.BorderSize = 0;
            import.Click += Import;

            var layout = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(42, 34, 42, 70), RowCount = 6, BackColor = Color.FromArgb(255, 255, 255) };
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize)); layout.RowStyles.Add(new RowStyle(SizeType.AutoSize)); layout.RowStyles.Add(new RowStyle(SizeType.AutoSize)); layout.RowStyles.Add(new RowStyle(SizeType.AutoSize)); layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layout.Controls.Add(_heading, 0, 0); layout.Controls.Add(_explanation, 0, 1); layout.Controls.Add(_stages, 0, 2); layout.Controls.Add(_dropHint, 0, 3); layout.Controls.Add(_vehicles, 0, 4); layout.Controls.Add(_targetBox, 0, 5);
            Controls.Add(layout); Controls.Add(sidebar); Controls.Add(import);
            import.BringToFront();
            _defaultRegions.TextChanged += delegate { RefreshDepartments(); }; _specialUnits.TextChanged += delegate { RefreshDepartments(); }; _customRegions.TextChanged += delegate { RefreshDepartments(); };
            _stages.SelectedIndexChanged += delegate { UpdateWizardScreen(); if (_stages.SelectedIndex == 4) ShowPreview(); else if (_stages.SelectedIndex > 0) BindDepartments(); };
            UpdateWizardScreen();
        }

        private void BuildModernShell()
        {
            var navy = Color.FromArgb(12, 20, 36); var panel = Color.FromArgb(24, 35, 56); var blue = Color.FromArgb(59, 130, 246);
            var root = new Panel { Dock = DockStyle.Fill, BackColor = navy };
            var rail = new Panel { Dock = DockStyle.Left, Width = 250, BackColor = Color.FromArgb(17, 27, 45), Padding = new Padding(22) };
            rail.Controls.Add(new Label { Text = "RENEGADE’S\nVEHICLE IMPORTER", Dock = DockStyle.Top, Height = 82, ForeColor = Color.White, Font = new Font("Segoe UI", 18F, FontStyle.Bold) });
            rail.Controls.Add(new Label { Text = "POLICING REDEFINED TOOLS", Dock = DockStyle.Top, Height = 30, ForeColor = Color.FromArgb(148, 163, 184), Font = new Font("Segoe UI", 8F, FontStyle.Bold) });
            var help = NavButton("Help & tutorial", panel); help.Click += delegate { ShowHelp(); }; var history = NavButton("Assignment history", panel); history.Click += delegate { ShowHistory(); };
            var language = NavButton("Language: " + Ui.Language, panel); language.Click += delegate { Ui.Language = LanguageSettings.ChooseLanguage(); ApplyLanguage(); language.Text = "Language: " + Ui.Language; };
            rail.Controls.Add(language); rail.Controls.Add(history); rail.Controls.Add(help);
            rail.Controls.Add(new Label { Text = "\nNeed help?\nDiscord: renegade9532\n\nCreated by Renegade9532", Dock = DockStyle.Bottom, Height = 115, ForeColor = Color.FromArgb(148, 163, 184), Font = new Font("Segoe UI", 9F) });

            var body = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(34, 28, 34, 28), BackColor = navy, RowCount = 5 };
            body.RowStyles.Add(new RowStyle(SizeType.Absolute, 90)); body.RowStyles.Add(new RowStyle(SizeType.Absolute, 125)); body.RowStyles.Add(new RowStyle(SizeType.Absolute, 115)); body.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); body.RowStyles.Add(new RowStyle(SizeType.Absolute, 128));
            var title = new Label { Text = "Vehicle workspace", ForeColor = Color.White, Font = new Font("Segoe UI", 25F, FontStyle.Bold), AutoSize = true, Location = new Point(0, 0) };
            var subtitle = new Label { Text = "Import packs, assign agencies, and update your Policing Redefined XML files.", ForeColor = Color.FromArgb(180, 194, 212), Font = new Font("Segoe UI", 10F), AutoSize = true, Location = new Point(2, 47) };
            var header = new Panel { Dock = DockStyle.Fill }; header.Controls.Add(title); header.Controls.Add(subtitle);
            var defaultBtn = HeaderButton("Default regions", blue); var specialBtn = HeaderButton("Special units", panel); var customBtn = HeaderButton("Custom regions", panel);
            defaultBtn.Location = new Point(540, 18); specialBtn.Location = new Point(670, 18); customBtn.Location = new Point(785, 18);
            defaultBtn.Click += delegate { _stages.SelectedIndex = 1; BindDepartments(); }; specialBtn.Click += delegate { _stages.SelectedIndex = 2; BindDepartments(); }; customBtn.Click += delegate { _stages.SelectedIndex = 3; BindDepartments(); };
            header.Controls.Add(defaultBtn); header.Controls.Add(specialBtn); header.Controls.Add(customBtn);

            var files = new Panel { Dock = DockStyle.Fill, BackColor = panel, Padding = new Padding(18) };
            var fileGrid = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 3, RowCount = 3 }; fileGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 165)); fileGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100)); fileGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 90));
            AddFileRow(fileGrid, 0, "Default Regions XML", _defaultRegions); AddFileRow(fileGrid, 1, "Special Units XML", _specialUnits); AddFileRow(fileGrid, 2, "Custom Regions XML", _customRegions); files.Controls.Add(fileGrid);

            _dropHint.Text = "DROP DLC.RPF FILES OR A PACK FOLDER HERE"; _dropHint.Dock = DockStyle.Fill; _dropHint.BackColor = Color.FromArgb(30, 49, 77); _dropHint.ForeColor = Color.FromArgb(191, 219, 254); _dropHint.AllowDrop = true; _dropHint.DragEnter += DragEnterFiles; _dropHint.DragDrop += DragDropFiles;
            var queue = new Panel { Dock = DockStyle.Fill, BackColor = panel, Padding = new Padding(18) }; queue.Controls.Add(new Label { Text = "VEHICLE QUEUE", Dock = DockStyle.Top, Height = 28, ForeColor = Color.FromArgb(191, 219, 254), Font = new Font("Segoe UI", 9F, FontStyle.Bold) }); _vehicles.Dock = DockStyle.Fill; _vehicles.AllowDrop = true; _vehicles.DragEnter += DragEnterFiles; _vehicles.DragDrop += DragDropFiles; queue.Controls.Add(_vehicles);
            _targetBox = new FlowLayoutPanel { Dock = DockStyle.Fill, BackColor = panel, Padding = new Padding(18), WrapContents = true };
            _department.Width = 330; _department.DropDownStyle = ComboBoxStyle.DropDownList; _assign.Text = "Assign selected"; _assign.BackColor = blue; _assign.ForeColor = Color.White; _assign.FlatStyle = FlatStyle.Flat; _assign.Click += delegate { AssignDepartment(); }; _removeAssignment.Click += delegate { RemoveSelectedAssignments(); }; _loadAssignments.Click += delegate { LoadXmlAssignments(); }; _scanRpf.Click += ScanRpf; _findGtaFiles.Click += FindGtaFiles; _showPreview.Click += delegate { ShowPreview(); };
            foreach (Button b in new[] { _removeAssignment, _loadAssignments, _scanRpf, _findGtaFiles, _showPreview }) { b.BackColor = Color.FromArgb(51, 65, 85); b.ForeColor = Color.White; b.FlatStyle = FlatStyle.Flat; }
            _targetBox.Controls.Add(new Label { Text = "ASSIGN SELECTED VEHICLES TO", ForeColor = Color.FromArgb(191, 219, 254), AutoSize = true, Padding = new Padding(0, 10, 12, 0) }); _targetBox.Controls.Add(_department); _targetBox.Controls.Add(_assign); _targetBox.Controls.Add(_removeAssignment); _targetBox.Controls.Add(_loadAssignments); _targetBox.Controls.Add(_scanRpf); _targetBox.Controls.Add(_findGtaFiles); _targetBox.Controls.Add(_showPreview); _targetBox.Controls.Add(_assignmentStatus); _targetBox.Controls.Add(_progress); _targetBox.Controls.Add(_progressText);
            ConfigureStages(); _stages.SelectedIndex = 1; _defaultRegions.TextChanged += delegate { RefreshDepartments(); }; _specialUnits.TextChanged += delegate { RefreshDepartments(); }; _customRegions.TextChanged += delegate { RefreshDepartments(); };
            body.Controls.Add(header, 0, 0); body.Controls.Add(files, 0, 1); body.Controls.Add(_dropHint, 0, 2); body.Controls.Add(queue, 0, 3); body.Controls.Add(_targetBox, 0, 4);
            root.Controls.Add(body); root.Controls.Add(rail); Controls.Add(root); RefreshDepartments();
        }

        private static Button NavButton(string text, Color backColor) { return new Button { Text = text, Dock = DockStyle.Top, Height = 40, TextAlign = ContentAlignment.MiddleLeft, BackColor = backColor, ForeColor = Color.White, FlatStyle = FlatStyle.Flat, Font = new Font("Segoe UI", 10F) }; }
        private static Button HeaderButton(string text, Color backColor) { return new Button { Text = text, Size = new Size(120, 38), BackColor = backColor, ForeColor = Color.White, FlatStyle = FlatStyle.Flat, Font = new Font("Segoe UI", 9F, FontStyle.Bold) }; }

        private void ApplyLanguage()
        {
            Text = Ui.T("Renegade's Vehicle Importer | MDT");
            _assign.Text = Ui.T("Assign selected vehicles"); _scanRpf.Text = Ui.T("Scan DLC.RPF"); _findGtaFiles.Text = Ui.T("Find XMLs in GTA Folder");
            _showPreview.Text = Ui.T("Review XML changes"); _back.Text = Ui.T("← Back"); _next.Text = Ui.T("Next →");
            if (_heading != null) _heading.Text = Ui.T("Vehicle Assignment Console"); if (_explanation != null) _explanation.Text = Ui.T("Locate files • scan vehicle packs • assign agencies • review • import");
            if (_languageButton != null) _languageButton.Text = "LANGUAGE: " + Ui.Language.ToUpperInvariant() + "  ▾";
            _vehicles.Refresh();
        }

        private void UpdateWizardScreen()
        {
            var fullScreen = _stages.SelectedTab == _welcomePage || _stages.SelectedTab == _previewPage;
            _dropHint.Visible = !fullScreen; _vehicles.Visible = !fullScreen; _targetBox.Visible = !fullScreen;
            _stages.Height = fullScreen ? 500 : 210;
            var page = _stages.SelectedIndex < 0 ? 0 : _stages.SelectedIndex;
            _back.Enabled = page > 1;
            _next.Text = page == 3 ? "Review →" : "Next →";
            _pageIndicator.Text = "STEP " + page + " OF 4";
        }

        private void ChangePage(int direction)
        {
            _stages.SelectedIndex = Math.Max(1, Math.Min(4, _stages.SelectedIndex + direction));
        }

        private void ShowHelp()
        {
            using (var help = new Form { Text = "Renegade's Vehicle Importer — Help", Size = new Size(650, 540), StartPosition = FormStartPosition.CenterParent, BackColor = Color.FromArgb(13, 18, 25), ForeColor = Color.White })
            {
                var text = new RichTextBox { Dock = DockStyle.Fill, ReadOnly = true, BorderStyle = BorderStyle.None, BackColor = Color.FromArgb(13, 18, 25), ForeColor = Color.FromArgb(220, 231, 248), Font = new Font("Segoe UI", 11F), Text = "HELP & TUTORIAL\n\n1. Find your XML files\nUse Find XMLs in GTA Folder or browse to each file.\n\n2. Add vehicle packs\nDrop DLC.RPF files or a complete pack folder. Each vehicle shows its source pack.\n\n3. Assign vehicles\nSelect multiple vehicles with Ctrl or Shift, choose an agency, then select Assign selected vehicles.\n\n4. Edit assignments\nUse Load XML vehicles to view existing assignments. Reassign selected vehicles or use Remove selected.\n\n5. Review and import\nReview the generated XML and back up your files when prompted.\n\nOpening this help window never clears your progress.\n\nSupport: Discord renegade9532" };
                help.Controls.Add(text); help.ShowDialog(this);
            }
        }

        private void ConfigureStages()
        {
            _stages.Dock = DockStyle.Top; _stages.Height = 210; _stages.Font = new Font("Segoe UI", 9F, FontStyle.Bold); _stages.Appearance = TabAppearance.FlatButtons; _stages.SizeMode = TabSizeMode.Fixed; _stages.ItemSize = new Size(0, 1);
            _welcomePage.BackColor = Color.FromArgb(24, 35, 57); _welcomePage.Padding = new Padding(22);
            var begin = new Button { Text = "START SETUP  →", AutoSize = true, BackColor = Color.FromArgb(34, 139, 230), ForeColor = Color.White, FlatStyle = FlatStyle.Flat, Font = new Font("Segoe UI", 10F, FontStyle.Bold), Padding = new Padding(12, 7, 12, 7) };
            begin.FlatAppearance.BorderSize = 0; begin.Click += delegate { _stages.SelectedIndex = 1; };
            var skip = new Button { Text = "Skip tutorial", AutoSize = true, BackColor = Color.FromArgb(45, 66, 101), ForeColor = Color.White, FlatStyle = FlatStyle.Flat, Font = new Font("Segoe UI", 10F), Padding = new Padding(12, 7, 12, 7) };
            skip.FlatAppearance.BorderSize = 0; skip.Click += delegate { _stages.SelectedIndex = 1; };
            var welcomeText = new Label { Text = "WELCOME TO RENEGADE'S VEHICLE IMPORTER\n\n1. FIND YOUR FILES\nUse Find XMLs in GTA Folder, or choose each XML file yourself.\n\n2. ADD VEHICLE PACKS\nDrop one or more DLC.RPF files, or drag in the whole pack folder. Every vehicle shows the pack it came from.\n\n3. ASSIGN VEHICLES\nOn each step, select one or more vehicles (hold Ctrl or Shift), choose an agency, then select Assign selected vehicles.\n\n4. EDIT EXISTING ASSIGNMENTS\nUse Load XML vehicles to see current assignments. Select vehicles to reassign them, or use Remove selected.\n\n5. REVIEW AND IMPORT\nReview the generated XML, back up your files when reminded, then import. Selected custom vehicles replace the stock vehicles for that agency.\n\nNeed help? Discord: renegade9532", ForeColor = Color.FromArgb(220, 231, 248), Font = new Font("Segoe UI", 10F), AutoSize = true, Dock = DockStyle.Top };
            _welcomePage.Controls.Add(begin); _welcomePage.Controls.Add(skip); _welcomePage.Controls.Add(welcomeText); begin.Dock = DockStyle.Bottom; skip.Dock = DockStyle.Bottom; _stages.TabPages.Add(_welcomePage);
            AddStage("1  Default Regions", "Default Regions XML", _defaultRegions);
            AddStage("2  Special Units", "Special Units XML", _specialUnits);
            AddStage("3  Custom Regions", "Custom Regions XML", _customRegions);
            _previewPage.Text = "4  Review & Import"; _previewPage.BackColor = Color.FromArgb(24, 35, 57); _previewPage.Padding = new Padding(12);
            _preview.BackColor = Color.FromArgb(11, 18, 32); _preview.ForeColor = Color.FromArgb(203, 224, 255); _preview.BorderStyle = BorderStyle.None; _preview.Font = new Font("Consolas", 9F); _preview.Dock = DockStyle.Fill; _preview.ReadOnly = true; _preview.WordWrap = false;
            var previewBack = new Button { Text = "← Back to Custom Regions", Dock = DockStyle.Bottom, Height = 38, FlatStyle = FlatStyle.Flat, BackColor = Color.FromArgb(45, 66, 101), ForeColor = Color.White };
            previewBack.FlatAppearance.BorderSize = 0; previewBack.Click += delegate { _stages.SelectedIndex = 3; };
            _previewPage.Controls.Add(_preview); _previewPage.Controls.Add(previewBack); _stages.TabPages.Add(_previewPage);
            _stages.SelectedTab = _welcomePage;
        }

        private void AddStage(string title, string label, TextBox box)
        {
            var page = new TabPage(title) { BackColor = Color.FromArgb(24, 35, 57), Padding = new Padding(12) };
            var table = new TableLayoutPanel { ColumnCount = 3, Dock = DockStyle.Top, Height = 72 };
            table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 160)); table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100)); table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 78));
            table.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            AddFileRow(table, 0, label, box); page.Controls.Add(table); _stages.TabPages.Add(page);
        }

        private static TextBox FileBox(string placeholder) { return new TextBox { Dock = DockStyle.Fill, Tag = placeholder, BackColor = Color.FromArgb(32, 46, 72), ForeColor = Color.White, BorderStyle = BorderStyle.FixedSingle, Font = new Font("Segoe UI", 9.5F) }; }

        private void AddFileRow(TableLayoutPanel panel, int row, string label, TextBox box)
        {
            panel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            var browse = new Button { Text = "Browse", Dock = DockStyle.Fill, FlatStyle = FlatStyle.Flat, BackColor = Color.FromArgb(45, 66, 101), ForeColor = Color.White };
            browse.FlatAppearance.BorderSize = 0;
            browse.Click += delegate { using (var dialog = new OpenFileDialog { Filter = "XML files (*.xml)|*.xml", Title = "Choose " + label }) if (dialog.ShowDialog() == DialogResult.OK) box.Text = dialog.FileName; };
            panel.Controls.Add(new Label { Text = label, ForeColor = Color.FromArgb(214, 225, 244), AutoSize = true, Padding = new Padding(12, 7, 8, 5) }, 0, row); panel.Controls.Add(box, 1, row); panel.Controls.Add(browse, 2, row);
        }

        private void DragEnterFiles(object sender, DragEventArgs e) { e.Effect = e.Data.GetDataPresent(DataFormats.FileDrop) ? DragDropEffects.Copy : DragDropEffects.None; }
        private void DragDropFiles(object sender, DragEventArgs e)
        {
            try
            {
                var paths = (string[])e.Data.GetData(DataFormats.FileDrop);
                var names = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (var path in paths)
                {
                    if (File.Exists(path) && Path.GetExtension(path).Equals(".rpf", StringComparison.OrdinalIgnoreCase)) AddVehicles(RpfReader.ReadVehicleModels(path), Path.GetFileName(Path.GetDirectoryName(path)));
                    else if (Directory.Exists(path))
                    {
                        var packName = Path.GetFileName(path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
                        var rpfs = Directory.EnumerateFiles(path, "dlc.rpf", SearchOption.AllDirectories).ToList();
                        if (rpfs.Count > 0) foreach (var rpf in rpfs) AddVehicles(RpfReader.ReadVehicleModels(rpf), packName);
                        else CollectModels(path, names);
                    }
                    else CollectModels(path, names);
                }
                AddVehicles(names, "Loose vehicle files");
                UpdateAssignmentStatus();
                _dropHint.Text = _vehicles.Items.Count == 0 ? "No vehicle .yft files were found — drop a DLC.RPF file or a pack folder." : "Vehicle packs added. Each vehicle displays the pack it came from.";
            }
            catch (Exception ex) { _dropHint.Text = "Could not read that file. Use Scan DLC.RPF for details."; MessageBox.Show("The dropped file could not be read.\n\n" + ex.Message, Text, MessageBoxButtons.OK, MessageBoxIcon.Warning); }
        }

        private async void ScanRpf(object sender, EventArgs e)
        {
            using (var dialog = new OpenFileDialog { Filter = "GTA V DLC archive (dlc.rpf)|dlc.rpf|RPF archives (*.rpf)|*.rpf", Title = "Choose one or more DLC.RPF files", Multiselect = true })
            {
                if (dialog.ShowDialog() != DialogResult.OK) return;
                try
                {
                    SetBusy(true, "Scanning DLC.RPF…");
                    var files = dialog.FileNames;
                    var results = await Task.Run(() => files.Select(path => new { Path = path, Models = RpfReader.ReadVehicleModels(path) }).ToList());
                    foreach (var result in results) AddVehicles(result.Models, Path.GetFileName(Path.GetDirectoryName(result.Path)));
                    _dropHint.Text = results.Sum(x => x.Models.Count) + " vehicle model(s) found. Each vehicle shows the pack it came from.";
                }
                catch (Exception ex) { MessageBox.Show("The archive could not be read.\n\n" + ex.Message, Text, MessageBoxButtons.OK, MessageBoxIcon.Warning); }
                finally { SetBusy(false, ""); }
            }
        }

        private async void FindGtaFiles(object sender, EventArgs e)
        {
            using (var dialog = new FolderBrowserDialog { Description = "Select your GTA V installation folder" })
            {
                if (dialog.ShowDialog() != DialogResult.OK) return;
                try
                {
                    SetBusy(true, "Searching GTA V folder…");
                    var found = await Task.Run(() => FindPolicingRedefinedFiles(dialog.SelectedPath));
                    if (found.ContainsKey("DefaultRegions.xml")) _defaultRegions.Text = found["DefaultRegions.xml"];
                    if (found.ContainsKey("SpecialUnits.xml")) _specialUnits.Text = found["SpecialUnits.xml"];
                    if (found.ContainsKey("CustomRegions.xml")) _customRegions.Text = found["CustomRegions.xml"];
                    var missing = new[] { "DefaultRegions.xml", "SpecialUnits.xml", "CustomRegions.xml" }.Where(x => !found.ContainsKey(x)).ToList();
                    MessageBox.Show(missing.Count == 0 ? "All Policing Redefined XML files were found and added." : "Found " + found.Count + " file(s). Not found: " + string.Join(", ", missing), Text, MessageBoxButtons.OK, missing.Count == 0 ? MessageBoxIcon.Information : MessageBoxIcon.Warning);
                }
                catch (Exception ex) { MessageBox.Show("The GTA V folder could not be searched.\n\n" + ex.Message, Text, MessageBoxButtons.OK, MessageBoxIcon.Warning); }
                finally { SetBusy(false, ""); }
            }
        }

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
                    foreach (var file in Directory.EnumerateFiles(directory, "*.xml"))
                    {
                        var name = Path.GetFileName(file);
                        if (wanted.Contains(name) && !found.ContainsKey(name)) found[name] = file;
                    }
                    foreach (var subdirectory in Directory.EnumerateDirectories(directory)) pending.Push(subdirectory);
                }
                catch (UnauthorizedAccessException) { }
                catch (IOException) { }
            }
            return found;
        }

        private void RefreshDepartments()
        {
            _departments.Clear();
            AddDefaultDepartments(_defaultRegions.Text); AddSpecialDepartments(_specialUnits.Text); AddCustomDepartments(_customRegions.Text);
            BindDepartments();
        }
        private void BindDepartments()
        {
            if (_stages.SelectedIndex == 0 || _stages.SelectedIndex == 4) { _department.DataSource = null; return; }
            var selected = _department.SelectedItem as DepartmentTarget;
            var kind = _stages.SelectedIndex == 1 ? "Default" : _stages.SelectedIndex == 2 ? "Special" : "Custom";
            var visible = _departments.Where(x => x.Kind == kind).ToList();
            _department.DataSource = null; _department.DataSource = visible;
            if (selected != null) _department.SelectedItem = visible.FirstOrDefault(x => x.Id == selected.Id);
        }

        private void AddDefaultDepartments(string path) { AddDepartments(path, "Default", doc => doc.Descendants("LocalPatrol").Elements().Select(x => new DepartmentTarget(path, "Default", x.Name.LocalName, "Default Regions • " + x.Name.LocalName))); }
        private void AddSpecialDepartments(string path) { AddDepartments(path, "Special", doc => doc.Descendants("SpecialUnit").Select(x => new DepartmentTarget(path, "Special", (string)x.Attribute("name"), "Special Units • " + ((string)x.Attribute("name") ?? "Unnamed unit")))); }
        private void AddCustomDepartments(string path) { AddDepartments(path, "Custom", doc => doc.Descendants("LocalPatrol").Elements("CustomRegion").Select(x => new DepartmentTarget(path, "Custom", (string)x.Attribute("name"), "Custom Regions • " + ((string)x.Attribute("name") ?? "Unnamed region")))); }
        private void AddDepartments(string path, string kind, Func<XDocument, IEnumerable<DepartmentTarget>> read)
        {
            if (!File.Exists(path)) return;
            try { _departments.AddRange(read(XDocument.Load(path))); } catch { }
        }
        private void AssignDepartment()
        {
            var department = _department.SelectedItem as DepartmentTarget;
            if (department == null || _vehicles.SelectedItems.Count == 0) { MessageBox.Show("Select one or more vehicles and choose an agency in this stage.", Text, MessageBoxButtons.OK, MessageBoxIcon.Information); return; }
            foreach (VehicleChoice choice in _vehicles.SelectedItems) choice.Department = department;
            _vehicles.Refresh(); UpdateAssignmentStatus();
            var models = string.Join(", ", _vehicles.SelectedItems.Cast<VehicleChoice>().Select(x => x.Model));
            AddHistory("Assigned " + models + " to " + department.Display);
            MessageBox.Show("Vehicle assignment saved.\n\n" + models + "\n\nAgency: " + department.Display, Text, MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void RemoveSelectedAssignments()
        {
            var selected = _vehicles.SelectedItems.Cast<VehicleChoice>().ToList();
            if (selected.Count == 0) { MessageBox.Show("Select a vehicle first.", Text, MessageBoxButtons.OK, MessageBoxIcon.Information); return; }
            foreach (var choice in selected) _vehicles.Items.Remove(choice);
            _vehicles.Refresh(); UpdateAssignmentStatus();
        }

        private void UpdateAssignmentStatus()
        {
            var assigned = _vehicles.Items.Cast<VehicleChoice>().Count(x => x.Department != null);
            _assignmentStatus.Text = assigned + " vehicle(s) assigned";
        }

        private void AddHistory(string entry)
        {
            _history.Insert(0, DateTime.Now.ToString("HH:mm:ss") + "  " + entry);
        }

        private void ShowHistory()
        {
            using (var history = new Form { Text = "Assignment History", Size = new Size(720, 450), StartPosition = FormStartPosition.CenterParent, BackColor = Color.FromArgb(13, 18, 25) })
            {
                var log = new ListBox { Dock = DockStyle.Fill, BackColor = Color.FromArgb(18, 29, 40), ForeColor = Color.FromArgb(220, 231, 248), Font = new Font("Consolas", 10F), BorderStyle = BorderStyle.None };
                foreach (var item in _history) log.Items.Add(item);
                if (log.Items.Count == 0) log.Items.Add("No selections or assignments have been recorded yet.");
                history.Controls.Add(log); history.ShowDialog(this);
            }
        }

        private void LoadXmlAssignments()
        {
            if (_departments.Count == 0) { MessageBox.Show("Choose or find the XML files first.", Text, MessageBoxButtons.OK, MessageBoxIcon.Information); return; }
            _vehicles.Items.Clear(); _loadedDepartments.Clear();
            var cache = new Dictionary<string, XDocument>();
            foreach (var target in _departments)
            {
                if (!cache.ContainsKey(target.FilePath)) cache[target.FilePath] = XDocument.Load(target.FilePath);
                var department = FindDepartment(cache[target.FilePath], target);
                if (department == null) continue;
                _loadedDepartments.Add(target.Id);
                foreach (var vehicle in department.Descendants("Vehicles").Elements("Vehicle")) _vehicles.Items.Add(new VehicleChoice { Model = vehicle.Value.Trim(), Department = target, Source = "Current XML" });
            }
            UpdateAssignmentStatus();
            MessageBox.Show("Current XML vehicle assignments are now shown. Select vehicles to reassign them, or select Remove selected to remove them from the next import.", Text, MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private static void CollectModels(string path, ISet<string> names)
        {
            if (File.Exists(path)) { AddModel(path, names); return; }
            if (!Directory.Exists(path)) return;
            try { foreach (var file in Directory.EnumerateFiles(path, "*.yft", SearchOption.AllDirectories)) AddModel(file, names); } catch (UnauthorizedAccessException) { }
        }

        private static void AddModel(string file, ISet<string> names)
        {
            var name = Path.GetFileNameWithoutExtension(file);
            if (!name.EndsWith("_hi", StringComparison.OrdinalIgnoreCase) && name.Length > 0) names.Add(name.ToUpperInvariant());
        }

        private void AddVehicles(IEnumerable<string> models, string source)
        {
            foreach (var model in models)
            {
                if (!_vehicles.Items.Cast<VehicleChoice>().Any(x => x.Model.Equals(model, StringComparison.OrdinalIgnoreCase) && x.Source.Equals(source, StringComparison.OrdinalIgnoreCase))) _vehicles.Items.Add(new VehicleChoice { Model = model, Source = source });
            }
            UpdateAssignmentStatus();
        }

        private async void Import(object sender, EventArgs e)
        {
            var assignments = _vehicles.Items.Cast<VehicleChoice>().Where(x => x.Department != null).ToList();
            if (assignments.Count == 0) { MessageBox.Show("Select one or more vehicle models, then choose a department for them.", Text, MessageBoxButtons.OK, MessageBoxIcon.Information); return; }
            if (MessageBox.Show("Reminder: back up your three Policing Redefined XML files before importing.\n\nContinue and edit the selected XML files now?", Text, MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return;
            try
            {
                SetBusy(true, "Updating XML files…");
                var loadedTargets = _departments.Where(x => _loadedDepartments.Contains(x.Id)).ToList();
                var results = await Task.Run(() => UpdateDepartments(assignments, loadedTargets));
                AddHistory("Imported " + assignments.Count + " vehicle assignment(s) to " + string.Join(", ", results));
                MessageBox.Show("Import complete — XML files saved.\n\n" + string.Join("\n", results), Text, MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex) { MessageBox.Show("Nothing else was changed after the error.\n\n" + ex.Message, Text, MessageBoxButtons.OK, MessageBoxIcon.Error); }
            finally { SetBusy(false, ""); }
        }

        private void SetBusy(bool busy, string message)
        {
            _progress.Visible = busy; _progressText.Visible = busy; _progressText.Text = message;
            _scanRpf.Enabled = !busy; _findGtaFiles.Enabled = !busy; _assign.Enabled = !busy; _showPreview.Enabled = !busy;
            UseWaitCursor = busy;
        }

        private void ShowPreview()
        {
            var assignments = _vehicles.Items.Cast<VehicleChoice>().Where(x => x.Department != null).ToList();
            if (assignments.Count == 0) { _preview.Text = "Assign vehicles to an agency to see a generated XML preview here."; _stages.SelectedTab = _previewPage; return; }
            try
            {
                var output = new StringBuilder();
                foreach (var fileGroup in assignments.GroupBy(x => x.Department.FilePath))
                {
                    var doc = XDocument.Load(fileGroup.Key, LoadOptions.PreserveWhitespace);
                    foreach (var departmentGroup in fileGroup.GroupBy(x => x.Department.Id))
                    {
                        var target = departmentGroup.First().Department; var department = FindDepartment(doc, target);
                        if (department == null) continue;
                        var models = departmentGroup.Select(x => x.Model).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
                        foreach (var list in department.Descendants("Vehicles"))
                        {
                            list.Elements("Vehicle").Remove();
                            for (var i = 0; i < models.Count; i++) list.Add(new XElement("Vehicle", new XAttribute("chance", i == models.Count - 1 ? 100 - (100 / models.Count) * i : 100 / models.Count), models[i]));
                        }
                    }
                    output.AppendLine("// " + Path.GetFileName(fileGroup.Key) + " — proposed result");
                    output.AppendLine(doc.ToString()); output.AppendLine(); output.AppendLine(new string('─', 88)); output.AppendLine();
                }
                _preview.Text = output.ToString(); _stages.SelectedTab = _previewPage;
            }
            catch (Exception ex) { _preview.Text = "Preview unavailable: " + ex.Message; _stages.SelectedTab = _previewPage; }
        }

        private static List<string> UpdateDepartments(IList<VehicleChoice> assignments, IList<DepartmentTarget> loadedTargets)
        {
            var results = new List<string>();
            var targets = assignments.Select(x => x.Department).Concat(loadedTargets).GroupBy(x => x.Id).Select(x => x.First()).ToList();
            foreach (var fileGroup in targets.GroupBy(x => x.FilePath))
            {
                var path = fileGroup.Key; var doc = XDocument.Load(path); var changedLists = 0;
                foreach (var target in fileGroup)
                {
                    var department = FindDepartment(doc, target);
                    if (department == null) throw new InvalidDataException("Could not find " + target.Display + " in " + Path.GetFileName(path) + ".");
                    var models = assignments.Where(x => x.Department.Id == target.Id).Select(x => x.Model).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
                    foreach (var list in department.Descendants("Vehicles"))
                    {
                        list.RemoveNodes();
                        for (var i = 0; i < models.Count; i++) list.Add(new XElement("Vehicle", new XAttribute("pax", "1"), new XAttribute("pax_chance", "30"), new XAttribute("chance", i == models.Count - 1 ? 100 - (100 / models.Count) * i : 100 / models.Count), models[i]));
                        var vehicles = list.Elements("Vehicle").ToList();
                        changedLists++;
                    }
                }
                SaveFormatted(doc, path);
                results.Add(Path.GetFileName(path) + ": saved to " + Path.GetDirectoryName(path));
            }
            return results;
        }

        private static void SaveFormatted(XDocument doc, string path)
        {
            var settings = new XmlWriterSettings { Indent = true, IndentChars = "  ", NewLineChars = Environment.NewLine, NewLineHandling = NewLineHandling.Replace };
            using (var writer = XmlWriter.Create(path, settings)) doc.Save(writer);
        }

        private static XElement FindDepartment(XDocument doc, DepartmentTarget target)
        {
            if (target.Kind == "Default") return doc.Descendants("LocalPatrol").Elements(target.Name).FirstOrDefault();
            if (target.Kind == "Special") return doc.Descendants("SpecialUnit").FirstOrDefault(x => string.Equals((string)x.Attribute("name"), target.Name, StringComparison.Ordinal));
            return doc.Descendants("LocalPatrol").Elements("CustomRegion").FirstOrDefault(x => string.Equals((string)x.Attribute("name"), target.Name, StringComparison.Ordinal));
        }
    }

    internal sealed class VehicleChoice
    {
        public string Model { get; set; }
        public string Source { get; set; }
        public DepartmentTarget Department { get; set; }
        public override string ToString() { return Model + "   •   " + (Department == null ? "Unassigned" : Department.Display) + "   [" + (Source ?? "Current XML") + "]"; }
    }

    internal sealed class DepartmentTarget
    {
        public string FilePath { get; private set; }
        public string Kind { get; private set; }
        public string Name { get; private set; }
        public string Display { get; private set; }
        public string Id { get { return FilePath + "|" + Kind + "|" + Name; } }
        public DepartmentTarget(string filePath, string kind, string name, string display) { FilePath = filePath; Kind = kind; Name = name; Display = display; }
        public override string ToString() { return Display; }
    }

    internal static class RpfReader
    {
        public static List<string> ReadVehicleModels(string path)
        {
            var models = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
            using (var stream = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.Read)) ReadArchive(stream, 0, stream.Length, models, 0);
            ScanRawYftNames(path, models);
            if (models.Count == 0) throw new InvalidDataException("No vehicle .yft models were found in this archive.");
            return models.ToList();
        }

        private static void ReadArchive(Stream stream, long archiveStart, long archiveLength, ISet<string> models, int depth)
        {
            if (depth > 8 || archiveStart < 0 || archiveStart + 16 > stream.Length) return;
            stream.Position = archiveStart;
            using (var reader = new BinaryReader(stream, Encoding.ASCII, true))
            {
                var magic = Encoding.ASCII.GetString(reader.ReadBytes(4));
                if (magic != "RPF7") return;
                var entryCount = reader.ReadInt32(); var namesLength = reader.ReadInt32(); var encryption = reader.ReadUInt32();
                if (encryption != 0 && encryption != 0x4E45504F) throw new InvalidDataException("This DLC.RPF uses encryption and cannot be read by the standalone importer.");
                var archiveEnd = Math.Min(stream.Length, archiveStart + archiveLength);
                if (entryCount < 0 || entryCount > 500000 || namesLength < 0 || archiveStart + 16L + entryCount * 16L + namesLength > archiveEnd) throw new InvalidDataException("The RPF table is invalid or incomplete.");
                var entries = reader.ReadBytes(entryCount * 16);
                var names = reader.ReadBytes(namesLength);
                for (var i = 0; i < entryCount; i++)
                {
                    var position = i * 16; var nameOffset = BitConverter.ToUInt16(entries, position);
                    if (nameOffset >= names.Length) continue;
                    var name = ReadName(names, nameOffset);
                    if (name.EndsWith(".yft", StringComparison.OrdinalIgnoreCase))
                    {
                        var model = Path.GetFileNameWithoutExtension(name);
                        if (!model.EndsWith("_hi", StringComparison.OrdinalIgnoreCase) && model.Length > 0) models.Add(model.ToUpperInvariant());
                    }
                    else if (name.EndsWith(".rpf", StringComparison.OrdinalIgnoreCase))
                    {
                        var blockOffset = BitConverter.ToUInt32(entries, position + 4) & 0x00FFFFFF;
                        var packedSize = BitConverter.ToUInt32(entries, position + 8);
                        var unpackedSize = BitConverter.ToUInt32(entries, position + 12);
                        var fileSize = packedSize == 0 ? unpackedSize : packedSize;
                        if (blockOffset != 0x007FFFFF && fileSize >= 16) ReadArchive(stream, archiveStart + blockOffset * 512L, fileSize, models, depth + 1);
                    }
                }
            }
        }

        private static string ReadName(byte[] names, int start)
        {
            var end = start; while (end < names.Length && names[end] != 0) end++;
            return Encoding.ASCII.GetString(names, start, end - start);
        }

        // Mod DLCs sometimes contain nested or non-standard RPF layouts. Filename tables remain readable,
        // so this fallback finds YFT entries even when the archive tree cannot be followed.
        private static void ScanRawYftNames(string path, ISet<string> models)
        {
            const int chunkSize = 1024 * 1024;
            var carry = new byte[128]; var carryLength = 0;
            using (var stream = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                var readBuffer = new byte[chunkSize]; int read;
                while ((read = stream.Read(readBuffer, 0, readBuffer.Length)) > 0)
                {
                    var data = new byte[carryLength + read];
                    Buffer.BlockCopy(carry, 0, data, 0, carryLength); Buffer.BlockCopy(readBuffer, 0, data, carryLength, read);
                    for (var i = 0; i + 3 < data.Length; i++)
                    {
                        if (data[i] != '.' || (data[i + 1] | 32) != 'y' || (data[i + 2] | 32) != 'f' || (data[i + 3] | 32) != 't') continue;
                        var start = i - 1;
                        while (start >= 0 && IsFileNameCharacter(data[start])) start--;
                        var name = Encoding.ASCII.GetString(data, start + 1, i - start - 1);
                        if (name.Length > 0 && !name.EndsWith("_hi", StringComparison.OrdinalIgnoreCase)) models.Add(name.ToUpperInvariant());
                    }
                    carryLength = Math.Min(carry.Length, data.Length);
                    Buffer.BlockCopy(data, data.Length - carryLength, carry, 0, carryLength);
                }
            }
        }

        private static bool IsFileNameCharacter(byte value)
        {
            return (value >= 'a' && value <= 'z') || (value >= 'A' && value <= 'Z') || (value >= '0' && value <= '9') || value == '_' || value == '-';
        }
    }

    internal static class Ui
    {
        public static string Language = "English";
        public static string T(string value)
        {
            if (Language == "Spanish")
            {
                if (value == "Renegade's Vehicle Importer | MDT") return "Importador de Vehículos de Renegade | MDT";
                if (value == "Vehicle Assignment Console") return "Consola de asignación de vehículos";
                if (value == "Locate files • scan vehicle packs • assign agencies • review • import") return "Buscar archivos • escanear paquetes • asignar agencias • revisar • importar";
                if (value == "Add custom vehicles") return "Añadir vehículos personalizados";
                if (value == "Follow the simple steps: choose files, choose vehicles, review, then import.") return "Sigue los pasos: elige archivos, vehículos, revisa e importa.";
                if (value == "Assign selected vehicles") return "Asignar vehículos seleccionados";
                if (value == "Scan DLC.RPF") return "Escanear DLC.RPF";
                if (value == "Find XMLs in GTA Folder") return "Buscar XML en carpeta GTA";
                if (value == "Review XML changes") return "Revisar cambios XML";
                if (value == "← Back") return "← Atrás";
                if (value == "Next →") return "Siguiente →";
            }
            if (Language == "French")
            {
                if (value == "Renegade's Vehicle Importer | MDT") return "Importateur de véhicules Renegade | MDT";
                if (value == "Vehicle Assignment Console") return "Console d’attribution des véhicules";
                if (value == "Locate files • scan vehicle packs • assign agencies • review • import") return "Fichiers • analyse des packs • attribution • vérification • importation";
                if (value == "Add custom vehicles") return "Ajouter des véhicules personnalisés";
                if (value == "Follow the simple steps: choose files, choose vehicles, review, then import.") return "Suivez les étapes : fichiers, véhicules, aperçu, puis importation.";
                if (value == "Assign selected vehicles") return "Attribuer les véhicules sélectionnés";
                if (value == "Scan DLC.RPF") return "Analyser DLC.RPF";
                if (value == "Find XMLs in GTA Folder") return "Trouver les XML dans GTA";
                if (value == "Review XML changes") return "Vérifier les changements XML";
                if (value == "← Back") return "← Retour";
                if (value == "Next →") return "Suivant →";
            }
            if (Language == "German")
            {
                if (value == "Renegade's Vehicle Importer | MDT") return "Renegades Fahrzeug-Importer | MDT";
                if (value == "Vehicle Assignment Console") return "Fahrzeugzuweisungs-Konsole";
                if (value == "Locate files • scan vehicle packs • assign agencies • review • import") return "Dateien • Fahrzeugpakete • Behörden • Prüfung • Import";
                if (value == "Add custom vehicles") return "Eigene Fahrzeuge hinzufügen";
                if (value == "Follow the simple steps: choose files, choose vehicles, review, then import.") return "Schritte: Dateien wählen, Fahrzeuge wählen, prüfen und importieren.";
                if (value == "Assign selected vehicles") return "Ausgewählte Fahrzeuge zuweisen";
                if (value == "Scan DLC.RPF") return "DLC.RPF scannen";
                if (value == "Find XMLs in GTA Folder") return "XMLs im GTA-Ordner finden";
                if (value == "Review XML changes") return "XML-Änderungen prüfen";
                if (value == "← Back") return "← Zurück";
                if (value == "Next →") return "Weiter →";
            }
            return value;
        }
    }

    internal static class LanguageSettings
    {
        public static string LoadOrChoose()
        {
            var file = Path.Combine(Application.UserAppDataPath, "language.txt");
            if (File.Exists(file)) return File.ReadAllText(file).Trim();
            return ChooseLanguage();
        }

        public static string ChooseLanguage()
        {
            var file = Path.Combine(Application.UserAppDataPath, "language.txt");
            var result = "English";
            using (var form = new Form { Text = "Choose your language", Size = new Size(460, 270), StartPosition = FormStartPosition.CenterScreen, FormBorderStyle = FormBorderStyle.FixedDialog, MaximizeBox = false, MinimizeBox = false, BackColor = Color.FromArgb(24, 35, 57) })
            {
                var text = new Label { Text = "Welcome!\n\nPlease choose your language. You can start using the importer straight away after this.", ForeColor = Color.White, Font = new Font("Segoe UI", 11F), AutoSize = false, Size = new Size(390, 90), Location = new Point(28, 25) };
                var choices = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Font = new Font("Segoe UI", 11F), Location = new Point(28, 125), Width = 390 };
                choices.Items.AddRange(LanguageOption.All.Cast<object>().ToArray()); choices.SelectedIndex = 0;
                var continueButton = new Button { Text = "Continue", DialogResult = DialogResult.OK, BackColor = Color.FromArgb(34, 139, 230), ForeColor = Color.White, FlatStyle = FlatStyle.Flat, Font = new Font("Segoe UI", 10F, FontStyle.Bold), Size = new Size(130, 40), Location = new Point(288, 178) };
                continueButton.FlatAppearance.BorderSize = 0; form.AcceptButton = continueButton; form.Controls.Add(text); form.Controls.Add(choices); form.Controls.Add(continueButton);
                form.ShowDialog();
                result = ((LanguageOption)choices.SelectedItem).Code;
            }
            Directory.CreateDirectory(Path.GetDirectoryName(file)); File.WriteAllText(file, result); return result;
        }
    }

    internal sealed class LanguageOption
    {
        public string Name { get; private set; }
        public string Code { get; private set; }
        public LanguageOption(string name, string code) { Name = name; Code = code; }
        public override string ToString() { return Name; }
        public static readonly LanguageOption[] All = new[]
        {
            new LanguageOption("English", "English"), new LanguageOption("Afrikaans", "Afrikaans"), new LanguageOption("العربية — Arabic", "Arabic"), new LanguageOption("বাংলা — Bengali", "Bengali"),
            new LanguageOption("Български — Bulgarian", "Bulgarian"), new LanguageOption("Català — Catalan", "Catalan"), new LanguageOption("中文（简体）— Chinese (Simplified)", "Chinese Simplified"), new LanguageOption("中文（繁體）— Chinese (Traditional)", "Chinese Traditional"),
            new LanguageOption("Hrvatski — Croatian", "Croatian"), new LanguageOption("Čeština — Czech", "Czech"), new LanguageOption("Dansk — Danish", "Danish"), new LanguageOption("Nederlands — Dutch", "Dutch"),
            new LanguageOption("Eesti — Estonian", "Estonian"), new LanguageOption("Suomi — Finnish", "Finnish"), new LanguageOption("Français — French", "French"), new LanguageOption("Deutsch — German", "German"),
            new LanguageOption("Ελληνικά — Greek", "Greek"), new LanguageOption("ગુજરાતી — Gujarati", "Gujarati"), new LanguageOption("עברית — Hebrew", "Hebrew"), new LanguageOption("हिन्दी — Hindi", "Hindi"),
            new LanguageOption("Magyar — Hungarian", "Hungarian"), new LanguageOption("Bahasa Indonesia — Indonesian", "Indonesian"), new LanguageOption("Italiano — Italian", "Italian"), new LanguageOption("日本語 — Japanese", "Japanese"),
            new LanguageOption("ಕನ್ನಡ — Kannada", "Kannada"), new LanguageOption("한국어 — Korean", "Korean"), new LanguageOption("Latviešu — Latvian", "Latvian"), new LanguageOption("Lietuvių — Lithuanian", "Lithuanian"),
            new LanguageOption("Bahasa Melayu — Malay", "Malay"), new LanguageOption("मराठी — Marathi", "Marathi"), new LanguageOption("Norsk — Norwegian", "Norwegian"), new LanguageOption("فارسی — Persian", "Persian"),
            new LanguageOption("Polski — Polish", "Polish"), new LanguageOption("Português (Brasil) — Portuguese (Brazil)", "Portuguese Brazil"), new LanguageOption("Português (Portugal) — Portuguese (Portugal)", "Portuguese Portugal"),
            new LanguageOption("ਪੰਜਾਬੀ — Punjabi", "Punjabi"), new LanguageOption("Română — Romanian", "Romanian"), new LanguageOption("Русский — Russian", "Russian"), new LanguageOption("Српски — Serbian", "Serbian"),
            new LanguageOption("Slovenčina — Slovak", "Slovak"), new LanguageOption("Slovenščina — Slovenian", "Slovenian"), new LanguageOption("Español — Spanish", "Spanish"), new LanguageOption("Svenska — Swedish", "Swedish"),
            new LanguageOption("தமிழ் — Tamil", "Tamil"), new LanguageOption("తెలుగు — Telugu", "Telugu"), new LanguageOption("ไทย — Thai", "Thai"), new LanguageOption("Türkçe — Turkish", "Turkish"),
            new LanguageOption("Українська — Ukrainian", "Ukrainian"), new LanguageOption("اردو — Urdu", "Urdu"), new LanguageOption("Tiếng Việt — Vietnamese", "Vietnamese"), new LanguageOption("Cymraeg — Welsh", "Welsh")
        };
    }
}
