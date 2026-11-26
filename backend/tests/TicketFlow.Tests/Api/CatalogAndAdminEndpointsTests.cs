using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using FluentAssertions;
using TicketFlow.Application.Auth;
using TicketFlow.Application.Catalogs;
using TicketFlow.Application.Common;
using TicketFlow.Application.Dashboard;
using TicketFlow.Application.Tickets;
using TicketFlow.Application.Users;
using TicketFlow.Domain.Enums;
using TicketFlow.Infrastructure.Persistence.Seed;

namespace TicketFlow.Tests.Api;

/// <summary>End-to-end tests of /api/branches, /api/categories, /api/users and /api/dashboard.</summary>
public class CatalogAndAdminEndpointsTests(TicketFlowApiFactory factory) : IClassFixture<TicketFlowApiFactory>
{
    private const string AdminEmail = "admin@ticketflow.demo";
    private const string TechEmail = "carlos.rivera@ticketflow.demo";
    private const string RequesterEmail = "maria.lopez@ticketflow.demo";

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };

    private async Task<(HttpClient Client, UserDto User)> LoginAsync(string email, string password = DemoDataSeeder.DemoPassword)
    {
        var client = factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest(email, password), Json);
        response.EnsureSuccessStatusCode();
        var auth = (await response.Content.ReadFromJsonAsync<AuthResponse>(Json))!;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.AccessToken);
        return (client, auth.User);
    }

    private static async Task<T> ReadAsync<T>(HttpResponseMessage response) =>
        (await response.Content.ReadFromJsonAsync<T>(Json))!;

    // ----- Catalogs -----

    [Fact]
    public async Task Any_user_can_read_the_catalogs()
    {
        var (requester, _) = await LoginAsync(RequesterEmail);

        var branches = await ReadAsync<List<BranchDto>>(await requester.GetAsync("/api/branches"));
        var categories = await ReadAsync<List<CategoryDto>>(await requester.GetAsync("/api/categories"));

        branches.Should().HaveCount(3).And.BeInAscendingOrder(b => b.Name);
        categories.Should().HaveCount(5).And.BeInAscendingOrder(c => c.Name);
    }

    [Fact]
    public async Task Only_admins_can_change_catalogs()
    {
        var (tech, _) = await LoginAsync(TechEmail);

        (await tech.PostAsJsonAsync("/api/branches", new CreateBranchRequest("X", "Y"), Json)).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await tech.PutAsJsonAsync("/api/categories/1", new SaveCategoryRequest("X", null), Json)).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await tech.DeleteAsync("/api/categories/1")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Admin_branch_crud_with_deactivation()
    {
        var (admin, _) = await LoginAsync(AdminEmail);
        var (requester, _) = await LoginAsync(RequesterEmail);

        var created = await admin.PostAsJsonAsync("/api/branches", new CreateBranchRequest("Comayagua Plaza", "Comayagua"), Json);
        created.StatusCode.Should().Be(HttpStatusCode.Created);
        var branch = await ReadAsync<BranchDto>(created);
        created.Headers.Location!.AbsolutePath.Should().Be($"/api/branches/{branch.Id}");

        var duplicate = await admin.PostAsJsonAsync("/api/branches", new CreateBranchRequest("COMAYAGUA PLAZA", "Comayagua"), Json);
        duplicate.StatusCode.Should().Be(HttpStatusCode.Conflict);

        var deactivated = await admin.PutAsJsonAsync($"/api/branches/{branch.Id}", new UpdateBranchRequest("Comayagua Plaza", "Comayagua", false), Json);
        deactivated.StatusCode.Should().Be(HttpStatusCode.OK);

        // Hidden for regular users; visible to admins on request.
        (await ReadAsync<List<BranchDto>>(await requester.GetAsync("/api/branches?includeInactive=true")))
            .Should().NotContain(b => b.Id == branch.Id);
        (await ReadAsync<List<BranchDto>>(await admin.GetAsync("/api/branches?includeInactive=true")))
            .Should().Contain(b => b.Id == branch.Id && !b.IsActive);

        // An inactive branch cannot receive new tickets.
        var ticket = await requester.PostAsJsonAsync("/api/tickets", new CreateTicketRequest("T", "D", null, 1, branch.Id), Json);
        ticket.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        (await admin.DeleteAsync($"/api/branches/{branch.Id}")).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await admin.GetAsync($"/api/branches/{branch.Id}")).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Catalog_with_tickets_cannot_be_deleted()
    {
        var (admin, _) = await LoginAsync(AdminEmail);

        var branch = await admin.DeleteAsync("/api/branches/1");
        var category = await admin.DeleteAsync("/api/categories/1");

        branch.StatusCode.Should().Be(HttpStatusCode.Conflict);
        category.StatusCode.Should().Be(HttpStatusCode.Conflict);
        branch.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json");
    }

    [Fact]
    public async Task Admin_category_crud()
    {
        var (admin, _) = await LoginAsync(AdminEmail);

        var created = await admin.PostAsJsonAsync("/api/categories", new SaveCategoryRequest("Telephony", "Desk phones"), Json);
        created.StatusCode.Should().Be(HttpStatusCode.Created);
        var category = await ReadAsync<CategoryDto>(created);

        var updated = await admin.PutAsJsonAsync($"/api/categories/{category.Id}", new SaveCategoryRequest("Telephony & VoIP", null), Json);
        (await ReadAsync<CategoryDto>(updated)).Should().Be(new CategoryDto(category.Id, "Telephony & VoIP", null));

        (await admin.DeleteAsync($"/api/categories/{category.Id}")).StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    // ----- Users -----

    [Theory]
    [InlineData(TechEmail)]
    [InlineData(RequesterEmail)]
    public async Task User_administration_is_admin_only(string email)
    {
        var (client, _) = await LoginAsync(email);

        (await client.GetAsync("/api/users")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await client.GetAsync("/api/users/1")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await client.PostAsJsonAsync("/api/users", new CreateUserRequest("X", "x@ticketflow.demo", "Strong-Pass-2026", UserRole.Admin), Json))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Admin_lists_users_without_password_hashes()
    {
        var (admin, _) = await LoginAsync(AdminEmail);

        var response = await admin.GetAsync("/api/users?role=Technician");
        var body = await response.Content.ReadAsStringAsync();
        var page = await ReadAsync<PagedResult<UserAccountDto>>(response);

        page.Items.Select(u => u.FullName).Should().Equal("Ana Torres", "Carlos Rivera");
        body.Should().NotContainEquivalentOf("password");
    }

    [Fact]
    public async Task Admin_creates_a_technician_who_can_log_in_and_be_assigned()
    {
        var (admin, _) = await LoginAsync(AdminEmail);

        var created = await admin.PostAsJsonAsync(
            "/api/users", new CreateUserRequest("Diego Flores", "diego.flores@ticketflow.demo", "Strong-Pass-2026", UserRole.Technician), Json);
        created.StatusCode.Should().Be(HttpStatusCode.Created);
        var user = await ReadAsync<UserAccountDto>(created);

        var (diego, me) = await LoginAsync("diego.flores@ticketflow.demo", "Strong-Pass-2026");
        me.Role.Should().Be(UserRole.Technician);
        var assignees = await ReadAsync<List<UserSummaryDto>>(await diego.GetAsync("/api/users/assignees"));
        assignees.Should().Contain(new UserSummaryDto(user.Id, "Diego Flores"));
    }

    [Fact]
    public async Task Deactivated_user_loses_access_with_an_existing_token()
    {
        var (admin, _) = await LoginAsync(AdminEmail);
        var created = await ReadAsync<UserAccountDto>(await admin.PostAsJsonAsync(
            "/api/users", new CreateUserRequest("Temp User", "temp.user@ticketflow.demo", "Strong-Pass-2026", UserRole.Requester), Json));
        var (temp, _) = await LoginAsync("temp.user@ticketflow.demo", "Strong-Pass-2026");

        var update = await admin.PutAsJsonAsync($"/api/users/{created.Id}",
            new UpdateUserRequest("Temp User", "temp.user@ticketflow.demo", UserRole.Requester, IsActive: false), Json);
        update.StatusCode.Should().Be(HttpStatusCode.OK);

        (await temp.GetAsync("/api/tickets")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Admin_cannot_remove_their_own_admin_role()
    {
        var (admin, me) = await LoginAsync(AdminEmail);

        var response = await admin.PutAsJsonAsync($"/api/users/{me.Id}",
            new UpdateUserRequest(me.FullName, me.Email, UserRole.Technician, true), Json);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Requesters_cannot_list_assignees()
    {
        var (requester, _) = await LoginAsync(RequesterEmail);

        (await requester.GetAsync("/api/users/assignees")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    // ----- Dashboard -----

    [Theory]
    [InlineData(AdminEmail)]
    [InlineData(TechEmail)]
    [InlineData(RequesterEmail)]
    public async Task Dashboard_totals_match_the_tickets_each_role_can_see(string email)
    {
        var (client, _) = await LoginAsync(email);

        var summary = await ReadAsync<DashboardSummaryDto>(await client.GetAsync("/api/dashboard/summary"));
        var list = await ReadAsync<PagedResult<TicketListItemDto>>(await client.GetAsync("/api/tickets?pageSize=1"));

        summary.TotalTickets.Should().Be(list.TotalCount);
        summary.ByStatus.Sum(s => s.Count).Should().Be(summary.TotalTickets);
        summary.ByPriority.Sum(p => p.Count).Should().Be(summary.TotalTickets);
        summary.ByBranch.Sum(b => b.Count).Should().Be(summary.TotalTickets);
        summary.ByCategory.Sum(c => c.Count).Should().Be(summary.TotalTickets);
    }

    [Fact]
    public async Task Admin_dashboard_reflects_the_demo_seed()
    {
        var (admin, _) = await LoginAsync(AdminEmail);

        var summary = await ReadAsync<DashboardSummaryDto>(await admin.GetAsync("/api/dashboard/summary"));

        summary.TotalTickets.Should().BeGreaterThanOrEqualTo(DemoDataSeeder.TicketCount);
        summary.ByStatus.Should().HaveCount(5).And.OnlyContain(s => s.Count > 0); // the seed has every status
        summary.ResolvedTickets.Should().BeGreaterThan(0);
        summary.AverageResolutionHours.Should().BePositive();
    }

    [Fact]
    public async Task Dashboard_requires_authentication()
    {
        (await factory.CreateClient().GetAsync("/api/dashboard/summary")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
