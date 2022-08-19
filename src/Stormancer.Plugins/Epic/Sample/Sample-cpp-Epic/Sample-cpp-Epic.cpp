#include "Users/ClientAPI.hpp"
#include "Users/Users.hpp"
#include "Party/Party.hpp"
#include "GameFinder/GameFinder.hpp"

#include <thread>

#include "../../cpp/Epic.hpp"

using namespace Stormancer;

int main()
{
	std::shared_ptr<MainThreadActionDispatcher> actionDispatcher = std::make_shared<MainThreadActionDispatcher>();

	auto config = Configuration::create("http://192.168.1.24", "vaus", "dev-server");
	config->additionalParameters[Epic::ConfigurationKeys::AuthenticationEnabled] = "true";
	config->additionalParameters[Epic::ConfigurationKeys::LoginMode] = "DevAuth";
	config->additionalParameters[Epic::ConfigurationKeys::DevAuthHost] = "http://localhost:4567";
	config->additionalParameters[Epic::ConfigurationKeys::DevAuthCredentialsName] = "antlafarge";
	config->additionalParameters[Epic::ConfigurationKeys::ProductId] = "973f4aae211b472c9cb766f47d88d8d0";
	config->additionalParameters[Epic::ConfigurationKeys::SandboxId] = "p-ep8zk3gxkagt2vqghdchdsdv4ql2uy";
	config->additionalParameters[Epic::ConfigurationKeys::DeploymentId] = "3c1b33a8937a4be0ac46d700a64d4e25";
	config->additionalParameters[Epic::ConfigurationKeys::ClientId] = "xyza7891JkSPExgoA1rutsjWjpmqkbjD";
	config->additionalParameters[Epic::ConfigurationKeys::ClientSecret] = "6hsLV6Ftg5tutmRQ8t/ujS7RxcNkngT+yeI75JmtmuU";
	config->actionDispatcher = actionDispatcher;
	config->addPlugin(new Users::UsersPlugin());
	config->addPlugin(new GameFinder::GameFinderPlugin());
	config->addPlugin(new Party::PartyPlugin());
	config->addPlugin(new Epic::EpicPlugin());
	auto client = IClient::create(config);
	auto usersApi = client->dependencyResolver().resolve<Users::UsersApi>();
	auto logger = client->dependencyResolver().resolve<ILogger>();

	bool disconnected = false;

	std::thread mainLoop([actionDispatcher, &disconnected]()
	{
		while (!disconnected)
		{
			actionDispatcher->update(std::chrono::milliseconds(10));
		}
	});

	std::thread thread([usersApi, logger, &disconnected, client]()
	{
		usersApi->login().get();
		std::string userId = usersApi->userId();
		std::string username = usersApi->username();
		logger->log(LogLevel::Info, "SampleMain", "Login succeed!", "userId = " + userId + "; userName = " + username);
		usersApi->logout()
			.then([client, &disconnected]()
		{
			client->disconnect()
				.then([&disconnected]()
			{
				disconnected = true;
			});
		});
	});

	thread.join();
	mainLoop.join();

	return 0;
}
