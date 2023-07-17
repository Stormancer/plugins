#include "pch.h"

#include "stormancer/Configuration.h"

#include "users/Users.hpp"
#include "party/Party.hpp"
#include "gameFinder/GameFinder.hpp"
#include "gameSession/Gamesession.hpp"

#include "stormancer/IActionDispatcher.h"
#include "stormancer/IClientFactory.h"
#include "stormancer/Logger/VisualStudioLogger.h"

//static constexpr const char* ServerEndpoint = "http://localhost:8080";
//static constexpr const char* ServerEndpoint = "http://gc3.stormancer.com";
static constexpr const char* ServerEndpoint = "http://stormancer-1.stormancer.com:8081";
constexpr  char* Account = "tests";
constexpr  char* Application = "test-app";

static void log(std::shared_ptr<Stormancer::IClient> client, Stormancer::LogLevel level, std::string msg)
{
	client->dependencyResolver().resolve<Stormancer::ILogger>()->log(level, "gameplay.disableGameSessionDirectConnection", msg);
}

static pplx::task<bool> JoinGameImpl(int id)
{
	auto client = Stormancer::IClientFactory::GetClient(id);

	auto users = client->dependencyResolver().resolve<Stormancer::Users::UsersApi>();

	//Configure authentication to use the ephemeral (anonymous, no user stored in database) authentication.
	//The get credentialsCallback provided is automatically called by the library whenever authentication is required (during connection/reconnection)
	// It returns a task to enable you to return credential asynchronously.
	// please note that if platform plugins are installed, they automatically provide credentials.
	users->getCredentialsCallback = []()
	{
		Stormancer::Users::AuthParameters authParameters;
		authParameters.type = "ephemeral";
		return pplx::task_from_result(authParameters);
	};

	auto gameFinder = client->dependencyResolver().resolve<Stormancer::GameFinder::GameFinderApi>();
	auto party = client->dependencyResolver().resolve<Stormancer::Party::PartyApi>();

	//Create a task that will complete the next time a game is found.
	auto gameFoundTask = gameFinder->waitGameFound();

	return users->login()
		.then([party]()
			{
				Stormancer::Party::PartyCreationOptions request;
				request.GameFinderName = "disable-direct-connection-test";
				//Name of the matchmaking, defined in Stormancer.Server.TestApp/TestPlugin.cs.
				//>  host.AddGamefinder("matchmaking", "matchmaking");

				return party->createPartyIfNotJoined(request);
			})
		.then([client]()
			{
				log(client, Stormancer::LogLevel::Debug, "connected to party");
				auto party = client->dependencyResolver().resolve<Stormancer::Party::PartyApi>();

				//Triggers matchmking by setting the player as ready.
				//Matchmaking starts when all players in the party are ready.
				return party->updatePlayerStatus(Stormancer::Party::PartyUserStatus::Ready);
			})
				.then([gameFoundTask]()
					{
						//Wait game found.
						return gameFoundTask;
					})
				.then([client](Stormancer::GameFinder::GameFoundEvent evt)
					{
						auto gameSessions = client->dependencyResolver().resolve<Stormancer::GameSessions::GameSession>();
						return gameSessions->connectToGameSession(evt.data.connectionToken, "", false);

					})
						//Errors flow through continuations that take TResult instead of task<TResult> as argument.
						//We want to handle errors in the last continuation, so this one takes task<TResult>. Inside we get the result of the task by calling task.get()
						//inside a try clause. If an error occured  .get() will throw. We return false (error). If it doesn't throw, everything succeeded.
						.then([id, client](Stormancer::GameSessions::GameSessionConnectionParameters params)
							{
								//P2P connection established.
								//In the host, this continuation is executed immediatly.
								//In clients this continuation is executed only if the host called gameSessions->setPlayerReady() (see below)
								if (params.isHost)
								{
									log(client, Stormancer::LogLevel::Info, "host=true");
									//Start the game host. To communicate with clients, either:
									//- Use the scene API to send and listen to messages.
									//- Start a datagram socket and bind to the port specified in config->severGamePort
								}
								else
								{
									log(client, Stormancer::LogLevel::Info, "host=false");
									//The host called "setPlayerReady". It should be ready to accept messages. To communicate with the server, either:
									//- Use the scene API to send and listen to messages.
									//- Start a socket on a random port (port 0) and send UDP datagrams to the endpoint specified in 'params.endpoint'.
									// They will be automatically routed to the socket bound by the host as described above.
								}
								auto gameSessions = client->dependencyResolver().resolve<Stormancer::GameSessions::GameSession>();
								return  gameSessions->setPlayerReady();

							})
						.then([client](pplx::task<void> t)
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

TEST(Gameplay, TestDisableGameSessionDirectConnection)
{
	//Create an action dispatcher to dispatch callbacks and continuation in the thread running the method.
	auto dispatcher = std::make_shared<Stormancer::MainThreadActionDispatcher>();

	//Create a configurator used for all clients.
	Stormancer::IClientFactory::SetDefaultConfigurator([dispatcher](size_t id)
		{
			//Create a configuration that connects to the test application.
			auto config = Stormancer::Configuration::create(std::string(ServerEndpoint), std::string(Account), std::string(Application));

			//Log in VS output window.
			config->logger = std::make_shared<Stormancer::VisualStudioLogger>();

			//Add plugins required by the test.
			config->addPlugin(new Stormancer::Users::UsersPlugin());
			config->addPlugin(new Stormancer::Party::PartyPlugin());
			config->addPlugin(new Stormancer::GameFinder::GameFinderPlugin());
			config->addPlugin(new Stormancer::GameSessions::GameSessionsPlugin());

			//Use the dispatcher we created earlier to ensure all callbacks are run on the test main thread.
			config->actionDispatcher = dispatcher;
			return config;
		});

	std::vector<pplx::task<bool>> tasks;
	tasks.push_back(JoinGameImpl(0));
	tasks.push_back(JoinGameImpl(1));
	auto t = pplx::when_all(tasks.begin(), tasks.end());

	//loop until test is completed and run library events.
	while (!t.is_done())
	{
		//Runs the  callbacks and continuations waiting to be executed (mostly user code) for max 5ms.
		dispatcher->update(std::chrono::milliseconds(5));
		std::this_thread::sleep_for(std::chrono::milliseconds(10));
	}

	for (auto t : tasks)
	{
		EXPECT_TRUE(t.get());
	}

	//We are connected to the game session, we can test the socket API.
	Stormancer::IClientFactory::ReleaseClient(0);
	Stormancer::IClientFactory::ReleaseClient(1);
}
