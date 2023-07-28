#include "pch.h"

#include "stormancer/Configuration.h"

#include "users/Users.hpp"
#include "party/Party.hpp"
#include "gameFinder/GameFinder.hpp"
#include "gameSession/Gamesession.hpp"
#include "gameSession/ServerPools.hpp"

#include "stormancer/IActionDispatcher.h"
#include "stormancer/IClientFactory.h"
#include "stormancer/Logger/VisualStudioLogger.h"

static constexpr const char* ServerEndpoint = "http://localhost:8080";//"http://gc3.stormancer.com";
constexpr  char* Account = "tests";
constexpr  char* Application = "test-app";
constexpr int clients = 4;
constexpr int maxIterations = 500;

static void log(std::shared_ptr<Stormancer::IClient> client, Stormancer::LogLevel level, std::string msg)
{
	client->dependencyResolver().resolve<Stormancer::ILogger>()->log(level, "gameplay.test-stress-join-game", msg);
}
struct GameCustomParameters
{
	bool test;
	MSGPACK_DEFINE_MAP(test);
};


static pplx::task<bool> JoinGameImpl(int id, const std::string& invitationCode)
{
	auto client = Stormancer::IClientFactory::GetClient(id);

	auto users = client->dependencyResolver().resolve<Stormancer::Users::UsersApi>();

	//Configure authentication to use the ephemeral (anonymous, no user stored in database) authentication.
	//The get credentialsCallback provided is automatically called by the library whenever authentication is required (during connection/reconnection)
	// It returns a task to enable you to return credential asynchronously.
	// please note that if platform plugins are installed, they automatically provide credentials.
	users->getCredentialsCallback = []() {
		Stormancer::Users::AuthParameters authParameters;
		authParameters.type = "ephemeral";
		return pplx::task_from_result(authParameters);
	};

	auto gameFinder = client->dependencyResolver().resolve<Stormancer::GameFinder::GameFinderApi>();
	auto party = client->dependencyResolver().resolve<Stormancer::Party::PartyApi>();
	return users->login().then([party, invitationCode]() {
		return party->joinPartyByInvitationCode(invitationCode);
	})
		.then([client]()
	{
		auto party = client->dependencyResolver().resolve<Stormancer::Party::PartyApi>();
		return party->getCurrentGameSessionConnectionToken();
	})
		.then([client](std::string token)
	{
		auto gameSessions = client->dependencyResolver().resolve<Stormancer::GameSessions::GameSession>();
		return gameSessions->connectToGameSession(token);
	})
		.then([client](pplx::task<Stormancer::GameSessions::GameSessionConnectionParameters> t)
	{
		//catch errors
		try
		{
			t.get();
			return true;
		}
		catch (std::exception& ex)
		{
			log(client, Stormancer::LogLevel::Error, ex.what());
			return false;
		}
	});
}
static std::string CreatePartyImpl(int id)
{


	auto client = Stormancer::IClientFactory::GetClient(id);

	auto users = client->dependencyResolver().resolve<Stormancer::Users::UsersApi>();

	//Configure authentication to use the ephemeral (anonymous, no user stored in database) authentication.
	//The get credentialsCallback provided is automatically called by the library whenever authentication is required (during connection/reconnection)
	// It returns a task to enable you to return credential asynchronously.
	// please note that if platform plugins are installed, they automatically provide credentials.
	users->getCredentialsCallback = []() {
		Stormancer::Users::AuthParameters authParameters;
		authParameters.type = "ephemeral";
		return pplx::task_from_result(authParameters);
	};

	auto gameFinder = client->dependencyResolver().resolve<Stormancer::GameFinder::GameFinderApi>();
	auto party = client->dependencyResolver().resolve<Stormancer::Party::PartyApi>();

	users->login().get();

	Stormancer::Party::PartyCreationOptions request;
	request.GameFinderName = "party-noP2P";
	party->createPartyIfNotJoined(request).get();
	log(client, Stormancer::LogLevel::Debug, "connected to party");

	return party->createInvitationCode().get();
}

static void JoinPartyImpl(int id, std::string invitationCode)
{
	auto client = Stormancer::IClientFactory::GetClient(id);

	auto users = client->dependencyResolver().resolve<Stormancer::Users::UsersApi>();

	//Configure authentication to use the ephemeral (anonymous, no user stored in database) authentication.
	//The get credentialsCallback provided is automatically called by the library whenever authentication is required (during connection/reconnection)
	// It returns a task to enable you to return credential asynchronously.
	// please note that if platform plugins are installed, they automatically provide credentials.
	users->getCredentialsCallback = []() {
		Stormancer::Users::AuthParameters authParameters;
		authParameters.type = "ephemeral";
		return pplx::task_from_result(authParameters);
	};

	auto gameFinder = client->dependencyResolver().resolve<Stormancer::GameFinder::GameFinderApi>();
	auto party = client->dependencyResolver().resolve<Stormancer::Party::PartyApi>();

	users->login().get();

	party->joinPartyByInvitationCode(invitationCode).get();
}

pplx::task<void> connectToGameSession(std::string& token,std::shared_ptr<Stormancer::IClient> client)
{
	return client->dependencyResolver().resolve<Stormancer::GameSessions::GameSession>()->connectToGameSession(token,"",false)
	.then([client](Stormancer::GameSessions::GameSessionConnectionParameters p) 
	{
		return client->dependencyResolver().resolve<Stormancer::GameSessions::GameSession>()->setPlayerReady();
	});
}

void createAndLeaveGameSession()
{
	auto client = Stormancer::IClientFactory::GetClient(0);

	auto t = client->dependencyResolver().resolve<Stormancer::GameFinder::GameFinderApi>()->waitGameFound();

	for (int i = 0; i < clients; i++)
	{
		auto c = Stormancer::IClientFactory::GetClient(i);
		c->dependencyResolver().resolve<Stormancer::Party::PartyApi>()->updatePlayerStatus(Stormancer::Party::PartyUserStatus::Ready);
	}
	auto evt = t.get();

	std::vector<pplx::task<void>> tasks;

	for (int i = 0; i < clients; i++)
	{
		auto c = Stormancer::IClientFactory::GetClient(i);
		tasks.push_back(connectToGameSession(evt.data.connectionToken,c));
	}
	pplx::when_all(tasks.begin(), tasks.end()).get();


	ASSERT_TRUE(client->dependencyResolver().resolve<Stormancer::Party::PartyApi>()->isInGameSession());

	tasks.clear();
	
	for (int i = 0; i < clients; i++)
	{
		auto c = Stormancer::IClientFactory::GetClient(i);
		c->dependencyResolver().resolve<Stormancer::GameSessions::GameSession>()->disconnectFromGameSession().get();
	}
	pplx::when_all(tasks.begin(), tasks.end()).get();

	ASSERT_FALSE(client->dependencyResolver().resolve<Stormancer::Party::PartyApi>()->isInGameSession());

}

TEST(StressTests, StressTestJoinGamesession) {

	
	//Create an action dispatcher to dispatch callbacks and continuation in the thread running the method.
	auto dispatcher = std::make_shared<Stormancer::MainThreadActionDispatcher>();

	//Create a configurator used for all clients.
	Stormancer::IClientFactory::SetDefaultConfigurator([dispatcher](size_t id) {

		//Create a configuration that connects to the test application.
		auto config = Stormancer::Configuration::create(std::string(ServerEndpoint), std::string(Account), std::string(Application));

		//Log in VS output window.
		config->logger = std::make_shared<Stormancer::VisualStudioLogger>(std::to_string(id));


		//Add plugins required by the test.
		config->addPlugin(new Stormancer::Users::UsersPlugin());
		config->addPlugin(new Stormancer::Party::PartyPlugin());
		config->addPlugin(new Stormancer::GameFinder::GameFinderPlugin());
		config->addPlugin(new Stormancer::GameSessions::GameSessionsPlugin());


		//Use the dispatcher we created earlier to ensure all callbacks are run on the test main thread.
		config->actionDispatcher = dispatcher;
		return config;
	});

	std::thread mainLoop([dispatcher]()
	{
		while (true)
		{
			std::this_thread::sleep_for(std::chrono::milliseconds(10));
			dispatcher->update(std::chrono::milliseconds(10));
		}
	});


	auto client = Stormancer::IClientFactory::GetClient(0);
	auto invitationCode = CreatePartyImpl(0);
	
	for (int i = 1; i < clients; i++)
	{
		JoinPartyImpl(i,invitationCode);
	}

	for (int i = 0; i < maxIterations; i++)
	{
		createAndLeaveGameSession();
		log(client, Stormancer::LogLevel::Info, "Iteration : "+std::to_string(i));
	}



	for (int i = 0; i < clients; i++)
	{
		Stormancer::IClientFactory::ReleaseClient(i);
	}

}