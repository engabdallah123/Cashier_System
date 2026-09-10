using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.StaticFiles;
using POS.Shared.Application.IService;
using POS.Shared.Domain;

namespace POS.Shared.Infrastructure.Services
{
    public class FileService : IFileService
    {
        private readonly IWebHostEnvironment _webHostEnvironment;

        public FileService(IWebHostEnvironment webHostEnvironment)
        {
            _webHostEnvironment = webHostEnvironment;
        }

        private string GetUploadsRootPath()
        {
            try
            {
                var commonAppData = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
                if (!string.IsNullOrWhiteSpace(commonAppData))
                {
                    var uploadsDir = Path.Combine(commonAppData, "POS Cashier System", "uploads");
                    if (!Directory.Exists(uploadsDir))
                    {
                        Directory.CreateDirectory(uploadsDir);
                    }
                    return uploadsDir;
                }
            }
            catch
            {
                // Fallback if CommonApplicationData cannot be accessed
            }

            try
            {
                var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                if (!string.IsNullOrWhiteSpace(localAppData))
                {
                    var uploadsDir = Path.Combine(localAppData, "POS Cashier System", "uploads");
                    if (!Directory.Exists(uploadsDir))
                    {
                        Directory.CreateDirectory(uploadsDir);
                    }
                    return uploadsDir;
                }
            }
            catch
            {
                // Fallback
            }

            return GetWebRootPath();
        }

        private string GetWebRootPath()
        {
            var webRoot = _webHostEnvironment.WebRootPath;
            if (string.IsNullOrWhiteSpace(webRoot))
            {
                webRoot = Path.Combine(_webHostEnvironment.ContentRootPath, "wwwroot");
            }

            if (!Directory.Exists(webRoot))
            {
                try
                {
                    Directory.CreateDirectory(webRoot);
                }
                catch
                {
                    // Ignore if read-only (e.g. Program Files)
                }
            }

            return webRoot;
        }

        private string ResolvePhysicalPath(string relativePath)
        {
            var cleanPath = relativePath.TrimStart('/', '\\').Replace('\\', '/');

            // 1. Check in CommonApplicationData uploads
            var uploadsRoot = GetUploadsRootPath();
            if (cleanPath.StartsWith("uploads/", StringComparison.OrdinalIgnoreCase))
            {
                var subPath = cleanPath["uploads/".Length..];
                var candidate = Path.Combine(uploadsRoot, subPath.Replace('/', Path.DirectorySeparatorChar));
                if (File.Exists(candidate))
                    return candidate;
            }
            else
            {
                var candidate = Path.Combine(uploadsRoot, cleanPath.Replace('/', Path.DirectorySeparatorChar));
                if (File.Exists(candidate))
                    return candidate;
            }

            // 2. Check in wwwroot
            var webRoot = GetWebRootPath();
            var webCandidate = Path.Combine(webRoot, cleanPath.Replace('/', Path.DirectorySeparatorChar));
            if (File.Exists(webCandidate))
                return webCandidate;

            // 3. Fallback expected destination in uploads root
            if (cleanPath.StartsWith("uploads/", StringComparison.OrdinalIgnoreCase))
            {
                var subPath = cleanPath["uploads/".Length..];
                return Path.Combine(uploadsRoot, subPath.Replace('/', Path.DirectorySeparatorChar));
            }

            return Path.Combine(uploadsRoot, cleanPath.Replace('/', Path.DirectorySeparatorChar));
        }

        public async Task<Result> DeleteFileAsync(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath))
                return Result.Failure(new Error("File.Path.Empty", "File path is empty"));

            try
            {
                var fullPath = ResolvePhysicalPath(filePath);

                if (File.Exists(fullPath))
                    File.Delete(fullPath);

                return Result.Success();
            }
            catch (Exception ex)
            {
                return Result.Failure(
                    new Error("File.DeleteFailed", ex.Message));
            }
        }

        public async Task<Result> DeleteAllFilesAsync(List<string> filePaths)
        {
            if (filePaths == null || !filePaths.Any())
                return Result.Failure(new Error("File.List.Empty", "Files list is empty"));

            foreach (var filePath in filePaths)
            {
                var result = await DeleteFileAsync(filePath);

                if (!result.IsSuccess)
                    return result;
            }

            return Result.Success();
        }

        public async Task<Result<string>> UploadFileAsync(IFormFile file, string folder)
        {
            if (file == null || file.Length == 0)
                return Result<string>.Failure(
                    new Error("File.Invalid", "file is empty"));

            try
            {
                var cleanFolder = (folder ?? string.Empty).TrimStart('/', '\\').Replace('\\', '/');
                string subFolder;
                if (cleanFolder.StartsWith("uploads/", StringComparison.OrdinalIgnoreCase))
                {
                    subFolder = cleanFolder["uploads/".Length..];
                }
                else if (string.Equals(cleanFolder, "uploads", StringComparison.OrdinalIgnoreCase))
                {
                    subFolder = string.Empty;
                }
                else
                {
                    subFolder = cleanFolder;
                }

                var uploadsRoot = GetUploadsRootPath();
                var targetDir = string.IsNullOrWhiteSpace(subFolder)
                    ? uploadsRoot
                    : Path.Combine(uploadsRoot, subFolder.Replace('/', Path.DirectorySeparatorChar));

                if (!Directory.Exists(targetDir))
                    Directory.CreateDirectory(targetDir);

                var extension = Path.GetExtension(file.FileName);
                if (string.IsNullOrWhiteSpace(extension)) extension = ".jpg";
                var fileName = $"{Guid.NewGuid()}{extension}";

                var fullPath = Path.Combine(targetDir, fileName);

                using (var fileStream = new FileStream(fullPath, FileMode.Create, FileAccess.Write, FileShare.None))
                {
                    await file.CopyToAsync(fileStream);
                }

                var relativeUrl = string.IsNullOrWhiteSpace(subFolder)
                    ? $"uploads/{fileName}"
                    : $"uploads/{subFolder.Trim('/')}/{fileName}";

                return Result<string>.Success(relativeUrl);
            }
            catch (Exception ex)
            {
                return Result<string>.Failure(
                    new Error("File.UploadFailed", ex.Message));
            }
        }

        public async Task<Result<byte[]>> GetFileAsByteArrayAsync(string imageSrc)
        {
            if (string.IsNullOrWhiteSpace(imageSrc))
                return Result<byte[]>.Failure(
                    new Error("File.Path.Empty", "image path is empty"));

            try
            {
                var fullPath = ResolvePhysicalPath(imageSrc);

                if (!File.Exists(fullPath))
                    return Result<byte[]>.Failure(
                        new Error("File.NotFound", "file not found"));

                var bytes = await File.ReadAllBytesAsync(fullPath);

                return Result<byte[]>.Success(bytes);
            }
            catch (Exception ex)
            {
                return Result<byte[]>.Failure(
                    new Error("File.ReadFailed", ex.Message));
            }
        }

        public async Task<Result<IFormFile>> GetFileAsIFormFileAsync(string imageSrc)
        {
            if (string.IsNullOrWhiteSpace(imageSrc))
                return Result<IFormFile>.Failure(
                    new Error("File.Path.Empty", "image path is empty"));

            try
            {
                var fullPath = ResolvePhysicalPath(imageSrc);

                if (!File.Exists(fullPath))
                    return Result<IFormFile>.Failure(
                        new Error("File.NotFound", "file not found"));

                var memoryStream = new MemoryStream(
                    await File.ReadAllBytesAsync(fullPath));

                var provider = new FileExtensionContentTypeProvider();

                if (!provider.TryGetContentType(fullPath, out var contentType))
                    contentType = "application/octet-stream";

                IFormFile formFile = new FormFile(
                    memoryStream,
                    0,
                    memoryStream.Length,
                    "file",
                    Path.GetFileName(fullPath))
                {
                    Headers = new HeaderDictionary(),
                    ContentType = contentType
                };

                return Result<IFormFile>.Success(formFile);
            }
            catch (Exception ex)
            {
                return Result<IFormFile>.Failure(
                    new Error("File.ReadFailed", ex.Message));
            }
        }

        public async Task<Result<string>> UploadFileWithUrlAsync(
            IFormFile file,
            string folder,
            string baseUrl)
        {
            var uploadResult = await UploadFileAsync(file, folder);

            if (!uploadResult.IsSuccess)
                return Result<string>.Failure(uploadResult.Error);

            var relativePath = uploadResult.Value;

            var publicUrl = $"{baseUrl.TrimEnd('/')}/{relativePath.TrimStart('/')}";

            return Result<string>.Success(publicUrl);
        }
    }
}