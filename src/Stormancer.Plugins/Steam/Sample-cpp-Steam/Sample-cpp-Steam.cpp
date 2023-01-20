#include "Users/ClientAPI.hpp"
#include "Users/Users.hpp"
#include "Party/Party.hpp"
#include "GameFinder/GameFinder.hpp"
#include "GameVersion/GameVersion.hpp"
#include "Profile/Profile.hpp"

#include "stormancer/Logger/ConsoleLogger.h"
#include "stormancer/cpprestsdk/cpprest/json.h"

#include <thread>

#include "../cpp/Steam.hpp"

// Copy GameProductConfig.sample.h to GameProductConfig.h with values corresponding to your Steam game product
#include "GameProductConfig.h"

int main()
{

	static std::shared_ptr<Stormancer::ILogger> s_logger = std::make_shared<Stormancer::ConsoleLogger>();
	std::shared_ptr<Stormancer::MainThreadActionDispatcher> actionDispatcher = std::make_shared<Stormancer::MainThreadActionDispatcher>();

	auto config = Stormancer::Configuration::create(STORM_ENDPOINT, STORM_ACCOUNT, STORM_APPLICATION);
	config->logger = s_logger;
	config->actionDispatcher = actionDispatcher;
	config->additionalParameters[Stormancer::Steam::ConfigurationKeys::AuthenticationEnabled] = "true";
	config->additionalParameters[Stormancer::Steam::ConfigurationKeys::SteamApiInitialize] = "true";
	config->additionalParameters[Stormancer::Steam::ConfigurationKeys::SteamApiRunCallbacks] = "true";
	config->additionalParameters[Stormancer::GameVersion::ConfigurationKeys::ClientVersion] = STORM_CLIENT_VERSION;
	config->addPlugin(new Stormancer::Users::UsersPlugin());
	config->addPlugin(new Stormancer::GameFinder::GameFinderPlugin());
	config->addPlugin(new Stormancer::Party::PartyPlugin());
	config->addPlugin(new Stormancer::Steam::SteamPlugin());
	config->addPlugin(new Stormancer::GameVersion::GameVersionPlugin());
	config->addPlugin(new Stormancer::Profile::ProfilePlugin());
	auto client = Stormancer::IClient::create(config);
	auto usersApi = client->dependencyResolver().resolve<Stormancer::Users::UsersApi>();
	auto profileApi = client->dependencyResolver().resolve<Stormancer::Profile::ProfileApi>();
	auto partyApi = client->dependencyResolver().resolve<Stormancer::Party::PartyApi>();

	bool disconnected = false;

	std::thread mainLoop([actionDispatcher, &disconnected]()
	{
		while (!disconnected)
		{
			actionDispatcher->update(std::chrono::milliseconds(10));
		}
	});

	auto onvitationReceivedSubscription = partyApi->subscribeOnInvitationReceived([](Stormancer::Party::PartyInvitation partyInvitation)
	{
		if (partyInvitation.isValid())
		{
			s_logger->log(Stormancer::LogLevel::Info, "SteamSample", "Party invitation received", partyInvitation.getSenderId());
			partyInvitation.acceptAndJoinParty()
				.then([](pplx::task<void> task)
			{
				try
			{
				task.get();
				s_logger->log(Stormancer::LogLevel::Info, "SteamSample", "Party invitation accepted and party joined");
			}
			catch (const std::exception& ex)
			{
				s_logger->log(Stormancer::LogLevel::Error, "SteamSample", "Fail to join a party after accepting the invitation", ex.what());
			}
			});
		}
		else
		{
			s_logger->log(Stormancer::LogLevel::Error, "SteamSample", "Invalid party invitation received", partyInvitation.getSenderId());
		}
	});

	try
	{
		usersApi->login().get();
	}
	catch (const std::exception& ex)
	{
		s_logger->log(Stormancer::LogLevel::Error, "Sample-cpp-Steam", "Login failed", ex.what());
		disconnected = true;
		mainLoop.join();
		return 1;
	}

	std::string stormancerUserId = usersApi->userId();
	s_logger->log(Stormancer::LogLevel::Info, "SampleMain", "Login succeed!", "userId=" + stormancerUserId);

	try
	{
		Stormancer::Profile::Profile profile = profileApi->getProfile(stormancerUserId, { { "character", "details" }, { "user", "details" }, {"steam", "details"} }).get();

		auto steamPart = profile.data["steam"];
		if (!steamPart)
		{
			throw std::runtime_error("Steam part missing");
		}
		Stormancer::web::json::value jsonValue = Stormancer::web::json::value::parse(Stormancer::utility::conversions::to_string_t(*steamPart));
		if (jsonValue.type() != Stormancer::web::json::value::value_type::Object)
		{
			throw std::runtime_error("Bad json type: not an object");
		}

		Stormancer::web::json::value& accountIdValue = jsonValue.as_object().at(Stormancer::utility::conversions::to_string_t("accountId"));
		if (accountIdValue.type() != Stormancer::web::json::value::value_type::String)
		{
			throw std::runtime_error("Bad json type: not a string");
		}
		std::string accountId = Stormancer::utility::conversions::to_utf8string(accountIdValue.as_string());

		Stormancer::web::json::value& productUserIdValue = jsonValue.as_object().at(Stormancer::utility::conversions::to_string_t("productUserId"));
		if (productUserIdValue.type() != Stormancer::web::json::value::value_type::String)
		{
			throw std::runtime_error("Bad json type: not a string");
		}
		std::string productUserId = Stormancer::utility::conversions::to_utf8string(productUserIdValue.as_string());

		Stormancer::web::json::value& displayNameValue = jsonValue.as_object().at(Stormancer::utility::conversions::to_string_t("displayName"));
		if (displayNameValue.type() != Stormancer::web::json::value::value_type::String)
		{
			throw std::runtime_error("Bad json type: not a string");
		}
		std::string displayName = Stormancer::utility::conversions::to_utf8string(displayNameValue.as_string());

		s_logger->log(Stormancer::LogLevel::Info, "SampleMain", "Profile retrieved", "AccountId=" + accountId + "; ProductUserId=" + productUserId + "; DisplayName=" + displayName);
	}
	catch (const std::exception& ex)
	{
		s_logger->log(Stormancer::LogLevel::Error, "SteamSampleMain", "Profile retrieve failed", ex.what());
	}

	Stormancer::Party::PartyCreationOptions partyCreationOptions;
	partyCreationOptions.isJoinable = true;
	partyCreationOptions.isPublic = true;
	partyApi->createParty(partyCreationOptions)
		.then([](pplx::task<void> task)
	{
		try
	{
		task.get();
	}
	catch (const std::exception& ex)
	{
		s_logger->log(Stormancer::LogLevel::Error, "SteamSampleMain", "Create party failed", ex.what());
	}
	})
		.get();

	//usersApi->logout()
	//	.then([client, &disconnected]()
	//{
	//	client->disconnect()
	//		.then([&disconnected]()
	//	{
	//		disconnected = true;
	//	});
	//});

	mainLoop.join();

	return 0;
}
