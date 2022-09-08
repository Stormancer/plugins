#include "Users/ClientAPI.hpp"
#include "Users/Users.hpp"
#include "Party/Party.hpp"
#include "GameFinder/GameFinder.hpp"
#include "GameVersion/GameVersion.hpp"
#include "Profile/Profile.hpp"

#include "stormancer/Logger/ConsoleLogger.h"
#include "stormancer/cpprestsdk/cpprest/json.h"

#include <thread>

#include "../cpp/Galaxy.hpp"

// Copy GameProductConfig.sample.h to GameProductConfig.h with values corresponding to your Galaxy game product
#include "GameProductConfig.h"

using namespace Stormancer;

int main()
{
	static std::shared_ptr<ILogger> s_logger = std::make_shared<ConsoleLogger>();
	std::shared_ptr<MainThreadActionDispatcher> actionDispatcher = std::make_shared<MainThreadActionDispatcher>();

	auto config = Configuration::create(STORM_ENDPOINT, STORM_ACCOUNT, STORM_APPLICATION);
	config->logger = s_logger;
	config->actionDispatcher = actionDispatcher;
	config->additionalParameters[Galaxy::ConfigurationKeys::InitPlatform] = "true";
	config->additionalParameters[Galaxy::ConfigurationKeys::AuthenticationEnabled] = "true";
	config->additionalParameters[Galaxy::ConfigurationKeys::ClientId] = STORM_GALAXY_CLIENT_ID;
	config->additionalParameters[Galaxy::ConfigurationKeys::ClientSecret] = STORM_GALAXY_CLIENT_SECRET;
	config->additionalParameters[GameVersion::ConfigurationKeys::ClientVersion] = "0.1.0";
	config->addPlugin(new Users::UsersPlugin());
	config->addPlugin(new GameFinder::GameFinderPlugin());
	config->addPlugin(new Party::PartyPlugin());
	config->addPlugin(new Galaxy::GalaxyPlugin());
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

	try
	{
		usersApi->login().get();
	}
	catch (const std::exception& ex)
	{
		s_logger->log(LogLevel::Error, "Sample-cpp-Galaxy", "Login failed", ex.what());
		disconnected = true;
		mainLoop.join();
		return 1;
	}

	std::string stormancerUserId = usersApi->userId();
	s_logger->log(LogLevel::Info, "SampleMain", "Login succeed!", "userId=" + stormancerUserId);

	try
	{
		Profile::Profile profile = profileApi->getProfile(stormancerUserId, { { "character", "details" }, { "user", "details" }, {"galaxy", "details"} }).get();

		web::json::value jsonValue = web::json::value::parse(utility::conversions::to_string_t(profile.data["galaxy"]));
		if (jsonValue.type() != web::json::value::value_type::Object)
		{
			throw std::runtime_error("Bad json type: not an object");
		}

		web::json::value& galaxyIdValue = jsonValue.as_object().at(utility::conversions::to_string_t("galaxyId"));
		if (galaxyIdValue.type() != web::json::value::value_type::String)
		{
			throw std::runtime_error("Bad json type: not a string");
		}
		std::string galaxyId = utility::conversions::to_utf8string(galaxyIdValue.as_string());

		web::json::value& usernameValue = jsonValue.as_object().at(utility::conversions::to_string_t("username"));
		if (usernameValue.type() != web::json::value::value_type::String)
		{
			throw std::runtime_error("Bad json type: not a string");
		}
		std::string username = utility::conversions::to_utf8string(usernameValue.as_string());

		s_logger->log(LogLevel::Info, "SampleMain", "Profile retrieved", "GalaxyId=" + galaxyId + "; Username=" + username);
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

	mainLoop.join();

	return 0;
}
