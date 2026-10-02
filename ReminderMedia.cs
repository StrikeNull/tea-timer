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
        private readonly Stream stream;
        internal readonly Image Image;
        private readonly int[] frameEnds;
        internal int DurationMilliseconds { get { return frameEnds[frameEnds.Length - 1]; } }
        internal int FrameCount { get { return frameEnds.Length; } }
        internal ReminderClip(int kind)
        {
            string[] names = { "TeaTimer.ReadyMaid", "TeaTimer.ReadyGpt", "TeaTimer.ReadyDragon" };
            stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(names[Math.Max(0, Math.Min(names.Length - 1, kind))]);
            if (stream == null) throw new InvalidOperationException("缺少提醒动画素材。");
            Image = System.Drawing.Image.FromStream(stream);
            frameEnds = new int[Image.GetFrameCount(FrameDimension.Time)];
            byte[] delays = Image.GetPropertyItem(0x5100).Value;
            int total = 0;
            for (int i = 0; i < frameEnds.Length; i++)
            {
                total += Math.Max(10, BitConverter.ToInt32(delays, i * 4) * 10);
                frameEnds[i] = total;
            }
        }
        internal int FrameAt(long elapsed)
        {
            for (int i = 0; i < frameEnds.Length; i++) if (elapsed < frameEnds[i]) return i;
            return frameEnds.Length - 1;
        }
        internal void SelectFrame(int index) { Image.SelectActiveFrame(FrameDimension.Time, index); }
        public void Dispose() { Image.Dispose(); stream.Dispose(); }
    }

    internal sealed class ReminderSpeech : IDisposable
    {
        private readonly Stream stream;
        private readonly SoundPlayer player;
        private bool disposed;
        internal bool IsLoaded { get { return player.IsLoadCompleted; } }
        internal ReminderSpeech()
        {
            stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("TeaTimer.ReadyVoice");
            if (stream == null) throw new InvalidOperationException("缺少提醒语音素材。");
            player = new SoundPlayer(stream); player.Load();
        }
        internal void Play() { player.Play(); }
        public void Dispose() { if (disposed) return; disposed = true; player.Stop(); player.Dispose(); stream.Dispose(); }
    }
}
