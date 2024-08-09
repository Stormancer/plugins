# Game history

The game history plugins stores in the database the game sessions that were created during gameplay and the player who joined them. Other plugins might leverage this information either by importing the GameHistoryService class or by implementing IGameHistoryEventHandler to react to updates to the game history.

Remark: Participants are only added to game histories if they join the corresponding game sessions.