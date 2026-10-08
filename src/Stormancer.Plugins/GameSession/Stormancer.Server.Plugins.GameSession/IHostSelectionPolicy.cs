using Stormancer.Server.Plugins.Users;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Stormancer.Server.Plugins.GameSession
{
    /// <summary>
    /// Defines a policy used to select hosts.
    /// </summary>
    public interface IHostSelectionPolicy
    {
        /// <summary>
        /// Tries to configure the policy for a given game session. Returns false if the policy should be disabled.
        /// </summary>
        /// <param name="configuration"></param>
        /// <param name="gameSession"></param>
        /// <returns></returns>
        public bool TryConfigure(HostSelectionPolicyConfiguration? configuration, IGameSessionService gameSession);

        /// <summary>
        /// returns a boolean indicating whether a peer represented by their session can host the gamesession.
        /// </summary>
        /// <param name="session"></param>
        /// <returns></returns>
        public bool IsHostCandidate(Session session);

        /// <summary>
        /// Selects a new host among a list of candidates.
        /// </summary>
        /// <param name="candidates"></param>
        /// <returns></returns>
        public void OnSelectingHost(List<Session> candidates);
    }

    /// <summary>
    /// Host selection policy that authorizes a server to become the host.
    /// </summary>
    public class ServerHostSelectionPolicy : IHostSelectionPolicy
    {
        /// <summary>
        /// Type of the host selection policy
        /// </summary>
        public const string Type = "server";

        /// <summary>
        /// Creates a policy configuration that activates the <see cref="ServerHostSelectionPolicy"/> host selection policy.
        /// </summary>
        /// <returns></returns>
        public static HostSelectionPolicyConfiguration CreateConfiguration()
        {
            return new HostSelectionPolicyConfiguration { Type = Type };
        }

        ///<inheritdoc/>
        public bool IsHostCandidate(Session session)
        {
            return session.IsDedicatedServer();
        }

        ///<inheritdoc/>
        public void OnSelectingHost(List<Session> candidates)
        {

        }

        ///<inheritdoc/>
        public bool TryConfigure(HostSelectionPolicyConfiguration? configuration, IGameSessionService gameSession)
        {
            return configuration?.Type == Type;
        }
    }

    /// <summary>
    /// Host selection policy that authorizes any peer to become the host.
    /// </summary>
    public class AnyHostSelectionPolicy : IHostSelectionPolicy
    {
        /// <summary>
        /// Type of the host selection policy
        /// </summary>
        public const string Type = "any";

        /// <summary>
        /// Creates a policy configuration that activates the <see cref="AnyHostSelectionPolicy"/> host selection policy.
        /// </summary>
        /// <returns></returns>
        public static HostSelectionPolicyConfiguration CreateConfiguration()
        {
            return new HostSelectionPolicyConfiguration { Type = Type };
        }


        ///<inheritdoc/>
        public bool IsHostCandidate(Session session)
        {
            return true;
        }

        ///<inheritdoc/>
        public void OnSelectingHost(List<Session> candidates)
        {

        }

        ///<inheritdoc/>
        public bool TryConfigure(HostSelectionPolicyConfiguration? configuration, IGameSessionService gameSession)
        {
            return configuration?.Type == Type;
        }
    }

    /// <summary>
    /// Host selection policy that authorizes any peer identified in the host candidates list to become the host.
    /// </summary>
    public class CandidatesHostSelectionPolicy : IHostSelectionPolicy
    {
        /// <summary>
        /// Type of the host selection policy
        /// </summary>
        public const string Type = "candidates";
        private IEnumerable<SessionId>? _candidates;

        /// <summary>
        /// Creates a policy configuration that activates the <see cref="CandidatesHostSelectionPolicy"/> host selection policy.
        /// </summary>
        /// <returns></returns>
        public static HostSelectionPolicyConfiguration CreateConfiguration()
        {
            return new HostSelectionPolicyConfiguration { Type  = Type };
        }

        ///<inheritdoc/>
        public bool IsHostCandidate(Session session)
        {
            return _candidates?.Contains(session.SessionId) ?? false;
        }

        ///<inheritdoc/>
        public void OnSelectingHost(List<Session> candidates)
        {

        }

        ///<inheritdoc/>
        public bool TryConfigure(HostSelectionPolicyConfiguration? configuration, IGameSessionService gameSession)
        {
            if (configuration?.Type == Type)
            {
                _candidates = gameSession.GetHostCandidates();
                return true;
            }
            else
            {
                return false;
            }
        }
    }

    /// <summary>
    /// An host selection policy that only authorizes a specific host.
    /// </summary>
    public class FixedHostSelectionPolicy : IHostSelectionPolicy
    {
        /// <summary>
        /// Type of the host selection policy.
        /// </summary>
        public const string Type = "fix";
        SessionId _selectedId;
        string? _selectedUserId;

        /// <summary>
        /// Create a configuration for <see cref="FixedHostSelectionPolicy"/> using a session id.
        /// </summary>
        /// <param name="sessionId"></param>
        /// <returns></returns>
        public static HostSelectionPolicyConfiguration CreateConfiguration(SessionId sessionId)
        {
            var config = new HostSelectionPolicyConfiguration { Type = Type };
            config.Arguments.Add("sessionId", sessionId.ToString());
            return config;
        }

        /// <summary>
        /// Create a configuration for <see cref="FixedHostSelectionPolicy"/> using an user id.
        /// </summary>
        /// <param name="userId"></param>
        /// <returns></returns>
        public static HostSelectionPolicyConfiguration CreateConfiguration(string userId)
        {
            var config = new HostSelectionPolicyConfiguration { Type = Type };
            config.Arguments.Add("userId", userId);
            return config;
        }

        ///<inheritdoc/>
        public bool IsHostCandidate(Session session)
        {
            if (!_selectedId.IsEmpty())
            {
                return session.SessionId == _selectedId;
            }
            else if (_selectedUserId != null)
            {
                return session.User?.Id == _selectedUserId;
            }
            else
                return false;
        }

        ///<inheritdoc/>
        public void OnSelectingHost(List<Session> candidates)
        {

        }

        ///<inheritdoc/>
        public bool TryConfigure(HostSelectionPolicyConfiguration? configuration, IGameSessionService gameSession)
        {
            if (configuration?.Type == Type)
            {
                if (configuration.Arguments.TryGetValue("sessionId", out var v))
                {
                    _selectedId = SessionId.From(v);
                    return true;
                }
                else if (configuration.Arguments.TryGetValue("userId", out v))
                {
                    _selectedUserId = v;
                    return true;
                }
            }

            return false;

        }
    }
}
