using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using MaxMind.GeoIP2;
using Microsoft.Win32.TaskScheduler;
using Serilog;
using Task = System.Threading.Tasks.Task;

namespace Netch.Utils
{
    public static class Misc
    {
        public static void Open(string path)
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = "explorer.exe",
                    Arguments = path,
                    UseShellExecute = true
                });
            }
            catch (Exception e)
            {
                Log.Warning(e, "打开 {Uri} 失败", path);
            }
        }

        public static async Task<int> TCPingAsync(IPAddress ip, int port, int timeout = 1000, CancellationToken ct = default)
        {
            using var client = new TcpClient(ip.AddressFamily);

            var stopwatch = Stopwatch.StartNew();

            var task = client.ConnectAsync(ip, port, ct).AsTask();

            var resTask = await Task.WhenAny(task, Task.Delay(timeout, ct));

            stopwatch.Stop();
            if (resTask == task && client.Connected)
            {
                var t = Convert.ToInt32(stopwatch.Elapsed.TotalMilliseconds);
                return t;
            }

            return timeout;
        }

        public static async Task<int> ICMPingAsync(IPAddress ip, int timeout = 1000)
        {
            var reply = await new Ping().SendPingAsync(ip, timeout);

            if (reply.Status == IPStatus.Success)
                return Convert.ToInt32(reply.RoundtripTime);

            return timeout;
        }

        public static string GetCityCode(string hostname)
        {
            if (hostname.Contains(":"))
                hostname = hostname.Split(':')[0];

            string? country = null;
            try
            {
                var databaseReader = new DatabaseReader("bin\\GeoLite2-Country.mmdb");

                if (IPAddress.TryParse(hostname, out _))
                {
                    country = databaseReader.Country(hostname).Country.IsoCode;
                }
                else
                {
                    var dnsResult = DnsUtils.Lookup(hostname);

                    if (dnsResult != null)
                        country = databaseReader.Country(dnsResult).Country.IsoCode;
                }
            }
            catch
            {
                // ignored
            }

            country ??= "Unknown";

            return country;
        }

        public static string Sha256CheckSum(string filePath)
        {
            try
            {
                var sha256 = SHA256.Create();
                using var fileStream = File.OpenRead(filePath);
                return string.Concat(sha256.ComputeHash(fileStream).Select(b => b.ToString("x2")));
            }
            catch
            {
                return "";
            }
        }

        public static string GetFileVersion(string file)
        {
            if (File.Exists(file))
                return FileVersionInfo.GetVersionInfo(file).FileVersion ?? "";

            return "";
        }

        public static void DrawCenterComboBox(object sender, DrawItemEventArgs e)
        {
            if (sender is ComboBox cbx)
            {
                e.DrawBackground();

                if (e.Index < 0)
                    return;

                TextRenderer.DrawText(e.Graphics,
                    cbx.Items[e.Index].ToString(),
                    cbx.Font,
                    e.Bounds,
                    (e.State & DrawItemState.Selected) == DrawItemState.Selected ? SystemColors.HighlightText : cbx.ForeColor,
                    TextFormatFlags.HorizontalCenter);
            }
        }

        public static void ComponentIterator(in Component component, in Action<Component> func)
        {
            func.Invoke(component);
            switch (component)
            {
                case ListView listView:
                    // ListView sub item
                    foreach (var item in listView.Columns.Cast<ColumnHeader>())
                        ComponentIterator(item, func);

                    break;
                case ToolStripMenuItem toolStripMenuItem:
                    // Iterator Menu strip sub item
                    foreach (var item in toolStripMenuItem.DropDownItems.Cast<ToolStripItem>())
                        ComponentIterator(item, func);

                    break;
                case MenuStrip menuStrip:
                    // Menu Strip
                    foreach (var item in menuStrip.Items.Cast<ToolStripItem>())
                        ComponentIterator(item, func);

                    break;
                case ContextMenuStrip contextMenuStrip:
                    foreach (var item in contextMenuStrip.Items.Cast<ToolStripItem>())
                        ComponentIterator(item, func);

                    break;
                case Control control:
                    foreach (var c in control.Controls.Cast<Control>())
                        ComponentIterator(c, func);

                    if (control.ContextMenuStrip != null)
                        ComponentIterator(control.ContextMenuStrip, func);

                    break;
            }
        }

        public static void RegisterNetchStartupItem()
        {
            const string TaskName = "Netch Startup";
            var folder = TaskService.Instance.GetFolder("\\");
            var taskIsExists = folder.Tasks.Any(task => task.Name == TaskName);

            if (Global.Settings.RunAtStartup)
            {
                if (taskIsExists)
                    folder.DeleteTask(TaskName, false);

                var td = TaskService.Instance.NewTask();

                td.RegistrationInfo.Author = "Netch";
                td.RegistrationInfo.Description = "Netch run at startup.";
                td.Principal.RunLevel = TaskRunLevel.Highest;

                td.Triggers.Add(new LogonTrigger());
                td.Actions.Add(new ExecAction(Global.NetchExecutable));

                td.Settings.ExecutionTimeLimit = TimeSpan.Zero;
                td.Settings.DisallowStartIfOnBatteries = false;
                td.Settings.StopIfGoingOnBatteries = false;
                td.Settings.IdleSettings.StopOnIdleEnd = false;
                td.Settings.IdleSettings.RestartOnIdle = false;
                td.Settings.RunOnlyIfIdle = false;
                td.Settings.Compatibility = TaskCompatibility.V2_1;

                TaskService.Instance.RootFolder.RegisterTaskDefinition("Netch Startup", td);
            }
            else
            {
                if (taskIsExists)
                    folder.DeleteTask(TaskName, false);
            }
        }

        public static void ChangeControlForeColor(Component component, Color color)
        {
            switch (component)
            {
                case TextBox _:
                case ComboBox _:
                    if (((Control)component).ForeColor != color)
                        ((Control)component).ForeColor = color;

                    break;
            }
        }

        public static int SubnetToCidr(string value)
        {
            var subnet = IPAddress.Parse(value);
            return SubnetToCidr(subnet);
        }

        public static int SubnetToCidr(IPAddress subnet)
        {
            return subnet.GetAddressBytes().Sum(b => Convert.ToString(b, 2).Count(c => c == '1'));
        }

        public static string HostAppendPort(string host, ushort port = 53)
        {
            if (!host.Contains(':'))
                host += $":{port}";

            return host;
        }

        public static string GetHostFromUri(string str)
        {
            var startIndex = str.LastIndexOf('/');
            if (startIndex != -1)
                str = str[(startIndex + 1)..];

            var endIndex = str.IndexOf(':');
            return endIndex == -1 ? str : str[..endIndex];
        }
    }
}