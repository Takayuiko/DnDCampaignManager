using SkiaSharp;

namespace DnDCampaignManager.Api.Services;

public static class MapThumbnail
{
    public const int MaxDimension = 640;
    public const int MaxBytes = 100000;

    public static byte[]? Create(byte[] source)
    {
        using var data = SKData.CreateCopy(source);
        using var codec = SKCodec.Create(data);
        if (codec is null || codec.Info.Width <= 0 || codec.Info.Height <= 0) return null;
        // Limit decoded memory independently of the compressed upload size.
        var scale = Math.Min(1d, (double)MaxDimension / Math.Max(codec.Info.Width, codec.Info.Height));
        var decodedSize = codec.GetScaledDimensions((float)scale);
        if ((long)decodedSize.Width * decodedSize.Height > 64000000) return null;
        using var decoded = new SKBitmap(new SKImageInfo(decodedSize.Width, decodedSize.Height));
        if (codec.GetPixels(decoded.Info, decoded.GetPixels()) != SKCodecResult.Success) return null;
        var width = Math.Max(1, (int)Math.Round(codec.Info.Width * scale));
        var height = Math.Max(1, (int)Math.Round(codec.Info.Height * scale));
        while (true)
        {
            using var resized = decoded.Resize(new SKImageInfo(width, height), new SKSamplingOptions(SKFilterMode.Linear));
            if (resized is null) return null;
            var rotated = codec.EncodedOrigin is SKEncodedOrigin.LeftTop or SKEncodedOrigin.RightTop or
                SKEncodedOrigin.RightBottom or SKEncodedOrigin.LeftBottom;
            using var surface = SKSurface.Create(new SKImageInfo(rotated ? height : width, rotated ? width : height));
            if (surface is null) return null;
            surface.Canvas.Clear(SKColors.White);
            // Match the browser's EXIF orientation, including mirrored images.
            switch (codec.EncodedOrigin)
            {
                case SKEncodedOrigin.TopRight: surface.Canvas.Translate(width, 0); surface.Canvas.Scale(-1, 1); break;
                case SKEncodedOrigin.BottomRight: surface.Canvas.Translate(width, height); surface.Canvas.RotateDegrees(180); break;
                case SKEncodedOrigin.BottomLeft: surface.Canvas.Translate(0, height); surface.Canvas.Scale(1, -1); break;
                case SKEncodedOrigin.LeftTop: surface.Canvas.RotateDegrees(90); surface.Canvas.Scale(1, -1); break;
                case SKEncodedOrigin.RightTop: surface.Canvas.Translate(height, 0); surface.Canvas.RotateDegrees(90); break;
                case SKEncodedOrigin.RightBottom: surface.Canvas.Translate(height, width); surface.Canvas.RotateDegrees(90); surface.Canvas.Scale(-1, 1); break;
                case SKEncodedOrigin.LeftBottom: surface.Canvas.Translate(0, width); surface.Canvas.RotateDegrees(270); break;
            }
            surface.Canvas.DrawBitmap(resized, 0, 0);
            using var image = surface.Snapshot();
            using var encoded = image.Encode(SKEncodedImageFormat.Jpeg, 75);
            if (encoded is null) return null;
            if (encoded.Size <= MaxBytes) return encoded.ToArray();
            width = Math.Max(1, width / 2);
            height = Math.Max(1, height / 2);
        }
    }
}
