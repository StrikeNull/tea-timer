using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace TeaTimer
{
    internal sealed class MediaSettingsForm : Form
    {
        private readonly ReminderProfile[][] drafts;
        private readonly ComboBox rolePicker, scenePicker, animationPicker, voicePicker;
        private readonly CheckBox enabled;
        private readonly Label animationFile, voiceFile, status;
        private ReminderSpeech audition;
        private bool loading;
        internal ReminderProfile[] Result { get; private set; }
        internal ReminderProfile[] GreetingResult { get; private set; }
        internal ReminderProfile[] BrewingResult { get; private set; }
        private ReminderProfile Current { get { return drafts[scenePicker.SelectedIndex][rolePicker.SelectedIndex]; } }
        internal MediaSettingsForm(ReminderProfile[] profiles, int role, ReminderProfile[] greetings = null, ReminderProfile[] brewing = null)
        {
            drafts = new[] { MediaCatalog.CopyProfiles(profiles), MediaCatalog.CopyProfiles(greetings, 1), MediaCatalog.CopyProfiles(brewing, 2) };
            SuspendLayout(); Text = "动画与语音 · 一盏茶"; BackColor = Style.Background; ForeColor = Style.Ink;
            Font = Style.Font(9, FontStyle.Regular); AutoScaleMode = AutoScaleMode.None;
            FormBorderStyle = FormBorderStyle.FixedDialog; MaximizeBox = false; MinimizeBox = false;
            ShowInTaskbar = false; StartPosition = FormStartPosition.CenterParent; ClientSize = new Size(454, 424);
            LabelAt("动画与语音", 20, 14, 414, 34, 16, true);
            LabelAt("按角色与场景分别记住，搭配喜欢的动作和声音。", 22, 50, 414, 24, 8.5f, false);
            LabelAt("角色", 22, 84, 54, 28, 9, true);
            rolePicker = Picker(82, 82, 352, "选择要配置提醒素材的角色");
            rolePicker.Items.AddRange(MascotCatalog.Names);
            LabelAt("场景", 22, 124, 54, 28, 9, true);
            scenePicker = Picker(82, 122, 216, "选择播放动画和语音的场景"); scenePicker.Items.AddRange(MediaCatalog.SceneNames);
            scenePicker.SelectedIndex = 0;
            enabled = new CheckBox { Text = "启用互动", AutoSize = true, Location = new Point(310, 126) }; Controls.Add(enabled);
            LabelAt("动画", 22, 167, 54, 28, 9, true);
            animationPicker = Picker(82, 164, 216, "动画选择");
            ButtonAt("预览", 310, 164, 56, delegate { PreviewAnimation(); });
            ButtonAt("导入", 378, 164, 56, delegate { PickFile(true); });
            animationFile = LabelAt("", 82, 198, 258, 24, 8, false); animationFile.AutoEllipsis = true;
            ButtonAt("恢复内置", 350, 197, 84, delegate { RestoreDefault(true); }, 25);
            LabelAt("语音", 22, 237, 54, 28, 9, true);
            voicePicker = Picker(82, 234, 216, "语音选择");
            ButtonAt("试听", 310, 234, 56, delegate { PreviewVoice(); });
            ButtonAt("导入", 378, 234, 56, delegate { PickFile(false); });
            voiceFile = LabelAt("", 82, 268, 258, 24, 8, false); voiceFile.AutoEllipsis = true;
            ButtonAt("恢复内置", 350, 267, 84, delegate { RestoreDefault(false); }, 25);
            LabelAt("GIF 动画 / WAV 语音；每份不超过 30 秒、20 MB。", 22, 305, 414, 24, 8, false);
            status = LabelAt("回到配置页点击「保存」后生效，素材会复制到本机。", 22, 332, 414, 36, 8, false);
            ActionButton apply = new ActionButton("应用", true); apply.SetBounds(20, 380, 298, 32);
            apply.Click += delegate { if (ApplyChanges()) { DialogResult = DialogResult.OK; Close(); } };
            Controls.Add(apply); AcceptButton = apply;
            ActionButton cancel = new ActionButton("取消", false) { DialogResult = DialogResult.Cancel }; cancel.SetBounds(330, 380, 104, 32);
            cancel.Click += delegate { Close(); }; Controls.Add(cancel); CancelButton = cancel;
            rolePicker.SelectedIndexChanged += delegate { LoadRole(); };
            scenePicker.SelectedIndexChanged += delegate { LoadRole(); };
            enabled.CheckedChanged += delegate { if (!loading) Current.Enabled = enabled.Checked; };
            animationPicker.SelectedIndexChanged += delegate
            {
                if (!loading) { Current.AnimationStyle = animationPicker.SelectedIndex; UpdateFileLabels(); }
            };
            voicePicker.SelectedIndexChanged += delegate
            {
                if (!loading) { StopVoice(); Current.VoiceStyle = voicePicker.SelectedIndex; UpdateFileLabels(); }
            };
            rolePicker.SelectedIndex = Math.Max(0, Math.Min(2, role));
            float scale; using (Graphics graphics = CreateGraphics()) scale = graphics.DpiY / 96f;
            foreach (Control control in Controls)
            {
                Rectangle b = control.Bounds;
                if (control.AutoSize) control.Location = new Point((int)Math.Round(b.X * scale), (int)Math.Round(b.Y * scale));
                else control.SetBounds((int)Math.Round(b.X * scale), (int)Math.Round(b.Y * scale), (int)Math.Round(b.Width * scale), (int)Math.Round(b.Height * scale));
            }
            ClientSize = new Size((int)Math.Round(454 * scale), (int)Math.Round(424 * scale));
            ResumeLayout(false);
        }
        private Label LabelAt(string text, int x, int y, int width, int height, float size, bool bold)
        {
            Label label = new Label { Text = text, Font = Style.Font(size, bold ? FontStyle.Bold : FontStyle.Regular),
                ForeColor = bold ? Style.Ink : Style.Muted, TextAlign = ContentAlignment.MiddleLeft };
            label.SetBounds(x, y, width, height); Controls.Add(label); return label;
        }
        private ComboBox Picker(int x, int y, int width, string name)
        {
            ComboBox picker = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, FlatStyle = FlatStyle.Flat,
                BackColor = Color.White, ForeColor = Style.Ink, AccessibleName = name };
            picker.SetBounds(x, y, width, 30); Controls.Add(picker); return picker;
        }
        private void ButtonAt(string text, int x, int y, int width, Action action, int height = 28)
        {
            ActionButton button = new ActionButton(text, false); button.Font = Style.Font(8, FontStyle.Regular);
            button.SetBounds(x, y, width, height); button.Click += delegate { action(); }; Controls.Add(button);
        }
        private void LoadRole()
        {
            StopVoice(); loading = true;
            int role = rolePicker.SelectedIndex; ReminderProfile profile = Current;
            animationPicker.Items.Clear(); animationPicker.Items.AddRange(new object[] { "举杯提醒", MediaCatalog.ExtraActions[role], "随机播放", "自定义 GIF" });
            voicePicker.Items.Clear(); voicePicker.Items.AddRange(MediaCatalog.VoiceNames);
            animationPicker.SelectedIndex = profile.AnimationStyle; voicePicker.SelectedIndex = profile.VoiceStyle;
            enabled.Visible = scenePicker.SelectedIndex != 0; enabled.Checked = profile.Enabled;
            loading = false; UpdateFileLabels();
        }
        private void UpdateFileLabels()
        {
            ReminderProfile profile = Current;
            animationFile.Text = String.IsNullOrEmpty(profile.AnimationFile) ? "可导入自己的动画。" : "自定义：" + Path.GetFileName(profile.AnimationFile);
            voiceFile.Text = String.IsNullOrEmpty(profile.VoiceFile) ? "可导入自己的提醒声音。" : "自定义：" + Path.GetFileName(profile.VoiceFile);
        }
        private void PickFile(bool animation)
        {
            using (OpenFileDialog picker = new OpenFileDialog { Title = animation ? "替换提醒动画" : "替换提醒语音",
                Filter = animation ? "GIF 动画 (*.gif)|*.gif" : "WAV 语音 (*.wav)|*.wav", CheckFileExists = true, RestoreDirectory = true })
                if (picker.ShowDialog(this) == DialogResult.OK) SetCustomFile(picker.FileName, animation);
        }
        internal bool SetCustomFile(string path, bool animation)
        {
            try
            {
                MediaLibrary.Validate(path, animation);
                ReminderProfile profile = Current;
                if (animation) { profile.AnimationFile = path; profile.AnimationStyle = MediaCatalog.CustomAnimation; }
                else { profile.VoiceFile = path; profile.VoiceStyle = MediaCatalog.CustomVoice; }
                LoadRole(); status.Text = "已选中素材，点击「应用」后再保存配置。"; status.ForeColor = Style.Muted; return true;
            }
            catch (Exception e) { if (!MediaLibrary.IsMediaError(e)) throw; status.Text = e.Message; status.ForeColor = Color.Firebrick; return false; }
        }
        internal void RestoreDefault(bool animation)
        {
            ReminderProfile profile = Current, defaults = MediaCatalog.DefaultProfile(scenePicker.SelectedIndex);
            if (animation) { profile.AnimationFile = ""; profile.AnimationStyle = defaults.AnimationStyle; }
            else { profile.VoiceFile = ""; profile.VoiceStyle = defaults.VoiceStyle; }
            LoadRole(); status.Text = "已恢复内置素材，保存配置后生效。"; status.ForeColor = Style.Muted;
        }
        private bool ValidateSelection()
        {
            foreach (ReminderProfile[] group in drafts) foreach (ReminderProfile profile in group)
            {
                if (!profile.Enabled) continue;
                if (profile.AnimationStyle == MediaCatalog.CustomAnimation) MediaLibrary.Validate(profile.AnimationFile, true);
                if (profile.VoiceStyle == MediaCatalog.CustomVoice) MediaLibrary.Validate(profile.VoiceFile, false);
            }
            return true;
        }
        internal bool ApplyChanges()
        {
            try
            {
                ValidateSelection(); Result = MediaCatalog.CopyProfiles(drafts[0]);
                GreetingResult = MediaCatalog.CopyProfiles(drafts[1], 1); BrewingResult = MediaCatalog.CopyProfiles(drafts[2], 2); return true;
            }
            catch (Exception e) { if (!MediaLibrary.IsMediaError(e)) throw; status.Text = e.Message; status.ForeColor = Color.Firebrick; return false; }
        }
        private void PreviewAnimation()
        {
            StopVoice(); int role = rolePicker.SelectedIndex;
            using (AlertForm preview = new AlertForm("", SystemIcons.Information, Style.Green, role, true, true, false, Current, true, scenePicker.SelectedIndex))
            { preview.ShowInTaskbar = false; preview.ShowDialog(this); }
        }
        private void PreviewVoice()
        {
            StopVoice(); ReminderProfile profile = Current;
            audition = new ReminderSpeech(profile.VoiceStyle, profile.VoiceFile, scenePicker.SelectedIndex); audition.Play();
        }
        private void StopVoice() { if (audition != null) { audition.Dispose(); audition = null; } }
        protected override void Dispose(bool disposing) { if (disposing) StopVoice(); base.Dispose(disposing); }
        internal void ChooseRole(int role) { rolePicker.SelectedIndex = role; }
        internal void ChooseScene(int scene) { scenePicker.SelectedIndex = scene; }
        internal void EnableInteraction(bool value) { enabled.Checked = value; }
        internal void ChooseAnimation(int style) { animationPicker.SelectedIndex = style; }
        internal void ChooseVoice(int style) { voicePicker.SelectedIndex = style; }
        internal void CapturePreview(string path) { Style.CaptureForm(this, path); }
    }
}
