using System;
using System.IO;
using System.Collections.Generic;
using System.Drawing;

class Program
{
    static void Main(string[] args)
    {
        var outPath = args.Length > 0 ? args[0] : "..\\Teine64\\Resources\\teine64.ico";
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(outPath))!);
        using var fs = new FileStream(outPath, FileMode.Create, FileAccess.Write);
        using var bw = new BinaryWriter(fs);

        // Icon sizes
        int[] sizes = {16, 24, 32, 48, 64, 128};
        var images = new List<byte[]>();

        foreach (var size in sizes)
        {
            using var bmp = new Bitmap(size, size);
            using (var g = Graphics.FromImage(bmp))
            {
                g.Clear(Color.Transparent);
                // Draw mug outline
                var bodyRect = new Rectangle(size/6, size/4, size/2, size/2 + size/8);
                var handleRect = new Rectangle(bodyRect.Right - 2, bodyRect.Top + size/8, size/4, bodyRect.Height - size/4);
                using var wall = new SolidBrush(Color.FromArgb(0xD8,0xD0,0xC8));
                using var tea = new SolidBrush(Color.FromArgb(0x5A,0x30,0x15));
                using var outline = new Pen(Color.FromArgb(0x4A,0x2A,0x10), Math.Max(1, size/32f));
                g.FillRectangle(wall, bodyRect);
                g.FillRectangle(wall, handleRect);
                // Tea surface
                var teaRect = new Rectangle(bodyRect.Left + size/16, bodyRect.Top + size/6, bodyRect.Width - size/8, bodyRect.Height - size/4);
                g.FillRectangle(tea, teaRect);
                g.DrawRectangle(outline, bodyRect);
                g.DrawRectangle(outline, handleRect);
            }
            images.Add(BitmapToPngBytes(bmp));
        }

        // ICONDIR header
        bw.Write((ushort)0); // reserved
        bw.Write((ushort)1); // type
        bw.Write((ushort)images.Count); // count

        int offset = 6 + (16 * images.Count);
        for (int i=0;i<images.Count;i++)
        {
            using var ms = new MemoryStream(images[i]);
            using var pngReader = new BinaryReader(ms);
            // Directory entry
            int w = sizes[i];
            int h = sizes[i];
            bw.Write((byte)(w >= 256 ? 0 : w));
            bw.Write((byte)(h >= 256 ? 0 : h));
            bw.Write((byte)0); // colors
            bw.Write((byte)0); // reserved
            bw.Write((ushort)1); // planes
            bw.Write((ushort)32); // bit count
            bw.Write(images[i].Length); // bytes
            bw.Write(offset); // offset
            offset += images[i].Length;
        }
        // Write image data blocks
        foreach (var data in images)
            bw.Write(data);
    }

    static byte[] BitmapToPngBytes(Bitmap bmp)
    {
        using var ms = new MemoryStream();
        bmp.Save(ms, System.Drawing.Imaging.ImageFormat.Png);
        return ms.ToArray();
    }
}
