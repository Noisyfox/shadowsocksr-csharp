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
        public abstract Server GetAServer(IStrategyCallerType type, IPEndPoint localIPEndPoint, EndPoint destEndPoint);
        public abstract void UpdateLatency(Server server, TimeSpan latency);
        public abstract void UpdateLastRead(Server server);
        public abstract void UpdateLastWrite(Server server);
        public abstract void SetFailure(Server server);
        public abstract void Activate();
        public abstract void Deactivate();
        public abstract void Dispose();

        public virtual List<MenuItem> MenuItems { get; } = null;
    }


    public abstract class ManagedStrategy<TConfig, TServerPersistence, TServerMemory> : ManagedStrategy
        where TConfig : new()
        where TServerPersistence : new()
        where TServerMemory : new()
    {
        public ShadowsocksController Controller { get; }
        public TConfig Config { get; private set; }


        private readonly SpinLock _persistenceLock = new SpinLock(true);
        private readonly SpinLock _memoryLock = new SpinLock(true);
        private readonly SpinLock _configLock = new SpinLock(true);

        private readonly Dictionary<string, TServerPersistence> _persistencesStorage = new Dictionary<string, TServerPersistence>();
        private readonly Dictionary<string, TServerMemory> _memoryStorage = new Dictionary<string, TServerMemory>();

        private readonly string _configStorageFilePath;
        private readonly string _persistencesStorageFilePath;

        public ReadOnlyCollection<Server> CurrentServers { get; private set; } = new List<Server>().AsReadOnly();

        protected ManagedStrategy(ShadowsocksController controller)
        {
            Controller = controller;

            _configStorageFilePath = Path.Combine(StrategyPath, $"{ID}.config.json");
            _persistencesStorageFilePath = Path.Combine(StrategyPath, $"{ID}.persist.json");

            LoadConfig();
            LoadPersistData();
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
                        _persistencesStorage.Remove(server.Identifier());
                        _memoryStorage.Remove(server.Identifier());
                    }

                    foreach (var server in addSet)
                    {
                        _persistencesStorage.Add(server.Identifier(), new TServerPersistence());
                        _memoryStorage.Add(server.Identifier(), new TServerMemory());
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

        private void LoadPersistData()
        {
            var path = _persistencesStorageFilePath;
            Logging.Debug($"loading persist data from {path}");

            Dictionary<string, TServerPersistence> data;

            if (Utils.DeserializeFromFile(path, out data))
            {
                _persistencesStorage.Clear();

                if (data != null)
                {
                    foreach (var kv in data)
                    {
                        _persistencesStorage.Add(kv.Key, kv.Value);
                    }
                }
            }
            else
            {
                Console.WriteLine("failed to load persist data");
            }
        }

        private void LoadConfig()
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

            Config = data;
        }

        public void SavePersistData()
        {
            var path = _persistencesStorageFilePath;

            Dictionary<string, TServerPersistence> d = new Dictionary<string, TServerPersistence>(_persistencesStorage);

            Logging.Debug($"save persist data to {path}");

            Utils.SerializeToFile(path, d);
        }

        protected void SaveConfig()
        {
            var path = _configStorageFilePath;

            Logging.Debug($"save config to {path}");

            Utils.SerializeToFile(path, Config);
        }


        public ServerData<TServerPersistence> AquirePersistenceExclusive()
        {
            return new ServerData<TServerPersistence>(_persistenceLock, _persistencesStorage);
        }


        public ServerData<TServerMemory> AquireMemoryExclusive()
        {
            return new ServerData<TServerMemory>(_memoryLock, _memoryStorage);
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

                Monitor.Enter(_lock, ref _taken);
            }

            public void Dispose()
            {
                if (_taken)
                {
                    Monitor.Exit(_lock);
                }

                GC.SuppressFinalize(this);
            }
        }

        public class ServerData<TRes> : Exclusive
        {
            private readonly Dictionary<string, TRes> _data;

            protected internal ServerData(SpinLock l, Dictionary<string, TRes> data) : base(l)
            {
                _data = data;
            }

            public TRes GetData(Server server)
            {
                string key = server.Identifier();
                if (_data.ContainsKey(key))
                {
                    return _data[key];
                }

                return default(TRes);
            }
        }

        #endregion

    }
}
