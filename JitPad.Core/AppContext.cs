﻿using System;
using System.IO;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using JitPad.Foundation;
using Reactive.Bindings.Extensions;
using System.Diagnostics;
using JitPad.Core.Processor;

namespace JitPad.Core
{
    public class AppContext : NotificationObject, IDisposable
    {
        public BuildingUnit BuildingUnit { get; }

        private readonly Config _config;
        private readonly CompositeDisposable _Trashes = new CompositeDisposable();

        public AppContext(Config config)
        {
            _config = config;

            var sourceCode = "";
            {
                try
                {
                    sourceCode = _config.LoadCodeTemplate();
                }
                catch
                {
                    _config.IsFileMonitoring = false;
                    _config.MonitoringFilePath = "";
                }
            }
            
            var compiler = new Compiler();
            var disassembler = new JitDisassembler("JitDasm/JitDasm.exe");

            BuildingUnit = new BuildingUnit(_config, compiler, disassembler)
            {
                SourceCode = sourceCode
            };

            SetupFileMonitoring();

            _config.ObserveProperty(x => x.MonitoringFilePath)
                .Subscribe(_ => LoadMonitoringFile())
                .AddTo(_Trashes);
        }

        public void Dispose()
        {
            ReleaseFileMonitor();

            _Trashes.Dispose();
            BuildingUnit.Dispose();
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

        #region file monitoring

        private FileMonitor? _fileMonitor;
        private IDisposable? _fileMonitorChanged;

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

        public void LoadMonitoringFile()
        {
            if (File.Exists(_config.MonitoringFilePath))
                BuildingUnit.SourceCode = File.ReadAllText(_config.MonitoringFilePath);
            else
                ApplyTemplateFile();
        }

        public void ApplyTemplateFile()
        {
            _config.MonitoringFilePath = "";
            BuildingUnit.SourceCode = _config.LoadCodeTemplate();
        }

        private void ReleaseFileMonitor()
        {
            _fileMonitorChanged?.Dispose();
            _fileMonitor?.Dispose();

            _fileMonitorChanged = null;
            _fileMonitor = null;
        }

        #endregion
    }
}