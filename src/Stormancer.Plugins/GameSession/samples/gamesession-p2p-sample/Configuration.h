#pragma once

#include "stormancer/IClient.h"
#include "stormancer/IActionDispatcher.h"

extern std::shared_ptr<Stormancer::MainThreadActionDispatcher> dispatcher;
extern std::shared_ptr<Stormancer::ILogger> logger;

std::shared_ptr<Stormancer::IClient> getClient();