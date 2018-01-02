using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
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

        private static void OnResponse(IAsyncResult ar)
        {
            var versionRegex = new Regex($@"<meta\s+name\s*=\s*'{SoftwareName}'\s+content\s*=\s*'(\d+\.\d+\.\d+\.\d+)'\s*>");
            var baseUrlRegex = new Regex(@"<meta\s+name\s*=\s*'BaseUrl'\s+content\s*=\s*'(.+)'\s*>");
            var webRequest = (WebRequest)ar.AsyncState;
            Stream responseStream;
            try
            {
                responseStream = webRequest.EndGetResponse(ar).GetResponseStream();
            }
            catch (Exception exception)
            {
                MessageBox.Show(string.Format("检查更新失败, 错误信息:{0}{1}{0}请联系TC以了解详情",
                    Environment.NewLine, exception.Message), @"Update Check Fail");
                responseStream = null;
            }
            if (responseStream == null) return;

            var streamReader = new StreamReader(responseStream);
            var context = streamReader.ReadToEnd();
            var result = versionRegex.Match(context);
            var urlResult = baseUrlRegex.Match(context);
            if (!result.Success || !result.Success) return;

            var remoteVersion = Version.Parse(result.Groups[1].Value);
            if (CurrentVersion >= remoteVersion)
            {
                return;
            }
            var dialogResult = MessageBox.Show(caption: @"Wow! Such Impressive", text: $"新车已发车 v{remoteVersion}，上车!",
                                               buttons: MessageBoxButtons.YesNo, icon: MessageBoxIcon.Asterisk);
            if (dialogResult != DialogResult.Yes) return;
            var formUpdater = new FormUpdater(Application.ExecutablePath, remoteVersion, urlResult.Groups[1].Value);
            formUpdater.ShowDialog();
        }

        public static void CheckUpdate()
        {
            if (!IsConnectInternet()) return;
            var webRequest = (HttpWebRequest)WebRequest.Create("https://tautcony.github.io/tcupdate");
            #if DEBUG
            webRequest = (HttpWebRequest)WebRequest.Create("http://127.0.0.1:4000/tcupdate.html");
            #endif
            var userName               = Environment.UserName.ToCharArray().Aggregate("", (current, c) => current + $"{(int)c:X} ");
            webRequest.UserAgent       = $"{userName}({Environment.OSVersion}) / {Assembly.GetExecutingAssembly().GetName().FullName}";
            webRequest.Method          = "GET";
            webRequest.Credentials     = CredentialCache.DefaultCredentials;
            webRequest.KeepAlive       = false;
            webRequest.ProtocolVersion = HttpVersion.Version10;
            webRequest.BeginGetResponse(OnResponse, webRequest);
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
}