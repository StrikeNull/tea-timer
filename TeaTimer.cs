using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Diagnostics;
using System.IO;
using System.Media;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using System.Xml.Serialization;

namespace TeaTimer
{
    public enum TimerState { Ready, Running, Paused, Finished }

    // Absolute uptime deadlines prevent timer-message delays from accumulating.
    // GetTickCount64 also includes sleep, so an overdue brew alerts on wake.
    public sealed class Countdown
    {
        private readonly Func<long> clock;
        private long deadline;
        private long remaining;
        public int DurationSeconds { get; private set; }
        public TimerState State { get; private set; }
        public Countdown(Func<long> clock) { this.clock = clock; Reset(120); }
        public long RemainingMilliseconds { get { return State == TimerState.Running ? Math.Max(0, deadline - clock()) : remaining; } }
        public int RemainingSeconds { get { return (int)Math.Ceiling(RemainingMilliseconds / 1000.0); } }
        public void Reset(int seconds)
        {
            if (seconds < 1 || seconds > 5999) throw new ArgumentOutOfRangeException("seconds");
            DurationSeconds = seconds; remaining = seconds * 1000L; State = TimerState.Ready;
        }
        public void Start()
        {
            if (State == TimerState.Running) return;
            if (State == TimerState.Finished) remaining = DurationSeconds * 1000L;
            deadline = clock() + remaining; State = TimerState.Running;
        }
        public bool Tick()
        {
            if (State != TimerState.Running || RemainingMilliseconds > 0) return false;
            remaining = 0; State = TimerState.Finished; return true;
        }
        // Return true if the deadline passed just before the pause click.
        public bool Pause()
        {
            if (State != TimerState.Running) return false;
            remaining = Math.Max(0, deadline - clock());
            if (remaining == 0) { State = TimerState.Finished; return true; }
            State = TimerState.Paused; return false;
        }
    }

    public sealed class Tea
    {
        public string Name, Caption, Hint;
        public int Seconds;
        public Color Accent;
        public Tea(string name, string caption, int seconds, string hint, string color)
        { Name = name; Caption = caption; Seconds = seconds; Hint = hint; Accent = ColorTranslator.FromHtml(color); }
        public static Tea[] All = {
            new Tea("绿茶", "清新 · 2 分钟", 120, "杯泡参考 2 分钟，喜欢清淡可适当缩短。", "#53785D"),
            new Tea("红茶", "醇香 · 3 分钟", 180, "杯泡参考 3–5 分钟，可按口味调整。", "#B14443"),
            new Tea("乌龙茶", "回甘 · 2 分 30 秒", 150, "杯泡参考 2–3 分钟，功夫泡请自定义短时间。", "#9C702D"),
            new Tea("白茶", "淡雅 · 2 分钟", 120, "杯泡起始参考 1–2 分钟，具体以茶叶说明为准。", "#887D65"),
            new Tea("熟普洱", "温润 · 4 分钟", 240, "杯泡参考 4–5 分钟，盖碗快出汤请自定义。", "#80493D"),
            new Tea("花草茶", "芳香 · 4 分钟", 240, "杯泡参考 3–4 分钟，具体以包装说明为准。", "#AD5A82")
        };
    }

    public sealed class Preferences
    {
        public int Selected;
        public int[] Times = new int[] { 120, 180, 150, 120, 240, 240 };
        public bool Sound = true;
        public bool OnTop = false;
        public bool ShowMascot = true;
        public bool AnimateMascot = true;
        public int MascotKind = 0;
        public int WindowWidth = 280;
        public int WindowHeight = 236;
        public static string FilePath { get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "YiZhanCha", "settings.xml"); } }
        public static Preferences Load(string path = null)
        {
            try
            {
                using (FileStream stream = File.OpenRead(path ?? FilePath))
                {
                    Preferences p = (Preferences)new XmlSerializer(typeof(Preferences)).Deserialize(stream);
                    if (p.Selected < 0 || p.Selected >= Tea.All.Length) p.Selected = 0;
                    if (p.Times == null || p.Times.Length != Tea.All.Length) p.Times = new Preferences().Times;
                    for (int i = 0; i < p.Times.Length; i++) if (p.Times[i] < 1 || p.Times[i] > 5999) p.Times[i] = Tea.All[i].Seconds;
                    p.WindowWidth = Math.Max(260, Math.Min(1280, p.WindowWidth));
                    p.WindowHeight = Math.Max(208, Math.Min(960, p.WindowHeight));
                    if (p.MascotKind < 0 || p.MascotKind >= MascotCatalog.Names.Length) p.MascotKind = 0;
                    return p;
                }
            }
            catch (IOException) { return new Preferences(); }
            catch (InvalidOperationException) { return new Preferences(); }
            catch (UnauthorizedAccessException) { return new Preferences(); }
        }
        public bool Save(string path = null)
        {
            try
            {
                path = path ?? FilePath;
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                string temporary = path + ".tmp";
                using (FileStream stream = File.Create(temporary)) new XmlSerializer(typeof(Preferences)).Serialize(stream, this);
                if (File.Exists(path)) File.Replace(temporary, path, null); else File.Move(temporary, path);
                return true;
            }
            catch (IOException) { return false; }
            catch (UnauthorizedAccessException) { return false; }
        }
    }

    internal static class Style
    {
        public static readonly Color Background = ColorTranslator.FromHtml("#FBF5F2");
        public static readonly Color Ink = ColorTranslator.FromHtml("#442D2B");
        public static readonly Color Muted = ColorTranslator.FromHtml("#947C76");
        public static readonly Color Green = ColorTranslator.FromHtml("#A84443");
        public static Color Blend(Color foreground, Color background, float weight)
        { return Color.FromArgb((int)(foreground.R * weight + background.R * (1 - weight)), (int)(foreground.G * weight + background.G * (1 - weight)), (int)(foreground.B * weight + background.B * (1 - weight))); }
        public static void CaptureForm(Form form, string path)
        {
            using (Bitmap bitmap = new Bitmap(form.Width, form.Height))
            {
                using (Graphics actual = form.CreateGraphics()) bitmap.SetResolution(actual.DpiX, actual.DpiY);
                form.DrawToBitmap(bitmap, new Rectangle(0, 0, form.Width, form.Height)); bitmap.Save(path, System.Drawing.Imaging.ImageFormat.Png);
            }
        }
        public static Font Font(float size, FontStyle style) { return new Font("Microsoft YaHei UI", size, style, GraphicsUnit.Point); }
        public static GraphicsPath Rounded(RectangleF rect, float radius)
        {
            GraphicsPath path = new GraphicsPath(); float d = radius * 2;
            path.AddArc(rect.X, rect.Y, d, d, 180, 90); path.AddArc(rect.Right - d, rect.Y, d, d, 270, 90);
            path.AddArc(rect.Right - d, rect.Bottom - d, d, d, 0, 90); path.AddArc(rect.X, rect.Bottom - d, d, d, 90, 90); path.CloseFigure(); return path;
        }
        public static void Text(Graphics g, string text, float size, FontStyle style, Color color, RectangleF rect, StringAlignment align)
        {
            using (Font f = new Font("Microsoft YaHei UI", size * 96f / 72f, style, GraphicsUnit.Pixel)) using (Brush b = new SolidBrush(color)) using (StringFormat sf = new StringFormat())
            { sf.Alignment = align; sf.LineAlignment = StringAlignment.Center; g.DrawString(text, f, b, rect, sf); }
        }
        public static Icon MakeIcon(Color accent = default(Color))
        {
            if (accent.IsEmpty) accent = Green;
            using (Bitmap b = new Bitmap(32, 32))
            {
                using (Graphics g = Graphics.FromImage(b))
                {
                    g.SmoothingMode = SmoothingMode.AntiAlias;
                    g.Clear(Color.Transparent);
                    using (Brush brush = new SolidBrush(accent)) g.FillEllipse(brush, 0, 0, 32, 32);
                    using (Pen pen = new Pen(Color.White, 2))
                    { g.DrawArc(pen, 20, 13, 7, 8, -90, 180); g.DrawArc(pen, 7, 8, 15, 15, 0, 180); g.DrawLine(pen, 7, 15, 7, 18); g.DrawLine(pen, 22, 15, 22, 18); g.DrawLine(pen, 8, 26, 23, 26); g.DrawArc(pen, 12, 3, 5, 9, 90, 160); }
                }
                IntPtr h = b.GetHicon();
                try { using (Icon original = Icon.FromHandle(h)) return (Icon)original.Clone(); }
                finally { Native.DestroyIcon(h); }
            }
        }
    }

    internal static class Native
    {
        [DllImport("kernel32.dll")] public static extern ulong GetTickCount64();
        [DllImport("user32.dll")] public static extern bool DestroyIcon(IntPtr icon);
    }

    internal sealed class ActionButton : Button
    {
        public bool Primary;
        public Color Accent = Color.Empty;
        private bool hover;
        public ActionButton(string text, bool primary)
        { Text = text; Primary = primary; Cursor = Cursors.Hand; FlatStyle = FlatStyle.Flat; FlatAppearance.BorderSize = 0; SetStyle(ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer, true); }
        protected override void OnMouseEnter(EventArgs e) { hover = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { hover = false; Invalidate(); base.OnMouseLeave(e); }
        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics; g.SmoothingMode = SmoothingMode.AntiAlias;
            Color accent = Accent.IsEmpty ? Style.Green : Accent;
            Color fill = !Enabled ? ColorTranslator.FromHtml("#E9E1DE") : Primary ? (hover ? Style.Blend(accent, Color.Black, .87f) : accent) : Color.White;
            using (GraphicsPath p = Style.Rounded(new RectangleF(1, 1, Width - 3, Height - 3), 12 * Height / 48f))
            using (Brush b = new SolidBrush(fill)) { g.FillPath(b, p); }
            Style.Text(g, Text, Font.Size * g.DpiY / 96f, FontStyle.Bold, !Enabled ? Style.Muted : Primary ? Color.White : accent, new RectangleF(0, 0, Width, Height), StringAlignment.Center);
            if (Focused) ControlPaint.DrawFocusRectangle(g, new Rectangle(6, 6, Width - 12, Height - 12), Primary ? Color.White : accent, fill);
        }
    }

    internal sealed class AlertForm : Form
    {
        internal readonly ReminderMascot Mascot;
        public AlertForm(string tea, Icon icon, Color accent = default(Color), int mascotKind = 0, bool showMascot = true, bool animateMascot = true)
        {
            SuspendLayout();
            if (accent.IsEmpty) accent = Style.Green;
            Text = "茶泡好了 · 一盏茶"; Icon = icon; TopMost = true; ShowInTaskbar = true;
            AutoScaleMode = AutoScaleMode.None;
            float scale; using (Graphics g = CreateGraphics()) scale = g.DpiY / 96f;
            ClientSize = new Size((int)Math.Round((showMascot ? 300 : 280) * scale), (int)Math.Round(166 * scale)); BackColor = Style.Blend(accent, Color.White, .06f); StartPosition = FormStartPosition.CenterScreen;
            FormBorderStyle = FormBorderStyle.FixedDialog; MaximizeBox = false; MinimizeBox = false;
            Label title = new Label { Text = "茶泡好了", Font = Style.Font(18, FontStyle.Bold), ForeColor = accent, TextAlign = ContentAlignment.MiddleCenter, Bounds = showMascot ? new Rectangle(120, 20, 168, 40) : new Rectangle(20, 16, 240, 40) };
            Label body = new Label { Text = tea + (showMascot ? "已到时间，\n请及时出汤。" : "已到时间，请及时出汤。"), Font = Style.Font(9, FontStyle.Regular), ForeColor = Style.Muted, TextAlign = ContentAlignment.MiddleCenter, Bounds = showMascot ? new Rectangle(120, 62, 168, 40) : new Rectangle(14, 60, 252, 32) };
            ActionButton ok = new ActionButton("知道了，喝茶去", true) { Accent = accent, Bounds = new Rectangle(showMascot ? 50 : 40, 116, 200, 36), DialogResult = DialogResult.OK };
            ok.Click += delegate { Close(); }; Controls.Add(title); Controls.Add(body); Controls.Add(ok); AcceptButton = ok; CancelButton = ok;
            if (showMascot)
            {
                Mascot = new ReminderMascot(mascotKind, accent, animateMascot) { Bounds = new Rectangle(6, 2, 112, 112) };
                Controls.Add(Mascot);
            }
            foreach (Control child in Controls)
            {
                Rectangle b = child.Bounds;
                child.Bounds = new Rectangle((int)Math.Round(b.X * scale), (int)Math.Round(b.Y * scale), (int)Math.Round(b.Width * scale), (int)Math.Round(b.Height * scale));
            }
            ResumeLayout(false);
        }
    }

    internal static class Program
    {
        [STAThread]
        private static void Main(string[] args)
        {
            Application.EnableVisualStyles(); Application.SetCompatibleTextRenderingDefault(false);
            if (args.Length > 0 && args[0] == "--self-test") { RunTests(args.Length > 1 ? args[1] : AppDomain.CurrentDomain.BaseDirectory); return; }
            bool created;
            using (System.Threading.Mutex mutex = new System.Threading.Mutex(true, "Local\\YiZhanCha-TeaTimer", out created))
            {
                if (!created) { MessageBox.Show("一盏茶已在运行，请双击系统托盘里的茶杯图标打开。", "一盏茶"); return; }
                Application.Run(new TeaForm(Preferences.Load(), delegate { return (long)Native.GetTickCount64(); }));
            }
        }
        private static void Assert(bool value, string message) { if (!value) throw new Exception(message); }
        private static void RunTests(string directory)
        {
            Directory.CreateDirectory(directory); string report = Path.Combine(directory, "test-results.txt");
            try
            {
                long now = 1000; Countdown c = new Countdown(delegate { return now; });
                using (Icon icon = Style.MakeIcon()) using (FileStream file = File.Create(Path.Combine(directory, "tea.ico"))) icon.Save(file);
                c.Reset(3); c.Start(); now += 1100; Assert(c.RemainingSeconds == 2, "ceiling display");
                c.Pause(); now += 90000; Assert(c.RemainingMilliseconds == 1900, "pause holds time");
                c.Start(); now += 1899; Assert(!c.Tick(), "no early completion"); now += 1;
                Assert(c.Tick() && c.State == TimerState.Finished, "completion"); Assert(!c.Tick(), "single completion");
                c.Start(); Assert(c.RemainingSeconds == 3, "repeat"); now += 3600000; Assert(c.Tick(), "delayed tick / sleep recovery");
                c.Reset(1); c.Start(); now += 1000; Assert(c.Pause() && c.State == TimerState.Finished, "pause deadline race");
                bool rejected = false; try { c.Reset(0); } catch (ArgumentOutOfRangeException) { rejected = true; } Assert(rejected, "zero rejected");
                c.Reset(5999); Assert(c.RemainingSeconds == 5999, "maximum duration");
                Preferences prefs = new Preferences();
                using (TeaForm form = new TeaForm(prefs, delegate { return now; }, true))
                {
                    form.Show(); Application.DoEvents();
                    File.WriteAllText(Path.Combine(directory, "layout.txt"), "Client: " + form.ClientSize + " Scale dimensions: " + form.AutoScaleDimensions + " Current: " + form.CurrentAutoScaleDimensions + " Screen: " + Screen.PrimaryScreen.WorkingArea);
                    form.CapturePreview(Path.Combine(directory, "preview.png"));
                    for (int i = 0; i < Tea.All.Length; i++) { form.SelectTea(i); Assert(form.Model.DurationSeconds == Tea.All[i].Seconds, "tea preset " + i); }
                    form.SetDuration(2); form.ToggleTimer(); now += 1000; form.ToggleTimer();
                    Assert(form.Model.State == TimerState.Paused, "UI pause"); now += 100000; form.ToggleTimer(); now += 1000; form.Pump(); form.Pump();
                    Assert(form.CompletionCount == 1 && form.Model.State == TimerState.Finished, "UI completes once");
                    form.CapturePreview(Path.Combine(directory, "preview-finished.png"));
                    form.ToggleTimer(); Assert(form.Model.State == TimerState.Running, "UI repeat"); form.ResetTimer(); Assert(form.Model.State == TimerState.Ready, "UI reset");
                    form.SetDuration(60); form.ToggleTimer(); now += 250; form.CapturePreview(Path.Combine(directory, "preview-brewing.png")); form.ResetTimer();
                    bool uiRejected = false; try { form.SetDuration(0); } catch (ArgumentOutOfRangeException) { uiRejected = true; }
                    Assert(uiRejected, "UI blocks zero");
                    form.TestNotifications = true; form.SetDuration(1); form.ToggleTimer(); now += 1000; form.Pump(); Application.DoEvents();
                    Assert(form.HasVisibleAlert, "actual completion alert");
                    Assert(form.CurrentAlert.Mascot.Kind == 0 && form.CurrentAlert.Mascot.AnimationRunning, "default animated reminder role");
                    ReminderMascot closedMascot = form.CurrentAlert.Mascot;
                    form.ResetTimer(); Assert(!form.HasVisibleAlert, "reset dismisses completion alert");
                    Assert(closedMascot.IsDisposed, "closing reminder disposes animation control");
                    form.SelectTea(0);
                    using (SettingsForm settings = new SettingsForm(prefs, false))
                    {
                        settings.Show(); Application.DoEvents(); settings.CapturePreview(Path.Combine(directory, "preview-settings.png"));
                        settings.SetTime(0, 0); Assert(!settings.SaveChanges(), "settings reject zero");
                        settings.SetTime(0, 67); settings.SetTime(1, 89);
                        settings.ChooseMascot(1);
                        Assert(settings.SaveChanges() && prefs.Times[0] == 120, "configuration edits are isolated until save");
                        form.ApplySettings(settings.Result); Assert(form.Model.DurationSeconds == 67 && prefs.Times[1] == 89, "apply per-tea times");
                        Assert(prefs.MascotKind == 1, "choose GPT mascot");
                        form.CapturePreview(Path.Combine(directory, "preview-gpt.png"));
                        settings.Close();
                    }
                    form.ToggleTimer(); now += 1000; long remaining = form.Model.RemainingMilliseconds;
                    Preferences changed = new Preferences(); changed.Times = (int[])prefs.Times.Clone(); changed.Times[0] = 40; changed.MascotKind = 1;
                    form.ApplySettings(changed);
                    Assert(form.Model.State == TimerState.Running && form.Model.DurationSeconds == 67 && form.Model.RemainingMilliseconds == remaining, "configuration preserves active countdown");
                    form.ResetTimer(); Assert(form.Model.DurationSeconds == 40, "next brew uses new duration");
                    form.SelectTea(1); form.CapturePreview(Path.Combine(directory, "preview-red.png"));
                    using (SettingsForm dragonSettings = new SettingsForm(prefs, false))
                    {
                        dragonSettings.ChooseMascot(2); Assert(dragonSettings.SaveChanges(), "dragon role settings");
                        form.ApplySettings(dragonSettings.Result); Assert(prefs.MascotKind == 2, "select dragon mascot");
                    }
                    form.CapturePreview(Path.Combine(directory, "preview-dragon.png"));
                    form.ToggleTimer(); now += 300; form.CapturePreview(Path.Combine(directory, "preview-dragon-brewing.png"));
                    now += 89000; form.Pump(); form.CapturePreview(Path.Combine(directory, "preview-dragon-ready.png"));
                    Assert(form.CurrentAlert.Mascot.Kind == 2, "completion reminder follows chosen dragon role");
                    Style.CaptureForm(form.CurrentAlert, Path.Combine(directory, "preview-alert-dragon.png")); form.ResetTimer();
                    Preferences reminderSettings = Preferences.Load(Path.Combine(directory, "no-settings.xml"));
                    reminderSettings.Times = (int[])prefs.Times.Clone(); reminderSettings.Times[1] = 1;
                    reminderSettings.MascotKind = 1; reminderSettings.AnimateMascot = false;
                    form.ApplySettings(reminderSettings); form.ToggleTimer(); now += 1000; form.Pump();
                    Assert(form.CurrentAlert.Mascot.Kind == 1 && !form.CurrentAlert.Mascot.AnimationRunning, "reminder respects static selected role");
                    form.ResetTimer(); reminderSettings.ShowMascot = false; form.ApplySettings(reminderSettings);
                    form.ToggleTimer(); now += 1000; form.Pump();
                    Assert(form.HasVisibleAlert && form.CurrentAlert.Mascot == null, "hiding mascot preserves completion reminder");
                    Style.CaptureForm(form.CurrentAlert, Path.Combine(directory, "preview-alert-text.png")); form.ResetTimer();
                    reminderSettings.ShowMascot = true; reminderSettings.AnimateMascot = true;
                    reminderSettings.MascotKind = 2; reminderSettings.Times[1] = 89; form.ApplySettings(reminderSettings);
                    form.Size = form.MinimumSize; Application.DoEvents(); form.CapturePreview(Path.Combine(directory, "preview-minimum.png"));
                    Assert(form.Controls[form.Controls.Count - 1].Bottom <= form.ClientSize.Height, "minimum-size buttons fit");
                    form.ClientSize = new Size(660, 440); Application.DoEvents();
                    form.RememberWindowSize(); form.CapturePreview(Path.Combine(directory, "preview-resized.png"));
                    string settingsPath = Path.Combine(directory, "settings-test.xml");
                    Assert(prefs.Save(settingsPath), "write configuration"); Preferences restored = Preferences.Load(settingsPath);
                    Assert(restored.Times[0] == 40 && restored.Times[1] == 89 && restored.Selected == 1 && restored.WindowWidth == prefs.WindowWidth && restored.WindowHeight == prefs.WindowHeight && restored.ShowMascot == prefs.ShowMascot && restored.AnimateMascot == prefs.AnimateMascot && restored.MascotKind == 2, "configuration and window-size persistence");
                    using (TeaForm reopened = new TeaForm(restored, delegate { return now; }, true))
                    {
                        reopened.Show(); Application.DoEvents();
                        Assert(reopened.Model.DurationSeconds == 89 && reopened.ClientSize == form.ClientSize, "reopening restores tea time and window size"); reopened.ShutdownTest();
                    }
                    form.ShutdownTest();
                }
                string legacyPath = Path.Combine(directory, "legacy-settings.xml");
                File.WriteAllText(legacyPath, "<Preferences><Selected>0</Selected><Times><int>60</int><int>180</int><int>150</int><int>120</int><int>240</int><int>240</int></Times><Sound>true</Sound><OnTop>true</OnTop></Preferences>");
                Preferences legacy = Preferences.Load(legacyPath);
                Assert(legacy.Times[0] == 60 && legacy.OnTop && legacy.WindowWidth == 280 && legacy.WindowHeight == 236, "old configuration migrates without losing times");
                for (int role = 0; role < MascotCatalog.Names.Length; role++)
                {
                    using (AlertForm alert = new AlertForm("花草茶", SystemIcons.Information, Tea.All[5].Accent, role))
                    {
                        alert.Show(); Application.DoEvents();
                        foreach (Control child in alert.Controls)
                            Assert(child.Right <= alert.ClientSize.Width && child.Bottom <= alert.ClientSize.Height, "reminder layout fits at current DPI");
                        Style.CaptureForm(alert, Path.Combine(directory, "preview-alert-" + role + ".png")); alert.Close();
                    }
                }
                File.WriteAllText(report, "PASS: countdown, pause/resume, deadline boundary, single completion, repeat, sleep recovery, zero/max bounds, all 6 presets, UI transitions, alerts, selected reminder roles, static/hidden reminders, animation disposal, reminder DPI layouts, configuration validation, active-timer preservation, resize layouts, persistence/reopening, legacy migration and renders.");
            }
            catch (Exception e) { File.WriteAllText(report, "FAIL: " + e); Environment.ExitCode = 1; }
        }
    }
}
