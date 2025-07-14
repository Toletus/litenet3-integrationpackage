using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace Toletus.LiteNet3.Handler.Biometrics.Interfaces;

public interface IImageProcessor
{
    Image<Rgba32> ProcessImage(byte[] dataBytes);
    byte[] DecompressData(byte[] dataBytes);
    Image<Rgba32> CreateImageFromData(byte[] imageData);
}