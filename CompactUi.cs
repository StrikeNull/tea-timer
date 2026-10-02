using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Media;
using System.Reflection;
using System.Windows.Forms;

namespace TeaTimer
{
    internal static class MascotCatalog
    {
        public static readonly string[] Names = { "蓝鲸女仆", "GPT 茶娘（原创）", "GPT 龙娘" };
        public static readonly string[] Resources = { "TeaTimer.TeaMaid", "TeaTimer.GptMaid", "TeaTimer.GptDragon" };
    }

    internal sealed class TeaPickerButton : Button
    {
        public Color Accent;
        public TeaPickerButton()
        {
            FlatStyle = FlatStyle.Flat; FlatAppearance.BorderSize = 0; Cursor = Cursors.Hand;
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true);
        }
        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics; g.SmoothingMode = SmoothingMode.AntiAlias; float scale = g.DpiY / 96f;
            using (GraphicsPath path = Style.Rounded(new RectangleF(1, 1, Width - 3, Height - 3), 8 * scale))
            using (Brush brush = new SolidBrush(Color.White)) g.FillPath(brush, path);
            Color ink = Enabled ? Accent : Style.Muted;
            using (Brush brush = new SolidBrush(ink)) g.FillEllipse(brush, 12 * scale, (Height - 6 * scale) / 2, 6 * scale, 6 * scale);
            Style.Text(g, Text, 10 * scale, FontStyle.Bold, ink, new RectangleF(27 * scale, 0, Width - 48 * scale, Height), StringAlignment.Near);
            using (Pen pen = new Pen(ink, 1.4f * scale))
            {
                float x = Width - 15 * scale, y = Height / 2;
                g.DrawLines(pen, new PointF[] { new PointF(x - 3 * scale, y - 1.5f * scale), new PointF(x, y + 1.5f * scale), new PointF(x + 3 * scale, y - 1.5f * scale) });
            }
            if (Focused) ControlPaint.DrawFocusRectangle(g, new Rectangle(5, 5, Width - 10, Height - 10));
        }
    }

    internal sealed class MascotArtwork : IDisposable
    {
        private readonly Bitmap[] mascots = new Bitmap[MascotCatalog.Names.Length];
        public MascotArtwork()
        {
            string[] resources = MascotCatalog.Resources;
            for (int i = 0; i < resources.Length; i++)
                using (Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(resources[i]))
                using (Image source = Image.FromStream(stream)) mascots[i] = new Bitmap(source);
        }
        public void Dispose() { foreach (Bitmap mascot in mascots) mascot.Dispose(); }
        public void Draw(Graphics g, RectangleF bounds, float scale, int kind, TimerState state, Color accent, bool animate, double phase)
        {
            float size = bounds.Height;
            bool moving = animate && state != TimerState.Paused;
            float bob = moving ? (float)Math.Sin(phase * 2.4) * 2 * scale : 0;
            float tilt = moving ? (float)Math.Sin(phase * 1.8) * (state == TimerState.Finished ? 2 : .7f) : 0;
            int frame = state == TimerState.Running ? 1 : state == TimerState.Finished ? 2 : 0;
            Bitmap mascot = mascots[Math.Max(0, Math.Min(mascots.Length - 1, kind))];
            float drawWidth = size * mascot.Width / 3f / mascot.Height;
            float x = bounds.X, y = bounds.Y + bob;
            GraphicsState saved = g.Save();
            g.TranslateTransform(x + size / 2, y + size / 2); g.RotateTransform(tilt);
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            g.DrawImage(mascot, new RectangleF(-drawWidth / 2, -size / 2, drawWidth, size),
                new RectangleF(frame * mascot.Width / 3f, 0, mascot.Width / 3f, mascot.Height), GraphicsUnit.Pixel);
            g.Restore(saved);
            if (state == TimerState.Running)
            {
                for (int i = 0; i < 3; i++)
                {
                    float rise = moving ? (float)((phase * .50 + i * .33) % 1) : .45f;
                    int alpha = (int)((1 - rise) * 120);
                    float steamX = x + size * (.40f + i * .055f), steamY = y + size * .53f - rise * size * .20f;
                    using (Pen pen = new Pen(Color.FromArgb(alpha, accent), 1.2f * scale))
                        g.DrawBezier(pen, steamX, steamY, steamX - 3 * scale, steamY - 3 * scale, steamX + 3 * scale, steamY - 6 * scale, steamX, steamY - 9 * scale);
                }
            }
            if (state == TimerState.Finished)
            {
                float pulse = moving ? .7f + .3f * (float)Math.Sin(phase * 3) : 1;
                DrawSparkle(g, accent, x + size * .12f, y + size * .22f, 4 * scale * pulse);
                DrawSparkle(g, accent, x + size * .90f, y + size * .09f, 3 * scale * pulse);
            }
        }
        private void DrawSparkle(Graphics g, Color color, float x, float y, float radius)
        {
            using (Brush brush = new SolidBrush(color))
                g.FillPolygon(brush, new PointF[] { new PointF(x, y - radius), new PointF(x + radius * .35f, y - radius * .35f), new PointF(x + radius, y), new PointF(x + radius * .35f, y + radius * .35f), new PointF(x, y + radius), new PointF(x - radius * .35f, y + radius * .35f), new PointF(x - radius, y), new PointF(x - radius * .35f, y - radius * .35f) });
        }
    }

    internal sealed class ReminderMascot : Control
    {
        private readonly MascotArtwork artwork = new MascotArtwork();
        private readonly System.Windows.Forms.Timer animation = new System.Windows.Forms.Timer { Interval = 50 };
        private readonly ReminderClip clip;
        private readonly ulong started;
        internal readonly int Kind;
        private readonly Color accent;
        internal bool AnimationRunning { get { return animation.Enabled; } }
        internal bool HasVideoClip { get { return clip != null; } }
        internal ReminderMascot(int kind, Color color, bool animate, ReminderProfile profile = null, int scene = 0)
        {
            Kind = kind; accent = color;
            started = Native.GetTickCount64();
            if (animate) clip = new ReminderClip(kind, profile == null ? 0 : profile.AnimationStyle, profile == null ? null : profile.AnimationFile, scene);
            SetStyle(ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint, true);
            animation.Tick += delegate
            {
                if (clip != null && Native.GetTickCount64() - started >= (ulong)clip.DurationMilliseconds) animation.Stop();
                Invalidate();
            };
            animation.Enabled = animate;
            AccessibleName = MascotCatalog.Names[Math.Max(0, Math.Min(MascotCatalog.Names.Length - 1, kind))] + "提醒你喝茶";
            TabStop = false;
        }
        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics; g.SmoothingMode = SmoothingMode.AntiAlias;
            float scale = g.DpiY / 96f;
            float size = Math.Min(Width, Height) - 4 * scale;
            if (clip != null)
            {
                clip.SelectFrame(clip.FrameAt((long)(Native.GetTickCount64() - started)));
                g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                float factor = Math.Min(size / clip.Image.Width, size / clip.Image.Height);
                float width = clip.Image.Width * factor, height = clip.Image.Height * factor;
                g.DrawImage(clip.Image, new RectangleF((Width - width) / 2, (Height - height) / 2, width, height));
            }
            else artwork.Draw(g, new RectangleF((Width - size) / 2, (Height - size) / 2, size, size),
                scale, Kind, TimerState.Finished, accent, false, 0);
        }
        protected override void Dispose(bool disposing)
        {
            if (disposing) { animation.Stop(); animation.Dispose(); if (clip != null) clip.Dispose(); artwork.Dispose(); }
            base.Dispose(disposing);
        }
    }

    internal sealed class ClockPanel : Control
    {
        public Countdown Countdown;
        public Tea Tea;
        public bool ShowMascot = true, AnimateMascot = true;
        public int MascotKind;
        public int BrewRound = 1;
        public bool ShowRound;
        public Func<long> Clock;
        private readonly MascotArtwork artwork = new MascotArtwork();
        private ReminderClip interactionClip;
        private string interactionCaption;
        private long interactionStarted;
        internal bool InteractionActive { get { return interactionCaption != null; } }
        internal bool HasInteractionClip { get { return interactionClip != null; } }
        public ClockPanel()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint, true);
        }
        internal void PlayInteraction(ReminderProfile profile, string caption, int scene)
        {
            StopInteraction(); interactionStarted = Clock(); interactionCaption = caption;
            if (ShowMascot && AnimateMascot) interactionClip = new ReminderClip(MascotKind, profile.AnimationStyle, profile.AnimationFile, scene);
            Invalidate();
        }
        internal void AdvanceInteraction()
        {
            if (InteractionActive && Clock() - interactionStarted >= (interactionClip == null ? 2400 : interactionClip.DurationMilliseconds)) StopInteraction();
        }
        internal void StopInteraction()
        {
            if (interactionClip != null) { interactionClip.Dispose(); interactionClip = null; }
            interactionCaption = null; Invalidate();
        }
        protected override void Dispose(bool disposing) { if (disposing) { StopInteraction(); artwork.Dispose(); } base.Dispose(disposing); }
        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics; g.SmoothingMode = SmoothingMode.AntiAlias;
            float scale = g.DpiY / 96f;
            using (GraphicsPath path = Style.Rounded(new RectangleF(1, 1, Width - 3, Height - 3), 12 * scale))
            using (Brush brush = new SolidBrush(Color.White)) g.FillPath(brush, path);
            int seconds = Countdown.RemainingSeconds;
            string time = (seconds / 60).ToString("00") + ":" + (seconds % 60).ToString("00");
            float mascotSize = Math.Min(Height - 12 * scale, Math.Min(Width * .55f, Width - 112 * scale));
            float textWidth = ShowMascot ? Width - mascotSize - 8 * scale : Width;
            float fontSize = Math.Min(78 * scale, Math.Min(textWidth * .26f, Height * (ShowRound ? .34f : .40f)));
            if (ShowRound) Style.Text(g, "第 " + BrewRound + " 轮", 8 * scale, FontStyle.Regular, Tea.Accent,
                new RectangleF(0, 3 * scale, textWidth, 18 * scale), StringAlignment.Center);
            using (Font font = new Font("Segoe UI", fontSize, FontStyle.Regular, GraphicsUnit.Pixel))
            using (Brush brush = new SolidBrush(Style.Blend(Tea.Accent, Color.Black, .55f))) using (StringFormat sf = new StringFormat())
            {
                sf.Alignment = StringAlignment.Center; sf.LineAlignment = StringAlignment.Center;
                g.DrawString(time, font, brush, new RectangleF(0, Height * (ShowRound ? .12f : .04f), textWidth, Height * (ShowRound ? .50f : .62f)), sf);
            }
            string state = Countdown.State == TimerState.Running ? "正在泡茶" : Countdown.State == TimerState.Paused ? "已暂停" : Countdown.State == TimerState.Finished ? "茶泡好了，请出汤" : "点击开始泡茶";
            if (InteractionActive) state = interactionCaption;
            Style.Text(g, state, 9 * scale, FontStyle.Regular, Tea.Accent,
                new RectangleF(0, Height * .66f, textWidth, 22 * scale), StringAlignment.Center);
            if (ShowMascot)
            {
                RectangleF bounds = new RectangleF(Width - mascotSize - 4 * scale, (Height - mascotSize) / 2 - 2 * scale, mascotSize, mascotSize);
                if (interactionClip != null)
                {
                    interactionClip.SelectFrame(interactionClip.FrameAt(Clock() - interactionStarted));
                    float factor = Math.Min(bounds.Width / interactionClip.Image.Width, bounds.Height / interactionClip.Image.Height);
                    float w = interactionClip.Image.Width * factor, h = interactionClip.Image.Height * factor;
                    g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                    g.DrawImage(interactionClip.Image, new RectangleF(bounds.X + (bounds.Width - w) / 2, bounds.Y + (bounds.Height - h) / 2, w, h));
                }
                else artwork.Draw(g, bounds, scale, MascotKind, Countdown.State, Tea.Accent, AnimateMascot, Clock == null ? 0 : Clock() / 1000.0);
            }
            float progress = Math.Max(0, Math.Min(1, 1f - (float)Countdown.RemainingMilliseconds / (Countdown.DurationSeconds * 1000f)));
            RectangleF track = new RectangleF(14 * scale, Height - 12 * scale, textWidth - 28 * scale, 3 * scale);
            using (Brush brush = new SolidBrush(ColorTranslator.FromHtml("#E9EEE5"))) g.FillRectangle(brush, track);
            if (progress > 0) using (Brush brush = new SolidBrush(Tea.Accent))
                g.FillRectangle(brush, track.X, track.Y, track.Width * progress, track.Height);
        }
    }

    internal sealed class TeaForm : Form
    {
        private readonly Preferences preferences;
        private readonly Countdown countdown;
        private readonly TeaPickerButton teaPicker;
        private readonly ContextMenuStrip teaMenu;
        private readonly ClockPanel clockPanel;
        private readonly ActionButton start, reset, configure;
        private readonly NotifyIcon tray;
        private readonly System.Windows.Forms.Timer timer;
        private AlertForm alert;
        private bool exiting;
        private bool shownOnce;
        private ReminderSpeech interactionSpeech;
        private int brewRound = 1;
        private readonly float uiScale;
        internal int CompletionCount { get; private set; }
        internal bool SilentTest, TestNotifications, TestInteractions;
        internal int InteractionCount { get; private set; }
        internal int LastInteractionScene { get; private set; }
        internal bool MainInteractionActive { get { return clockPanel.InteractionActive; } }
        internal bool MainHasInteractionClip { get { return clockPanel.HasInteractionClip; } }
        internal Countdown Model { get { return countdown; } }
        internal int BrewRound { get { return brewRound; } }
        internal AlertForm CurrentAlert { get { return alert; } }
        internal bool HasVisibleAlert { get { return alert != null && alert.Visible && alert.TopMost; } }
        internal TeaForm(Preferences prefs, Func<long> now, bool silentTest = false)
        {
            SuspendLayout(); SilentTest = silentTest; preferences = prefs;
            countdown = new Countdown(now);
            Text = "一盏茶"; Icon = Style.MakeIcon(); BackColor = Style.Background; ForeColor = Style.Ink;
            Font = Style.Font(9, FontStyle.Regular); AutoScaleMode = AutoScaleMode.None;
            FormBorderStyle = FormBorderStyle.Sizable; StartPosition = FormStartPosition.CenterScreen; DoubleBuffered = true;
            using (Graphics g = CreateGraphics()) uiScale = g.DpiY / 96f;
            ClientSize = new Size(Px(preferences.WindowWidth), Px(preferences.WindowHeight));
            MinimumSize = new Size(Px(260) + Width - ClientSize.Width, Px(208) + Height - ClientSize.Height);
            teaPicker = new TeaPickerButton { AccessibleName = "选择茶类" };
            teaMenu = new ContextMenuStrip();
            for (int i = 0; i < Tea.All.Length; i++)
            {
                int index = i; ToolStripMenuItem choice = new ToolStripMenuItem(Tea.All[i].Name) { ForeColor = Tea.All[i].Accent };
                choice.Click += delegate { SelectTea(index); }; teaMenu.Items.Add(choice);
            }
            teaPicker.Click += delegate { OpenTeaPicker(); };
            Controls.Add(teaPicker);
            configure = new ActionButton("配置", false); configure.Click += delegate { ShowConfiguration(); }; Controls.Add(configure);
            clockPanel = new ClockPanel { Countdown = countdown, Tea = Tea.All[preferences.Selected], Clock = now, ShowMascot = preferences.ShowMascot, AnimateMascot = preferences.AnimateMascot, MascotKind = preferences.MascotKind }; Controls.Add(clockPanel);
            start = new ActionButton("开始泡茶", true); start.Font = Style.Font(10, FontStyle.Bold);
            start.Click += delegate { ToggleTimer(); }; Controls.Add(start);
            reset = new ActionButton("重置", false); reset.Click += delegate { ResetTimer(); }; Controls.Add(reset);
            ContextMenuStrip menu = new ContextMenuStrip(); menu.Items.Add("打开一盏茶", null, delegate { RestoreWindow(); });
            menu.Items.Add("配置", null, delegate { RestoreWindow(); ShowConfiguration(); });
            menu.Items.Add("退出（停止计时）", null, delegate { exiting = true; Close(); });
            tray = new NotifyIcon { Icon = Icon, Text = "一盏茶 · 泡茶计时", ContextMenuStrip = menu, Visible = !silentTest };
            tray.DoubleClick += delegate { RestoreWindow(); }; tray.BalloonTipClicked += delegate { RestoreWindow(); };
            Resize += delegate { LayoutControls(); if (WindowState == FormWindowState.Minimized) Hide(); };
            ResizeEnd += delegate { RememberWindowSize(); SavePreferences(); };
            FormClosing += delegate(object sender, FormClosingEventArgs e)
            {
                RememberWindowSize(); SavePreferences();
                if (!exiting && (IsBusy || alert != null))
                {
                    e.Cancel = true; Hide();
                    if (!SilentTest) tray.ShowBalloonTip(3000, "一盏茶仍在后台", "计时和提醒继续。双击托盘茶杯打开，右键可退出。", ToolTipIcon.Info);
                }
            };
            VisibleChanged += delegate { if (!Visible) StopInteraction(); };
            FormClosed += delegate { StopInteraction(); timer.Stop(); timer.Dispose(); tray.Visible = false; tray.Dispose(); menu.Dispose(); teaMenu.Dispose(); DismissAlert(); Icon.Dispose(); };
            timer = new System.Windows.Forms.Timer { Interval = 100 }; timer.Tick += delegate { Pump(); };
            TopMost = preferences.OnTop;
            ResumeLayout(false); LayoutControls(); SelectTea(preferences.Selected); timer.Start();
        }
        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            if (!shownOnce) { shownOnce = true; PlayInteraction(1); }
        }
        private void PlayInteraction(int scene)
        {
            StopInteraction();
            if (!Visible || (SilentTest && !TestInteractions)) return;
            ReminderProfile profile = (scene == 1 ? preferences.Greetings : preferences.Brewing)[preferences.MascotKind];
            if (!profile.Enabled) return;
            InteractionCount++; LastInteractionScene = scene;
            clockPanel.PlayInteraction(profile, scene == 1 ? "今天喝什么茶？" : "开始泡茶啦", scene);
            if (preferences.Sound && preferences.VoiceReminder && !SilentTest)
            { interactionSpeech = new ReminderSpeech(profile.VoiceStyle, profile.VoiceFile, scene); interactionSpeech.Play(); }
        }
        private void StopInteraction()
        {
            clockPanel.StopInteraction();
            if (interactionSpeech != null) { interactionSpeech.Dispose(); interactionSpeech = null; }
        }
        private int Px(float value) { return (int)Math.Round(value * uiScale); }
        private bool IsBusy { get { return countdown.State == TimerState.Running || countdown.State == TimerState.Paused; } }
        private void OpenTeaPicker()
        {
            if (IsBusy || !Visible) return;
            DismissAlert(); PlayInteraction(1);
            for (int i = 0; i < teaMenu.Items.Count; i++) ((ToolStripMenuItem)teaMenu.Items[i]).Checked = i == preferences.Selected;
            teaMenu.Show(teaPicker, new Point(0, teaPicker.Height));
        }
        internal void ClickTeaPicker() { teaPicker.PerformClick(); }
        internal void CloseTeaPicker() { teaMenu.Close(); }
        private void LayoutControls()
        {
            if (clockPanel == null || start == null) return;
            int inset = Px(10), gap = Px(6), configWidth = Px(58), rowHeight = Px(30), buttonHeight = Px(36);
            teaPicker.SetBounds(inset, inset + Px(2), Math.Max(1, ClientSize.Width - inset * 2 - configWidth - gap), rowHeight);
            configure.SetBounds(ClientSize.Width - inset - configWidth, inset, configWidth, rowHeight);
            int bottom = ClientSize.Height - inset - buttonHeight, clockTop = inset + rowHeight + gap;
            clockPanel.SetBounds(inset, clockTop, ClientSize.Width - inset * 2, Math.Max(Px(80), bottom - gap - clockTop));
            int resetWidth = Px(66);
            start.SetBounds(inset, bottom, ClientSize.Width - inset * 2 - resetWidth - gap, buttonHeight);
            reset.SetBounds(ClientSize.Width - inset - resetWidth, bottom, resetWidth, buttonHeight);
        }
        internal void RememberWindowSize()
        {
            if (WindowState != FormWindowState.Normal) return;
            preferences.WindowWidth = Math.Max(260, Math.Min(1280, (int)Math.Round(ClientSize.Width / uiScale)));
            preferences.WindowHeight = Math.Max(208, Math.Min(960, (int)Math.Round(ClientSize.Height / uiScale)));
        }
        private void SavePreferences()
        {
            if (!SilentTest) Text = preferences.Save() ? "一盏茶" : "一盏茶 · 设置未保存";
        }
        internal void SelectTea(int index)
        {
            if (IsBusy || index < 0 || index >= Tea.All.Length) return;
            DismissAlert(); preferences.Selected = index;
            teaPicker.Text = Tea.All[index].Name;
            brewRound = 1; clockPanel.Tea = Tea.All[index]; countdown.Reset(RoundDuration()); UpdateState(); SavePreferences();
            ApplyTheme();
        }
        private void ApplyTheme()
        {
            Color accent = Tea.All[preferences.Selected].Accent;
            BackColor = Style.Blend(accent, Color.White, .075f);
            start.Accent = reset.Accent = configure.Accent = teaPicker.Accent = accent;
            Icon previous = Icon; Icon = Style.MakeIcon(accent); tray.Icon = Icon; previous.Dispose();
            Invalidate(true);
        }
        internal void SetDuration(int duration)
        {
            if (duration < 1 || duration > 5999) throw new ArgumentOutOfRangeException("duration");
            if (IsBusy) return;
            DismissAlert(); brewRound = 1; preferences.Times[preferences.Selected] = duration; countdown.Reset(duration); UpdateState(); SavePreferences();
        }
        private void ShowConfiguration()
        {
            StopInteraction();
            using (SettingsForm settings = new SettingsForm(preferences, IsBusy))
                if (settings.ShowDialog(this) == DialogResult.OK) ApplySettings(settings.Result);
        }
        internal void ApplySettings(Preferences result)
        {
            StopInteraction();
            preferences.Times = (int[])result.Times.Clone(); preferences.Sound = result.Sound; preferences.OnTop = result.OnTop;
            preferences.RoundIncrements = (int[])result.RoundIncrements.Clone();
            preferences.VoiceReminder = result.VoiceReminder;
            preferences.Reminders = MediaCatalog.CopyProfiles(result.Reminders);
            preferences.Greetings = MediaCatalog.CopyProfiles(result.Greetings, 1);
            preferences.Brewing = MediaCatalog.CopyProfiles(result.Brewing, 2);
            preferences.ShowMascot = result.ShowMascot; preferences.AnimateMascot = result.AnimateMascot;
            preferences.MascotKind = result.MascotKind;
            clockPanel.ShowMascot = preferences.ShowMascot; clockPanel.AnimateMascot = preferences.AnimateMascot;
            clockPanel.MascotKind = preferences.MascotKind;
            TopMost = preferences.OnTop;
            // A running or paused brew keeps its original deadline. New durations
            // apply to the next brew, including repeating after completion.
            if (!IsBusy) { DismissAlert(); if (countdown.State == TimerState.Ready) countdown.Reset(RoundDuration()); }
            UpdateState(); SavePreferences();
        }
        internal void ToggleTimer()
        {
            bool fresh = countdown.State == TimerState.Ready || countdown.State == TimerState.Finished;
            if (countdown.State == TimerState.Running) { StopInteraction(); if (countdown.Pause()) Completed(); }
            else
            {
                DismissAlert();
                if (countdown.State == TimerState.Finished && brewRound < int.MaxValue) brewRound++;
                if (countdown.State != TimerState.Paused) countdown.Reset(RoundDuration());
                countdown.Start();
            }
            UpdateState();
            if (fresh) PlayInteraction(2);
        }
        internal void ResetTimer()
        { StopInteraction(); DismissAlert(); brewRound = 1; countdown.Reset(RoundDuration()); UpdateState(); }
        private int RoundDuration()
        { return (int)Math.Min(5999L, preferences.Times[preferences.Selected] + (long)(brewRound - 1) * preferences.RoundIncrements[preferences.Selected]); }
        internal void Pump()
        {
            clockPanel.AdvanceInteraction(); if (countdown.Tick()) Completed(); clockPanel.Invalidate();
            if (countdown.State == TimerState.Running) tray.Text = "一盏茶 · " + Tea.All[preferences.Selected].Name + " · 剩余 " + countdown.RemainingSeconds + " 秒";
        }
        private void UpdateState()
        {
            teaPicker.Enabled = !IsBusy;
            clockPanel.BrewRound = brewRound; clockPanel.ShowRound = preferences.RoundIncrements[preferences.Selected] > 0;
            start.Text = countdown.State == TimerState.Running ? "暂停" : countdown.State == TimerState.Paused ? "继续" : countdown.State == TimerState.Finished ? "再泡一杯" : "开始泡茶";
            tray.Text = "一盏茶 · " + (countdown.State == TimerState.Paused ? "已暂停" : "泡茶计时"); clockPanel.Invalidate();
        }
        private void Completed()
        {
            StopInteraction(); CompletionCount++; UpdateState(); if (SilentTest && !TestNotifications) return;
            Tea tea = Tea.All[preferences.Selected];
            if (preferences.Sound && !preferences.VoiceReminder && !SilentTest) SystemSounds.Exclamation.Play();
            if (!SilentTest) tray.ShowBalloonTip(6000, "茶泡好了", tea.Name + "已到时间，请及时出汤。", ToolTipIcon.Info);
            alert = new AlertForm(tea.Name, Icon, tea.Accent, preferences.MascotKind, preferences.ShowMascot, preferences.AnimateMascot, preferences.Sound && preferences.VoiceReminder && !SilentTest, preferences.Reminders[preferences.MascotKind]); alert.FormClosed += delegate { alert = null; }; alert.Show();
        }
        private void DismissAlert() { if (alert != null) { alert.Close(); alert = null; } }
        private void RestoreWindow() { Show(); WindowState = FormWindowState.Normal; Activate(); if (alert != null) alert.Activate(); }
        internal void ShutdownTest() { exiting = true; Close(); }
        internal void CapturePreview(string path)
        {
            Style.CaptureForm(this, path);
        }
    }

    internal sealed class SettingsForm : Form
    {
        private readonly Preferences original;
        private readonly NumericUpDown[] minutes = new NumericUpDown[6], seconds = new NumericUpDown[6];
        private readonly NumericUpDown[] increments = new NumericUpDown[6];
        private readonly CheckBox sound, voiceReminder, onTop, showMascot, animateMascot;
        private ReminderSpeech previewSpeech;
        private ReminderProfile[] reminderProfiles;
        private ReminderProfile[] greetingProfiles, brewingProfiles;
        private readonly string mediaDirectory;
        private readonly TeaPickerButton mascotPicker;
        private readonly ContextMenuStrip mascotMenu;
        private int selectedMascot;
        private readonly Label status;
        internal Preferences Result { get; private set; }
        internal SettingsForm(Preferences preferences, bool busy, string mediaDirectory = null)
        {
            SuspendLayout(); original = preferences; reminderProfiles = MediaCatalog.CopyProfiles(preferences.Reminders); this.mediaDirectory = mediaDirectory;
            greetingProfiles = MediaCatalog.CopyProfiles(preferences.Greetings, 1); brewingProfiles = MediaCatalog.CopyProfiles(preferences.Brewing, 2);
            Text = "配置 · 一盏茶"; BackColor = Style.Background; ForeColor = Style.Ink; Font = Style.Font(9, FontStyle.Regular);
            FormBorderStyle = FormBorderStyle.FixedDialog; MaximizeBox = false; MinimizeBox = false;
            ShowInTaskbar = false; StartPosition = FormStartPosition.CenterParent; TopMost = preferences.OnTop;
            AutoScaleMode = AutoScaleMode.None; ClientSize = new Size(454, 508);
            AddLabel("泡茶配置", 20, 14, 330, 36, 16, FontStyle.Bold);
            AddLabel("每种茶的时间分别保存，下次打开仍保留。", 20, 50, 330, 26, 8.5f, FontStyle.Regular);
            AddLabel("茶类", 22, 82, 90, 25, 9, FontStyle.Bold);
            AddLabel("分钟", 126, 82, 70, 25, 9, FontStyle.Bold);
            AddLabel("秒钟", 210, 82, 70, 25, 9, FontStyle.Bold);
            AddLabel("每轮 +秒", 294, 82, 80, 25, 9, FontStyle.Bold);
            for (int i = 0; i < Tea.All.Length; i++)
            {
                int index = i, y = 112 + i * 30;
                AddLabel(Tea.All[i].Name, 22, y, 92, 27, 9, FontStyle.Regular);
                minutes[i] = MakeNumber(126, y, 99, preferences.Times[i] / 60, Tea.All[i].Name + "分钟");
                seconds[i] = MakeNumber(210, y, 59, preferences.Times[i] % 60, Tea.All[i].Name + "秒钟");
                increments[i] = MakeNumber(294, y, 5999, preferences.RoundIncrements[i], Tea.All[i].Name + "每轮增加秒数");
                ActionButton recommended = new ActionButton("推荐", false); recommended.SetBounds(378, y, 56, 27);
                recommended.Font = Style.Font(8, FontStyle.Regular);
                recommended.Click += delegate { SetTime(index, Tea.All[index].Seconds); }; Controls.Add(recommended);
            }
            sound = new CheckBox { Text = "提示音", Checked = preferences.Sound, AutoSize = true, Location = new Point(22, 308) };
            onTop = new CheckBox { Text = "主窗口置顶", Checked = preferences.OnTop, AutoSize = true, Location = new Point(134, 308) };
            Controls.Add(sound); Controls.Add(onTop);
            voiceReminder = new CheckBox { Text = "语音提醒", Checked = preferences.VoiceReminder, Enabled = preferences.Sound, AutoSize = true, Location = new Point(270, 308) };
            sound.CheckedChanged += delegate { voiceReminder.Enabled = sound.Checked; };
            Controls.Add(voiceReminder);
            ActionButton audition = new ActionButton("试听", false) { AccessibleName = "试听泡茶娘提醒语音" }; audition.SetBounds(378, 304, 56, 27);
            audition.Font = Style.Font(8, FontStyle.Regular);
            audition.Click += delegate
            {
                if (previewSpeech != null) previewSpeech.Dispose();
                ReminderProfile profile = reminderProfiles[selectedMascot];
                previewSpeech = new ReminderSpeech(profile.VoiceStyle, profile.VoiceFile); previewSpeech.Play();
            }; Controls.Add(audition);
            Disposed += delegate { if (previewSpeech != null) previewSpeech.Dispose(); };
            showMascot = new CheckBox { Text = "显示泡茶娘", Checked = preferences.ShowMascot, AutoSize = true, Location = new Point(22, 338) };
            animateMascot = new CheckBox { Text = "播放动画", Checked = preferences.AnimateMascot, Enabled = preferences.ShowMascot, AutoSize = true, Location = new Point(180, 338) };
            showMascot.CheckedChanged += delegate { animateMascot.Enabled = showMascot.Checked; };
            Controls.Add(showMascot); Controls.Add(animateMascot);
            AddLabel("形象", 22, 367, 48, 25, 9, FontStyle.Bold);
            mascotPicker = new TeaPickerButton { Accent = Style.Green, AccessibleName = "选择泡茶娘形象" }; mascotPicker.SetBounds(82, 362, 352, 32); Controls.Add(mascotPicker);
            mascotMenu = new ContextMenuStrip();
            for (int i = 0; i < MascotCatalog.Names.Length; i++)
            {
                int index = i; mascotMenu.Items.Add(MascotCatalog.Names[i], null, delegate { ChooseMascot(index); });
            }
            mascotPicker.Click += delegate { mascotMenu.Show(mascotPicker, new Point(0, mascotPicker.Height)); };
            ChooseMascot(preferences.MascotKind); Disposed += delegate { mascotMenu.Dispose(); };
            AddLabel("每轮 +秒：0 不增加，重置 / 换茶回第一轮。", 22, 398, 280, 25, 8, FontStyle.Regular);
            ActionButton media = new ActionButton("动画与语音", false) { AccessibleName = "选择或替换提醒动画和语音" };
            media.Font = Style.Font(8, FontStyle.Regular); media.SetBounds(314, 398, 120, 27);
            media.Click += delegate
            {
                if (previewSpeech != null) { previewSpeech.Dispose(); previewSpeech = null; }
                using (MediaSettingsForm editor = new MediaSettingsForm(reminderProfiles, selectedMascot, greetingProfiles, brewingProfiles))
                    if (editor.ShowDialog(this) == DialogResult.OK)
                    { reminderProfiles = editor.Result; greetingProfiles = editor.GreetingResult; brewingProfiles = editor.BrewingResult; }
            }; Controls.Add(media);
            status = AddLabel(busy ? "正在计时：时间修改在下一次泡茶生效。" : "每轮时间上限：99 分 59 秒。", 22, 422, 414, 27, 8, FontStyle.Regular);
            ActionButton save = new ActionButton("保存", true); save.SetBounds(20, 462, 298, 34);
            save.Click += delegate { if (SaveChanges()) { DialogResult = DialogResult.OK; Close(); } }; Controls.Add(save);
            ActionButton cancel = new ActionButton("取消", false); cancel.SetBounds(330, 462, 104, 34); cancel.DialogResult = DialogResult.Cancel;
            cancel.Click += delegate { Close(); }; Controls.Add(cancel); AcceptButton = save; CancelButton = cancel;
            float scale; using (Graphics graphics = CreateGraphics()) scale = graphics.DpiY / 96f;
            foreach (Control control in Controls)
            {
                Rectangle bounds = control.Bounds;
                if (control.AutoSize) control.Location = new Point((int)Math.Round(bounds.X * scale), (int)Math.Round(bounds.Y * scale));
                else control.SetBounds((int)Math.Round(bounds.X * scale), (int)Math.Round(bounds.Y * scale), (int)Math.Round(bounds.Width * scale), (int)Math.Round(bounds.Height * scale));
            }
            ClientSize = new Size((int)Math.Round(454 * scale), (int)Math.Round(508 * scale));
            ResumeLayout(false);
        }
        private Label AddLabel(string text, int x, int y, int width, int height, float size, FontStyle fontStyle)
        {
            Label label = new Label { Text = text, Font = Style.Font(size, fontStyle), ForeColor = fontStyle == FontStyle.Bold ? Style.Ink : Style.Muted, TextAlign = ContentAlignment.MiddleLeft };
            label.SetBounds(x, y, width, height); Controls.Add(label); return label;
        }
        private NumericUpDown MakeNumber(int x, int y, int maximum, int value, string accessibleName)
        {
            NumericUpDown input = new NumericUpDown { Minimum = 0, Maximum = maximum, Value = value, Font = Style.Font(10, FontStyle.Regular), TextAlign = HorizontalAlignment.Center, AccessibleName = accessibleName };
            input.SetBounds(x, y, 68, 27); Controls.Add(input); return input;
        }
        internal void SetTime(int index, int duration) { minutes[index].Value = duration / 60; seconds[index].Value = duration % 60; }
        internal void SetIncrement(int index, int increment) { increments[index].Value = increment; }
        internal void ChooseVoiceReminder(bool enabled) { voiceReminder.Checked = enabled; }
        internal void ChooseReminderProfiles(ReminderProfile[] profiles) { reminderProfiles = MediaCatalog.CopyProfiles(profiles); }
        internal void ChooseInteractionProfiles(ReminderProfile[] greetings, ReminderProfile[] brewing)
        { greetingProfiles = MediaCatalog.CopyProfiles(greetings, 1); brewingProfiles = MediaCatalog.CopyProfiles(brewing, 2); }
        internal void ChooseMascot(int kind)
        {
            selectedMascot = Math.Max(0, Math.Min(MascotCatalog.Names.Length - 1, kind));
            mascotPicker.Text = MascotCatalog.Names[selectedMascot];
            for (int i = 0; i < mascotMenu.Items.Count; i++) ((ToolStripMenuItem)mascotMenu.Items[i]).Checked = i == selectedMascot;
        }
        internal bool SaveChanges()
        {
            int[] times = new int[Tea.All.Length];
            int[] increases = new int[Tea.All.Length];
            for (int i = 0; i < times.Length; i++)
            {
                times[i] = (int)minutes[i].Value * 60 + (int)seconds[i].Value;
                increases[i] = (int)increments[i].Value;
                if (times[i] == 0) { status.Text = Tea.All[i].Name + "的时间需要大于 0 秒。"; status.ForeColor = Color.Firebrick; minutes[i].Focus(); return false; }
            }
            ReminderProfile[] stored, storedGreetings, storedBrewing;
            try
            {
                stored = MediaLibrary.Store(reminderProfiles, mediaDirectory);
                storedGreetings = MediaLibrary.Store(greetingProfiles, mediaDirectory, 1);
                storedBrewing = MediaLibrary.Store(brewingProfiles, mediaDirectory, 2);
            }
            catch (Exception e) { if (!MediaLibrary.IsMediaError(e)) throw; status.Text = "素材保存失败：" + e.Message; status.ForeColor = Color.Firebrick; return false; }
            Result = new Preferences { Selected = original.Selected, Times = times, RoundIncrements = increases, Sound = sound.Checked, VoiceReminder = voiceReminder.Checked, OnTop = onTop.Checked, ShowMascot = showMascot.Checked, AnimateMascot = animateMascot.Checked, MascotKind = selectedMascot, Reminders = stored, Greetings = storedGreetings, Brewing = storedBrewing, WindowWidth = original.WindowWidth, WindowHeight = original.WindowHeight };
            return true;
        }
        internal void CapturePreview(string path) { Style.CaptureForm(this, path); }
    }
}
