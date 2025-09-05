using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

namespace Kiritori.Helpers
{
    internal static class FastBitmapHash
    {
        public static ulong Compute(Bitmap bmp)
        {
            // すでに 32bppPArgb で来る想定だが、違う場合は LockBits で変換なしに読み出す
            var rect = new Rectangle(0, 0, bmp.Width, bmp.Height);
            var data = bmp.LockBits(rect, ImageLockMode.ReadOnly, PixelFormat.Format32bppPArgb);
            try
            {
                return Compute(data);
            }
            finally
            {
                bmp.UnlockBits(data);
            }
        }

        public static unsafe ulong Compute(BitmapData data)
        {
            // FNV-1a 64bit
            const ulong FNV_OFFSET_BASIS = 1469598103934665603UL;
            const ulong FNV_PRIME = 1099511628211UL;

            int w = data.Width;
            int h = data.Height;
            int stride = data.Stride; // バイト/行（しばしば width*4 以上）

            byte* basePtr = (byte*)data.Scan0.ToPointer();
            ulong hash = FNV_OFFSET_BASIS;

            // 全画素版（最も確実）
            for (int y = 0; y < h; y++)
            {
                byte* row = basePtr + y * stride;
                // 1行あたり 4 * w バイト（32bpp）
                for (int x = 0; x < w * 4; x++)
                {
                    hash ^= row[x];
                    hash *= FNV_PRIME;
                }
            }
            return hash;
        }

        // さらに高速化したい場合のサンプリング版（必要なら差し替え）
        public static unsafe ulong ComputeSampled(BitmapData data, int stepY = 2, int stepXBytes = 8)
        {
            const ulong FNV_OFFSET_BASIS = 1469598103934665603UL;
            const ulong FNV_PRIME = 1099511628211UL;

            int w = data.Width;
            int h = data.Height;
            int stride = data.Stride;

            byte* basePtr = (byte*)data.Scan0.ToPointer();
            ulong hash = FNV_OFFSET_BASIS;

            for (int y = 0; y < h; y += stepY)
            {
                byte* row = basePtr + y * stride;
                int rowBytes = w * 4;
                for (int b = 0; b < rowBytes; b += stepXBytes)
                {
                    hash ^= row[b];
                    hash *= FNV_PRIME;
                }
            }
            return hash;
        }
    }
}
