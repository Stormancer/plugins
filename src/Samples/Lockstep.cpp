#include "Lockstep.h"
#include "stormancer/IClientFactory.h"
#include "GameSessionViewModel.h"
#include "ViewModel.h"

LockstepViewModel::LockstepViewModel(GameSessionViewModel* parent)
	:parent(parent)
	, _clientId(parent->parent->id)
{

	
}

void LockstepViewModel::initialize()
{
	currentState = "";
	auto client = Stormancer::IClientFactory::GetClient(_clientId);
	_onStepSubscription = this->_onStepSubscription = client->dependencyResolver().resolve<Stormancer::Gameplay::LockstepApi>()->onStep.subscribe([this](Stormancer::Gameplay::Frame step)
	{
		for (auto& cmd : step.commands)
		{
			char c = (char)cmd.content[0];
			currentState = currentState + c;
		}
	});

	_onRollbackSubscription = this->_onRollbackSubscription = client->dependencyResolver().resolve<Stormancer::Gameplay::LockstepApi>()->onRollback.subscribe([this](Stormancer::Gameplay::RollbackContext& ctx)
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

bool LockstepViewModel::isEnabled()
{
	auto client = Stormancer::IClientFactory::GetClient(_clientId);

	auto api = client->dependencyResolver().resolve<Stormancer::Gameplay::LockstepApi>();
	return api->isEnabled();
}

void LockstepViewModel::Reset()
{
	currentState = "";
	snapshots.clear();
	Snapshot snapshot;
	snapshots.push_back(snapshot);
}

std::vector<::Stormancer::Gameplay::LockstepPlayer> LockstepViewModel::getPlayers()
{
	auto client = Stormancer::IClientFactory::GetClient(_clientId);

	auto api = client->dependencyResolver().resolve<Stormancer::Gameplay::LockstepApi>();
	return api->getPlayers();
}

void LockstepViewModel::addCommand(byte cmd)
{
	auto client = Stormancer::IClientFactory::GetClient(_clientId);

	auto api = client->dependencyResolver().resolve<Stormancer::Gameplay::LockstepApi>();

	api->pushCommand(&cmd, 1);

}

float LockstepViewModel::getLockstepTime()
{
	auto client = Stormancer::IClientFactory::GetClient(_clientId);

	auto api = client->dependencyResolver().resolve<Stormancer::Gameplay::LockstepApi>();

	return api->getCurrentTime();
}

float LockstepViewModel::getTargetTime()
{
	auto client = Stormancer::IClientFactory::GetClient(_clientId);

	auto api = client->dependencyResolver().resolve<Stormancer::Gameplay::LockstepApi>();

	return api->getTargetTime();
}

bool LockstepViewModel::isPaused()
{
	auto client = Stormancer::IClientFactory::GetClient(_clientId);

	auto api = client->dependencyResolver().resolve<Stormancer::Gameplay::LockstepApi>();

	return api->isPaused();
}

void LockstepViewModel::Pause(bool pause)
{ 
	auto client = Stormancer::IClientFactory::GetClient(_clientId);

	auto api = client->dependencyResolver().resolve<Stormancer::Gameplay::LockstepApi>();

	api->pause(pause);
}

bool LockstepViewModel::tick(float delta)
{
	auto client = Stormancer::IClientFactory::GetClient(_clientId);

	auto api = client->dependencyResolver().resolve<Stormancer::Gameplay::LockstepApi>();
	//return api->tick((int)(delta * 1000));
	return true;
}