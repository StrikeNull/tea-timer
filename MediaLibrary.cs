using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;

namespace TeaTimer
{
    public sealed class ReminderProfile
    {
        public bool Enabled = true;
        public int AnimationStyle;
        public int VoiceStyle;
        public string AnimationFile = "";
        public string VoiceFile = "";
        internal ReminderProfile Copy()
        {
            return new ReminderProfile { Enabled = Enabled, AnimationStyle = AnimationStyle, VoiceStyle = VoiceStyle,
                AnimationFile = AnimationFile ?? "", VoiceFile = VoiceFile ?? "" };
        }
    }

    internal static class MediaCatalog
    {
        internal const int CustomAnimation = 3, CustomVoice = 5;
        internal static readonly string[] ExtraActions = { "挥手迎茶", "俏皮眨眼", "摇尾招呼" };
        internal static readonly string[] SceneNames = { "到时提醒", "启动 / 点击选茶", "开始 / 再泡一杯" };
        internal static readonly string[] VoiceNames = { "清甜出汤", "活泼女仆", "温柔茶香", "俏皮催茶", "随机出汤语音", "自定义 WAV", "今天喝什么茶", "开始泡茶" };
        private static readonly Random random = new Random();
        internal static string AnimationResource(int role, int style)
        {
            role = Math.Max(0, Math.Min(2, role));
            if (style == 2) style = random.Next(2);
            string[] names = style == 1 ? new[] { "TeaTimer.ExtraMaid", "TeaTimer.ExtraGpt", "TeaTimer.ExtraDragon" }
                : new[] { "TeaTimer.ReadyMaid", "TeaTimer.ReadyGpt", "TeaTimer.ReadyDragon" };
            return names[role];
        }
        internal static string VoiceResource(int style)
        {
            if (style == 4) style = random.Next(4);
            string[] names = { "TeaTimer.ReadyVoice", "TeaTimer.VoiceCheerful", "TeaTimer.VoiceSoft", "TeaTimer.VoicePlayful",
                "TeaTimer.ReadyVoice", "TeaTimer.ReadyVoice", "TeaTimer.VoiceToday", "TeaTimer.VoiceBrewing" };
            return names[Math.Max(0, Math.Min(7, style))];
        }
        internal static ReminderProfile DefaultProfile(int scene)
        { return new ReminderProfile { AnimationStyle = scene == 1 ? 1 : 0, VoiceStyle = scene == 1 ? 6 : scene == 2 ? 7 : 0 }; }
        internal static ReminderProfile[] CopyProfiles(ReminderProfile[] profiles, int scene = 0)
        {
            ReminderProfile[] result = new ReminderProfile[3];
            for (int i = 0; i < result.Length; i++)
            {
                result[i] = profiles != null && i < profiles.Length && profiles[i] != null ? profiles[i].Copy() : DefaultProfile(scene);
                result[i].AnimationStyle = Math.Max(0, Math.Min(CustomAnimation, result[i].AnimationStyle));
                result[i].VoiceStyle = Math.Max(0, Math.Min(7, result[i].VoiceStyle));
            }
            return result;
        }
    }

    internal static class MediaLibrary
    {
        internal static string DirectoryPath { get { return Path.Combine(Path.GetDirectoryName(Preferences.FilePath), "media"); } }
        internal static bool IsMediaError(Exception e)
        {
            return e is IOException || e is UnauthorizedAccessException || e is ArgumentException || e is InvalidOperationException
                || e is ExternalException || e is OutOfMemoryException || e is NotSupportedException;
        }
        internal static Stream OpenLocal(string path, bool animation)
        {
            if (!String.Equals(Path.GetExtension(path), animation ? ".gif" : ".wav", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException(animation ? "请选择 GIF 动画。" : "请选择 WAV 语音。");
            FileStream stream = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.Read);
            if (stream.Length == 0 || stream.Length > 20 * 1024 * 1024)
            {
                stream.Dispose(); throw new InvalidOperationException("素材不能为空，大小请控制在 20 MB 以内。");
            }
            return stream;
        }
        internal static void Validate(string path, bool animation)
        {
            if (animation) { using (ReminderClip clip = new ReminderClip(path)) { } }
            else { using (ReminderSpeech speech = new ReminderSpeech(path)) { } }
        }
        internal static void ValidateWave(Stream stream)
        {
            BinaryReader reader = new BinaryReader(stream, Encoding.ASCII, true);
            if (stream.Length < 44 || new string(reader.ReadChars(4)) != "RIFF") throw new InvalidOperationException("WAV 文件无法读取。");
            reader.ReadUInt32();
            if (new string(reader.ReadChars(4)) != "WAVE") throw new InvalidOperationException("请选择有效的 WAV 语音。");
            bool formatFound = false; long dataBytes = 0; uint bytesPerSecond = 0;
            while (stream.Position + 8 <= stream.Length)
            {
                string kind = new string(reader.ReadChars(4)); uint size = reader.ReadUInt32(); long end = stream.Position + size;
                if (end > stream.Length) throw new InvalidOperationException("WAV 文件不完整。");
                if (kind == "fmt ")
                {
                    if (size < 16) throw new InvalidOperationException("WAV 格式信息不完整。");
                    ushort format = reader.ReadUInt16(), channels = reader.ReadUInt16(); uint rate = reader.ReadUInt32();
                    bytesPerSecond = reader.ReadUInt32(); ushort alignment = reader.ReadUInt16(), bits = reader.ReadUInt16();
                    if (format != 1 || channels < 1 || channels > 2 || rate < 8000 || rate > 96000 || (bits != 8 && bits != 16)
                        || alignment != channels * bits / 8 || bytesPerSecond != rate * alignment)
                        throw new InvalidOperationException("请选择单声道或双声道的 8/16 位 PCM WAV 语音。");
                    formatFound = true;
                }
                if (kind == "data") dataBytes += size;
                stream.Position = end + (size % 2);
            }
            if (!formatFound || dataBytes == 0 || bytesPerSecond == 0 || dataBytes * 1000 / bytesPerSecond > 30000)
                throw new InvalidOperationException("语音需有有效声音，时长不超过 30 秒。");
            stream.Position = 0;
        }
        internal static string Import(string source, bool animation, string directory = null)
        {
            Validate(source, animation);
            directory = directory ?? DirectoryPath;
            Directory.CreateDirectory(directory);
            string hash;
            using (Stream stream = OpenLocal(source, animation)) using (SHA256 sha = SHA256.Create())
                hash = BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", "").ToLowerInvariant();
            string basename = Path.GetFileNameWithoutExtension(source);
            if (basename.Length > 48) basename = basename.Substring(0, 48);
            string targetDirectory = Path.Combine(directory, hash); Directory.CreateDirectory(targetDirectory);
            string target = Path.Combine(targetDirectory, basename + (animation ? ".gif" : ".wav"));
            if (!String.Equals(Path.GetFullPath(source), Path.GetFullPath(target), StringComparison.OrdinalIgnoreCase))
            {
                bool identical = false;
                if (File.Exists(target)) using (Stream existing = OpenLocal(target, animation)) using (SHA256 sha = SHA256.Create())
                    identical = BitConverter.ToString(sha.ComputeHash(existing)).Replace("-", "").ToLowerInvariant() == hash;
                if (!identical) File.Copy(source, target, true);
            }
            Validate(target, animation);
            return target;
        }
        internal static ReminderProfile[] Store(ReminderProfile[] profiles, string directory = null, int scene = 0)
        {
            ReminderProfile[] result = MediaCatalog.CopyProfiles(profiles, scene);
            foreach (ReminderProfile profile in result)
            {
                if (!String.IsNullOrEmpty(profile.AnimationFile))
                {
                    try { profile.AnimationFile = Import(profile.AnimationFile, true, directory); }
                    catch (Exception e) { if (!IsMediaError(e) || (profile.Enabled && profile.AnimationStyle == MediaCatalog.CustomAnimation)) throw; profile.AnimationFile = ""; }
                }
                if (!String.IsNullOrEmpty(profile.VoiceFile))
                {
                    try { profile.VoiceFile = Import(profile.VoiceFile, false, directory); }
                    catch (Exception e) { if (!IsMediaError(e) || (profile.Enabled && profile.VoiceStyle == MediaCatalog.CustomVoice)) throw; profile.VoiceFile = ""; }
                }
            }
            return result;
        }
    }
}
