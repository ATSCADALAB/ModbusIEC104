using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using IEC104;
using ModbusIEC104.Common;

namespace ModbusIEC104
{
    public class DeviceReader : IDisposable
    {
        #region FIELDS
        private readonly IEC104Driver driver;
        private readonly object lockObject = new object();
        private readonly List<BlockReader> blockReaders;
        private Timer readTimer;
        private bool isDisposed = false;
        private bool isInitialized = false;
        #endregion

        #region PROPERTIES
        public string DeviceName { get; set; }
        public string DeviceID { get; set; }
        public IEC104DeviceSettings Settings { get; set; }
        public bool IsRunning { get; private set; }
        public DateTime LastReadTime { get; private set; }
        public string LastError { get; private set; }
        public int ReadCount { get; private set; }
        public int ErrorCount { get; private set; }

        // IEC104 specific properties
        public ushort CommonAddress => Settings?.CommonAddress ?? 1;
        public bool IsConnected => GetClientAdapter()?.IsConnected ?? false;
        public DateTime LastInterrogationTime { get; private set; }
        #endregion

        #region CONSTRUCTOR
        public DeviceReader(IEC104Driver driver)
        {
            this.driver = driver ?? throw new ArgumentNullException(nameof(driver));
            this.blockReaders = new List<BlockReader>();
            this.LastReadTime = DateTime.MinValue;
            this.LastInterrogationTime = DateTime.MinValue;
        }
        #endregion

        #region INITIALIZE & DISPOSE
        public bool Initialize()
        {
            try
            {
                if (isInitialized)
                    return true;

                if (Settings == null)
                    throw new InvalidOperationException("Settings cannot be null");

                // Create block readers from settings
                CreateBlockReaders();

                // Initialize read timer
                InitializeReadTimer();

                isInitialized = true;
                return true;
            }
            catch (Exception ex)
            {
                LastError = $"Initialize failed: {ex.Message}";
                return false;
            }
        }

        public void Dispose()
        {
            if (!isDisposed)
            {
                Stop();
                readTimer?.Dispose();

                lock (lockObject)
                {
                    foreach (var blockReader in blockReaders)
                    {
                        blockReader?.Dispose();
                    }
                    blockReaders.Clear();
                }

                isDisposed = true;
            }
        }
        #endregion

        #region PUBLIC METHODS
        public bool Start()
        {
            try
            {
                if (!isInitialized)
                {
                    if (!Initialize())
                        return false;
                }

                if (IsRunning)
                    return true;

                // Start the read timer
                var interval = Settings.ReadInterval > 0 ? Settings.ReadInterval : 1000;
                readTimer?.Change(0, interval);

                IsRunning = true;
                return true;
            }
            catch (Exception ex)
            {
                LastError = $"Start failed: {ex.Message}";
                return false;
            }
        }

        public bool Stop()
        {
            try
            {
                if (!IsRunning)
                    return true;

                // Stop the read timer
                readTimer?.Change(Timeout.Infinite, Timeout.Infinite);

                lock (lockObject)
                {
                    foreach (var blockReader in blockReaders)
                    {
                        blockReader.Stop();
                    }
                }

                IsRunning = false;
                return true;
            }
            catch (Exception ex)
            {
                LastError = $"Stop failed: {ex.Message}";
                return false;
            }
        }

        public bool ReadAllBlocks()
        {
            if (!IsRunning || isDisposed)
                return false;

            try
            {
                bool allSuccess = true;

                lock (lockObject)
                {
                    foreach (var blockReader in blockReaders)
                    {
                        if (!blockReader.ReadBlock())
                        {
                            allSuccess = false;
                            ErrorCount++;
                        }
                    }
                }

                LastReadTime = DateTime.Now;
                ReadCount++;

                // Check if interrogation is needed
                CheckAndPerformInterrogation();

                return allSuccess;
            }
            catch (Exception ex)
            {
                LastError = $"ReadAllBlocks failed: {ex.Message}";
                ErrorCount++;
                return false;
            }
        }

        public bool SendInterrogation(InterrogationType type = InterrogationType.General)
        {
            try
            {
                var clientAdapter = GetClientAdapter();
                if (clientAdapter == null)
                    return false;

                var result = clientAdapter.SendInterrogation(CommonAddress, type);
                if (result)
                {
                    LastInterrogationTime = DateTime.Now;
                }

                return result;
            }
            catch (Exception ex)
            {
                LastError = $"SendInterrogation failed: {ex.Message}";
                return false;
            }
        }

        public bool SendCommand(uint informationObjectAddress, byte typeId, object value, bool selectBeforeOperate = false)
        {
            try
            {
                var clientAdapter = GetClientAdapter();
                if (clientAdapter == null)
                    return false;

                return clientAdapter.SendCommand(CommonAddress, informationObjectAddress, typeId, value, selectBeforeOperate);
            }
            catch (Exception ex)
            {
                LastError = $"SendCommand failed: {ex.Message}";
                return false;
            }
        }

        public List<InformationObject> ProcessSpontaneousData()
        {
            try
            {
                var clientAdapter = GetClientAdapter();
                if (clientAdapter == null)
                    return new List<InformationObject>();

                if (clientAdapter.ProcessSpontaneousData(out List<InformationObject> objects))
                {
                    return objects;
                }

                return new List<InformationObject>();
            }
            catch (Exception ex)
            {
                LastError = $"ProcessSpontaneousData failed: {ex.Message}";
                return new List<InformationObject>();
            }
        }
        #endregion

        #region PRIVATE METHODS
        private void CreateBlockReaders()
        {
            lock (lockObject)
            {
                blockReaders.Clear();

                if (Settings.BlockSettings == null || Settings.BlockSettings.Count == 0)
                    return;

                foreach (var blockSetting in Settings.BlockSettings)
                {
                    var blockReader = new BlockReader(this, blockSetting);
                    if (blockReader.Initialize())
                    {
                        blockReaders.Add(blockReader);
                    }
                }
            }
        }

        private void InitializeReadTimer()
        {
            var interval = Settings.ReadInterval > 0 ? Settings.ReadInterval : 1000;
            readTimer = new Timer(ReadTimerCallback, null, Timeout.Infinite, interval);
        }

        private void ReadTimerCallback(object state)
        {
            if (!IsRunning || isDisposed)
                return;

            Task.Run(() => ReadAllBlocks());
        }

        private void CheckAndPerformInterrogation()
        {
            if (Settings.InterrogationInterval <= 0)
                return;

            var timeSinceLastInterrogation = DateTime.Now - LastInterrogationTime;
            if (timeSinceLastInterrogation.TotalSeconds >= Settings.InterrogationInterval)
            {
                Task.Run(() => SendInterrogation(Settings.DefaultInterrogation));
            }
        }

        private IEC104ClientAdapter GetClientAdapter()
        {
            return driver?.GetClientAdapter(Settings.ClientID) as IEC104ClientAdapter;
        }
        #endregion

        #region HELPER METHODS
        public override string ToString()
        {
            return $"DeviceReader[{DeviceName}] - {Settings?.IpAddress}:{Settings?.Port} (COA: {CommonAddress})";
        }

        public string GetStatus()
        {
            var status = IsRunning ? "Running" : "Stopped";
            var connection = IsConnected ? "Connected" : "Disconnected";
            return $"{status} | {connection} | Reads: {ReadCount} | Errors: {ErrorCount}";
        }

        public Dictionary<string, object> GetDiagnosticInfo()
        {
            return new Dictionary<string, object>
            {
                ["DeviceName"] = DeviceName,
                ["DeviceID"] = DeviceID,
                ["IsRunning"] = IsRunning,
                ["IsConnected"] = IsConnected,
                ["ReadCount"] = ReadCount,
                ["ErrorCount"] = ErrorCount,
                ["LastReadTime"] = LastReadTime,
                ["LastInterrogationTime"] = LastInterrogationTime,
                ["LastError"] = LastError,
                ["BlockReadersCount"] = blockReaders.Count,
                ["CommonAddress"] = CommonAddress,
                ["IpAddress"] = Settings?.IpAddress,
                ["Port"] = Settings?.Port
            };
        }
        #endregion
    }

    #region SUPPORTING CLASSES
    public class IEC104DeviceSettings : DeviceSettings
    {
        #region IEC104 SPECIFIC PROPERTIES
        public ushort CommonAddress { get; set; } = 1;

        // Protocol Parameters
        public ushort K { get; set; } = 12;         // Max unacknowledged I-frames
        public ushort W { get; set; } = 8;          // ACK window

        // Timeouts (in seconds)
        public ushort T0 { get; set; } = 30;        // Connection timeout
        public ushort T1 { get; set; } = 15;        // Send timeout
        public ushort T2 { get; set; } = 10;        // Receive timeout
        public ushort T3 { get; set; } = 20;        // Test frame timeout

        // Interrogation Settings
        public InterrogationType DefaultInterrogation { get; set; } = InterrogationType.General;
        public int InterrogationInterval { get; set; } = 300; // seconds
        #endregion

        #region OVERRIDE PROPERTIES
        public override string ClientID => $"{IpAddress}-{Port}-{CommonAddress}";
        #endregion

        #region METHODS
        public static IEC104DeviceSettings Initialize(string deviceID)
        {
            var settings = new IEC104DeviceSettings();

            if (string.IsNullOrEmpty(deviceID))
                return settings;

            try
            {
                // Format: IP|Port|COA|K|W|T0|T1|T2|T3|InterrogationType|InterrogationInterval|BlockSettings
                var parts = deviceID.Split('|');

                if (parts.Length >= 2)
                {
                    settings.IpAddress = parts[0];
                    if (ushort.TryParse(parts[1], out ushort port))
                        settings.Port = port;
                }

                if (parts.Length >= 3 && ushort.TryParse(parts[2], out ushort coa))
                    settings.CommonAddress = coa;

                if (parts.Length >= 4 && ushort.TryParse(parts[3], out ushort k))
                    settings.K = k;

                if (parts.Length >= 5 && ushort.TryParse(parts[4], out ushort w))
                    settings.W = w;

                if (parts.Length >= 6 && ushort.TryParse(parts[5], out ushort t0))
                    settings.T0 = t0;

                if (parts.Length >= 7 && ushort.TryParse(parts[6], out ushort t1))
                    settings.T1 = t1;

                if (parts.Length >= 8 && ushort.TryParse(parts[7], out ushort t2))
                    settings.T2 = t2;

                if (parts.Length >= 9 && ushort.TryParse(parts[8], out ushort t3))
                    settings.T3 = t3;

                if (parts.Length >= 10 && Enum.TryParse(parts[9], out InterrogationType interrogationType))
                    settings.DefaultInterrogation = interrogationType;

                if (parts.Length >= 11 && int.TryParse(parts[10], out int interrogationInterval))
                    settings.InterrogationInterval = interrogationInterval;

                // Parse block settings if present
                if (parts.Length >= 12)
                {
                    settings.BlockSettings = ParseBlockSettings(parts[11]);
                }
            }
            catch (Exception ex)
            {
                // Log error but continue with default settings
                System.Diagnostics.Debug.WriteLine($"Error parsing IEC104DeviceSettings: {ex.Message}");
            }

            return settings;
        }

        private static List<BlockSettings> ParseBlockSettings(string blockSettingsString)
        {
            var blockSettings = new List<BlockSettings>();

            if (string.IsNullOrEmpty(blockSettingsString))
                return blockSettings;

            // Implementation depends on your block settings format
            // This is a placeholder - you should implement based on your actual format

            return blockSettings;
        }
        #endregion
    }

    public enum InterrogationType
    {
        General = 20,
        Group1 = 21,
        Group2 = 22,
        Group3 = 23,
        Group4 = 24,
        Group5 = 25,
        Group6 = 26,
        Group7 = 27,
        Group8 = 28,
        Group9 = 29,
        Group10 = 30,
        Group11 = 31,
        Group12 = 32,
        Group13 = 33,
        Group14 = 34,
        Group15 = 35,
        Group16 = 36
    }

    public class InformationObject
    {
        public uint ObjectAddress { get; set; }
        public byte TypeId { get; set; }
        public object Value { get; set; }
        public DateTime TimeStamp { get; set; }
        public byte Quality { get; set; }

        public override string ToString()
        {
            return $"IOA: {ObjectAddress}, Type: {TypeId}, Value: {Value}, Quality: {Quality:X2}";
        }
    }
    #endregion
}