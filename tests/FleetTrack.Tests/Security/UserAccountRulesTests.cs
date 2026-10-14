using FleetTrack.Core.Security;

namespace FleetTrack.Tests.Security;

public class UserAccountRulesTests
{
    [Fact]
    public void RemovingTheOnlyAdmin_IsBlocked()
    {
        Assert.True(UserAccountRules.WouldRemoveLastAdmin(activeAdminCount: 1, targetIsActiveAdmin: true, targetRemainsActiveAdmin: false));
    }

    [Fact]
    public void RemovingOneOfTwoAdmins_IsAllowed()
    {
        Assert.False(UserAccountRules.WouldRemoveLastAdmin(activeAdminCount: 2, targetIsActiveAdmin: true, targetRemainsActiveAdmin: false));
    }

    [Fact]
    public void ChangingAStaffUser_IsAlwaysAllowed()
    {
        Assert.False(UserAccountRules.WouldRemoveLastAdmin(activeAdminCount: 1, targetIsActiveAdmin: false, targetRemainsActiveAdmin: false));
    }

    [Fact]
    public void EditingTheOnlyAdminWithoutRemovingAdminRights_IsAllowed()
    {
        Assert.False(UserAccountRules.WouldRemoveLastAdmin(activeAdminCount: 1, targetIsActiveAdmin: true, targetRemainsActiveAdmin: true));
    }
}
