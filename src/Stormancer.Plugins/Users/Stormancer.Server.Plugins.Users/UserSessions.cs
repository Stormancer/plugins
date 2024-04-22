// MIT License
//
// Copyright (c) 2019 Stormancer
//
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
//
// The above copyright notice and this permission notice shall be included in all
// copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
// SOFTWARE.

using Jose;
using Lucene.Net.Documents;
using Lucene.Net.Index;
using Lucene.Net.Search;
using Lucene.Net.Store;
using Microsoft.IO;
using Newtonsoft.Json.Linq;
using Stormancer.Core;
using Stormancer.Core.Helpers;
using Stormancer.Diagnostics;
using Stormancer.Plugins;
using Stormancer.Server.Components;
using Stormancer.Server.Plugins.Configuration;
using Stormancer.Server.Plugins.Database;
using Stormancer.Server.Plugins.Queries;
using Stormancer.Server.Plugins.Utilities;
using Stormancer.Server.Plugins.Utilities.Extensions;
using System;
using System.Buffers;
using System.Collections.Concurrent;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.IO.Pipelines;
using System.Linq;
using System.Reactive.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace Stormancer.Server.Plugins.Users
{
    /// <summary>
    /// Possible disconnection reasons.
    /// </summary>
    public enum DisconnectionReason
    {
        /// <summary>
        /// The client disconnected.
        /// </summary>
        ClientDisconnected,

        /// <summary>
        /// The session with the client was lost.
        /// </summary>
        ConnectionLoss,

        /// <summary>
        /// The session was replaced with a new connection for the same user.
        /// </summary>
        NewConnection,

        /// <summary>
        /// The session was closed by the server.
        /// </summary>
        ServerRequest
    }




    internal class SessionsRepository
    {


        private IndexState _index;

        private Dictionary<SessionId, Document<SessionRecord>> _sessions = new Dictionary<SessionId, Document<SessionRecord>>();

        private object _syncRoot = new object();
        public SessionsRepository()
        {
            _index = new IndexState(new RAMDirectory(), DefaultMapper.JsonMapper);
        }

        private Filters _filtersEngine = new Filters(new IFilterExpressionFactory[] { new CommonFiltersExpressionFactory() });

        public int Count
        {
            get
            {
                lock (_syncRoot)
                {
                    return _sessions.Count;
                }
            }
        }
        public IEnumerable<Document<SessionRecord>> All
        {
            get
            {
                lock (_syncRoot)
                {
                    return _sessions.Values.ToArray();
                }
            }
        }
        public bool AddOrUpdateSessionRecord(SessionRecord session, int? version)
        {
            var sessionId = session.SessionId;
            var id = sessionId.ToString();
            var success = false;
            lock (_syncRoot)
            {

                if (_sessions.TryGetValue(sessionId, out var document))
                {
                    if (version == document.Version || version == null)
                    {
                        _sessions[sessionId] = new Document<SessionRecord>(id, session) { Version = (uint)(document.Version + 1) };
                        success = true;
                    }
                    else
                    {
                        success = false;
                    }
                }
                else
                {
                    if (version < 0 || version == null)
                    {
                        _sessions[sessionId] = new Document<SessionRecord>(id, session) { Version = 1 };
                        success = true;
                    }
                    else
                    {
                        success = false;
                    }
                }


            }

            if (success)
            {
                _index.Writer.UpdateDocument(new Lucene.Net.Index.Term("_id", id), MapSessionRecord(session));
                _index.Writer.Flush(false, false);
            }

            return success;
        }

        private IEnumerable<IIndexableField> MapSessionRecord(SessionRecord record)
        {
            yield return new StringField("_id", record.SessionId.ToString(), Field.Store.YES);

            var user = record.User;
            if (user is not null)
            {
                yield return new StringField("user.id", user.Id, Field.Store.NO);
                foreach (var field in DefaultMapper.JsonMapper("user.auth", user.Auth))
                {
                    yield return field;
                }
                foreach (var field in DefaultMapper.JsonMapper("user.data", user.UserData))
                {
                    yield return field;
                }
            }

        }
        public bool TryRemoveSession(SessionId sessionId, [NotNullWhen(true)] out SessionRecord? session)
        {
            bool success = false;
            lock (_syncRoot)
            {
                success = _sessions.Remove(sessionId, out var doc);

                session = doc?.Source;

            }

            if (success)
            {
                _index.Writer.DeleteDocuments(new Lucene.Net.Index.Term("_id", sessionId.ToString()));
                _index.Writer.Flush(false, false);
            }
            return success;
        }
        public Document<SessionRecord>? GetSession(SessionId sessionId)
        {
            lock (_syncRoot)
            {

                return _sessions.TryGetValue(sessionId, out var document) ? document : null;

            }
        }
        public Dictionary<SessionId, Document<SessionRecord>?> GetSessions(IEnumerable<SessionId> sessionIds)
        {
            lock (_syncRoot)
            {
                var dictionary = new Dictionary<SessionId, Document<SessionRecord>?>();

                foreach (var sessionId in sessionIds)
                {
                    dictionary[sessionId] = _sessions.TryGetValue(sessionId, out var document) ? document : null;
                }
                return dictionary;

            }
        }

        public SearchResult<SessionRecord> Filter(JObject query, uint size = 20, uint skip = 0)
        {
            var result = new SearchResult<SessionRecord>();


            using var reader = _index.Writer.GetReader(true);
            var searcher = new IndexSearcher(reader);

            var filter = _filtersEngine.Parse(query);

            var docs = searcher.Search(new ConstantScoreQuery(filter.ToLuceneQuery()), (int)(size + skip));
            result.Hits = docs.ScoreDocs.Skip((int)skip).Select(hit => searcher.Doc(hit.Doc)?.Get("_id"))
                .Where(id => id is not null)
                .Select(id =>
                {
                    Debug.Assert(id != null);
                    var sessionId = SessionId.From(id);
                    var document = _sessions.TryGetValue(sessionId, out var a) ? a : default;
                    return document;
                })
            .WhereNotNull().ToList();
            result.Total = (uint)docs.TotalHits;
            return result;


        }
    }
    /// <summary>
    /// Stored object representing a session. 
    /// </summary>
    public class SessionRecord
    {
        /// <summary>
        /// Gets the platformId associated with the session.
        /// </summary>
        public PlatformId platformId { get; set; }

        /// <summary>
        /// Gets the user associated with the session.
        /// </summary>
        /// <remarks>
        /// Can be null if the session is anonymous.
        /// </remarks>
        public User? User { get; set; }

        /// <summary>
        /// Gets the id of the session.
        /// </summary>
        public SessionId SessionId { get; set; }

        /// <summary>
        /// Gets the identities associated with the session.
        /// </summary>
        public ConcurrentDictionary<string, string> Identities { get; set; } = new ConcurrentDictionary<string, string>();

        /// <summary>
        /// Gets the authentication expiration dates per identity.
        /// </summary>
        public Dictionary<string, DateTime> AuthenticationExpirationDates { get; } = new Dictionary<string, DateTime>();

        /// <summary>
        /// Gets the session data.
        /// </summary>
        public ConcurrentDictionary<string, byte[]> SessionData { get; internal set; } = new ConcurrentDictionary<string, byte[]>();

        /// <summary>
        /// Gets or sets the date the session was created.
        /// </summary>
        public DateTime ConnectedOn { get; internal set; }

        /// <summary>
        /// absolute scene Url to the authenticator scene
        /// </summary>
        public string AuthenticatorUrl { get; set; } = default!;

        /// <summary>
        /// If the session is cached, the date at which it should expire
        /// </summary>
        public DateTimeOffset? MaxAge { get => AuthenticationExpirationDates.Count > 0 ? AuthenticationExpirationDates.Min().Value : (DateTimeOffset?)null; }


        private Dictionary<string, string> _dimensions = new Dictionary<string, string>();
        private FrozenDictionary<string, string>? _cachedDimensions;

        /// <summary>
        /// Sets the value associated in a dimension of the session.
        /// </summary>
        /// <remarks>
        /// Dimensions provides drill down and filtering capabilities for real time count of connected sessions. 
        /// </remarks>
        /// <param name="key"></param>
        /// <param name="value"></param>
        public void SetDimension(string key, string value)
        {
            _dimensions[key] = value;
            _cachedDimensions = null;
        }

        /// <summary>
        /// Gets the current dimensions associated with this player session.
        /// </summary>
        public FrozenDictionary<string, string> Dimensions
        {
            get
            {
                if (_cachedDimensions == null)
                {
                    _cachedDimensions = _dimensions.ToFrozenDictionary();
                }
                return _cachedDimensions;
            }
        }
        /// <summary>
        /// Version of the record
        /// </summary>
        public int Version { get; set; }

        /// <summary>
        /// Creates a view of the session.
        /// </summary>
        /// <returns></returns>
        public Session CreateView()
        {
            return new Session
            {
                platformId = platformId,
                User = User,
                SessionId = SessionId,
                SessionData = new Dictionary<string, byte[]>(SessionData),
                Identities = new Dictionary<string, string>(Identities),
                ConnectedOn = ConnectedOn,
                AuthenticatorUrl = AuthenticatorUrl,
                MaxAge = MaxAge
            };
        }
    }

    /// <summary>
    /// Extension methods for sessions.
    /// </summary>
    public static class SessionDocExtensions
    {
        /// <summary>
        /// Creates a view from a session record.
        /// </summary>
        /// <param name="record"></param>
        /// <returns></returns>
        public static Session? CreateView(this Document<SessionRecord> record)
        {
            var s = record?.Source?.CreateView();
            if (s != null && record != null)
            {
                s.Version = record.Version;
            }
            return s;
        }
    }



    internal class UserSessions : IUserSessions
    {

        private readonly SessionsRepository repository;
        private readonly RecyclableMemoryStreamProvider _memoryStreamProvider;
        private readonly Func<IEnumerable<IUserSessionEventHandler>> _eventHandlers;
        private readonly IESClientFactory _esClientFactory;
        private readonly IClusterSerializer clusterSerializer;
        private readonly ISerializer _clientSerializer;
        private readonly IEnvironment env;
        private readonly ISceneHost _scene;
        private readonly IConfiguration _configuration;
        private readonly ILogger logger;
        private readonly IUserService _userService;



        public UserSessions(IUserService userService,
            SessionsRepository repository,
            RecyclableMemoryStreamProvider memoryStreamProvider,
            Func<IEnumerable<IUserSessionEventHandler>> eventHandlers,
            IClusterSerializer serializer,
            ISerializer clientSerializer,
            IESClientFactory eSClientFactory,
            IEnvironment env,
            ISceneHost scene,
            IConfiguration configuration,
            ILogger logger)
        {
            _esClientFactory = eSClientFactory;
            _userService = userService;
            this.repository = repository;
            _memoryStreamProvider = memoryStreamProvider;
            _eventHandlers = eventHandlers;
            _scene = scene;
            _configuration = configuration;
            this.clusterSerializer = serializer;
            _clientSerializer = clientSerializer;
            this.env = env;
            this.logger = logger;

        }

        public async Task<User?> GetUser(IScenePeerClient peer, CancellationToken cancellationToken)
        {
            var session = await GetSession(peer, cancellationToken);

            return session?.User;
        }

        public async Task<bool> IsAuthenticated(IScenePeerClient peer, CancellationToken cancellationToken)
        {
            return (await GetUser(peer, cancellationToken)) != null;
        }

        public async Task<bool> DeletePlayerSession(SessionId id, DisconnectionReason reason)
        {


            if (repository.TryRemoveSession(id, out var record))
            {
                var session = record.CreateView();

                var logoutContext = new LogoutContext { Session = session, ConnectedOn = session.ConnectedOn, Reason = reason };
                await _eventHandlers().RunEventHandler(h => h.OnLoggedOut(logoutContext), ex => logger.Log(LogLevel.Error, "userSessions", "An error occurred while running LoggedOut event handlers", ex));

                return true;
            }
            else
            {
                return false;
            }
        }

        public async Task CreateSessionForPeer(IScenePeerClient peer, User? user, PlatformId onlineId, Dictionary<string, byte[]> sessionData, Action<SessionRecord> sessionModifier, CancellationToken cancellationToken)
        {
            if (peer == null)
            {
                throw new ArgumentNullException("peer");
            }
            var currentSessions = user != null ? repository.Filter(JObject.FromObject(new
            {
                match = new
                {
                    field = "user.id",
                    value = user.Id
                }
            }), 10, 0).Hits.Select(d => d.Source).WhereNotNull() : Enumerable.Empty<SessionRecord>();

            var session = currentSessions.FirstOrDefault(s => s.SessionId == peer.SessionId);

            if (session == null)
            {
                session = new SessionRecord
                {
                    User = user,
                    platformId = onlineId,
                    SessionData = new ConcurrentDictionary<string, byte[]>(sessionData.AsEnumerable()),
                    SessionId = peer.SessionId,
                    ConnectedOn = DateTime.UtcNow,
                    AuthenticatorUrl = await GetAuthenticatorUrl(),
                    Version = 0
                };
            }
            sessionModifier?.Invoke(session);


            if (repository.AddOrUpdateSessionRecord(session, null))
            {

                var loginContext = new LoginContext
                {
                    Session = session,
                    Client = peer,
                    ConnectedSessions = currentSessions.Select(s => s.CreateView()),
                    AuthenticatedUser = user
                };

                await _eventHandlers().RunEventHandler(h => h.OnLoggedIn(loginContext), ex => logger.Log(LogLevel.Error, "userSessions", "An error occurred while running LoggedIn event handlers", ex));

                foreach (var oldPeer in currentSessions.Select(s => _scene.TryGetPeer(s.SessionId, out var peer) ? peer : null).WhereNotNull())
                {
                    if (peer != oldPeer)
                    {
                        await peer.DisconnectFromServer("ALREADY_CONNECTED");
                    }
                }
            }
            else
            {
                throw new InvalidOperationException("Session already logged in.");
            }
        }

        private async Task<string> GetAuthenticatorUrl()
        {
            var infos = await env.GetApplicationInfos();
            // Support older grid versions where these values are null
            if (string.IsNullOrEmpty(infos.HostUrl) || string.IsNullOrEmpty(infos.ClusterId))
            {
                return _scene.Id;
            }
            return $"scene:/{infos.ClusterId}/{infos.HostUrl}/{_scene.Id}#{_scene.ShardId}";
        }

        internal async Task<bool> UpdateSession(SessionId id, Func<SessionRecord, Task<SessionRecord>> mutator)
        {
            var session = repository.GetSession(id);
            if (session is not null && session.Source is not null)
            {
                var updatedSession = await mutator(session.Source);
                return repository.AddOrUpdateSessionRecord(updatedSession, (int)session.Version);
            }
            else
            {
                return false;
            }
        }

        public async Task UpdateUserData<T>(IScenePeerClient peer, T data, CancellationToken cancellationToken)
        {
            var user = await GetUser(peer, cancellationToken);
            if (user == null)
            {
                throw new InvalidOperationException("User not found.");
            }
            else
            {
                user.UserData = Newtonsoft.Json.Linq.JObject.FromObject(data!);
                await _userService.UpdateUserData(user.Id, data);
            }
        }

        public Task<IEnumerable<SessionId>> GetPeers(string userId, CancellationToken cancellationToken)
        {
            var result = repository.Filter(JObject.FromObject(new
            {
                match = new
                {
                    field = "user.id",
                    value = userId
                }
            }), 20, 0);


            return Task.FromResult(result.Hits.Select(h => h.Source!.SessionId));

        }
        public Task<IEnumerable<Session>> GetSession(string userId, CancellationToken cancellationToken)
        {
            return Task.FromResult(GetSessionImpl(userId));
        }

        private IEnumerable<Session> GetSessionImpl(string userId)
        {
            var result = repository.Filter(JObject.FromObject(new
            {
                match = new
                {
                    field = "user.id",
                    value = userId
                }
            }), 10, 0);



            return result.Hits.Select(d => d.Source?.CreateView()).WhereNotNull();


        }




        public async Task<Session?> GetSession(IScenePeerClient peer, CancellationToken cancellationToken)
        {
            return peer != null ? await GetSessionById(peer.SessionId, cancellationToken) : null;
        }

        public Task<SessionRecord?> GetSessionRecordById(SessionId sessionId)
        {
            return Task.FromResult(GetSessionRecordByIdImpl(sessionId));
        }

        private SessionRecord? GetSessionRecordByIdImpl(SessionId sessionId)
        {
            return repository.GetSession(sessionId)?.Source;
        }

        public Task<Session?> GetSessionById(SessionId sessionId, CancellationToken cancellationToken)
        {
            return Task.FromResult(GetSessionRecordByIdImpl(sessionId)?.CreateView());

        }

        private Session? GetSessionByIdImpl(SessionId sessionId)
        {
            return GetSessionRecordByIdImpl(sessionId)?.CreateView();
        }

        public Task<IEnumerable<Session>> GetSessionsByUserId(string userId, CancellationToken cancellationToken)
        {
            return GetSession(userId, cancellationToken);
        }

        public Task<Session?> GetSessionById(SessionId sessionId, string authType, CancellationToken cancellationToken)
        {
            return GetSessionById(sessionId, cancellationToken);
        }

        public async Task UpdateSessionData(SessionId sessionId, string key, byte[] data, CancellationToken cancellationToken)
        {
            var session = await GetSessionRecordById(sessionId);
            if (session == null)
            {
                throw new ClientException("session.notFound");
            }
            session.SessionData[key] = data;
        }

        public Task UpdateSessionData<T>(SessionId sessionId, string key, T data, CancellationToken cancellationToken)
        {

            using var stream = _memoryStreamProvider.GetStream();
            clusterSerializer.Serialize(stream, data);
            return UpdateSessionData(sessionId, key, stream.ToArray(), cancellationToken);
        }

        public async Task<byte[]?> GetSessionData(SessionId sessionId, string key, CancellationToken cancellationToken)
        {
            var session = await GetSessionById(sessionId, cancellationToken);
            if (session == null)
            {
                throw new ClientException("NotFound");
            }
            if (session.SessionData.TryGetValue(key, out var value))
            {
                return value;
            }
            else
            {
                return null;
            }
        }

        public async Task<T?> GetSessionData<T>(SessionId sessionId, string key, CancellationToken cancellationToken)
        {
            var data = await GetSessionData(sessionId, key, cancellationToken);
            if (data != null && clusterSerializer.TryDeserialize<T>(new ReadOnlySequence<byte>(data), out var value, out _))
            {

                return value;

            }
            else
            {
                return default(T);
            }
        }



        public Task<Dictionary<string, User?>> GetUsers(IEnumerable<string> userIds, CancellationToken cancellationToken)
        {
            return _userService.GetUsers(userIds, cancellationToken);
        }

        public Task<IEnumerable<User>> Query(IEnumerable<KeyValuePair<string, string>> query, int take, int skip, CancellationToken cancellationToken)
        {
            return _userService.Query(query, take, skip, cancellationToken);
        }

        private static int _randomTracker = 0;

        private static ThreadLocal<Random> _random = new ThreadLocal<Random>(() =>
        {
            var seed = (int)(Environment.TickCount & 0xFFFFFF00 | (byte)(Interlocked.Increment(ref _randomTracker) % 255));
            var random = new Random(seed);
            return random;
        });

        private static bool _handleUserMappingCreated = false;
        private static AsyncLock _mappingLock = new AsyncLock();


        private int _handleSuffixUpperBound = 10000;
        private int _handleMaxNumCharacters = 32;

        private async Task EnsureHandleUserMappingCreated()
        {
            if (!_handleUserMappingCreated)
            {
                using (await _mappingLock.LockAsync())
                {
                    if (!_handleUserMappingCreated)
                    {
                        _handleUserMappingCreated = true;
                        await _esClientFactory.EnsureMappingCreated<HandleUserRelation>("handleUserMapping", m => m
                            .Properties(pd => pd
                                .Keyword(kpd => kpd.Name(record => record.Id).Index())
                                .Keyword(kpd => kpd.Name(record => record.HandleWithoutNum).Index())
                                .Number(npd => npd.Name(record => record.HandleNum).Type(Nest.NumberType.Integer).Index())
                                .Keyword(kpd => kpd.Name(record => record.UserId).Index(false))
                                ));
                    }
                }
            }
        }



        private class PeerRequest : IRemotePipe
        {

            public Pipe InputPipe = new Pipe();
            public Pipe OutputPipe = new Pipe();

            public PipeReader Reader => OutputPipe.Reader;

            public PipeWriter Writer => InputPipe.Writer;

            public ValueTask DisposeAsync() => ValueTask.CompletedTask;

            public void Send() { }

        }
        public IRemotePipe SendRequest(string operationName, string senderUserId, string recipientUserId, CancellationToken cancellationToken)
        {
            var rq = new PeerRequest();

            async Task SendRequestImpl()
            {
                try
                {

                    var sessionId = await GetPeers(recipientUserId, cancellationToken);
                    var peer = _scene.RemotePeers.FirstOrDefault(p => p.SessionId == sessionId.FirstOrDefault());
                    if (peer == null)
                    {
                        throw new ClientException("NotConnected");

                    }
                    using var stream = _memoryStreamProvider.GetStream();
                    await rq.InputPipe.Reader.TryCopyToAsync(PipeWriter.Create(stream), false, cancellationToken);
                    rq.InputPipe.Reader.Complete();
                    stream.Seek(0, SeekOrigin.Begin);
                    var rpc = peer.Rpc("sendRequest", s =>
                    {
                        try
                        {
                            peer.Serializer().Serialize(senderUserId, s);
                            peer.Serializer().Serialize(operationName, s);
                            stream.CopyTo(s);

                        }
                        finally
                        {
                            stream.Dispose();

                        }

                    }).ToAsyncEnumerable().WithCancellation(cancellationToken);

                    bool headerSent = false;
                    try
                    {
                        await foreach (var packet in rpc)
                        {
                            using (packet)
                            {
                                if (!headerSent)
                                {
                                    headerSent = true;
                                    rq.OutputPipe.Writer.WriteObject(true, clusterSerializer);
                                }

                                await packet.Stream.CopyToAsync(rq.OutputPipe.Writer);
                            }
                        }

                    }
                    catch (Exception ex)
                    {
                        if (!headerSent)
                        {
                            rq.OutputPipe.Writer.WriteObject(false, clusterSerializer);
                            rq.OutputPipe.Writer.WriteObject(ex.Message, clusterSerializer);
                        }
                        else
                        {
                            logger.Log(LogLevel.Error, "userSessions.SendRequest", "An error occurred while sending a request to a client.", ex);
                        }
                    }

                    if (!headerSent)
                    {
                        headerSent = true;
                        rq.OutputPipe.Writer.WriteObject(true, clusterSerializer);
                    }

                    rq.OutputPipe.Writer.Complete();
                }
                catch (Exception ex)
                {

                    rq.OutputPipe.Writer.WriteObject(false, clusterSerializer);
                    rq.OutputPipe.Writer.WriteObject(ex.Message, clusterSerializer);

                    rq.InputPipe.Reader.Complete(ex);
                    rq.OutputPipe.Writer.Complete(ex);
                }

            }
            _ = Task.Run(SendRequestImpl);


            return rq;
        }

        public Task<SendRequestResult<TReturn>> SendRequest<TReturn, TArg>(string operationName, string senderUserId, string recipientUserId, TArg arg, CancellationToken cancellationToken)
             => SendRequestImpl<TReturn, TArg>(this, _clientSerializer, clusterSerializer, operationName, senderUserId, recipientUserId, arg, cancellationToken);


        public Task<SendRequestResult<TReturn>> SendRequest<TReturn, TArg1, TArg2>(string operationName, string senderUserId, string recipientUserId, TArg1 arg1, TArg2 arg2, CancellationToken cancellationToken)
            => SendRequestImpl<TReturn, TArg1, TArg2>(this, _clientSerializer, clusterSerializer, operationName, senderUserId, recipientUserId, arg1, arg2, cancellationToken);

        public Task<SendRequestResult> SendRequest<TArg>(string operationName, string senderUserId, string recipientUserId, TArg arg, CancellationToken cancellationToken)
            => SendRequestImpl<TArg>(this, _clientSerializer, clusterSerializer, operationName, senderUserId, recipientUserId, arg, cancellationToken);


        internal static async Task<SendRequestResult<TReturn>> SendRequestImpl<TReturn, TArg>(IUserSessions sessions, ISerializer clientSerializer, IClusterSerializer clusterSerializer, string operationName, string senderUserId, string recipientUserId, TArg arg, CancellationToken cancellationToken)
        {
            await using var rq = sessions.SendRequest(operationName, senderUserId, recipientUserId, cancellationToken);
            clientSerializer.Serialize(arg, rq.Writer);

            rq.Writer.Complete();
            rq.Send();

            try
            {
                if (await rq.Reader.ReadObject<bool>(clusterSerializer, cancellationToken))
                {
                    return new SendRequestResult<TReturn> { Success = true, Value = await clientSerializer.DeserializeAsync<TReturn>(rq.Reader, cancellationToken) };
                }
                else
                {
                    return new SendRequestResult<TReturn> { Success = false, Error = await rq.Reader.ReadObject<string>(clusterSerializer, cancellationToken) };
                }
            }
            finally
            {
                rq.Reader.Complete();
            }
        }

        internal static async Task<SendRequestResult<TReturn>> SendRequestImpl<TReturn, TArg1, TArg2>(IUserSessions sessions, ISerializer clientSerializer, IClusterSerializer clusterSerializer, string operationName, string senderUserId, string recipientUserId, TArg1 arg1, TArg2 arg2, CancellationToken cancellationToken)
        {
            await using var rq = sessions.SendRequest(operationName, senderUserId, recipientUserId, cancellationToken);
            clientSerializer.Serialize(arg1, rq.Writer);
            clientSerializer.Serialize(arg2, rq.Writer);
            rq.Writer.Complete();
            rq.Send();

            try
            {
                if (await rq.Reader.ReadObject<bool>(clusterSerializer, cancellationToken))
                {

                    return new SendRequestResult<TReturn> { Value = await clientSerializer.DeserializeAsync<TReturn>(rq.Reader, cancellationToken), Success = true };
                }
                else
                {
                    return new SendRequestResult<TReturn> { Error = await rq.Reader.ReadObject<string>(clusterSerializer, cancellationToken), Success = false };
                }
            }
            finally
            {
                rq.Reader.Complete();
            }
        }

        internal static async Task<SendRequestResult> SendRequestImpl<TArg>(IUserSessions sessions, ISerializer clientSerializer, IClusterSerializer clusterSerializer, string operationName, string senderUserId, string recipientUserId, TArg arg, CancellationToken cancellationToken)
        {
            await using var rq = sessions.SendRequest(operationName, senderUserId, recipientUserId, cancellationToken);
            clientSerializer.Serialize(arg, rq.Writer);
            rq.Writer.Complete();
            rq.Send();

            try
            {

                if (await rq.Reader.ReadObject<bool>(clusterSerializer, cancellationToken))
                {
                    return new SendRequestResult { Success = true };
                }
                else
                {

                    return new SendRequestResult { Success = false, Error = await rq.Reader.ReadObject<string>(clusterSerializer, cancellationToken) };
                }
            }
            finally
            {
                rq.Reader.Complete();
            }

        }


        public Task<int> GetAuthenticatedUsersCount(CancellationToken cancellationToken)
        {
            return Task.FromResult(AuthenticatedUsersCount);
        }

        public async Task<Dictionary<SessionId, Session?>> GetSessions(IEnumerable<SessionId> sessionIds, CancellationToken cancellationToken)
        {

            var sessions = new Dictionary<SessionId, Session?>();

            foreach (var id in sessionIds)
            {
                if (!sessions.ContainsKey(id))
                {
                    var session = await GetSessionById(id, cancellationToken);
                    if (session != null)
                    {
                        sessions.TryAdd(id, session);
                    }
                    else
                    {
                        sessions.TryAdd(id, null);
                    }
                }
            }

            return sessions;

        }

        public async Task KickUser(IEnumerable<string> userIds, string reason, CancellationToken cancellationToken)
        {
            if (userIds.Contains("*"))
            {
                await Task.WhenAll(_scene.RemotePeers.Select(async p =>
                {
                    var ctx = new KickContext(p, GetSessionByIdImpl(p.SessionId), userIds);
                    await _eventHandlers().RunEventHandler(h => h.OnKicking(ctx), ex => logger.Log(LogLevel.Error, "userSessions", "An error occurred while running onKick event.", new { }));

                    if (ctx.Kick)
                    {
                        await p.DisconnectFromServer(reason);
                    }
                }));
            }
            else if (userIds.Contains("*/authenticated"))
            {
                await Task.WhenAll(_scene.RemotePeers.Select(async p =>
                {
                    if (GetSession(p, cancellationToken) != null)
                    {
                        var ctx = new KickContext(p, GetSessionByIdImpl(p.SessionId), userIds);
                        await _eventHandlers().RunEventHandler(h => h.OnKicking(ctx), ex => logger.Log(LogLevel.Error, "userSessions", "An error occurred while running onKick event.", new { }));

                        if (ctx.Kick)
                        {
                            await p.DisconnectFromServer(reason);
                        }
                    }
                }));
            }
            else if (userIds.Contains("*/!authenticated"))
            {
                await Task.WhenAll(_scene.RemotePeers.Select(async p =>
                {
                    if (GetSession(p, cancellationToken) != null)
                    {
                        var ctx = new KickContext(p, GetSessionByIdImpl(p.SessionId), userIds);
                        await _eventHandlers().RunEventHandler(h => h.OnKicking(ctx), ex => logger.Log(LogLevel.Error, "userSessions", "An error occurred while running onKick event.", new { }));

                        if (ctx.Kick)
                        {
                            await p.DisconnectFromServer(reason);
                        }
                    }
                }));
            }
            else
            {
                foreach (var userId in userIds)
                {
                    var sessionIds = await GetPeers(userId, cancellationToken);
                    foreach (var sessionId in sessionIds)
                    {
                        var p = _scene.RemotePeers.FirstOrDefault(p => p.SessionId == sessionId);
                        if (p != null)
                        {
                            var session = GetSessionByIdImpl(p.SessionId);
                            if (session != null)
                            {
                                var ctx = new KickContext(p, session, userIds);
                                await _eventHandlers().RunEventHandler(h => h.OnKicking(ctx), ex => logger.Log(LogLevel.Error, "userSessions", "An error occurred while running onKick event.", new { }));

                                if (ctx.Kick)
                                {
                                    await p.DisconnectFromServer(reason);
                                }
                            }
                        }
                    }
                }

            }
        }

        public IAsyncEnumerable<Session> GetSessionsAsync(CancellationToken cancellationToken)
        {
            return repository.All.Select(r => r.CreateView()).WhereNotNull().ToAsyncEnumerable();
        }


        public int AuthenticatedUsersCount
        {
            get
            {
                return repository.Count;
            }
        }

        public ValueTask<IEnumerable<AuthenticatedUsersCount>> GetAuthenticatedUsersByDimensionsAsync()
        {
            return ValueTask.FromResult(repository
            .All
            .Select(d => d.Source)
            .WhereNotNull()
            .GroupBy(s => s.Dimensions, _dimensionsComparer)
            .Select(g => new AuthenticatedUsersCount
            {
                Count = g.Count(),
                Dimensions = g.Key
            }));
        }

        private static DimensionsComparer _dimensionsComparer = new DimensionsComparer();
        private class DimensionsComparer : IEqualityComparer<FrozenDictionary<string, string>>
        {
            public bool Equals(FrozenDictionary<string, string>? x, FrozenDictionary<string, string>? y)
            {
                if (x == null)
                {
                    if (y == null)
                    {
                        return true;
                    }
                    else
                    {
                        return false;
                    }
                }

                if (y == null)
                {
                    if (x == null)
                    {
                        return true;
                    }
                    else
                    {
                        return false;
                    }
                }

                if (x.Count != y.Count)
                {
                    return false;
                }

                foreach (var (key, value) in x)
                {
                    if (!y.TryGetValue(key, out var v) || v != value)
                    {
                        return false;
                    }
                }

                return true;

            }

            public int GetHashCode([DisallowNull] FrozenDictionary<string, string> obj)
            {
                var hashCode = new HashCode();
                foreach (var (key, value) in obj)
                {
                    hashCode.Add(key);
                    hashCode.Add(value);
                }
                return hashCode.ToHashCode();
            }
        }
    }

    /// <summary>
    /// Count of sessions matching a set of dimension values.
    /// </summary>
    public class AuthenticatedUsersCount
    {
        /// <summary>
        /// 
        /// </summary>
        public required FrozenDictionary<string, string> Dimensions { get; set; }

        /// <summary>
        /// Count of players matching the vector in the dimensions space.
        /// </summary>
        public required int Count { get; set; }
    }

    /// <summary>
    /// Record object representing an association between handle &amp; user.
    /// </summary>
    public class HandleUserRelation
    {
        /// <summary>
        /// Indexed by user's handle
        /// </summary>       
        public string Id { get; set; } = default!;

        /// <summary>
        /// Gets or sets the user handle without numbered suffix.
        /// </summary>
        public string HandleWithoutNum { get; set; } = default!;

        /// <summary>
        /// Gets or sets the numbered suffix.
        /// </summary>
        public int HandleNum { get; set; }

        /// <summary>
        /// Gets or sets the user Id.
        /// </summary>
        public string UserId { get; set; } = default!;
    }
}
