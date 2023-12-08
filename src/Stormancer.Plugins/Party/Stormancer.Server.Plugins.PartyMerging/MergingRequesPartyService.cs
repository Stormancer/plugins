using Org.BouncyCastle.Asn1.Ocsp;
using Stormancer.Core;
using Stormancer.Server.Plugins.Party;
using Stormancer.Server.Plugins.Party.Dto;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Stormancer.Server.Plugins.PartyMerging
{

    internal class MergingRequestPartyState
    {
        private Task? _task;

        /// <summary>
        /// Gets a value indicating if a merging operation is currently in progress.
        /// </summary>
        public bool IsMergingInProgress => _task != null;
        public bool IsMergingCancelled => _cts?.IsCancellationRequested ?? false;
        public string? CurrentPartyMergerId { get; internal set; }

        private CancellationTokenSource? _cts;
        internal ValueTask StartMerging(Func<CancellationToken, Task> task, CancellationToken cancellationToken)
        {

            _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);


            _task = task(_cts.Token);

            return WhenMergingCompleteAsync();

        }

        /// <summary>
        /// Cancels merging and wait for completion.
        /// </summary>
        /// <returns></returns>
        public ValueTask CancelMerging()
        {
            if (_cts != null)
            {
                _cts.Cancel();

            }
            CurrentPartyMergerId = null;

            return WhenMergingCompleteAsync();

        }

        /// <summary>
        /// Wait asynchronously for the merging task to complete.
        /// </summary>
        /// <returns></returns>
        public async ValueTask WhenMergingCompleteAsync()
        {
            if (_task == null)
            {
                return;
            }
            else
            {
                try
                {
                    await _task;
                }
                finally
                {
                    _task = null;
                    _cts?.Dispose();
                    _cts = null;
                }
            }
        }
    }
    /// <summary>
    /// Controls merging operations in a party.
    /// </summary>
    public interface IMergingPartyService
    {
        /// <summary>
        /// Cancels merging and wait for completion.
        /// </summary>
        /// <returns></returns>
        public ValueTask CancelMerging();


        /// <summary>
        /// Pause the merging process.
        /// </summary>
        /// <returns></returns>
        public ValueTask PauseMerging();

        /// <summary>
        /// Restarts a paused merging process
        /// </summary>
        /// <returns></returns>
        public bool TryRestartMerging();

        /// <summary>
        /// Wait asynchronously for the merging task to complete.
        /// </summary>
        /// <returns></returns>
        public ValueTask WhenMergingCompleteAsync();

        /// <summary>
        /// Starts merging the party.
        /// </summary>
        /// <param name="partyMergerId"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        public ValueTask StartAsync(string partyMergerId, CancellationToken cancellationToken);

    }

    internal class MergingPartyService : IMergingPartyService
    {
        private readonly MergingRequestPartyState _state;
        private readonly ISceneHost _scene;

        public MergingPartyService(MergingRequestPartyState state, ISceneHost scene)
        {
            _state = state;
            _scene = scene;

        }

        /// <summary>
        /// Cancels merging and wait for completion.
        /// </summary>
        /// <returns></returns>
        public ValueTask CancelMerging()
        {

            return _state.CancelMerging();
        }
        /// <summary>
        /// Wait asynchronously for the merging task to complete.
        /// </summary>
        /// <returns></returns>
        public ValueTask WhenMergingCompleteAsync()
        {
            return _state.WhenMergingCompleteAsync();
        }

        /// <summary>
        /// Starts merging the party.
        /// </summary>
        /// <param name="partyMergerId"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        public ValueTask StartAsync(string partyMergerId, CancellationToken cancellationToken)
        {

            static async Task RunMergingRequestAsync(ISceneHost scene, string partyMergerId, CancellationToken cancellationToken)
            {
                await using var scope = scene.CreateRequestScope();

                var _state = scope.Resolve<MergingRequestPartyState>();
                var _party = scope.Resolve<IPartyService>();
                var _partyMerger = scope.Resolve<PartyMergerProxy>();
                var _serializer = scope.Resolve<ISerializer>();
                try
                {

                    _state.CurrentPartyMergerId = partyMergerId;
                    await _party.UpdateSettings(state =>
                     {


                         var partySettings = new PartySettingsDto(state);
                         if (partySettings.PublicServerData == null)
                         {
                             partySettings.PublicServerData = new Dictionary<string, string>();
                         }
                         partySettings.PublicServerData["stormancer.partyMerging.status"] = "InProgress";
                         partySettings.PublicServerData["stormancer.partyMerging.merger"] = partyMergerId;
                         partySettings.PublicServerData.Remove("stormancer.partyMerging.lastError");
                         return partySettings;


                     }, cancellationToken);

                    if (!cancellationToken.IsCancellationRequested)
                    {
                        using var subscription = cancellationToken.Register(() =>
                        {
                            _ = _partyMerger.StopMerge(partyMergerId, _party.PartyId, CancellationToken.None);
                        });

                        var connectionToken = await _partyMerger.StartMerge(partyMergerId, _party.PartyId, cancellationToken);


                        var sessionIds = _party.PartyMembers.Where(kvp => kvp.Value.ConnectionStatus == Party.Model.PartyMemberConnectionStatus.Connected).Select(kvp => kvp.Key);
                        await scene.Send(new MatchArrayFilter(sessionIds),
                       "partyMerging.connectionToken",
                       s => _serializer.Serialize(connectionToken, s), PacketPriority.MEDIUM_PRIORITY, PacketReliability.RELIABLE);

                        await _party.UpdateSettings(state =>
                        {
                            var partySettings = new PartySettingsDto(state);
                            if (partySettings.PublicServerData == null)
                            {
                                partySettings.PublicServerData = new Dictionary<string, string>();
                            }
                            partySettings.PublicServerData["stormancer.partyMerging.status"] = connectionToken != null ? "PartyFound" : "Completed";

                            return partySettings;


                        }, cancellationToken);
                    }


                }
                catch (Exception ex)
                {
                    if (cancellationToken.IsCancellationRequested)
                    {

                        _ = _party.UpdateSettings(state =>
                        {


                            var partySettings = new PartySettingsDto(state);
                            if ((_state.IsMergingCancelled || !_state.IsMergingInProgress) && partySettings.PublicServerData["stormancer.partyMerging.status"] == "InProgress")
                            {
                                if (partySettings.PublicServerData == null)
                                {
                                    partySettings.PublicServerData = new System.Collections.Generic.Dictionary<string, string>();
                                }
                                partySettings.PublicServerData["stormancer.partyMerging.status"] = "Cancelled";
                            }
                            return partySettings;


                        }, CancellationToken.None);
                        throw new ClientException("cancelled");
                    }
                    else
                    {
                        await _party.UpdateSettings(state =>
                        {


                            var partySettings = new PartySettingsDto(state);
                            if (partySettings.PublicServerData == null)
                            {
                                partySettings.PublicServerData = new System.Collections.Generic.Dictionary<string, string>();
                            }
                            partySettings.PublicServerData["stormancer.partyMerging.status"] = "Error";
                            partySettings.PublicServerData["stormancer.partyMerging.lastError"] = ex.Message;
                            return partySettings;


                        }, CancellationToken.None);
                    }
                    throw;
                }
                finally
                {
                    _state.CurrentPartyMergerId = partyMergerId;
                }
            };


            return _state.StartMerging(ct => RunMergingRequestAsync(_scene, partyMergerId, ct), cancellationToken);

        }

        public async ValueTask PauseMerging()
        {
            var currentPartyMergerId = _state.CurrentPartyMergerId;
            try
            {
                await CancelMerging();
            }
            catch (OperationCanceledException)
            {

            }
            _state.CurrentPartyMergerId = currentPartyMergerId;
        }

        public bool TryRestartMerging()
        {
            if (_state.CurrentPartyMergerId != null)
            {

                _ = StartAsync(_state.CurrentPartyMergerId, CancellationToken.None);
                return true;
            }
            else
            {
                return false;
            }
        }
    }
}
