using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

internal static class GuiController
{
    internal static void Generate(string input, string output, bool overwrite, Action<string> log)
    {
        // Do not pass detached input bytes: this retains the shared path and file-handle guard.
        PatcherCore.Process(input, output, false, overwrite, log);
    }
}

internal sealed class MainForm : Form
{
    private static readonly Color Accent = Color.FromArgb(22, 138, 92);
    private static readonly Color Muted = Color.FromArgb(110, 110, 110);
    private static readonly Color Ok = Color.FromArgb(22, 120, 60);
    private static readonly Color Bad = Color.FromArgb(176, 40, 40);

    private readonly TextBox input = new TextBox { ReadOnly = true, Font = new Font("Consolas", 9.5F) };
    private readonly TextBox logs = new TextBox
    {
        ReadOnly = true,
        Multiline = true,
        ScrollBars = ScrollBars.Vertical,
        WordWrap = false,
        Font = new Font("Consolas", 9F),
        BackColor = Color.FromArgb(250, 250, 250),
        BorderStyle = BorderStyle.FixedSingle,
    };
    private readonly Button pick = new Button { Text = "选择文件…" };
    private readonly Button generate = new Button { Text = "生成解锁版副本…", Enabled = false };
    private readonly Label status = new Label { AutoSize = false, Height = 20, ForeColor = Muted };
    private readonly ProgressBar progress = new ProgressBar { Style = ProgressBarStyle.Marquee, Visible = false };

    public MainForm()
    {
        Text = "PCL 整合包解锁器 v3.2（第三方修复版）";
        Font = new Font("Microsoft YaHei UI", 9.5F);
        ClientSize = new Size(780, 600);
        MinimumSize = new Size(700, 540);
        StartPosition = FormStartPosition.CenterScreen;
        BackColor = Color.White;
        AllowDrop = true;

        // 倒序加入，使停靠顺序为：标题 → 第1步 → 第2步 → 日志 → 底部
        Controls.Add(BuildLog());
        Controls.Add(BuildFooter());
        Controls.Add(BuildStepTwo());
        Controls.Add(BuildStepOne());
        Controls.Add(BuildHeader());

        pick.Click += (s, e) => PickFile();
        generate.Click += (s, e) => DoGenerate();
        DragEnter += (s, e) => e.Effect = e.Data.GetDataPresent(DataFormats.FileDrop) ? DragDropEffects.Copy : DragDropEffects.None;
        DragDrop += (s, e) =>
        {
            var files = e.Data.GetData(DataFormats.FileDrop) as string[];
            if (files != null && files.Length == 1) SelectInput(files[0]);
        };
    }

    // ── 顶部标题 ───────────────────────────────────────────
    private Control BuildHeader()
    {
        var header = new Panel { Dock = DockStyle.Top, Height = 92, BackColor = Accent };

        var title = new Label
        {
            Text = "PCL 整合包解锁器",
            Font = new Font("Microsoft YaHei UI", 18F, FontStyle.Bold),
            ForeColor = Color.White,
            AutoSize = true,
            Location = new Point(24, 16),
        };
        var subtitle = new Label
        {
            Text = "为 PCL 2.99 恢复整合包安装能力 · 原启动器不会被修改",
            ForeColor = Color.FromArgb(226, 244, 236),
            AutoSize = true,
            Location = new Point(26, 58),
        };
        header.Controls.Add(title);
        header.Controls.Add(subtitle);
        return header;
    }

    // ── 第 1 步 ────────────────────────────────────────────
    private Control BuildStepOne()
    {
        var box = new GroupBox
        {
            Text = " 第 1 步 · 选择原版 PCL2.exe ",
            Dock = DockStyle.Top,
            Height = 138,
            Padding = new Padding(14),
            ForeColor = Color.FromArgb(60, 60, 60),
        };

        // 用 TableLayoutPanel 排「输入框 + 按钮」，避免锚定互相压盖
        var row = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            Height = 32,
            ColumnCount = 2,
            RowCount = 1,
            Margin = new Padding(0),
            Padding = new Padding(0),
        };
        row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        row.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 118F));
        input.Dock = DockStyle.Fill;
        input.Margin = new Padding(0, 2, 8, 0);
        pick.AutoSize = false;
        pick.Text = "选择文件…";
        pick.Dock = DockStyle.Fill;
        pick.Margin = new Padding(0, 2, 0, 0);
        row.Controls.Add(input, 0, 0);
        row.Controls.Add(pick, 1, 0);

        var hint = new Label
        {
            Text = "也可以直接把 PCL2.exe 拖进这个窗口",
            ForeColor = Muted,
            Dock = DockStyle.Top,
            Height = 24,
            Padding = new Padding(2, 6, 0, 0),
        };

        status.Dock = DockStyle.Top;
        status.Height = 22;
        status.Text = "尚未选择文件。支持 2.99.0.0 的三种官方构建，其余版本会被拒绝。";

        // 倒序添加，使停靠顺序为：row → hint → status
        box.Controls.Add(status);
        box.Controls.Add(hint);
        box.Controls.Add(row);
        return box;
    }

    // ── 第 2 步 ────────────────────────────────────────────
    private Control BuildStepTwo()
    {
        var box = new GroupBox
        {
            Text = " 第 2 步 · 生成解锁版副本 ",
            Dock = DockStyle.Top,
            Height = 126,
            Padding = new Padding(14),
            ForeColor = Color.FromArgb(60, 60, 60),
        };

        generate.SetBounds(16, 28, 170, 32);
        generate.BackColor = Color.FromArgb(232, 246, 238);

        progress.SetBounds(198, 33, 540, 22);
        progress.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;

        var hint = new Label
        {
            Text = "另存为一个新的 exe，原启动器保持不动。\n生成前会自动校验：产物哈希、其余方法体、元数据与资源；全部通过才写盘。",
            ForeColor = Muted,
            Location = new Point(18, 70),
            Size = new Size(720, 40),
        };

        box.Controls.Add(generate);
        box.Controls.Add(progress);
        box.Controls.Add(hint);
        return box;
    }

    // ── 日志 ───────────────────────────────────────────────
    private Control BuildLog()
    {
        var panel = new Panel { Dock = DockStyle.Fill, Padding = new Padding(14, 6, 14, 6) };

        var caption = new Label
        {
            Text = "处理日志",
            Dock = DockStyle.Top,
            Height = 22,
            ForeColor = Color.FromArgb(60, 60, 60),
        };

        logs.Dock = DockStyle.Fill;
        panel.Controls.Add(logs);
        panel.Controls.Add(caption);
        return panel;
    }

    // ── 底部 ───────────────────────────────────────────────
    private Control BuildFooter()
    {
        var footer = new Label
        {
            Text = "支持：更新累积版 / 赞助版 / 未赞助版 · 单文件工具 · .NET Framework 4.8（系统自带）",
            Dock = DockStyle.Bottom,
            Height = 30,
            TextAlign = ContentAlignment.MiddleLeft,
            Padding = new Padding(16, 0, 0, 0),
            ForeColor = Muted,
            Font = new Font("Microsoft YaHei UI", 8.5F),
        };
        return footer;
    }

    // ── 逻辑 ───────────────────────────────────────────────
    private void PickFile()
    {
        using (var dialog = new OpenFileDialog { Filter = "PCL 启动器 (*.exe)|*.exe", Title = "选择原版 PCL2 启动器" })
            if (dialog.ShowDialog(this) == DialogResult.OK) SelectInput(dialog.FileName);
    }

    private void SelectInput(string path)
    {
        generate.Enabled = false;
        try
        {
            string info = PatcherCore.Inspect(path);
            string version = info;
            string hash = "";
            int split = info.IndexOf("; SHA256=", StringComparison.Ordinal);
            if (split >= 0)
            {
                version = info.Substring(0, split);
                hash = info.Substring(split + "; SHA256=".Length);
            }
            input.Text = Path.GetFullPath(path);
            status.ForeColor = Ok;
            status.Text = "√ 已识别：" + version + "     SHA-256：" + hash;
            logs.Clear();
            Log("已读取：" + Path.GetFileName(path));
            Log("版本：" + version);
            Log("输入 SHA-256：" + hash);
            generate.Enabled = true;
        }
        catch (Exception e)
        {
            input.Clear();
            status.ForeColor = Bad;
            status.Text = "× 不支持这个文件：" + e.Message;
            Log("拒绝：" + e.Message);
            MessageBox.Show(this, e.Message, "无法使用此文件", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    private void DoGenerate()
    {
        using (var dialog = new SaveFileDialog
        {
            Title = "保存解锁版副本（不会覆盖原启动器）",
            Filter = "PCL 启动器 (*.exe)|*.exe",
            FileName = Path.GetFileNameWithoutExtension(input.Text) + ".整合包解锁版.exe",
            InitialDirectory = Path.GetDirectoryName(input.Text),
            OverwritePrompt = true,
        })
        {
            if (dialog.ShowDialog(this) != DialogResult.OK) return;

            pick.Enabled = generate.Enabled = false;
            progress.Visible = true;
            status.ForeColor = Muted;
            status.Text = "正在生成，请稍候……";
            try
            {
                GuiController.Generate(input.Text, dialog.FileName, File.Exists(dialog.FileName), Log);
                status.ForeColor = Ok;
                status.Text = "√ 已生成并通过全部校验：" + Path.GetFileName(dialog.FileName);
                MessageBox.Show(this, "已生成并校验副本，原文件保持不变。\n\n" + dialog.FileName, "完成",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception e)
            {
                status.ForeColor = Bad;
                status.Text = "× 生成失败：" + e.Message;
                Log("生成失败：" + e.Message);
                MessageBox.Show(this, e.Message, "生成失败", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                progress.Visible = false;
                pick.Enabled = true;
                generate.Enabled = true;
            }
        }
    }

    private void Log(string text)
    {
        logs.AppendText("[" + DateTime.Now.ToString("HH:mm:ss") + "] " + text + Environment.NewLine);
    }
}
