#pragma once
#include "stormancer/Subscription.h"
#define STRM_PLUGIN_IMPL 0
#include "replication/Lockstep.hpp"

struct Snapshot
{
	int frame;
	std::string state;
};

class GameSessionViewModel;

class LockstepViewModel
{
public:
	LockstepViewModel(GameSessionViewModel* parent);

	void initialize();

	bool isEnabled();
	void Reset();

	void addCommand(byte cmd);

	void tick(float delta);

	int getLockstepTime();

	bool isPaused();

	void Pause(bool pause);

	std::string currentState;

	std::vector<::Stormancer::Gameplay::LockstepPlayer> getPlayers();

private:
	int _clientId;
	GameSessionViewModel* parent;
	Stormancer::Subscription _onStepSubscription;
	Stormancer::Subscription _onRollbackSubscription;

	std::vector<Snapshot> snapshots;
};