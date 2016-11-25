using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Shadowsocks.Model;
using Shadowsocks.Util;
using Newtonsoft.Json;

namespace Shadowsocks.Controller.Strategy
{
    public abstract class ManagedStrategy : IStrategy
    {
        protected static readonly string StrategyPath;

        static ManagedStrategy()
        {
            try
            {
                Directory.CreateDirectory(Path.Combine(Application.StartupPath, "strategy"));
                StrategyPath = Path.Combine(Application.StartupPath, "strategy");
            }
            catch (Exception e)
            {
                StrategyPath = Utils.GetTempPath();
                Logging.LogUsefulException(e);
            }
        }

        public abstract string Name { get; }
        public abstract string ID { get; }
        public abstract void ReloadServers();
        public abstract Server GetAServer(StrategyCallerType type, IPEndPoint localIPEndPoint, EndPoint destEndPoint);
        public abstract void UpdateLatency(Server server, TimeSpan latency);
        public abstract void UpdateLastRead(Server server);
        public abstract void UpdateLastWrite(Server server);
        public abstract void SetFailure(Server server);
        public abstract void Activate();
        public abstract void Deactivate();
        public abstract void Dispose();

        public virtual MenuItem[] SubMenuItems { get; } = null;
    }


    public abstract class ManagedStrategy<TConfig, TPersistence, TServerPersistence, TMemory, TServerMemory> : ManagedStrategy
        where TConfig : new()
        where TPersistence : new()
        where TServerPersistence : new()
        where TMemory : new()
        where TServerMemory : new()
    {

        public ShadowsocksController Controller { get; }
        
        private readonly SpinLock _configLock = new SpinLock(true);

        private readonly StrategyDataStorage<TPersistence, TServerPersistence> _persistencesStorage;
        private readonly StrategyDataStorage<TMemory, TServerMemory> _memoryStorage;

        private readonly string _configStorageFilePath;
        private readonly string _persistencesStorageFilePath;

        public TConfig Config { get; }
        public ReadOnlyCollection<Server> CurrentServers { get; private set; } = new List<Server>().AsReadOnly();

        protected ManagedStrategy(ShadowsocksController controller)
        {
            Controller = controller;

            _configStorageFilePath = Path.Combine(StrategyPath, $"{ID}.config.json");
            _persistencesStorageFilePath = Path.Combine(StrategyPath, $"{ID}.persist.json");

            _memoryStorage = new StrategyDataStorage<TMemory, TServerMemory>();
            _memoryStorage.Init();

            Config = LoadConfig();
            _persistencesStorage = LoadPersistData();
        }

        public override void ReloadServers()
        {
            var newServerList = Controller.GetConfigurationCopy().configs.AsReadOnly();

            HashSet<Server> currentSet = new HashSet<Server>(CurrentServers);
            HashSet<Server> newSet = new HashSet<Server>(newServerList);

            var addSet = newSet.Except(currentSet);
            var removeSet = currentSet.Except(newSet);

            using (AquirePersistenceExclusive())
            {
                using (AquireMemoryExclusive())
                {

                    foreach (var server in removeSet)
                    {
                        _persistencesStorage.ServerData.Remove(server.Identifier());
                        _memoryStorage.ServerData.Remove(server.Identifier());
                    }

                    foreach (var server in addSet)
                    {
                        _persistencesStorage.ServerData.Add(server.Identifier(), new TServerPersistence());
                        _memoryStorage.ServerData.Add(server.Identifier(), new TServerMemory());
                    }
                }

                SavePersistData();
            }

            CurrentServers = newServerList;
        }

        public override void Deactivate()
        {
            using (AquirePersistenceExclusive())
            {
                SavePersistData();
            }

            using (AquireConfigExclusive())
            {
                SaveConfig();
            }
        }


        #region Data Management

        [Serializable]
        private class StrategyDataStorage<TData, TServerData>
            where TData : new()
            where TServerData : new()
        {
            [JsonIgnore]
            public SpinLock Lock { get; } = new SpinLock();

            public TData Data;
            public Dictionary<string, TServerData> ServerData;

            public void Init()
            {
                if (Data == null)
                {
                    Data = new TData();
                }
                if (ServerData == null)
                {
                    ServerData = new Dictionary<string, TServerData>();
                }
            }
        }

        private StrategyDataStorage<TPersistence, TServerPersistence> LoadPersistData()
        {
            var path = _persistencesStorageFilePath;
            Logging.Debug($"loading persist data from {path}");

            StrategyDataStorage<TPersistence, TServerPersistence> storage;

            if (!Utils.DeserializeFromFile(path, out storage))
            {
                Console.WriteLine("failed to load persist data");
            }

            if (storage == null)
            {
                storage = new StrategyDataStorage<TPersistence, TServerPersistence>();
            }

            storage.Init();

            return storage;
        }

        private TConfig LoadConfig()
        {
            var path = _configStorageFilePath;
            Logging.Debug($"loading config from {path}");

            TConfig data;

            if (!Utils.DeserializeFromFile(path, out data))
            {
                Console.WriteLine("failed to load config");
            }

            if (data == null)
            {
                data = new TConfig();
            }

            return data;
        }

        public void SavePersistData()
        {
            var path = _persistencesStorageFilePath;

            Logging.Debug($"save persist data to {path}");

            Utils.SerializeToFile(path, _persistencesStorage);
        }

        protected void SaveConfig()
        {
            var path = _configStorageFilePath;

            Logging.Debug($"save config to {path}");

            Utils.SerializeToFile(path, Config);
        }


        public ManagedData<TPersistence, TServerPersistence> AquirePersistenceExclusive()
        {
            return new ManagedData<TPersistence, TServerPersistence>(_persistencesStorage.Lock, _persistencesStorage.Data, _persistencesStorage.ServerData);
        }

        public ManagedData<TMemory, TServerMemory> AquireMemoryExclusive()
        {
            return new ManagedData<TMemory, TServerMemory>(_memoryStorage.Lock, _memoryStorage.Data, _memoryStorage.ServerData);
        }

        public Exclusive AquireConfigExclusive()
        {
            return new Exclusive(_configLock);
        }

        public class Exclusive : IDisposable
        {
            private readonly SpinLock _lock;
            private bool _taken = false;

            protected internal Exclusive(SpinLock l)
            {
                _lock = l;

                _lock.Enter(ref _taken);
            }

            public void Dispose()
            {
                if (_taken)
                {
                    _lock.Exit();
                }

                GC.SuppressFinalize(this);
            }
        }

        public class ManagedData<TData, TServerData> : Exclusive
        {
            private readonly Dictionary<string, TServerData> _serverData;

            protected internal ManagedData(SpinLock l, TData data, Dictionary<string, TServerData> serverData) : base(l)
            {
                Data = data;
                _serverData = serverData;
            }

            public TServerData GetData(Server server)
            {
                string key = server.Identifier();
                if (_serverData.ContainsKey(key))
                {
                    return _serverData[key];
                }

                return default(TServerData);
            }

            public TData Data { get; }
        }

        #endregion

    }
}
