using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Updater
{
    public static class Utils
    {
        private static string _softwareName;
        public static string SoftwareName
        {
            set => _softwareName = value;
            get
            {
                if (_softwareName == null)
                {
                    throw new NullReferenceException($"You must set {nameof(SoftwareName)} before using it");
                }
                return _softwareName;
            }
        }

        private static string _repoName;
        public static string RepoName
        {
            set => _repoName = value;
            get
            {
                if (_repoName == null)
                {
                    throw new NullReferenceException($"You must set {nameof(RepoName)} before using it");
                }
                return _repoName;
            }
        }

        private static Version _currentVersion;

        public static Version CurrentVersion
        {
            set => _currentVersion = value;
            get
            {
                if (_currentVersion == null)
                {
                    throw new NullReferenceException($"You must set {nameof(SoftwareName)} before using it");
                }
                return _currentVersion;
            }
        }

        public static async void CheckUpdate(bool showMessage = false)
        { 
            if (!IsConnectInternet()) return;
            try
            {
                var httpClient = new HttpClient
                {
                    Timeout = TimeSpan.FromSeconds(10)
                };
                httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                httpClient.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue(SoftwareName, CurrentVersion.ToString()));
                var httpResponseMessage =
                    await httpClient.GetAsync(new Uri($"https://api.github.com/repos/{RepoName}/releases/latest"));
                if (httpResponseMessage.StatusCode != HttpStatusCode.OK)
                {
                    var body = await httpResponseMessage.Content.ReadAsStringAsync();
                    Show($"[{httpResponseMessage.StatusCode}]请求发送失败，错误信息:\n{body}");
                    return;
                }

                var result =
                    Jil.JSON.Deserialize<GithubRelease>(
                        new StreamReader(await httpResponseMessage.Content.ReadAsStreamAsync()));
                var remoteVersion = Version.Parse(Convert.ToString(result.tag_name));
                if (remoteVersion > CurrentVersion)
                {
                    foreach (var asset in result.assets)
                    {
                        if (asset.content_type == "application/x-msdownload")
                        {
                            var dialogResult = MessageBox.Show(caption: @"Wow! Such Impressive",
                                text: $"新车已发车 v{remoteVersion}，上车!",
                                buttons: MessageBoxButtons.YesNo, icon: MessageBoxIcon.Asterisk);
                            if (dialogResult != DialogResult.Yes) return;
                            var formUpdater = new FormUpdater(Application.ExecutablePath, remoteVersion,
                                Convert.ToString(asset.browser_download_url));
                            formUpdater.ShowDialog();
                            return;
                        }
                    }
                }
                else
                {
                    Show($"{CurrentVersion}已为最新版");
                    return;
                }

                Show("无可用的资源，请向项目维护人咨询具体情况");
            }
            catch (TaskCanceledException)
            {
                Show("请求超时");
            }
            catch (Exception e)
            {
                Show($"[{e.GetType()}]请求失败: {e.Message}");
            }
            void Show(string message)
            {
                if (showMessage)
                    MessageBox.Show(message, "程序更新");
            }
        }

        public static bool CheckUpdateWeekly(string program)
        {
            var reg = RegistryStorage.Load(@"Software\" + program, "LastCheck");
            if (string.IsNullOrEmpty(reg))
            {
                RegistryStorage.Save(DateTime.Now.ToString(CultureInfo.InvariantCulture), @"Software\" + program, "LastCheck");
                return false;
            }
            var lastCheckTime = DateTime.Parse(reg);
            if (DateTime.Now - lastCheckTime > new TimeSpan(7, 0, 0, 0))
            {
                CheckUpdate();
                RegistryStorage.Save(DateTime.Now.ToString(CultureInfo.InvariantCulture), @"Software\" + program, "LastCheck");
                return true;
            }
            return false;
        }

        private static bool IsConnectInternet()
        {
            return InternetGetConnectedState(0, 0);
        }

        [DllImport("wininet.dll")]
        private static extern bool InternetGetConnectedState(int description, int reservedValue);
    }

    public class GithubAsset
    {
        public string url;
        public string name;
        public string content_type;
        public long size;
        public string created_at;
        public string updated_at;
        public string browser_download_url;
    }

    public class GithubRelease
    {
        public string url;
        public string assets_url;
        public string tag_name;
        public string name;
        public List<GithubAsset> assets;
        public string zipball_url;
        public string body;
    }
}