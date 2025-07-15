#pragma once
#include "stormancer/Subscription.h"
#include <users/Users.hpp>
#include <friends/Friends.hpp>
#include <string>

class ClientViewModel;

//struct SingleFriendViewModel
//{
//	std::vector<Stormancer::Users::UserId> userIds;
//	std::unordered_map<std::string, std::string> status; // platform -> status
//	std::vector<std::string> tags;
//	std::string customData;
//};

class FriendsViewModel
{
public:
	FriendsViewModel(ClientViewModel* parent);
	void initialize();
	Stormancer::Friends::FriendsResult getFriends();
	void AnswerFriendRequest(const Stormancer::Users::UserId& userId, bool accept);
	void UnblockUser(const Stormancer::Users::UserId& userId);
	void AddFriend(const Stormancer::Users::UserId& userId);
	void BlockUser(const Stormancer::Users::UserId& userId);
	void RemoveFriend(const Stormancer::Users::UserId& userId);
	void InvitePlayer(const Stormancer::Users::UserId& userId);

	ClientViewModel* parent;
	Stormancer::Subscription subscription;	

private:
	std::shared_ptr<Stormancer::Friends::FriendsApi> getFriendsApi();
};