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
#include "users/ClientAPI.hpp"
#include "stormancer/IPlugin.h"

namespace Stormancer
{
	namespace BlobStorage
	{
		namespace details
		{
			struct StageBlockArgs
			{
				std::string token;
				std::string blockId;

				STRM_MSGPACK_DEFINE(token, blockId)
			};

			struct CommitBlocksArgs
			{
				std::string token;
				std::vector<std::string> blockIds;

				STRM_MSGPACK_DEFINE(token, blockIds)
			};

			class BlobStorageService
			{
			public:

				BlobStorageService(std::weak_ptr<RpcService> rpc, std::shared_ptr<Serializer> serializer)
					: _serializer(serializer)
					, _rpc(rpc)
				{}


				pplx::task<void> stageBlock(std::string uploadToken, std::string blockId, const byte* buffer, const size_t length)
				{
					auto rpc = _rpc.lock();
					auto serializer = _serializer;
					return rpc->rpc("Blob.StageBlock", [serializer, uploadToken, blockId, buffer, length](obytestream& stream)
						{
							StageBlockArgs args;
							args.token = uploadToken;
							args.blockId = blockId;
							serializer->serialize(stream, args);
							stream.write(buffer, length);
						});
				}



				pplx::task<void> commitBlocks(std::string uploadToken, std::vector<std::string> blockIds)
				{
					auto rpc = _rpc.lock();
					CommitBlocksArgs args;
					args.token = uploadToken;
					args.blockIds = blockIds;
					return rpc->rpc("Blob.CommitBlocks", args);
				}


			private:

				std::shared_ptr<Serializer> _serializer;
				std::weak_ptr<RpcService> _rpc;
			};
		}

		class BlobStoragePlugin;
		class BlobStorageApi : public  ClientAPI<BlobStorageApi, details::BlobStorageService>
		{
			friend class ReportsPlugin;
		public:

			BlobStorageApi(std::weak_ptr<Users::UsersApi> users)
				: ClientAPI(users, "stormancer.blobStorage")
			{

			}



			pplx::task<void> stageBlock(std::string uploadToken, std::string blockId, const byte* buffer, const size_t length)
			{
				if (length > 4 * 1024 * 1024)
				{
					return pplx::task_from_exception<void>(std::runtime_error("data cannot be more than 4MB"));
				}

				return getService().then([uploadToken, blockId, buffer, length](std::shared_ptr<details::BlobStorageService> service)
					{
						return service->stageBlock(uploadToken, blockId, buffer, length);
					});
			}


			pplx::task<void> commitBlocks(std::string uploadToken, std::vector<std::string> blockIds)
			{

				return getService().then([uploadToken, blockIds](std::shared_ptr<details::BlobStorageService> service)
					{
						return service->commitBlocks(uploadToken, blockIds);
					});
			}


		private:

		};


		class BlobStoragePlugin : public Stormancer::IPlugin
		{
			static constexpr const char* PLUGIN_NAME = "BlobStorage";
			static constexpr const char* PLUGIN_VERSION = "1.0.0";
			static constexpr const char* METADATA_KEY = "stormancer.blobStorage";
			PluginDescription getDescription() override
			{
				return PluginDescription(PLUGIN_NAME, PLUGIN_VERSION);
			}

			void registerSceneDependencies(Stormancer::ContainerBuilder& builder, std::shared_ptr<Stormancer::Scene> scene) override
			{

				if (scene)
				{
					auto name = scene->getHostMetadata(METADATA_KEY);

					if (!name.empty())
					{
						builder.registerDependency<Stormancer::BlobStorage::details::BlobStorageService, RpcService, Serializer>().singleInstance();
					}
				}

			}
			void registerClientDependencies(Stormancer::ContainerBuilder& builder) override
			{
				builder.registerDependency<Stormancer::BlobStorage::BlobStorageApi, Stormancer::Users::UsersApi>().as<Stormancer::BlobStorage::BlobStorageApi>().singleInstance();
			}
		};
	}
}