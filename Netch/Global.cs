using Netch.Forms;
using Netch.Models;
using System;
using System.Collections.Generic;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Windows.Forms;
using Netch.Services;

namespace Netch
{
    public static class Global
    {
        /// <summary>
        ///     主窗体的静态实例
        /// </summary>
        private static readonly Lazy<MainForm> LazyMainForm = new(DI.GetRequiredService<MainForm>);

        /// <summary>
        ///     用于读取和写入的配置
        /// </summary>
        public static Setting Settings = new();

        /// <summary>
        ///     用于存储模式
        /// </summary>
        public static readonly List<Mode> Modes = new();

        public static readonly string NetchDir;
        public static readonly string NetchExecutable;

        static Global()
        {
            NetchExecutable = Application.ExecutablePath;
            NetchDir = Application.StartupPath;
        }

        /// <summary>
        ///     主窗体的静态实例
        /// </summary>
        public static MainForm MainForm => LazyMainForm.Value;

        public static JsonSerializerOptions NewDefaultJsonSerializerOptions => new()
        {
            WriteIndented = true,
            IgnoreNullValues = true,
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        };
    }
}