using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows.Forms;

[assembly: AssemblyTitle("FileRenameTool")]
[assembly: AssemblyDescription("FileRenameTool - 轻量、可预览的 Windows 文件重命名工具")]
[assembly: AssemblyCompany("六朝声")]
[assembly: AssemblyProduct("FileRenameTool")]
[assembly: AssemblyCopyright("Copyright © 六朝声 2026")]
[assembly: AssemblyVersion("2.1.0.0")]
[assembly: AssemblyFileVersion("2.1.0.0")]
[assembly: AssemblyInformationalVersion("2.1")]

namespace FileRenameTool
{
    internal sealed class RenamePreview
    {
        public string SourcePath;
        public string TargetPath;
        public string Status;
        public bool CanRename;
    }

    internal sealed class MainForm : Form
    {
        private readonly ListView fileList;
        private readonly ComboBox versionType;
        private readonly ComboBox companyPrefix;
        private readonly CheckBox includeVersion;
        private readonly CheckBox onlyKeepFileName;
        private readonly Label dropHint;
        private readonly Label summary;
        private readonly Button renameButton;
        private readonly Button saveCopyButton;
        private readonly Button clearReadOnlyButton;
        private readonly TextBox manualBaseName;
        private readonly Button applyBaseNameButton;
        private readonly Button resetBaseNameButton;
        private readonly List<string> sourceFiles = new List<string>();
        private readonly List<string> prefixHistory = new List<string>();
        private readonly List<string> customTypeHistory = new List<string>();
        private readonly Dictionary<string, string> manualBaseNames =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        private readonly string settingsPath;
        private string prefixTextBeforeDropDown = String.Empty;
        private bool prefixSelectionByKeyboard;
        private string typeTextBeforeDropDown = String.Empty;
        private bool typeSelectionByKeyboard;
        private bool isInitializing = true;

        private static readonly string[] BuiltInVersionTypes =
        {
            "修订版",
            "清洁版",
            "审核版",
            "Reviewed Version",
            "Clean Version"
        };

        private static readonly Lazy<Regex> CopySuffixRegex = new Lazy<Regex>(delegate
        {
            return new Regex(@"\s*[\(（]\d+[\)）]\s*$", RegexOptions.CultureInvariant);
        });

        private static readonly Lazy<Regex> StandardNameRegex = new Lazy<Regex>(delegate
        {
            return new Regex(
                @"^(?<base>.+)-(?<date>\d{8})-v(?<major>\d+)\.(?<minor>\d+)-(?<suffix>.+)$",
                RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        });

        private static readonly Lazy<Regex> LegacyStandardNameRegex = new Lazy<Regex>(delegate
        {
            return new Regex(
                @"^(?<date>\d{8})-(?<base>.+)-v(?<major>\d+)\.(?<minor>\d+)-(?<suffix>.+)$",
                RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        });

        private static readonly Lazy<Regex> NoVersionNameRegex = new Lazy<Regex>(delegate
        {
            return new Regex(
                @"^(?<base>.+)-(?<date>\d{8})-(?<suffix>.+)$",
                RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        });

        private static readonly Lazy<Regex> LegacyNoVersionNameRegex = new Lazy<Regex>(delegate
        {
            return new Regex(
                @"^(?<date>\d{8})-(?<base>.+)-(?<suffix>.+)$",
                RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        });

        private static readonly Lazy<Regex> GenericCurrentNameRegex = new Lazy<Regex>(delegate
        {
            return new Regex(
                @"^(?<base>.+)-\d{8}-v\d+\.\d+-.+$",
                RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        });

        private static readonly Lazy<Regex> GenericLegacyNameRegex = new Lazy<Regex>(delegate
        {
            return new Regex(
                @"^\d{8}-(?<base>.+)-v\d+\.\d+-.+$",
                RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        });

        private static readonly Lazy<Regex> GenericVersionNameRegex = new Lazy<Regex>(delegate
        {
            return new Regex(
                @"^(?<base>.+?)(?:-\d{8})?-v(?<major>\d+)\.(?<minor>\d+|x)(?:-.+)?$",
                RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        });

        private static readonly Lazy<Regex> LeadingDateRegex = new Lazy<Regex>(delegate
        {
            return new Regex(@"^\d{8}-(?<base>.+)$", RegexOptions.CultureInvariant);
        });

        private static readonly Lazy<Regex> TrailingDateRegex = new Lazy<Regex>(delegate
        {
            return new Regex(@"^(?<base>.+)-\d{8}$", RegexOptions.CultureInvariant);
        });

        private static readonly char[] InvalidFileNameChars = Path.GetInvalidFileNameChars();
        private static readonly Color PaperColor = Color.FromArgb(247, 249, 252);
        private static readonly Color MistBlueColor = Color.FromArgb(237, 242, 247);
        private static readonly Color DeepBlueColor = Color.FromArgb(11, 58, 130);
        private static readonly Color InkBlueColor = Color.FromArgb(16, 42, 86);
        private static readonly Color GrayBlueColor = Color.FromArgb(82, 97, 115);
        private static readonly Color BorderColor = Color.FromArgb(203, 213, 225);
        private static readonly Color PaleBlueColor = Color.FromArgb(230, 239, 252);
        private static readonly Color ErrorColor = Color.FromArgb(196, 61, 75);

        public MainForm()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint |
                ControlStyles.OptimizedDoubleBuffer, true);
            SuspendLayout();
            Text = "FileRenameTool";
            StartPosition = FormStartPosition.CenterScreen;
            MinimumSize = new Size(1020, 640);
            Size = new Size(1100, 760);
            Font = new Font("Microsoft YaHei UI", 9.5F);
            BackColor = PaperColor;
            ForeColor = InkBlueColor;
            AllowDrop = true;
            try
            {
                Icon = Icon.ExtractAssociatedIcon(Assembly.GetExecutingAssembly().Location);
            }
            catch
            {
                // 图标读取失败不影响重命名功能。
            }
            settingsPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "FileRenameTool", "settings.txt");

            var topPanel = new Panel
            {
                Dock = DockStyle.Top,
                Height = 216,
                Padding = new Padding(24, 14, 24, 10),
                BackColor = MistBlueColor
            };
            topPanel.SuspendLayout();

            var companyLabel = new Label
            {
                AutoSize = true,
                Text = "公司简称：",
                Location = new Point(24, 21),
                Font = new Font("Microsoft YaHei UI", 9.5F, FontStyle.Bold),
                ForeColor = InkBlueColor
            };

            companyPrefix = new ComboBox
            {
                DropDownStyle = ComboBoxStyle.DropDown,
                DrawMode = DrawMode.OwnerDrawFixed,
                ItemHeight = 24,
                DropDownWidth = 230,
                Width = 185,
                Location = new Point(112, 15),
                MaxLength = 50
            };
            companyPrefix.TextChanged += delegate { RefreshPreview(); };
            companyPrefix.DrawItem += CompanyPrefix_DrawItem;
            companyPrefix.DropDown += delegate
            {
                prefixTextBeforeDropDown = companyPrefix.Text;
                prefixSelectionByKeyboard = false;
            };
            companyPrefix.KeyDown += delegate { prefixSelectionByKeyboard = true; };
            companyPrefix.KeyUp += delegate { prefixSelectionByKeyboard = false; };
            companyPrefix.SelectionChangeCommitted += CompanyPrefix_SelectionChangeCommitted;

            var rememberButton = CreateButton("记住简称", 320, 14, 92);
            rememberButton.Click += delegate { RememberCurrentPrefix(); };

            var memoryHint = new Label
            {
                AutoSize = true,
                ForeColor = GrayBlueColor,
                Text = "下拉项右侧 × 可删除",
                Location = new Point(424, 21)
            };

            var typeLabel = new Label
            {
                AutoSize = true,
                Text = "版本类型：",
                Location = new Point(24, 62),
                Font = new Font("Microsoft YaHei UI", 9.5F, FontStyle.Bold),
                ForeColor = InkBlueColor
            };

            versionType = new ComboBox
            {
                DropDownStyle = ComboBoxStyle.DropDown,
                DrawMode = DrawMode.OwnerDrawFixed,
                ItemHeight = 24,
                DropDownWidth = 230,
                Width = 185,
                Location = new Point(112, 56),
                MaxLength = 50
            };
            versionType.TextChanged += delegate { RefreshPreview(); };
            versionType.DrawItem += VersionType_DrawItem;
            versionType.DropDown += delegate
            {
                typeTextBeforeDropDown = versionType.Text;
                typeSelectionByKeyboard = false;
            };
            versionType.KeyDown += delegate { typeSelectionByKeyboard = true; };
            versionType.KeyUp += delegate { typeSelectionByKeyboard = false; };
            versionType.SelectionChangeCommitted += VersionType_SelectionChangeCommitted;

            var rememberTypeButton = CreateButton("记住类型", 320, 55, 92);
            rememberTypeButton.Click += delegate { RememberCurrentType(); };

            var typeHint = new Label
            {
                AutoSize = true,
                ForeColor = GrayBlueColor,
                Text = "可直接输入自定义类型，记忆项右侧 × 可删除",
                Location = new Point(424, 62)
            };

            var addButton = CreateButton("添加文件", 24, 96, 92);
            addButton.Click += AddButton_Click;
            StyleAccentButton(addButton);

            var removeButton = CreateButton("移除选中", 128, 96, 98);
            removeButton.Click += delegate { RemoveSelected(); };

            var clearButton = CreateButton("清空", 238, 96, 78);
            clearButton.Click += delegate
            {
                sourceFiles.Clear();
                manualBaseNames.Clear();
                RefreshPreview();
            };

            saveCopyButton = CreateButton("另存为新文件", 328, 96, 116);
            saveCopyButton.Enabled = false;
            saveCopyButton.Click += SaveCopyButton_Click;
            StyleAccentButton(saveCopyButton);

            clearReadOnlyButton = CreateButton("仅解除只读", 0, 17, 112);
            clearReadOnlyButton.Height = 38;
            clearReadOnlyButton.Enabled = false;
            clearReadOnlyButton.Click += ClearReadOnlyButton_Click;

            dropHint = new Label
            {
                AutoSize = true,
                ForeColor = GrayBlueColor,
                Text = "可将一个或多个文件直接拖入下方区域",
                Location = new Point(576, 103)
            };

            includeVersion = new CheckBox
            {
                AutoSize = true,
                Checked = true,
                Text = "包含版本号",
                Location = new Point(24, 140),
                ForeColor = InkBlueColor
            };
            includeVersion.CheckedChanged += delegate { RefreshPreview(); };

            onlyKeepFileName = new CheckBox
            {
                AutoSize = true,
                Text = "仅保留文件名（清除日期、版本、公司简称、类型和 (1)(2)）",
                Location = new Point(150, 140),
                ForeColor = InkBlueColor
            };
            onlyKeepFileName.CheckedChanged += delegate
            {
                companyPrefix.Enabled = !onlyKeepFileName.Checked;
                versionType.Enabled = !onlyKeepFileName.Checked;
                includeVersion.Enabled = !onlyKeepFileName.Checked;
                rememberButton.Enabled = !onlyKeepFileName.Checked;
                rememberTypeButton.Enabled = !onlyKeepFileName.Checked;
                RefreshPreview();
            };

            var manualBaseNameLabel = new Label
            {
                AutoSize = true,
                Text = "文件名部分：",
                Location = new Point(24, 180),
                Font = new Font("Microsoft YaHei UI", 9.5F, FontStyle.Bold),
                ForeColor = InkBlueColor
            };

            manualBaseName = new TextBox
            {
                Width = 380,
                Location = new Point(124, 174),
                Enabled = false,
                MaxLength = 180,
                BackColor = Color.White,
                ForeColor = InkBlueColor,
                BorderStyle = BorderStyle.FixedSingle
            };
            manualBaseName.KeyDown += delegate(object sender, KeyEventArgs e)
            {
                if (e.KeyCode == Keys.Enter && applyBaseNameButton.Enabled)
                {
                    ApplyManualBaseName();
                    e.SuppressKeyPress = true;
                }
            };

            applyBaseNameButton = CreateButton("应用到选中", 516, 173, 106);
            applyBaseNameButton.Enabled = false;
            applyBaseNameButton.Click += delegate { ApplyManualBaseName(); };
            StyleAccentButton(applyBaseNameButton);

            resetBaseNameButton = CreateButton("恢复自动", 634, 173, 92);
            resetBaseNameButton.Enabled = false;
            resetBaseNameButton.Click += delegate { ResetManualBaseName(); };

            var manualNameHint = new Label
            {
                AutoSize = true,
                ForeColor = GrayBlueColor,
                Text = "单选后可调整，Enter 应用",
                Location = new Point(738, 180)
            };

            topPanel.Controls.Add(companyLabel);
            topPanel.Controls.Add(companyPrefix);
            topPanel.Controls.Add(rememberButton);
            topPanel.Controls.Add(memoryHint);
            topPanel.Controls.Add(typeLabel);
            topPanel.Controls.Add(versionType);
            topPanel.Controls.Add(rememberTypeButton);
            topPanel.Controls.Add(typeHint);
            topPanel.Controls.Add(addButton);
            topPanel.Controls.Add(removeButton);
            topPanel.Controls.Add(clearButton);
            topPanel.Controls.Add(saveCopyButton);
            topPanel.Controls.Add(dropHint);
            topPanel.Controls.Add(includeVersion);
            topPanel.Controls.Add(onlyKeepFileName);
            topPanel.Controls.Add(manualBaseNameLabel);
            topPanel.Controls.Add(manualBaseName);
            topPanel.Controls.Add(applyBaseNameButton);
            topPanel.Controls.Add(resetBaseNameButton);
            topPanel.Controls.Add(manualNameHint);
            topPanel.Layout += delegate
            {
                // 使用控件实际宽度排布，避免高 DPI 或字体缩放后标签与输入框互相遮挡。
                companyPrefix.Left = companyLabel.Right + 8;
                rememberButton.Left = companyPrefix.Right + 12;
                memoryHint.Left = rememberButton.Right + 12;
                versionType.Left = typeLabel.Right + 8;
                rememberTypeButton.Left = versionType.Right + 12;
                typeHint.Left = rememberTypeButton.Right + 12;
                onlyKeepFileName.Left = includeVersion.Right + 18;
                manualBaseName.Left = manualBaseNameLabel.Right + 8;
                applyBaseNameButton.Left = manualBaseName.Right + 12;
                resetBaseNameButton.Left = applyBaseNameButton.Right + 12;
                manualNameHint.Left = resetBaseNameButton.Right + 12;
            };

            fileList = new ListView
            {
                Dock = DockStyle.Fill,
                View = View.Details,
                FullRowSelect = true,
                GridLines = false,
                HideSelection = false,
                AllowDrop = true,
                BackColor = Color.White,
                ForeColor = InkBlueColor,
                BorderStyle = BorderStyle.FixedSingle
            };
            fileList.Columns.Add("原文件名", 330);
            fileList.Columns.Add("新文件名（预览）", 470);
            fileList.Columns.Add("状态", 150);
            fileList.DragEnter += DragEnterFiles;
            fileList.DragDrop += DragDropFiles;
            fileList.SelectedIndexChanged += delegate { UpdateSelectionActions(); };
            fileList.Resize += delegate { ResizeFileListColumns(); };
            fileList.KeyDown += delegate(object sender, KeyEventArgs e)
            {
                if (e.KeyCode == Keys.Delete)
                {
                    RemoveSelected();
                    e.Handled = true;
                }
            };

            var bottomPanel = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 74,
                Padding = new Padding(16, 12, 16, 12),
                BackColor = Color.White
            };
            bottomPanel.SuspendLayout();

            summary = new Label
            {
                AutoSize = true,
                Text = "尚未添加文件，可拖入文件或点击“添加文件”",
                Location = new Point(17, 27),
                ForeColor = GrayBlueColor
            };

            renameButton = new Button
            {
                Text = "执行重命名",
                Width = 132,
                Height = 38,
                Anchor = AnchorStyles.Top | AnchorStyles.Right,
                Location = new Point(ClientSize.Width - 164, 17),
                Enabled = false,
                FlatStyle = FlatStyle.Flat,
                BackColor = PaleBlueColor,
                ForeColor = GrayBlueColor,
                UseVisualStyleBackColor = false,
                Cursor = Cursors.Hand,
                Font = new Font("Microsoft YaHei UI", 9.5F, FontStyle.Bold)
            };
            renameButton.FlatAppearance.BorderSize = 0;
            renameButton.FlatAppearance.MouseOverBackColor = InkBlueColor;
            renameButton.Click += RenameButton_Click;

            var aboutButton = CreateButton("关于", 0, 17, 76);
            aboutButton.Height = 38;
            aboutButton.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            aboutButton.Click += delegate { ShowAboutDialog(); };
            clearReadOnlyButton.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            bottomPanel.Resize += delegate
            {
                renameButton.Left = bottomPanel.ClientSize.Width - renameButton.Width - 16;
                clearReadOnlyButton.Left = renameButton.Left - clearReadOnlyButton.Width - 12;
                aboutButton.Left = clearReadOnlyButton.Left - aboutButton.Width - 12;
            };

            bottomPanel.Controls.Add(summary);
            bottomPanel.Controls.Add(aboutButton);
            bottomPanel.Controls.Add(clearReadOnlyButton);
            bottomPanel.Controls.Add(renameButton);

            Controls.Add(fileList);
            Controls.Add(bottomPanel);
            Controls.Add(topPanel);

            DragEnter += DragEnterFiles;
            DragDrop += DragDropFiles;
            FormClosing += delegate
            {
                SaveSettings(companyPrefix.Text.Trim());
            };

            LoadSettings();
            topPanel.ResumeLayout(false);
            topPanel.PerformLayout();
            bottomPanel.ResumeLayout(false);
            bottomPanel.PerformLayout();
            ResumeLayout(true);
            isInitializing = false;
            RefreshPreview();
            ResizeFileListColumns();
        }

        private static Button CreateButton(string text, int left, int top, int width)
        {
            var button = new Button
            {
                Text = text,
                Location = new Point(left, top),
                Size = new Size(width, 30),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.White,
                ForeColor = InkBlueColor,
                UseVisualStyleBackColor = false,
                Cursor = Cursors.Hand
            };
            button.FlatAppearance.BorderColor = BorderColor;
            button.FlatAppearance.BorderSize = 1;
            button.FlatAppearance.MouseOverBackColor = PaleBlueColor;
            return button;
        }

        private static void StyleAccentButton(Button button)
        {
            button.BackColor = PaleBlueColor;
            button.ForeColor = DeepBlueColor;
            button.FlatAppearance.BorderColor = DeepBlueColor;
        }

        private void ResizeFileListColumns()
        {
            if (isInitializing) return;
            if (fileList == null || fileList.Columns.Count < 3)
            {
                return;
            }

            var availableWidth = Math.Max(600, fileList.ClientSize.Width - 4);
            var originalWidth = (int)(availableWidth * 0.33);
            var previewWidth = (int)(availableWidth * 0.49);
            fileList.Columns[0].Width = originalWidth;
            fileList.Columns[1].Width = previewWidth;
            fileList.Columns[2].Width = Math.Max(120, availableWidth - originalWidth - previewWidth);
        }

        private void AddButton_Click(object sender, EventArgs e)
        {
            using (var dialog = new OpenFileDialog())
            {
                dialog.Multiselect = true;
                dialog.Title = "选择要重命名的文件";
                dialog.Filter = "所有文件 (*.*)|*.*";
                if (dialog.ShowDialog(this) == DialogResult.OK)
                {
                    AddFiles(dialog.FileNames);
                }
            }
        }

        private void DragEnterFiles(object sender, DragEventArgs e)
        {
            e.Effect = e.Data.GetDataPresent(DataFormats.FileDrop)
                ? DragDropEffects.Copy
                : DragDropEffects.None;
        }

        private void DragDropFiles(object sender, DragEventArgs e)
        {
            var paths = e.Data.GetData(DataFormats.FileDrop) as string[];
            if (paths != null)
            {
                AddFiles(paths);
            }
        }

        private void AddFiles(IEnumerable<string> paths)
        {
            var existing = new HashSet<string>(sourceFiles, StringComparer.OrdinalIgnoreCase);
            var addedPaths = new List<string>();
            var ignoredFolders = 0;

            foreach (var path in paths)
            {
                if (!File.Exists(path))
                {
                    if (Directory.Exists(path)) ignoredFolders++;
                    continue;
                }

                var fullPath = Path.GetFullPath(path);
                if (existing.Add(fullPath))
                {
                    sourceFiles.Add(fullPath);
                    addedPaths.Add(fullPath);
                }
            }

            RefreshPreview();
            if (addedPaths.Count == 1)
            {
                SelectSingleSourceFile(addedPaths[0]);
            }
            else if (addedPaths.Count > 1)
            {
                ClearFileSelection();
            }

            if (ignoredFolders > 0)
            {
                MessageBox.Show(this, "当前仅处理文件，已忽略拖入的文件夹。", "提示",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }

        private void RemoveSelected()
        {
            if (fileList.SelectedIndices.Count == 0) return;

            var indexes = new List<int>();
            foreach (int index in fileList.SelectedIndices) indexes.Add(index);
            indexes.Sort();
            indexes.Reverse();

            foreach (var index in indexes)
            {
                if (index >= 0 && index < sourceFiles.Count)
                {
                    manualBaseNames.Remove(sourceFiles[index]);
                    sourceFiles.RemoveAt(index);
                }
            }
            RefreshPreview();
        }

        private void SelectSingleSourceFile(string sourcePath)
        {
            foreach (ListViewItem item in fileList.Items)
            {
                var preview = item.Tag as RenamePreview;
                var isTarget = preview != null && String.Equals(
                    preview.SourcePath, sourcePath, StringComparison.OrdinalIgnoreCase);
                item.Selected = isTarget;
                item.Focused = isTarget;
                if (isTarget) item.EnsureVisible();
            }
            fileList.Focus();
            UpdateSelectionActions();
        }

        private void ClearFileSelection()
        {
            foreach (ListViewItem item in fileList.Items)
            {
                item.Selected = false;
                item.Focused = false;
            }
            UpdateSelectionActions();
        }

        private void ShowAboutDialog()
        {
            var version = FileVersionInfo.GetVersionInfo(
                Assembly.GetExecutingAssembly().Location).FileVersion;
            Version parsedVersion;
            var displayVersion = Version.TryParse(version, out parsedVersion)
                ? String.Format("v{0}.{1}", parsedVersion.Major, parsedVersion.Minor)
                : "v" + version;

            using (var dialog = new Form())
            {
                dialog.Text = "关于 FileRenameTool";
                dialog.StartPosition = FormStartPosition.CenterParent;
                dialog.FormBorderStyle = FormBorderStyle.FixedDialog;
                dialog.MaximizeBox = false;
                dialog.MinimizeBox = false;
                dialog.ShowInTaskbar = false;
                dialog.ClientSize = new Size(560, 262);
                dialog.BackColor = PaperColor;
                dialog.ForeColor = InkBlueColor;
                dialog.Font = new Font("Microsoft YaHei UI", 9.5F);
                dialog.Icon = Icon;

                var titleLabel = new Label
                {
                    AutoSize = true,
                    Location = new Point(28, 25),
                    Text = "FileRenameTool",
                    Font = new Font("Microsoft YaHei UI", 15F, FontStyle.Bold),
                    ForeColor = InkBlueColor
                };
                var descriptionLabel = new Label
                {
                    AutoSize = true,
                    Location = new Point(30, 59),
                    Text = "轻量、可预览的 Windows 文件整理工具",
                    ForeColor = GrayBlueColor
                };
                var versionLabel = new Label
                {
                    AutoSize = true,
                    Location = new Point(27, 105),
                    Text = "当前版本：" + displayVersion,
                    ForeColor = InkBlueColor
                };
                var authorLabel = new Label
                {
                    AutoSize = true,
                    Location = new Point(27, 136),
                    Text = "作者：六朝声",
                    ForeColor = InkBlueColor
                };
                var repositoryLabel = new Label
                {
                    AutoSize = true,
                    Location = new Point(27, 167),
                    Text = "仓库：",
                    ForeColor = InkBlueColor
                };
                var repositoryLink = new LinkLabel
                {
                    AutoSize = true,
                    Location = new Point(82, 167),
                    Text = "https://github.com/bluntvoice/FileRenameTool",
                    LinkColor = DeepBlueColor,
                    ActiveLinkColor = InkBlueColor
                };
                repositoryLink.LinkClicked += delegate
                {
                    try
                    {
                        Process.Start(new ProcessStartInfo(repositoryLink.Text)
                        {
                            UseShellExecute = true
                        });
                    }
                    catch
                    {
                        Clipboard.SetText(repositoryLink.Text);
                        MessageBox.Show(dialog, "仓库地址已复制到剪贴板。", "无法打开浏览器",
                            MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                };

                var closeButton = CreateButton("关闭", 458, 210, 76);
                closeButton.Height = 38;
                closeButton.DialogResult = DialogResult.OK;
                dialog.Controls.Add(titleLabel);
                dialog.Controls.Add(descriptionLabel);
                dialog.Controls.Add(versionLabel);
                dialog.Controls.Add(authorLabel);
                dialog.Controls.Add(repositoryLabel);
                dialog.Controls.Add(repositoryLink);
                dialog.Controls.Add(closeButton);
                dialog.AcceptButton = closeButton;
                dialog.CancelButton = closeButton;
                dialog.ShowDialog(this);
            }
        }

        private List<string> GetSelectedSourcePaths()
        {
            var selected = new List<string>();
            if (fileList == null) return selected;

            foreach (ListViewItem item in fileList.SelectedItems)
            {
                var preview = item.Tag as RenamePreview;
                if (preview != null && !String.IsNullOrEmpty(preview.SourcePath))
                    selected.Add(preview.SourcePath);
            }
            return selected;
        }

        private void UpdateSelectionActions()
        {
            if (fileList == null || manualBaseName == null) return;

            var selected = GetSelectedSourcePaths();
            var hasSelection = selected.Count > 0;
            saveCopyButton.Enabled = hasSelection;
            clearReadOnlyButton.Enabled = hasSelection;

            var canEditOne = selected.Count == 1;
            manualBaseName.Enabled = canEditOne;
            applyBaseNameButton.Enabled = canEditOne;
            resetBaseNameButton.Enabled = canEditOne && manualBaseNames.ContainsKey(selected[0]);

            if (!canEditOne)
            {
                manualBaseName.Text = String.Empty;
                return;
            }

            string value;
            if (!manualBaseNames.TryGetValue(selected[0], out value))
            {
                var stem = Path.GetFileNameWithoutExtension(selected[0]);
                value = ExtractBaseNameForCleanup(stem);
            }
            manualBaseName.Text = value;
            manualBaseName.SelectionStart = manualBaseName.Text.Length;
        }

        private void ApplyManualBaseName()
        {
            var selected = GetSelectedSourcePaths();
            if (selected.Count != 1) return;

            var value = manualBaseName.Text.Trim();
            string validationMessage;
            if (!TryValidateFileNamePart(value, out validationMessage))
            {
                MessageBox.Show(this, validationMessage, "文件名部分无效",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                manualBaseName.Focus();
                return;
            }

            manualBaseNames[selected[0]] = value;
            RefreshPreview();
        }

        private void ResetManualBaseName()
        {
            var selected = GetSelectedSourcePaths();
            if (selected.Count != 1) return;

            manualBaseNames.Remove(selected[0]);
            RefreshPreview();
        }

        private static bool TryValidateFileNamePart(string value, out string message)
        {
            if (String.IsNullOrWhiteSpace(value))
            {
                message = "文件名部分不能为空。";
                return false;
            }
            if (value.IndexOfAny(InvalidFileNameChars) >= 0)
            {
                message = "文件名部分不能包含以下字符：\\ / : * ? \" < > |";
                return false;
            }
            if (value == "." || value == ".." || value.EndsWith(".") || value.EndsWith(" "))
            {
                message = "文件名部分不能是 . 或 ..，也不能以句点或空格结尾。";
                return false;
            }

            message = String.Empty;
            return true;
        }

        private string GetManualBaseName(string sourcePath, string automaticBaseName)
        {
            string manualValue;
            return manualBaseNames.TryGetValue(sourcePath, out manualValue)
                ? manualValue.Trim()
                : automaticBaseName;
        }

        private List<RenamePreview> BuildPreviews()
        {
            if (onlyKeepFileName.Checked)
                return BuildCleanNamePreviews();

            var previews = new List<RenamePreview>();
            var reservedTargets = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var selectedType = versionType.Text.Trim();
            var selectedPrefix = companyPrefix.Text.Trim();
            var suffix = selectedPrefix + selectedType;
            var today = DateTime.Now.ToString("yyyyMMdd");

            var invalidStatus = String.Empty;
            if (selectedPrefix.IndexOfAny(InvalidFileNameChars) >= 0)
                invalidStatus = "简称含非法字符";
            else if (String.IsNullOrWhiteSpace(selectedType))
                invalidStatus = "版本类型为空";
            else if (selectedType.IndexOfAny(InvalidFileNameChars) >= 0)
                invalidStatus = "版本类型含非法字符";

            if (!String.IsNullOrEmpty(invalidStatus))
            {
                foreach (var sourcePath in sourceFiles)
                {
                    previews.Add(new RenamePreview
                    {
                        SourcePath = sourcePath,
                        TargetPath = sourcePath,
                        Status = invalidStatus,
                        CanRename = false
                    });
                }
                return previews;
            }

            foreach (var sourcePath in sourceFiles)
            {
                if (!File.Exists(sourcePath))
                {
                    previews.Add(new RenamePreview
                    {
                        SourcePath = sourcePath,
                        TargetPath = sourcePath,
                        Status = "原文件不存在",
                        CanRename = false
                    });
                    continue;
                }

                var directory = Path.GetDirectoryName(sourcePath);
                var extension = Path.GetExtension(sourcePath);
                var stem = Path.GetFileNameWithoutExtension(sourcePath);

                stem = RemoveCopySuffixes(stem);

                var baseName = stem;
                var major = 1;
                var minor = 0;
                int parsedMajor;
                int parsedMinor;
                string parsedBase;
                if (TryParseStandardName(stem, out parsedBase, out parsedMajor, out parsedMinor))
                {
                    baseName = parsedBase;
                    major = parsedMajor;
                    minor = parsedMinor;
                    if (includeVersion.Checked)
                        IncrementVersion(ref major, ref minor);
                }
                else if (GenericVersionNameRegex.Value.IsMatch(stem))
                {
                    var genericVersion = GenericVersionNameRegex.Value.Match(stem);
                    baseName = genericVersion.Groups["base"].Value.Trim();
                    var leadingDate = LeadingDateRegex.Value.Match(baseName);
                    if (leadingDate.Success)
                        baseName = leadingDate.Groups["base"].Value.Trim();
                    major = ParsePositiveInt(genericVersion.Groups["major"].Value, 1);
                    minor = ParsePositiveInt(genericVersion.Groups["minor"].Value, 0);
                    if (includeVersion.Checked)
                        IncrementVersion(ref major, ref minor);
                }
                else if (TryParseNoVersionName(stem, out parsedBase))
                {
                    baseName = parsedBase;
                }
                else
                {
                    var dated = Regex.Match(stem, @"^\d{8}-(?<base>.+)$");
                    if (dated.Success)
                        baseName = dated.Groups["base"].Value.Trim();
                }

                baseName = GetManualBaseName(sourcePath, baseName);

                if (String.IsNullOrWhiteSpace(baseName)) baseName = "未命名文件";

                string targetPath;
                while (true)
                {
                    var targetName = includeVersion.Checked
                        ? String.Format("{0}-{1}-v{2}.{3}-{4}{5}",
                            baseName, today, major, minor, suffix, extension)
                        : String.Format("{0}-{1}-{2}{3}",
                            baseName, today, suffix, extension);
                    targetPath = Path.Combine(directory, targetName);

                    var occupiedByOtherFile = File.Exists(targetPath) &&
                        !String.Equals(targetPath, sourcePath, StringComparison.OrdinalIgnoreCase);

                    if (!occupiedByOtherFile && !reservedTargets.Contains(targetPath)) break;

                    if (!includeVersion.Checked)
                    {
                        previews.Add(new RenamePreview
                        {
                            SourcePath = sourcePath,
                            TargetPath = targetPath,
                            Status = occupiedByOtherFile ? "目标文件已存在" : "目标名称重复",
                            CanRename = false
                        });
                        targetPath = null;
                        break;
                    }

                    IncrementVersion(ref major, ref minor);
                }

                if (targetPath == null) continue;
                reservedTargets.Add(targetPath);
                previews.Add(new RenamePreview
                {
                    SourcePath = sourcePath,
                    TargetPath = targetPath,
                    Status = "等待重命名",
                    CanRename = true
                });
            }

            return previews;
        }

        private List<RenamePreview> BuildCleanNamePreviews()
        {
            var previews = new List<RenamePreview>();
            var reservedTargets = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var sourcePath in sourceFiles)
            {
                if (!File.Exists(sourcePath))
                {
                    previews.Add(new RenamePreview
                    {
                        SourcePath = sourcePath,
                        TargetPath = sourcePath,
                        Status = "原文件不存在",
                        CanRename = false
                    });
                    continue;
                }

                var directory = Path.GetDirectoryName(sourcePath);
                var extension = Path.GetExtension(sourcePath);
                var stem = RemoveCopySuffixes(Path.GetFileNameWithoutExtension(sourcePath));
                var baseName = ExtractBaseNameForCleanup(stem);

                baseName = GetManualBaseName(sourcePath, baseName);

                baseName = RemoveCopySuffixes(baseName.Trim());
                if (String.IsNullOrWhiteSpace(baseName)) baseName = "未命名文件";

                var targetPath = Path.Combine(directory, baseName + extension);
                var isSamePath = String.Equals(sourcePath, targetPath,
                    StringComparison.OrdinalIgnoreCase);
                var occupied = !isSamePath && File.Exists(targetPath);
                var duplicated = !isSamePath && reservedTargets.Contains(targetPath);

                string status;
                bool canRename;
                if (isSamePath)
                {
                    status = "无需修改";
                    canRename = true;
                }
                else if (occupied)
                {
                    status = "目标文件已存在";
                    canRename = false;
                }
                else if (duplicated)
                {
                    status = "目标名称重复";
                    canRename = false;
                }
                else
                {
                    status = "等待清理文件名";
                    canRename = true;
                }

                if (!duplicated) reservedTargets.Add(targetPath);
                previews.Add(new RenamePreview
                {
                    SourcePath = sourcePath,
                    TargetPath = targetPath,
                    Status = status,
                    CanRename = canRename
                });
            }

            return previews;
        }

        private static string ExtractBaseNameForCleanup(string stem)
        {
            var cleanedStem = RemoveCopySuffixes(stem);
            var baseName = cleanedStem;

            int parsedMajor;
            int parsedMinor;
            string parsedBase;
            if (TryParseStandardName(cleanedStem, out parsedBase, out parsedMajor, out parsedMinor))
            {
                baseName = parsedBase;
            }
            else
            {
                var genericCurrent = GenericCurrentNameRegex.Value.Match(cleanedStem);
                var genericLegacy = GenericLegacyNameRegex.Value.Match(cleanedStem);
                var genericVersion = GenericVersionNameRegex.Value.Match(cleanedStem);

                if (genericCurrent.Success)
                    baseName = genericCurrent.Groups["base"].Value;
                else if (genericLegacy.Success)
                    baseName = genericLegacy.Groups["base"].Value;
                else if (genericVersion.Success)
                    baseName = genericVersion.Groups["base"].Value;
                else if (TryParseNoVersionName(cleanedStem, out parsedBase))
                    baseName = parsedBase;
                else
                {
                    var trailingDate = TrailingDateRegex.Value.Match(cleanedStem);
                    if (trailingDate.Success)
                        baseName = trailingDate.Groups["base"].Value;
                }
            }

            // 兼容“日期-文件名-v1.X”这类旧式或不完整名称。
            var leadingDate = LeadingDateRegex.Value.Match(baseName.Trim());
            if (leadingDate.Success)
                baseName = leadingDate.Groups["base"].Value;

            return RemoveCopySuffixes(baseName.Trim());
        }

        private static bool TryParseStandardName(string stem, out string baseName,
            out int major, out int minor)
        {
            var match = StandardNameRegex.Value.Match(stem);
            if (!match.Success) match = LegacyStandardNameRegex.Value.Match(stem);

            if (!match.Success)
            {
                baseName = stem;
                major = 1;
                minor = 0;
                return false;
            }

            baseName = match.Groups["base"].Value.Trim();
            major = ParsePositiveInt(match.Groups["major"].Value, 1);
            minor = ParsePositiveInt(match.Groups["minor"].Value, 0);
            return true;
        }

        private static bool TryParseNoVersionName(string stem, out string baseName)
        {
            var match = NoVersionNameRegex.Value.Match(stem);
            if (!match.Success) match = LegacyNoVersionNameRegex.Value.Match(stem);

            if (!match.Success)
            {
                baseName = stem;
                return false;
            }

            baseName = match.Groups["base"].Value.Trim();
            return true;
        }

        private static string RemoveCopySuffixes(string stem)
        {
            var result = stem.Trim();
            while (CopySuffixRegex.Value.IsMatch(result))
            {
                result = CopySuffixRegex.Value.Replace(result, String.Empty).Trim();
            }
            return result;
        }

        private static int ParsePositiveInt(string value, int fallback)
        {
            int parsed;
            return Int32.TryParse(value, out parsed) && parsed >= 0 ? parsed : fallback;
        }

        private static void IncrementVersion(ref int major, ref int minor)
        {
            minor++;
            if (minor >= 10)
            {
                major++;
                minor = 0;
            }
        }

        private void RefreshPreview()
        {
            if (isInitializing) return;
            if (fileList == null) return;

            var selectedPaths = new HashSet<string>(GetSelectedSourcePaths(),
                StringComparer.OrdinalIgnoreCase);

            fileList.BeginUpdate();
            fileList.Items.Clear();

            var rowIndex = 0;
            foreach (var preview in BuildPreviews())
            {
                var sourceName = Path.GetFileName(preview.SourcePath);
                if (IsReadOnlyFile(preview.SourcePath)) sourceName += "（只读）";
                var item = new ListViewItem(sourceName);
                item.SubItems.Add(Path.GetFileName(preview.TargetPath));
                item.SubItems.Add(preview.Status);
                item.Tag = preview;
                item.BackColor = rowIndex % 2 == 0 ? Color.White : PaperColor;
                item.ForeColor = preview.CanRename ? InkBlueColor : ErrorColor;
                fileList.Items.Add(item);
                item.Selected = selectedPaths.Contains(preview.SourcePath);
                rowIndex++;
            }

            fileList.EndUpdate();
            summary.Text = sourceFiles.Count == 0
                ? "尚未添加文件，可拖入文件或点击“添加文件”"
                : onlyKeepFileName.Checked
                    ? String.Format("共 {0} 个文件；将仅保留原始文件名和扩展名", sourceFiles.Count)
                    : includeVersion.Checked
                        ? String.Format("共 {0} 个文件；包含版本号，重名时自动递增版本", sourceFiles.Count)
                        : String.Format("共 {0} 个文件；不含版本号，将更新已有日期和类型", sourceFiles.Count);
            UpdateRenameButtonState();
            UpdateSelectionActions();
        }

        private void UpdateRenameButtonState()
        {
            var canRename = sourceFiles.Count > 0;
            renameButton.Enabled = canRename;
            renameButton.BackColor = canRename ? DeepBlueColor : PaleBlueColor;
            renameButton.ForeColor = canRename ? Color.White : GrayBlueColor;
        }

        private void RenameButton_Click(object sender, EventArgs e)
        {
            var selectedPrefix = companyPrefix.Text.Trim();
            if (!ValidateNamingInputs()) return;

            var previews = BuildPreviews();
            if (previews.Count == 0) return;

            var availableCount = 0;
            foreach (var preview in previews)
                if (preview.CanRename) availableCount++;

            if (availableCount == 0)
            {
                MessageBox.Show(this, "当前没有可以执行的文件，请检查列表中的状态提示。",
                    "无法执行", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            var confirm = MessageBox.Show(this,
                String.Format("即将处理 {0} 个文件，并默认解除源文件的只读属性。\r\n\r\n是否继续？", availableCount),
                "确认重命名", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
            if (confirm != DialogResult.Yes) return;

            SaveSettings(selectedPrefix);

            var success = 0;
            var failures = new List<string>();

            foreach (var preview in previews)
            {
                if (!preview.CanRename)
                {
                    failures.Add(Path.GetFileName(preview.SourcePath) + "：" + preview.Status);
                    continue;
                }

                try
                {
                    if (!File.Exists(preview.SourcePath))
                        throw new FileNotFoundException("原文件不存在");

                    RemoveReadOnlyAttribute(preview.SourcePath);

                    if (String.Equals(preview.SourcePath, preview.TargetPath,
                        StringComparison.OrdinalIgnoreCase))
                    {
                        success++;
                        continue;
                    }

                    File.Move(preview.SourcePath, preview.TargetPath);
                    success++;
                }
                catch (Exception ex)
                {
                    failures.Add(Path.GetFileName(preview.SourcePath) + "：" + ex.Message);
                }
            }

            sourceFiles.Clear();
            manualBaseNames.Clear();
            RefreshPreview();

            if (failures.Count == 0)
            {
                MessageBox.Show(this,
                    String.Format("完成，已处理 {0} 个文件并解除只读属性。", success),
                    "重命名完成", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            else
            {
                var detail = String.Join("\r\n", failures.ToArray());
                MessageBox.Show(this,
                    String.Format("成功 {0} 个，失败 {1} 个。\r\n\r\n{2}",
                        success, failures.Count, detail),
                    "部分文件未完成", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private bool ValidateNamingInputs()
        {
            if (onlyKeepFileName.Checked) return true;

            var selectedPrefix = companyPrefix.Text.Trim();
            if (selectedPrefix.IndexOfAny(InvalidFileNameChars) >= 0)
            {
                MessageBox.Show(this,
                    "公司简称不能包含以下字符：\\ / : * ? \" < > |",
                    "公司简称无效", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                companyPrefix.Focus();
                return false;
            }

            var selectedType = versionType.Text.Trim();
            if (String.IsNullOrWhiteSpace(selectedType))
            {
                MessageBox.Show(this, "版本类型不能为空。", "版本类型无效",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                versionType.Focus();
                return false;
            }
            if (selectedType.IndexOfAny(InvalidFileNameChars) >= 0)
            {
                MessageBox.Show(this,
                    "版本类型不能包含以下字符：\\ / : * ? \" < > |",
                    "版本类型无效", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                versionType.Focus();
                return false;
            }
            return true;
        }

        private void SaveCopyButton_Click(object sender, EventArgs e)
        {
            if (!ValidateNamingInputs()) return;

            var selectedPaths = new HashSet<string>(GetSelectedSourcePaths(),
                StringComparer.OrdinalIgnoreCase);
            if (selectedPaths.Count == 0) return;

            var selectedPreviews = new List<RenamePreview>();
            foreach (var preview in BuildPreviews())
            {
                if (selectedPaths.Contains(preview.SourcePath))
                    selectedPreviews.Add(preview);
            }

            var availableCount = 0;
            foreach (var preview in selectedPreviews)
            {
                if (preview.CanRename && !String.Equals(preview.SourcePath, preview.TargetPath,
                    StringComparison.OrdinalIgnoreCase))
                    availableCount++;
            }

            if (availableCount == 0)
            {
                MessageBox.Show(this,
                    "选中的文件没有可用的新文件名，请检查预览和状态提示。",
                    "无法另存", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            var confirm = MessageBox.Show(this,
                String.Format("将以预览名称另存 {0} 个新文件，原文件会保留。\r\n" +
                    "原文件和新文件的只读属性都会自动解除。\r\n\r\n是否继续？", availableCount),
                "确认另存为新文件", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
            if (confirm != DialogResult.Yes) return;

            SaveSettings(companyPrefix.Text.Trim());
            var success = 0;
            var failures = new List<string>();

            foreach (var preview in selectedPreviews)
            {
                if (!preview.CanRename)
                {
                    failures.Add(Path.GetFileName(preview.SourcePath) + "：" + preview.Status);
                    continue;
                }
                if (String.Equals(preview.SourcePath, preview.TargetPath,
                    StringComparison.OrdinalIgnoreCase))
                {
                    failures.Add(Path.GetFileName(preview.SourcePath) + "：新文件名与原文件名相同");
                    continue;
                }

                try
                {
                    RemoveReadOnlyAttribute(preview.SourcePath);
                    CopyAsWritableFile(preview.SourcePath, preview.TargetPath);
                    success++;
                }
                catch (Exception ex)
                {
                    failures.Add(Path.GetFileName(preview.SourcePath) + "：" + ex.Message);
                }
            }

            RefreshPreview();
            if (failures.Count == 0)
            {
                MessageBox.Show(this,
                    String.Format("完成，已另存 {0} 个新文件，并解除原文件和新文件的只读属性。", success),
                    "另存完成", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            else
            {
                MessageBox.Show(this,
                    String.Format("成功 {0} 个，失败 {1} 个。\r\n\r\n{2}",
                        success, failures.Count, String.Join("\r\n", failures.ToArray())),
                    "部分文件未另存", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private void ClearReadOnlyButton_Click(object sender, EventArgs e)
        {
            var selectedPaths = GetSelectedSourcePaths();
            if (selectedPaths.Count == 0) return;

            var changed = 0;
            var alreadyWritable = 0;
            var failures = new List<string>();
            foreach (var path in selectedPaths)
            {
                try
                {
                    if (!File.Exists(path)) throw new FileNotFoundException("文件不存在");
                    if (RemoveReadOnlyAttribute(path)) changed++;
                    else alreadyWritable++;
                }
                catch (Exception ex)
                {
                    failures.Add(Path.GetFileName(path) + "：" + ex.Message);
                }
            }

            var message = String.Format("已解除只读 {0} 个；原本可写 {1} 个。", changed, alreadyWritable);
            if (failures.Count > 0)
                message += "\r\n\r\n失败：\r\n" + String.Join("\r\n", failures.ToArray());
            RefreshPreview();
            MessageBox.Show(this, message, failures.Count == 0 ? "处理完成" : "部分文件未完成",
                MessageBoxButtons.OK,
                failures.Count == 0 ? MessageBoxIcon.Information : MessageBoxIcon.Warning);
        }

        private static void CopyAsWritableFile(string sourcePath, string targetPath)
        {
            if (!File.Exists(sourcePath)) throw new FileNotFoundException("原文件不存在");
            if (File.Exists(targetPath)) throw new IOException("目标文件已存在");
            File.Copy(sourcePath, targetPath, false);
            RemoveReadOnlyAttribute(targetPath);
        }

        private static bool RemoveReadOnlyAttribute(string path)
        {
            var attributes = File.GetAttributes(path);
            if ((attributes & FileAttributes.ReadOnly) == 0) return false;
            File.SetAttributes(path, attributes & ~FileAttributes.ReadOnly);
            return true;
        }

        private static bool IsReadOnlyFile(string path)
        {
            try
            {
                return File.Exists(path) &&
                    (File.GetAttributes(path) & FileAttributes.ReadOnly) != 0;
            }
            catch
            {
                return false;
            }
        }

        private void LoadSettings()
        {
            prefixHistory.Clear();
            customTypeHistory.Clear();
            var lastPrefix = String.Empty;
            var lastType = BuiltInVersionTypes[0];

            try
            {
                if (File.Exists(settingsPath))
                {
                    foreach (var line in File.ReadAllLines(settingsPath, Encoding.UTF8))
                    {
                        if (line.StartsWith("last="))
                        {
                            lastPrefix = DecodeSetting(line.Substring(5));
                        }
                        else if (line.StartsWith("item="))
                        {
                            var item = DecodeSetting(line.Substring(5)).Trim();
                            if (!String.IsNullOrEmpty(item) && !ContainsPrefix(item))
                                prefixHistory.Add(item);
                        }
                        else if (line.StartsWith("lastType="))
                        {
                            lastType = DecodeSetting(line.Substring(9)).Trim();
                        }
                        else if (line.StartsWith("typeItem="))
                        {
                            var item = DecodeSetting(line.Substring(9)).Trim();
                            if (!String.IsNullOrEmpty(item) &&
                                !IsBuiltInVersionType(item) && !ContainsCustomType(item))
                                customTypeHistory.Add(item);
                        }
                    }
                }
            }
            catch
            {
                lastPrefix = String.Empty;
                lastType = BuiltInVersionTypes[0];
                prefixHistory.Clear();
                customTypeHistory.Clear();
            }

            RefreshPrefixItems(lastPrefix);
            RefreshVersionTypeItems(lastType);
        }

        private void SaveSettings(string lastPrefix)
        {
            try
            {
                var directory = Path.GetDirectoryName(settingsPath);
                if (!Directory.Exists(directory)) Directory.CreateDirectory(directory);

                var lines = new List<string>();
                lines.Add("last=" + EncodeSetting(lastPrefix));
                foreach (var item in prefixHistory)
                    lines.Add("item=" + EncodeSetting(item));
                lines.Add("lastType=" + EncodeSetting(versionType.Text.Trim()));
                foreach (var item in customTypeHistory)
                    lines.Add("typeItem=" + EncodeSetting(item));

                File.WriteAllLines(settingsPath, lines.ToArray(), new UTF8Encoding(false));
            }
            catch
            {
                // 设置保存失败不影响文件重命名功能。
            }
        }

        private void RememberCurrentPrefix()
        {
            var value = companyPrefix.Text.Trim();
            if (String.IsNullOrEmpty(value))
            {
                MessageBox.Show(this, "公司简称为空，无需加入记忆。", "提示",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            if (value.IndexOfAny(InvalidFileNameChars) >= 0)
            {
                MessageBox.Show(this,
                    "公司简称不能包含以下字符：\\ / : * ? \" < > |",
                    "公司简称无效", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if (!ContainsPrefix(value)) prefixHistory.Add(value);
            RefreshPrefixItems(value);
            SaveSettings(value);
        }

        private void RememberCurrentType()
        {
            var value = versionType.Text.Trim();
            if (String.IsNullOrEmpty(value))
            {
                MessageBox.Show(this, "版本类型为空，无法加入记忆。", "提示",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            if (value.IndexOfAny(InvalidFileNameChars) >= 0)
            {
                MessageBox.Show(this,
                    "版本类型不能包含以下字符：\\ / : * ? \" < > |",
                    "版本类型无效", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if (!IsBuiltInVersionType(value) && !ContainsCustomType(value))
                customTypeHistory.Add(value);
            RefreshVersionTypeItems(value);
            SaveSettings(companyPrefix.Text.Trim());
        }

        private static bool IsBuiltInVersionType(string value)
        {
            foreach (var item in BuiltInVersionTypes)
            {
                if (String.Equals(item, value, StringComparison.OrdinalIgnoreCase))
                    return true;
            }
            return false;
        }

        private bool ContainsCustomType(string value)
        {
            return customTypeHistory.Exists(delegate(string item)
            {
                return String.Equals(item, value, StringComparison.OrdinalIgnoreCase);
            });
        }

        private void RefreshVersionTypeItems(string currentText)
        {
            versionType.BeginUpdate();
            versionType.Items.Clear();
            foreach (var item in BuiltInVersionTypes) versionType.Items.Add(item);
            foreach (var item in customTypeHistory) versionType.Items.Add(item);
            versionType.EndUpdate();

            versionType.Text = String.IsNullOrWhiteSpace(currentText)
                ? BuiltInVersionTypes[0]
                : currentText;
            versionType.SelectionStart = versionType.Text.Length;
        }

        private void VersionType_DrawItem(object sender, DrawItemEventArgs e)
        {
            e.DrawBackground();
            if (e.Index < 0 || e.Index >= versionType.Items.Count) return;

            var value = Convert.ToString(versionType.Items[e.Index]);
            var isCustom = !IsBuiltInVersionType(value);
            var foreColor = (e.State & DrawItemState.Selected) == DrawItemState.Selected
                ? SystemColors.HighlightText
                : InkBlueColor;
            var textBounds = new Rectangle(e.Bounds.Left + 6, e.Bounds.Top,
                Math.Max(0, e.Bounds.Width - (isCustom ? 38 : 10)), e.Bounds.Height);
            TextRenderer.DrawText(e.Graphics, value, versionType.Font, textBounds,
                foreColor, TextFormatFlags.Left | TextFormatFlags.VerticalCenter |
                TextFormatFlags.EndEllipsis);

            if (isCustom)
            {
                var deleteBounds = new Rectangle(e.Bounds.Right - 32, e.Bounds.Top,
                    28, e.Bounds.Height);
                using (var deleteFont = new Font(versionType.Font.FontFamily, 12F))
                {
                    TextRenderer.DrawText(e.Graphics, "×", deleteFont, deleteBounds,
                        foreColor, TextFormatFlags.HorizontalCenter |
                        TextFormatFlags.VerticalCenter);
                }
            }
            e.DrawFocusRectangle();
        }

        private void VersionType_SelectionChangeCommitted(object sender, EventArgs e)
        {
            if (versionType.SelectedIndex < 0) return;
            var value = Convert.ToString(versionType.SelectedItem);
            var isCustom = !IsBuiltInVersionType(value);
            var dropDownLeft = versionType.PointToScreen(new Point(0, versionType.Height)).X;
            var relativeX = Cursor.Position.X - dropDownLeft;
            var clickedDelete = isCustom && !typeSelectionByKeyboard &&
                relativeX >= versionType.DropDownWidth - 36;

            if (!clickedDelete)
            {
                versionType.Text = value;
                versionType.SelectionStart = versionType.Text.Length;
                return;
            }
            DeleteTypeMemory(value);
        }

        private void DeleteTypeMemory(string value)
        {
            var matched = customTypeHistory.FindIndex(delegate(string item)
            {
                return String.Equals(item, value, StringComparison.OrdinalIgnoreCase);
            });
            if (matched < 0) return;

            customTypeHistory.RemoveAt(matched);
            var restoredText = String.Equals(typeTextBeforeDropDown, value,
                StringComparison.OrdinalIgnoreCase)
                ? BuiltInVersionTypes[0]
                : typeTextBeforeDropDown;
            RefreshVersionTypeItems(restoredText);
            SaveSettings(companyPrefix.Text.Trim());

            if (customTypeHistory.Count > 0)
            {
                BeginInvoke(new MethodInvoker(delegate
                {
                    versionType.DroppedDown = true;
                }));
            }
        }

        private void CompanyPrefix_DrawItem(object sender, DrawItemEventArgs e)
        {
            e.DrawBackground();
            if (e.Index < 0 || e.Index >= companyPrefix.Items.Count) return;

            var value = Convert.ToString(companyPrefix.Items[e.Index]);
            var foreColor = (e.State & DrawItemState.Selected) == DrawItemState.Selected
                ? SystemColors.HighlightText
                : InkBlueColor;

            var textBounds = new Rectangle(
                e.Bounds.Left + 6,
                e.Bounds.Top,
                Math.Max(0, e.Bounds.Width - 38),
                e.Bounds.Height);
            TextRenderer.DrawText(e.Graphics, value, companyPrefix.Font, textBounds,
                foreColor, TextFormatFlags.Left | TextFormatFlags.VerticalCenter |
                TextFormatFlags.EndEllipsis);

            var deleteBounds = new Rectangle(
                e.Bounds.Right - 32,
                e.Bounds.Top,
                28,
                e.Bounds.Height);
            using (var deleteFont = new Font(companyPrefix.Font.FontFamily, 12F))
            {
                TextRenderer.DrawText(e.Graphics, "×", deleteFont,
                    deleteBounds, foreColor,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
            }
            e.DrawFocusRectangle();
        }

        private void CompanyPrefix_SelectionChangeCommitted(object sender, EventArgs e)
        {
            if (companyPrefix.SelectedIndex < 0) return;
            var value = Convert.ToString(companyPrefix.SelectedItem);

            var dropDownLeft = companyPrefix.PointToScreen(new Point(0, companyPrefix.Height)).X;
            var relativeX = Cursor.Position.X - dropDownLeft;
            var clickedDelete = !prefixSelectionByKeyboard &&
                relativeX >= companyPrefix.DropDownWidth - 36;

            if (!clickedDelete)
            {
                companyPrefix.Text = value;
                companyPrefix.SelectionStart = companyPrefix.Text.Length;
                return;
            }

            DeletePrefixMemory(value);
        }

        private void DeletePrefixMemory(string value)
        {
            var matched = prefixHistory.FindIndex(delegate(string item)
            {
                return String.Equals(item, value, StringComparison.OrdinalIgnoreCase);
            });
            if (matched < 0)
                return;

            prefixHistory.RemoveAt(matched);
            var restoredText = String.Equals(prefixTextBeforeDropDown, value,
                StringComparison.OrdinalIgnoreCase)
                ? String.Empty
                : prefixTextBeforeDropDown;
            RefreshPrefixItems(restoredText);
            SaveSettings(restoredText.Trim());

            if (prefixHistory.Count > 0)
            {
                BeginInvoke(new MethodInvoker(delegate
                {
                    companyPrefix.DroppedDown = true;
                }));
            }
        }

        private bool ContainsPrefix(string value)
        {
            return prefixHistory.Exists(delegate(string item)
            {
                return String.Equals(item, value, StringComparison.OrdinalIgnoreCase);
            });
        }

        private void RefreshPrefixItems(string currentText)
        {
            companyPrefix.BeginUpdate();
            companyPrefix.Items.Clear();
            foreach (var item in prefixHistory) companyPrefix.Items.Add(item);
            companyPrefix.EndUpdate();
            companyPrefix.Text = currentText;
            companyPrefix.SelectionStart = companyPrefix.Text.Length;
        }

        private static string EncodeSetting(string value)
        {
            return Convert.ToBase64String(Encoding.UTF8.GetBytes(value ?? String.Empty));
        }

        private static string DecodeSetting(string value)
        {
            try
            {
                return Encoding.UTF8.GetString(Convert.FromBase64String(value));
            }
            catch
            {
                return String.Empty;
            }
        }

    }

    internal static class Program
    {
        [STAThread]
        private static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new MainForm());
        }
    }
}
