using System.IO;
using System.IO.Compression;
using System.Text;

namespace Pogrujenie.Core
{
    /// <summary>
    /// Текстура модели художника в формате PGT1 (делает tools/glb_to_unity.py из PNG внутри GLB):
    /// "PGT1", int32 ширина, int32 высота, затем пиксели RGB24 строками снизу вверх, сжатые raw deflate.
    /// Свой формат, потому что модуль ImageConversion (Texture2D.LoadImage) в проект не подключён.
    /// </summary>
    public sealed class TexturePack
    {
        public int Width, Height;
        public byte[] Rgb;

        public static TexturePack Read(byte[] data)
        {
            if (data.Length < 12 || Encoding.ASCII.GetString(data, 0, 4) != "PGT1") throw new InvalidDataException("не PGT1");
            int w = System.BitConverter.ToInt32(data, 4), h = System.BitConverter.ToInt32(data, 8);
            if (w <= 0 || h <= 0 || w > 4096 || h > 4096) throw new InvalidDataException("размер текстуры");
            var rgb = new byte[w * h * 3];
            try
            {
                using (var z = new DeflateStream(new MemoryStream(data, 12, data.Length - 12), CompressionMode.Decompress))
                {
                    int got = 0;
                    while (got < rgb.Length)
                    {
                        int n = z.Read(rgb, got, rgb.Length - got);
                        if (n <= 0) throw new InvalidDataException("текстура короче, чем заявлено");
                        got += n;
                    }
                }
            }
            catch (System.Exception e) when (!(e is InvalidDataException))
            {
                throw new InvalidDataException("битая текстура: " + e.Message);
            }
            return new TexturePack { Width = w, Height = h, Rgb = rgb };
        }
    }
}
