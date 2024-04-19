#include "UI.h"
#include "imgui.h"
#include "imgui_stdlib.h"
#include "ViewModel.h"

#include "PartyUI.h"
#include "GameFinderUI.h"
#include "GameSessionUI.h"

void ShowUI(AppViewModel& vm)
{

	ShowMainMenu(vm);

	if (vm.showSettingsWindow)
	{
		ShowSettings(vm.settings);
	}

	// 1. Show the big demo window (Most of the sample code is in ImGui::ShowDemoWindow()! You can browse its code to learn more about Dear ImGui!).
	if (vm.showDemoWindow)
	{
		::ImGui::ShowDemoWindow(&vm.showDemoWindow);
	}

	for (auto clientVm : vm.clients)
	{
		ShowClient(*clientVm);
	}
}

void ShowMainMenu(AppViewModel& vm)
{

	ImGui::BeginMainMenuBar();
	if (ImGui::BeginMenu("Clients"))
	{
		ImGui::MenuItem("Add", nullptr, &vm.addClientCmd);
		ImGui::EndMenu();
	}
	if (ImGui::BeginMenu("Windows"))
	{
		ImGui::MenuItem("Settings", nullptr, &vm.showSettingsWindow);
		ImGui::MenuItem("Imgui Demo", nullptr, &vm.showDemoWindow);
		ImGui::EndMenu();
	}
	ImGui::EndMainMenuBar();


}

void ShowSettings(SettingsViewModel& vm)
{

	ImGui::Begin("Settings", &vm.parent->showSettingsWindow);

	ImGui::InputTextWithHint("Endpoint", "Server endpoint.", &vm.endpoint);
	ImGui::InputTextWithHint("Account", "Application's account.", &vm.account);
	ImGui::InputTextWithHint("Application", "Application's name.", &vm.application);
	ImGui::InputTextWithHint("Game version", "game version.", &vm.gameVersion);

	ImGui::End();
}

void ShowClient(ClientViewModel& vm)
{
	

	auto title = "Client " + std::to_string(vm.id);
	ImGui::Begin(title.c_str(), &vm.running);

	ImGui::Text(vm.getServerApp().c_str());

	ImGui::Text(vm.getConnectionStatus());
	ImGui::Text(vm.getSessionId().c_str());
	if (ImGui::Button("Show logs"))
	{
		vm.showLogsWindow = true;
	}

	if (vm.showLogsWindow)
	{
		vm.logs.Draw(("logs " + title).c_str(), &vm.showLogsWindow);
	}

	bool processing = vm.isProcessing;
	if (processing)
	{
		ImGui::BeginDisabled();
	}
	if (ImGui::CollapsingHeader("Connection", ImGuiTreeNodeFlags_None))
	{
		ImGui::InputTextWithHint("User id", "Device identifier", &vm.deviceIdentifier);

		if (ImGui::Button("Connect"))
		{
			vm.connect();
		}
		if (ImGui::Button("Disconnect"))
		{
			vm.disconnect();
		}
	}
	if (ImGui::CollapsingHeader("Party", ImGuiTreeNodeFlags_None))
	{
		ShowUI(vm.party);
	}

	if (ImGui::CollapsingHeader("GameFinder", ImGuiTreeNodeFlags_None))
	{
		ShowUI(vm.gameFinder);
	}

	if (ImGui::CollapsingHeader("GameSession", ImGuiTreeNodeFlags_None))
	{
		float deltaTime = vm.deltaTime;
		float nextDeltaTime;
		ShowUI(vm.gameSession,deltaTime,nextDeltaTime);
		vm.deltaTime = nextDeltaTime;
	}
	if (processing)
	{
		ImGui::EndDisabled();
	}

	ImGui::End();
}