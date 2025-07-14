using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace Toletus.LiteNet3.Handler.Biometrics.Interfaces;

public interface IImageSaver
{
    void SaveImage(Image<Rgba32> image, string filePath);
}