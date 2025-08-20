using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Metadata;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using SkiaSharp;
using Toletus.LiteNet3.Handler.Biometrics.Interfaces;

namespace Toletus.LiteNet3.Handler.Biometrics.Images;

public class ImageProcessor : IImageProcessor
{
    private const int Width = 320;
    private const int Height = 400;

    public Image<Rgba32> ProcessImage(byte[] dataBytes)
    {
        var decompressedData = DecompressData(dataBytes);
        return CreateImageFromData(decompressedData);
    }

    public byte[] DecompressData(byte[] dataBytes)
    {
        var firstDecompression = FirstDecompression(dataBytes);
        return SecondDecompression(firstDecompression);
    }

    #region Teste Bitmap

    public SKBitmap CreateBitmapFromData(byte[] imageData)
    {
        var bmp = new SKBitmap(Width, Height, SKColorType.Bgra8888, SKAlphaType.Opaque);

        var index = 0;
        for (var y = 0; y < Height; y++)
        {
            for (var x = 0; x < Width; x++)
            {
                if (index + 1 >= imageData.Length)
                {
                    bmp.SetPixel(x, y, new SKColor(0, 0, 0));
                    continue;
                }

                var value = (ushort)(imageData[index] | (imageData[index + 1] << 8));
                index += 2;

                var r = Expand5To8((byte)((value >> 10) & 0x1F));
                var g = Expand5To8((byte)((value >> 5) & 0x1F));
                var b = Expand5To8((byte)(value & 0x1F));
                var gray = (byte)(0.299 * r + 0.587 * g + 0.114 * b);

                bmp.SetPixel(x, y, new SKColor(gray, gray, gray));
            }
        }

        using var surface = SKSurface.Create(new SKImageInfo(Width, Height));
        var canvas = surface.Canvas;
        canvas.Scale(-1, 1);
        canvas.Translate(-Width, 0);
        canvas.DrawBitmap(bmp, 0, 0);
        canvas.Flush();

        var snapshot = surface.Snapshot();
        var flipped = SKBitmap.FromImage(snapshot);
        bmp.Dispose();
        return flipped;
        
        static byte Expand5To8(byte v) => (byte)((v << 3) | (v >> 2));
    }

    public Image<Rgba32> ToImageSharp(SKBitmap bmp)
    {
        using var ms = new MemoryStream();
        bmp.Encode(ms, SKEncodedImageFormat.Png, 100);
        ms.Position = 0;

        var image = Image.Load<Rgba32>(ms);

        ApplyImageTransformations(image);

        image.Metadata.ResolutionUnits = PixelResolutionUnit.PixelsPerInch;
        image.Metadata.HorizontalResolution = 500;
        image.Metadata.VerticalResolution = 500;

        return image;
    }

    #endregion

    public Image<Rgba32> CreateImageFromData(byte[] imageData)
    {
        var image = new Image<Rgba32>(Width, Height);
        var index = 0;

        for (var y = 0; y < Height; y++)
        {
            for (var x = 0; x < Width; x++)
            {
                if (index + 1 >= imageData.Length) continue;

                var value = BitConverter.ToInt16(imageData, index);
                index += 2;

                var r = ExpandToByte(value >> 10 & 0x1F);
                var g = ExpandToByte(value >> 5 & 0x1F);
                var b = ExpandToByte(value & 0x1F);

                image[x, y] = new Rgba32(r, g, b);
            }
        }

        ApplyImageTransformations(image);

        return image;
    }

    private static byte[] FirstDecompression(byte[] dataBytes)
    {
        var bytesParaProcessar = dataBytes.Length;
        for (var i = 1; i < dataBytes.Length; i++)
        {
            if (dataBytes[i - 1] != 255 || dataBytes[i] != 254) continue;

            bytesParaProcessar = dataBytes.Length - 2;
            break;
        }

        var buffer = new byte[40000];
        var indiceBuffer = 0;

        for (var i = 0; i < bytesParaProcessar; i++)
        {
            var byteAtual = dataBytes[i];

            if (byteAtual == 255 && i + 1 < bytesParaProcessar)
            {
                var repeticoes = dataBytes[++i];
                for (var j = 0; j <= repeticoes; j++)
                    buffer[indiceBuffer++] = 255;

                continue;
            }

            buffer[indiceBuffer++] = byteAtual;
        }

        var resultado = new byte[indiceBuffer];
        Array.Copy(buffer, resultado, indiceBuffer);
        return resultado;
    }

    private static byte[] SecondDecompression(byte[] compressedData)
    {
        const int expectedLength = 3 * Width * Height;
        var decompressedData = new byte[expectedLength];
        var decomIndex = 0;

        foreach (var value in compressedData)
        {
            for (var bit = 0; bit < 8 && decomIndex < expectedLength; bit++)
                decompressedData[decomIndex++] = TestBit(value, bit) ? (byte)255 : (byte)0;

            if (decomIndex >= expectedLength)
                break;
        }

        return decompressedData;
    }

    private static bool TestBit(int number, int bitPosition) => (number & (1 << bitPosition)) != 0;

    private static byte ExpandToByte(int value) => (byte)(value * 255 / 31);

    private static void ApplyImageTransformations(Image<Rgba32> image)
    {
        image.Mutate(ctx => ctx
            .Grayscale()
            .Flip(FlipMode.Horizontal));

        image.Metadata.ResolutionUnits = PixelResolutionUnit.PixelsPerInch;
        image.Metadata.HorizontalResolution = 500;
        image.Metadata.VerticalResolution = 500;
    }
}