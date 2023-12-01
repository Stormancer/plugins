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

void GameSessionViewModel::setPlayerReady()
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

void GameSessionViewModel::leaveGameSession()
{
	auto client = Stormancer::IClientFactory::GetClient(this->parent->id);

	auto gameSession = client->dependencyResolver().resolve<Stormancer::GameSessions::GameSession>();

	this->parent->isProcessing = true;
	gameSession->disconnectFromGameSession().then([this](pplx::task<void> t)
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
	auto peers = gameSession->scene()->connectedPeers();
	for (auto p : peers)
	{
		P2PRemotePeerViewModel vm;



		vm.isRelay = p.second->useRelay();
		vm.sessionId = p.second->sessionId();
		results.push_back(vm);

	}
	return results;
}