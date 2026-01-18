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
        private int _deletedFiles = 0;
        private readonly int _maxConcurrentDownloads = 8;
        private readonly int _bufferSize = 65536; // 64KB buffer
        private readonly FtpCacheManager _cacheManager = new();
        private TimeSpan _cacheLifetime = TimeSpan.FromHours(1);

        public async Task<bool> DownloadRepositoryAsync(Repository repository, string destinationPath, IProgress<string>? progress = null, CancellationToken cancellationToken = default, bool forceRefresh = false)
        {
            try
            {
                _skippedFiles = 0;
                _downloadedFiles = 0;
                _deletedFiles = 0;

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

                FtpDirectoryCache cache;

                if (!forceRefresh)
                {
                    var cachedData = _cacheManager.LoadCache(repository.Id.ToString());

                    if (cachedData != null && cachedData.IsValidForRepository(repository, _cacheLifetime))
                    {
                        progress?.Report($"? Using cached directory structure (scanned {FormatAge(DateTime.Now - cachedData.LastScanned)} ago)");
                        progress?.Report($"  {cachedData.TotalFiles} files, {cachedData.TotalDirectories} directories cached");

                        cache = ConvertCachedDataToCache(cachedData);

                        progress?.Report("Starting download from cache...");
                    }
                    else
                    {
                        var reason = cachedData == null ? "no cache found"
                                   : cachedData.IsExpired() ? "cache expired"
                                   : "repository settings changed";

                        progress?.Report($"Building fresh cache ({reason})...");
                        cache = await BuildAndSaveCache(repository, ftpUri, progress, cancellationToken);
                    }
                }
                else
                {
                    progress?.Report("Force refresh - rebuilding cache...");
                    cache = await BuildAndSaveCache(repository, ftpUri, progress, cancellationToken);
                }
                
                cancellationToken.ThrowIfCancellationRequested();
                
                await DownloadDirectoryFromCache(cache, "/", repository.Username, repository.Password, destinationPath, ftpUri.Host, ftpUri.Port, progress, cancellationToken);
                
                var deletionMessage = _deletedFiles > 0 ? $", {_deletedFiles} files deleted" : "";
                progress?.Report($"? Sync completed: {_downloadedFiles} downloaded, {_skippedFiles} up-to-date{deletionMessage}");
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

        private async Task<FtpDirectoryCache> BuildAndSaveCache(Repository repository, Uri ftpUri, IProgress<string>? progress, CancellationToken cancellationToken)
        {
            var cache = await Task.Run(() => BuildDirectoryCache(ftpUri, repository.Username, repository.Password, "/", progress, cancellationToken), cancellationToken);

            progress?.Report($"Cache built: {cache.Cache.Values.Sum(x => x.Count(i => !i.IsDirectory))} files in {cache.Cache.Count} directories");

            var cachedData = new CachedRepositoryData
            {
                RepositoryId = repository.Id.ToString(),
                RepositoryName = repository.Name,
                LastScanned = DateTime.Now,
                ExpiresAt = DateTime.Now.Add(_cacheLifetime),
                RepositorySnapshot = CachedRepositoryData.GenerateRepositorySnapshot(repository),
                DirectoryCache = ConvertCacheToCachedData(cache),
                TotalFiles = cache.Cache.Values.Sum(x => x.Count(i => !i.IsDirectory)),
                TotalDirectories = cache.Cache.Count,
                TotalSizeBytes = cache.Cache.Values.SelectMany(x => x).Sum(x => x.Size)
            };

            _cacheManager.SaveCache(cachedData);
            progress?.Report($"? Cache saved to disk (expires in {_cacheLifetime.TotalHours:F1} hours)");

            return cache;
        }

        private FtpDirectoryCache ConvertCachedDataToCache(CachedRepositoryData cachedData)
        {
            var cache = new FtpDirectoryCache();

            foreach (var kvp in cachedData.DirectoryCache)
            {
                cache.Cache[kvp.Key] = kvp.Value.Select(item => new FtpItem
                {
                    Name = item.Name,
                    FullPath = item.FullPath,
                    IsDirectory = item.IsDirectory,
                    Size = item.Size,
                    LastModified = item.LastModified
                }).ToList();
            }

            return cache;
        }

        private Dictionary<string, List<CachedFtpItem>> ConvertCacheToCachedData(FtpDirectoryCache cache)
        {
            var result = new Dictionary<string, List<CachedFtpItem>>();

            foreach (var kvp in cache.Cache)
            {
                result[kvp.Key] = kvp.Value.Select(item => new CachedFtpItem
                {
                    Name = item.Name,
                    FullPath = item.FullPath,
                    IsDirectory = item.IsDirectory,
                    Size = item.Size,
                    LastModified = item.LastModified
                }).ToList();
            }

            return result;
        }

        private string FormatAge(TimeSpan age)
        {
            if (age.TotalMinutes < 1)
                return "just now";
            if (age.TotalHours < 1)
                return $"{(int)age.TotalMinutes} minutes";
            if (age.TotalDays < 1)
                return $"{(int)age.TotalHours} hours";
            return $"{(int)age.TotalDays} days";
        }

        public void SetCacheLifetime(TimeSpan lifetime)
        {
            _cacheLifetime = lifetime;
        }

        public string GetCacheInfo(string repositoryId)
        {
            return _cacheManager.GetCacheInfo(repositoryId);
        }

        public void InvalidateCache(string repositoryId)
        {
            _cacheManager.InvalidateCache(repositoryId);
        }

        public void ClearExpiredCaches()
        {
            _cacheManager.ClearExpiredCaches();
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
                // Release semaphore and re-throw without additional reporting
                // The top-level handler will report the cancellation
                semaphore.Release();
                throw;
            }
            catch (Exception ex)
            {
                progress?.Report($"Error scanning {path}: {ex.Message}");
                semaphore.Release();
                throw;
            }
        }

        private async Task DownloadDirectoryFromCache(FtpDirectoryCache cache, string currentPath, string username, string password, string destinationPath, string ftpHost, int ftpPort, IProgress<string>? progress, CancellationToken cancellationToken)
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
                    .Where(f => ShouldDownloadFile(f.localPath, f.file.Size))
                    .ToList();

                progress?.Report($"{filesToDownload.Count} files need to be downloaded, {allFilesToDownload.Count - filesToDownload.Count} files up-to-date");

                cancellationToken.ThrowIfCancellationRequested();
                
                // Delete local files that don't exist on FTP (FTP is the source of truth)
                await DeleteOrphanedFiles(cache, currentPath, destinationPath, progress, cancellationToken);
                
                // Download all files in parallel
                var semaphore = new SemaphoreSlim(_maxConcurrentDownloads);
                var downloadTasks = filesToDownload.Select(async fileInfo =>
                {
                    await semaphore.WaitAsync(cancellationToken);
                    try
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        
                        // Ensure directory exists
                        var directory = Path.GetDirectoryName(fileInfo.localPath);
                        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                        {
                            lock (cache)
                            {
                                Directory.CreateDirectory(directory);
                            }
                        }

                        progress?.Report($"Downloading: {fileInfo.file.Name} => {fileInfo.localPath} ({FormatFileSize(fileInfo.file.Size)})");
                        
                        // Properly escape the path for URI construction to handle @ symbols
                        var escapedPath = Uri.EscapeDataString(fileInfo.file.FullPath).Replace("%2F", "/");
                        var itemUri = new Uri($"ftp://{ftpHost}:{ftpPort}{escapedPath}");
                        
                        await Task.Run(() => DownloadFileWithProgress(itemUri, username, password, fileInfo.localPath, fileInfo.file.Size, fileInfo.file.LastModified, progress, cancellationToken), cancellationToken);
                        Interlocked.Increment(ref _downloadedFiles);
                    }
                    catch (OperationCanceledException)
                    {
                        // Silently handle cancellation for individual tasks
                        throw;
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

                try
                {
                    await Task.WhenAll(downloadTasks);
                }
                catch (OperationCanceledException)
                {
                    progress?.Report("Download cancelled.");
                    throw;
                }
                
                Interlocked.Add(ref _skippedFiles, allFilesToDownload.Count - filesToDownload.Count);
            }
            catch (OperationCanceledException)
            {
                // Let cancellation propagate up without additional error reporting
                throw;
            }
            catch (Exception ex)
            {
                progress?.Report($"Error downloading {currentPath}: {ex.Message}");
                throw;
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

        private async Task DeleteOrphanedFiles(FtpDirectoryCache cache, string currentPath, string destinationPath, IProgress<string>? progress, CancellationToken cancellationToken)
        {
            try
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (!Directory.Exists(destinationPath))
                    return;

                // Build a set of all files that should exist based on FTP cache
                var ftpFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                var ftpDirs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                BuildExpectedFileSet(cache, currentPath, destinationPath, ftpFiles, ftpDirs);

                // Scan local directory and delete files not in FTP
                await ScanAndDeleteOrphanedFiles(destinationPath, ftpFiles, ftpDirs, progress, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                progress?.Report($"Error checking for orphaned files: {ex.Message}");
            }
        }

        private void BuildExpectedFileSet(FtpDirectoryCache cache, string currentPath, string destinationPath, HashSet<string> ftpFiles, HashSet<string> ftpDirs)
        {
            if (!cache.Cache.TryGetValue(currentPath, out var items))
                return;

            var subdirectories = items.Where(i => i.IsDirectory && i.Name != "." && i.Name != "..").ToList();
            var files = items.Where(i => !i.IsDirectory).ToList();

            // Add files from current directory
            foreach (var file in files)
            {
                var localPath = Path.Combine(destinationPath, file.Name);
                ftpFiles.Add(localPath);
            }

            // Add subdirectories and recursively process them
            foreach (var dir in subdirectories)
            {
                var localPath = Path.Combine(destinationPath, dir.Name);
                ftpDirs.Add(localPath);
                BuildExpectedFileSet(cache, dir.FullPath, localPath, ftpFiles, ftpDirs);
            }
        }

        private async Task ScanAndDeleteOrphanedFiles(string localPath, HashSet<string> ftpFiles, HashSet<string> ftpDirs, IProgress<string>? progress, CancellationToken cancellationToken)
        {
            await Task.Run(() =>
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (!Directory.Exists(localPath))
                    return;

                // Delete orphaned files
                var localFiles = Directory.GetFiles(localPath);
                foreach (var file in localFiles)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    
                    if (!ftpFiles.Contains(file))
                    {
                        try
                        {
                            File.Delete(file);
                            Interlocked.Increment(ref _deletedFiles);
                            progress?.Report($"Deleted (not in FTP): {Path.GetFileName(file)}");
                        }
                        catch (Exception ex)
                        {
                            progress?.Report($"Failed to delete {Path.GetFileName(file)}: {ex.Message}");
                        }
                    }
                }

                // Recursively process subdirectories
                var localDirs = Directory.GetDirectories(localPath);
                foreach (var dir in localDirs)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    ScanAndDeleteOrphanedFiles(dir, ftpFiles, ftpDirs, progress, cancellationToken).Wait();
                }

                // Delete empty directories that don't exist in FTP
                foreach (var dir in localDirs)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    
                    if (!ftpDirs.Contains(dir))
                    {
                        try
                        {
                            if (Directory.GetFiles(dir, "*", SearchOption.AllDirectories).Length == 0)
                            {
                                Directory.Delete(dir, true);
                                progress?.Report($"Deleted directory (not in FTP): {Path.GetFileName(dir)}");
                            }
                        }
                        catch (Exception ex)
                        {
                            progress?.Report($"Failed to delete directory {Path.GetFileName(dir)}: {ex.Message}");
                        }
                    }
                }
            }, cancellationToken);
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

        private void DownloadDirectory(Uri ftpUri, string username, string password, string destinationPath, IProgress<string>? progress, CancellationToken cancellationToken = default)
        {
            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                
                if (!Directory.Exists(destinationPath))
                {
                    Directory.CreateDirectory(destinationPath);
                    progress?.Report($"Created directory: {Path.GetFileName(destinationPath)}");
                }

                var items = GetDirectoryListing(ftpUri, username, password);

                foreach (var item in items)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    
                    if (item.Name == "." || item.Name == "..")
                        continue;

                    var itemUri = new Uri(ftpUri, item.Name);
                    var localPath = Path.Combine(destinationPath, item.Name);

                    if (item.IsDirectory)
                    {
                        progress?.Report($"Entering directory: {item.Name}");
                        DownloadDirectory(new Uri(itemUri.ToString() + "/"), username, password, localPath, progress, cancellationToken);
                    }
                    else
                    {
                        if (ShouldDownloadFile(localPath, item.Size))
                        {
                            progress?.Report($"Downloading file: {item.Name} => {localPath} ({FormatFileSize(item.Size)})");
                            DownloadFileWithProgress(itemUri, username, password, localPath, item.Size, item.LastModified, progress, cancellationToken);
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
            catch (OperationCanceledException)
            {
                // Let cancellation propagate - main handler will report it
                throw;
            }
            catch (Exception ex)
            {
                progress?.Report($"Error in directory {ftpUri.AbsolutePath}: {ex.Message}");
                throw;
            }
        }

        private bool ShouldDownloadFile(string localPath, long remoteSize)
        {
            if (!File.Exists(localPath))
                return true;

            var localFileInfo = new FileInfo(localPath);
            
            // Only check file size - ignore modified date
            if (localFileInfo.Length != remoteSize)
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

        private void DownloadFileWithProgress(Uri ftpUri, string username, string password, string destinationPath, long fileSize, DateTime remoteTimestamp, IProgress<string>? progress, CancellationToken cancellationToken = default)
        {
            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                
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
                    cancellationToken.ThrowIfCancellationRequested();
                    fileStream.Write(buffer, 0, bytesRead);
                    totalBytesRead += bytesRead;
                }

                fileStream.Close();

                if (remoteTimestamp != DateTime.MinValue)
                {
                    File.SetLastWriteTime(destinationPath, remoteTimestamp);
                }
            }
            catch (OperationCanceledException)
            {
                // Clean up partial file on cancellation
                try
                {
                    if (File.Exists(destinationPath))
                        File.Delete(destinationPath);
                }
                catch { }
                throw;
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

        public async Task<List<FtpBrowseItem>> BrowseDirectoryAsync(Repository repository, string path = "/")
        {
            return await BrowseDirectoryAsync(
                repository.Url,
                repository.Port,
                repository.Username,
                repository.Password,
                path,
                repository.Id.ToString()
            );
        }

        public async Task<List<FtpBrowseItem>> BrowseDirectoryAsync(string ftpUrl, int port, string username, string password, string path = "/", string? repositoryId = null)
        {
            return await Task.Run(() =>
            {
                var items = new List<FtpBrowseItem>();
                
                // Try to use persistent cache if repositoryId is provided
                if (!string.IsNullOrEmpty(repositoryId))
                {
                    var cachedData = _cacheManager.LoadCache(repositoryId);
                    
                    if (cachedData != null && !cachedData.IsExpired())
                    {
                        // Check if this specific path exists in cache
                        if (cachedData.DirectoryCache.TryGetValue(path, out var cachedItems))
                        {
                            System.Diagnostics.Debug.WriteLine($"? Using cached browse data for {path} in repository {repositoryId}");
                            
                            foreach (var cachedItem in cachedItems)
                            {
                                if (cachedItem.Name == "." || cachedItem.Name == "..")
                                    continue;

                                items.Add(new FtpBrowseItem
                                {
                                    Name = cachedItem.Name,
                                    FullPath = cachedItem.FullPath,
                                    IsDirectory = cachedItem.IsDirectory
                                });
                            }
                            
                            return items;
                        }
                    }
                }
                
                // Cache miss or not available - fetch live from FTP
                try
                {
                    System.Diagnostics.Debug.WriteLine($"Fetching live FTP browse data for {path}");
                    
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
                    
                    // Save browse results to cache if repositoryId is provided
                    if (!string.IsNullOrEmpty(repositoryId))
                    {
                        try
                        {
                            System.Diagnostics.Debug.WriteLine($"Merging browse results for {path} into cache for repository {repositoryId}");
                            
                            // Load existing cache or create new one
                            var existingCache = _cacheManager.LoadCache(repositoryId);
                            
                            Dictionary<string, List<CachedFtpItem>> cacheDict;
                            string repositoryName;
                            string snapshot;
                            
                            if (existingCache != null && !existingCache.IsExpired())
                            {
                                // Merge with existing cache
                                cacheDict = existingCache.DirectoryCache;
                                repositoryName = existingCache.RepositoryName;
                                snapshot = existingCache.RepositorySnapshot;
                                System.Diagnostics.Debug.WriteLine($"Merging into existing cache with {cacheDict.Count} directories");
                            }
                            else
                            {
                                // Create new cache
                                cacheDict = new Dictionary<string, List<CachedFtpItem>>();
                                repositoryName = $"Repository_{repositoryId}";
                                
                                // Generate snapshot hash
                                var snapshotData = $"{ftpUrl}:{port}:{username}";
                                using var sha256 = System.Security.Cryptography.SHA256.Create();
                                var bytes = System.Text.Encoding.UTF8.GetBytes(snapshotData);
                                var hash = sha256.ComputeHash(bytes);
                                snapshot = Convert.ToBase64String(hash);
                                System.Diagnostics.Debug.WriteLine($"Creating new cache");
                            }
                            
                            // Add/update this path in cache
                            cacheDict[path] = ftpItems.Select(item => new CachedFtpItem
                            {
                                Name = item.Name,
                                FullPath = path.TrimEnd('/') + "/" + item.Name,
                                IsDirectory = item.IsDirectory,
                                Size = item.Size,
                                LastModified = item.LastModified
                            }).Where(i => i.Name != "." && i.Name != "..").ToList();
                            
                            // Save updated cache
                            var cacheData = new CachedRepositoryData
                            {
                                RepositoryId = repositoryId,
                                RepositoryName = repositoryName,
                                LastScanned = DateTime.Now,
                                ExpiresAt = DateTime.Now.Add(_cacheLifetime),
                                RepositorySnapshot = snapshot,
                                DirectoryCache = cacheDict,
                                TotalFiles = cacheDict.Values.Sum(x => x.Count(i => !i.IsDirectory)),
                                TotalDirectories = cacheDict.Count,
                                TotalSizeBytes = cacheDict.Values.SelectMany(x => x).Sum(x => x.Size)
                            };
                            
                            _cacheManager.SaveCache(cacheData);
                            System.Diagnostics.Debug.WriteLine($"? Saved browse cache for {path} (total: {cacheData.TotalDirectories} dirs, {cacheData.TotalFiles} files)");
                        }
                        catch (Exception ex)
                        {
                            System.Diagnostics.Debug.WriteLine($"Failed to save browse cache: {ex.Message}");
                        }
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
            await DownloadFolderAsync(ftpUrl, port, username, password, remotePath, localPath, progress, cancellationToken, null);
        }

        public async Task DownloadFolderAsync(Repository repository, string remotePath, string localPath, IProgress<string>? progress = null, CancellationToken cancellationToken = default)
        {
            await DownloadFolderAsync(repository.Url, repository.Port, repository.Username, repository.Password, remotePath, localPath, progress, cancellationToken, repository.Id.ToString());
        }

        private async Task DownloadFolderAsync(string ftpUrl, int port, string username, string password, string remotePath, string localPath, IProgress<string>? progress, CancellationToken cancellationToken, string? repositoryId)
        {
            await Task.Run(async () =>
            {
                try
                {
                    _skippedFiles = 0;
                    _downloadedFiles = 0;
                    _deletedFiles = 0;

                    cancellationToken.ThrowIfCancellationRequested();
                    
                    FtpDirectoryCache cache;
                    bool cacheUsed = false;

                    // Try to use existing cache first if repository ID is provided
                    if (!string.IsNullOrEmpty(repositoryId))
                    {
                        var existingCache = _cacheManager.LoadCache(repositoryId);
                        
                        if (existingCache != null && !existingCache.IsExpired())
                        {
                            // Check if cache contains the requested path
                            if (existingCache.DirectoryCache.ContainsKey(remotePath))
                            {
                                progress?.Report($"? Using cached data for {remotePath} (scanned {FormatAge(DateTime.Now - existingCache.LastScanned)} ago)");
                                System.Diagnostics.Debug.WriteLine($"? Using cached folder data for {remotePath} in repository {repositoryId}");
                                
                                cache = ConvertCachedDataToCache(existingCache);
                                cacheUsed = true;
                            }
                            else
                            {
                                // Cache exists but doesn't have this specific path - need to scan and merge
                                progress?.Report($"Building cache for new path: {remotePath}");
                                System.Diagnostics.Debug.WriteLine($"Cache exists but missing path {remotePath}, scanning FTP...");
                                
                                var baseUri = new Uri($"ftp://{ftpUrl}:{port}/");
                                cache = BuildDirectoryCache(baseUri, username, password, remotePath, progress, cancellationToken);
                                
                                cancellationToken.ThrowIfCancellationRequested();
                                
                                // Merge new path into existing cache
                                var mergedCache = existingCache.DirectoryCache;
                                var newCacheData = ConvertCacheToCachedData(cache);
                                
                                foreach (var kvp in newCacheData)
                                {
                                    mergedCache[kvp.Key] = kvp.Value;
                                }
                                
                                // Save updated cache
                                var totalFiles = mergedCache.Values.Sum(x => x.Count(i => !i.IsDirectory));
                                var totalDirs = mergedCache.Count;
                                var totalSize = mergedCache.Values.SelectMany(x => x).Sum(x => x.Size);
                                
                                var cachedData = new CachedRepositoryData
                                {
                                    RepositoryId = repositoryId,
                                    RepositoryName = existingCache.RepositoryName,
                                    LastScanned = DateTime.Now,
                                    ExpiresAt = DateTime.Now.Add(_cacheLifetime),
                                    RepositorySnapshot = existingCache.RepositorySnapshot,
                                    DirectoryCache = mergedCache,
                                    TotalFiles = totalFiles,
                                    TotalDirectories = totalDirs,
                                    TotalSizeBytes = totalSize
                                };
                                
                                _cacheManager.SaveCache(cachedData);
                                progress?.Report($"? Cache updated: {totalFiles} files, {totalDirs} directories");
                                System.Diagnostics.Debug.WriteLine($"Merged new path {remotePath} into existing cache for repository {repositoryId}");
                            }
                        }
                        else
                        {
                            // No cache or expired - build new cache
                            var reason = existingCache == null ? "no cache found" : "cache expired";
                            progress?.Report($"Building folder structure cache for: {remotePath} ({reason})");
                            System.Diagnostics.Debug.WriteLine($"Building new cache for {remotePath}: {reason}");
                            
                            var baseUri = new Uri($"ftp://{ftpUrl}:{port}/");
                            cache = BuildDirectoryCache(baseUri, username, password, remotePath, progress, cancellationToken);
                            
                            cancellationToken.ThrowIfCancellationRequested();
                            
                            // Save new cache
                            var newCacheData = ConvertCacheToCachedData(cache);
                            var totalFiles = newCacheData.Values.Sum(x => x.Count(i => !i.IsDirectory));
                            var totalDirs = newCacheData.Count;
                            var totalSize = newCacheData.Values.SelectMany(x => x).Sum(x => x.Size);
                            
                            var cachedData = new CachedRepositoryData
                            {
                                RepositoryId = repositoryId,
                                RepositoryName = "Unknown",
                                LastScanned = DateTime.Now,
                                ExpiresAt = DateTime.Now.Add(_cacheLifetime),
                                RepositorySnapshot = "",
                                DirectoryCache = newCacheData,
                                TotalFiles = totalFiles,
                                TotalDirectories = totalDirs,
                                TotalSizeBytes = totalSize
                            };
                            
                            _cacheManager.SaveCache(cachedData);
                            progress?.Report($"? Cache saved: {totalFiles} files, {totalDirs} directories");
                            System.Diagnostics.Debug.WriteLine($"Created new cache for repository {repositoryId}");
                        }
                    }
                    else
                    {
                        // No repository ID - can't use cache
                        progress?.Report($"Building folder structure cache for: {remotePath}");
                        
                        var baseUri = new Uri($"ftp://{ftpUrl}:{port}/");
                        cache = BuildDirectoryCache(baseUri, username, password, remotePath, progress, cancellationToken);
                        
                        cancellationToken.ThrowIfCancellationRequested();
                        
                        progress?.Report($"Cache built. Found {cache.Cache.Values.Sum(x => x.Count(i => !i.IsDirectory))} files.");
                    }
                    
                    progress?.Report($"Downloading folder: {remotePath}");
                    
                    await DownloadDirectoryFromCache(cache, remotePath, username, password, localPath, ftpUrl, port, progress, cancellationToken);
                    
                    var deletionMessage = _deletedFiles > 0 ? $", {_deletedFiles} files deleted" : "";
                    progress?.Report($"Folder download completed: {_downloadedFiles} files downloaded, {_skippedFiles} files up-to-date{deletionMessage}");
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

        public async Task CacheAllRepositoriesAsync(IEnumerable<Repository> repositories, IProgress<string>? progress = null, CancellationToken cancellationToken = default)
        {
            var repoList = repositories.ToList();
            int total = repoList.Count;
            int current = 0;
            int skipped = 0;

            System.Diagnostics.Debug.WriteLine($"Starting background cache for {total} repositories...");

            foreach (var repository in repoList)
            {
                cancellationToken.ThrowIfCancellationRequested();

                current++;
                
                // Check if cache already exists and is valid
                var cachedData = _cacheManager.LoadCache(repository.Id.ToString());
                if (cachedData != null && cachedData.IsValidForRepository(repository, _cacheLifetime))
                {
                    progress?.Report($"Caching repositories in background... ({current}/{total} - {skipped} skipped) - {repository.Name}: Already cached");
                    System.Diagnostics.Debug.WriteLine($"? Repository '{repository.Name}' already has valid cache, skipping");
                    skipped++;
                    continue;
                }

                progress?.Report($"Caching repositories in background... ({current}/{total} - {skipped} skipped) - Scanning {repository.Name}...");
                System.Diagnostics.Debug.WriteLine($"Caching repository '{repository.Name}'...");

                try
                {
                    var ftpUri = new Uri($"ftp://{repository.Url}:{repository.Port}/");
                    var cache = await Task.Run(() => BuildDirectoryCache(ftpUri, repository.Username, repository.Password, "/", null, cancellationToken), cancellationToken);

                    var newCachedData = new CachedRepositoryData
                    {
                        RepositoryId = repository.Id.ToString(),
                        RepositoryName = repository.Name,
                        LastScanned = DateTime.Now,
                        ExpiresAt = DateTime.Now.Add(_cacheLifetime),
                        RepositorySnapshot = CachedRepositoryData.GenerateRepositorySnapshot(repository),
                        DirectoryCache = ConvertCacheToCachedData(cache),
                        TotalFiles = cache.Cache.Values.Sum(x => x.Count(i => !i.IsDirectory)),
                        TotalDirectories = cache.Cache.Count,
                        TotalSizeBytes = cache.Cache.Values.SelectMany(x => x).Sum(x => x.Size)
                    };

                    _cacheManager.SaveCache(newCachedData);
                    System.Diagnostics.Debug.WriteLine($"? Cached repository '{repository.Name}': {newCachedData.TotalFiles} files, {newCachedData.TotalDirectories} directories");
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"? Failed to cache repository '{repository.Name}': {ex.Message}");
                    skipped++;
                }

                // Small delay to avoid hammering FTP servers
                if (current < total)
                {
                    await Task.Delay(1000, cancellationToken);
                }
            }

            progress?.Report($"Background caching complete! ({current}/{total} - {skipped} skipped)");
            System.Diagnostics.Debug.WriteLine($"? Background caching complete: {current - skipped} cached, {skipped} skipped");
        }

        public async Task RefreshCacheForRepositoryAsync(Repository repository, IProgress<string>? progress = null, CancellationToken cancellationToken = default)
        {
            System.Diagnostics.Debug.WriteLine($"Refreshing cache for repository: {repository.Name}");

            // Invalidate existing cache
            InvalidateCache(repository.Id.ToString());

            try
            {
                var ftpUri = new Uri($"ftp://{repository.Url}:{repository.Port}/");
                var cache = await Task.Run(() => BuildDirectoryCache(ftpUri, repository.Username, repository.Password, "/", null, cancellationToken), cancellationToken);

                var cachedData = new CachedRepositoryData
                {
                    RepositoryId = repository.Id.ToString(),
                    RepositoryName = repository.Name,
                    LastScanned = DateTime.Now,
                    ExpiresAt = DateTime.Now.Add(_cacheLifetime),
                    RepositorySnapshot = CachedRepositoryData.GenerateRepositorySnapshot(repository),
                    DirectoryCache = ConvertCacheToCachedData(cache),
                    TotalFiles = cache.Cache.Values.Sum(x => x.Count(i => !i.IsDirectory)),
                    TotalDirectories = cache.Cache.Count,
                    TotalSizeBytes = cache.Cache.Values.SelectMany(x => x).Sum(x => x.Size)
                };

                _cacheManager.SaveCache(cachedData);
                System.Diagnostics.Debug.WriteLine($"? Cache refreshed for repository '{repository.Name}': {cachedData.TotalFiles} files, {cachedData.TotalDirectories} directories");

                progress?.Report($"? Cache refreshed for {repository.Name}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"? Failed to refresh cache for repository '{repository.Name}': {ex.Message}");
                throw;
            }
        }
    }
}
