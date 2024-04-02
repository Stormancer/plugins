#pragma once
#include <string>
#include "Party/Party.hpp"
class ClientViewModel;

class PartyViewModel
{
public:
	PartyViewModel(ClientViewModel* parent);
	PartyViewModel(PartyViewModel& value) = delete;

	void createParty();
	void leaveParty();
	void createInvitationCode();
	void joinByInvitationCode();

	std::string invitationCode;
	std::string gameFinderName;

	void updatePartyState(Stormancer::Party::PartyUserStatus newStatus);

	void joinCurrentGameSession();
	bool isInGameSession();

	std::string mergerId;
	int currentMergerPartiesCount;
	int currentMergerPlayersCount;
	std::string currentMergerAlgorithmId;
	void startMerging();
	void stopMerging();

	void getMergerStatus();



	ClientViewModel* parent;
};