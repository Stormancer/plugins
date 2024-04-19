#include "GameSessionUI.h"
#include "imgui.h"
#include "imgui_stdlib.h"

#include "GameSessionViewModel.h"
#include "Lockstep.h"
#include "ViewModel.h"

void ShowUI(GameSessionViewModel& vm, float deltaTime,float& nextDeltaTime)
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
			if (vm.lockstep->tick(deltaTime))
			{
				nextDeltaTime = 0.016f;
			}
			else
			{
				nextDeltaTime = 0.0f;
			}

			ImGui::SeparatorText("Lockstep");

			if (vm.lockstep->isPaused())
			{
				if (ImGui::Button("Unpause"))
				{
					vm.lockstep->Pause(false);
				}
			}
			else
			{
				if (ImGui::Button("Pause"))
				{
					vm.lockstep->Pause(true);
				}
			}
			if (ImGui::BeginTable("state", 2))
			{
				ImGui::TableNextRow();
				ImGui::TableNextColumn();
				ImGui::Text("time");
				ImGui::TableNextColumn();
				ImGui::Text(std::to_string(vm.lockstep->getLockstepTime()).c_str());
				ImGui::TableNextRow();
				ImGui::TableNextColumn();
				ImGui::Text("target time");
				ImGui::TableNextColumn();
				ImGui::Text(std::to_string(vm.lockstep->getTargetTime()).c_str());
				ImGui::TableNextRow();
				ImGui::TableNextColumn();
				ImGui::Text("state");
				ImGui::TableNextColumn();
				ImGui::Text(vm.lockstep->currentState.c_str());
				ImGui::EndTable();

				for (auto& player : vm.lockstep->getPlayers())
				{
					if (ImGui::BeginTable("players", 2))
					{
						ImGui::TableNextRow();
						ImGui::TableNextColumn();
						ImGui::Text("Player id");
						ImGui::TableNextColumn();
						ImGui::Text(std::to_string(player.playerId).c_str());

						ImGui::TableNextRow();
						ImGui::TableNextColumn();
						ImGui::Text("Session id");
						ImGui::TableNextColumn();
						ImGui::Text(player.sessionId.toString().c_str());

						ImGui::TableNextRow();
						ImGui::TableNextColumn();
						ImGui::Text("is local");
						ImGui::TableNextColumn();
						ImGui::Text(player.localPlayer ? "true":"false");

						ImGui::TableNextRow();
						ImGui::TableNextColumn();
						ImGui::Text("Latency");
						ImGui::TableNextColumn();
						ImGui::Text(std::to_string(player.latencyMs).c_str());
						
						ImGui::TableNextRow();
						ImGui::TableNextColumn();
						ImGui::Text("last Command id");
						ImGui::TableNextColumn();
						ImGui::Text(std::to_string(player.lastCommandId).c_str());

						ImGui::TableNextRow();
						ImGui::TableNextColumn();
						ImGui::Text("Synchronized until");
						ImGui::TableNextColumn();
						ImGui::Text(std::to_string(player.synchronizedUntilMs).c_str());
						ImGui::EndTable();
					}
				}

				if (ImGui::Button("Push command"))
				{
					vm.lockstep->addCommand('A');
				}
			}
		}
	}

	


}