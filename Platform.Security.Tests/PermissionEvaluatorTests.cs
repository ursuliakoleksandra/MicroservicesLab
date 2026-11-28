using System.Security.Claims;
using Platform.Security;
using Platform.Security.Authorization;

namespace Platform.Security.Tests;

public class PermissionEvaluatorTests
{
    private static ClaimsPrincipal User(params (string Type, string Value)[] claims) =>
        new(new ClaimsIdentity(claims.Select(c => new Claim(c.Type, c.Value)), "test"));

    [Fact]
    public void Local_Admin_HasEveryPermission() =>
        Assert.True(PermissionEvaluator.HasPermission(User(("role", "Admin")), Permissions.Orders.Delete));

    [Theory]
    [InlineData("orders:read", true)]
    [InlineData("products:create", true)]
    [InlineData("orders:update", false)]
    [InlineData("products:delete", false)]
    public void Local_User_CanReadAndCreateOnly(string permission, bool expected) =>
        Assert.Equal(expected, PermissionEvaluator.HasPermission(User(("role", "User")), permission));

    [Fact]
    public void NoClaims_NoPermissions() =>
        Assert.False(PermissionEvaluator.HasPermission(User(), Permissions.Products.Read));

    [Fact]
    public void ClientCredentials_ScopeMapsToPermission()
    {
        var principal = User(("scope", "orders.read products.read"), ("client_id", "aggregator_service"));
        Assert.True(PermissionEvaluator.HasPermission(principal, Permissions.Orders.Read));
        Assert.False(PermissionEvaluator.HasPermission(principal, Permissions.Orders.Create));
    }

    [Fact]
    public void Scope_DotAndColonAreEquivalent() =>
        Assert.True(PermissionEvaluator.HasScope(User(("scope", "orders.read")), "orders:read"));

    [Fact]
    public void Keycloak_UmaPermissions_AreChecked()
    {
        const string json = """{"permissions":[{"rsname":"orders-api","scopes":["orders:read","orders:create"]}]}""";
        var principal = User(("authorization", json));
        Assert.True(PermissionEvaluator.HasPermission(principal, "orders:create"));
        Assert.False(PermissionEvaluator.HasPermission(principal, "orders:delete"));
    }

    [Fact]
    public void Keycloak_ProtocolMapperPermissions_AreChecked()
    {
        // claim authorization.permissions, який створює protocol mapper із Pulumi: плоский список рядків
        const string json = """{"permissions":["orders:read","orders:update","orders:approve","products:read"]}""";
        var principal = User(("authorization", json));
        Assert.True(PermissionEvaluator.HasPermission(principal, "orders:approve"));
        Assert.False(PermissionEvaluator.HasPermission(principal, "orders:delete"));      // manager: DELETE /orders/{id} -> 403
        Assert.False(PermissionEvaluator.HasPermission(principal, "payments:read"));      // payments — лише admin
    }

    [Fact]
    public void Keycloak_ClientRoles_AreChecked()
    {
        const string json = """{"orders-api":{"roles":["orders:read","orders:update"]},"account":{"roles":["view-profile"]}}""";
        var principal = User(("resource_access", json));
        Assert.True(PermissionEvaluator.HasPermission(principal, "orders:update"));
        Assert.False(PermissionEvaluator.HasPermission(principal, "orders:approve"));
    }

    [Fact]
    public void MalformedJsonClaims_DoNotGrantAnything() =>
        Assert.False(PermissionEvaluator.HasPermission(User(("authorization", "{not json"), ("resource_access", "[")), "orders:read"));
}
