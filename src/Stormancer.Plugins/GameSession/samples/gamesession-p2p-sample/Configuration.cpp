#include "Configuration.h"
#include "stormancer/Configuration.h"
#include "stormancer/IClientFactory.h"

#include "users/Users.hpp"
#include "party/Party.hpp"
#include "gamefinder/GameFinder.hpp"
#include "gamesession/GameSession.hpp"

#include "stormancer/Logger/ConsoleLogger.h"

std::shared_ptr<Stormancer::MainThreadActionDispatcher> dispatcher = nullptr;
std::shared_ptr<Stormancer::ILogger> logger = nullptr;
std::shared_ptr<Stormancer::IClient> getClient()
{
	dispatcher = std::make_shared<Stormancer::MainThreadActionDispatcher>();
	logger = std::make_shared<Stormancer::ConsoleLogger>();
	Stormancer::IClientFactory::SetDefaultConfigurator([](size_t id) {
		
		auto config = Stormancer::Configuration::create("http://91.170.22.30:40101", "tests", "test-app");

		config->addPlugin(new Stormancer::Users::UsersPlugin());
		config->addPlugin(new Stormancer::Party::PartyPlugin());
		config->addPlugin(new Stormancer::GameFinder::GameFinderPlugin());
		config->addPlugin(new Stormancer::GameSessions::GameSessionsPlugin());
		config->actionDispatcher = dispatcher;
		config->logger = logger;
		return config;
	});

	return Stormancer::IClientFactory::GetClient(0);
	

}