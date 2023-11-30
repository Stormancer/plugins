#include "PartyUI.h"
#include "imgui.h"
#include "imgui_stdlib.h"
#include "Party/Party.hpp"
#include "Party/PartyMerging.hpp"
#include "gamefinder/GameFinder.hpp"
#include "stormancer/IClientFactory.h"
#include "ViewModel.h"
#include "GameFinderUI.h"


void ShowUI(PartyViewModel& vm)
{
	auto client = Stormancer::IClientFactory::GetClient(vm.parent->id);


	auto party = client->dependencyResolver().resolve<Stormancer::Party::PartyApi>();

	if (party->isInParty())
	{
		if (ImGui::TreeNode(party->getPartyId().id.c_str()))
		{

			if (ImGui::TreeNode(("Leader:" + party->getPartyLeaderId()).c_str()))
			{
				ImGui::TreePop();
			}
			if (ImGui::TreeNode("Settings"))
			{
				auto settings = party->getPartySettings();
				ImGui::Text(("Game finder : " + settings.gameFinderName).c_str());
				ImGui::Text(("Custom data : " + settings.customData).c_str());
				ImGui::Text(("Indexed document : " + settings.indexedDocument).c_str());

				if (ImGui::TreeNode("Public server data"))
				{
					if (ImGui::BeginTable("public server data", 2))
					{
						ImGui::TableNextRow();
						for (auto& kvp : settings.publicServerData)
						{
							ImGui::TableNextColumn();
							ImGui::Text(kvp.first.c_str());
							ImGui::TableNextColumn();
							ImGui::Text(kvp.second.c_str());
						}
						ImGui::EndTable();
					}


					ImGui::TreePop();
				}
				ImGui::TreePop();
			}
			if (ImGui::TreeNode("Local member"))
			{
				auto member = party->getLocalMember();

				if (ImGui::BeginTable(member.userId.c_str(), 2))
				{
					ImGui::TableNextRow();
					ImGui::TableNextColumn();
					ImGui::Text("user id");
					ImGui::TableNextColumn();
					ImGui::Text(member.userId.c_str());

					ImGui::TableNextRow();
					ImGui::TableNextColumn();
					ImGui::Text("session id");
					ImGui::TableNextColumn();
					ImGui::Text(member.sessionId.toString().c_str());

					ImGui::TableNextRow();
					ImGui::TableNextColumn();
					ImGui::Text("user data length");
					ImGui::TableNextColumn();
					ImGui::Text(std::to_string(member.userData.size()).c_str());

					ImGui::EndTable();
				}
				ImGui::TreePop();
			}
			if (ImGui::TreeNode("Members"))
			{
				for (auto& member : party->getPartyMembers())
				{
					if (ImGui::BeginTable(member.userId.c_str(), 2))
					{
						ImGui::TableNextRow();
						ImGui::TableNextColumn();
						ImGui::Text("user id");
						ImGui::TableNextColumn();
						ImGui::Text(member.userId.c_str());

						ImGui::TableNextRow();
						ImGui::TableNextColumn();
						ImGui::Text("session id");
						ImGui::TableNextColumn();
						ImGui::Text(member.sessionId.toString().c_str());

						ImGui::TableNextRow();
						ImGui::TableNextColumn();
						ImGui::Text("user data length");
						ImGui::TableNextColumn();
						ImGui::Text(std::to_string(member.userData.size()).c_str());

						ImGui::EndTable();
					}
				}
				ImGui::TreePop();
			}

			ImGui::TreePop();
		}
	}
	ImGui::InputText("Gamefinder name", &vm.gameFinderName);

	if (ImGui::Button("Create party"))
	{
		vm.createParty();
	}


	ImGui::InputText("Invitation code", &vm.invitationCode);
	if (ImGui::Button("Join by invitation code"))
	{
		vm.joinByInvitationCode();
	}

	if (party->isInParty())
	{
		if (ImGui::Button("Create invitation code"))
		{
			vm.createInvitationCode();
		}

		if (ImGui::Button("Leave party"))
		{
			vm.leaveParty();
		}

		ImGui::SeparatorText("GAME FINDING");

		if (party->getLocalMember().partyUserStatus == Stormancer::Party::PartyUserStatus::Ready)
		{
			ImGui::Text("Player ready");
			if (ImGui::Button("Cancel ready"))
			{
				vm.updatePartyState(Stormancer::Party::PartyUserStatus::NotReady);
			}
		}
		else
		{
			ImGui::Text("Player not ready");
			if (ImGui::Button("Set ready"))
			{
				vm.updatePartyState(Stormancer::Party::PartyUserStatus::Ready);
			}

		}

		ImGui::SeparatorText("PARTY MERGING");

		ImGui::InputText("Merger name", &vm.mergerId);
		auto merger = client->dependencyResolver().resolve<Stormancer::Party::PartyMergingApi>();

		if (ImGui::Button("Start merging"))
		{
			vm.startMerging();
		}
		if (ImGui::Button("Stop merging"))
		{
			vm.stopMerging();
		}
	
		if (ImGui::BeginTable("mergingState",2))
		{
			auto state = merger->getStatus();
			ImGui::TableNextRow();
			ImGui::TableNextColumn();
			ImGui::Text("merger id");
			ImGui::TableNextColumn();
			ImGui::Text(state.mergerId.c_str());

			ImGui::TableNextRow();
			ImGui::TableNextColumn();
			ImGui::Text("status");
			ImGui::TableNextColumn();
			ImGui::Text(std::to_string((int)state.status).c_str());

			ImGui::TableNextRow();
			ImGui::TableNextColumn();
			ImGui::Text("last error");
			ImGui::TableNextColumn();
			ImGui::Text(state.lastError.c_str());

			ImGui::EndTable();
		}

	

	}
	if (party->isInGameSession())
	{
		ImGui::Text("Party in game session ");
		if (ImGui::Button("Join current game session"))
		{
			vm.joinCurrentGameSession();
		}
	}


}
