#include "pch.h"

#include "stormancer/Configuration.h"

#include "users/Users.hpp"
#include "party/Party.hpp"
#include "party/PartyMerging.hpp"
#include "gamefinder/GameFinder.hpp"

#include "stormancer/IActionDispatcher.h"
#include "stormancer/IClientFactory.h"
#include "stormancer/Logger/VisualStudioLogger.h"

static constexpr const char* ServerEndpoint = "http://localhost:8080";//"http://gc3.stormancer.com";
constexpr  char* Account = "tests";
constexpr  char* Application = "test-app";

static void log(std::shared_ptr<Stormancer::IClient> client, Stormancer::LogLevel level, std::string msg)
{
	client->dependencyResolver().resolve<Stormancer::ILogger>()->log(level, "gameplay.test-join-game", msg);
}
struct GameCustomParameters
{
	bool test;
	MSGPACK_DEFINE_MAP(test);
};
static pplx::task<bool> CreateParty(int id)
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

	auto party = client->dependencyResolver().resolve<Stormancer::Party::PartyApi>();
	auto merger = client->dependencyResolver().resolve<Stormancer::Party::PartyMergingApi>();
	return users->login().then([party]() 
	{
		Stormancer::Party::PartyCreationOptions request;
		request.GameFinderName = "joingame-test";
		return party->createPartyIfNotJoined(request);
	}).then([merger]() 
	{
		return merger->start("duo");

	}).then([]()
	{
	
		using namespace std::chrono_literals;
		return Stormancer::taskDelay(1000ms);
	}).then([party]()
	{
		using namespace std::chrono_literals;
		pplx::cancellation_token_source cts;
		return Stormancer::withRetries<bool>([party](pplx::cancellation_token ct)
		{
			auto size = party->getPartyMembers().size();
			if (size == 2)
			{
				return pplx::task_from_result(true);
			}
			else
			{
				throw std::runtime_error("notJoined");
			}

		}, 1000ms, 10, [](const std::exception&) {return true; },pplx::get_ambient_scheduler(),cts.get_token());
		
	}).then([client](pplx::task<bool> t)
	{
		try
		{
			return t.get();
		}
		catch (std::exception& ex)
		{
			log(client, Stormancer::LogLevel::Error, ex.what());
			return false;
		}
	});
		
}


TEST(Gameplay, TestPartyMerger) {

	//Create an action dispatcher to dispatch callbacks and continuation in the thread running the method.
	auto dispatcher = std::make_shared<Stormancer::MainThreadActionDispatcher>();

	//Create a configurator used for all clients.
	Stormancer::IClientFactory::SetDefaultConfigurator([dispatcher](size_t id) {

		//Create a configuration that connects to the test application.
		auto config = Stormancer::Configuration::create(std::string(ServerEndpoint), std::string(Account), std::string(Application));

		//Log in VS output window.
		config->logger = std::make_shared<Stormancer::VisualStudioLogger>();


		//Add plugins required by the test.
		config->addPlugin(new Stormancer::Users::UsersPlugin());
		config->addPlugin(new Stormancer::Party::PartyPlugin());
		config->addPlugin(new Stormancer::GameFinder::GameFinderPlugin());
		config->addPlugin(new Stormancer::Party::PartyMergingPlugin());

		//Use the dispatcher we created earlier to ensure all callbacks are run on the test main thread.
		config->actionDispatcher = dispatcher;
		return config;
	});





	auto t0 = CreateParty(0);
	auto t1 = CreateParty(1);

	//loop until test is completed and run library events.
	while (!(t0.is_done() && t1.is_done()))
	{
		//Runs the  callbacks and continuations waiting to be executed (mostly user code) for max 5ms.
		dispatcher->update(std::chrono::milliseconds(5));
		std::this_thread::sleep_for(std::chrono::milliseconds(10));
	}
	
	EXPECT_TRUE(t0.get());
	EXPECT_TRUE(t1.get());
	//We are connected to the game session, we can test the socket API.



	Stormancer::IClientFactory::ReleaseClient(0);
	Stormancer::IClientFactory::ReleaseClient(1);


}

TEST(Gameplay, TestPartyMergerCancellation) {

	//Create an action dispatcher to dispatch callbacks and continuation in the thread running the method.
	auto dispatcher = std::make_shared<Stormancer::MainThreadActionDispatcher>();

	//Create a configurator used for all clients.
	Stormancer::IClientFactory::SetDefaultConfigurator([dispatcher](size_t id) {

		//Create a configuration that connects to the test application.
		auto config = Stormancer::Configuration::create(std::string(ServerEndpoint), std::string(Account), std::string(Application));

		//Log in VS output window.
		config->logger = std::make_shared<Stormancer::VisualStudioLogger>();


		//Add plugins required by the test.
		config->addPlugin(new Stormancer::Users::UsersPlugin());
		config->addPlugin(new Stormancer::Party::PartyPlugin());
		config->addPlugin(new Stormancer::GameFinder::GameFinderPlugin());
		config->addPlugin(new Stormancer::Party::PartyMergingPlugin());

		//Use the dispatcher we created earlier to ensure all callbacks are run on the test main thread.
		config->actionDispatcher = dispatcher;
		return config;
	});

	auto t0 = CreateParty(0);
	auto t1 = CreateParty(1);

	//loop until test is completed and run library events.
	while (!(t0.is_done() && t1.is_done()))
	{
		//Runs the  callbacks and continuations waiting to be executed (mostly user code) for max 5ms.
		dispatcher->update(std::chrono::milliseconds(5));
		std::this_thread::sleep_for(std::chrono::milliseconds(10));
	}

	EXPECT_TRUE(t0.get());
	EXPECT_TRUE(t1.get());
	//We are connected to the game session, we can test the socket API.



	Stormancer::IClientFactory::ReleaseClient(0);
	Stormancer::IClientFactory::ReleaseClient(1);


}