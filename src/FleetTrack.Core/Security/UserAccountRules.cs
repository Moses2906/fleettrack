namespace FleetTrack.Core.Security;

public static class UserAccountRules
{
    /// <summary>
    /// True when a change would leave no active admin: nobody could then manage users or
    /// delete records, and the only fix would be editing the database by hand.
    /// </summary>
    /// <param name="activeAdminCount">Active admins right now, including the target user if they are one.</param>
    /// <param name="targetIsActiveAdmin">Whether the user being changed is currently an active admin.</param>
    /// <param name="targetRemainsActiveAdmin">Whether they will still be an active admin after the change.</param>
    public static bool WouldRemoveLastAdmin(int activeAdminCount, bool targetIsActiveAdmin, bool targetRemainsActiveAdmin) =>
        targetIsActiveAdmin && !targetRemainsActiveAdmin && activeAdminCount <= 1;
}
