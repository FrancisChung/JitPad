﻿using System;
using System.IO;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using JitPad.Foundation;
using Reactive.Bindings.Extensions;
using System.Diagnostics;

namespace JitPad.Core
{
    public class AppContext : NotificationObject, IDisposable
    {
        public ProcessingUnit ProcessingUnit { get; }

        private readonly Config _config;
        private readonly CompositeDisposable _Trashes = new CompositeDisposable();

        public AppContext(Config config)
        {
            _config = config;

            var sourceCode = _config.CodeTemplate;
            {
                try
                {
                    sourceCode = File.ReadAllText(config.MonitoringFilePath);
                }
                catch
                {
                    _config.IsFileMonitoring = false;
                    _config.MonitoringFilePath = "";
                }
            }

            ProcessingUnit = new ProcessingUnit(_config)
            {
                SourceCode = sourceCode
            };

            SetupFileMonitoring();
        }

        public void Dispose()
        {
            ReleaseFileMonitor();

            _Trashes.Dispose();
            ProcessingUnit.Dispose();
        }

        #region file monitoring

        private FileMonitor? _fileMonitor;
        private IDisposable? _fileMonitorChanged;

        public void ReloadMonitoringFile()
        {
            LoadMonitoringFile();
        }

        public void OpenConfigFolder()
        {
            var dir = Path.GetDirectoryName(_config.FilePath);

            Process.Start("explorer", $"\"{dir}\"");
        }

        public void OpenAboutJitPad()
        {
            const string url = "https://github.com/YoshihiroIto/JitPad";

            Process.Start(new ProcessStartInfo("cmd", $"/c start {url}") {CreateNoWindow = true});
        }

        private void SetupFileMonitoring()
        {
            Observable
                .Merge(_config.ObserveProperty(x => x.IsFileMonitoring).ToUnit())
                .Merge(_config.ObserveProperty(x => x.MonitoringFilePath).ToUnit())
                .Subscribe(_ => UpdateFileMonitoring())
                .AddTo(_Trashes);
        }

        private void UpdateFileMonitoring()
        {
            ReleaseFileMonitor();

            if (_config.IsFileMonitoring && File.Exists(_config.MonitoringFilePath))
            {
                _fileMonitor = new FileMonitor(_config.MonitoringFilePath);
                _fileMonitorChanged = _fileMonitor.Changed
                    .Subscribe(x => LoadMonitoringFile());
            }
        }

        private void LoadMonitoringFile()
        {
            ProcessingUnit.SourceCode =
                File.Exists(_config.MonitoringFilePath)
                    ? File.ReadAllText(_config.MonitoringFilePath)
                    : _config.CodeTemplate;
        }

        private void ReleaseFileMonitor()
        {
            _fileMonitorChanged?.Dispose();
            _fileMonitor?.Dispose();

            _fileMonitor = null;
            _fileMonitorChanged = null;
        }

        #endregion
    }
}