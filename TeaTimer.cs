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
        public int[] RoundIncrements = new int[6];
        public bool Sound = true;
        public bool VoiceReminder = true;
        public bool OnTop = false;
        public bool ShowMascot = true;
        public bool AnimateMascot = true;
        public int MascotKind = 0;
        public ReminderProfile[] Reminders = { new ReminderProfile(), new ReminderProfile(), new ReminderProfile() };
        public ReminderProfile[] Greetings = MediaCatalog.CopyProfiles(null, 1);
        public ReminderProfile[] Brewing = MediaCatalog.CopyProfiles(null, 2);
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
                    if (p.RoundIncrements == null || p.RoundIncrements.Length != Tea.All.Length) p.RoundIncrements = new int[Tea.All.Length];
                    for (int i = 0; i < p.Times.Length; i++) if (p.Times[i] < 1 || p.Times[i] > 5999) p.Times[i] = Tea.All[i].Seconds;
                    for (int i = 0; i < p.RoundIncrements.Length; i++) p.RoundIncrements[i] = Math.Max(0, Math.Min(5999, p.RoundIncrements[i]));
                    p.WindowWidth = Math.Max(260, Math.Min(1280, p.WindowWidth));
                    p.WindowHeight = Math.Max(208, Math.Min(960, p.WindowHeight));
                    if (p.MascotKind < 0 || p.MascotKind >= MascotCatalog.Names.Length) p.MascotKind = 0;
                    p.Reminders = MediaCatalog.CopyProfiles(p.Reminders);
                    p.Greetings = MediaCatalog.CopyProfiles(p.Greetings, 1);
                    p.Brewing = MediaCatalog.CopyProfiles(p.Brewing, 2);
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
        private readonly ReminderSpeech speech;
        public AlertForm(string tea, Icon icon, Color accent = default(Color), int mascotKind = 0, bool showMascot = true, bool animateMascot = true, bool voiceReminder = false, ReminderProfile profile = null, bool preview = false, int mediaScene = 0)
        {
            SuspendLayout();
            if (accent.IsEmpty) accent = Style.Green;
            Text = preview ? "动画预览 · 一盏茶" : "茶泡好了 · 一盏茶"; Icon = icon; TopMost = true; ShowInTaskbar = true;
            AutoScaleMode = AutoScaleMode.None;
            float scale; using (Graphics g = CreateGraphics()) scale = g.DpiY / 96f;
            ClientSize = new Size((int)Math.Round((showMascot ? 320 : 280) * scale), (int)Math.Round(166 * scale)); BackColor = Style.Blend(accent, Color.White, .06f); StartPosition = FormStartPosition.CenterScreen;
            FormBorderStyle = FormBorderStyle.FixedDialog; MaximizeBox = false; MinimizeBox = false;
            Label title = new Label { Text = preview ? "动画预览" : "茶泡好了", Font = Style.Font(18, FontStyle.Bold), ForeColor = accent, TextAlign = ContentAlignment.MiddleCenter, Bounds = showMascot ? new Rectangle(154, 16, 158, 40) : new Rectangle(20, 16, 240, 40) };
            Label body = new Label { Text = preview ? "播放一次后\n停在最后一帧。" : tea + (showMascot ? "已到时间，\n请及时出汤。" : "已到时间，请及时出汤。"), Font = Style.Font(9, FontStyle.Regular), ForeColor = Style.Muted, TextAlign = ContentAlignment.MiddleCenter, Bounds = showMascot ? new Rectangle(154, 58, 158, 40) : new Rectangle(14, 60, 252, 32) };
            ActionButton ok = new ActionButton(preview ? "关闭预览" : "知道了，喝茶去", true) { Accent = accent, Bounds = showMascot ? new Rectangle(164, 116, 144, 36) : new Rectangle(40, 116, 200, 36), DialogResult = DialogResult.OK };
            ok.Click += delegate { Close(); }; Controls.Add(title); Controls.Add(body); Controls.Add(ok); AcceptButton = ok; CancelButton = ok;
            if (showMascot)
            {
                Mascot = new ReminderMascot(mascotKind, accent, animateMascot, profile, mediaScene) { Bounds = new Rectangle(4, 4, 150, 154) };
                Controls.Add(Mascot);
            }
            foreach (Control child in Controls)
            {
                Rectangle b = child.Bounds;
                child.Bounds = new Rectangle((int)Math.Round(b.X * scale), (int)Math.Round(b.Y * scale), (int)Math.Round(b.Width * scale), (int)Math.Round(b.Height * scale));
            }
            ResumeLayout(false);
            if (voiceReminder) speech = new ReminderSpeech(profile == null ? 0 : profile.VoiceStyle, profile == null ? null : profile.VoiceFile, mediaScene);
        }
        protected override void OnShown(EventArgs e) { base.OnShown(e); if (speech != null) speech.Play(); }
        protected override void Dispose(bool disposing) { if (disposing && speech != null) speech.Dispose(); base.Dispose(disposing); }
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
        private static void RunInteractionTests(string directory)
        {
            long now = 1000;
            Preferences prefs = new Preferences { MascotKind = 2 };
            prefs.Times[0] = prefs.Times[1] = 6; prefs.RoundIncrements[1] = 2;
            using (TeaForm form = new TeaForm(prefs, delegate { return now; }, true) { TestInteractions = true, TestNotifications = true })
            {
                form.Show(); Application.DoEvents();
                Assert(form.InteractionCount == 1 && form.LastInteractionScene == 1 && form.MainHasInteractionClip, "startup plays greeting in main window once");
                now += 250; form.Pump(); form.CapturePreview(Path.Combine(directory, "preview-today.png"));
                now += 3000; form.Pump(); Assert(!form.MainInteractionActive && form.Model.State == TimerState.Ready, "greeting finishes without starting timer");
                form.Hide(); form.Show(); Application.DoEvents(); Assert(form.InteractionCount == 1, "restoring window does not repeat startup greeting");
                form.SelectTea(1); Assert(form.InteractionCount == 2 && form.LastInteractionScene == 1, "changing tea plays greeting");
                form.SelectTea(1); Assert(form.InteractionCount == 2, "same tea selection does not duplicate greeting");
                form.ToggleTimer(); Assert(form.InteractionCount == 3 && form.LastInteractionScene == 2 && form.Model.State == TimerState.Running, "start plays brewing interaction and starts countdown");
                now += 300; form.Pump(); form.CapturePreview(Path.Combine(directory, "preview-brewing-interaction.png"));
                form.SelectTea(0); Assert(form.InteractionCount == 3 && prefs.Selected == 1, "tea cannot change during brew");
                form.ToggleTimer(); Assert(!form.MainInteractionActive, "pause clears brewing interaction");
                long remaining = form.Model.RemainingMilliseconds; now += 20000; form.ToggleTimer();
                Assert(form.InteractionCount == 3 && form.Model.RemainingMilliseconds == remaining, "resume does not replay or change countdown");
                now += remaining; form.Pump(); Assert(form.HasVisibleAlert && !form.MainInteractionActive, "completion retains original reminder and clears interaction");
                form.ToggleTimer(); Assert(form.InteractionCount == 4 && form.LastInteractionScene == 2 && form.BrewRound == 2 && form.Model.DurationSeconds == 8, "repeat shares brewing interaction and preserves round increment");
                form.Hide(); Assert(!form.MainInteractionActive && !form.MainHasInteractionClip, "hiding releases interaction animation");
                form.Show(); Application.DoEvents(); Assert(form.InteractionCount == 4, "restore during brew does not replay interaction");
                form.ResetTimer(); prefs.Greetings[2].Enabled = prefs.Brewing[2].Enabled = false;
                form.SelectTea(0); form.ToggleTimer(); Assert(form.InteractionCount == 4 && !form.MainInteractionActive, "per-role interaction switches suppress playback");
                form.ResetTimer();
                Preferences hidden = new Preferences { MascotKind = 2, ShowMascot = false, AnimateMascot = false };
                hidden.Times[0] = hidden.Times[1] = 6; form.ApplySettings(hidden); form.SelectTea(1);
                Assert(form.MainInteractionActive && !form.MainHasInteractionClip, "hidden character respects global setting during interaction");
                form.ResetTimer(); hidden.ShowMascot = true; hidden.AnimateMascot = false; form.ApplySettings(hidden); form.ToggleTimer();
                Assert(form.MainInteractionActive && !form.MainHasInteractionClip, "disabled animation keeps static character during interaction");
                form.ShutdownTest();
            }
            using (ReminderSpeech today = new ReminderSpeech(6)) Assert(today.IsLoaded, "today voice loads");
            using (ReminderSpeech brewing = new ReminderSpeech(7)) Assert(brewing.IsLoaded, "brewing voice loads");
            string sourceDir = Path.Combine(directory, "interaction-source"); Directory.CreateDirectory(sourceDir);
            string gif = Path.Combine(sourceDir, "greeting.gif"), wave = Path.Combine(sourceDir, "brewing.wav");
            using (Stream source = System.Reflection.Assembly.GetExecutingAssembly().GetManifestResourceStream("TeaTimer.ExtraDragon"))
            using (FileStream target = File.Create(gif)) source.CopyTo(target);
            using (Stream source = System.Reflection.Assembly.GetExecutingAssembly().GetManifestResourceStream("TeaTimer.VoiceBrewing"))
            using (FileStream target = File.Create(wave)) source.CopyTo(target);
            Preferences original = new Preferences(); ReminderProfile[] greetings, brews;
            using (MediaSettingsForm editor = new MediaSettingsForm(original.Reminders, 2, original.Greetings, original.Brewing))
            {
                editor.Show(); editor.ChooseScene(1); Application.DoEvents();
                Assert(editor.SetCustomFile(gif, true) && editor.SetCustomFile(wave, false), "greeting supports both custom imports");
                editor.CapturePreview(Path.Combine(directory, "preview-greeting-custom.png"));
                editor.ChooseScene(2); Assert(editor.SetCustomFile(gif, true) && editor.SetCustomFile(wave, false), "brewing supports both custom imports");
                editor.ChooseRole(1); editor.EnableInteraction(false); editor.ChooseRole(2);
                Assert(editor.ApplyChanges() && original.Greetings[2].AnimationFile == "" && original.Brewing[1].Enabled, "scene edits remain isolated");
                greetings = editor.GreetingResult; brews = editor.BrewingResult;
                Assert(editor.Result[2].AnimationStyle == 0 && greetings[2].VoiceStyle == MediaCatalog.CustomVoice && !brews[1].Enabled, "scenes and roles have independent choices");
                editor.Close();
            }
            string storage = Path.Combine(directory, "interaction-media"); Preferences saved;
            using (SettingsForm settings = new SettingsForm(original, false, storage))
            { settings.ChooseInteractionProfiles(greetings, brews); Assert(settings.SaveChanges(), "scene imports save to managed storage"); saved = settings.Result; }
            string config = Path.Combine(directory, "interaction-settings.xml"); Assert(saved.Save(config), "scene settings save");
            Preferences restored = Preferences.Load(config);
            Assert(restored.Greetings[2].AnimationFile.StartsWith(storage) && restored.Brewing[2].VoiceFile.StartsWith(storage)
                && !restored.Brewing[1].Enabled && restored.Reminders[2].VoiceStyle == 0, "scene selections, switches and old reminders restore independently");
            File.Delete(gif); File.Delete(wave);
            using (ReminderClip clip = new ReminderClip(2, restored.Greetings[2].AnimationStyle, restored.Greetings[2].AnimationFile, 1))
                Assert(clip.FrameCount >= 20, "greeting copy survives original removal");
            using (ReminderSpeech speech = new ReminderSpeech(restored.Brewing[2].VoiceStyle, restored.Brewing[2].VoiceFile, 2))
                Assert(speech.IsLoaded, "brewing copy survives original removal");
            restored.MascotKind = 2;
            using (TeaForm custom = new TeaForm(restored, delegate { return now; }, true) { TestInteractions = true })
            {
                custom.Show(); Application.DoEvents(); Assert(custom.MainHasInteractionClip && custom.LastInteractionScene == 1, "saved greeting import plays in main window");
                custom.ToggleTimer(); Assert(custom.MainHasInteractionClip && custom.LastInteractionScene == 2, "saved brewing import plays in main window"); custom.ResetTimer();
                File.Delete(restored.Greetings[2].AnimationFile); File.Delete(restored.Brewing[2].VoiceFile);
                custom.SelectTea(1); Assert(custom.MainHasInteractionClip && custom.LastInteractionScene == 1, "missing imported greeting still animates with built-in fallback");
                custom.ToggleTimer(); Assert(custom.MainHasInteractionClip && custom.Model.State == TimerState.Running, "missing imported brewing cannot stop countdown"); custom.ShutdownTest();
            }
            using (MediaSettingsForm editor = new MediaSettingsForm(restored.Reminders, 2, restored.Greetings, restored.Brewing))
            {
                editor.ChooseScene(1); editor.RestoreDefault(true); editor.RestoreDefault(false);
                editor.ChooseScene(2); editor.RestoreDefault(true); editor.RestoreDefault(false); Assert(editor.ApplyChanges(), "scene defaults restore");
                Assert(editor.GreetingResult[2].AnimationStyle == 1 && editor.GreetingResult[2].VoiceStyle == 6 && editor.BrewingResult[2].VoiceStyle == 7, "restore uses correct scene defaults");
                editor.Show(); editor.ChooseScene(1); Application.DoEvents(); editor.CapturePreview(Path.Combine(directory, "preview-greeting-settings.png"));
                foreach (Control control in editor.Controls) Assert(control.Right <= editor.ClientSize.Width && control.Bottom <= editor.ClientSize.Height, "scene editor fits current DPI"); editor.Close();
            }
            using (ReminderSpeech fallback = new ReminderSpeech(MediaCatalog.CustomVoice, Path.Combine(sourceDir, "missing.wav"), 1))
                Assert(fallback.IsLoaded && MediaCatalog.DefaultProfile(1).VoiceStyle == 6, "missing greeting voice uses scene fallback");
        }
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
                        settings.SetIncrement(0, 15); settings.SetIncrement(1, 10);
                        settings.ChooseVoiceReminder(false);
                        settings.ChooseMascot(1);
                        Assert(settings.SaveChanges() && prefs.Times[0] == 120, "configuration edits are isolated until save");
                        Assert(prefs.RoundIncrements[0] == 0, "increment edits are isolated until save");
                        form.ApplySettings(settings.Result); Assert(form.Model.DurationSeconds == 67 && prefs.Times[1] == 89, "apply per-tea times");
                        Assert(prefs.RoundIncrements[0] == 15 && prefs.RoundIncrements[1] == 10, "apply per-tea increments");
                        Assert(!prefs.VoiceReminder, "voice reminder setting applies");
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
                    Assert(restored.Times[0] == 40 && restored.Times[1] == 89 && restored.Selected == 1 && restored.WindowWidth == prefs.WindowWidth && restored.WindowHeight == prefs.WindowHeight && restored.ShowMascot == prefs.ShowMascot && restored.AnimateMascot == prefs.AnimateMascot && restored.MascotKind == 2 && restored.VoiceReminder == prefs.VoiceReminder, "configuration and window-size persistence");
                    using (TeaForm reopened = new TeaForm(restored, delegate { return now; }, true))
                    {
                        reopened.Show(); Application.DoEvents();
                        Assert(reopened.Model.DurationSeconds == 89 && reopened.ClientSize == form.ClientSize, "reopening restores tea time and window size"); reopened.ShutdownTest();
                    }
                    form.ShutdownTest();
                }
                Preferences roundPrefs = new Preferences { MascotKind = 2 };
                roundPrefs.Times[0] = 30; roundPrefs.RoundIncrements[0] = 10;
                roundPrefs.Times[1] = 2; roundPrefs.RoundIncrements[1] = 5;
                using (TeaForm rounds = new TeaForm(roundPrefs, delegate { return now; }, true))
                {
                    rounds.Show(); Application.DoEvents();
                    Assert(rounds.BrewRound == 1 && rounds.Model.DurationSeconds == 30, "first round uses base duration");
                    rounds.ToggleTimer(); now += 30000; rounds.Pump(); rounds.ToggleTimer();
                    Assert(rounds.BrewRound == 2 && rounds.Model.DurationSeconds == 40, "second round adds increment");
                    now += 5000; rounds.ToggleTimer(); long held = rounds.Model.RemainingMilliseconds;
                    now += 100000; rounds.ToggleTimer();
                    Assert(rounds.BrewRound == 2 && rounds.Model.RemainingMilliseconds == held, "pause and resume do not add a round");
                    now += held; rounds.Pump(); rounds.ToggleTimer();
                    Assert(rounds.BrewRound == 3 && rounds.Model.DurationSeconds == 50, "third round accumulates increment");
                    rounds.CapturePreview(Path.Combine(directory, "preview-round-three.png"));
                    rounds.ResetTimer(); Assert(rounds.BrewRound == 1 && rounds.Model.DurationSeconds == 30, "reset restores first round");
                    rounds.SelectTea(1); Assert(rounds.BrewRound == 1 && rounds.Model.DurationSeconds == 2, "switch tea restores its base duration");
                    rounds.ToggleTimer(); now += 2000; rounds.Pump(); rounds.ToggleTimer();
                    Assert(rounds.BrewRound == 2 && rounds.Model.DurationSeconds == 7, "each tea uses its own increment"); rounds.ResetTimer();
                    rounds.ToggleTimer();
                    Preferences updated = new Preferences(); updated.Times = (int[])roundPrefs.Times.Clone(); updated.RoundIncrements = (int[])roundPrefs.RoundIncrements.Clone();
                    updated.Times[1] = 3; updated.RoundIncrements[1] = 7; rounds.ApplySettings(updated);
                    Assert(rounds.BrewRound == 1 && rounds.Model.DurationSeconds == 2 && rounds.Model.RemainingSeconds == 2, "changing increments preserves active round deadline");
                    now += 2000; rounds.Pump(); updated.RoundIncrements[1] = 8; rounds.ApplySettings(updated);
                    Assert(rounds.Model.State == TimerState.Finished && rounds.BrewRound == 1, "settings after completion preserve repeat sequence");
                    rounds.ToggleTimer(); Assert(rounds.BrewRound == 2 && rounds.Model.DurationSeconds == 11, "next round uses updated base and increment");
                    rounds.ResetTimer(); Assert(rounds.Model.DurationSeconds == 3 && rounds.BrewRound == 1, "reset uses new base duration");
                    updated.Times[1] = 5990; updated.RoundIncrements[1] = 20; rounds.ApplySettings(updated);
                    rounds.ToggleTimer(); now += 5990000; rounds.Pump(); rounds.ToggleTimer();
                    Assert(rounds.Model.DurationSeconds == 5999 && rounds.BrewRound == 2, "increased round clamps at maximum duration");
                    now += 5999000; rounds.Pump(); rounds.ToggleTimer();
                    Assert(rounds.Model.DurationSeconds == 5999 && rounds.BrewRound == 3, "subsequent rounds stay at maximum duration");
                    string roundPath = Path.Combine(directory, "round-settings.xml"); Assert(roundPrefs.Save(roundPath), "save per-tea increments");
                    Preferences savedRounds = Preferences.Load(roundPath);
                    Assert(savedRounds.RoundIncrements[0] == 10 && savedRounds.RoundIncrements[1] == 20, "per-tea increment persistence");
                    rounds.ShutdownTest();
                    using (TeaForm reopenedRounds = new TeaForm(savedRounds, delegate { return now; }, true))
                    {
                        Assert(reopenedRounds.BrewRound == 1 && reopenedRounds.Model.DurationSeconds == 5990, "reopening starts a new first round with saved increments");
                        reopenedRounds.Show(); reopenedRounds.Size = reopenedRounds.MinimumSize;
                        Preferences hiddenRounds = new Preferences { Times = (int[])savedRounds.Times.Clone(), RoundIncrements = (int[])savedRounds.RoundIncrements.Clone(), ShowMascot = false };
                        reopenedRounds.ApplySettings(hiddenRounds); Application.DoEvents();
                        reopenedRounds.CapturePreview(Path.Combine(directory, "preview-round-hidden-minimum.png")); reopenedRounds.ShutdownTest();
                    }
                }
                string legacyPath = Path.Combine(directory, "legacy-settings.xml");
                File.WriteAllText(legacyPath, "<Preferences><Selected>0</Selected><Times><int>60</int><int>180</int><int>150</int><int>120</int><int>240</int><int>240</int></Times><Sound>true</Sound><OnTop>true</OnTop></Preferences>");
                Preferences legacy = Preferences.Load(legacyPath);
                Assert(legacy.Times[0] == 60 && legacy.OnTop && legacy.WindowWidth == 280 && legacy.WindowHeight == 236, "old configuration migrates without losing times");
                Assert(legacy.RoundIncrements.Length == 6 && legacy.RoundIncrements[0] == 0, "legacy settings default to no round increase");
                Assert(legacy.VoiceReminder, "legacy settings enable voice option");
                Assert(legacy.Reminders.Length == 3 && legacy.Reminders[2].AnimationStyle == 0 && legacy.Reminders[2].VoiceStyle == 0, "legacy settings keep original media defaults");
                Assert(legacy.Greetings.Length == 3 && legacy.Greetings[2].VoiceStyle == 6 && legacy.Brewing[2].VoiceStyle == 7, "legacy settings acquire appropriate interaction defaults");
                using (ReminderSpeech speech = new ReminderSpeech()) Assert(speech.IsLoaded, "embedded local TTS WAV loads for playback");
                for (int role = 0; role < MascotCatalog.Names.Length; role++)
                {
                    using (ReminderClip extra = new ReminderClip(role, 1))
                        Assert(extra.FrameCount >= 20 && extra.DurationMilliseconds >= 2000 && extra.DurationMilliseconds <= 3000, "each character has an additional local animation");
                    using (ReminderClip clip = new ReminderClip(role))
                    {
                        Assert(clip.FrameCount >= 20 && clip.DurationMilliseconds >= 2000 && clip.DurationMilliseconds <= 3000, "each character has a 2-3 second animated clip");
                        Assert(clip.FrameAt(0) == 0 && clip.FrameAt(clip.DurationMilliseconds + 1) == clip.FrameCount - 1, "short animation holds final frame after completion");
                        clip.SelectFrame(clip.FrameCount / 2);
                        using (Bitmap frame = new Bitmap(clip.Image)) frame.Save(Path.Combine(directory, "clip-middle-" + role + ".png"));
                    }
                    using (AlertForm alert = new AlertForm("花草茶", SystemIcons.Information, Tea.All[5].Accent, role))
                    {
                        Assert(alert.Mascot.HasVideoClip, "reminder loads selected video clip");
                        alert.Show(); Application.DoEvents();
                        foreach (Control child in alert.Controls)
                            Assert(child.Right <= alert.ClientSize.Width && child.Bottom <= alert.ClientSize.Height, "reminder layout fits at current DPI");
                        if (role == 0)
                        {
                            DateTime until = DateTime.UtcNow.AddMilliseconds(3000);
                            while (alert.Mascot.AnimationRunning && DateTime.UtcNow < until)
                            {
                                Application.DoEvents(); System.Threading.Thread.Sleep(20);
                            }
                            Assert(!alert.Mascot.AnimationRunning, "reminder animation stops after one playback");
                        }
                        Style.CaptureForm(alert, Path.Combine(directory, "preview-alert-" + role + ".png")); alert.Close();
                    }
                }
                for (int voice = 0; voice < 4; voice++) using (ReminderSpeech speech = new ReminderSpeech(voice))
                    Assert(speech.IsLoaded, "all four embedded WAV voices load");
                string mediaSource = Path.Combine(directory, "custom-source"); Directory.CreateDirectory(mediaSource);
                string customGif = Path.Combine(mediaSource, "my-animation.gif"), customWave = Path.Combine(mediaSource, "my-voice.wav");
                using (Stream source = System.Reflection.Assembly.GetExecutingAssembly().GetManifestResourceStream("TeaTimer.ExtraDragon"))
                using (FileStream target = File.Create(customGif)) source.CopyTo(target);
                using (Stream source = System.Reflection.Assembly.GetExecutingAssembly().GetManifestResourceStream("TeaTimer.VoiceSoft"))
                using (FileStream target = File.Create(customWave)) source.CopyTo(target);
                Preferences mediaPrefs = new Preferences(); ReminderProfile[] choices;
                using (MediaSettingsForm editor = new MediaSettingsForm(mediaPrefs.Reminders, 2))
                {
                    editor.Show(); Application.DoEvents();
                    Assert(editor.SetCustomFile(customGif, true) && editor.SetCustomFile(customWave, false), "valid GIF and WAV can be selected");
                    Assert(mediaPrefs.Reminders[2].AnimationFile == "" && mediaPrefs.Reminders[2].VoiceFile == "", "media edits remain isolated until save");
                    editor.ChooseRole(0); editor.ChooseAnimation(2); editor.ChooseVoice(4);
                    editor.ChooseRole(1); editor.ChooseAnimation(1); editor.ChooseVoice(1);
                    editor.ChooseRole(2); editor.CapturePreview(Path.Combine(directory, "preview-media-custom.png"));
                    Assert(editor.ApplyChanges(), "media choices apply for all characters"); choices = editor.Result; editor.Close();
                }
                string mediaDirectory = Path.Combine(directory, "imported-media");
                using (SettingsForm settings = new SettingsForm(mediaPrefs, false, mediaDirectory))
                {
                    settings.ChooseReminderProfiles(choices); Assert(settings.SaveChanges(), "save imports custom media");
                    Assert(mediaPrefs.Reminders[2].AnimationFile == "", "saving staged settings does not mutate original preferences");
                    mediaPrefs = settings.Result;
                }
                ReminderProfile imported = mediaPrefs.Reminders[2];
                Assert(imported.AnimationFile.StartsWith(mediaDirectory) && imported.VoiceFile.StartsWith(mediaDirectory), "custom media is copied to managed storage");
                File.Delete(customGif); File.Delete(customWave);
                using (ReminderClip clip = new ReminderClip(2, imported.AnimationStyle, imported.AnimationFile))
                    Assert(clip.FrameCount >= 20, "imported animation survives removal of original file");
                using (ReminderSpeech speech = new ReminderSpeech(imported.VoiceStyle, imported.VoiceFile))
                    Assert(speech.IsLoaded, "imported voice survives removal of original file");
                string mediaSettingsPath = Path.Combine(directory, "media-settings.xml"); Assert(mediaPrefs.Save(mediaSettingsPath), "media settings serialize");
                Preferences restoredMedia = Preferences.Load(mediaSettingsPath);
                Assert(restoredMedia.Reminders[0].AnimationStyle == 2 && restoredMedia.Reminders[0].VoiceStyle == 4
                    && restoredMedia.Reminders[1].VoiceStyle == 1 && restoredMedia.Reminders[2].AnimationFile == imported.AnimationFile
                    && restoredMedia.Reminders[2].VoiceFile == imported.VoiceFile, "each character restores its own media choices");
                string invalidGif = Path.Combine(mediaSource, "broken.gif"), invalidWave = Path.Combine(mediaSource, "broken.wav");
                File.WriteAllText(invalidGif, "invalid image"); File.WriteAllText(invalidWave, "invalid audio");
                using (MediaSettingsForm editor = new MediaSettingsForm(restoredMedia.Reminders, 2))
                {
                    Assert(!editor.SetCustomFile(invalidGif, true) && !editor.SetCustomFile(invalidWave, false), "invalid imports are rejected");
                    editor.RestoreDefault(true); editor.RestoreDefault(false); Assert(editor.ApplyChanges(), "restore built-in media choices");
                    Assert(editor.Result[2].AnimationStyle == 0 && editor.Result[2].AnimationFile == "" && editor.Result[2].VoiceFile == "", "restore default clears replacements");
                    Assert(restoredMedia.Reminders[2].AnimationFile == imported.AnimationFile, "cancelled editor cannot change saved profile");
                }
                using (ReminderClip fallback = new ReminderClip(2, MediaCatalog.CustomAnimation, invalidGif))
                    Assert(fallback.FrameCount >= 20, "broken custom animation falls back to embedded clip");
                using (ReminderSpeech fallback = new ReminderSpeech(MediaCatalog.CustomVoice, invalidWave))
                    Assert(fallback.IsLoaded, "broken custom voice falls back to embedded WAV");
                File.Delete(imported.AnimationFile); File.Delete(imported.VoiceFile);
                using (AlertForm fallbackAlert = new AlertForm("红茶", SystemIcons.Information, Tea.All[1].Accent, 2, true, true, false, imported))
                {
                    fallbackAlert.Show(); Application.DoEvents(); Assert(fallbackAlert.Mascot.HasVideoClip, "missing custom file cannot break reminder"); fallbackAlert.Close();
                }
                using (ReminderSpeech fallback = new ReminderSpeech(MediaCatalog.CustomVoice, imported.VoiceFile))
                    Assert(fallback.IsLoaded, "missing voice file falls back to embedded WAV");
                using (MediaSettingsForm editor = new MediaSettingsForm(new Preferences().Reminders, 0))
                {
                    editor.Show(); Application.DoEvents(); editor.CapturePreview(Path.Combine(directory, "preview-media.png"));
                    foreach (Control child in editor.Controls) Assert(child.Right <= editor.ClientSize.Width && child.Bottom <= editor.ClientSize.Height, "media editor fits current DPI");
                    editor.Close();
                }
                RunInteractionTests(directory);
                File.WriteAllText(report, "PASS: timer, round increments, legacy settings, six animations, six voices, startup once, tea-change greeting, start/repeat interactions, pause/resume without replay, completion reminder, hidden/static characters, animation cleanup, scene and role switches, isolated scene edits, scene persistence, GIF/WAV import, managed copies, original-file removal, invalid-file rejection, missing/corrupt-file fallback, scene defaults, DPI layouts and renders.");
            }
            catch (Exception e) { File.WriteAllText(report, "FAIL: " + e); Environment.ExitCode = 1; }
        }
    }
}
