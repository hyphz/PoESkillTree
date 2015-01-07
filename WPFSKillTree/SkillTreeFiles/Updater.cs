using System;
using System.ComponentModel;
using System.IO;
using System.Net;
using System.Text;
using Raven.Json.Linq;
using Ionic.Zip;

namespace POESKillTree.SkillTreeFiles
{
    /* Update manager.
     * Provides API for semi-synchronous update process (downloading is asynchronous, while checking for updates and installation are synchronous tasks).
     */ 
    public class Updater
    {
        // Git API URL to fetch releases (the first one is latest one).
        private const string GitApiLatestReleaseUrl = "https://api.github.com/repos/EmmittJ/PoESkillTree/releases";
        // The flag whether check for updates was done and was successful.
        public static bool IsChecked;
        // The flag whether download is complete.
        public static bool IsDownloaded { get { return _latest != null && _latest.IsDownloaded; } }
        // The flag whether download is in progress.
        public static bool IsDownloading { get { return _latest != null && _latest.IsDownloading; } }
        // The flag whether installation completed.
        public static bool IsInstalled;
        // Latest release.
        private static Release _latest;
        // HTTP request timeout for release checks and downloads (in seconds).
        private const int RequestTimeout = 15;
        // Work directory of update process (relative to installation root).
        private const string WorkDir = ".update";

        // Release informations.
        public class Release
        {
            // The web client instance of current download process.
            private WebClient _client;
            // The name.
            public string Name;
            // The description.
            public string Description;
            // The flag whether release was downloaded.
            public bool IsDownloaded { get { return _client == null && _temporaryFile != null; } }
            // The flag whether download is still in progress.
            public bool IsDownloading { get { return _client != null; } }
            // The temporary file for package download.
            private string _temporaryFile;
            // The URI of release package.
            public Uri URI;
            // The version string.
            public string Version;

            ~Release()
            {
                try
                {
                    Dispose();
                }
                catch (Exception e) {}
            }

            // Cancels download.
            public void Cancel()
            {
                // Cancel download in progress.
                if (_client.IsBusy)
                    _client.CancelAsync();
            }

            // Dispose of all resources.
            public void Dispose()
            {
                // Dispose web client.
                if (_client != null)
                {
                    _client.Dispose();
                    _client = null;
                }

                // Delete temporary file.
                if (_temporaryFile != null)
                {
                    try
                    {
                        File.Delete(_temporaryFile);
                        _temporaryFile = null;
                    }
                    catch (Exception e) { }
                }

                // Delete work directory.
                if (Directory.Exists(WorkDir))
                {
                    try
                    {
                        Directory.Delete(WorkDir, true);
                    }
                    catch (Exception e) {}
                }
            }

            /* Downloads release.
             * Throws UpdaterException if error occurs.
             */
            public void Download(AsyncCompletedEventHandler completedHandler, DownloadProgressChangedEventHandler progressHandler)
            {
                if (_client != null)
                    throw new UpdaterException("Download already in progress");
                if (_temporaryFile != null)
                    throw new UpdaterException("Download already completed");

                try
                {
                    // Initialize web client.
                    _client = new UpdaterWebClient();
                    _client.DownloadFileCompleted += DownloadCompleted;
                    if (completedHandler != null)
                        _client.DownloadFileCompleted += completedHandler;
                    if (progressHandler != null)
                        _client.DownloadProgressChanged += progressHandler;

                    // Create temporary file.
                    _temporaryFile = Path.GetTempFileName();

                    // Start download.
                    _client.DownloadFileAsync(URI, _temporaryFile);
                }
                catch (Exception e)
                {
                    Dispose();
                    throw new UpdaterException(e.Message, e);
                }
            }

            // Invoked when download completes, aborts or fails.
            private void DownloadCompleted(Object sender, AsyncCompletedEventArgs e)
            {
                // Dispose web client.
                _client.Dispose();
                _client = null;

                // Dispose of resources so download can be retried.
                if (e.Cancelled || e.Error != null) Dispose();
            }

            // Returns source directory of an update.
            private string GetSourceDir()
            {
                DirectoryInfo work = new DirectoryInfo(WorkDir);
                DirectoryInfo[] dirs = work.GetDirectories();

                return dirs.Length == 0 ? null : dirs[0].FullName;
            }

            /* Installs release.
             * Throws UpdaterException if error occurs.
             */
            public void Install()
            {
                if (_client != null)
                    throw new UpdaterException("Download still in progress");

                if (_temporaryFile == null)
                    throw new UpdaterException("No package downloaded");

                try
                {
                    // Create empty work directory.
                    Directory.CreateDirectory(WorkDir);

                    // Extract package.
                    ZipFile zip = ZipFile.Read(_temporaryFile);
                    zip.ExtractAll(WorkDir);

                    // Copy content of first directory found in work directory to installation root.
                    string sourceDir = GetSourceDir();
                    if (sourceDir == null)
                        throw new UpdaterException("Invalid package content");
                    CopyTo(sourceDir, ".");

                    Dispose();
                }
                catch (Exception e)
                {
                    Dispose();
                    throw new UpdaterException(e.Message, e);
                }
            }
        }

        // WebClient HTTP request override.
        class UpdaterWebClient : WebClient
        {
            protected override WebRequest GetWebRequest(Uri address)
            {
                WebRequest request = base.GetWebRequest(address);

                var webRequest = request as HttpWebRequest;
                if (webRequest != null)
                {
                    HttpWebRequest httpRequest = webRequest;
                    httpRequest.UserAgent = "PoESkillTree";
                    httpRequest.KeepAlive = false;
                    httpRequest.Timeout = RequestTimeout * 1000;
                }

                return request;
            }
        }

        // Cancels download.
        public static void Cancel()
        {
            if (_latest != null && _latest.IsDownloading)
                _latest.Cancel();
        }

        /* Checks for updates and returns release informations when there is newer one.
         * Returns null if there is no newer release.
         * An existing last checked release will be discarded.
         * Throws UpdaterException if error occurs.
         */
        public static Release CheckForUpdates()
        {
            if (_latest != null)
            {
                if (_latest.IsDownloading)
                    throw new UpdaterException("Download already in progress");
                _latest.Dispose();
            }
            _latest = null;
            IsChecked = IsInstalled = false;

            var webClient = new UpdaterWebClient {Encoding = Encoding.UTF8};

            try
            {
                string json = webClient.DownloadString(GitApiLatestReleaseUrl);

                RavenJArray releases = RavenJArray.Parse(json);
                if (releases.Length < 1)
                    throw new UpdaterException("No release found");

                RavenJObject latest = (RavenJObject)releases[0];
                RavenJArray assets = (RavenJArray)latest["assets"];
                if (assets.Length < 1)
                    throw new UpdaterException("Package for release is missing");

                string current = GetCurrentVersion();
                string tag = latest["tag_name"].Value<string>();
                if (tag == current)
                {
                    IsChecked = true;

                    return null;
                }

                string url = ((RavenJObject)assets[0])["browser_download_url"].Value<string>();

                IsChecked = true;
                _latest = new Release
                {
                    Name = latest["name"].Value<string>(),
                    Description = latest["body"].Value<string>(),
                    Version = tag,
                    URI = new Uri(url)
                };
            }
            catch (WebException e)
            {
                if (e.Status == WebExceptionStatus.ProtocolError)
                    throw new UpdaterException("HTTP " + ((int)((HttpWebResponse)e.Response).StatusCode) + " " + ((HttpWebResponse)e.Response).StatusDescription);
                else
                    throw new UpdaterException(e.Message, e);
            }
            catch (Exception e)
            {
                throw new UpdaterException(e.Message, e);
            }

            return _latest;
        }

        // Copies files or directories to target directory recursively.
        public static void CopyTo(string sourcePath, string targetPath)
        {
            if (!Directory.Exists(targetPath))
                throw new DirectoryNotFoundException("No such directory: " + targetPath);

            if (File.Exists(sourcePath))
            {
                FileInfo src = new FileInfo(sourcePath);
                src.CopyTo(Path.Combine(targetPath, src.Name), true);
            }
            else if (Directory.Exists(sourcePath))
            {
                DirectoryInfo src = new DirectoryInfo(sourcePath);

                foreach (FileInfo file in src.GetFiles())
                    file.CopyTo(Path.Combine(targetPath, file.Name), true);

                foreach (DirectoryInfo dir in src.GetDirectories())
                {
                    string subdir = Path.Combine(targetPath, dir.Name);
                    if (!Directory.Exists(subdir))
                        Directory.CreateDirectory(subdir);

                    CopyTo(dir.FullName, subdir);
                }
            }
            else
                throw new FileNotFoundException("No such file or directory: " + sourcePath);
        }

        // Dispose of current update process.
        public static void Dispose()
        {
            if (_latest != null)
            {
                if (_latest.IsDownloading)
                    throw new UpdaterException("Download still in progress");
                _latest.Dispose();
                _latest = null;
            }

            IsChecked = IsInstalled = false;
        }

        /* Downloads latest release.
         * Throws UpdaterException if error occurs.
         */
        public static void Download(AsyncCompletedEventHandler completedHandler = null, DownloadProgressChangedEventHandler progressHandler = null)
        {
            if (_latest != null)
            {
                if (_latest.IsDownloaded || _latest.IsDownloading)
                    throw new UpdaterException("Download completed or still in progress");

                _latest.Download(completedHandler, progressHandler);
            }
        }

        // Returns current version.
        public static string GetCurrentVersion()
        {
            return Properties.Version.ProductVersion;
        }

        // Return latest release, or null if there is none or it wasn't checked for yet.
        public static Release GetLatestRelease()
        {
            return _latest;
        }

        /* Installs downloaded release.
         * Throws UpdaterException if error occurs.
         */
        public static void Install()
        {
            if (_latest != null)
            {
                if (_latest.IsDownloading)
                    throw new UpdaterException("Download still in progress");
                if (!_latest.IsDownloaded)
                    throw new UpdaterException("No package downloaded");

                // If installation fails (exception will be thrown), latest release will be in ready to re-download state.
                _latest.Install();
                _latest = null;
                IsInstalled = true;
            }
        }

        // Restarts application.
        public static void RestartApplication()
        {
            Bootstrap.Restart();
        }
    }

    // Updater exception.
    public class UpdaterException : Exception
    {
        public UpdaterException()
            : base()
        {
        }

        public UpdaterException(string message)
            : base(message)
        {
        }

        public UpdaterException(string message, Exception inner)
            : base(message, inner)
        {
        }
    }
}
