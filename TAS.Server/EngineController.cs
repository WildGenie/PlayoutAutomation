﻿using System;
using System.Collections.Generic;
using System.Configuration;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.FtpClient;
using System.Threading.Tasks;
using System.Xml.Serialization;
using TAS.Common;
using TAS.Common.Database;
using TAS.Common.Database.Interfaces;
using TAS.Common.Interfaces.MediaDirectory;
using TAS.Server.Media;

namespace TAS.Server
{
    public class EngineController
    {
        private readonly double _referenceLoudnessLevel;

        private EngineController()
        {
            if (!double.TryParse(ConfigurationManager.AppSettings["ReferenceLoudnessLevel"], NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out _referenceLoudnessLevel))
                _referenceLoudnessLevel = -23;
        }

        private static readonly NLog.Logger Logger = NLog.LogManager.GetCurrentClassLogger();

        public static EngineController Current { get; } = new EngineController();

        public List<CasparServer> Servers;

        public List<IngestDirectory> IngestDirectories { get; set; }

        public List<Engine> Engines { get; private set; }

        public ArchiveDirectory[] ArchiveDirectories { get; private set; }

        public IDatabase Database { get; private set; }

        public double ReferenceLoudnessLevel => _referenceLoudnessLevel;

        public void LoadDbProvider()
        {
            Enum.TryParse<DatabaseType>(ConfigurationManager.AppSettings["DatabaseType"], out var databaseType);
            Database = DatabaseProviderLoader.LoadDatabaseProviders().FirstOrDefault(db => db.DatabaseType == databaseType);
            Logger.Debug("Connecting to database");
            Database.Open(ConfigurationManager.ConnectionStrings);
            Database.InitializeFieldLengths();
        }
        public void InitializeEngines()
        {
            FtpTrace.AddListener(new NLog.NLogTraceListener());
            Logger.Info("Engines initializing");
            Servers = Database.LoadServers<CasparServer>();
            Servers.ForEach(s =>
            {
                s.ChannelsSer.ForEach(c => c.Owner = s);
                s.RecordersSer.ForEach(r => r.SetOwner(s));
            });

            Engines = Database.LoadEngines<Engine>(ulong.Parse(ConfigurationManager.AppSettings["Instance"]));
            foreach (var e in Engines)
                e.Initialize(Servers);
            LoadArchiveDirectories();
            foreach (var e in Engines)
                ((MediaManager) e.MediaManager).Initialize(
                    ArchiveDirectories.FirstOrDefault(a => a.IdArchive == e.IdArchive));
            Logger.Debug("Engines initialized");
        }

        private void LoadArchiveDirectories()
        {
            ArchiveDirectories = Engines.Where(e => e.IdArchive > 0)
                .Select(e => Database.LoadArchiveDirectory<ArchiveDirectory>(e.IdArchive)).ToArray();
            foreach (var archiveDirectory in ArchiveDirectories)
                archiveDirectory?.RefreshVolumeInfo();
        }

        public void LoadIngestDirectories()
        {
            Logger.Debug("Loading ingest directories");
            var fileName = Path.Combine(Directory.GetCurrentDirectory(), ConfigurationManager.AppSettings["IngestFolders"]);
            if (!string.IsNullOrEmpty(fileName) && File.Exists(fileName))
            {
                var reader = new XmlSerializer(typeof(List<IngestDirectory>), new XmlRootAttribute(nameof(IngestDirectories)));
                using (var file = new StreamReader(fileName))
                    IngestDirectories = ((List<IngestDirectory>)reader.Deserialize(file)).ToList();
                foreach (var d in IngestDirectories)
                    d.Initialize();
            }
            else IngestDirectories = new List<IngestDirectory>();
            Logger.Debug("IngestDirectories loaded");
        }

        public void ShutDown()
        {
            Engines?.ForEach(e => e.Dispose());
            Logger.Info("Engines shutdown completed");
            Database?.Close();
            Logger.Info("Database closed");
            Servers?.ForEach(s => s.Dispose());
        }

        public int GetConnectedClientCount() => Engines.Sum(e => e.Remote?.ClientCount ?? 0);
    }
}
