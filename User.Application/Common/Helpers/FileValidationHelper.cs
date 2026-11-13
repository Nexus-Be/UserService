namespace User.Application.Common.Helpers;

using System;
using System.Collections.Generic;
using System.IO;
using User.Domain.Exceptions;

public static class FileValidationHelper
{
    private static readonly HashSet<string> ImageExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg", ".jpeg", ".png", ".webp"
    };

    private static readonly HashSet<string> DocumentExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg", ".jpeg", ".png", ".pdf"
    };

    private static readonly HashSet<string> VideoExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".mp4", ".webm"
    };

    public static void ValidateImage(Stream stream, string fileName, long sizeBytes, int maxSizeMb, string invalidTypeMessage)
    {
        ValidateBase(stream, fileName, sizeBytes, maxSizeMb);
        var ext = Path.GetExtension(fileName);
        if (!ImageExtensions.Contains(ext) || !HasValidMagicBytes(stream, ext))
        {
            throw new BusinessRuleValidationException("INVALID_FILE_TYPE", invalidTypeMessage);
        }
    }

    public static void ValidateKycDocument(Stream stream, string fileName, long sizeBytes, int maxSizeMb)
    {
        ValidateBase(stream, fileName, sizeBytes, maxSizeMb);
        var ext = Path.GetExtension(fileName);
        if (!DocumentExtensions.Contains(ext) || !HasValidMagicBytes(stream, ext))
        {
            throw new BusinessRuleValidationException("INVALID_FILE_TYPE", "Invalid document format, only JPG, PNG, and PDF are accepted.");
        }
    }

    public static bool ValidateReportMedia(Stream stream, string fileName, long sizeBytes, int maxImageMb, int maxVideoMb)
    {
        if (stream == null || string.IsNullOrWhiteSpace(fileName) || sizeBytes <= 0)
        {
            throw new BusinessRuleValidationException("VAL_INVALID_INPUT", "Uploaded file is empty or invalid.");
        }

        var ext = Path.GetExtension(fileName);
        if (ImageExtensions.Contains(ext))
        {
            if (sizeBytes > maxImageMb * 1024L * 1024L)
            {
                throw new DomainException("FILE_TOO_LARGE", $"File size exceeds maximum allowed limit of {maxImageMb}MB.", 413);
            }
            if (!HasValidMagicBytes(stream, ext))
            {
                throw new BusinessRuleValidationException("INVALID_FILE_TYPE", "Invalid file format, only JPG, PNG, WEBP, MP4, and WEBM are accepted.");
            }
            return false; // isVideo = false
        }

        if (VideoExtensions.Contains(ext))
        {
            if (sizeBytes > maxVideoMb * 1024L * 1024L)
            {
                throw new DomainException("FILE_TOO_LARGE", $"Video size exceeds maximum allowed limit of {maxVideoMb}MB.", 413);
            }
            if (!HasValidMagicBytes(stream, ext))
            {
                throw new BusinessRuleValidationException("INVALID_FILE_TYPE", "Invalid file format, only JPG, PNG, WEBP, MP4, and WEBM are accepted.");
            }
            return true; // isVideo = true
        }

        throw new BusinessRuleValidationException("INVALID_FILE_TYPE", "Invalid file format, only JPG, PNG, WEBP, MP4, and WEBM are accepted.");
    }

    private static void ValidateBase(Stream stream, string fileName, long sizeBytes, int maxSizeMb)
    {
        if (stream == null || string.IsNullOrWhiteSpace(fileName) || sizeBytes <= 0)
        {
            throw new BusinessRuleValidationException("VAL_INVALID_INPUT", "Uploaded file is empty or invalid.");
        }

        if (sizeBytes > maxSizeMb * 1024L * 1024L)
        {
            throw new DomainException("FILE_TOO_LARGE", $"File size exceeds maximum allowed limit of {maxSizeMb}MB.", 413);
        }
    }

    private static bool HasValidMagicBytes(Stream stream, string ext)
    {
        if (!stream.CanSeek)
        {
            return false;
        }

        var originalPos = stream.Position;
        try
        {
            Span<byte> header = stackalloc byte[12];
            var read = stream.Read(header);
            if (read < 4)
            {
                return false;
            }

            return ext.ToLowerInvariant() switch
            {
                ".jpg" or ".jpeg" => header[0] == 0xFF && header[1] == 0xD8 && header[2] == 0xFF,
                ".png" => header[0] == 0x89 && header[1] == 0x50 && header[2] == 0x4E && header[3] == 0x47,
                ".webp" => read >= 12
                    && header[0] == 0x52 && header[1] == 0x49 && header[2] == 0x46 && header[3] == 0x46
                    && header[8] == 0x57 && header[9] == 0x45 && header[10] == 0x42 && header[11] == 0x50,
                ".pdf" => header[0] == 0x25 && header[1] == 0x50 && header[2] == 0x44 && header[3] == 0x46,
                ".mp4" => read >= 8 && header[4] == 0x66 && header[5] == 0x74 && header[6] == 0x79 && header[7] == 0x70,
                ".webm" => header[0] == 0x1A && header[1] == 0x45 && header[2] == 0xDF && header[3] == 0xA3,
                _ => false
            };
        }
        finally
        {
            stream.Position = originalPos;
        }
    }
}
