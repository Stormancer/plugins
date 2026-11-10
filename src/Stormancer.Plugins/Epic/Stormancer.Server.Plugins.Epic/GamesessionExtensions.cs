using Microsoft.CodeAnalysis.CSharp.Syntax;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Stormancer.Server.Plugins.Epic
{
    /// <summary>
    /// Easy anticheat validation mode in a game session.
    /// </summary>
    public enum EasyAntiCheatMode
    {
        /// <summary>
        /// Disabled
        /// </summary>
        Disabled,

        /// <summary>
        /// The gamesession uses the Client server EAC mode.
        /// </summary>
        /// <remarks>
        /// documentation: https://dev.epicgames.com/docs/epic-online-services/trust-and-safety/anti-cheat-interfaces/anti-cheat-interfaces#dedicated-server-client-server-mode
        /// </remarks>
        ClientServer,

        /// <summary>
        /// The gamesession uses the P2P EAC mode.
        /// </summary>
        /// <remarks>
        /// documentation: https://dev.epicgames.com/docs/epic-online-services/trust-and-safety/anti-cheat-interfaces/anti-cheat-interfaces#peer-to-peer-mesh-peer-to-peer-mode
        /// </remarks>
        Mesh,

    }
    /// <summary>
    /// Provides method to customize game sessions.
    /// </summary>
    public static class GamesessionExtensions
    {
        /// <summary>
        /// Configures EAC for a game session.
        /// </summary>
        /// <param name="builder"></param>
        /// <param name="mode"></param>
        /// <returns></returns>
        public static GameSessionTemplateConfiguration ConfigureEAC(this GameSessionTemplateConfiguration builder, EasyAntiCheatMode mode)
        {
            return builder.CustomizeScene(s => s.TemplateMetadata["eac"] = mode.ToString());
        }
    }
}
