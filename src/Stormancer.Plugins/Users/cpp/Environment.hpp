// Users client library for Stormancer
// Copyright (C) 2025 Stormancer
// 
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
// 
// The above copyright notice and this permission notice shall be included in
// all copies or substantial portions of the Software.
// 
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE 
// SOFTWARE.

#ifndef STORM_PLUGIN_IMPL
#define STORM_PLUGIN_IMPL 0
#endif

#ifndef STORM_PLUGIN_ENVIRONMENT_H
#define STORM_PLUGIN_ENVIRONMENT_H

#include <unordered_map>
#include <string>

namespace Stormancer
{
	class IProjectEnvironmentEventsHandler
	{
	public:
		virtual void onGetMetadata(std::unordered_map<std::string, std::string>& metadata) = 0;

		virtual ~IProjectEnvironmentEventsHandler() {}
	};

	class IProjectEnvironment
	{
	public:
		virtual std::unordered_map<std::string, std::string> getMetadata() = 0;

		virtual ~IProjectEnvironment() {}
	};
}

#endif
#if STORM_PLUGIN_IMPL
#undef STORM_PLUGIN_IMPL
#include "stormancer/IPlugin.h"
#include "stormancer/IClient.h"

#define STORM_PLUGIN_IMPL 1
#include <vector>

namespace Stormancer
{
	class ProjectEnvironmentImpl: public IProjectEnvironment
	{
	public:
		ProjectEnvironmentImpl(std::vector<std::shared_ptr<IProjectEnvironmentEventsHandler>> handlers)
			: _handlers(handlers)
		{

		}

		std::unordered_map<std::string, std::string> getMetadata() override
		{
			std::unordered_map<std::string, std::string> metadata;
			for (auto& handler : _handlers)
			{
				handler->onGetMetadata(metadata);
			}
			return metadata;
		}


	private:
		std::vector<std::shared_ptr<IProjectEnvironmentEventsHandler>> _handlers;
	};

	class EnvironmentPlugin : public IPlugin
	{
		static constexpr const char* PLUGIN_NAME = "Environment";
		static constexpr const char* PLUGIN_VERSION = "1.0.0";

		PluginDescription getDescription() override
		{
			return PluginDescription(PLUGIN_NAME, PLUGIN_VERSION);
		}

		void registerClientDependencies(ContainerBuilder& clientBuilder)
		{
			clientBuilder.registerDependency<ProjectEnvironmentImpl,ContainerBuilder::All<IProjectEnvironmentEventsHandler>>().as<IProjectEnvironment>();
		}
	};
}
#endif