using System.Collections.Specialized;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace ImmichBridge;

public static class FileClipboard
{
    private const int ExifOrientationId = 0x0112;
    private const int DropEffectCopy = 1;

    // Puts the file itself on the clipboard, as Explorer's Copy does, so file-aware targets
    // (Explorer, mail attachments, messengers) receive the untouched original. Decodable
    // images are added as pixels too, so image editors paste the full-resolution picture.
    public static void Copy(string localPath)
    {
        var data = new DataObject();
        data.SetFileDropList(new StringCollection { localPath });
        data.SetData("Preferred DropEffect", new MemoryStream(BitConverter.GetBytes(DropEffectCopy)));

        if (Path.GetExtension(localPath).Equals(".png", StringComparison.OrdinalIgnoreCase))
        {
            // The registered PNG format keeps transparency; the original bytes are already PNG.
            data.SetData("PNG", new MemoryStream(File.ReadAllBytes(localPath)));
        }

        using var image = TryLoadImage(localPath);
        if (image is not null)
        {
            // Only the native bitmap format: SetImage would also add a serialized .NET-only
            // copy that no paste target reads and that takes seconds to encode for large photos.
            data.SetData(DataFormats.Bitmap, autoConvert: false, image);
        }

        // copy: true flushes every format to the clipboard so it survives this process exiting.
        Clipboard.SetDataObject(data, copy: true, retryTimes: 10, retryDelay: 100);
    }

    private static Bitmap? TryLoadImage(string localPath)
    {
        try
        {
            using var stream = File.OpenRead(localPath);
            using var decoded = Image.FromStream(stream);
            var bitmap = new Bitmap(decoded);
            ApplyExifOrientation(decoded, bitmap);
            return bitmap;
        }
        catch (Exception ex) when (ex is ArgumentException or ExternalException or OutOfMemoryException)
        {
            // Not an image GDI+ can decode (video, RAW, HEIC, ...): the file copy alone remains useful.
            return null;
        }
    }

    private static void ApplyExifOrientation(Image source, Bitmap target)
    {
        if (!source.PropertyIdList.Contains(ExifOrientationId))
        {
            return;
        }

        var value = source.GetPropertyItem(ExifOrientationId)?.Value;
        if (value is null || value.Length < 2)
        {
            return;
        }

        var rotation = BitConverter.ToUInt16(value, 0) switch
        {
            2 => RotateFlipType.RotateNoneFlipX,
            3 => RotateFlipType.Rotate180FlipNone,
            4 => RotateFlipType.Rotate180FlipX,
            5 => RotateFlipType.Rotate90FlipX,
            6 => RotateFlipType.Rotate90FlipNone,
            7 => RotateFlipType.Rotate270FlipX,
            8 => RotateFlipType.Rotate270FlipNone,
            _ => RotateFlipType.RotateNoneFlipNone
        };

        target.RotateFlip(rotation);
    }
}
