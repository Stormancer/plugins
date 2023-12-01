#pragma once

#include "stormancer/SessionId.h"

class ClientViewModel;

struct P2PRemotePeerViewModel
{
	std::string sessionId;
	bool isRelay;
};

class GameSessionViewModel
{
public:
	GameSessionViewModel(ClientViewModel* parent);

	bool isHost;
	Stormancer::SessionId hostSessionId;
	
	bool isInGameSession();

	void setPlayerReady();

	void leaveGameSession();

	ClientViewModel* parent;

	std::vector<P2PRemotePeerViewModel> getP2PRemotePeers();
};