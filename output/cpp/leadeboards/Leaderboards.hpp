// MIT License
//
// Copyright (c) 2020 Stormancer
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

#pragma once
#include "Users/ClientAPI.hpp"
#include "stormancer/IPlugin.h"

namespace Stormancer
{
	namespace Leaderboards
	{
		enum class ComparisonOperator : int8
		{
			GREATER_THAN_OR_EQUAL = 0,
			GREATER_THAN = 1,
			LESSER_THAN_OR_EQUAL = 2,
			LESSER_THAN = 3
		};

		enum class LeaderboardOrdering : int8
		{
			Ascending = 0,
			Descending = 1
		};

		struct ScoreFilter
		{
			ComparisonOperator type;
			std::string path;
			float value = 0;

			MSGPACK_DEFINE(type, path, value);
		};

		struct FieldFilter
		{
			std::string field;
			std::vector<std::string> values;

			MSGPACK_DEFINE(field, values);
		};

		struct LeaderboardQuery
		{
			std::string startId;
			std::vector<ScoreFilter> scoreFilters;
			std::vector<FieldFilter> fieldFilters;
			int32 size = 1;
			int32 skip = 0;
			std::string leaderboardName;
			std::vector<std::string> friendsIds;
			LeaderboardOrdering order = LeaderboardOrdering::Descending;
			bool friendsOnly = false;

			///<summary>
			/// Path in the scores object to use for ranking in the query.
			///</summary>
			std::string scorePath;

			MSGPACK_DEFINE(startId, scoreFilters, fieldFilters, size, skip, leaderboardName, friendsIds, order, scorePath, friendsOnly);
		};

		/// <summary>
		/// Represents a score
		/// </summary>
		/// <typeparam name="TScores"></typeparam>
		/// <typeparam name="TDocument"></typeparam>
		template<typename TScores, typename TDocument>
		struct ScoreEntry
		{
			std::string id;
			TScores scores;
			int64 createdOn = 0;
			TDocument document;

			MSGPACK_DEFINE(id, scores, createdOn, document);
		};
		template<typename TScores, typename TDocument>
		struct LeaderboardRanking
		{
			int32 ranking = 0;
			ScoreEntry<TScores, TDocument> document;

			MSGPACK_DEFINE(ranking, document);
		};

		template<typename TScores, typename TDocument>
		struct LeaderboardResult
		{
			std::string leaderboardName;
			std::vector<LeaderboardRanking<TScores, TDocument>> results;
			std::string next;
			std::string previous;
			int64 total;

			MSGPACK_DEFINE(leaderboardName, results, next, previous, total);
		};



		class LeaderboardPlugin;

		namespace details
		{
			class LeaderboardService
			{
			public:

				LeaderboardService(std::weak_ptr<Scene> scene, std::shared_ptr<RpcService> rpc)
					: _scene(scene)
					, _rpcService(rpc)
				{
				}

				//Query a leaderboard
				template<typename TScores, typename TDocument>
				pplx::task<LeaderboardResult<TScores, TDocument>> query(LeaderboardQuery query)
				{
					return _rpcService->rpc<LeaderboardResult<TScores, TDocument>>("leaderboard.query", query);
				}

				//Query a leaderboard using a cursor obtained from a LeaderboardResult (result.next or result.previous)
				template<typename TScores, typename TDocument>
				pplx::task<LeaderboardResult<TScores, TDocument>> query(const std::string& cursor)
				{
					return _rpcService->rpc<LeaderboardResult<TScores, TDocument>>("leaderboard.cursor", cursor);
				}

			private:

				std::weak_ptr<Scene> _scene;
				std::shared_ptr<RpcService> _rpcService;
			};
		}

		class Leaderboard : public Stormancer::ClientAPI<Leaderboard, details::LeaderboardService>
		{
		public:

			Leaderboard(std::weak_ptr<Stormancer::Users::UsersApi> users)
				: Stormancer::ClientAPI<Leaderboard, details::LeaderboardService>(users, "stormancer.plugins.leaderboards")
			{
			}

			~Leaderboard() {}

			//Query a leaderboard
			template<typename TScores, typename TDocument>
			pplx::task<LeaderboardResult<TScores, TDocument>> query(LeaderboardQuery query)
			{
				return getLeaderboardService()
					.then([query](std::shared_ptr<Stormancer::Leaderboards::details::LeaderboardService> service)
						{
							return service->query<TScores, TDocument>(query);
						});
			}

			template<typename TScores, typename TDocument>
			//Query a leaderboard using a cursor obtained from a LeaderboardResult (result.next or result.previous)
			pplx::task<LeaderboardResult<TScores, TDocument>> query(const std::string& cursor)
			{
				return getLeaderboardService()
					.then([cursor](std::shared_ptr<Stormancer::Leaderboards::details::LeaderboardService> service)
						{
							return service->query<TScores, TDocument>(cursor);
						});
			}

		private:

			pplx::task<std::shared_ptr<Stormancer::Leaderboards::details::LeaderboardService>> getLeaderboardService()
			{
				return this->getService();
			}
		};

		class LeaderboardPlugin : public Stormancer::IPlugin
		{
		public:

			static constexpr const char* PLUGIN_NAME = "Leaderboard";
			static constexpr const char* PLUGIN_VERSION = "1.0.0";

			PluginDescription getDescription() override
			{
				return PluginDescription(PLUGIN_NAME, PLUGIN_VERSION);
			}

		private:

			void registerSceneDependencies(Stormancer::ContainerBuilder& builder, std::shared_ptr<Stormancer::Scene> scene) override
			{

				if (scene)
				{
					auto name = scene->getHostMetadata("stormancer.leaderboard");

					if (!name.empty())
					{
						builder.registerDependency<Stormancer::Leaderboards::details::LeaderboardService, Scene, RpcService>().singleInstance();
					}
				}

			}
			void registerClientDependencies(Stormancer::ContainerBuilder& builder) override
			{
				builder.registerDependency<Stormancer::Leaderboards::Leaderboard, Users::UsersApi>().as<Leaderboard>().singleInstance();
			}

		};

	}

}


MSGPACK_ADD_ENUM(Stormancer::Leaderboards::ComparisonOperator);
MSGPACK_ADD_ENUM(Stormancer::Leaderboards::LeaderboardOrdering);