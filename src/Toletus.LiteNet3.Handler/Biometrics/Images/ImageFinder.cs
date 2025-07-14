using System.Diagnostics;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using Toletus.LiteNet3.Handler.Biometrics.Interfaces;

namespace Toletus.LiteNet3.Handler.Biometrics.Images;

public class ImageFinder
{
    public string ImageName { get; set; } = $"{Guid.NewGuid()}.png";
    public string SavePath { get; set; } = @"C:\Images";
    public Image<Rgba32> Image { get; private set; }
    public byte[] DecompressedImage { get; private set; }

    private readonly IImageProcessor _imageProcessor;
    private readonly IImageSaver _imageSaver;

    public ImageFinder(byte[] dataBytes) : this(new ImageProcessor(), new ImageSaver())
    {
        DecompressedImage = _imageProcessor.DecompressData(dataBytes);
    }

    public ImageFinder(IImageProcessor imageProcessor, IImageSaver imageSaver)
    {
        _imageProcessor = imageProcessor;
        _imageSaver = imageSaver;
    }

    public void SaveImage(string filePath)
    {
        _imageSaver.SaveImage(Image, filePath);
    }

    public void SaveImage() => SaveImage($"{SavePath}\\{ImageName}");
}