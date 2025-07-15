#include "FriendsViewModel.h"
#include "ViewModel.h"

#include "stormancer/IClientFactory.h"
#include "friends/Friends.hpp"

FriendsViewModel::FriendsViewModel(ClientViewModel* parent)
{
	this->parent = parent;
}

std::shared_ptr<Stormancer::Friends::FriendsApi> FriendsViewModel::getFriendsApi()
{
	auto client = Stormancer::IClientFactory::GetClient(parent->id);
	return client->dependencyResolver().resolve<Stormancer::Friends::FriendsApi>();
}

void FriendsViewModel::initialize()
{
	auto friendsApi = getFriendsApi();
	subscription = friendsApi->subscribeFriendListUpdatedEvent([this](Stormancer::Friends::FriendListUpdatedEvent evt) {
		});
}

Stormancer::Friends::FriendsResult FriendsViewModel::getFriends()
{
	auto friendsApi = getFriendsApi();
	return friendsApi->friends();
}

void FriendsViewModel::AnswerFriendRequest(const Stormancer::Users::UserId& userId, bool accept)
{
	auto friendsApi = getFriendsApi();
	if (friendsApi->isLoaded())
	{
		friendsApi->answerFriendInvitation(userId, accept)
			.then([this](pplx::task<void> t) {
			try
			{
				t.get();
			}
			catch (std::exception& ex)
			{
				this->parent->lastError = ex.what();
			}
				});
	}
}

void FriendsViewModel::UnblockUser(const Stormancer::Users::UserId& userId)
{
	auto friendsApi = getFriendsApi();
	if (friendsApi->isLoaded())
	{
		friendsApi->unblock(userId)
			.then([this](pplx::task<void> t) {
			try
			{
				t.get();
			}
			catch (std::exception& ex)
			{
				this->parent->lastError = ex.what();
			}
				});
	}
}

void FriendsViewModel::AddFriend(const Stormancer::Users::UserId& userId)
{
	auto friendsApi = getFriendsApi();
	if (friendsApi->isLoaded())
	{
		friendsApi->inviteFriend(userId)
			.then([this](pplx::task<void> t) {
			try
			{
				t.get();
			}
			catch (std::exception& ex)
			{
				this->parent->lastError = ex.what();
			}
				});
	}
}

void FriendsViewModel::BlockUser(const Stormancer::Users::UserId& userId)
{
	auto friendsApi = getFriendsApi();
	if (friendsApi->isLoaded())
	{
		friendsApi->block(userId)
			.then([this](pplx::task<void> t) {
			try
			{
				t.get();
			}
			catch (std::exception& ex)
			{
				this->parent->lastError = ex.what();
			}
				});
	}
}

void FriendsViewModel::RemoveFriend(const Stormancer::Users::UserId& userId)
{
	auto friendsApi = getFriendsApi();
	if (friendsApi->isLoaded())
	{
		friendsApi->removeFriend(userId)
			.then([this](pplx::task<void> t) {
			try
			{
				t.get();
			}
			catch (std::exception& ex)
			{
				this->parent->lastError = ex.what();
			}
				});
	}
}

void FriendsViewModel::InvitePlayer(const Stormancer::Users::UserId& userId)
{
	parent->party.InvitePlayerToParty(userId);
}
