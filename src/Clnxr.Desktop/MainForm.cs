using System;
using System.ComponentModel;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Clnxr.Application;
using Clnxr.Core;
using Clnxr.Evidence;
using Clnxr.Platform.Windows;

namespace Clnxr.Desktop
{
    internal enum DesktopPage
    {
        Overview,
        Scan,
        Results,
        Tools,
        History,
        Rules,
        Settings
    }

    internal sealed class FindingRow
    {
        public bool Selected { get; set; }
        public string Risk { get; set; }
        public string Category { get; set; }
        public string Explanation { get; set; }
        public string RequiredProcesses { get; set; }
        public long Files { get; set; }
        public string EstimatedSize { get; set; }
        public string Volume { get; set; }
        public string RuleId { get; set; }
        public string Path { get; set; }
        public Finding Finding { get; set; }
    }

    internal sealed class RuleRow
    {
        public string RuleId { get; set; }
        public string Version { get; set; }
        public string Risk { get; set; }
        public string Category { get; set; }
        public string MinimumAge { get; set; }
        public string RequiredProcesses { get; set; }
        public string Explanation { get; set; }
    }

    internal sealed class HistoryRow
    {
        public string Receipt { get; set; }
        public string Modified { get; set; }
        public string Integrity { get; set; }
        public string Path { get; set; }
    }

    internal sealed class ReceiptDetailRow
    {
        public string Section { get; set; }
        public string Field { get; set; }
        public string Value { get; set; }
    }

    internal sealed class DiskMapRow
    {
        public string Volume { get; set; }
        public string Path { get; set; }
        public long Files { get; set; }
        public string Size { get; set; }
    }

    internal sealed class LargeFileRow
    {
        public string Volume { get; set; }
        public string Name { get; set; }
        public string Size { get; set; }
        public string Modified { get; set; }
        public string Path { get; set; }
    }

    internal sealed class DuplicateRow
    {
        public string Hash { get; set; }
        public int Files { get; set; }
        public string SizePerFile { get; set; }
        public string Potential { get; set; }
        public string FirstPath { get; set; }
    }

    internal sealed class OverviewRow
    {
        public string Metric { get; set; }
        public string Value { get; set; }
        public string Detail { get; set; }
    }

    internal sealed class MainForm : Form
    {
        private static readonly Color Graphite = Color.FromArgb(22, 25, 31);
        private static readonly Color PanelColor = Color.FromArgb(31, 35, 44);
        private static readonly Color RaisedPanel = Color.FromArgb(39, 44, 55);
        private static readonly Color Cyan = Color.FromArgb(57, 204, 218);
        private static readonly Color TextMuted = Color.FromArgb(177, 188, 201);
        private static readonly Color Success = Color.FromArgb(102, 204, 139);
        private static readonly Color Review = Color.FromArgb(235, 183, 68);

        private readonly CleanerApplicationService application;
        private readonly Label profileLabel;
        private readonly ComboBox profileBox;
        private readonly Button analyzeButton;
        private readonly Button cleanButton;
        private readonly Button cancelButton;
        private readonly Button recycleQueryButton;
        private readonly Button recycleEmptyButton;
        private readonly Button storageSenseButton;
        private readonly Button diskMapButton;
        private readonly Button largeFilesButton;
        private readonly Button duplicatesButton;
        private readonly Button viewReceiptButton;
        private readonly Button exportReceiptButton;
        private readonly Button customRuleButton;
        private readonly Button deleteCustomRuleButton;
        private readonly DataGridView grid;
        private readonly Label pageTitle;
        private readonly Label pageDescription;
        private readonly Label summaryLabel;
        private readonly Label statusLabel;
        private readonly Panel resultsFilterPanel;
        private readonly TextBox resultSearchBox;
        private readonly ComboBox resultRiskBox;
        private readonly CheckBox resultSelectedOnly;
        private readonly Dictionary<DesktopPage, Button> navigation;
        private readonly HashSet<string> personalizedRuleIds;
        private BindingList<FindingRow> allFindingRows;
        private BindingList<FindingRow> findingRows;
        private ScanSession currentSession;
        private CancellationTokenSource activeOperation;
        private DesktopPage currentPage;
        private RecycleBinSnapshot recycleBin;
        private StorageAnalysisResult diskMapResult;
        private StorageAnalysisResult largeFilesResult;
        private StorageAnalysisResult duplicatesResult;
        private readonly object storageProgressLock = new object();
        private DateTime lastStorageProgressUtc;

        public MainForm()
        {
            application = new CleanerApplicationService();
            navigation = new Dictionary<DesktopPage, Button>();
            personalizedRuleIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            Text = "CLNXR (nome provisório)";
            AccessibleName = "CLNXR Portable Windows Cleaner";
            AccessibleRole = AccessibleRole.Window;
            StartPosition = FormStartPosition.CenterScreen;
            MinimumSize = new Size(1040, 660);
            Size = new Size(1260, 780);
            BackColor = Graphite;
            ForeColor = Color.White;
            Font = new Font("Segoe UI", 9f, FontStyle.Regular);

            Panel header = BuildHeader();
            Panel sidebar = BuildSidebar();

            Panel workspace = new Panel();
            workspace.Dock = DockStyle.Fill;
            workspace.BackColor = Graphite;
            workspace.Padding = new Padding(26, 20, 26, 18);

            Panel titlePanel = new Panel();
            titlePanel.Dock = DockStyle.Top;
            titlePanel.Height = 94;
            titlePanel.BackColor = Graphite;

            pageTitle = new Label();
            pageTitle.AutoSize = true;
            pageTitle.Font = new Font("Segoe UI", 20f, FontStyle.Bold);
            pageTitle.ForeColor = Color.White;
            pageTitle.Location = new Point(0, 0);

            pageDescription = new Label();
            pageDescription.AutoSize = false;
            pageDescription.Size = new Size(850, 38);
            pageDescription.ForeColor = TextMuted;
            pageDescription.Location = new Point(2, 40);

            titlePanel.Controls.Add(pageTitle);
            titlePanel.Controls.Add(pageDescription);

            Panel controls = new Panel();
            controls.Dock = DockStyle.Top;
            controls.Height = 110;
            controls.BackColor = PanelColor;
            controls.Padding = new Padding(16, 12, 16, 10);
            controls.Location = new Point(26, 98);

            profileLabel = new Label();
            profileLabel.Text = "Perfil de limpeza";
            profileLabel.AutoSize = true;
            profileLabel.ForeColor = TextMuted;
            profileLabel.Location = new Point(18, 18);

            profileBox = new ComboBox();
            profileBox.DropDownStyle = ComboBoxStyle.DropDownList;
            profileBox.AccessibleName = "Perfil de limpeza";
            profileBox.TabIndex = 0;
            profileBox.Items.Add("Seguro — temporários e relatórios de falha");
            profileBox.Items.Add("Completo — inclui caches de GPU e miniaturas para revisão");
            profileBox.Items.Add("Jogos — cache Unreal selecionado para revisão");
            profileBox.Items.Add("Desenvolvedor — caches NuGet, npm e pip para revisão");
            profileBox.Items.Add("Personalizado — somente regras escolhidas");
            profileBox.SelectedIndex = 0;
            profileBox.Width = 372;
            profileBox.Location = new Point(18, 43);

            analyzeButton = CreateActionButton("Analisar unidades", Cyan, new Point(410, 40));
            analyzeButton.Click += async delegate { await AnalyzeAsync(); };

            cleanButton = CreateActionButton("Limpar selecionados", Success, new Point(557, 40));
            cleanButton.Enabled = false;
            cleanButton.Click += async delegate { await CleanAsync(); };

            cancelButton = CreateActionButton("Cancelar", Review, new Point(731, 40));
            cancelButton.Enabled = false;
            cancelButton.Click += delegate
            {
                if (activeOperation != null) activeOperation.Cancel();
            };

            recycleQueryButton = CreateActionButton("Analisar Lixeira", Cyan, new Point(18, 40));
            recycleQueryButton.Visible = false;
            recycleQueryButton.Click += async delegate { await QueryRecycleBinAsync(); };

            recycleEmptyButton = CreateActionButton("Esvaziar Lixeira", Review, new Point(166, 40));
            recycleEmptyButton.Visible = false;
            recycleEmptyButton.Enabled = false;
            recycleEmptyButton.Click += async delegate { await EmptyRecycleBinAsync(); };

            storageSenseButton = CreateActionButton("Abrir limpeza oficial", Cyan, new Point(312, 40));
            storageSenseButton.Visible = false;
            storageSenseButton.Click += delegate { OpenStorageSense(); };

            diskMapButton = CreateActionButton("Mapa de disco", Cyan, new Point(488, 40));
            diskMapButton.Visible = false;
            diskMapButton.Click += async delegate { await AnalyzeDiskMapAsync(); };

            largeFilesButton = CreateActionButton("Arquivos grandes", Cyan, new Point(611, 40));
            largeFilesButton.Visible = false;
            largeFilesButton.Click += async delegate { await FindLargeFilesAsync(); };

            duplicatesButton = CreateActionButton("Duplicados", Review, new Point(750, 40));
            duplicatesButton.Visible = false;
            duplicatesButton.Click += async delegate { await FindDuplicatesAsync(); };

            viewReceiptButton = CreateActionButton("Ver recibo", Cyan, new Point(18, 40));
            viewReceiptButton.Visible = false;
            viewReceiptButton.Enabled = false;
            viewReceiptButton.Click += delegate { ShowSelectedReceipt(); };

            exportReceiptButton = CreateActionButton("Exportar recibo", Cyan, new Point(140, 40));
            exportReceiptButton.Visible = false;
            exportReceiptButton.Enabled = false;
            exportReceiptButton.Click += delegate { ExportSelectedReceipt(); };

            customRuleButton = CreateActionButton("Adicionar regra personalizada", Success, new Point(18, 40));
            customRuleButton.Visible = false;
            customRuleButton.Click += async delegate { await AddCustomRuleAsync(); };

            deleteCustomRuleButton = CreateActionButton("Excluir personalizada", Review, new Point(230, 40));
            deleteCustomRuleButton.Visible = false;
            deleteCustomRuleButton.Enabled = false;
            deleteCustomRuleButton.Click += delegate { DeleteSelectedCustomRule(); };

            Label protectedData = new Label();
            protectedData.Text = "Proteção fixa: navegadores, logins, cookies, Downloads, saves e arquivos pessoais ficam fora do catálogo.";
            protectedData.AutoSize = true;
            protectedData.ForeColor = TextMuted;
            protectedData.Location = new Point(18, 78);

            controls.Controls.Add(profileLabel);
            controls.Controls.Add(profileBox);
            controls.Controls.Add(analyzeButton);
            controls.Controls.Add(cleanButton);
            controls.Controls.Add(cancelButton);
            controls.Controls.Add(recycleQueryButton);
            controls.Controls.Add(recycleEmptyButton);
            controls.Controls.Add(storageSenseButton);
            controls.Controls.Add(diskMapButton);
            controls.Controls.Add(largeFilesButton);
            controls.Controls.Add(duplicatesButton);
            controls.Controls.Add(viewReceiptButton);
            controls.Controls.Add(exportReceiptButton);
            controls.Controls.Add(customRuleButton);
            controls.Controls.Add(deleteCustomRuleButton);
            controls.Controls.Add(protectedData);

            resultsFilterPanel = new Panel();
            resultsFilterPanel.Dock = DockStyle.Top;
            resultsFilterPanel.Height = 42;
            resultsFilterPanel.BackColor = Graphite;
            resultsFilterPanel.Visible = false;

            Label resultsFilterLabel = new Label();
            resultsFilterLabel.Text = "Filtrar resultados";
            resultsFilterLabel.AutoSize = true;
            resultsFilterLabel.ForeColor = TextMuted;
            resultsFilterLabel.Location = new Point(0, 13);

            resultSearchBox = new TextBox();
            resultSearchBox.Width = 250;
            resultSearchBox.Location = new Point(112, 8);
            resultSearchBox.AccessibleName = "Buscar por categoria, regra ou caminho";
            resultSearchBox.AccessibleRole = AccessibleRole.Text;
            resultSearchBox.TextChanged += delegate { ApplyFindingFilter(); };

            resultRiskBox = new ComboBox();
            resultRiskBox.DropDownStyle = ComboBoxStyle.DropDownList;
            resultRiskBox.Width = 130;
            resultRiskBox.Location = new Point(372, 8);
            resultRiskBox.AccessibleName = "Filtrar por risco";
            resultRiskBox.Items.Add("Todos os riscos");
            resultRiskBox.Items.Add("SAFE");
            resultRiskBox.Items.Add("REVIEW");
            resultRiskBox.Items.Add("ADVANCED");
            resultRiskBox.Items.Add("BLOCKED");
            resultRiskBox.SelectedIndex = 0;
            resultRiskBox.SelectedIndexChanged += delegate { ApplyFindingFilter(); };

            resultSelectedOnly = new CheckBox();
            resultSelectedOnly.Text = "Somente selecionados";
            resultSelectedOnly.AutoSize = true;
            resultSelectedOnly.ForeColor = TextMuted;
            resultSelectedOnly.Location = new Point(516, 11);
            resultSelectedOnly.AccessibleName = "Mostrar somente itens selecionados";
            resultSelectedOnly.CheckedChanged += delegate { ApplyFindingFilter(); };

            resultsFilterPanel.Controls.Add(resultsFilterLabel);
            resultsFilterPanel.Controls.Add(resultSearchBox);
            resultsFilterPanel.Controls.Add(resultRiskBox);
            resultsFilterPanel.Controls.Add(resultSelectedOnly);

            grid = CreateGrid();
            grid.Dock = DockStyle.Fill;
            grid.CellDoubleClick += delegate(object sender, DataGridViewCellEventArgs args)
            {
                if (args.RowIndex >= 0 && args.RowIndex < findingRows.Count) ShowFindingDetails(findingRows[args.RowIndex]);
            };
            grid.CellValueChanged += delegate(object sender, DataGridViewCellEventArgs args)
            {
                if (args.RowIndex >= 0 && args.ColumnIndex == 0) BeginInvoke((MethodInvoker)delegate { ApplyFindingFilter(); });
            };
            grid.SelectionChanged += delegate
            {
                if (currentPage == DesktopPage.Rules && deleteCustomRuleButton != null)
                    deleteCustomRuleButton.Enabled = IsCustomRuleSelected();
            };

            Panel footer = new Panel();
            footer.Dock = DockStyle.Bottom;
            footer.Height = 78;
            footer.BackColor = PanelColor;
            footer.Padding = new Padding(16, 10, 16, 10);

            summaryLabel = new Label();
            summaryLabel.AutoSize = true;
            summaryLabel.Font = new Font("Segoe UI", 10f, FontStyle.Bold);
            summaryLabel.ForeColor = Color.White;
            summaryLabel.Location = new Point(18, 14);

            statusLabel = new Label();
            statusLabel.AutoSize = true;
            statusLabel.ForeColor = Cyan;
            statusLabel.Location = new Point(18, 42);

            footer.Controls.Add(summaryLabel);
            footer.Controls.Add(statusLabel);

            workspace.Controls.Add(grid);
            workspace.Controls.Add(footer);
            workspace.Controls.Add(resultsFilterPanel);
            workspace.Controls.Add(controls);
            workspace.Controls.Add(titlePanel);

            Controls.Add(workspace);
            Controls.Add(sidebar);
            Controls.Add(header);

            Navigate(DesktopPage.Overview);
        }

        private Panel BuildHeader()
        {
            Panel header = new Panel();
            header.Dock = DockStyle.Top;
            header.Height = 72;
            header.BackColor = Color.FromArgb(27, 30, 37);

            Label brand = new Label();
            brand.Text = "CLNXR";
            brand.AutoSize = true;
            brand.Font = new Font("Segoe UI", 21f, FontStyle.Bold);
            brand.ForeColor = Cyan;
            brand.Location = new Point(24, 13);

            Label status = new Label();
            status.Text = "PORTÁTIL • LOCAL-FIRST • ANALISA ANTES DE LIMPAR";
            status.AutoSize = true;
            status.Font = new Font("Segoe UI", 8.5f, FontStyle.Bold);
            status.ForeColor = TextMuted;
            status.Location = new Point(143, 29);

            Label workingName = new Label();
            workingName.Text = "Nome de trabalho — marca pública pendente de validação";
            workingName.AutoSize = true;
            workingName.ForeColor = TextMuted;
            workingName.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            workingName.Location = new Point(760, 29);
            header.Resize += delegate { workingName.Left = Math.Max(540, header.ClientSize.Width - workingName.Width - 24); };

            header.Controls.Add(brand);
            header.Controls.Add(status);
            header.Controls.Add(workingName);
            return header;
        }

        private Panel BuildSidebar()
        {
            Panel sidebar = new Panel();
            sidebar.Dock = DockStyle.Left;
            sidebar.Width = 188;
            sidebar.BackColor = Color.FromArgb(27, 30, 37);
            sidebar.Padding = new Padding(10, 16, 10, 12);

            AddNavigation(sidebar, DesktopPage.Overview, "Visão geral");
            AddNavigation(sidebar, DesktopPage.Scan, "Analisar");
            AddNavigation(sidebar, DesktopPage.Results, "Resultados");
            AddNavigation(sidebar, DesktopPage.Tools, "Ferramentas");
            AddNavigation(sidebar, DesktopPage.History, "Histórico");
            AddNavigation(sidebar, DesktopPage.Rules, "Regras");
            AddNavigation(sidebar, DesktopPage.Settings, "Configurações");
            return sidebar;
        }

        private void AddNavigation(Panel parent, DesktopPage page, string text)
        {
            Button button = new Button();
            button.Text = text;
            button.Tag = page;
            button.Dock = DockStyle.Top;
            button.Height = 44;
            button.FlatStyle = FlatStyle.Flat;
            button.FlatAppearance.BorderSize = 0;
            button.TextAlign = ContentAlignment.MiddleLeft;
            button.Padding = new Padding(12, 0, 0, 0);
            button.ForeColor = TextMuted;
            button.BackColor = Color.Transparent;
            button.AccessibleName = text;
            button.AccessibleRole = AccessibleRole.PushButton;
            button.Click += delegate { Navigate((DesktopPage)button.Tag); };
            navigation.Add(page, button);
            parent.Controls.Add(button);
            parent.Controls.SetChildIndex(button, 0);
        }

        private Button CreateActionButton(string text, Color accent, Point location)
        {
            Button button = new Button();
            button.Text = text;
            button.AutoSize = true;
            button.Height = 32;
            button.Location = location;
            button.FlatStyle = FlatStyle.Flat;
            button.FlatAppearance.BorderColor = accent;
            button.FlatAppearance.BorderSize = 1;
            button.BackColor = RaisedPanel;
            button.ForeColor = Color.White;
            button.AccessibleName = text;
            button.TabStop = true;
            return button;
        }

        private DataGridView CreateGrid()
        {
            DataGridView view = new DataGridView();
            view.AutoGenerateColumns = false;
            view.AllowUserToAddRows = false;
            view.AllowUserToDeleteRows = false;
            view.AllowUserToResizeRows = false;
            view.RowHeadersVisible = false;
            view.BorderStyle = BorderStyle.None;
            view.BackgroundColor = Graphite;
            view.GridColor = Color.FromArgb(56, 62, 74);
            view.EnableHeadersVisualStyles = false;
            view.ColumnHeadersDefaultCellStyle.BackColor = RaisedPanel;
            view.ColumnHeadersDefaultCellStyle.ForeColor = Color.White;
            view.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI", 9f, FontStyle.Bold);
            view.DefaultCellStyle.BackColor = Graphite;
            view.DefaultCellStyle.ForeColor = Color.White;
            view.DefaultCellStyle.SelectionBackColor = Color.FromArgb(45, 65, 76);
            view.DefaultCellStyle.SelectionForeColor = Color.White;
            view.AlternatingRowsDefaultCellStyle.BackColor = Color.FromArgb(26, 29, 36);
            view.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            view.AccessibleName = "Resultados do CLNXR";
            view.AccessibleRole = AccessibleRole.Table;
            view.TabIndex = 1;
            return view;
        }

        private void Navigate(DesktopPage page)
        {
            currentPage = page;
            bool toolToolbar = page == DesktopPage.Tools;
            bool historyToolbar = page == DesktopPage.History;
            bool rulesToolbar = page == DesktopPage.Rules;
            resultsFilterPanel.Visible = page == DesktopPage.Results;
            profileLabel.Visible = !toolToolbar && !historyToolbar;
            profileBox.Visible = !toolToolbar && !historyToolbar;
            analyzeButton.Visible = !toolToolbar && !historyToolbar;
            cleanButton.Visible = !toolToolbar && !historyToolbar;
            recycleQueryButton.Visible = toolToolbar;
            recycleEmptyButton.Visible = toolToolbar;
            storageSenseButton.Visible = toolToolbar;
            diskMapButton.Visible = toolToolbar;
            largeFilesButton.Visible = toolToolbar;
            duplicatesButton.Visible = toolToolbar;
            viewReceiptButton.Visible = historyToolbar;
            exportReceiptButton.Visible = historyToolbar;
            customRuleButton.Visible = rulesToolbar;
            deleteCustomRuleButton.Visible = rulesToolbar;
            deleteCustomRuleButton.Enabled = rulesToolbar && IsCustomRuleSelected();
            foreach (KeyValuePair<DesktopPage, Button> pair in navigation)
            {
                bool active = pair.Key == page;
                pair.Value.BackColor = active ? Color.FromArgb(39, 60, 69) : Color.Transparent;
                pair.Value.ForeColor = active ? Cyan : TextMuted;
            }

            if (page == DesktopPage.History)
            {
                ShowHistory();
                return;
            }
            if (page == DesktopPage.Rules)
            {
                ShowRules();
                return;
            }
            if (page == DesktopPage.Tools)
            {
                ShowTools();
                return;
            }
            if (page == DesktopPage.Settings)
            {
                ShowSettings();
                return;
            }

            if (page == DesktopPage.Overview)
            {
                ShowOverview();
                return;
            }

            BuildFindingsColumns();
            if (page == DesktopPage.Scan)
            {
                pageTitle.Text = "Analisar";
                pageDescription.Text = "A varredura é somente leitura. Cancele a qualquer momento; nenhum arquivo será removido nessa etapa.";
            }
            else
            {
                pageTitle.Text = "Resultados";
                pageDescription.Text = "Revise regra, risco, estimativa e caminho antes de selecionar qualquer ação.";
            }

            BindFindings();
            UpdateActionsForFindings();
        }

        private void ShowOverview()
        {
            pageTitle.Text = "Visão geral";
            pageDescription.Text = "Acompanhe a última análise e inicie uma sessão. O CLNXR não executa limpeza automática.";
            BuildSimpleColumns(new[]
            {
                new ColumnDefinition("Metric", "Indicador", 230),
                new ColumnDefinition("Value", "Estado", 260),
                new ColumnDefinition("Detail", "Interpretação", 650)
            });

            List<OverviewRow> rows = new List<OverviewRow>
            {
                new OverviewRow { Metric = "Proteção de dados", Value = "Ativa e fixa", Detail = "Cookies, logins, histórico, Downloads, saves e áreas pessoais ficam fora das regras padrão." },
                new OverviewRow { Metric = "Telemetria", Value = "Desligada", Detail = "Nenhuma lista de arquivos ou histórico sai deste computador sem exportação explícita." },
                new OverviewRow { Metric = "Movimento", Value = "Interface estática", Detail = "Não há loops, partículas ou transformações; o modo reduzido não perde informação." }
            };

            if (currentSession == null)
            {
                rows.Add(new OverviewRow { Metric = "Última análise", Value = "Nenhuma", Detail = "Escolha um perfil e use Analisar unidades para criar uma sessão cancelável." });
                summaryLabel.Text = "Nenhuma sessão criada nesta execução.";
                statusLabel.ForeColor = Cyan;
                statusLabel.Text = "Pronto para uma análise somente leitura.";
            }
            else
            {
                long bytes = currentSession.Findings.Sum(finding => finding.EstimatedBytes);
                long files = currentSession.Findings.Sum(finding => finding.FileCount);
                rows.Add(new OverviewRow { Metric = "Última análise", Value = currentSession.ProfileName + " — " + currentSession.State, Detail = currentSession.StartedUtc.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss") });
                rows.Add(new OverviewRow { Metric = "Prévia encontrada", Value = string.Format("{0:N0} arquivo(s) / {1}", files, SizeText(bytes)), Detail = "A estimativa só vira espaço recuperado depois da execução e do recibo." });
                summaryLabel.Text = string.Format("Sessão {0}: {1:N0} arquivo(s) em prévia.", currentSession.ProfileName, files);
                statusLabel.ForeColor = currentSession.State == SessionState.Cancelled ? Review : Cyan;
                statusLabel.Text = currentSession.Issues.Count == 0 ? "A análise pode ser revisada na página Resultados." : string.Format("{0} aviso(s) foram preservados no diagnóstico.", currentSession.Issues.Count);
            }

            grid.DataSource = new BindingList<OverviewRow>(rows);
            cleanButton.Enabled = false;
        }

        private void ShowHistory()
        {
            pageTitle.Text = "Histórico local";
            pageDescription.Text = "Recibos ficam apenas neste computador até que você os exporte manualmente.";
            cleanButton.Enabled = false;
            BuildSimpleColumns(new[]
            {
                new ColumnDefinition("Receipt", "Recibo", 280),
                new ColumnDefinition("Modified", "Modificado", 170),
                new ColumnDefinition("Integrity", "Integridade", 160),
                new ColumnDefinition("Path", "Caminho local", 390)
            });

            List<HistoryRow> rows = new List<HistoryRow>();
            foreach (string path in application.ListReceiptPaths())
            {
                FileInfo info = new FileInfo(path);
                ReceiptFileVerification verification = application.VerifyReceiptFile(path);
                rows.Add(new HistoryRow
                {
                    Receipt = info.Name,
                    Modified = info.LastWriteTime.ToString("yyyy-MM-dd HH:mm:ss"),
                    Integrity = verification.IsValid ? "Íntegro" : "Verificação falhou",
                    Path = path
                });
            }
            grid.DataSource = new BindingList<HistoryRow>(rows);
            viewReceiptButton.Enabled = rows.Count > 0;
            exportReceiptButton.Enabled = rows.Count > 0;
            summaryLabel.Text = rows.Count == 0 ? "Nenhum recibo local encontrado." : rows.Count + " recibo(s) local(is).";
            statusLabel.Text = "Histórico não envia dados para a internet.";
        }

        private void ShowSelectedReceipt()
        {
            HistoryRow row = grid.CurrentRow == null ? null : grid.CurrentRow.DataBoundItem as HistoryRow;
            if (row == null || !File.Exists(row.Path))
            {
                MessageBox.Show(this, "Selecione um recibo local existente para visualizar.", "CLNXR", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            try
            {
                ReceiptFileVerification verification = application.VerifyReceiptFile(row.Path);
                ReceiptDocument document = application.ReadReceiptDocument(row.Path);
                Form dialog = new Form();
                dialog.Text = "Recibo CLNXR — somente leitura";
                dialog.StartPosition = FormStartPosition.CenterParent;
                dialog.MinimumSize = new Size(760, 460);
                dialog.Size = new Size(980, 620);
                dialog.BackColor = Graphite;
                dialog.ForeColor = Color.White;
                dialog.Font = Font;

                Label heading = new Label();
                heading.Text = Path.GetFileName(row.Path) + "  |  " + (string.IsNullOrWhiteSpace(document.SchemaVersion) ? "esquema ausente" : document.SchemaVersion);
                heading.AutoSize = false;
                heading.Dock = DockStyle.Top;
                heading.Height = 32;
                heading.Padding = new Padding(14, 7, 14, 0);
                heading.Font = new Font("Segoe UI", 10f, FontStyle.Bold);
                heading.BackColor = RaisedPanel;

                Label integrity = new Label();
                integrity.Text = verification.Message;
                integrity.AutoSize = false;
                integrity.Dock = DockStyle.Top;
                integrity.Height = 36;
                integrity.Padding = new Padding(14, 8, 14, 0);
                integrity.ForeColor = verification.IsValid ? Success : Review;
                integrity.BackColor = PanelColor;

                DataGridView details = CreateGrid();
                details.Dock = DockStyle.Fill;
                details.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "Section", HeaderText = "Seção", Width = 135 });
                details.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "Field", HeaderText = "Campo", Width = 190 });
                details.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "Value", HeaderText = "Valor", AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill, MinimumWidth = 260 });
                details.DataSource = new BindingList<ReceiptDetailRow>(document.Details.Select(detail => new ReceiptDetailRow
                {
                    Section = detail.Section,
                    Field = detail.Field,
                    Value = detail.Value
                }).ToList());

                dialog.Controls.Add(details);
                dialog.Controls.Add(integrity);
                dialog.Controls.Add(heading);
                dialog.ShowDialog(this);
            }
            catch (Exception ex)
            {
                statusLabel.ForeColor = Color.FromArgb(235, 104, 104);
                statusLabel.Text = "Falha ao abrir recibo: " + ex.Message;
                MessageBox.Show(this, "Não foi possível abrir o recibo: " + ex.Message, "CLNXR", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void ExportSelectedReceipt()
        {
            HistoryRow row = grid.CurrentRow == null ? null : grid.CurrentRow.DataBoundItem as HistoryRow;
            if (row == null || !File.Exists(row.Path))
            {
                MessageBox.Show(this, "Selecione um recibo local existente para exportar.", "CLNXR", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            using (SaveFileDialog dialog = new SaveFileDialog())
            {
                dialog.Title = "Exportar recibo CLNXR";
                dialog.Filter = "Recibo JSON (*.json)|*.json|Todos os arquivos (*.*)|*.*";
                dialog.FileName = Path.GetFileName(row.Path);
                dialog.OverwritePrompt = true;
                if (dialog.ShowDialog(this) != DialogResult.OK) return;

                try
                {
                    File.Copy(row.Path, dialog.FileName, true);
                    statusLabel.ForeColor = Success;
                    statusLabel.Text = "Recibo exportado para " + dialog.FileName;
                }
                catch (Exception ex)
                {
                    statusLabel.ForeColor = Color.FromArgb(235, 104, 104);
                    statusLabel.Text = "Falha ao exportar recibo: " + ex.Message;
                    MessageBox.Show(this, "Não foi possível exportar o recibo: " + ex.Message, "CLNXR", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        private void ShowRules()
        {
            pageTitle.Text = "Catálogo de regras";
            pageDescription.Text = "Regras definem o escopo; a interface não aceita exclusão livre por caminho.";
            cleanButton.Enabled = false;
            BuildSimpleColumns(new[]
            {
                new ColumnDefinition("RuleId", "ID", 210),
                new ColumnDefinition("Version", "Versão", 70),
                new ColumnDefinition("Risk", "Risco", 90),
                new ColumnDefinition("Category", "Categoria", 210),
                new ColumnDefinition("MinimumAge", "Idade mínima", 105),
                new ColumnDefinition("RequiredProcesses", "Processos fechados", 190),
                new ColumnDefinition("Explanation", "Explicação", 500)
            });
            IList<Rule> catalogRules;
            try
            {
                catalogRules = GetSelectableRules();
            }
            catch (Exception ex)
            {
                catalogRules = new List<Rule>();
                statusLabel.ForeColor = Color.FromArgb(235, 104, 104);
                statusLabel.Text = "Falha ao ler regras personalizadas: " + ex.Message;
            }
            List<RuleRow> rows = catalogRules.Select(rule => new RuleRow
            {
                RuleId = rule.RuleId,
                Version = rule.Version,
                Risk = RiskText(rule.Risk),
                Category = rule.Category,
                MinimumAge = rule.MinimumAgeDays <= 0 ? "Sem filtro" : rule.MinimumAgeDays + " dia(s)",
                RequiredProcesses = rule.RequiredClosedProcesses.Count == 0 ? "Nenhum" : string.Join(", ", rule.RequiredClosedProcesses.ToArray()),
                Explanation = rule.Explanation
            }).ToList();
            grid.DataSource = new BindingList<RuleRow>(rows);
            deleteCustomRuleButton.Enabled = rows.Count > 0 && IsCustomRuleSelected();
            summaryLabel.Text = rows.Count + " regra(s) catalogada(s). Regras REVIEW e ADVANCED não são selecionadas automaticamente.";
            if (statusLabel.ForeColor != Color.FromArgb(235, 104, 104))
                statusLabel.Text = "Regras personalizadas ficam locais, sem assinatura, e sempre exigem prévia no perfil Personalizado.";
        }

        private void ShowTools()
        {
            pageTitle.Text = "Ferramentas";
            pageDescription.Text = "Ferramentas P1 são separadas do limpador principal para não ocultar risco ou ampliar escopo sem revisão.";
            RenderTools();
            cleanButton.Enabled = false;
        }

        private void RenderTools()
        {
            BuildSimpleColumns(new[]
            {
                new ColumnDefinition("Name", "Ferramenta", 260),
                new ColumnDefinition("Status", "Estado", 180),
                new ColumnDefinition("Reason", "Critério de segurança", 600)
            });
            List<ToolRow> rows = new List<ToolRow>
            {
                new ToolRow
                {
                    Name = "Lixeira",
                    Status = recycleBin == null ? "Aguardando análise" : recycleBin.Available ? string.Format("{0:N0} item(ns) em {1}", recycleBin.ItemCount, SizeText(recycleBin.Bytes)) : "Consulta indisponível",
                    Reason = recycleBin == null ? "Use Analisar Lixeira; a ação de esvaziar exige confirmação separada." : recycleBin.Message
                },
                new ToolRow { Name = "Limpeza oficial do Windows", Status = "Abre Storage Sense sob confirmação", Reason = "O CLNXR abre apenas a página oficial; as opções e a execução permanecem sob seu controle no Windows." },
                new ToolRow { Name = "Caches de jogos e desenvolvimento", Status = "Perfis REVIEW disponíveis", Reason = "Unreal, NuGet, npm e pip são regras separadas, desmarcadas por padrão e com processos relacionados verificados." },
                new ToolRow { Name = "Mapa de disco", Status = diskMapResult == null ? "Ainda não analisado" : diskMapResult.WasCancelled ? "Análise cancelada" : diskMapResult.DiskEntries.Count + " entrada(s) locais", Reason = "Somente leitura; junctions e caminhos indisponíveis são ignorados e relatados." },
                new ToolRow { Name = "Arquivos grandes", Status = largeFilesResult == null ? "Ainda não analisado" : largeFilesResult.WasCancelled ? "Análise cancelada" : largeFilesResult.LargeFiles.Count + " resultado(s)", Reason = "Somente leitura; mostra até 100 arquivos a partir de 512 MB e não pré-seleciona nada." },
                new ToolRow { Name = "Duplicados", Status = duplicatesResult == null ? "Ainda não analisado" : duplicatesResult.WasCancelled ? "Análise cancelada" : duplicatesResult.DuplicateGroups.Count + " grupo(s)", Reason = "Somente leitura; compara SHA-256 apenas após você acionar a ferramenta e não remove cópias." }
            };
            grid.DataSource = new BindingList<ToolRow>(rows);
            recycleEmptyButton.Enabled = recycleBin != null && recycleBin.Available && recycleBin.ItemCount > 0 && activeOperation == null;
            summaryLabel.Text = recycleBin == null
                ? "A Lixeira não foi consultada nesta sessão."
                : recycleBin.Available
                    ? string.Format("Lixeira: {0:N0} item(ns) em {1}. Nada será apagado sem confirmação.", recycleBin.ItemCount, SizeText(recycleBin.Bytes))
                    : "A API da Lixeira não está disponível nesta sessão.";
            statusLabel.Text = "A Lixeira é uma ferramenta separada; dados protegidos não entram no catálogo de cache.";
        }

        private void ShowSettings()
        {
            pageTitle.Text = "Configurações";
            pageDescription.Text = "Preferências de produto não podem reduzir as proteções permanentes de dados pessoais e do sistema.";
            BuildSimpleColumns(new[]
            {
                new ColumnDefinition("Setting", "Configuração", 280),
                new ColumnDefinition("Value", "Estado", 260),
                new ColumnDefinition("Detail", "Detalhe", 500)
            });
            grid.DataSource = new BindingList<SettingRow>(new List<SettingRow>
            {
                new SettingRow { Setting = "Telemetria", Value = "Desligada", Detail = "Nenhuma telemetria foi implementada." },
                new SettingRow { Setting = "Recibos", Value = "Locais", Detail = "LocalAppData\\CLNXR\\Receipts" },
                new SettingRow { Setting = "Movimento reduzido", Value = "Padrão estático", Detail = "Esta interface não usa animações que atrasem ações." },
                new SettingRow { Setting = "Privilégios", Value = "Sem elevação automática", Detail = "Itens sem acesso são pulados e registrados." }
            });
            cleanButton.Enabled = false;
            summaryLabel.Text = "As proteções de caminho não podem ser desativadas pela interface.";
            statusLabel.Text = "Configurações avançadas de regras ainda não são expostas.";
        }

        private async Task QueryRecycleBinAsync()
        {
            if (activeOperation != null) return;
            recycleQueryButton.Enabled = false;
            SetBusy(true, "Consultando a Lixeira em todas as unidades; nenhuma exclusão será feita...", false);
            string finalStatus = "Consulta da Lixeira não concluída.";
            try
            {
                recycleBin = await Task.Run(delegate { return application.QueryRecycleBin(); });
                finalStatus = recycleBin.Message;
            }
            catch (Exception ex)
            {
                recycleBin = new RecycleBinSnapshot(false, 0, 0, "Falha ao consultar a Lixeira: " + ex.Message);
                finalStatus = recycleBin.Message;
            }
            finally
            {
                SetBusy(false, null);
                RenderTools();
                statusLabel.ForeColor = recycleBin != null && recycleBin.Available ? Cyan : Color.FromArgb(235, 104, 104);
                statusLabel.Text = finalStatus;
                recycleQueryButton.Enabled = true;
            }
        }

        private async Task EmptyRecycleBinAsync()
        {
            if (activeOperation != null) return;
            if (recycleBin == null || !recycleBin.Available)
            {
                MessageBox.Show(this, "Analise a Lixeira antes de solicitar o esvaziamento.", "CLNXR", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            if (recycleBin.ItemCount == 0)
            {
                statusLabel.Text = "A Lixeira já está vazia segundo a última consulta.";
                return;
            }

            string confirmation = string.Format(
                "Esvaziar a Lixeira de todas as unidades?{0}{0}A API oficial do Windows removerá {1:N0} item(ns), estimados em {2}.{0}Essa ação não faz parte da limpeza de cache e não poderá ser desfeita.",
                Environment.NewLine, recycleBin.ItemCount, SizeText(recycleBin.Bytes));
            if (MessageBox.Show(this, confirmation, "Confirmar esvaziamento da Lixeira", MessageBoxButtons.YesNo, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2) != DialogResult.Yes)
            {
                statusLabel.Text = "Esvaziamento da Lixeira cancelado antes de iniciar.";
                return;
            }

            recycleEmptyButton.Enabled = false;
            SetBusy(true, "Esvaziando a Lixeira pela API oficial do Windows...");
            try
            {
                ToolExecution result = await Task.Run(delegate { return application.EmptyRecycleBin(recycleBin); });
                recycleBin = application.QueryRecycleBin();
                RenderTools();
                statusLabel.ForeColor = result.Succeeded ? Success : Color.FromArgb(235, 104, 104);
                statusLabel.Text = result.Message + " Recibo local: " + result.ReceiptPath;
            }
            catch (Exception ex)
            {
                RenderTools();
                statusLabel.ForeColor = Color.FromArgb(235, 104, 104);
                statusLabel.Text = "Falha ao esvaziar a Lixeira: " + ex.Message;
            }
            finally
            {
                SetBusy(false, null);
                recycleEmptyButton.Enabled = recycleBin != null && recycleBin.Available && recycleBin.ItemCount > 0;
            }
        }

        private void OpenStorageSense()
        {
            const string message = "Abrir as Configuracoes oficiais do Windows em Storage Sense?\r\n\r\nO CLNXR nao executara limpeza nesta etapa. Qualquer regra de limpeza ou retencao sera escolhida e confirmada por voce no Windows.";
            if (MessageBox.Show(this, message, "Abrir limpeza oficial do Windows", MessageBoxButtons.YesNo, MessageBoxIcon.Information, MessageBoxDefaultButton.Button2) != DialogResult.Yes)
            {
                statusLabel.Text = "Abertura do Storage Sense cancelada.";
                return;
            }

            ToolExecution result = application.OpenStorageSense();

            statusLabel.ForeColor = result.Succeeded ? Success : Color.FromArgb(235, 104, 104);
            statusLabel.Text = result.Message + " Recibo local: " + result.ReceiptPath;
        }

        private async Task AnalyzeDiskMapAsync()
        {
            if (activeOperation != null) return;
            activeOperation = new CancellationTokenSource();
            SetBusy(true, "Mapeando unidades acessíveis em modo somente leitura...");
            try
            {
                StorageAnalysisResult result = await Task.Run(delegate
                {
                    return application.AnalyzeDiskMap(activeOperation.Token, ReportStorageProgress);
                });
                diskMapResult = result;
                ShowDiskMapResult(result);
            }
            catch (Exception ex)
            {
                ShowStorageError("Falha ao mapear discos", ex);
            }
            finally
            {
                activeOperation.Dispose();
                activeOperation = null;
                SetBusy(false, null);
            }
        }

        private async Task FindLargeFilesAsync()
        {
            if (activeOperation != null) return;
            activeOperation = new CancellationTokenSource();
            SetBusy(true, "Procurando arquivos a partir de 512 MB em modo somente leitura...");
            try
            {
                StorageAnalysisResult result = await Task.Run(delegate
                {
                    return application.FindLargeFiles(536870912L, 100, activeOperation.Token, ReportStorageProgress);
                });
                largeFilesResult = result;
                ShowLargeFilesResult(result);
            }
            catch (Exception ex)
            {
                ShowStorageError("Falha ao procurar arquivos grandes", ex);
            }
            finally
            {
                activeOperation.Dispose();
                activeOperation = null;
                SetBusy(false, null);
            }
        }

        private async Task FindDuplicatesAsync()
        {
            if (activeOperation != null) return;
            const string confirmation = "Comparar possíveis duplicados em todas as unidades acessíveis?\r\n\r\nA ferramenta abrirá arquivos apenas para leitura e calculará hashes SHA-256 de candidatos a partir de 64 MB. Ela não removerá ou selecionará cópia alguma.";
            if (MessageBox.Show(this, confirmation, "Analisar duplicados", MessageBoxButtons.YesNo, MessageBoxIcon.Information, MessageBoxDefaultButton.Button2) != DialogResult.Yes)
            {
                statusLabel.Text = "Análise de duplicados cancelada antes de iniciar.";
                return;
            }

            activeOperation = new CancellationTokenSource();
            SetBusy(true, "Comparando candidatos duplicados em modo somente leitura...");
            try
            {
                StorageAnalysisResult result = await Task.Run(delegate
                {
                    return application.FindDuplicates(67108864L, 10000, activeOperation.Token, ReportStorageProgress);
                });
                duplicatesResult = result;
                ShowDuplicatesResult(result);
            }
            catch (Exception ex)
            {
                ShowStorageError("Falha ao comparar duplicados", ex);
            }
            finally
            {
                activeOperation.Dispose();
                activeOperation = null;
                SetBusy(false, null);
            }
        }

        private void ShowDiskMapResult(StorageAnalysisResult result)
        {
            pageTitle.Text = "Mapa de disco";
            pageDescription.Text = "Inventário local em modo somente leitura. Nada desta tela é selecionável para exclusão.";
            BuildSimpleColumns(new[]
            {
                new ColumnDefinition("Volume", "Unidade", 80),
                new ColumnDefinition("Path", "Caminho", 560),
                new ColumnDefinition("Files", "Arquivos", 100),
                new ColumnDefinition("Size", "Tamanho", 140)
            });
            grid.DataSource = new BindingList<DiskMapRow>(result.DiskEntries.Select(entry => new DiskMapRow
            {
                Volume = entry.Volume,
                Path = entry.Path,
                Files = entry.FileCount,
                Size = SizeText(entry.Bytes)
            }).ToList());
            summaryLabel.Text = result.WasCancelled
                ? string.Format("Mapa cancelado após {0:N0} arquivo(s) medido(s).", result.FilesVisited)
                : string.Format("Mapa local: {0:N0} entrada(s), {1:N0} arquivo(s) medido(s).", result.DiskEntries.Count, result.FilesVisited);
            statusLabel.ForeColor = result.WasCancelled ? Review : Cyan;
            statusLabel.Text = FormatStorageIssues(result);
        }

        private void ShowLargeFilesResult(StorageAnalysisResult result)
        {
            pageTitle.Text = "Arquivos grandes";
            pageDescription.Text = "Resultados locais a partir de 512 MB. Esta ferramenta não altera, seleciona ou move arquivos.";
            BuildSimpleColumns(new[]
            {
                new ColumnDefinition("Volume", "Unidade", 80),
                new ColumnDefinition("Name", "Arquivo", 200),
                new ColumnDefinition("Size", "Tamanho", 120),
                new ColumnDefinition("Modified", "Modificado", 155),
                new ColumnDefinition("Path", "Caminho", 450)
            });
            grid.DataSource = new BindingList<LargeFileRow>(result.LargeFiles.Select(file => new LargeFileRow
            {
                Volume = file.Volume,
                Name = Path.GetFileName(file.Path),
                Size = SizeText(file.Bytes),
                Modified = file.ModifiedUtc.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss"),
                Path = file.Path
            }).ToList());
            summaryLabel.Text = result.WasCancelled
                ? string.Format("Análise cancelada após {0:N0} arquivo(s) visitado(s).", result.FilesVisited)
                : string.Format("{0:N0} arquivo(s) grande(s) mostrado(s), sem nenhuma alteração.", result.LargeFiles.Count);
            statusLabel.ForeColor = result.WasCancelled ? Review : Cyan;
            statusLabel.Text = FormatStorageIssues(result);
        }

        private void ShowDuplicatesResult(StorageAnalysisResult result)
        {
            pageTitle.Text = "Possíveis duplicados";
            pageDescription.Text = "Grupos com hash SHA-256 igual. Revise manualmente; o CLNXR não apaga cópias nesta ferramenta.";
            BuildSimpleColumns(new[]
            {
                new ColumnDefinition("Hash", "SHA-256", 150),
                new ColumnDefinition("Files", "Arquivos", 90),
                new ColumnDefinition("SizePerFile", "Por arquivo", 115),
                new ColumnDefinition("Potential", "Potencial", 115),
                new ColumnDefinition("FirstPath", "Primeiro caminho", 520)
            });
            grid.DataSource = new BindingList<DuplicateRow>(result.DuplicateGroups.Select(group => new DuplicateRow
            {
                Hash = group.Hash.Length > 16 ? group.Hash.Substring(0, 16) + "..." : group.Hash,
                Files = group.FileCount,
                SizePerFile = SizeText(group.BytesPerFile),
                Potential = SizeText(group.PotentialRecoverableBytes),
                FirstPath = group.Paths.Count == 0 ? string.Empty : group.Paths[0]
            }).ToList());
            summaryLabel.Text = result.WasCancelled
                ? string.Format("Comparação cancelada após {0:N0} arquivo(s) visitado(s).", result.FilesVisited)
                : string.Format("{0:N0} grupo(s) encontrado(s); {1} é apenas estimativa e nada foi removido.", result.DuplicateGroups.Count, SizeText(result.DuplicateGroups.Sum(group => group.PotentialRecoverableBytes)));
            statusLabel.ForeColor = result.WasCancelled ? Review : Cyan;
            statusLabel.Text = FormatStorageIssues(result);
        }

        private void ShowStorageError(string operation, Exception ex)
        {
            statusLabel.ForeColor = Color.FromArgb(235, 104, 104);
            summaryLabel.Text = operation + ". Nenhum arquivo foi alterado.";
            statusLabel.Text = ex.Message;
            MessageBox.Show(this, operation + ": " + ex.Message, "CLNXR", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }

        private static string FormatStorageIssues(StorageAnalysisResult result)
        {
            if (result.WasCancelled) return "Operação cancelada. Nenhum arquivo foi alterado.";
            if (result.Issues.Count == 0) return "Análise concluída em modo somente leitura; nenhum arquivo foi alterado.";
            return string.Format("Análise concluída com {0:N0} aviso(s); caminhos indisponíveis e reparse points foram preservados.", result.Issues.Count);
        }

        private bool ConfigurePersonalizedRules()
        {
            Form dialog = new Form();
            dialog.Text = "Escolher regras personalizadas";
            dialog.StartPosition = FormStartPosition.CenterParent;
            dialog.MinimumSize = new Size(760, 520);
            dialog.Size = new Size(900, 620);
            dialog.BackColor = Graphite;
            dialog.ForeColor = Color.White;
            dialog.Font = Font;

            Label heading = new Label();
            heading.Text = "Selecione somente regras catalogadas. Regras BLOCKED nunca podem ser escolhidas.";
            heading.Dock = DockStyle.Top;
            heading.Height = 42;
            heading.Padding = new Padding(14, 12, 14, 0);
            heading.ForeColor = TextMuted;

            CheckedListBox choices = new CheckedListBox();
            choices.Dock = DockStyle.Fill;
            choices.BackColor = Graphite;
            choices.ForeColor = Color.White;
            choices.BorderStyle = BorderStyle.None;
            choices.CheckOnClick = true;

            IList<Rule> rules;
            try
            {
                rules = GetSelectableRules();
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "Não foi possível carregar o catálogo local de regras: " + ex.Message, "CLNXR", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return false;
            }
            foreach (Rule rule in rules)
            {
                string label = string.Format("{0} — {1} [{2}]", rule.RuleId, rule.Category, RiskText(rule.Risk));
                int index = choices.Items.Add(label);
                choices.SetItemChecked(index, personalizedRuleIds.Contains(rule.RuleId));
            }

            Button cancel = CreateActionButton("Cancelar", Review, new Point(0, 0));
            cancel.DialogResult = DialogResult.Cancel;
            Button save = CreateActionButton("Usar regras selecionadas", Success, new Point(0, 0));
            save.DialogResult = DialogResult.OK;
            FlowLayoutPanel buttons = new FlowLayoutPanel();
            buttons.Dock = DockStyle.Bottom;
            buttons.Height = 54;
            buttons.FlowDirection = FlowDirection.RightToLeft;
            buttons.Padding = new Padding(10, 8, 10, 8);
            buttons.Controls.Add(cancel);
            buttons.Controls.Add(save);

            dialog.Controls.Add(choices);
            dialog.Controls.Add(buttons);
            dialog.Controls.Add(heading);
            dialog.AcceptButton = save;
            dialog.CancelButton = cancel;

            if (dialog.ShowDialog(this) != DialogResult.OK) return false;

            HashSet<string> selected = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (int index = 0; index < choices.Items.Count; index++)
            {
                if (!choices.GetItemChecked(index)) continue;
                selected.Add(rules[index].RuleId);
            }

            if (selected.Count == 0)
            {
                MessageBox.Show(this, "Escolha ao menos uma regra para o perfil Personalizado.", "CLNXR", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return false;
            }

            personalizedRuleIds.Clear();
            foreach (string ruleId in selected) personalizedRuleIds.Add(ruleId);
            return true;
        }

        private IList<Rule> GetSelectableRules()
        {
            List<Rule> rules = application.ListRules()
                .Where(rule => rule.Risk != RiskLevel.Blocked)
                .ToList();
            foreach (CustomRuleDefinition definition in application.ListCustomRules())
                rules.Add(definition.ToRule());
            return rules
                .OrderBy(rule => rule.Category, StringComparer.OrdinalIgnoreCase)
                .ThenBy(rule => rule.RuleId, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        private bool IsCustomRuleSelected()
        {
            RuleRow row = grid == null || grid.CurrentRow == null ? null : grid.CurrentRow.DataBoundItem as RuleRow;
            return row != null && row.RuleId.StartsWith("custom-", StringComparison.OrdinalIgnoreCase);
        }

        private void DeleteSelectedCustomRule()
        {
            RuleRow row = grid.CurrentRow == null ? null : grid.CurrentRow.DataBoundItem as RuleRow;
            if (row == null || !row.RuleId.StartsWith("custom-", StringComparison.OrdinalIgnoreCase))
            {
                MessageBox.Show(this, "Selecione uma regra personalizada para excluir. Regras declarativas embutidas não podem ser removidas.", "CLNXR", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            if (MessageBox.Show(this, "Excluir somente a definição local " + row.RuleId + "? Nenhum arquivo será removido.",
                "Excluir regra personalizada", MessageBoxButtons.YesNo, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2) != DialogResult.Yes) return;
            try
            {
                if (application.DeleteCustomRule(row.RuleId))
                {
                    personalizedRuleIds.Remove(row.RuleId);
                    ShowRules();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "Não foi possível excluir a definição local: " + ex.Message, "CLNXR", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private IList<CustomRuleDefinition> GetSelectedCustomRules()
        {
            if (SelectedProfile != ScanProfile.Personalized) return new List<CustomRuleDefinition>();
            return application.ListCustomRules()
                .Where(rule => personalizedRuleIds.Contains(rule.RuleId))
                .ToList();
        }

        private Task AddCustomRuleAsync()
        {
            using (Form dialog = new Form())
            {
                dialog.Text = "Adicionar regra personalizada";
                dialog.StartPosition = FormStartPosition.CenterParent;
                dialog.MinimumSize = new Size(780, 520);
                dialog.Size = new Size(900, 600);
                dialog.BackColor = Graphite;
                dialog.ForeColor = Color.White;
                dialog.Font = Font;

                Label heading = new Label();
                heading.Text = "A regra será ADVANCED, local e sem assinatura. A prévia enumera os arquivos antes de salvar.";
                heading.Dock = DockStyle.Top;
                heading.Height = 44;
                heading.Padding = new Padding(14, 12, 14, 0);
                heading.ForeColor = TextMuted;

                Panel fields = new Panel();
                fields.Dock = DockStyle.Fill;
                fields.Padding = new Padding(14, 8, 14, 8);

                Label nameLabel = CreateFieldLabel("Nome da regra", 0, 8);
                TextBox nameBox = CreateFieldTextBox(150, 4, 670, "Nome descritivo da regra");
                nameBox.Text = "Cache personalizada";

                Label rootLabel = CreateFieldLabel("Pasta raiz", 0, 52);
                TextBox rootBox = CreateFieldTextBox(150, 48, 560, "Pasta absoluta a analisar");
                Button browse = CreateActionButton("Escolher...", Cyan, new Point(720, 46));
                browse.Click += delegate
                {
                    using (FolderBrowserDialog folder = new FolderBrowserDialog())
                    {
                        folder.Description = "Escolha uma pasta específica; a raiz do seu perfil pessoal é recusada.";
                        folder.ShowNewFolderButton = false;
                        if (folder.ShowDialog(dialog) == DialogResult.OK) rootBox.Text = folder.SelectedPath;
                    }
                };

                Label ageLabel = CreateFieldLabel("Idade mínima", 0, 96);
                NumericUpDown ageBox = new NumericUpDown();
                ageBox.Minimum = 0;
                ageBox.Maximum = 3650;
                ageBox.Value = 7;
                ageBox.Width = 120;
                ageBox.Location = new Point(150, 92);

                Label extensionLabel = CreateFieldLabel("Extensões", 0, 140);
                TextBox extensionBox = CreateFieldTextBox(150, 136, 670, "tmp;log (vazio = todos os arquivos)");
                extensionBox.Text = ".tmp";

                Label exclusionLabel = CreateFieldLabel("Exclusões", 0, 184);
                TextBox exclusionBox = CreateFieldTextBox(150, 180, 670, "Uma pasta relativa por linha; nunca use ..");
                exclusionBox.Multiline = true;
                exclusionBox.Height = 84;
                exclusionBox.ScrollBars = ScrollBars.Vertical;

                Label attributionLabel = CreateFieldLabel("Atribuição", 0, 280);
                TextBox attributionBox = CreateFieldTextBox(150, 276, 670, "Opcional; origem da regra");

                Label hint = new Label();
                hint.Text = "Dados pessoais, navegadores, cookies, logins, Downloads e reparse points continuam protegidos.";
                hint.AutoSize = false;
                hint.Size = new Size(670, 42);
                hint.Location = new Point(150, 320);
                hint.ForeColor = TextMuted;

                fields.Controls.Add(nameLabel);
                fields.Controls.Add(nameBox);
                fields.Controls.Add(rootLabel);
                fields.Controls.Add(rootBox);
                fields.Controls.Add(browse);
                fields.Controls.Add(ageLabel);
                fields.Controls.Add(ageBox);
                fields.Controls.Add(extensionLabel);
                fields.Controls.Add(extensionBox);
                fields.Controls.Add(exclusionLabel);
                fields.Controls.Add(exclusionBox);
                fields.Controls.Add(attributionLabel);
                fields.Controls.Add(attributionBox);
                fields.Controls.Add(hint);

                Label operationStatus = new Label();
                operationStatus.Dock = DockStyle.Bottom;
                operationStatus.Height = 32;
                operationStatus.Padding = new Padding(14, 8, 14, 0);
                operationStatus.ForeColor = Cyan;

                Button cancel = CreateActionButton("Cancelar", Review, new Point(0, 0));
                cancel.DialogResult = DialogResult.Cancel;
                Button previewButton = CreateActionButton("Pré-visualizar e salvar", Success, new Point(0, 0));
                FlowLayoutPanel buttons = new FlowLayoutPanel();
                buttons.Dock = DockStyle.Bottom;
                buttons.Height = 52;
                buttons.FlowDirection = FlowDirection.RightToLeft;
                buttons.Padding = new Padding(10, 8, 10, 8);
                buttons.Controls.Add(cancel);
                buttons.Controls.Add(previewButton);

                bool saved = false;
                previewButton.Click += async delegate
                {
                    if (string.IsNullOrWhiteSpace(rootBox.Text) || string.IsNullOrWhiteSpace(nameBox.Text))
                    {
                        MessageBox.Show(dialog, "Informe o nome e escolha uma pasta raiz antes da prévia.", "CLNXR", MessageBoxButtons.OK, MessageBoxIcon.Information);
                        return;
                    }

                    CustomRuleDraft draft = new CustomRuleDraft(
                        nameBox.Text.Trim(),
                        rootBox.Text.Trim(),
                        (int)ageBox.Value,
                        SplitRuleValues(extensionBox.Text, true),
                        SplitRuleValues(exclusionBox.Text, false),
                        attributionBox.Text.Trim());
                    previewButton.Enabled = false;
                    cancel.Enabled = false;
                    operationStatus.ForeColor = Cyan;
                    operationStatus.Text = "Enumerando somente para gerar a prévia...";
                    try
                    {
                        CustomRulePreview preview = await Task.Run(delegate
                        {
                            return application.PreviewCustomRule(draft, CancellationToken.None, delegate(string message)
                            {
                                if (dialog.IsDisposed || !dialog.IsHandleCreated) return;
                                dialog.BeginInvoke((Action)delegate { operationStatus.Text = message; });
                            });
                        });

                        string issueText = preview.Issues.Count == 0 ? "Nenhum aviso." : string.Join(Environment.NewLine, preview.Issues.ToArray());
                        string examples = preview.Examples.Count == 0 ? "(nenhum exemplo)" : string.Join(Environment.NewLine, preview.Examples.ToArray());
                        string summary = string.Format("Regra: {0}{1}Risco: ADVANCED | Assinatura: unsigned{1}Arquivos na prévia: {2}{1}Bytes estimados: {3}{1}Exemplos redigidos:{1}{4}{1}{1}Avisos:{1}{5}",
                            preview.Definition == null ? draft.Name : preview.Definition.RuleId,
                            Environment.NewLine,
                            preview.Finding == null ? 0 : preview.Finding.FileCount,
                            preview.Finding == null ? SizeText(0) : SizeText(preview.Finding.EstimatedBytes),
                            examples,
                            issueText);
                        MessageBox.Show(dialog, summary, "Prévia da regra personalizada", MessageBoxButtons.OK,
                            preview.CanSave ? MessageBoxIcon.Information : MessageBoxIcon.Warning);
                        if (!preview.CanSave)
                        {
                            operationStatus.ForeColor = Review;
                            operationStatus.Text = "A regra não pode ser salva enquanto houver avisos ou nenhum arquivo elegível.";
                            return;
                        }

                        if (MessageBox.Show(dialog, "Salvar esta regra localmente? Ela só será usada quando você escolher o perfil Personalizado.",
                            "Confirmar regra personalizada", MessageBoxButtons.YesNo, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2) != DialogResult.Yes)
                        {
                            operationStatus.Text = "Regra não salva.";
                            return;
                        }

                        CustomRuleDefinition definition = await Task.Run(delegate
                        {
                            return application.SaveCustomRule(draft, CancellationToken.None, null);
                        });
                        personalizedRuleIds.Add(definition.RuleId);
                        saved = true;
                        operationStatus.ForeColor = Success;
                        operationStatus.Text = "Regra salva: " + definition.RuleId;
                        dialog.DialogResult = DialogResult.OK;
                        dialog.Close();
                    }
                    catch (Exception ex)
                    {
                        operationStatus.ForeColor = Color.FromArgb(235, 104, 104);
                        operationStatus.Text = "Falha: " + ex.Message;
                        MessageBox.Show(dialog, "Não foi possível criar a regra: " + ex.Message, "CLNXR", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                    finally
                    {
                        previewButton.Enabled = true;
                        cancel.Enabled = true;
                    }
                };

                dialog.Controls.Add(fields);
                dialog.Controls.Add(operationStatus);
                dialog.Controls.Add(buttons);
                dialog.Controls.Add(heading);
                dialog.CancelButton = cancel;
                dialog.ShowDialog(this);
                if (saved) ShowRules();
            }
            return Task.FromResult<object>(null);
        }

        private Label CreateFieldLabel(string text, int x, int y)
        {
            Label label = new Label();
            label.Text = text;
            label.AutoSize = true;
            label.ForeColor = TextMuted;
            label.Location = new Point(x, y + 4);
            return label;
        }

        private TextBox CreateFieldTextBox(int x, int y, int width, string accessibleName)
        {
            TextBox box = new TextBox();
            box.Location = new Point(x, y);
            box.Width = width;
            box.AccessibleName = accessibleName;
            return box;
        }

        private static IEnumerable<string> SplitRuleValues(string text, bool commaSeparated)
        {
            if (string.IsNullOrWhiteSpace(text)) return new string[0];
            char[] separators = commaSeparated ? new[] { ',', ';', '\r', '\n' } : new[] { ';', '\r', '\n' };
            return text.Split(separators, StringSplitOptions.RemoveEmptyEntries).Select(value => value.Trim()).Where(value => value.Length > 0).ToArray();
        }

        private async Task AnalyzeAsync()
        {
            if (activeOperation != null) return;
            IEnumerable<string> selectedRuleIds = null;
            IList<CustomRuleDefinition> selectedCustomRules = new List<CustomRuleDefinition>();
            if (SelectedProfile == ScanProfile.Personalized)
            {
                if (personalizedRuleIds.Count == 0 && !ConfigurePersonalizedRules()) return;
                selectedRuleIds = personalizedRuleIds.ToArray();
                try
                {
                    selectedCustomRules = GetSelectedCustomRules();
                }
                catch (Exception ex)
                {
                    MessageBox.Show(this, "Não foi possível carregar as regras personalizadas salvas: " + ex.Message, "CLNXR", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }
            }
            Navigate(DesktopPage.Scan);
            activeOperation = new CancellationTokenSource();
            SetBusy(true, "Preparando análise somente leitura...");
            try
            {
                ScanSession session = await Task.Run(delegate
                {
                    return application.Analyze(SelectedProfile, selectedRuleIds, selectedCustomRules, activeOperation.Token, ReportScanProgress);
                });

                currentSession = session;
                Navigate(DesktopPage.Results);
                BindFindings();
                UpdateActionsForFindings();

                long bytes = session.Findings.Sum(finding => finding.EstimatedBytes);
                long files = session.Findings.Sum(finding => finding.FileCount);
                summaryLabel.Text = session.State == SessionState.Cancelled
                    ? "Análise cancelada. A lista pode estar incompleta e não será usada para limpeza."
                    : session.Findings.Count == 0
                        ? "Nenhum item elegível foi encontrado neste perfil."
                        : string.Format("Prévia: {0:N0} arquivo(s) em {1}. Itens REVIEW permanecem desmarcados.", files, SizeText(bytes));
                statusLabel.ForeColor = session.State == SessionState.Cancelled ? Review : Cyan;
                statusLabel.Text = session.Issues.Count == 0
                    ? "Análise concluída. Revise a lista antes de confirmar qualquer limpeza."
                    : string.Format("Análise concluída com {0} aviso(s); itens sem acesso ou bloqueados foram preservados.", session.Issues.Count);
            }
            catch (Exception ex)
            {
                currentSession = null;
                statusLabel.ForeColor = Color.FromArgb(235, 104, 104);
                summaryLabel.Text = "Falha na análise. Nenhum arquivo foi removido.";
                statusLabel.Text = ex.Message;
                MessageBox.Show(this, "A análise falhou: " + ex.Message, "CLNXR", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                activeOperation.Dispose();
                activeOperation = null;
                SetBusy(false, null);
            }
        }

        private async Task CleanAsync()
        {
            if (currentSession == null || currentSession.State != SessionState.ReviewReady || allFindingRows == null) return;
            List<FindingRow> selectedRows = allFindingRows.Where(row => row.Selected).ToList();
            if (selectedRows.Count == 0)
            {
                MessageBox.Show(this, "Selecione pelo menos um resultado SAFE ou REVIEW para criar um plano de limpeza.", "CLNXR", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            int reviewCount = selectedRows.Count(row => row.Finding.Rule.Risk == RiskLevel.Review);
            long estimatedBytes = selectedRows.Sum(row => row.Finding.EstimatedBytes);
            string confirmation = string.Format(
                "Confirmar plano com {0} resultado(s), estimados em {1}?{2}{2}{3} resultado(s) REVIEW foram selecionados manualmente.{2}Itens em uso, bloqueados ou fora da política serão pulados. O CLNXR não encerra processos.",
                selectedRows.Count, SizeText(estimatedBytes), Environment.NewLine, reviewCount);
            if (MessageBox.Show(this, confirmation, "Confirmar limpeza", MessageBoxButtons.YesNo, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2) != DialogResult.Yes)
            {
                statusLabel.Text = "Limpeza cancelada antes de iniciar. Nenhum arquivo foi removido.";
                return;
            }

            activeOperation = new CancellationTokenSource();
            SetBusy(true, "Criando plano imutável e revalidando alvos...");
            try
            {
                CleanupExecution execution = await Task.Run(delegate
                {
                    return application.Clean(currentSession, selectedRows.Select(row => row.Finding.FindingId), activeOperation.Token, ReportCleanupProgress);
                });
                CleanupReceipt receipt = execution.Receipt;
                string receiptPath = execution.ReceiptPath;

                statusLabel.ForeColor = receipt.WasCancelled ? Review : Success;
                statusLabel.Text = receipt.WasCancelled
                    ? string.Format("Limpeza cancelada. {0:N0} item(ns) foram preservados/pulados. O recibo parcial foi salvo em {1}", receipt.TotalItemsSkipped, receiptPath)
                    : string.Format("Limpeza concluída. {0:N0} item(ns) foram preservados/pulados. O recibo local verificável foi salvo em {1}", receipt.TotalItemsSkipped, receiptPath);

                SetBusy(true, "Verificando o resultado com nova análise...");
                IEnumerable<string> refreshedRuleIds = SelectedProfile == ScanProfile.Personalized
                    ? personalizedRuleIds.ToArray()
                    : null;
                IList<CustomRuleDefinition> refreshedCustomRules = SelectedProfile == ScanProfile.Personalized
                    ? GetSelectedCustomRules()
                    : new List<CustomRuleDefinition>();
                ScanSession refreshed = await Task.Run(delegate
                {
                    return application.Analyze(SelectedProfile, refreshedRuleIds, refreshedCustomRules, CancellationToken.None, ReportScanProgress);
                });
                currentSession = refreshed;
                Navigate(DesktopPage.Results);
                BindFindings();
                UpdateActionsForFindings();

                long remainingBytes = refreshed.Findings.Sum(finding => finding.EstimatedBytes);
                long remainingFiles = refreshed.Findings.Sum(finding => finding.FileCount);
                summaryLabel.Text = string.Format(
                    "Resultado verificado: {0:N0} arquivo(s) removido(s), {1} liberados e {2:N0} item(ns) preservados/pulados. Restam {3:N0} arquivo(s) elegíveis em {4}.",
                    receipt.TotalFilesRemoved, SizeText(receipt.TotalBytesRemoved), receipt.TotalItemsSkipped, remainingFiles, SizeText(remainingBytes));
            }
            catch (Exception ex)
            {
                statusLabel.ForeColor = Color.FromArgb(235, 104, 104);
                statusLabel.Text = "Falha no fluxo de limpeza: " + ex.Message;
                MessageBox.Show(this, "A limpeza falhou: " + ex.Message, "CLNXR", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                if (activeOperation != null)
                {
                    activeOperation.Dispose();
                    activeOperation = null;
                }
                SetBusy(false, null);
            }
        }

        private void BuildFindingsColumns()
        {
            grid.DataSource = null;
            grid.Columns.Clear();
            grid.Columns.Add(new DataGridViewCheckBoxColumn { DataPropertyName = "Selected", HeaderText = "Limpar", Width = 58 });
            grid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "Risk", HeaderText = "Risco", Width = 88 });
            grid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "Category", HeaderText = "Categoria", Width = 230 });
            grid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "Explanation", HeaderText = "Motivo", Width = 330 });
            grid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "RequiredProcesses", HeaderText = "Processos fechados", Width = 170 });
            grid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "Files", HeaderText = "Arquivos", Width = 82 });
            grid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "EstimatedSize", HeaderText = "Estimativa", Width = 100 });
            grid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "Volume", HeaderText = "Unidade", Width = 72 });
            grid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "RuleId", HeaderText = "Regra", Width = 170 });
            grid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "Path", HeaderText = "Caminho avaliado", AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill, MinimumWidth = 250 });
        }

        private void BuildSimpleColumns(IEnumerable<ColumnDefinition> definitions)
        {
            grid.DataSource = null;
            grid.Columns.Clear();
            foreach (ColumnDefinition definition in definitions)
            {
                DataGridViewTextBoxColumn column = new DataGridViewTextBoxColumn();
                column.DataPropertyName = definition.Property;
                column.HeaderText = definition.Header;
                column.Width = definition.Width;
                if (definition.Width >= 500) column.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
                grid.Columns.Add(column);
            }
        }

        private void BindFindings()
        {
            if (currentSession == null)
            {
                allFindingRows = new BindingList<FindingRow>();
                findingRows = new BindingList<FindingRow>();
                grid.DataSource = findingRows;
                summaryLabel.Text = "Nenhuma análise em andamento. Escolha um perfil e analise as unidades.";
                statusLabel.ForeColor = Cyan;
                statusLabel.Text = "Nenhum arquivo foi removido.";
                return;
            }

            allFindingRows = new BindingList<FindingRow>(currentSession.Findings.Select(finding => new FindingRow
            {
                Selected = finding.DefaultSelected,
                Risk = RiskText(finding.Rule.Risk),
                Category = finding.Rule.Category,
                Explanation = finding.Rule.Explanation,
                RequiredProcesses = finding.Rule.RequiredClosedProcesses.Count == 0 ? "Nenhum" : string.Join(", ", finding.Rule.RequiredClosedProcesses.ToArray()),
                Files = finding.FileCount,
                EstimatedSize = SizeText(finding.EstimatedBytes),
                Volume = finding.Volume,
                RuleId = finding.Rule.RuleId,
                Path = finding.TargetPath,
                Finding = finding
            }).ToList());
            ApplyFindingFilter();
        }

        private void ApplyFindingFilter()
        {
            if (allFindingRows == null || resultSearchBox == null || resultRiskBox == null || resultSelectedOnly == null) return;

            string query = (resultSearchBox.Text ?? string.Empty).Trim();
            string risk = resultRiskBox.SelectedItem == null ? "Todos os riscos" : resultRiskBox.SelectedItem.ToString();
            IEnumerable<FindingRow> filtered = allFindingRows.Where(row =>
                (string.Equals(risk, "Todos os riscos", StringComparison.OrdinalIgnoreCase) || string.Equals(row.Risk, risk, StringComparison.OrdinalIgnoreCase)) &&
                (!resultSelectedOnly.Checked || row.Selected) &&
                (query.Length == 0 || ContainsIgnoreCase(row.Category, query) || ContainsIgnoreCase(row.RuleId, query) || ContainsIgnoreCase(row.Path, query) || ContainsIgnoreCase(row.Explanation, query)));

            findingRows = new BindingList<FindingRow>(filtered.ToList());
            if (grid != null) grid.DataSource = findingRows;
            if (currentPage == DesktopPage.Results && statusLabel != null)
            {
                int selected = allFindingRows.Count(row => row.Selected);
                statusLabel.Text = string.Format("Exibindo {0:N0} de {1:N0} resultado(s); {2:N0} selecionado(s). Clique duas vezes para ver detalhes.", findingRows.Count, allFindingRows.Count, selected);
            }
        }

        private static bool ContainsIgnoreCase(string value, string query)
        {
            return !string.IsNullOrEmpty(value) && value.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private void ShowFindingDetails(FindingRow row)
        {
            if (row == null || row.Finding == null) return;
            string processes = row.Finding.Rule.RequiredClosedProcesses.Count == 0
                ? "Nenhum"
                : string.Join(", ", row.Finding.Rule.RequiredClosedProcesses.ToArray());
            string details = string.Format(
                "Regra: {0} (v{1}){8}Risco: {2}{8}Categoria: {3}{8}Arquivos: {4:N0}{8}Estimativa: {5}{8}Processos que precisam estar fechados: {6}{8}Caminho avaliado: {7}{8}{8}{9}",
                row.Finding.Rule.RuleId,
                row.Finding.Rule.Version,
                RiskText(row.Finding.Rule.Risk),
                row.Finding.Rule.Category,
                row.Finding.FileCount,
                SizeText(row.Finding.EstimatedBytes),
                processes,
                PathRedactor.Redact(row.Finding.TargetPath),
                Environment.NewLine,
                row.Finding.Rule.Explanation);
            MessageBox.Show(this, details, "Detalhes do resultado", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void UpdateActionsForFindings()
        {
            bool canClean = currentPage == DesktopPage.Results && currentSession != null && currentSession.State == SessionState.ReviewReady && currentSession.Findings.Count > 0 && activeOperation == null;
            cleanButton.Enabled = canClean;
        }

        private void SetBusy(bool busy, string message, bool canCancel = true)
        {
            analyzeButton.Enabled = !busy;
            profileBox.Enabled = !busy;
            cancelButton.Enabled = busy && canCancel;
            cleanButton.Enabled = !busy && currentPage == DesktopPage.Results && currentSession != null && currentSession.State == SessionState.ReviewReady && currentSession.Findings.Count > 0;
            recycleQueryButton.Enabled = !busy;
            recycleEmptyButton.Enabled = !busy && recycleBin != null && recycleBin.Available && recycleBin.ItemCount > 0;
            storageSenseButton.Enabled = !busy;
            diskMapButton.Enabled = !busy;
            largeFilesButton.Enabled = !busy;
            duplicatesButton.Enabled = !busy;
            customRuleButton.Enabled = !busy;
            deleteCustomRuleButton.Enabled = !busy && currentPage == DesktopPage.Rules && IsCustomRuleSelected();
            Cursor = busy ? Cursors.WaitCursor : Cursors.Default;
            if (!string.IsNullOrEmpty(message))
            {
                statusLabel.ForeColor = Cyan;
                statusLabel.Text = message;
            }
        }

        private void ReportScanProgress(string message)
        {
            ReportUi(message, Cyan);
        }

        private void ReportCleanupProgress(CleanupProgress progress)
        {
            ReportUi(string.Format("Limpando {0} de {1}: {2}", progress.CompletedFindings, progress.TotalFindings, progress.Category), Cyan);
        }

        private void ReportStorageProgress(StorageAnalysisProgress progress)
        {
            lock (storageProgressLock)
            {
                DateTime now = DateTime.UtcNow;
                if ((now - lastStorageProgressUtc).TotalMilliseconds < 100) return;
                lastStorageProgressUtc = now;
            }
            ReportUi(string.Format("{0}: {1:N0} arquivo(s) visitado(s) — {2}", progress.Stage, progress.FilesVisited, progress.CurrentPath), Cyan);
        }

        private void ReportUi(string message, Color color)
        {
            if (IsDisposed || !IsHandleCreated) return;
            BeginInvoke((Action)delegate
            {
                statusLabel.ForeColor = color;
                statusLabel.Text = message;
            });
        }

        private static string RiskText(RiskLevel risk)
        {
            if (risk == RiskLevel.Safe) return "SAFE";
            if (risk == RiskLevel.Review) return "REVIEW";
            if (risk == RiskLevel.Advanced) return "ADVANCED";
            return "BLOCKED";
        }

        private ScanProfile SelectedProfile
        {
            get
            {
                if (profileBox.SelectedIndex == 1) return ScanProfile.Complete;
                if (profileBox.SelectedIndex == 2) return ScanProfile.Gaming;
                if (profileBox.SelectedIndex == 3) return ScanProfile.Developer;
                if (profileBox.SelectedIndex == 4) return ScanProfile.Personalized;
                return ScanProfile.Safe;
            }
        }

        private static string SizeText(long bytes)
        {
            if (bytes >= 1073741824L) return string.Format("{0:N2} GB", bytes / 1073741824d);
            if (bytes >= 1048576L) return string.Format("{0:N2} MB", bytes / 1048576d);
            if (bytes >= 1024L) return string.Format("{0:N2} KB", bytes / 1024d);
            return bytes + " B";
        }

        private sealed class ColumnDefinition
        {
            public ColumnDefinition(string property, string header, int width)
            {
                Property = property;
                Header = header;
                Width = width;
            }

            public string Property { get; private set; }
            public string Header { get; private set; }
            public int Width { get; private set; }
        }

        private sealed class ToolRow
        {
            public string Name { get; set; }
            public string Status { get; set; }
            public string Reason { get; set; }
        }

        private sealed class SettingRow
        {
            public string Setting { get; set; }
            public string Value { get; set; }
            public string Detail { get; set; }
        }
    }
}
