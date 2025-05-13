#pragma once
#include <string>
#include <vector>
#include <memory>

#include "PartyViewModel.h"
#include "GameSessionViewModel.h"
#include "GameFinderViewModel.h"

#include "LogsUI.h"

class AppViewModel;

#if defined(ENABLE_EPIC)
class EpicSettingsviewModel
{
public:
	bool enabled = false;
	std::string productName = "Sample-cpp-Epic";
	std::string productVersion = "0.1";
	std::string loginMode = "DevAuth";
	std::string devAuthHost = "localhost:8888";
	std::string devAuthCredentialsName = "MyName";
	std::string productId = "0123456789abcdef0123456789abcdef";
	std::string sandboxId = "0123456789abcdef0123456789abcdef";
	std::string deploymentId = "0123456789abcdef0123456789abcdef";
	std::string clientId = "ZWirRLAaSjGsO3aCNbokY05JgPou53fO";
	std::string clientSecret = "NXtiEGDaQY769e9Ms5uF1X8s/TN6IEWn0fhsETfUEx0";
};
#endif

class SettingsViewModel
{
public:
	SettingsViewModel(AppViewModel* parent);

	std::string endpoint;
	std::string account;
	std::string application;

	std::string gameVersion;

	std::string gameFinderName;

#if defined(ENABLE_EPIC)
	EpicSettingsviewModel epicSettings;
#endif

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
	std::shared_ptr<Stormancer::MainThreadActionDispatcher> actionDispatcher = std::make_shared<Stormancer::MainThreadActionDispatcher>();


	int nextClientId = 0;
	std::vector<std::shared_ptr<ClientViewModel>> clients;


	bool addClientCmd = false;


	void tick();

	

private:
	
	
	void addClient();
};



