#pragma once
#include "FriendsViewModel.h"

void ShowUI(FriendsViewModel& vm);

void DrawSingleFriend(FriendsViewModel& vm, Stormancer::Friends::Friend const& f);

void DrawSingleFriend(Stormancer::Friends::Friend const& f);