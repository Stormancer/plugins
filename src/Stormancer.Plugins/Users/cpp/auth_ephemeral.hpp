#pragma once
#include "users/Users.hpp"

namespace Stormancer
{
	namespace Users
	{
		namespace Auth
		{
			namespace details
			{
				class EphemeralAuth: public ::Stormancer::Users::IAuthenticationEventHandler
				{
					virtual pplx::task<void> retrieveCredentials(const ::Stormancer::Users::CredentialsContext& ctx)
					{
						if (ctx.tryUseProvider("ephemeral"))
						{
							ctx.authParameters->type = "ephemeral";
						}
						return pplx::task_from_result();
					}
				};

			}

			/// <summary>
			/// Enables Ephemeral auth
			/// </summary>
			class EphemeralPlugin: public IPlugin
			{
				static constexpr const char* PLUGIN_NAME = "Users.Auth.Ephemeral";
				static constexpr const char* PLUGIN_VERSION = "1.0.0";

				PluginDescription getDescription() override
				{
					return PluginDescription(PLUGIN_NAME, PLUGIN_VERSION);
				}

			private:

				void registerClientDependencies(ContainerBuilder& builder) override
				{
					builder.registerDependency<details::EphemeralAuth>().as<IAuthenticationEventHandler>();
					
				}
			};
		}
	}
}