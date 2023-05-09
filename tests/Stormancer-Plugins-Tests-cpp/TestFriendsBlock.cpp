#include "pch.h"

#include "stormancer/Configuration.h"

#include "Users/Users.hpp"
#include "Friends/Friends.hpp"

#include "stormancer/IActionDispatcher.h"
#include "stormancer/IClientFactory.h"
#include "stormancer/Logger/VisualStudioLogger.h"

static constexpr const char* ServerEndpoint = "http://localhost:80";//"http://gc3.stormancer.com";
constexpr char* Account = "tests";
constexpr char* Application = "test-app";

std::shared_ptr<Stormancer::ILogger> logger = std::make_shared<Stormancer::VisualStudioLogger>();

std::string userId0;
std::string userId1;

static pplx::task<void> login(int id)
{
	auto client = Stormancer::IClientFactory::GetClient(id);

	auto users = client->dependencyResolver().resolve<Stormancer::Users::UsersApi>();

	users->getCredentialsCallback = [id]()
	{
		Stormancer::Users::AuthParameters auth;
		auth.type = "deviceidentifier";
		auth.parameters.emplace("deviceidentifier", std::to_string(id));
		return pplx::task_from_result(auth);
	};

	return users->login()
		.then([id, users]()
	{
		if (id == 0)
		{
			userId0 = users->userId();
		}
		else
		{
			userId1 = users->userId();
		}
	});
}

static pplx::task<void> block(int id)
{
	auto client = Stormancer::IClientFactory::GetClient(id);

	auto friends = client->dependencyResolver().resolve<Stormancer::Friends::Friends>();

	std::string userIdToBlock = (id == 0 ? userId1 : userId0);

	return friends->block(userIdToBlock);
}

static pplx::task<void> unblock(int id)
{
	auto client = Stormancer::IClientFactory::GetClient(id);

	auto friends = client->dependencyResolver().resolve<Stormancer::Friends::Friends>();

	std::string userIdToUnblock = (id == 0 ? userId1 : userId0);

	return friends->unblock(userIdToUnblock);
}

static pplx::task<void> checkBlocked(int id)
{
	auto client = Stormancer::IClientFactory::GetClient(id);

	auto friends = client->dependencyResolver().resolve<Stormancer::Friends::Friends>();

	std::string blockedUserId = (id == 0 ? userId1 : userId0);

	return friends->getBlockedList()
		.then([blockedUserId](std::vector<std::string> blockedUserIds)
	{
		if (std::find(blockedUserIds.begin(), blockedUserIds.end(), blockedUserId) == blockedUserIds.end())
		{
			throw std::runtime_error("User not found in blocked list");
		}
	});
}

static pplx::task<void> checkUnblocked(int id)
{
	auto client = Stormancer::IClientFactory::GetClient(id);

	auto friends = client->dependencyResolver().resolve<Stormancer::Friends::Friends>();

	std::string unblockedUserId = (id == 0 ? userId1 : userId0);

	return friends->getBlockedList()
		.then([unblockedUserId](std::vector<std::string> blockedUserIds)
	{
		if (std::find(blockedUserIds.begin(), blockedUserIds.end(), unblockedUserId) != blockedUserIds.end())
		{
			throw std::runtime_error("User found in blocked list");
		}
	});
}

TEST(Metagame, TestFriendsBlock)
{
	//Create an action dispatcher to dispatch callbacks and continuation in the thread running the method.
	auto dispatcher = std::make_shared<Stormancer::MainThreadActionDispatcher>();

	//Create a configurator used for all clients.
	Stormancer::IClientFactory::SetDefaultConfigurator([dispatcher](size_t id)
	{
		//Create a configuration that connects to the test application.
		auto config = Stormancer::Configuration::create(std::string(ServerEndpoint), std::string(Account), std::string(Application));

		//Log in VS output window.
		config->logger = logger;

		//Add plugins required by the test.
		config->addPlugin(new Stormancer::Users::UsersPlugin());
		config->addPlugin(new Stormancer::Friends::FriendsPlugin());
		config->encryptionEnabled = true;

		//Use the dispatcher we created earlier to ensure all callbacks are run on the test main thread.
		config->actionDispatcher = dispatcher;
		return config;
	});

	logger->log(Stormancer::LogLevel::Info, "gameplay.test-friends", "========== LOGIN ==========", "0");

	auto task = login(0)
		.then([]()
	{
		logger->log(Stormancer::LogLevel::Info, "gameplay.test-friends", "========== LOGIN ==========", "1");
		return login(1);
	})
		.then([]()
	{
		logger->log(Stormancer::LogLevel::Info, "gameplay.test-friends", "========== BLOCK ==========", "0");
		return block(0);
	})
		.then([]()
	{
		logger->log(Stormancer::LogLevel::Info, "gameplay.test-friends", "========== CHECK BLOCKED ==========", "0");
		return checkBlocked(0);
	})
		.then([]()
	{
		logger->log(Stormancer::LogLevel::Info, "gameplay.test-friends", "========== UNBLOCK ==========", "0");
		return unblock(0);
	})
		.then([]()
	{
		logger->log(Stormancer::LogLevel::Info, "gameplay.test-friends", "========== CHECK UNBLOCKED ==========", "0");
		return checkUnblocked(0);
	})
		.then([](pplx::task<void> task)
	{
		try
		{
			task.get();
			logger->log(Stormancer::LogLevel::Info, "gameplay.test-friends", "========== FINISHED ==========", "0");
			return true;
		}
		catch (const std::exception& ex)
		{
			logger->log(Stormancer::LogLevel::Error, "gameplay.test-friends", "Test failed", ex.what());
			return false;
		}
	});

	//loop until test is completed and run library events.
	while (!task.is_done())
	{
		//Runs the callbacks and continuations waiting to be executed (mostly user code) for max 5ms.
		dispatcher->update(std::chrono::milliseconds(10));
		std::this_thread::sleep_for(std::chrono::milliseconds(10));
	}

	EXPECT_TRUE(task.get());
	//We are connected to the game session, we can test the socket API.

	Stormancer::IClientFactory::ReleaseClient(0);
	Stormancer::IClientFactory::ReleaseClient(1);
}
