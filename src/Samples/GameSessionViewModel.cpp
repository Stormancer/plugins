#include "GameSessionViewModel.h"
#include "stormancer/IClientFactory.h"
#include "gamesession/GameSession.hpp"
#include "ViewModel.h"

GameSessionViewModel::GameSessionViewModel(ClientViewModel* parent)
	:parent(parent)
{
}

bool GameSessionViewModel::isInGameSession()
{

	auto client = Stormancer::IClientFactory::GetClient(this->parent->id);

	auto gameSession = client->dependencyResolver().resolve<Stormancer::GameSessions::GameSession>();

	return gameSession->isInSession();
}

void GameSessionViewModel::setPeerReady()
{

	auto client = Stormancer::IClientFactory::GetClient(this->parent->id);

	auto gameSession = client->dependencyResolver().resolve<Stormancer::GameSessions::GameSession>();

	this->parent->isProcessing = true;
	gameSession->setPlayerReady().then([this](pplx::task<void> t)
	{

		this->parent->isProcessing = false;
		try
		{
			t.get();
		}
		catch (std::exception& ex)
		{
			this->parent->lastError = ex.what();
		}

	});
}

std::vector<P2PRemotePeerViewModel> GameSessionViewModel::getP2PRemotePeers()
{
	auto client = Stormancer::IClientFactory::GetClient(this->parent->id);

	auto gameSession = client->dependencyResolver().resolve<Stormancer::GameSessions::GameSession>();

	std::vector<P2PRemotePeerViewModel> results;
	for (auto p : gameSession->scene()->remotePeers())
	{
		P2PRemotePeerViewModel vm;
		auto p2pPeer = std::static_pointer_cast<Stormancer::IP2PScenePeer>(p);

		vm.isRelay = p2pPeer->useRelay();
		vm.sessionId = p2pPeer->sessionId();
		results.push_back(vm);
	}
	return results;
}