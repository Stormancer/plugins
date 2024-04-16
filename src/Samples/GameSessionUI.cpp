#include "GameSessionUI.h"
#include "imgui.h"
#include "imgui_stdlib.h"

#include "GameSessionViewModel.h"
#include "Lockstep.h"
#include "ViewModel.h"

void ShowUI(GameSessionViewModel& vm, float deltaTime)
{
	if (vm.parent->gameFinder.isGameFound())
	{
		if (ImGui::Button("Join from gamefinder"))
		{
			vm.parent->gameFinder.joinGameFound();
		}
	}

	if (vm.parent->party.isInGameSession())
	{
		if (ImGui::Button("Join from party"))
		{
			vm.parent->party.joinCurrentGameSession();
		}
	}

	if (vm.isInGameSession())
	{
		ImGui::BeginDisabled();
		ImGui::Checkbox("Is host", &vm.isHost);
		ImGui::EndDisabled();

		ImGui::Text(("Host : " + vm.hostSessionId.toString()).c_str());

		if (ImGui::Button("Set gameSession ready"))
		{
			vm.setPlayerReady();
		}

		ImGui::SeparatorText("P2P");

		for (auto& peer : vm.getP2PRemotePeers())
		{
			if (ImGui::BeginTable("peers", 2))
			{
				ImGui::TableNextRow();
				ImGui::TableNextColumn();
				ImGui::Text(peer.sessionId.c_str());
				ImGui::TableNextColumn();
				ImGui::Text(peer.isRelay ? "relay" : "direct");

				ImGui::EndTable();
			}
		}

		if (ImGui::Button("leave game session"))
		{
			vm.leaveGameSession();
		}

		if (vm.lockstep->isEnabled())
		{
			vm.lockstep->tick(deltaTime);
			ImGui::SeparatorText("Lockstep");


			if (ImGui::BeginTable("state", 2))
			{
				ImGui::TableNextRow();
				ImGui::TableNextColumn();
				ImGui::Text("time");
				ImGui::TableNextColumn();
				ImGui::Text(std::to_string(vm.lockstep->getLockstepTime()).c_str());
				ImGui::TableNextRow();
				ImGui::TableNextColumn();
				ImGui::Text("state");
				ImGui::TableNextColumn();
				ImGui::Text(vm.lockstep->currentState.c_str());
				ImGui::EndTable();


				if (ImGui::Button("Push command"))
				{
					vm.lockstep->addCommand('A');
				}
			}
		}
	}

	


}