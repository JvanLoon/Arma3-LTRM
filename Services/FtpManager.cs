using System.IO;
using System.Net;
using System.Text;
using System.Windows;
using Arma_3_LTRM.Models;
using MessageBox = System.Windows.MessageBox;

namespace Arma_3_LTRM.Services
{
    public class FtpManager
    {
        private class FtpItem
        {
            public string Name { get; set; } = string.Empty;
            public string FullPath { get; set; } = string.Empty;
            public bool IsDirectory { get; set; }
            public long Size { get; set; }
            public DateTime LastModified { get; set; }
        }

        private class FtpDirectoryCache
        {
            public Dictionary<string, List<FtpItem>> Cache { get; } = new Dictionary<string, List<FtpItem>>();
        }

        private int _skippedFiles = 0;
        private int _downloadedFiles = 0;

        public async Task<bool> DownloadRepositoryAsync(Repository repository, string destinationPath, IProgress<string>? progress = null)
        {
            try
            {
                _skippedFiles = 0;
                _downloadedFiles = 0;

                progress?.Report($"Connecting to {repository.Url}:{repository.Port}...");

                var ftpUri = new Uri($"ftp://{repository.Url}:{repository.Port}/");
                
                if (!TestConnection(repository))
                {
                    progress?.Report("Failed to connect to repository.");
                    MessageBox.Show($"Cannot connect to repository '{repository.Name}'. Please check the connection settings.", 
                        "Connection Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    return false;
                }

                progress?.Report("Connection successful. Building folder structure cache...");
                
                // Build complete directory cache upfront
                var cache = await Task.Run(() => BuildDirectoryCache(ftpUri, repository.Username, repository.Password, "/", progress));
                
                progress?.Report($"Cache built. Found {cache.Cache.Values.Sum(x => x.Count(i => !i.IsDirectory))} files in {cache.Cache.Count} directories.");
                progress?.Report("Starting download...");
                
                await Task.Run(() => DownloadDirectoryFromCache(cache, "/", repository.Username, repository.Password, destinationPath, ftpUri.Host, ftpUri.Port, progress));
                
                progress?.Report($"Sync completed: {_downloadedFiles} files downloaded, {_skippedFiles} files up-to-date");
                return true;
            }
            catch (Exception ex)
            {
                progress?.Report($"Error: {ex.Message}");
                MessageBox.Show($"Failed to download from repository '{repository.Name}': {ex.Message}", 
                    "Download Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }
        }

        private FtpDirectoryCache BuildDirectoryCache(Uri ftpUri, string username, string password, string currentPath, IProgress<string>? progress)
        {
            var cache = new FtpDirectoryCache();
            BuildDirectoryCacheRecursive(ftpUri, username, password, currentPath, cache, progress);
            return cache;
        }

        private void BuildDirectoryCacheRecursive(Uri baseUri, string username, string password, string currentPath, FtpDirectoryCache cache, IProgress<string>? progress)
        {
            try
            {
                var ftpUri = new Uri($"{baseUri.Scheme}://{baseUri.Host}:{baseUri.Port}{currentPath}");
                if (!ftpUri.AbsolutePath.EndsWith("/"))
                {
                    ftpUri = new Uri(ftpUri.ToString() + "/");
                }

                progress?.Report($"Scanning: {currentPath}");

                var items = GetDirectoryListingOptimized(ftpUri, username, password, currentPath);
                cache.Cache[currentPath] = items;

                foreach (var item in items.Where(i => i.IsDirectory && i.Name != "." && i.Name != ".."))
                {
                    BuildDirectoryCacheRecursive(baseUri, username, password, item.FullPath, cache, progress);
                }
            }
            catch (Exception ex)
            {
                progress?.Report($"Error scanning {currentPath}: {ex.Message}");
            }
        }

        private void DownloadDirectoryFromCache(FtpDirectoryCache cache, string currentPath, string username, string password, string destinationPath, string ftpHost, int ftpPort, IProgress<string>? progress)
        {
            try
            {
                if (!Directory.Exists(destinationPath))
                {
                    Directory.CreateDirectory(destinationPath);
                    progress?.Report($"Created directory: {Path.GetFileName(destinationPath)}");
                }

                if (!cache.Cache.TryGetValue(currentPath, out var items))
                    return;

                foreach (var item in items)
                {
                    if (item.Name == "." || item.Name == "..")
                        continue;

                    var localPath = Path.Combine(destinationPath, item.Name);

                    if (item.IsDirectory)
                    {
                        DownloadDirectoryFromCache(cache, item.FullPath, username, password, localPath, ftpHost, ftpPort, progress);
                    }
                    else
                    {
                        if (ShouldDownloadFile(localPath, item.Size, item.LastModified))
                        {
                            progress?.Report($"Downloading: {item.Name} ({FormatFileSize(item.Size)})");
                            var itemUri = new Uri($"ftp://{ftpHost}:{ftpPort}{item.FullPath}");
                            DownloadFileWithProgress(itemUri, username, password, localPath, item.Size, item.LastModified, progress);
                            _downloadedFiles++;
                        }
                        else
                        {
                            _skippedFiles++;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                progress?.Report($"Error downloading {currentPath}: {ex.Message}");
            }
        }

        private List<FtpItem> GetDirectoryListingOptimized(Uri ftpUri, string username, string password, string currentPath)
        {
            var items = new List<FtpItem>();

            try
            {
                var detailsRequest = (FtpWebRequest)WebRequest.Create(ftpUri);
                detailsRequest.Method = WebRequestMethods.Ftp.ListDirectoryDetails;
                detailsRequest.Credentials = new NetworkCredential(username, password);
                detailsRequest.UseBinary = true;
                detailsRequest.KeepAlive = false;

                using var detailsResponse = (FtpWebResponse)detailsRequest.GetResponse();
                using var detailsStream = detailsResponse.GetResponseStream();
                using var detailsReader = new StreamReader(detailsStream);

                while (!detailsReader.EndOfStream)
                {
                    var line = detailsReader.ReadLine();
                    if (string.IsNullOrEmpty(line))
                        continue;

                    var item = ParseListLineOptimized(line, currentPath);
                    if (item != null && item.Name != "." && item.Name != "..")
                    {
                        items.Add(item);
                    }
                }
            }
            catch
            {
                try
                {
                    var listRequest = (FtpWebRequest)WebRequest.Create(ftpUri);
                    listRequest.Method = WebRequestMethods.Ftp.ListDirectory;
                    listRequest.Credentials = new NetworkCredential(username, password);
                    listRequest.UseBinary = true;
                    listRequest.KeepAlive = false;

                    using var listResponse = (FtpWebResponse)listRequest.GetResponse();
                    using var listStream = listResponse.GetResponseStream();
                    using var listReader = new StreamReader(listStream);

                    while (!listReader.EndOfStream)
                    {
                        var fileName = listReader.ReadLine();
                        if (string.IsNullOrEmpty(fileName) || fileName == "." || fileName == "..")
                            continue;

                        var itemPath = currentPath.TrimEnd('/') + "/" + fileName;
                        var itemUri = new Uri(ftpUri, fileName);
                        var item = new FtpItem 
                        { 
                            Name = fileName,
                            FullPath = itemPath
                        };

                        try
                        {
                            var sizeRequest = (FtpWebRequest)WebRequest.Create(itemUri);
                            sizeRequest.Method = WebRequestMethods.Ftp.GetFileSize;
                            sizeRequest.Credentials = new NetworkCredential(username, password);
                            sizeRequest.UseBinary = true;
                            sizeRequest.KeepAlive = false;
                            sizeRequest.Timeout = 5000;

                            using var sizeResponse = (FtpWebResponse)sizeRequest.GetResponse();
                            item.Size = sizeResponse.ContentLength;
                            item.IsDirectory = false;
                            item.LastModified = DateTime.MinValue;
                        }
                        catch
                        {
                            item.IsDirectory = true;
                            item.Size = 0;
                            item.LastModified = DateTime.MinValue;
                        }

                        items.Add(item);
                    }
                }
                catch
                {
                }
            }

            return items;
        }

        private FtpItem? ParseListLineOptimized(string line, string currentPath)
        {
            try
            {
                if (line.Length < 10)
                    return null;

                var parts = line.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length < 9)
                    return null;

                var item = new FtpItem();
                
                if (line[0] == 'd')
                {
                    item.IsDirectory = true;
                    item.Size = 0;
                }
                else
                {
                    item.IsDirectory = false;
                    if (parts.Length > 4 && long.TryParse(parts[4], out long size))
                    {
                        item.Size = size;
                    }
                }

                item.Name = string.Join(" ", parts.Skip(8));
                item.FullPath = currentPath.TrimEnd('/') + "/" + item.Name;
                item.LastModified = DateTime.MinValue;

                try
                {
                    if (parts.Length >= 8)
                    {
                        var dateStr = $"{parts[5]} {parts[6]} {parts[7]}";
                        if (DateTime.TryParse(dateStr, out DateTime parsed))
                        {
                            item.LastModified = parsed;
                        }
                    }
                }
                catch
                {
                }

                return item;
            }
            catch
            {
                return null;
            }
        }

        private void DownloadDirectory(Uri ftpUri, string username, string password, string destinationPath, IProgress<string>? progress)
        {
            try
            {
                if (!Directory.Exists(destinationPath))
                {
                    Directory.CreateDirectory(destinationPath);
                    progress?.Report($"Created directory: {Path.GetFileName(destinationPath)}");
                }

                var items = GetDirectoryListing(ftpUri, username, password);

                foreach (var item in items)
                {
                    if (item.Name == "." || item.Name == "..")
                        continue;

                    var itemUri = new Uri(ftpUri, item.Name);
                    var localPath = Path.Combine(destinationPath, item.Name);

                    if (item.IsDirectory)
                    {
                        progress?.Report($"Entering directory: {item.Name}");
                        DownloadDirectory(new Uri(itemUri.ToString() + "/"), username, password, localPath, progress);
                    }
                    else
                    {
                        if (ShouldDownloadFile(localPath, item.Size, item.LastModified))
                        {
                            progress?.Report($"Downloading file: {item.Name} ({FormatFileSize(item.Size)})");
                            DownloadFileWithProgress(itemUri, username, password, localPath, item.Size, item.LastModified, progress);
                            _downloadedFiles++;
                        }
                        else
                        {
                            progress?.Report($"Skipping (up-to-date): {item.Name}");
                            _skippedFiles++;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                progress?.Report($"Error in directory {ftpUri.AbsolutePath}: {ex.Message}");
            }
        }

        private bool ShouldDownloadFile(string localPath, long remoteSize, DateTime remoteLastModified)
        {
            if (!File.Exists(localPath))
                return true;

            var localFileInfo = new FileInfo(localPath);
            
            if (localFileInfo.Length != remoteSize)
                return true;

            if (remoteLastModified != DateTime.MinValue && remoteLastModified > localFileInfo.LastWriteTime.AddSeconds(2))
                return true;

            return false;
        }

        private List<FtpItem> GetDirectoryListing(Uri ftpUri, string username, string password)
        {
            var items = new List<FtpItem>();

            try
            {
                var listRequest = (FtpWebRequest)WebRequest.Create(ftpUri);
                listRequest.Method = WebRequestMethods.Ftp.ListDirectory;
                listRequest.Credentials = new NetworkCredential(username, password);
                listRequest.UseBinary = true;
                listRequest.KeepAlive = false;

                using var listResponse = (FtpWebResponse)listRequest.GetResponse();
                using var listStream = listResponse.GetResponseStream();
                using var listReader = new StreamReader(listStream);

                while (!listReader.EndOfStream)
                {
                    var fileName = listReader.ReadLine();
                    if (string.IsNullOrEmpty(fileName) || fileName == "." || fileName == "..")
                        continue;

                    var itemUri = new Uri(ftpUri, fileName);
                    var item = new FtpItem { Name = fileName };

                    try
                    {
                        var sizeRequest = (FtpWebRequest)WebRequest.Create(itemUri);
                        sizeRequest.Method = WebRequestMethods.Ftp.GetFileSize;
                        sizeRequest.Credentials = new NetworkCredential(username, password);
                        sizeRequest.UseBinary = true;
                        sizeRequest.KeepAlive = false;

                        using var sizeResponse = (FtpWebResponse)sizeRequest.GetResponse();
                        item.Size = sizeResponse.ContentLength;
                        item.IsDirectory = false;

                        try
                        {
                            var timestampRequest = (FtpWebRequest)WebRequest.Create(itemUri);
                            timestampRequest.Method = WebRequestMethods.Ftp.GetDateTimestamp;
                            timestampRequest.Credentials = new NetworkCredential(username, password);
                            timestampRequest.UseBinary = true;
                            timestampRequest.KeepAlive = false;

                            using var timestampResponse = (FtpWebResponse)timestampRequest.GetResponse();
                            item.LastModified = timestampResponse.LastModified;
                        }
                        catch
                        {
                            item.LastModified = DateTime.MinValue;
                        }
                    }
                    catch
                    {
                        item.IsDirectory = true;
                        item.Size = 0;
                        item.LastModified = DateTime.MinValue;
                    }

                    items.Add(item);
                }
            }
            catch (Exception)
            {
                try
                {
                    var detailsRequest = (FtpWebRequest)WebRequest.Create(ftpUri);
                    detailsRequest.Method = WebRequestMethods.Ftp.ListDirectoryDetails;
                    detailsRequest.Credentials = new NetworkCredential(username, password);
                    detailsRequest.UseBinary = true;
                    detailsRequest.KeepAlive = false;

                    using var detailsResponse = (FtpWebResponse)detailsRequest.GetResponse();
                    using var detailsStream = detailsResponse.GetResponseStream();
                    using var detailsReader = new StreamReader(detailsStream);

                    while (!detailsReader.EndOfStream)
                    {
                        var line = detailsReader.ReadLine();
                        if (string.IsNullOrEmpty(line))
                            continue;

                        var item = ParseListLine(line);
                        if (item != null && item.Name != "." && item.Name != "..")
                        {
                            items.Add(item);
                        }
                    }
                }
                catch
                {
                }
            }

            return items;
        }

        private FtpItem? ParseListLine(string line)
        {
            try
            {
                if (line.Length < 10)
                    return null;

                var parts = line.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length < 9)
                    return null;

                var item = new FtpItem();
                
                if (line[0] == 'd')
                {
                    item.IsDirectory = true;
                    item.Size = 0;
                }
                else
                {
                    item.IsDirectory = false;
                    if (parts.Length > 4 && long.TryParse(parts[4], out long size))
                    {
                        item.Size = size;
                    }
                }

                item.Name = string.Join(" ", parts.Skip(8));
                item.LastModified = DateTime.MinValue;

                try
                {
                    if (parts.Length >= 8)
                    {
                        var dateStr = $"{parts[5]} {parts[6]} {parts[7]}";
                        if (DateTime.TryParse(dateStr, out DateTime parsed))
                        {
                            item.LastModified = parsed;
                        }
                    }
                }
                catch
                {
                }

                return item;
            }
            catch
            {
                return null;
            }
        }

        private void DownloadFileWithProgress(Uri ftpUri, string username, string password, string destinationPath, long fileSize, DateTime remoteTimestamp, IProgress<string>? progress)
        {
            try
            {
                var request = (FtpWebRequest)WebRequest.Create(ftpUri);
                request.Method = WebRequestMethods.Ftp.DownloadFile;
                request.Credentials = new NetworkCredential(username, password);
                request.UseBinary = true;
                request.KeepAlive = false;

                using var response = (FtpWebResponse)request.GetResponse();
                using var responseStream = response.GetResponseStream();
                using var fileStream = File.Create(destinationPath);

                byte[] buffer = new byte[8192];
                long totalBytesRead = 0;
                int bytesRead;
                int lastReportedPercent = -1;

                while ((bytesRead = responseStream.Read(buffer, 0, buffer.Length)) > 0)
                {
                    fileStream.Write(buffer, 0, bytesRead);
                    totalBytesRead += bytesRead;

                    if (fileSize > 0)
                    {
                        int currentPercent = (int)((totalBytesRead * 100) / fileSize);
                        if (currentPercent != lastReportedPercent && currentPercent % 25 == 0)
                        {
                            progress?.Report($"  Progress: {currentPercent}% ({FormatFileSize(totalBytesRead)}/{FormatFileSize(fileSize)})");
                            lastReportedPercent = currentPercent;
                        }
                    }
                }

                fileStream.Close();

                if (remoteTimestamp != DateTime.MinValue)
                {
                    File.SetLastWriteTime(destinationPath, remoteTimestamp);
                }

                progress?.Report($"  Completed: {Path.GetFileName(destinationPath)}");
            }
            catch (Exception ex)
            {
                progress?.Report($"  Failed to download {Path.GetFileName(destinationPath)}: {ex.Message}");
                throw new Exception($"Failed to download file from {ftpUri}: {ex.Message}", ex);
            }
        }

        private string FormatFileSize(long bytes)
        {
            string[] sizes = { "B", "KB", "MB", "GB" };
            double len = bytes;
            int order = 0;
            
            while (len >= 1024 && order < sizes.Length - 1)
            {
                order++;
                len = len / 1024;
            }

            return $"{len:0.##} {sizes[order]}";
        }

        public bool TestConnection(Repository repository)
        {
            try
            {
                var ftpUri = new Uri($"ftp://{repository.Url}:{repository.Port}/");
                var request = (FtpWebRequest)WebRequest.Create(ftpUri);
                request.Method = WebRequestMethods.Ftp.ListDirectory;
                request.Credentials = new NetworkCredential(repository.Username, repository.Password);
                request.Timeout = 10000;
                request.UseBinary = true;
                request.KeepAlive = false;

                using var response = (FtpWebResponse)request.GetResponse();
                return response.StatusCode == FtpStatusCode.OpeningData || 
                       response.StatusCode == FtpStatusCode.DataAlreadyOpen ||
                       response.StatusCode == FtpStatusCode.ClosingData;
            }
            catch
            {
                return false;
            }
        }

        public class FtpBrowseItem
        {
            public string Name { get; set; } = string.Empty;
            public string FullPath { get; set; } = string.Empty;
            public bool IsDirectory { get; set; }
        }

        public async Task<List<FtpBrowseItem>> BrowseDirectoryAsync(string ftpUrl, int port, string username, string password, string path = "/")
        {
            return await Task.Run(() =>
            {
                var items = new List<FtpBrowseItem>();
                try
                {
                    var ftpUri = new Uri($"ftp://{ftpUrl}:{port}{path}");
                    if (!ftpUri.AbsolutePath.EndsWith("/"))
                    {
                        ftpUri = new Uri(ftpUri.ToString() + "/");
                    }

                    var ftpItems = GetDirectoryListing(ftpUri, username, password);
                    
                    foreach (var item in ftpItems)
                    {
                        if (item.Name == "." || item.Name == "..")
                            continue;

                        var fullPath = path.TrimEnd('/') + "/" + item.Name;
                        
                        items.Add(new FtpBrowseItem
                        {
                            Name = item.Name,
                            FullPath = fullPath,
                            IsDirectory = item.IsDirectory
                        });
                    }
                }
                catch
                {
                    // Silently fail for browsing
                }
                return items;
            });
        }

        public async Task DownloadFolderAsync(string ftpUrl, int port, string username, string password, string remotePath, string localPath, IProgress<string>? progress = null)
        {
            await Task.Run(() =>
            {
                try
                {
                    _skippedFiles = 0;
                    _downloadedFiles = 0;

                    var ftpUri = new Uri($"ftp://{ftpUrl}:{port}{remotePath}");
                    if (!ftpUri.AbsolutePath.EndsWith("/"))
                    {
                        ftpUri = new Uri(ftpUri.ToString() + "/");
                    }

                    progress?.Report($"Building folder structure cache for: {remotePath}");
                    
                    var baseUri = new Uri($"ftp://{ftpUrl}:{port}/");
                    var cache = BuildDirectoryCache(baseUri, username, password, remotePath, progress);
                    
                    progress?.Report($"Cache built. Found {cache.Cache.Values.Sum(x => x.Count(i => !i.IsDirectory))} files.");
                    progress?.Report($"Downloading folder: {remotePath}");
                    
                    DownloadDirectoryFromCache(cache, remotePath, username, password, localPath, ftpUrl, port, progress);
                    
                    progress?.Report($"Folder download completed: {_downloadedFiles} files downloaded, {_skippedFiles} files up-to-date");
                }
                catch (Exception ex)
                {
                    progress?.Report($"Error downloading folder {remotePath}: {ex.Message}");
                }
            });
        }
    }
}
