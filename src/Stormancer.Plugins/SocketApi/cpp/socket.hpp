#ifndef STORMANCER_SOCKET_API
#define STORMANCER_SOCKET_API

#include "stormancer/Tasks.h"
namespace Stormancer
{
	namespace Socket
	{
		struct ReceivedMsgInfos
		{
			std::string sessionId;
			int length;
		};
		class SocketApi
		{
		public:
			void send(std::string sceneId,std::string destSessionId, char* buffer, int offset, int length);

			ReceivedMsgInfos receive(std::string, char* buffer, int offset, int maxLength);

			pplx::task<ReceivedMsgInfos> receiveAsync(std::string, char* buffer, int offset, int maxLength, pplx::cancellation_token cancellationToken);

		};
	}
}
#endif

#if !defined(STORMANCER_SOCKET_API_IMPL) && (!defined(STORMANCER_PLUGINS_SEPARATE_IMPL) || defined(STORMANCER_PLUGINS_CONFIG))
#define STORMANCER_SOCKET_API_IMPL

#include "stormancer/IPlugin.h"
#include "stormancer/Version.h"

namespace Stormancer
{
	namespace Socket
	{
		class SocketApiPlugin : public IPlugin
		{
		private:
			static constexpr const char* PLUGIN_NAME = "stormancer.socket";
			static constexpr const char* PLUGIN_VERSION = "0.1.0";
		public:
			PluginDescription getDescription() override
			{
				return PluginDescription(PLUGIN_NAME, PLUGIN_VERSION);
			}
		};
	}
}
#endif