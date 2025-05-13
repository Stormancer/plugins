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
#include "users/auth_ephemeral.hpp"
#include "users/auth_deviceIdentifier.hpp"

#if defined(ENABLE_STEAM)
#include "steam/Steam.hpp"
#endif

#if defined(ENABLE_EPIC)
#include "Epic/Epic.hpp"
#endif

#include "gamesession/P2PMesh.hpp"
#include "replication/Lockstep.hpp"
#include <filesystem>
class DeviceIdentifier : public Stormancer::Users::Auth::IDeviceIdentifier
{
public:

	DeviceIdentifier(std::filesystem::path path)
	{
		wchar_t buffer[1024];
		wchar_t* filenamePart;
		GetFullPathName(path.wstring().c_str(), 1024, buffer, &filenamePart);
		_handle = CreateFile(path.wstring().c_str(), GENERIC_WRITE, 0, 0, CREATE_ALWAYS, FILE_ATTRIBUTE_NORMAL, NULL);

		_path = path;
	}
	bool isValid()
	{
		return _handle != INVALID_HANDLE_VALUE;
	}
	std::string get() override
	{

		return _path.filename().string();
	}

	virtual ~DeviceIdentifier()
	{
		if (_handle)
		{
			CloseHandle(_handle);
			std::filesystem::remove(_path);
			_handle = nullptr;
		}
	}
private:
	std::filesystem::path _path;
	HANDLE  _handle = 0;
};
class DeviceIdentifierProvider : public Stormancer::Users::Auth::IDeviceIdentifierProvider
{
	Stormancer::Users::Auth::IDeviceIdentifier* capture() override
	{
		auto path = std::filesystem::path("identifiers");
		std::filesystem::create_directory(path);
		for (int i = 0; i < 1000; i++)
		{
			auto identifier = new DeviceIdentifier(path / std::to_string(i));
			if (identifier->isValid())
			{
				return identifier;
			}
		}

		throw std::runtime_error("failed to create identifier.");
	}
};

/// <summary>
/// Sample plugin that adds an implementation for the contract Stormancer::Users::Auth::IDeviceIdentifierProvider.
/// </summary>
class SamplePlugin : public Stormancer::IPlugin
{
public:

	static constexpr const char* PLUGIN_NAME = "Environment";
	static constexpr const char* PLUGIN_VERSION = "1.0.0";

	Stormancer::PluginDescription getDescription() override
	{
		return Stormancer::PluginDescription(PLUGIN_NAME, PLUGIN_VERSION);
	}
	void registerClientDependencies(Stormancer::ContainerBuilder& clientBuilder)
	{
		clientBuilder.registerDependency<DeviceIdentifierProvider>().as<Stormancer::Users::Auth::IDeviceIdentifierProvider>();
	}
};

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

		if (data.find("gameFinderName") != data.end())
		{
			gameFinderName = data["gameFinderName"].get<std::string>();
		}

#if defined(ENABLE_EPIC)
		if (data.find("epicSettings") != data.end())
		{
			auto epicSettings = data["epicSettings"];
			if (epicSettings.find("enabled") != epicSettings.end())
			{
				this->epicSettings.enabled = epicSettings["enabled"].get<bool>();
			}
			if (epicSettings.find("productName") != epicSettings.end())
			{
				this->epicSettings.productName = epicSettings["productName"].get<std::string>();
			}
			if (epicSettings.find("productVersion") != epicSettings.end())
			{
				this->epicSettings.productVersion = epicSettings["productVersion"].get<std::string>();
			}
			if (epicSettings.find("loginMode") != epicSettings.end())
			{
				this->epicSettings.loginMode = epicSettings["loginMode"].get<std::string>();
			}
			if (epicSettings.find("devAuthHost") != epicSettings.end())
			{
				this->epicSettings.devAuthHost = epicSettings["devAuthHost"].get<std::string>();
			}
			if (epicSettings.find("devAuthCredentialsName") != epicSettings.end())
			{
				this->epicSettings.devAuthCredentialsName = epicSettings["devAuthCredentialsName"].get<std::string>();
			}
			if (epicSettings.find("productId") != epicSettings.end())
			{
				this->epicSettings.productId = epicSettings["productId"].get<std::string>();
			}
			if (epicSettings.find("sandboxId") != epicSettings.end())
			{
				this->epicSettings.sandboxId = epicSettings["sandboxId"].get<std::string>();
			}
			if (epicSettings.find("deploymentId") != epicSettings.end())
			{
				this->epicSettings.deploymentId = epicSettings["deploymentId"].get<std::string>();
			}
			if (epicSettings.find("clientId") != epicSettings.end())
			{
				this->epicSettings.clientId = epicSettings["clientId"].get<std::string>();
			}
			if (epicSettings.find("clientSecret") != epicSettings.end())
			{
				this->epicSettings.clientSecret = epicSettings["clientSecret"].get<std::string>();
			}
		}
#endif
	}

}

void SettingsViewModel::save()
{
	json j = {
		{"endpoint",endpoint},
		{"account",account},
		{"application",application},
		{"gameVersion",gameVersion},
		{"gameFinderName",gameFinderName}
#if defined(ENABLE_EPIC)
		,
		{"epicSettings",{
			{"enabled", epicSettings.enabled},
			{"productName", epicSettings.productName},
			{"productVersion",epicSettings.productVersion},
			{"loginMode",epicSettings.loginMode},
			{"devAuthHost",epicSettings.devAuthHost},
			{"devAuthCredentialsName",epicSettings.devAuthCredentialsName},
			{"productId",epicSettings.productId},
			{"sandboxId",epicSettings.sandboxId},
			{"deploymentId",epicSettings.deploymentId},
			{"clientId",epicSettings.clientId},
			{"clientSecret",epicSettings.clientSecret}
			}
		}
#endif		
	};

	std::ofstream o("settings.json");
	o << j << std::endl;
}






void AppViewModel::tick()
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

	actionDispatcher->update(std::chrono::milliseconds(10));
}

void AppViewModel::addClient()
{
	clients.push_back(std::make_shared<ClientViewModel>(nextClientId++, this));
}

ClientViewModel::ClientViewModel(int id, AppViewModel* parent)
	: id(id)
	, parent(parent)
	, deviceIdentifier("client-" + std::to_string(id))
	, party(this)
	, gameSession(this)
	, gameFinder(this)
{
	Stormancer::IClientFactory::SetConfig(id, [this](size_t configId)
		{
			auto config = Stormancer::Configuration::create(this->parent->settings.account, this->parent->settings.application);

			config->addServerEndpoint(this->parent->settings.endpoint);
			_logger = std::make_shared<Logger>(&(this->logs));
			config->logger = _logger;
			config->addPlugin(new Stormancer::Users::UsersPlugin());
			config->addPlugin(new Stormancer::Party::PartyPlugin());
			config->addPlugin(new Stormancer::GameFinder::GameFinderPlugin());
			config->addPlugin(new Stormancer::GameSessions::GameSessionsPlugin());
			config->addPlugin(new Stormancer::GameVersion::GameVersionPlugin());
			config->addPlugin(new Stormancer::Party::PartyMergingPlugin());
			config->addPlugin(new Stormancer::Gameplay::LockstepPlugin());
			config->addPlugin(new Stormancer::P2PMeshPlugin());
			config->addPlugin(new Stormancer::Users::Auth::EphemeralPlugin());
			config->addPlugin(new Stormancer::Users::Auth::AuthDeviceIdentifierPlugin());
			config->addPlugin< SamplePlugin>();

#if defined(ENABLE_STEAM)
			config->addPlugin(new Stormancer::Epic::EpicPlugin());
#endif

#if defined(ENABLE_EPIC)
			auto epicSettings = this->parent->settings.epicSettings;
			config->addPlugin(new Stormancer::Epic::EpicPlugin());
			config->additionalParameters[Stormancer::Epic::ConfigurationKeys::InitPlatform] = epicSettings.enabled ? "true" : "false";
			config->additionalParameters[Stormancer::Epic::ConfigurationKeys::AuthenticationEnabled] = epicSettings.enabled ? "true" : "false";
			config->additionalParameters[Stormancer::Epic::ConfigurationKeys::ProductName] = epicSettings.productName;
			config->additionalParameters[Stormancer::Epic::ConfigurationKeys::ProductVersion] = epicSettings.productVersion;
			config->additionalParameters[Stormancer::Epic::ConfigurationKeys::LoginMode] = epicSettings.loginMode;
			config->additionalParameters[Stormancer::Epic::ConfigurationKeys::DevAuthHost] = epicSettings.devAuthHost;
			config->additionalParameters[Stormancer::Epic::ConfigurationKeys::DevAuthCredentialsName] = epicSettings.devAuthCredentialsName;
			config->additionalParameters[Stormancer::Epic::ConfigurationKeys::ProductId] = epicSettings.productId;
			config->additionalParameters[Stormancer::Epic::ConfigurationKeys::SandboxId] = epicSettings.sandboxId;
			config->additionalParameters[Stormancer::Epic::ConfigurationKeys::DeploymentId] = epicSettings.deploymentId;
			config->additionalParameters[Stormancer::Epic::ConfigurationKeys::ClientId] = epicSettings.clientId;
			config->additionalParameters[Stormancer::Epic::ConfigurationKeys::ClientSecret] = epicSettings.clientSecret;
			config->additionalParameters[Stormancer::Epic::ConfigurationKeys::Diagnostics] = "true";
#endif

			config->additionalParameters[Stormancer::GameVersion::ConfigurationKeys::ClientVersion] = this->parent->settings.gameVersion;

			config->actionDispatcher = this->parent->actionDispatcher;

			return config;
		});

	auto client = Stormancer::IClientFactory::GetClient(id);
	using namespace std::chrono_literals;
	client->setServerTimeout(60s);
	auto users = client->dependencyResolver().resolve<Stormancer::Users::UsersApi>();

	auto lockstepOptions = client->dependencyResolver().resolve<Stormancer::Gameplay::LockstepOptions>();
	lockstepOptions->FixedDeltaTimeSeconds = deltaTime;

	authenticationProviders = users->getAuthenticationProviders();

	gameFinder.initialize();
	gameSession.initialize();
}

ClientViewModel::~ClientViewModel()
{
	if (_logger)
	{
		_logger->disable();
	}
	Stormancer::IClientFactory::ReleaseClient(id);

}


std::string ClientViewModel::getServerApp()
{
	return this->parent->settings.endpoint + "/" + this->parent->settings.account + "/" + this->parent->settings.application;
}

void ClientViewModel::connect()
{
	auto client = Stormancer::IClientFactory::GetClient(id);

	isProcessing = true;
	auto users = client->dependencyResolver().resolve<Stormancer::Users::UsersApi>();
	users->authProvider = authenticationProvider;
	users->login().then([this](pplx::task<void> t)
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
	client->dependencyResolver().resolve<Stormancer::Users::UsersApi>()->logout().then([this](pplx::task<void> t)
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

	return client->sessionId().toString();

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