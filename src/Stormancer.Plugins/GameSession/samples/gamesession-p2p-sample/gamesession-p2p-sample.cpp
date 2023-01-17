// gamesession-p2p-sample.cpp : This file contains the 'main' function. Program execution begins and ends there.
//

#include <iostream>

#include "Configuration.h"

#include "users/Users.hpp"
#include "party/Party.hpp"
#include "gamefinder/GameFinder.hpp"
#include "gamesession/GameSession.hpp"
#include <future>

enum class State
{
	Initializing = 0,
	LoggedIn= 1,
	Matchmaking = 2,
	JoiningGame = 3,
	InGame = 4
};
State state = State::Initializing;
bool stateChanged = false;


std::string GetLineFromCin() {
	std::string line;
	std::getline(std::cin, line);
	return line;
}


void updateState(State newState)
{
	state = newState;
	stateChanged = true;
}
int main()
{
	
    auto client = getClient();

    auto users = client->dependencyResolver().resolve<Stormancer::Users::UsersApi>();
    auto party = client->dependencyResolver().resolve<Stormancer::Party::PartyApi>();
    auto gamefinder = client->dependencyResolver().resolve<Stormancer::GameFinder::GameFinderApi>();
	auto gameSession = client->dependencyResolver().resolve<Stormancer::GameSessions::GameSession>();

	//Configure authentication to use the ephemeral (anonymous, no user stored in database) authentication.
	//The get credentialsCallback provided is automatically called by the library whenever authentication is required (during connection/reconnection)
	// It returns a task to enable you to return credential asynchronously.
	// please note that if platform plugins are installed, they automatically provide credentials.
	users->getCredentialsCallback = []() {
		Stormancer::Users::AuthParameters authParameters;
		authParameters.type = "ephemeral";
		return pplx::task_from_result(authParameters);
	};

	
	users->login().then([party]() 
	{
		Stormancer::Party::PartyCreationOptions request;
		request.GameFinderName = "joingame-test";
		return party->createPartyIfNotJoined(request);
	}).then([gamefinder]()
	{
		updateState(State::LoggedIn);
		return gamefinder->waitGameFound();
	}).then([gameSession](Stormancer::GameFinder::GameFoundEvent evt)
	{
		updateState(State::JoiningGame);
		return gameSession->connectToGameSession(evt.data.connectionToken, "", false);
	}).then([gameSession](Stormancer::GameSessions::GameSessionConnectionParameters p)
	{
		if (p.isHost)
		{
			updateState(State::InGame);
		}

		return  gameSession->setPlayerReady();

	}).then([]()
	{
		updateState(State::InGame);
	});


	//loop until test is completed and run library events.
	auto future = std::async(std::launch::async, GetLineFromCin);

	while (true)
	{
		//Runs the  callbacks and continuations waiting to be executed (mostly user code) for max 5ms.
		dispatcher->update(std::chrono::milliseconds(5));
		std::this_thread::sleep_for(std::chrono::milliseconds(10));
		if (stateChanged)
		{
			stateChanged = false;
			switch (state)
			{
			case State::LoggedIn:
				std::cout << "Logged in. Enter the 'start game' command to start matchmaking.";
				break;
			case State::InGame:
				std::cout << "In game";
			default:
				break;
			}
		}
		if (future.wait_for(std::chrono::seconds(0)) == std::future_status::ready)
		{
			auto line = future.get();
			if (line == "start game")
			{
				updateState(State::Matchmaking);
				party->updatePlayerStatus(Stormancer::Party::PartyUserStatus::Ready);

			}

			future = std::async(std::launch::async, GetLineFromCin);
		}


	}


    std::cout << "Hello World!\n";
}

// Run program: Ctrl + F5 or Debug > Start Without Debugging menu
// Debug program: F5 or Debug > Start Debugging menu

// Tips for Getting Started: 
//   1. Use the Solution Explorer window to add/manage files
//   2. Use the Team Explorer window to connect to source control
//   3. Use the Output window to see build output and other messages
//   4. Use the Error List window to view errors
//   5. Go to Project > Add New Item to create new code files, or Project > Add Existing Item to add existing code files to the project
//   6. In the future, to open this project again, go to File > Open > Project and select the .sln file
