Sometimes, players want to find other players to fill up their parties. To this effect they can use party invitations and codes, or make their party searchable through the party search API.

This plugin offers another option. Using it, parties can declare themselves as searching for other members. When doing that, the party joins a matchmaking system which will try to match the party with other parties once every second until the party leaves matchmaking or it is filled.

The matchmaking system works by offering members of one or several of the parties currently in matchmaking to leave their party and join another one, therefore merging the player parties. It can also query for searchable parties and offer players to join these if they match, on any kind of similar logic.

# Usage
## Server

# Getting the current party merging status.

The current status of the party merging system is available in the public server data of the party.

    auto party = tryGetParty();
    if (party != nullptr)
    {
        const auto& serverData = party->settings().publicServerData;
        auto it = serverData.find("stormancer.partyMerging.status");
        
        if(it != serverData.end())
        {
            std::string status = it->second; //InProgress, Completed, Cancelled or Error.
        }
    }
    else
    {
       //Not in a party
    }

Fields related to the party merging plugin in public server data are:

`stormancer.partyMerging.status` contains the status of the current or last merging operation. Possible values are:

- InProgress
- Completed
- Error
- Cancelled

`stormancer.partyMerging.merger` contains the id of the last merger used.

`stormancer.partyMerging.lastError` contains the message associated with the last error encountered while running a merger.

`stormancer.partyMerging.merged` exists if the merger has been successfully run on this party once.