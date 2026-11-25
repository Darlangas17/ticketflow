using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using TicketFlow.Application.Auth;
using TicketFlow.Application.Common;
using TicketFlow.Application.Tickets;
using TicketFlow.Domain.Enums;
using TicketFlow.Infrastructure.Persistence.Seed;

namespace TicketFlow.Tests.Api;

/// <summary>End-to-end tests of /api/tickets over HTTP, against the demo seed in SQLite.</summary>
public class TicketEndpointsTests(TicketFlowApiFactory factory) : IClassFixture<TicketFlowApiFactory>
{
    private const string AdminEmail = "admin@ticketflow.demo";
    private const string TechEmail = "carlos.rivera@ticketflow.demo";
    private const string RequesterEmail = "maria.lopez@ticketflow.demo";

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };

    private async Task<(HttpClient Client, UserDto User)> LoginAsync(string email)
    {
        var client = factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest(email, DemoDataSeeder.DemoPassword), Json);
        response.EnsureSuccessStatusCode();
        var auth = (await response.Content.ReadFromJsonAsync<AuthResponse>(Json))!;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.AccessToken);
        return (client, auth.User);
    }

    private static async Task<T> ReadAsync<T>(HttpResponseMessage response) =>
        (await response.Content.ReadFromJsonAsync<T>(Json))!;

    private static async Task<TicketDetailsDto> CreateTicketAsync(HttpClient client, string title = "Keyboard not working")
    {
        var response = await client.PostAsJsonAsync(
            "/api/tickets", new CreateTicketRequest(title, "Several keys do not respond.", TicketPriority.High, 1, 1), Json);
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        return await ReadAsync<TicketDetailsDto>(response);
    }

    private static Task<HttpResponseMessage> ChangeStatusAsync(HttpClient client, int id, TicketStatus status) =>
        client.PatchAsJsonAsync($"/api/tickets/{id}/status", new ChangeTicketStatusRequest(status), Json);

    [Fact]
    public async Task Tickets_require_authentication()
    {
        var response = await factory.CreateClient().GetAsync("/api/tickets");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Requester_only_lists_their_own_tickets()
    {
        var (client, user) = await LoginAsync(RequesterEmail);

        var page = await ReadAsync<PagedResult<TicketListItemDto>>(await client.GetAsync("/api/tickets?pageSize=100"));

        page.Items.Should().NotBeEmpty();
        page.Items.Should().OnlyContain(t => t.Requester.Id == user.Id);
    }

    [Fact]
    public async Task Technician_lists_tickets_assigned_to_them_or_unassigned()
    {
        var (client, user) = await LoginAsync(TechEmail);

        var page = await ReadAsync<PagedResult<TicketListItemDto>>(await client.GetAsync("/api/tickets?pageSize=100"));

        page.Items.Should().Contain(t => t.Assignee == null);
        page.Items.Should().Contain(t => t.Assignee != null);
        page.Items.Should().OnlyContain(t => t.Assignee == null || t.Assignee.Id == user.Id);
    }

    [Fact]
    public async Task Admin_sees_all_seeded_tickets_paginated()
    {
        var (client, _) = await LoginAsync(AdminEmail);

        var page = await ReadAsync<PagedResult<TicketListItemDto>>(await client.GetAsync("/api/tickets?page=1&pageSize=10"));

        page.Items.Should().HaveCount(10);
        page.TotalCount.Should().BeGreaterThanOrEqualTo(DemoDataSeeder.TicketCount);
        page.Items.Should().BeInDescendingOrder(t => t.CreatedAt);
    }

    [Fact]
    public async Task List_filters_by_status_from_the_query_string()
    {
        var (client, _) = await LoginAsync(AdminEmail);

        var page = await ReadAsync<PagedResult<TicketListItemDto>>(await client.GetAsync("/api/tickets?status=OnHold&pageSize=100"));

        page.Items.Should().NotBeEmpty().And.OnlyContain(t => t.Status == TicketStatus.OnHold);
    }

    [Theory]
    [InlineData("status=Sleeping")]
    [InlineData("pageSize=500")]
    [InlineData("sort=password")]
    public async Task Invalid_query_parameters_return_400(string queryString)
    {
        var (client, _) = await LoginAsync(AdminEmail);

        var response = await client.GetAsync($"/api/tickets?{queryString}");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json");
    }

    [Fact]
    public async Task Create_returns_201_with_location_of_the_new_ticket()
    {
        var (client, user) = await LoginAsync(RequesterEmail);

        var response = await client.PostAsJsonAsync(
            "/api/tickets", new CreateTicketRequest("Mouse broken", "Left button fails.", null, 2, 1), Json);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var ticket = await ReadAsync<TicketDetailsDto>(response);
        response.Headers.Location!.AbsolutePath.Should().Be($"/api/tickets/{ticket.Id}");
        ticket.Requester.Id.Should().Be(user.Id);
        ticket.Status.Should().Be(TicketStatus.Open);
    }

    [Fact]
    public async Task Create_with_missing_fields_returns_validation_errors()
    {
        var (client, _) = await LoginAsync(RequesterEmail);

        var response = await client.PostAsJsonAsync("/api/tickets", new { title = "", description = "", categoryId = 0, branchId = 0 }, Json);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var problem = await ReadAsync<ValidationProblemDetails>(response);
        problem.Errors.Keys.Should().Contain(["Title", "Description", "CategoryId", "BranchId"]);
    }

    [Fact]
    public async Task Ticket_of_another_requester_returns_404()
    {
        var (owner, _) = await LoginAsync("jorge.castillo@ticketflow.demo");
        var ticket = await CreateTicketAsync(owner);
        var (other, _) = await LoginAsync(RequesterEmail);

        var response = await other.GetAsync($"/api/tickets/{ticket.Id}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Invalid_status_transition_returns_400_with_a_clear_message()
    {
        var (requester, _) = await LoginAsync(RequesterEmail);
        var ticket = await CreateTicketAsync(requester);
        var (admin, _) = await LoginAsync(AdminEmail);

        var response = await ChangeStatusAsync(admin, ticket.Id, TicketStatus.Closed);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var problem = await ReadAsync<ProblemDetails>(response);
        problem.Detail.Should().Be("Invalid status transition from Open to Closed. Allowed: InProgress.");
    }

    [Fact]
    public async Task Requester_cannot_assign_or_delete()
    {
        var (requester, _) = await LoginAsync(RequesterEmail);
        var ticket = await CreateTicketAsync(requester);

        var assign = await requester.PatchAsJsonAsync($"/api/tickets/{ticket.Id}/assign", new AssignTicketRequest(2), Json);
        var delete = await requester.DeleteAsync($"/api/tickets/{ticket.Id}");

        assign.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        delete.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Technician_cannot_delete_but_admin_can()
    {
        var (requester, _) = await LoginAsync(RequesterEmail);
        var ticket = await CreateTicketAsync(requester);
        var (tech, _) = await LoginAsync(TechEmail);
        var (admin, _) = await LoginAsync(AdminEmail);

        (await tech.DeleteAsync($"/api/tickets/{ticket.Id}")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await admin.DeleteAsync($"/api/tickets/{ticket.Id}")).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await admin.GetAsync($"/api/tickets/{ticket.Id}")).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Ticket_lifecycle_create_assign_comment_resolve_and_close()
    {
        var (requester, _) = await LoginAsync(RequesterEmail);
        var (tech, techUser) = await LoginAsync(TechEmail);
        var ticket = await CreateTicketAsync(requester, "Printer offline in warehouse");

        // Technician takes the unassigned ticket and works on it.
        var assigned = await tech.PatchAsJsonAsync($"/api/tickets/{ticket.Id}/assign", new AssignTicketRequest(techUser.Id), Json);
        assigned.StatusCode.Should().Be(HttpStatusCode.OK);
        (await ChangeStatusAsync(tech, ticket.Id, TicketStatus.InProgress)).StatusCode.Should().Be(HttpStatusCode.OK);

        var comment = await tech.PostAsJsonAsync($"/api/tickets/{ticket.Id}/comments", new AddCommentRequest("Restarted the print server."), Json);
        comment.StatusCode.Should().Be(HttpStatusCode.Created);

        (await ChangeStatusAsync(tech, ticket.Id, TicketStatus.Resolved)).StatusCode.Should().Be(HttpStatusCode.OK);

        // The requester confirms the fix by closing the ticket.
        var closed = await ChangeStatusAsync(requester, ticket.Id, TicketStatus.Closed);
        closed.StatusCode.Should().Be(HttpStatusCode.OK);

        var details = await ReadAsync<TicketDetailsDto>(await requester.GetAsync($"/api/tickets/{ticket.Id}"));
        details.Status.Should().Be(TicketStatus.Closed);
        details.ResolvedAt.Should().NotBeNull();
        details.Assignee!.Id.Should().Be(techUser.Id);
        details.Comments.Should().ContainSingle().Which.Body.Should().Be("Restarted the print server.");
        details.History.Select(h => $"{h.Field}:{h.NewValue}").Should().Equal(
            "Assignee:Carlos Rivera", "Status:InProgress", "Status:Resolved", "Status:Closed");
    }
}
