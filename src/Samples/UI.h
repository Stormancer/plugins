#pragma once
#include "ViewModel.h"

void ShowUI(AppViewModel& vm,float deltaTime, bool& pauseTime);

void ShowMainMenu(AppViewModel& vm);

void ShowSettings(SettingsViewModel& vm);

void ShowClient(ClientViewModel& vm,float deltaTime, bool& pauseTime);