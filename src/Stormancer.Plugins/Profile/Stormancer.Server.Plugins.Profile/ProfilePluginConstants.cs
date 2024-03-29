﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Stormancer.Server.Plugins.Profile
{
    /// <summary>
    /// Constants for the plugin Profile.
    /// </summary>
    public static class ProfilePluginConstants
    {
        /// <summary>
        /// Id of the scene exposing profiles services.
        /// </summary>
        public const string SCENE_ID = "profile";

        /// <summary>
        /// Id of the template of the profiles scene.
        /// </summary>
        public const string SCENE_TEMPLATE = "profile";

        /// <summary>
        /// Id of the profiles service.
        /// </summary>
        public const string SERVICE_ID = "stormancer.profile";

        /// <summary>
        /// Id of the profiles service.
        /// </summary>
        public const string SERVICE_ID_DEPRECATED = "stormancer.profiles";
    }
}
