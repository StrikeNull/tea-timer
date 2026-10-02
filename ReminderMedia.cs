using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Media;
using System.Reflection;

namespace TeaTimer
{
    internal sealed class ReminderClip : IDisposable
    {
        private Stream stream;
        internal Image Image { get; private set; }
        internal Rectangle ContentBounds { get; private set; }
        private int[] frameEnds;
        internal int DurationMilliseconds { get { return frameEnds[frameEnds.Length - 1]; } }
        internal int FrameCount { get { return frameEnds.Length; } }
        internal ReminderClip(int kind, int style = 0, string file = null, int scene = 0)
        {
            if (style == MediaCatalog.CustomAnimation && !String.IsNullOrEmpty(file))
            {
                try { Load(MediaLibrary.OpenLocal(file, true)); }
                catch (Exception e) { if (!MediaLibrary.IsMediaError(e)) throw; }
            }
            if (Image == null)
            {
                Load(Assembly.GetExecutingAssembly().GetManifestResourceStream(MediaCatalog.AnimationResource(kind,
                    style == MediaCatalog.CustomAnimation ? MediaCatalog.DefaultProfile(scene).AnimationStyle : style)));
                ContentBounds = SpriteDrawing.AnimationBounds(Image, FrameCount);
            }
        }
        internal ReminderClip(string file) { Load(MediaLibrary.OpenLocal(file, true)); }
        private void Load(Stream data)
        {
            stream = data;
            try
            {
                if (stream == null) throw new InvalidOperationException("缺少提醒动画素材。");
                Image = System.Drawing.Image.FromStream(stream);
                ContentBounds = new Rectangle(0, 0, Image.Width, Image.Height);
                if (Image.RawFormat.Guid != ImageFormat.Gif.Guid || Image.Width > 2048 || Image.Height > 2048)
                    throw new InvalidOperationException("请选择宽高不超过 2048 像素的 GIF 动画。");
                int count = Image.GetFrameCount(FrameDimension.Time);
                if (count < 2 || count > 600) throw new InvalidOperationException("GIF 需要有 2～600 帧动画。");
                frameEnds = new int[count]; byte[] delays = Image.GetPropertyItem(0x5100).Value;
                if (delays.Length < count * 4) throw new InvalidOperationException("GIF 动画时间信息不完整。");
                int total = 0;
                for (int i = 0; i < count; i++)
                {
                    int delay = BitConverter.ToInt32(delays, i * 4);
                    if (delay < 0 || delay > 3000) throw new InvalidOperationException("GIF 动画时长不超过 30 秒。");
                    total += Math.Max(10, delay * 10);
                    if (total > 30000) throw new InvalidOperationException("GIF 动画时长不超过 30 秒。");
                    frameEnds[i] = total;
                }
            }
            catch { Dispose(); throw; }
        }
        internal int FrameAt(long elapsed)
        {
            for (int i = 0; i < frameEnds.Length; i++) if (elapsed < frameEnds[i]) return i;
            return frameEnds.Length - 1;
        }
        internal void SelectFrame(int index) { Image.SelectActiveFrame(FrameDimension.Time, index); }
        public void Dispose() { if (Image != null) { Image.Dispose(); Image = null; } if (stream != null) { stream.Dispose(); stream = null; } }
    }

    internal sealed class ReminderSpeech : IDisposable
    {
        private Stream stream;
        private SoundPlayer player;
        private bool disposed;
        internal bool IsLoaded { get { return player.IsLoadCompleted; } }
        internal ReminderSpeech(int style = 0, string file = null, int scene = 0)
        {
            if (style == MediaCatalog.CustomVoice && !String.IsNullOrEmpty(file))
            {
                try { Load(MediaLibrary.OpenLocal(file, false)); }
                catch (Exception e) { if (!MediaLibrary.IsMediaError(e)) throw; disposed = false; }
            }
            if (player == null) Load(Assembly.GetExecutingAssembly().GetManifestResourceStream(MediaCatalog.VoiceResource(
                style == MediaCatalog.CustomVoice ? MediaCatalog.DefaultProfile(scene).VoiceStyle : style)));
        }
        internal ReminderSpeech(string file) { Load(MediaLibrary.OpenLocal(file, false)); }
        private void Load(Stream data)
        {
            stream = data;
            try
            {
                if (stream == null) throw new InvalidOperationException("缺少提醒语音素材。");
                MediaLibrary.ValidateWave(stream); player = new SoundPlayer(stream); player.Load();
            }
            catch { Dispose(); throw; }
        }
        internal void Play() { player.Play(); }
        public void Dispose()
        {
            if (disposed) return; disposed = true;
            if (player != null) { player.Stop(); player.Dispose(); player = null; }
            if (stream != null) { stream.Dispose(); stream = null; }
        }
    }
}
