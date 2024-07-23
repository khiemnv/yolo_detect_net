//using System.Text.Json;

public static class ImageExtensions
{
    public static Bitmap CloneFromFile(this string path)
    {
        var bytes = File.ReadAllBytes(path);
        var ms = new MemoryStream(bytes);
        var img = System.Drawing.Image.FromStream(ms);
        return (Bitmap)img;
    }
}
