#pragma once
#include "GameSessionViewModel.h"
#include "stormancer/Event.h"
#define STRM_PLUGIN_IMPL 0
#include "replication/Lockstep.hpp"

struct Snapshot
{
	int frame;
	std::string state;
};
class LockstepViewModel
{
public:
	LockstepViewModel(GameSessionViewModel* parent);

	void Reset();

	void addCommand(byte cmd);

	void tick(float delta);

	int getLockstepTime();

	std::string currentState;

private:
	int _clientId;
	GameSessionViewModel* parent;
	Stormancer::Subscription _onStepSubscription;
	Stormancer::Subscription _onRollbackSubscription;

	std::vector<Snapshot> snapshots;
};