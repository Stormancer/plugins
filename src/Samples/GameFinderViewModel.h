#pragma once
#include <string>
#include "stormancer/Subscription.h"

class ClientViewModel;

class GameFinderViewModel
{
public:
	GameFinderViewModel(ClientViewModel* parent);

	void initialize();

	void joinGameFound();

	bool isGameFound();

	ClientViewModel* parent;

	std::string lastConnectionToken;

	Stormancer::Subscription subscription;
};