#include "FriendsUI.h"
#include "imgui.h"

void ShowUI(FriendsViewModel& vm)
{
	auto friends = vm.getFriends();

	ImVec4 const oReadyColor = friends.isReady ? ImVec4(0.0f, 1.0f, 0.0f, 1.0f) : ImVec4(1.0f, 0.0f, 0.0f, 1.0f);
	ImGui::TextColored(oReadyColor, "%s", friends.isReady ? "ready" : "not ready");

	if (friends.isReady)
	{
		for (auto& f : friends.friends)
		{
			DrawSingleFriend(vm, f);
		}
	}
}

void DrawSingleFriend(FriendsViewModel& vm, Stormancer::Friends::Friend const& f)
{
	if (ImGui::TreeNode(f.userIds.front().toString().c_str()))
	{
		if (ImGui::TreeNode("user ids"))
		{
			for (auto& userId : f.userIds)
			{
				ImGui::Text(userId.toString().c_str());
			}
			ImGui::TreePop();
		}
		std::string status;
		ImVec4 statusColor;
		switch (f.getStatusForPlatform("stormancer"))
		{
		case Stormancer::Friends::FriendStatus::Connected:
			status = "Online";
			statusColor = ImVec4(0.0f, 1.0f, 0.0f, 1.0f);
			break;
		case Stormancer::Friends::FriendStatus::Away:
			status = "Away";
			statusColor = ImVec4(1.0f, 1.0f, 0.0f, 1.0f);
			break;
		case Stormancer::Friends::FriendStatus::Disconnected:
			status = "Disconnected";
			statusColor = ImVec4(1.0f, 0.0f, 0.0f, 1.0f);
			break;
		}
		ImGui::TextUnformatted("status:");
		ImGui::SameLine();
		ImGui::TextColored(statusColor, "%s", status.c_str());

		if (std::find(f.tags.begin(), f.tags.end(), "friends.invitation.received") != f.tags.end())
		{
			if (ImGui::Button("Accept"))
			{
				vm.AnswerFriendRequest(f.userIds.front(), true);
			}

			ImGui::SameLine();
			if (ImGui::Button("Reject"))
			{
				vm.AnswerFriendRequest(f.userIds.front(), false);
			}
		}
		else if (std::find(f.tags.begin(), f.tags.end(), "friends.blocked") != f.tags.end())
		{
			if (ImGui::Button("Unblock"))
			{
				vm.UnblockUser(f.userIds.front());
			}
		}
		else if (std::find(f.tags.begin(), f.tags.end(), "recentlyMet") != f.tags.end())
		{
			if (ImGui::Button("Invite"))
			{
				vm.AddFriend(f.userIds.front());
 			}
			ImGui::SameLine();
			if (ImGui::Button("Block"))
			{
				vm.BlockUser(f.userIds.front());
			}
		}

		if (ImGui::Button("Remove"))
		{
			vm.RemoveFriend(f.userIds.front());
		}

		if (ImGui::Button("Invite to party"))
		{
			vm.InvitePlayer(f.userIds.front());
		}

		ImGui::TextUnformatted("Tags:");
		for (std::string const& tag : f.tags)
		{
			ImGui::BulletText(tag.c_str());
		}

		ImGui::Text("Custom data: %s", f.customData.c_str());


		ImGui::TreePop();
	}
}
