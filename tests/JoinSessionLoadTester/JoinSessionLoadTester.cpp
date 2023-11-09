#include <iostream>

#include "stormancer/Configuration.h"

#include "users/Users.hpp"
#include "party/Party.hpp"
#include "gameFinder/GameFinder.hpp"
#include "gameSession/Gamesession.hpp"
#include "gameSession/ServerPools.hpp"

#include "stormancer/IActionDispatcher.h"
#include "stormancer/IClientFactory.h"
#include "stormancer/Logger/VisualStudioLogger.h"
#include "stormancer/Logger/NullLogger.h"

static void log(std::shared_ptr<Stormancer::IClient> client, Stormancer::LogLevel level, std::string msg)
{
	client->dependencyResolver().resolve<Stormancer::ILogger>()->log(level, "gameplay.test-join-game", msg);
}
struct GameCustomParameters
{
	bool test;
	MSGPACK_DEFINE_MAP(test);
};
static pplx::task<bool> JoinGameImpl(int id, const std::string& invitationCode)
{
	auto client = Stormancer::IClientFactory::GetClient(id);

	log(client, Stormancer::LogLevel::Info, "JoinGameImpl");

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
			return gameSessions->connectToGameSession(token, "", false);
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
static pplx::task<std::string> CreateGameImpl(int id)
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

	//Create a task that will complete the next time a game is found.
	auto gameFoundTask = gameFinder->waitGameFound();




	//Name of the matchmaking, defined in Stormancer.Server.TestApp/TestPlugin.cs.
	//>  host.AddGamefinder("matchmaking", "matchmaking");

	return users->login().then([party]() {
		Stormancer::Party::PartyCreationOptions request;
	request.GameFinderName = "joinpartygame-test";
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
			.then([client, gameFoundTask]()
				{
					log(client, Stormancer::LogLevel::Info, "player status updated");

			//Wait game found.
			return gameFoundTask;
				})
				.then([client](Stormancer::GameFinder::GameFoundEvent evt)
					{
						log(client, Stormancer::LogLevel::Info, "game found");

				auto gameSessions = client->dependencyResolver().resolve<Stormancer::GameSessions::GameSession>();
				return gameSessions->connectToGameSession(evt.data.connectionToken, "", false);

					})
					//Errors flow through continuations that take TResult instead of task<TResult> as argument.
					//We want to handle errors in the last continuation, so this one takes task<TResult>. Inside we get the result of the task by calling task.get()
					//inside a try clause. If an error occured  .get() will throw. We return false (error). If it doesn't throw, everything succeeded.
						.then([id, client](Stormancer::GameSessions::GameSessionConnectionParameters params)
							{
								log(client, Stormancer::LogLevel::Info, "connected to game session");


					//P2P connection established.
					//In the host, this continuation is executed immediatly.
					//In clients this continuation is executed only if the host called gameSessions->setPlayerReady() (see below)
					if (params.isHost)
					{
						//Start the game host. To communicate with clients, either:
						//- Use the scene API to send and listen to messages.
						//- Start a datagram socket and bind to the port specified in config->severGamePort
					}
					else
					{
						//The host called "setPlayerReady". It should be ready to accept messages. To communicate with the server, either:
						//- Use the scene API to send and listen to messages.
						//- Start a socket on a random port (port 0) and send UDP datagrams to the endpoint specified in 'params.endpoint'.
						// They will be automatically routed to the socket bound by the host as described above.
					}
					auto gameSessions = client->dependencyResolver().resolve<Stormancer::GameSessions::GameSession>();
					return  gameSessions->setPlayerReady();

							})
						.then([client]()
							{
								log(client, Stormancer::LogLevel::Info, "player ready set");

							auto party = client->dependencyResolver().resolve<Stormancer::Party::PartyApi>();
							return party->createInvitationCode();
							})
								.then([client](pplx::task<std::string> t)
									{
										//catch errors
										try
							{
								auto result = t.get();
								log(client, Stormancer::LogLevel::Info, "created invitation code");

								return result;
							}
							catch (std::exception& ex)
							{
								log(client, Stormancer::LogLevel::Error, ex.what());
								return std::string("");
							}
									});


}

int TestJoinGamesession(size_t runNumber, int iterations)
{
	int result = 0;



	for (int i = 0; i < iterations; i++)
	{
		int hostIndex = 2 * (runNumber * iterations + i);
		int clientIndex = hostIndex + 1;

		//printf("host:%d, client:%d\n", hostIndex, clientIndex);

		auto t = CreateGameImpl(hostIndex);
		auto invitationCode = t.get();
		auto check = invitationCode.size() != 0;
		bool success = false;
		if (check)
		{
			auto t2 = JoinGameImpl(clientIndex, invitationCode);

			success = t2.get();
		}

		if (success)
		{
			result++;
			//printf("success (%d/%d)\n", result, iterations);
		}
		else
		{
			//printf("failure (%d/%d)\n", result, iterations);
		}

		Stormancer::IClientFactory::ReleaseClient(hostIndex);
		Stormancer::IClientFactory::ReleaseClient(clientIndex);
	}
	return result;
}


int main(int argc, char* argv[])
{
	if (argc != 6)
	{
		printf("Usage\n");
		printf("\t<endpoint> (ex: http://localhost)\n");
		printf("\t<account> (ex: tests)\n");
		printf("\t<app> (ex: test-app)\n");
		printf("\t<pairs count>\n");
		printf("\t<iterations count>\n");
		return 1;
	}

	std::string endpoint(argv[1]);
	std::string account(argv[2]);
	std::string app(argv[3]);
	int nbPairs = std::stoi(argv[4]);
	int nbGames = std::stoi(argv[5]);

	//Create an action dispatcher to dispatch callbacks and continuation in the thread running the method.
	auto dispatcher = std::make_shared<Stormancer::MainThreadActionDispatcher>();


	//Create a configurator used for all clients.
	Stormancer::IClientFactory::SetDefaultConfigurator([endpoint, account, app, dispatcher](size_t id) {

		//Create a configuration that connects to the test application.
		auto config = Stormancer::Configuration::create(endpoint, account, app);

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

	auto start = std::chrono::system_clock::now();
	std::vector<pplx::task<int>> tasks;

	for (int i = 0; i < nbPairs; i++)
	{
		tasks.push_back(pplx::create_task([i, nbGames]() { return TestJoinGamesession(i, nbGames); }));
	}

	int result = 0;
	for (int i = 0; i < nbPairs; i++)
	{
		auto currentTask = tasks[i];
		while (!currentTask.is_done())
		{
			//Runs the  callbacks and continuations waiting to be executed (mostly user code) for max 5ms.
			dispatcher->update(std::chrono::milliseconds(30));
			std::this_thread::sleep_for(std::chrono::milliseconds(5));
		}
		result += currentTask.get();
	}

	auto end = std::chrono::system_clock::now();

	auto elapsedmilliseconds = std::chrono::duration_cast<std::chrono::milliseconds>(end - start);
	printf("{'total':%d, 'success':%d, 'elapsedms':%d}", nbPairs * nbGames, result, (int)elapsedmilliseconds.count());
}
