#pragma once
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


#include "stormancer/Tasks.h"
#include <string>
#include <unordered_map>
#include "stormancer/msgpack_define.h"
#include "gamesession/GameSession.hpp"
#if !defined(STORM_PLUGIN_IMPL)
#define STORM_PLUGIN_IMPL 0
#endif

namespace Stormancer
{
	namespace GameSessions
	{
		namespace GamesessionsBrowserConstants
		{
			constexpr const char* METADATA_KEY = "stormancer.gamesessions.search";
			constexpr const char* PLUGIN_NAME = "GamesessionsBrowser";
			constexpr const char* PLUGIN_VERSION = "0.1.0";
		}

		struct GameSessionDocumentBody
		{
			std::unordered_map<std::string, std::string> settings;
			std::vector<Stormancer::GameSessions::TeamConfiguration> teams;
			int playerCount;
			int64 createdOn;
			std::string name;

			STRM_MSGPACK_DEFINE(settings, teams, playerCount, createdOn, name)
		};
		struct GamesessionDocument
		{
			std::string id;
			GameSessionDocumentBody content;

			STRM_MSGPACK_DEFINE(id, content)
		};

		struct GamesessionsSearchResult
		{
			Stormancer::uint32 total;

			std::vector<GamesessionDocument> hits;
			STRM_MSGPACK_DEFINE(total, hits)
		};


		class GamesessionsBrowserApi
		{
		public:

			virtual ~GamesessionsBrowserApi() = default;



			/// <summary>
			/// Search game sessions using the provided query.
			/// </summary>
			/// <param name="jsonQuery">
			/// the json query, using the Lucene query format.
			/// 
			/// For instance :
			///
			/// "bool":{
			/// 	"must": [
			/// 	{
			/// 		"term":"numplayers",
			/// 			"value" : 2
			/// 	},
			/// 	{
			/// 		"term":"gamemode.map",
			/// 		"value" : "level3-a"
			/// 	}]
			/// }
			/// </param>
			/// <param name="skip">Number of records the database should skip before the first returned document. For performance reason, avoid high values.</param>
			/// <param name="size"></param>
			/// <param name="cancellationToken"></param>
			/// <returns></returns>
			virtual pplx::task<GamesessionsSearchResult> searchGamesessions(const std::string& jsonQuery, Stormancer::uint32 skip, Stormancer::uint32 size, pplx::cancellation_token cancellationToken) = 0;
		};
	}
}

#if STORM_PLUGIN_IMPL == 1
#include "stormancer/IPlugin.h"
#include "stormancer/Logger/ILogger.h"
#include "users/ClientAPI.hpp"
#include "gamesession/GameSession.hpp"

namespace Stormancer
{
	namespace GameSessions
	{


		class GamesessionsBrowserService
		{
		public:

			GamesessionsBrowserService(std::shared_ptr<Scene> scene, std::shared_ptr<IClient> client)
				: _wScene(scene)
				, _wClient(client)
				, _rpcService(scene->dependencyResolver().resolve<RpcService>())
				, _serializer(scene->dependencyResolver().resolve<Serializer>())
				, _logger(scene->dependencyResolver().resolve<ILogger>())
			{}

			~GamesessionsBrowserService()
			{}

			pplx::task<GamesessionsSearchResult> searchGamesessions(const std::string& jsonQuery, Stormancer::uint32 skip, Stormancer::uint32 size, pplx::cancellation_token cancellationToken)
			{
				auto rpc = _rpcService.lock();
				if (!rpc)
				{
					throw Stormancer::ObjectDeletedException("Scene");
				}
				return rpc->rpc<GamesessionsSearchResult>("GamesessionBrowser.Search", cancellationToken, jsonQuery, skip, size);
					
			}

		private:

			std::weak_ptr<Scene> _wScene;
			std::weak_ptr<IClient> _wClient;
			std::weak_ptr<RpcService> _rpcService;
			std::shared_ptr<Serializer> _serializer;
			std::shared_ptr<Stormancer::ILogger> _logger;
		};
		

		class GamesessionsBrowser_Impl : public ClientAPI<GamesessionsBrowser_Impl, GamesessionsBrowserService>, public GamesessionsBrowserApi
		{
		public:

			GamesessionsBrowser_Impl(std::weak_ptr<Users::UsersApi> users)
				: ClientAPI(users, GamesessionsBrowserConstants::METADATA_KEY)
				
			{}

			pplx::task<GamesessionsSearchResult> searchGamesessions(const std::string& jsonQuery, Stormancer::uint32 skip, Stormancer::uint32 size, pplx::cancellation_token cancellationToken) override
			{
				return getGamesessionsBrowserService(cancellationToken)
					.then([jsonQuery, skip, size, cancellationToken](std::shared_ptr<GamesessionsBrowserService> gr)
						{
							return gr->searchGamesessions(jsonQuery, skip, size, cancellationToken);
						});
			}


		private:
			pplx::task<std::shared_ptr<GamesessionsBrowserService>> getGamesessionsBrowserService(pplx::cancellation_token cancellationToken)
			{
				return this->getService([](auto, auto, auto) {}, [](auto, auto) {}, cancellationToken);
			}
			
		};


		class GamesessionsBrowserPlugin : public IPlugin
		{
		public:

			PluginDescription getDescription() override
			{
				return PluginDescription(GamesessionsBrowserConstants::PLUGIN_NAME, GamesessionsBrowserConstants::PLUGIN_VERSION);
			}

		private:

			void registerSceneDependencies(ContainerBuilder& builder, std::shared_ptr<Scene> scene) override
			{
				if (scene)
				{
					auto name = scene->getHostMetadata(GamesessionsBrowserConstants::METADATA_KEY);
					if (!name.empty())
					{
						builder.registerDependency<GamesessionsBrowserService, Scene, IClient>().singleInstance();
					}
					
				}
			}

			void registerClientDependencies(ContainerBuilder& builder) override
			{
				builder.registerDependency<GamesessionsBrowser_Impl, Users::UsersApi>().as<GamesessionsBrowserApi>().singleInstance();
			}
		};
	}
}

#endif
