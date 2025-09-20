using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.Serialization;

namespace Kiritori.Services.Recording
{
    public static class GifWriter
    {
        private const int PropertyTagFrameDelay = 0x5100; // LONG[frameCount], 1/100 sec
        private const int PropertyTagLoopCount = 0x5101; // SHORT, 0 = infinite

        public static void SaveAnimatedGif(IList<Bitmap> frames, IList<int> delaysCs, string outPath, bool loopForever)
        {
            if (frames == null || frames.Count == 0) throw new ArgumentException("frames empty");
            if (delaysCs == null || delaysCs.Count != frames.Count) throw new ArgumentException("delays length mismatch");
            Directory.CreateDirectory(Path.GetDirectoryName(outPath));

            ImageCodecInfo gifEnc = GetEncoder(ImageFormat.Gif);
            if (gifEnc == null) throw new InvalidOperationException("GIF encoder not found");

            // 先頭フレームに全フレーム分の遅延とループ回数を埋めてから MultiFrame 保存
            using (Bitmap first = (Bitmap)frames[0].Clone())
            {
                // ループ回数（0=無限）
                SetPropertyItem(first, PropertyTagLoopCount, 3 /*Short*/, BitConverter.GetBytes((short)(loopForever ? 0 : 1)));

                // フレームディレイ（各フレーム4byteの配列）
                byte[] delayBytes = new byte[4 * delaysCs.Count];
                for (int i = 0; i < delaysCs.Count; i++)
                {
                    // 最低でも2cs(=20ms)程度に丸めると互換性が良い
                    int cs = delaysCs[i] < 2 ? 2 : delaysCs[i];
                    byte[] b = BitConverter.GetBytes(cs);
                    Buffer.BlockCopy(b, 0, delayBytes, i * 4, 4);
                }
                SetPropertyItem(first, PropertyTagFrameDelay, 4 /*Long*/, delayBytes);

                EncoderParameters ep = new EncoderParameters(1);
                ep.Param[0] = new EncoderParameter(Encoder.SaveFlag, (long)EncoderValue.MultiFrame);
                first.Save(outPath, gifEnc, ep);

                // 2枚目以降を追加
                ep.Param[0] = new EncoderParameter(Encoder.SaveFlag, (long)EncoderValue.FrameDimensionTime);
                for (int i = 1; i < frames.Count; i++)
                {
                    using (Bitmap f = (Bitmap)frames[i].Clone())
                    {
                        first.SaveAdd(f, ep);
                    }
                }

                // Flush
                ep.Param[0] = new EncoderParameter(Encoder.SaveFlag, (long)EncoderValue.Flush);
                first.SaveAdd(ep);
            }
        }

        private static void SetPropertyItem(Image img, int id, short type, byte[] value)
        {
            // PropertyItemはnewできないので未初期化インスタンスを作る
            PropertyItem pi = (PropertyItem)FormatterServices.GetUninitializedObject(typeof(PropertyItem));
            pi.Id = id;
            pi.Type = type;
            pi.Len = value != null ? value.Length : 0;
            pi.Value = value;
            img.SetPropertyItem(pi);
        }

        private static ImageCodecInfo GetEncoder(ImageFormat fmt)
        {
            ImageCodecInfo[] encs = ImageCodecInfo.GetImageEncoders();
            for (int i = 0; i < encs.Length; i++)
                if (encs[i].FormatID == fmt.Guid) return encs[i];
            return null;
        }
    }
}
