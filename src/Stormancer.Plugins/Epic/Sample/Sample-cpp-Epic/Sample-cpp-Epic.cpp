#include "Users/ClientAPI.hpp"
#include "Users/Users.hpp"
#include "Party/Party.hpp"
#include "GameFinder/GameFinder.hpp"
#include "GameVersion/GameVersion.hpp"
#include "Profile/Profile.hpp"

#include "stormancer/Logger/ConsoleLogger.h"

#include <thread>

#include "../../cpp/Epic.hpp"

// Copy GameProductConfig.sample.h to GameProductConfig.h with values corresponding to your Epic game product
#include "GameProductConfig.h"

using namespace Stormancer;

int main()
{
	static std::shared_ptr<ILogger> s_logger = std::make_shared<ConsoleLogger>();
	std::shared_ptr<MainThreadActionDispatcher> actionDispatcher = std::make_shared<MainThreadActionDispatcher>();

	auto config = Configuration::create(STORM_ENDPOINT, STORM_ACCOUNT, STORM_APPLICATION);
	config->logger = s_logger;
	config->actionDispatcher = actionDispatcher;
	config->additionalParameters[Epic::ConfigurationKeys::InitPlatform] = "true";
	config->additionalParameters[Epic::ConfigurationKeys::ProductName] = "Sample-cpp-Epic";
	config->additionalParameters[Epic::ConfigurationKeys::ProductVersion] = "0.1";
	config->additionalParameters[Epic::ConfigurationKeys::AuthenticationEnabled] = "true";
	config->additionalParameters[Epic::ConfigurationKeys::LoginMode] = STORM_EPIC_LOGIN_MODE;
	config->additionalParameters[Epic::ConfigurationKeys::DevAuthHost] = STORM_EPIC_DEVAUTH_CREDENTIALS_HOST;
	config->additionalParameters[Epic::ConfigurationKeys::DevAuthCredentialsName] = STORM_EPIC_DEVAUTH_CREDENTIALS_NAME;
	config->additionalParameters[Epic::ConfigurationKeys::ProductId] = STORM_EPIC_PRODUCT_ID;
	config->additionalParameters[Epic::ConfigurationKeys::SandboxId] = STORM_EPIC_SANDBOX_ID;
	config->additionalParameters[Epic::ConfigurationKeys::DeploymentId] = STORM_EPIC_DEPLOYMENT_ID;
	config->additionalParameters[Epic::ConfigurationKeys::ClientId] = STORM_EPIC_CLIENT_ID;
	config->additionalParameters[Epic::ConfigurationKeys::ClientSecret] = STORM_EPIC_CLIENT_SECRET;
	config->additionalParameters[Epic::ConfigurationKeys::Diagnostics] = "true";
	config->additionalParameters[GameVersion::ConfigurationKeys::ClientVersion] = "0.1.0";
	config->addPlugin(new Users::UsersPlugin());
	config->addPlugin(new GameFinder::GameFinderPlugin());
	config->addPlugin(new Party::PartyPlugin());
	config->addPlugin(new Epic::EpicPlugin());
	config->addPlugin(new GameVersion::GameVersionPlugin());
	config->addPlugin(new GameVersion::GameVersionPlugin());
	config->addPlugin(new Profile::ProfilePlugin());
	auto client = IClient::create(config);
	auto usersApi = client->dependencyResolver().resolve<Users::UsersApi>();
	auto profileApi = client->dependencyResolver().resolve<Profile::ProfileApi>();

	bool disconnected = false;

	std::thread mainLoop([actionDispatcher, &disconnected]()
	{
		while (!disconnected)
		{
			actionDispatcher->update(std::chrono::milliseconds(10));
		}
	});

	std::thread thread([usersApi, profileApi, &disconnected, client]()
	{
		try
		{
			usersApi->login().get();
		}
		catch (const std::exception& ex)
		{
			s_logger->log(LogLevel::Error, "Sample-cpp-Epic", "Login failed", ex.what());
			return;
		}

		std::string userId = usersApi->userId();
		s_logger->log(LogLevel::Info, "SampleMain", "Login succeed!", "userId = " + userId);

		try
		{
			Profile::Profile profile = profileApi->getProfile(userId, { { "character", "details" }, { "epic", "details" } }).get();
			s_logger->log(LogLevel::Info, "SampleMain", "Profile retrieved", "UserName=" + profile.data["displayName"]);
		}
		catch (const std::exception& ex)
		{
			s_logger->log(LogLevel::Error, "SampleMain", "Profile retrieve failed", ex.what());
		}

		usersApi->logout()
			.then([client, &disconnected]()
		{
			client->disconnect()
				.then([&disconnected]()
			{
				disconnected = true;
			});
		});
	});

	thread.join();
	mainLoop.join();

	return 0;
}
