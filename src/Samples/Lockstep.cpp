#include "Lockstep.h"
#include "stormancer/IClientFactory.h"
#include "GameSessionViewModel.h"
#include "ViewModel.h"

LockstepViewModel::LockstepViewModel(GameSessionViewModel* parent)
	:parent(parent)
	, _clientId(parent->parent->id)
{
	
	auto client = Stormancer::IClientFactory::GetClient(_clientId);
	this->_onStepSubscription = client->dependencyResolver().resolve<Stormancer::Gameplay::LockstepApi>()->onStep.subscribe([this](Stormancer::Gameplay::Frame step) 
	{
		
	});

	this->_onRollbackSubscription = client->dependencyResolver().resolve<Stormancer::Gameplay::LockstepApi>()->onRollback.subscribe([this](Stormancer::Gameplay::RollbackContext& ctx)
	{
		Snapshot& current = snapshots.front();
		for (auto& snapshot : snapshots)
		{
			if (snapshot.frame > ctx.targetFrame)
			{
				break;
			}
			else
			{
				current = snapshot;
			}
		}
		
		currentState = current.state;
		ctx.restoredFrame = current.frame;

	});
}

void LockstepViewModel::Reset()
{
	currentState = "";
	snapshots.clear();
	Snapshot snapshot;
	snapshots.push_back(snapshot);
}

void LockstepViewModel::addCommand(byte cmd)
{
	auto client = Stormancer::IClientFactory::GetClient(_clientId);

	auto api = client->dependencyResolver().resolve<Stormancer::Gameplay::LockstepApi>();
	
	api->pushCommand(&cmd, 1);

}

int LockstepViewModel::getLockstepTime()
{
	auto client = Stormancer::IClientFactory::GetClient(_clientId);

	auto api = client->dependencyResolver().resolve<Stormancer::Gameplay::LockstepApi>();
	
	return api->getCurrentTime();
}

void LockstepViewModel::tick(float delta)
{
	auto client = Stormancer::IClientFactory::GetClient(_clientId);

	auto api = client->dependencyResolver().resolve<Stormancer::Gameplay::LockstepApi>();
	api->tick((int)(delta*1000));
}