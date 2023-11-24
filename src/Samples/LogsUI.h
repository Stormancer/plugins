#pragma once
#include "stormancer/Logger/ILogger.h"
#include "imgui.h"

struct LogsComponent
{
    ImGuiTextBuffer     Buf;
    ImGuiTextFilter     Filter;
    ImVector<int>       LineOffsets; // Index to lines offset. We maintain this with AddLog() calls.
    bool                AutoScroll;  // Keep scrolling if already at the bottom.

    LogsComponent();

    void    Clear();

    void    AddLog(const  std::string& level, const  std::string& category, const   std::string& msg, const  std::string& data);

    void    Draw(const char* title, bool* p_open = nullptr);
};

class Logger : public Stormancer::ILogger
{
public:
    Logger(LogsComponent* component);
    void log(Stormancer::LogLevel level, const std::string& category, const std::string& message, const std::string& data = "") override;

private:
    LogsComponent* _component;
};