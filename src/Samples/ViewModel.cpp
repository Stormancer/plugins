#include "ViewModel.h"
#include "json.hpp"
#include <iostream>
#include <sstream>
#include <fstream>

#include "stormancer/IClientFactory.h"
#include "stormancer/Configuration.h"

#define STORM_PLUGIN_IMPL 1

#include "users/Users.hpp"
#include "Party/Party.hpp"
#include "Party/PartyMerging.hpp"
#include "gamefinder/GameFinder.hpp"
#include "gamesession/GameSession.hpp"
#include "gameversion/GameVersion.hpp"
#include "replication/Lockstep.hpp"

using json = nlohmann::json;

SettingsViewModel::SettingsViewModel(AppViewModel* parent)
	: parent(parent)
{

}
void SettingsViewModel::load()
{

	std::ifstream input("settings.json");
	if (!input.fail())
	{

		json data = json::parse(input);

		endpoint = data["endpoint"].get<std::string>();
		account = data["account"].get<std::string>();
		application = data["application"].get<std::string>();
		if (data.find("gameVersion") != data.end())
		{
			gameVersion = data["gameVersion"].get<std::string>();
		}

	}

}

void SettingsViewModel::save()
{
	json j = {
		{"endpoint",endpoint},
		{"account",account},
		{"application",application},
		{"gameVersion",gameVersion}
	};

	std::ofstream o("settings.json");
	o << j << std::endl;
}






void AppViewModel::process()
{
	if (addClientCmd)
	{
		addClientCmd = false;
		addClient();
	}
	
	for (auto it = clients.begin(); it != clients.end(); it++)
	{
		if (!it->get()->running)
		{
			clients.erase(it);
			break;
		}
	}
	
}

void AppViewModel::addClient()
{
	clients.push_back(std::make_shared<ClientViewModel>(nextClientId++, settings, this));
}

ClientViewModel::ClientViewModel(int id,SettingsViewModel settings, AppViewModel* parent)
	: id(id)
	,_settings(settings)
	, parent(parent)
	, deviceIdentifier("client-"+std::to_string(id))
	,party(this)
	,gameSession(this)
	,gameFinder(this)
{
	Stormancer::IClientFactory::SetConfig(id, [this](size_t configId) 
	{
		auto config = Stormancer::Configuration::create(this->_settings.endpoint,this->_settings.account, this->_settings.application);

		config->logger = std::make_shared<Logger>(&(this->logs));
		config->addPlugin(new Stormancer::Users::UsersPlugin());
		config->addPlugin(new Stormancer::Party::PartyPlugin());
		config->addPlugin(new Stormancer::GameFinder::GameFinderPlugin());
		config->addPlugin(new Stormancer::GameSessions::GameSessionsPlugin());
		config->addPlugin(new Stormancer::GameVersion::GameVersionPlugin());
		config->addPlugin(new Stormancer::Party::PartyMergingPlugin());
		config->addPlugin(new Stormancer::Gameplay::LockstepPlugin());
		config->additionalParameters[Stormancer::GameVersion::ConfigurationKeys::ClientVersion] = this->_settings.gameVersion;
		return config;
	});

	auto client = Stormancer::IClientFactory::GetClient(id);

	auto users = client->dependencyResolver().resolve<Stormancer::Users::UsersApi>();
	users->getCredentialsCallback = [this]() {
		Stormancer::Users::AuthParameters params;
		params.type = "deviceidentifier";
		params.parameters["deviceidentifier"] = this->deviceIdentifier;
		return pplx::task_from_result(params);
	};

	gameFinder.initialize();

	
}

ClientViewModel::~ClientViewModel()
{
	Stormancer::IClientFactory::ReleaseClient(id);

}


void ClientViewModel::connect()
{
	auto client = Stormancer::IClientFactory::GetClient(id);

	isProcessing = true;
	client->dependencyResolver().resolve<Stormancer::Users::UsersApi>()->login().then([this](pplx::task<void> t) 
	{
	
		this->isProcessing = false;
		try
		{
			t.get();
		}
		catch (std::exception&)
		{
			
		}

	});
}

void ClientViewModel::disconnect()
{
	auto client = Stormancer::IClientFactory::GetClient(id);

	isProcessing = true;
	client->disconnect().then([this](pplx::task<void> t)
	{

		this->isProcessing = false;
		try
		{
			t.get();
		}
		catch (std::exception&)
		{

		}

	});
}
std::string ClientViewModel::getSessionId() const
{
	auto client = Stormancer::IClientFactory::GetClient(id);

	return client->sessionId();

}
const char* ClientViewModel::getConnectionStatus() const
{
	auto client = Stormancer::IClientFactory::GetClient(id);


	auto users = client->dependencyResolver().resolve<Stormancer::Users::UsersApi>();

	switch (users->connectionState().state)
	{
	case Stormancer::Users::GameConnectionState::Disconnected:
		return "Disconnected";
	case Stormancer::Users::GameConnectionState::Authenticated:
		return "Authenticated";
	case Stormancer::Users::GameConnectionState::Connecting:
		return "Connecting";
	case Stormancer::Users::GameConnectionState::Disconnecting:
		return "Disconnecting";
	case Stormancer::Users::GameConnectionState::Authenticating:
		return "Authenticating";
	case Stormancer::Users::GameConnectionState::Reconnecting:
		return "Reconnecting";
	default:
		return "unknown";

	}
}