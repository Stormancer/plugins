#include "ViewModel.h"
#include "Party/Party.hpp"
#include "Party/PartyMerging.hpp"
#include "gamesession/GameSession.hpp"
#include "stormancer/IClientFactory.h"

PartyViewModel::PartyViewModel(ClientViewModel* parent)
	:parent(parent)
{
}

void PartyViewModel::createParty()
{
	auto client = Stormancer::IClientFactory::GetClient(this->parent->id);
	auto party = client->dependencyResolver().resolve<Stormancer::Party::PartyApi>();
	parent->isProcessing = true;

	Stormancer::Party::PartyCreationOptions options;
	options.GameFinderName = gameFinderName;

	party->createParty(options).then([this](pplx::task<void> t)
	{

		this->parent->isProcessing = false;
		try
		{
			t.get();
		}
		catch (std::exception&)
		{

		}

	});
}

void PartyViewModel::createInvitationCode()
{
	auto client = Stormancer::IClientFactory::GetClient(this->parent->id);
	auto party = client->dependencyResolver().resolve<Stormancer::Party::PartyApi>();

	party->createInvitationCode().then([this](pplx::task<std::string> t)
	{

		this->parent->isProcessing = false;
		try
		{
			this->invitationCode =t.get();
		}
		catch (std::exception&)
		{

		}

	});
}

void PartyViewModel::joinByInvitationCode()
{
	auto client = Stormancer::IClientFactory::GetClient(this->parent->id);
	auto party = client->dependencyResolver().resolve<Stormancer::Party::PartyApi>();
	party->joinPartyByInvitationCode(invitationCode).then([this](pplx::task<void> t)
	{

		this->parent->isProcessing = false;
		try
		{
			t.get();
		}
		catch (std::exception&)
		{

		}

	});
}

void PartyViewModel::updatePartyState(Stormancer::Party::PartyUserStatus newStatus)
{
	auto client = Stormancer::IClientFactory::GetClient(this->parent->id);
	auto party = client->dependencyResolver().resolve<Stormancer::Party::PartyApi>();
	parent->isProcessing = true;
	party->updatePlayerStatus(newStatus).then([this](pplx::task<void> t)
	{

		this->parent->isProcessing = false;
		try
		{
			t.get();
		}
		catch (std::exception&)
		{

		}

	});
}

void PartyViewModel::joinCurrentGameSession()
{
	//First get the connection token of the game session the party is currently in.
	//Then connect to the game session.
	auto client = Stormancer::IClientFactory::GetClient(this->parent->id);
	auto party = client->dependencyResolver().resolve<Stormancer::Party::PartyApi>();
	parent->isProcessing = true;
	party->getCurrentGameSessionConnectionToken()
		.then([client](std::string connectionToken)
	{
		auto gs = client->dependencyResolver().resolve<Stormancer::GameSessions::GameSession>();
		return gs->connectToGameSession(connectionToken);
	})
		.then([this](pplx::task<Stormancer::GameSessions::GameSessionConnectionParameters> t)
	{

		this->parent->isProcessing = false;
		try
		{
			t.get();
		}
		catch (std::exception&)
		{

		}

	});
}

void PartyViewModel::startMerging()
{
	auto client = Stormancer::IClientFactory::GetClient(this->parent->id);
	auto merging = client->dependencyResolver().resolve<Stormancer::Party::PartyMergingApi>();
	parent->isProcessing = true;
	merging->start(mergerId).then([this](pplx::task<void> t)
	{

		this->parent->isProcessing = false;
		try
		{
			t.get();
		}
		catch (std::exception&)
		{

		}

	});
}

void PartyViewModel::stopMerging()
{
	auto client = Stormancer::IClientFactory::GetClient(this->parent->id);
	auto merging = client->dependencyResolver().resolve<Stormancer::Party::PartyMergingApi>();
	parent->isProcessing = true;
	merging->stop(mergerId).then([this](pplx::task<void> t)
	{

		this->parent->isProcessing = false;
		try
		{
			t.get();
		}
		catch (std::exception&)
		{

		}

	});
}

void PartyViewModel::leaveParty()
{
	auto client = Stormancer::IClientFactory::GetClient(this->parent->id);
	auto party = client->dependencyResolver().resolve<Stormancer::Party::PartyApi>();
	parent->isProcessing = true;
	party->leaveParty().then([this](pplx::task<void> t)
	{

		this->parent->isProcessing = false;
		try
		{
			t.get();
		}
		catch (std::exception&)
		{

		}

	});
}