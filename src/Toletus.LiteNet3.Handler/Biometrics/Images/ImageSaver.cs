using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using Toletus.LiteNet3.Handler.Biometrics.Interfaces;

namespace Toletus.LiteNet3.Handler.Biometrics.Images;

public class ImageSaver : IImageSaver
{
    public void SaveImage(Image<Rgba32> image, string filePath)
    {
        ArgumentNullException.ThrowIfNull(image);
        try
        {
            image.Save(filePath);
            Console.WriteLine($"Imagem salva em: {filePath}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Erro ao salvar a imagem: {ex.Message}");
        }
    }
}