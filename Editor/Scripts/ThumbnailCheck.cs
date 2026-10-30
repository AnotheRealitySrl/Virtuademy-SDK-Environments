using System;
using System.Collections.Generic;

namespace Virtuademy.SDK.Environments.Editor
{
    /// <summary>One thing a file check found. <see cref="Row"/> is set for the CSV's rows (1-based, the header is row 1).</summary>
    internal sealed class FileIssue
    {
        public string Code;
        public string Message;
        public int? Row;

        public override string ToString() => Row == null ? Message : $"row {Row}: {Message}";
    }

    /// <summary>What a file check found: the reasons the platform would refuse the file (<see cref="Errors"/>) and what is worth knowing before it goes (<see cref="Warnings"/>).</summary>
    internal class FileCheckResult
    {
        public List<FileIssue> Errors = new();
        public List<FileIssue> Warnings = new();

        /// <summary>True when more errors were found than were kept.</summary>
        public bool ErrorsTruncated;

        public bool Valid => Errors.Count == 0;

        protected void Error(string code, string message, int? row = null) => Errors.Add(new FileIssue { Code = code, Message = message, Row = row });

        protected void Warn(string code, string message, int? row = null) => Warnings.Add(new FileIssue { Code = code, Message = message, Row = row });
    }

    /// <summary>
    /// What the SDK checks in a thumbnail before it sends one: that it is a PNG or a JPEG the platform can decode (anything else is
    /// refused there, so it is refused here, and nothing is sent), that it is no larger than the platform accepts, and, as
    /// <b>warnings only</b>, that it is big enough and 16:9 to look right where it is shown. A thumbnail is never refused for its size or
    /// shape: the platform fits it into the sizes it needs and does not reject any. It also says whether a JPEG can be stored as it is
    /// (<see cref="StoredAsIs"/>), which is also what the SDK's preparation of an image decides on. The size is read from the file's own header, not
    /// from Unity's import of it, so the check needs no texture and works in batch mode.
    /// </summary>
    internal sealed class ThumbnailCheck : FileCheckResult
    {
        /// <summary>The largest thumbnail the platform accepts, in bytes.</summary>
        public const long MaxBytes = 10L * 1024 * 1024;

        public const int MinWidth = 1280;
        public const int MinHeight = 720;

        /// <summary>How far the aspect ratio may be from 16:9 before it is worth a warning, as a fraction.</summary>
        public const double AspectTolerance = 0.01;

        /// <summary><c>png</c> or <c>jpeg</c>; null when the file is neither.</summary>
        public string Format;

        /// <summary>The <c>Content-Type</c> the file is sent with: <c>image/png</c> or <c>image/jpeg</c>; null when it is neither.</summary>
        public string ContentType;

        public int Width;
        public int Height;

        /// <summary>The colour components of a JPEG (1 greyscale, 3 colour, 4 CMYK); 0 for a PNG.</summary>
        public int Components;

        /// <summary>The EXIF orientation of a JPEG (1 to 8); 0 when it has none.</summary>
        public int ExifOrientation;

        /// <summary>The largest hero image the platform keeps, and so the largest the SDK sends.</summary>
        public const int MaxWidth = 2560;

        public const int MaxHeight = 1440;

        /// <summary>
        /// True for a JPEG the platform's <c>JpegHeader.TryReadPlain</c> accepts: SOI; before the scan only APP0 "JFIF", DQT, DHT, DRI and exactly one
        /// SOF0 (8 bits, one or three components); in the scan only entropy-coded bytes (FF 00, restart markers, fill); the last two bytes FF D9 and nothing
        /// after. No EXIF, XMP, ICC or other APPn, no comment, no progressive or extended frame.
        /// </summary>
        public bool PlainBaseline;

        /// <summary>
        /// True for a JPEG the platform keeps byte for byte as the hero image: <see cref="PlainBaseline"/> and within
        /// <see cref="MaxWidth"/> x <see cref="MaxHeight"/>. The SDK sends such a file unchanged and re-encodes anything else (the
        /// platform would re-encode it too, and the hash the SDK compares would then be another file's).
        /// </summary>
        public bool StoredAsIs => Format == "jpeg" && PlainBaseline && Width <= MaxWidth && Height <= MaxHeight;

        public static ThumbnailCheck Check(byte[] bytes)
        {
            ThumbnailCheck result = new();

            if (bytes == null || bytes.Length == 0)
            {
                result.Error("empty", "The file is empty.");
                return result;
            }

            if (bytes.Length > MaxBytes)
                result.Error("too-large", $"The file is {bytes.Length / (1024.0 * 1024.0):0.#} MB: the platform accepts a thumbnail of at most {MaxBytes / (1024 * 1024)} MB.");

            bool ok = IsPng(bytes)
                ? ReadPng(bytes, result)
                : IsJpeg(bytes) && ReadJpeg(bytes, result);

            if (!ok)
            {
                result.Format = null;
                result.ContentType = null;
                result.Error("not-an-image", "The file is not a PNG or JPEG image.");
                return result;
            }

            if (result.Width < MinWidth || result.Height < MinHeight)
                result.Warn("low-resolution", $"This image is smaller than {MinWidth} × {MinHeight}: it may look blurred.");

            double aspect = (double)result.Width / result.Height;
            if (Math.Abs(aspect - 16.0 / 9.0) / (16.0 / 9.0) > AspectTolerance)
                result.Warn("aspect", "This image is not 16:9: the card crops it.");

            return result;
        }

        private static bool IsPng(byte[] b) =>
            b.Length >= 8 && b[0] == 0x89 && b[1] == 0x50 && b[2] == 0x4E && b[3] == 0x47 && b[4] == 0x0D && b[5] == 0x0A && b[6] == 0x1A && b[7] == 0x0A;

        private static bool IsJpeg(byte[] b) => b.Length >= 3 && b[0] == 0xFF && b[1] == 0xD8 && b[2] == 0xFF;

        // The IHDR chunk is always first: length (4), "IHDR" (4), width (4), height (4).
        private static bool ReadPng(byte[] b, ThumbnailCheck result)
        {
            if (b.Length < 24 || b[12] != (byte)'I' || b[13] != (byte)'H' || b[14] != (byte)'D' || b[15] != (byte)'R')
                return false;

            result.Width = ReadInt32(b, 16);
            result.Height = ReadInt32(b, 20);
            result.Format = "png";
            result.ContentType = "image/png";
            result.Components = 0;
            return result.Width > 0 && result.Height > 0;
        }

        // Walks the marker segments up to the first start-of-frame, which carries the size.
        private static bool ReadJpeg(byte[] b, ThumbnailCheck result)
        {
            int i = 2;
            while (i + 3 < b.Length)
            {
                if (b[i] != 0xFF)
                {
                    i++;
                    continue;
                }

                byte marker = b[i + 1];
                if (marker == 0xFF)
                {
                    i++;
                    continue;
                }

                // Markers with no length: padding, restart markers, start of image.
                if (marker == 0x00 || marker == 0x01 || marker == 0xD8 || (marker >= 0xD0 && marker <= 0xD7))
                {
                    i += 2;
                    continue;
                }

                if (marker == 0xD9 || marker == 0xDA)
                    return false;

                int length = (b[i + 2] << 8) | b[i + 3];
                if (marker == 0xE1 && length >= 8 && i + 2 + length <= b.Length)
                {
                    int orientation = ReadExifOrientation(b, i + 4, length - 2);
                    if (orientation > 0)
                        result.ExifOrientation = orientation;
                }

                bool startOfFrame = marker >= 0xC0 && marker <= 0xCF && marker != 0xC4 && marker != 0xC8 && marker != 0xCC;
                if (startOfFrame)
                {
                    if (i + 10 >= b.Length)
                        return false;

                    result.Height = (b[i + 5] << 8) | b[i + 6];
                    result.Width = (b[i + 7] << 8) | b[i + 8];
                    result.Components = b[i + 9];
                    result.PlainBaseline = IsPlainBaseline(b);
                    result.Format = "jpeg";
                    result.ContentType = "image/jpeg";
                    return result.Width > 0 && result.Height > 0;
                }

                if (length < 2)
                    return false;

                i += 2 + length;
            }

            return false;
        }

        // The platform's rule for keeping an upload as the hero image byte for byte (Virtuademy-WebAPI-Application, Helpers/JpegHeader.TryReadPlain), ported:
        // 1. SOI. 2. Up to the first SOS only APP0 "JFIF\0", DQT, DHT, DRI and exactly one SOF0, each with a length of at least 2 inside the file; any other
        // marker, a fill byte or a stray byte there is not allowed. 3. The SOF0 has 8 bits, 1 or 3 components, a width and a height of at least 1, and comes
        // before the SOS. 4. After the SOS header only entropy-coded bytes: FF may be followed only by 00, D0-D7 or another FF. 5. The last two bytes are FF D9,
        // and no FF D9 occurs earlier as a marker. The size limit is the caller's (StoredAsIs).
        private static bool IsPlainBaseline(byte[] data)
        {
            if (data.Length < 4 || data[0] != 0xFF || data[1] != 0xD8)
                return false;

            bool frameSeen = false;
            int position = 2;

            while (true)
            {
                if (position + 4 > data.Length || data[position] != 0xFF)
                    return false;

                byte marker = data[position + 1];
                int length = (data[position + 2] << 8) | data[position + 3];
                if (length < 2 || position + 2 + length > data.Length)
                    return false;

                int payload = position + 4;
                int payloadLength = length - 2;

                switch (marker)
                {
                    case 0xE0:
                        if (payloadLength < 5 || data[payload] != (byte)'J' || data[payload + 1] != (byte)'F' || data[payload + 2] != (byte)'I' || data[payload + 3] != (byte)'F' || data[payload + 4] != 0)
                            return false;
                        break;
                    case 0xDB:
                    case 0xC4:
                    case 0xDD:
                        break;
                    case 0xC0:
                        if (frameSeen || payloadLength < 6)
                            return false;

                        int height = (data[payload + 1] << 8) | data[payload + 2];
                        int width = (data[payload + 3] << 8) | data[payload + 4];
                        int components = data[payload + 5];
                        if (data[payload] != 8 || (components != 1 && components != 3) || width < 1 || height < 1)
                            return false;

                        frameSeen = true;
                        break;
                    case 0xDA:
                        return frameSeen && ScanEndsTheFile(data, position + 2 + length);
                    default:
                        return false;
                }

                position += 2 + length;
            }
        }

        // From the first byte after the SOS header to the end: entropy-coded data, then FF D9 as the last two bytes.
        private static bool ScanEndsTheFile(byte[] data, int position)
        {
            while (position < data.Length)
            {
                if (data[position] != 0xFF)
                {
                    position++;
                    continue;
                }

                if (position + 1 >= data.Length)
                    return false;

                byte code = data[position + 1];
                if (code == 0xD9)
                    return position + 2 == data.Length;

                if (code == 0x00 || (code >= 0xD0 && code <= 0xD7))
                    position += 2;
                else if (code == 0xFF)
                    position++;
                else
                    return false;
            }

            return false;
        }

        // The APP1 segment of a camera file: "Exif  ", a TIFF header, and in the first directory the Orientation tag (0x0112).
        private static int ReadExifOrientation(byte[] b, int start, int length)
        {
            if (length < 14 || b[start] != (byte)'E' || b[start + 1] != (byte)'x' || b[start + 2] != (byte)'i' || b[start + 3] != (byte)'f' || b[start + 4] != 0)
                return 0;

            int tiff = start + 6;
            int end = start + length;
            bool little = b[tiff] == (byte)'I' && b[tiff + 1] == (byte)'I';
            if (!little && !(b[tiff] == (byte)'M' && b[tiff + 1] == (byte)'M'))
                return 0;

            int U16(int at) => little ? b[at] | (b[at + 1] << 8) : (b[at] << 8) | b[at + 1];
            long U32(int at) => little
                ? b[at] | ((long)b[at + 1] << 8) | ((long)b[at + 2] << 16) | ((long)b[at + 3] << 24)
                : ((long)b[at] << 24) | ((long)b[at + 1] << 16) | ((long)b[at + 2] << 8) | b[at + 3];

            long ifd = tiff + U32(tiff + 4);
            if (ifd < tiff || ifd + 2 > end)
                return 0;

            int count = U16((int)ifd);
            for (int e = 0; e < count; e++)
            {
                int entry = (int)ifd + 2 + e * 12;
                if (entry + 12 > end)
                    return 0;

                if (U16(entry) == 0x0112)
                {
                    int value = U16(entry + 8);
                    return value >= 1 && value <= 8 ? value : 0;
                }
            }

            return 0;
        }

        private static int ReadInt32(byte[] b, int offset) => (b[offset] << 24) | (b[offset + 1] << 16) | (b[offset + 2] << 8) | b[offset + 3];
    }
}
