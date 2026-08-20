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
		const static size_t MAX_BLOCK_SIZE = 4 * 1024 * 1024; // 4MB

		namespace details
		{
			struct Result
			{
				bool success;
				std::string reason;

				STRM_MSGPACK_DEFINE(success, reason)
			};

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


				pplx::task<Result> stageBlock(std::string uploadToken, std::string blockId, const byte* buffer, const size_t length)
				{
					auto rpc = _rpc.lock();
					auto serializer = _serializer;
					return rpc->rpc<Result>("Blob.StageBlock", [serializer, uploadToken, blockId, buffer, length](BufferWriter& stream)
						{
							StageBlockArgs args;
							args.token = uploadToken;
							args.blockId = blockId;
							serializer->serialize(stream, args);
							auto span = stream.getSpan(length);
							std::memcpy(span.data(), buffer, length);
							stream.advance(length);
						});
				}



				pplx::task<Result> commitBlocks(std::string uploadToken, std::vector<std::string> blockIds)
				{
					auto rpc = _rpc.lock();
					CommitBlocksArgs args;
					args.token = uploadToken;
					args.blockIds = blockIds;
					return rpc->rpc<Result>("Blob.CommitBlocks", args);
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
		private:
			static pplx::task<void> uploadFileRecursive(
				std::weak_ptr<BlobStorageApi> wThis,
				std::string uploadToken,
				int blockId,
				const byte* bufferPosition,
				const size_t length,
				const size_t blockSize)
			{
				auto that = wThis.lock();
				if (!that)
				{
					//stop uploading if the BlobStorageApi is destroyed
				}
				if (length < blockSize)
				{
					return that->stageBlock(uploadToken, std::to_string(blockId), bufferPosition, length);
				}
				else
				{
					return that->stageBlock(uploadToken, std::to_string(blockId), bufferPosition, blockSize)
						.then([wThis, uploadToken, blockId, bufferPosition, length, blockSize]()
							{
								return uploadFileRecursive(wThis, uploadToken, blockId + 1, bufferPosition + blockSize, length - blockSize, blockSize);
							});
				}
			}

		public:
			BlobStorageApi(std::weak_ptr<Users::UsersApi> users)
				: ClientAPI(users, "stormancer.blobStorage")
			{}

			pplx::task<void> uploadFile(std::string uploadToken, const byte* buffer, const size_t length, const size_t blockSize = MAX_BLOCK_SIZE)
			{
				if (length > MAX_BLOCK_SIZE && blockSize > MAX_BLOCK_SIZE)
				{
					return pplx::task_from_exception<void>(std::runtime_error("data block size cannot be more than 4MB"));
				}

				std::weak_ptr<BlobStorageApi> wThis = this->shared_from_this();

				return uploadFileRecursive(wThis, uploadToken, 0, buffer, length, blockSize)
					.then([this, uploadToken, length, blockSize]()
						{
							std::vector<std::string> blockIds;
							for (int i = 0; i < (length + blockSize - 1) / blockSize; i++)
							{
								blockIds.push_back(std::to_string(i));
							}
							return commitBlocks(uploadToken, blockIds);
						});
			}

			pplx::task<void> stageBlock(std::string uploadToken, std::string blockId, const byte* buffer, const size_t length)
			{
				if (length > MAX_BLOCK_SIZE)
				{
					return pplx::task_from_exception<void>(std::runtime_error("data cannot be more than 4MB"));
				}

				return getService().then([uploadToken, blockId, buffer, length](std::shared_ptr<details::BlobStorageService> service)
					{
						return service->stageBlock(uploadToken, blockId, buffer, length);
					})
					.then([](details::Result result)
						{
							if (!result.success)
							{
								throw std::runtime_error(result.reason);
							}
						});
			}


			pplx::task<void> commitBlocks(std::string uploadToken, std::vector<std::string> blockIds)
			{

				return getService().then([uploadToken, blockIds](std::shared_ptr<details::BlobStorageService> service)
					{
						return service->commitBlocks(uploadToken, blockIds);
					})
					.then([](details::Result result)
						{
							if (!result.success)
							{
								throw std::runtime_error(result.reason);
							}
						});
			}

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