#include "GameFinderViewModel.h"
#include "GameSessionViewModel.h"
#include "Lockstep.h"
#include "ViewModel.h"
#include "stormancer/IClientFactory.h"
#include "gamefinder/GameFinder.hpp"
#include "gamesession/GameSession.hpp"

GameFinderViewModel::GameFinderViewModel(ClientViewModel* parent)
	:parent(parent)
{
	
}

void GameFinderViewModel::initialize()
{
	auto client = Stormancer::IClientFactory::GetClient(this->parent->id);
	auto gameFinder = client->dependencyResolver().resolve<Stormancer::GameFinder::GameFinderApi>();
	subscription = gameFinder->subscribeGameFound([this](Stormancer::GameFinder::GameFoundEvent evt) {

		this->lastConnectionToken = evt.data.connectionToken;
	});
}

void GameFinderViewModel::joinGameFound()
{
	auto client = Stormancer::IClientFactory::GetClient(this->parent->id);

	auto gameSession = client->dependencyResolver().resolve<Stormancer::GameSessions::GameSession>();

	this->parent->isProcessing = true;
	gameSession->connectToGameSession(lastConnectionToken, "", false).then([this](pplx::task<Stormancer::GameSessions::GameSessionConnectionParameters> t) {
		this->parent->isProcessing = false;
		try
		{
			auto p = t.get();
			this->parent->gameSession.isHost = p.isHost;
			this->parent->gameSession.lockstep->currentState = "";
			Stormancer::SessionId::tryParse(p.hostSessionId, this->parent->gameSession.hostSessionId);
			
			 
		}
		catch (std::exception& ex)
		{
			this->parent->lastError = ex.what();
		}
	});

}

bool GameFinderViewModel::isGameFound()
{

	return this->lastConnectionToken != "";
}
