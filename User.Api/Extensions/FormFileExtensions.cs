namespace User.Api.Extensions;

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using User.Application.Common.Models;

public static class FormFileExtensions
{
    public static async Task<IActionResult> ExecuteWithMediaFilesAsync(
        this IEnumerable<IFormFile>? files,
        Func<IReadOnlyList<UploadMediaFileInput>, Task<IActionResult>> action)
    {
        var openedStreams = new List<Stream>();
        try
        {
            var inputs = new List<UploadMediaFileInput>();
            if (files != null)
            {
                foreach (var file in files)
                {
                    var stream = file.OpenReadStream();
                    openedStreams.Add(stream);
                    inputs.Add(new UploadMediaFileInput(stream, file.FileName, file.ContentType, file.Length));
                }
            }

            return await action(inputs);
        }
        finally
        {
            foreach (var stream in openedStreams)
            {
                await stream.DisposeAsync();
            }
        }
    }
}
