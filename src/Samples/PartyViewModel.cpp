// Stormancer client sample app
// Copyright (C) 2025 Stormancer
// 
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
// 
// The above copyright notice and this permission notice shall be included in
// all copies or substantial portions of the Software.
// 
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE 
// SOFTWARE.

#include "ViewModel.h"
#include "Party/Party.hpp"
#include "Party/PartyMerging.hpp"
#include "gamesession/GameSession.hpp"
#include "stormancer/IClientFactory.h"

PartyViewModel::PartyViewModel(ClientViewModel* parent)
	:parent(parent)
{
	gameFinderName = parent->parent->settings.gameFinderName;
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
				this->invitationCode = t.get();
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

void PartyViewModel::updatePartySettings()
{
	auto client = Stormancer::IClientFactory::GetClient(this->parent->id);
	auto party = client->dependencyResolver().resolve<Stormancer::Party::PartyApi>();

	auto partySettings = party->getPartySettings();
	partySettings.customData.resize(partySettingsCustomData.length());
	std::memcpy(partySettings.customData.data(), this->partySettingsCustomData.c_str(), this->partySettingsCustomData.length());
	party->updatePartySettings(partySettings).then([this](pplx::task<void> t)
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

void PartyViewModel::updateMemberData()
{
	auto client = Stormancer::IClientFactory::GetClient(this->parent->id);
	auto party = client->dependencyResolver().resolve<Stormancer::Party::PartyApi>();


	std::vector<Stormancer::byte> bytes;

	bytes.resize(memberCustomData.length());

	std::memcpy(bytes.data(), memberCustomData.c_str(), memberCustomData.length());

	party->updatePlayerData(bytes).then([this](pplx::task<void> t)
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
				return gs->connectToGameSession(connectionToken, "", false);
			})
		.then([this](pplx::task<Stormancer::GameSessions::GameSessionConnectionParameters> t)
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

bool PartyViewModel::isInGameSession()
{
	auto client = Stormancer::IClientFactory::GetClient(this->parent->id);
	auto party = client->dependencyResolver().resolve<Stormancer::Party::PartyApi>();
	return party->isInGameSession();
}

void PartyViewModel::InvitePlayerToParty(const Stormancer::Users::UserId& userId)
{
	auto client = Stormancer::IClientFactory::GetClient(this->parent->id);
	auto party = client->dependencyResolver().resolve<Stormancer::Party::PartyApi>();
	if (party->isInParty())
	{
		parent->isProcessing = true;
		party->sendInvitation(userId, true).then([this](pplx::task<void> t)
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
			catch (std::exception& ex)
			{
				this->parent->lastError = ex.what();
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
			catch (std::exception& ex)
			{
				this->parent->lastError = ex.what();
			}

		});
}

void PartyViewModel::getMergerStatus()
{
	auto client = Stormancer::IClientFactory::GetClient(this->parent->id);
	auto merging = client->dependencyResolver().resolve<Stormancer::Party::PartyMergingApi>();
	parent->isProcessing = true;
	merging->getMergerStatus(mergerId).then([this](pplx::task<Stormancer::Party::PartyMergerStatusResponse<Stormancer::Party::EmptyMergingStatusDetails>> t)
		{

			this->parent->isProcessing = false;
			try
			{

				auto r = t.get();
				this->currentMergerAlgorithmId = r.data.algorithm;
				this->currentMergerPartiesCount = r.data.partiesCount;
				this->currentMergerPlayersCount = r.data.playersCount;
			}
			catch (std::exception& ex)
			{
				this->parent->lastError = ex.what();
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
			catch (std::exception& ex)
			{
				this->parent->lastError = ex.what();
			}

		});
}