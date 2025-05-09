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

	Stormancer::Gameplay::Time getLockstepTime();

	Stormancer::Gameplay::Time getTargetTime();

	Stormancer::Gameplay::FrameDuration getCommandLatency();

	bool isPaused();

	void Pause(bool pause);

	std::string currentState;

	std::vector<::Stormancer::Gameplay::LockstepPlayer> getPlayers();

private:
	double gameplayTime = 0;
	double realTime = 0;

	int _clientId;
	GameSessionViewModel* parent;
	Stormancer::Subscription _onStepSubscription;
	Stormancer::Subscription _onRollbackSubscription;

	Stormancer::Subscription _onInstallSnapshotSubscription;
	Stormancer::Subscription _onCreateSnapshotSubscription;
	Stormancer::Subscription _onStartSubscription;

	std::vector<Snapshot> snapshots;
};