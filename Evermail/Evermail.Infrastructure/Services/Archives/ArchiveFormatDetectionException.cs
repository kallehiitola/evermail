using System;

namespace Evermail.Infrastructure.Services.Archives;

public class ArchiveFormatDetectionException : Exception
{
    public ArchiveFormatDetectionException(string message)
        : base(message)
    {
    }

    public ArchiveFormatDetectionException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}



