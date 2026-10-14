using FleetTrack.Core.Security;

namespace FleetTrack.Tests.Security;

public class RolePermissionsTests
{
    [Fact]
    public void Admin_HasEveryPermission()
    {
        Assert.All(Enum.GetValues<Permission>(), p => Assert.True(RolePermissions.RoleHas(Roles.Admin, p), p.ToString()));
    }

    [Theory]
    [InlineData(Permission.ViewRecords)]
    [InlineData(Permission.AddServiceRecords)]
    [InlineData(Permission.ViewDocuments)]
    public void Staff_CanViewAddServicesAndViewDocuments(Permission permission)
    {
        Assert.True(RolePermissions.RoleHas(Roles.Staff, permission));
    }

    [Theory]
    [InlineData(Permission.EditRecords)]
    [InlineData(Permission.UploadDocuments)]
    [InlineData(Permission.DeleteRecords)]
    [InlineData(Permission.ManageUsers)]
    [InlineData(Permission.ViewAuditLog)]
    public void Staff_CannotEditDeleteUploadOrAdminister(Permission permission)
    {
        Assert.False(RolePermissions.RoleHas(Roles.Staff, permission));
    }

    [Fact]
    public void UnknownRole_HasNoPermissions()
    {
        Assert.All(Enum.GetValues<Permission>(), p => Assert.False(RolePermissions.RoleHas("Guest", p)));
    }

    [Fact]
    public void OnlyAdmins_CanDelete()
    {
        Assert.Equal([Roles.Admin], RolePermissions.RolesWith(Permission.DeleteRecords));
    }
}
