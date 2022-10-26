Invitation codes
----------------

Customizing invitation codes:

```
{
	"party":{
		"authorizedInvitationCodeCharacters":"01234567890" // defaults to "23456789ABCDEFGHJKLMNPQRSTUVWXYZ"
		"invitationCodeLength" : 4 //defaults to 6
	}
}
```

Joining the gamesession your party is in
----------------------------------------

When joining a party, it's possible to know if it is currently in a gamession, and join this gamesession. 
If not used by the application, this behavior can be disabled by setting to application configuration value `party.enableGameSessionPartyStatus` to false.