
using Stormancer.Diagnostics;
using Stormancer.Server.Plugins.Configuration;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Stormancer.Server.Plugins.Users.OAuth
{
    internal class KeysMaintenanceWorker
    {
        private readonly ConfigurationMonitor<OAuthConfigurationSection> _configuration;
        private readonly JWKManager _keysManager;
        private readonly ILogger _logger;
        private readonly PeriodicTimer _timer = new PeriodicTimer(TimeSpan.FromMinutes(5));

        public KeysMaintenanceWorker(ConfigurationMonitor<OAuthConfigurationSection> configuration, JWKManager keysManager, ILogger logger)
        {
            _configuration = configuration;
            _keysManager = keysManager;
            _logger = logger;
        }

        public async Task RunAsync(CancellationToken cancellationToken)
        {
            do
            {
                try
                {
                    if(_configuration.Value.KeysPath == null)
                    {
                        continue;
                    }

                    var keys = (await _keysManager.GetPrivateKeys()).Keys.ToArray();

                    var dirty = false;
                    var obsoleteKeys = keys.Where(k => k.CreatedOn + _configuration.Value.KeysRetentionPeriod < DateTime.UtcNow);

                    foreach(var key in obsoleteKeys)
                    {
                        dirty = true;
                        _keysManager.RemovePrivateKey(key.Key.KeyId);
                    }


                    var lastKey = keys.OrderByDescending(k => k.CreatedOn).FirstOrDefault();
                    if(lastKey == null || lastKey.CreatedOn + _configuration.Value.KeyRenewalPeriod < DateTime.UtcNow)
                    {
                        dirty = true;
                        _keysManager.AddPrivateKey(_keysManager.CreatePrivateKey(Guid.NewGuid().ToString(), _configuration.Value.KeySize));
                    }

                    if(dirty)
                    {
                        await _keysManager.SaveKeysToStorage();
                    }
                }
                catch (Exception ex)
                {
                    _logger.Log(LogLevel.Error, "maintenance.jwk", "An error occurred while running keys maintenance.", ex);
                }
            }
            while (await _timer.WaitForNextTickAsync(cancellationToken));
        }
    }
}
