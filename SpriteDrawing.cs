using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

namespace TeaTimer
{
    internal static class SpriteDrawing
    {
        internal static Rectangle AlphaBounds(Image image, Rectangle region)
        {
            using (Bitmap copy = new Bitmap(region.Width, region.Height, PixelFormat.Format32bppArgb))
            {
                using (Graphics g = Graphics.FromImage(copy))
                    g.DrawImage(image, new Rectangle(0, 0, copy.Width, copy.Height), region, GraphicsUnit.Pixel);
                BitmapData data = copy.LockBits(new Rectangle(0, 0, copy.Width, copy.Height), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
                int left = copy.Width, top = copy.Height, right = -1, bottom = -1;
                try
                {
                    byte[] row = new byte[copy.Width * 4];
                    for (int y = 0; y < copy.Height; y++)
                    {
                        Marshal.Copy(IntPtr.Add(data.Scan0, y * data.Stride), row, 0, row.Length);
                        for (int x = 0; x < copy.Width; x++) if (row[x * 4 + 3] > 24)
                        { left = Math.Min(left, x); right = Math.Max(right, x); top = Math.Min(top, y); bottom = Math.Max(bottom, y); }
                    }
                }
                finally { copy.UnlockBits(data); }
                if (right < left) return region;
                return Rectangle.FromLTRB(region.X + Math.Max(0, left - 2), region.Y + Math.Max(0, top - 2),
                    region.X + Math.Min(region.Width, right + 3), region.Y + Math.Min(region.Height, bottom + 3));
            }
        }
        internal static Rectangle AnimationBounds(Image image, int frames)
        {
            Rectangle result = Rectangle.Empty, full = new Rectangle(0, 0, image.Width, image.Height);
            for (int frame = 0; frame < frames; frame++)
            {
                image.SelectActiveFrame(FrameDimension.Time, frame);
                Rectangle bounds = AlphaBounds(image, full);
                result = result.IsEmpty ? bounds : Rectangle.Union(result, bounds);
            }
            image.SelectActiveFrame(FrameDimension.Time, 0); return result;
        }
        internal static RectangleF FittedBounds(Rectangle source, RectangleF bounds)
        {
            float factor = .94f * Math.Min(bounds.Width / source.Width, bounds.Height / source.Height);
            float width = source.Width * factor, height = source.Height * factor;
            return new RectangleF(bounds.X + (bounds.Width - width) / 2, bounds.Bottom - bounds.Height * .02f - height, width, height);
        }
        internal static void DrawFit(Graphics g, Image image, Rectangle source, RectangleF bounds)
        { g.DrawImage(image, FittedBounds(source, bounds), source, GraphicsUnit.Pixel); }
        internal static void DrawOpacity(Graphics g, Image image, Rectangle bounds, float opacity)
        {
            if (opacity <= 0) return;
            if (opacity >= 1) { g.DrawImage(image, bounds); return; }
            using (ImageAttributes attributes = new ImageAttributes())
            {
                ColorMatrix matrix = new ColorMatrix(); matrix.Matrix33 = opacity; attributes.SetColorMatrix(matrix);
                g.DrawImage(image, bounds, 0, 0, image.Width, image.Height, GraphicsUnit.Pixel, attributes);
            }
        }
    }
}
