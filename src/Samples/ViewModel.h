#pragma once
#include <string>
#include <vector>
#include <memory>

#include "PartyViewModel.h"
#include "GameSessionViewModel.h"
#include "GameFinderViewModel.h"

#include "LogsUI.h"

class AppViewModel;

class SettingsViewModel
{
public:
	SettingsViewModel(AppViewModel* parent);

	std::string endpoint;
	std::string account;
	std::string application;

	std::string gameVersion;
	
	void load();
	void save();

	AppViewModel* parent;
};


class ClientViewModel
{
public:
	ClientViewModel(int id, SettingsViewModel settings, AppViewModel* parent);
	ClientViewModel(ClientViewModel& v) = delete;
	~ClientViewModel();

	int id = 0;

	bool isProcessing = false;

	std::string lastError;

	bool running = true;

	std::string deviceIdentifier;
	
	std::string getServerApp()
	{
		return _settings.endpoint + "/" + _settings.account + "/" + _settings.application;
	}

	float deltaTime = 0.016f;
	//AUTH
	void connect();

	void disconnect();

	PartyViewModel party;
	GameSessionViewModel gameSession;
	GameFinderViewModel gameFinder;

	bool showLogsWindow = false;
	LogsComponent logs;

	const char* getConnectionStatus() const;
	std::string getSessionId() const;

	AppViewModel* parent;

private:
	SettingsViewModel _settings;

	
};

class AppViewModel
{
public:
	AppViewModel() 
		:settings(SettingsViewModel(this))
	{}

	

	bool showSettingsWindow = false;
	bool showDemoWindow = false;
	SettingsViewModel settings;


	int nextClientId = 0;
	std::vector<std::shared_ptr<ClientViewModel>> clients;


	bool addClientCmd = false;


	void process();

	

private:
	
	
	void addClient();
};



