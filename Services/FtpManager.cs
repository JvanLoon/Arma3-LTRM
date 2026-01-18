using System.IO;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows;
using System.Collections.Concurrent;
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
        private readonly int _maxConcurrentDownloads = 8;
        private readonly int _bufferSize = 65536; // 64KB buffer

        public async Task<bool> DownloadRepositoryAsync(Repository repository, string destinationPath, IProgress<string>? progress = null, CancellationToken cancellationToken = default)
        {
            try
            {
                _skippedFiles = 0;
                _downloadedFiles = 0;

                progress?.Report($"Connecting to {repository.Url}:{repository.Port}...");

                var ftpUri = new Uri($"ftp://{repository.Url}:{repository.Port}/");
                
                cancellationToken.ThrowIfCancellationRequested();
                
                if (!TestConnection(repository))
                {
                    progress?.Report("Failed to connect to repository.");
                    MessageBox.Show($"Cannot connect to repository '{repository.Name}'. Please check the connection settings.", 
                        "Connection Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    return false;
                }

                progress?.Report("Connection successful. Building folder structure cache...");
                
                cancellationToken.ThrowIfCancellationRequested();
                
                // Build complete directory cache upfront
                var cache = await Task.Run(() => BuildDirectoryCache(ftpUri, repository.Username, repository.Password, "/", progress, cancellationToken), cancellationToken);
                
                progress?.Report($"Cache built. Found {cache.Cache.Values.Sum(x => x.Count(i => !i.IsDirectory))} files in {cache.Cache.Count} directories.");
                progress?.Report("Starting download...");
                
                cancellationToken.ThrowIfCancellationRequested();
                
                await Task.Run(() => DownloadDirectoryFromCache(cache, "/", repository.Username, repository.Password, destinationPath, ftpUri.Host, ftpUri.Port, progress, cancellationToken), cancellationToken);
                
                progress?.Report($"Sync completed: {_downloadedFiles} files downloaded, {_skippedFiles} files up-to-date");
                return true;
            }
            catch (OperationCanceledException)
            {
                progress?.Report("Download cancelled by user.");
                return false;
            }
            catch (Exception ex)
            {
                progress?.Report($"Error: {ex.Message}");
                MessageBox.Show($"Failed to download from repository '{repository.Name}': {ex.Message}", 
                    "Download Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }
        }

        private FtpDirectoryCache BuildDirectoryCache(Uri ftpUri, string username, string password, string currentPath, IProgress<string>? progress, CancellationToken cancellationToken)
        {
            var cache = new FtpDirectoryCache();
            var processedPaths = new System.Collections.Concurrent.ConcurrentDictionary<string, bool>();
            var semaphore = new SemaphoreSlim(_maxConcurrentDownloads);
            
            ScanDirectoryRecursive(ftpUri, username, password, currentPath, cache, processedPaths, semaphore, progress, cancellationToken);
            
            return cache;
        }

        private void ScanDirectoryRecursive(Uri ftpUri, string username, string password, string path, 
            FtpDirectoryCache cache, System.Collections.Concurrent.ConcurrentDictionary<string, bool> processedPaths, 
            SemaphoreSlim semaphore, IProgress<string>? progress, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            
            // Use TryAdd to atomically check and add - prevents duplicate processing
            if (!processedPaths.TryAdd(path, true))
                return;

            semaphore.Wait(cancellationToken);
            try
            {
                var items = GetDirectoryListingOptimized(ftpUri, username, password, path);
                lock (cache.Cache)
                {
                    cache.Cache[path] = items;
                }
                progress?.Report($"Scanned: {path} ({items.Count} items)");

                // Release semaphore before recursing to allow parallel processing
                semaphore.Release();

                var subdirs = items.Where(i => i.IsDirectory && i.Name != "." && i.Name != "..").ToList();
                
                // Process subdirectories in parallel
                Parallel.ForEach(subdirs, new ParallelOptions { MaxDegreeOfParallelism = _maxConcurrentDownloads, CancellationToken = cancellationToken }, dir =>
                {
                    ScanDirectoryRecursive(ftpUri, username, password, dir.FullPath, cache, processedPaths, semaphore, progress, cancellationToken);
                });
            }
            catch (OperationCanceledException)
            {
                progress?.Report("Download cancelled by user.");
                semaphore.Release();
            }
            catch (Exception ex)
            {
                progress?.Report($"Error scanning {path}: {ex.Message}");
                semaphore.Release();
            }
        }

        private void DownloadDirectoryFromCache(FtpDirectoryCache cache, string currentPath, string username, string password, string destinationPath, string ftpHost, int ftpPort, IProgress<string>? progress, CancellationToken cancellationToken)
        {
            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                
                // Collect all files to download first
                var allFilesToDownload = new ConcurrentBag<(FtpItem file, string localPath)>();
                CollectFilesToDownload(cache, currentPath, destinationPath, allFilesToDownload);

                progress?.Report($"Found {allFilesToDownload.Count} files to check...");

                // Filter files that actually need downloading
                var filesToDownload = allFilesToDownload
                    .Where(f => ShouldDownloadFile(f.localPath, f.file.Size, f.file.LastModified))
                    .ToList();

                progress?.Report($"{filesToDownload.Count} files need to be downloaded, {allFilesToDownload.Count - filesToDownload.Count} files up-to-date");

                cancellationToken.ThrowIfCancellationRequested();
                
                // Download all files in parallel
                var semaphore = new SemaphoreSlim(_maxConcurrentDownloads);
                var downloadTasks = filesToDownload.Select(async fileInfo =>
                {
                    await semaphore.WaitAsync();
                    try
                    {
                        // Ensure directory exists
                        var directory = Path.GetDirectoryName(fileInfo.localPath);
                        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                        {
                            lock (cache)
                            {
                                Directory.CreateDirectory(directory);
                            }
                        }

                        progress?.Report($"Downloading: {fileInfo.file.Name} ({FormatFileSize(fileInfo.file.Size)})");
                        
                        // Properly escape the path for URI construction to handle @ symbols
                        var escapedPath = Uri.EscapeDataString(fileInfo.file.FullPath).Replace("%2F", "/");
                        var itemUri = new Uri($"ftp://{ftpHost}:{ftpPort}{escapedPath}");
                        
                        await Task.Run(() => DownloadFileWithProgress(itemUri, username, password, fileInfo.localPath, fileInfo.file.Size, fileInfo.file.LastModified, progress));
                        Interlocked.Increment(ref _downloadedFiles);
                    }
                    catch (Exception ex)
                    {
                        progress?.Report($"Failed to download {fileInfo.file.Name}: {ex.Message}");
                    }
                    finally
                    {
                        semaphore.Release();
                    }
                }).ToArray();

                Task.WaitAll(downloadTasks);
                Interlocked.Add(ref _skippedFiles, allFilesToDownload.Count - filesToDownload.Count);
            }
            catch (Exception ex)
            {
                progress?.Report($"Error downloading {currentPath}: {ex.Message}");
            }
        }

        private void CollectFilesToDownload(FtpDirectoryCache cache, string currentPath, string destinationPath, ConcurrentBag<(FtpItem file, string localPath)> filesToDownload)
        {
            if (!cache.Cache.TryGetValue(currentPath, out var items))
                return;

            var subdirectories = items.Where(i => i.IsDirectory && i.Name != "." && i.Name != "..").ToList();
            var files = items.Where(i => !i.IsDirectory).ToList();

            // Add files from current directory
            foreach (var file in files)
            {
                var localPath = Path.Combine(destinationPath, file.Name);
                filesToDownload.Add((file, localPath));
            }

            // Recursively collect from subdirectories
            foreach (var dir in subdirectories)
            {
                var localPath = Path.Combine(destinationPath, dir.Name);
                CollectFilesToDownload(cache, dir.FullPath, localPath, filesToDownload);
            }
        }

        private List<FtpItem> GetDirectoryListingOptimized(Uri baseUri, string username, string password, string currentPath)
        {
            var items = new List<FtpItem>();
            
            // Construct the full URI for this path - escape special characters like @
            var escapedPath = Uri.EscapeDataString(currentPath.TrimStart('/')).Replace("%2F", "/");
            var pathUri = new Uri(baseUri, escapedPath);
            if (!pathUri.AbsolutePath.EndsWith("/"))
            {
                pathUri = new Uri(pathUri.ToString() + "/");
            }

            // Try MLSD first (modern, structured listing)
            try
            {
                var mlsdRequest = (FtpWebRequest)WebRequest.Create(pathUri);
                mlsdRequest.Method = "MLSD";
                mlsdRequest.Credentials = new NetworkCredential(username, password);
                mlsdRequest.UseBinary = true;
                mlsdRequest.KeepAlive = false;

                using var mlsdResponse = (FtpWebResponse)mlsdRequest.GetResponse();
                using var mlsdStream = mlsdResponse.GetResponseStream();
                using var mlsdReader = new StreamReader(mlsdStream);

                while (!mlsdReader.EndOfStream)
                {
                    var line = mlsdReader.ReadLine();
                    if (string.IsNullOrEmpty(line))
                        continue;

                    var item = ParseMLSDLine(line, currentPath);
                    if (item != null && item.Name != "." && item.Name != "..")
                    {
                        items.Add(item);
                    }
                }
                return items;
            }
            catch
            {
                // MLSD not supported, fall back to LIST
            }

            // Try LIST -al (detailed listing)
            try
            {
                var detailsRequest = (FtpWebRequest)WebRequest.Create(pathUri);
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
                return items;
            }
            catch
            {
                // Fall back to simple listing with size checks
            }

            // Fallback: Simple listing (slowest)
            try
            {
                var listRequest = (FtpWebRequest)WebRequest.Create(pathUri);
                listRequest.Method = WebRequestMethods.Ftp.ListDirectory;
                listRequest.Credentials = new NetworkCredential(username, password);
                listRequest.UseBinary = true;
                listRequest.KeepAlive = false;

                using var listResponse = (FtpWebResponse)listRequest.GetResponse();
                using var listStream = listResponse.GetResponseStream();
                using var listReader = new StreamReader(listStream);

                var fileNames = new List<string>();
                while (!listReader.EndOfStream)
                {
                    var fileName = listReader.ReadLine();
                    if (!string.IsNullOrEmpty(fileName) && fileName != "." && fileName != "..")
                        fileNames.Add(fileName);
                }

                // Batch check file types
                var parallelOptions = new ParallelOptions { MaxDegreeOfParallelism = _maxConcurrentDownloads };
                var itemsList = new ConcurrentBag<FtpItem>();

                Parallel.ForEach(fileNames, parallelOptions, fileName =>
                {
                    var itemPath = currentPath.TrimEnd('/') + "/" + fileName;
                    // Properly escape the file name for URI construction
                    var escapedFileName = Uri.EscapeDataString(fileName);
                    var itemUri = new Uri(pathUri, escapedFileName);
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

                    itemsList.Add(item);
                });

                items = itemsList.ToList();
            }
            catch
            {
            }

            return items;
        }

        private FtpItem? ParseMLSDLine(string line, string currentPath)
        {
            try
            {
                // MLSD format: "type=dir;size=0;modify=20231201120000; folderName"
                var parts = line.Split(new[] { "; " }, 2, StringSplitOptions.None);
                if (parts.Length < 2)
                    return null;

                var facts = parts[0];
                var name = parts[1];

                var item = new FtpItem
                {
                    Name = name,
                    FullPath = currentPath.TrimEnd('/') + "/" + name
                };

                var factPairs = facts.Split(';');
                foreach (var fact in factPairs)
                {
                    var kv = fact.Split('=');
                    if (kv.Length != 2) continue;

                    var key = kv[0].ToLower().Trim();
                    var value = kv[1].Trim();

                    switch (key)
                    {
                        case "type":
                            item.IsDirectory = value.ToLower() == "dir" || value.ToLower() == "cdir" || value.ToLower() == "pdir";
                            break;
                        case "size":
                            if (long.TryParse(value, out long size))
                                item.Size = size;
                            break;
                        case "modify":
                            if (DateTime.TryParseExact(value, "yyyyMMddHHmmss", null, System.Globalization.DateTimeStyles.None, out DateTime dt))
                                item.LastModified = dt;
                            break;
                    }
                }

                return item;
            }
            catch
            {
                return null;
            }
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

                    // Properly escape the file name for URI construction to handle @ symbols
                    var escapedFileName = Uri.EscapeDataString(fileName);
                    var itemUri = new Uri(ftpUri, escapedFileName);
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
                request.UsePassive = true;

                using var response = (FtpWebResponse)request.GetResponse();
                using var responseStream = response.GetResponseStream();
                using var fileStream = File.Create(destinationPath);

                byte[] buffer = new byte[_bufferSize];
                long totalBytesRead = 0;
                int bytesRead;

                while ((bytesRead = responseStream.Read(buffer, 0, buffer.Length)) > 0)
                {
                    fileStream.Write(buffer, 0, bytesRead);
                    totalBytesRead += bytesRead;
                }

                fileStream.Close();

                if (remoteTimestamp != DateTime.MinValue)
                {
                    File.SetLastWriteTime(destinationPath, remoteTimestamp);
                }
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
                    // Properly escape the path to handle @ symbols and other special characters
                    var escapedPath = Uri.EscapeDataString(path).Replace("%2F", "/");
                    var ftpUri = new Uri($"ftp://{ftpUrl}:{port}{escapedPath}");
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

        public async Task DownloadFolderAsync(string ftpUrl, int port, string username, string password, string remotePath, string localPath, IProgress<string>? progress = null, CancellationToken cancellationToken = default)
        {
            await Task.Run(() =>
            {
                try
                {
                    _skippedFiles = 0;
                    _downloadedFiles = 0;

                    cancellationToken.ThrowIfCancellationRequested();
                    
                    progress?.Report($"Building folder structure cache for: {remotePath}");
                    
                    var baseUri = new Uri($"ftp://{ftpUrl}:{port}/");
                    var cache = BuildDirectoryCache(baseUri, username, password, remotePath, progress, cancellationToken);
                    
                    cancellationToken.ThrowIfCancellationRequested();
                    
                    progress?.Report($"Cache built. Found {cache.Cache.Values.Sum(x => x.Count(i => !i.IsDirectory))} files.");
                    progress?.Report($"Downloading folder: {remotePath}");
                    
                    DownloadDirectoryFromCache(cache, remotePath, username, password, localPath, ftpUrl, port, progress, cancellationToken);
                    
                    progress?.Report($"Folder download completed: {_downloadedFiles} files downloaded, {_skippedFiles} files up-to-date");
                }
                catch (OperationCanceledException)
                {
                    progress?.Report("Download cancelled by user.");
                }
                catch (Exception ex)
                {
                    progress?.Report($"Error downloading folder {remotePath}: {ex.Message}");
                }
            }, cancellationToken);
        }
    }
}
