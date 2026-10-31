using Stormancer.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Stormancer.Server.Plugins.Voice
{
    /// <summary>
    /// Voice chat related exension methods.
    /// </summary>
    public static class VoiceChatExtensions
    {
        /// <summary>
        /// Add a voice room to the scene.
        /// </summary>
        /// <param name="scene"></param>
        public static void AddVoiceRoom(this ISceneHost scene)
        {
            scene.AddController<VoiceChatController>();
        }
    }
}
