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

	std::string gameFinderName;
	
	void load();
	void save();

	AppViewModel* parent;
};


class ClientViewModel
{
public:
	ClientViewModel(int id, AppViewModel* parent);
	ClientViewModel(ClientViewModel& v) = delete;
	~ClientViewModel();

	int id = 0;

	bool isProcessing = false;

	std::string lastError;

	bool running = true;

	std::string deviceIdentifier;

	std::string authenticationProvider = "ephemeral";
	std::vector<std::string> authenticationProviders;
	
	std::string getServerApp();

	float deltaTime = 1.0f/60;
	//AUTH
	void connect();

	void disconnect();
	AppViewModel* parent;
	PartyViewModel party;
	GameSessionViewModel gameSession;
	GameFinderViewModel gameFinder;

	bool showLogsWindow = false;
	LogsComponent logs;

	const char* getConnectionStatus() const;
	std::string getSessionId() const;

	
private:
	std::shared_ptr<Logger> _logger;

	
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



