
# Friendlist records states

Notations:
A record is identifier by [(friendId,ownerId):Status]

Status:
    - SentInvitation : An invitation that was sent by the owner.
    - PendingInvitation  : An invitation received by the owner.
    - Accepted: An accepted invitation
    - Blocked: the owner blocked this user.
    - DeletedByFriend: the friend is invisible, because they deleted the record in their own friendlist.
 
NEVER delete (X,Y):Blocked from actions triggered by X.   
In the following algorithms, ignore any delete operation or status changes of (X,Y):Blocked.

- User X invite Y
    If (Y,X) does not exist
    - Create Record [(Y,X):SentInvitation]
    If not (X,Y):Blocked
    - Create Record [(X,Y):PendingInvitation]
    If (Y,X) exists
        If (Y,X):SentInvitation
            If (X,Y):PendingInvitation => nothing
            If (X,Y):Accepted => Accept
            If (X,Y):Blocked => nothing
            If (X,Y):SentInvitation => Accept
            If (X,Y):DeletedByFriend => Accept
        If (Y,X):PendingInvitation => Accept
        If (Y,X):Invisible
            
        else nothing
        

- Y Refuses the invitation
    - Delete Record (X,Y)
    - Keep the record (Y,X):SentInvitation, X doesn't know that Y refused the invitation.

- Y Blocks the user
    set (X,Y):Blocked
    set (Y,X):DeleteByFriend

- X deletes (Y,X):
    If (Y:X):SentInvitation
        - Deletes (Y,X) (X,Y)
    If (Y:X):PendingInvitation
        - Delete (Y,X)
    If (Y:X):Accepted
        - Delete (Y,X)
        - if not (X,Y):Blocked Set (X,Y):Deleted //We keep X in Y friendlist, but we don't notify status changes anymore
    if (Y:X):Deleted
    -  Delete (Y,X), (X,Y)
    if (Y:)


