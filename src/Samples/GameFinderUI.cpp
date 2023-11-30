#include "GameFinderUI.h"

#include "imgui.h"
#include "imgui_stdlib.h"

#include "ViewModel.h"

#include "stormancer/IClientFactory.h"
#include "gamefinder/GameFinder.hpp"


std::string to_string(Stormancer::GameFinder::GameFinderStatus status)
{
	using namespace Stormancer::GameFinder;

	switch (status)
	{
	case GameFinderStatus::Idle: return "idle";
	case GameFinderStatus::Searching: return "searching";
	case GameFinderStatus::CandidateFound: return "candidateFound";
	case GameFinderStatus::WaitingPlayersReady: return "waitingPlayerReady";
	case GameFinderStatus::Success: return "success";
	case GameFinderStatus::Failed: return "failed";
	case GameFinderStatus::Canceled: return "canceled";
	case GameFinderStatus::Loading: return "loading";
	default: return "<unknown>";
	}
}

void ShowUI(GameFinderViewModel& vm)
{
	if (ImGui::BeginTable("gameFinderState", 2))
	{
		auto client = Stormancer::IClientFactory::GetClient(vm.parent->id);


		auto gamefinder = client->dependencyResolver().resolve<Stormancer::GameFinder::GameFinderApi>();

		for (auto kvp : gamefinder->getPendingFindGameStatus())
		{
			ImGui::TableNextRow();
			ImGui::TableNextColumn();
			ImGui::Text(kvp.first.c_str());
			ImGui::TableNextColumn();
			ImGui::Text(to_string(kvp.second.status).c_str());

		}

		ImGui::EndTable();
	}
}

