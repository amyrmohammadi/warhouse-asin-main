using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Warehouse.IntegrationTests.Infrastructure;

namespace Warehouse.IntegrationTests.Identity;

public sealed class CurrentUserProfileTests
{
    [Fact]
    public async Task CurrentUser_CanUpdateProfileImageAndChangeOwnPassword()
    {
        using var factory = new FullStackWebApplicationFactory();
        await factory.InitializeDatabaseAsync();
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"),
            AllowAutoRedirect = false
        });

        var tokens = await LoginAsync(
            client,
            FullStackWebApplicationFactory.UserEmail,
            FullStackWebApplicationFactory.UserPassword);

        var png = new byte[]
        {
            0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A,
            0x00, 0x00, 0x00, 0x00
        };
        using var form = new MultipartFormDataContent();
        form.Add(new StringContent("Updated Profile User"), "fullName");
        form.Add(new StringContent("+989121234567"), "phoneNumber");
        form.Add(new StringContent("false"), "removeProfileImage");
        using var imageContent = new ByteArrayContent(png);
        imageContent.Headers.ContentType = new MediaTypeHeaderValue("image/png");
        form.Add(imageContent, "profileImage", "avatar.png");

        using (var updateRequest = new HttpRequestMessage(
                   HttpMethod.Put,
                   "/endpoint/auth/profile") { Content = form })
        {
            updateRequest.Headers.Authorization =
                new AuthenticationHeaderValue("Bearer", tokens.AccessToken);
            using var updateResponse = await client.SendAsync(updateRequest);
            Assert.Equal(HttpStatusCode.OK, updateResponse.StatusCode);
        }

        using (var profileRequest = new HttpRequestMessage(
                   HttpMethod.Get,
                   "/endpoint/auth/profile"))
        {
            profileRequest.Headers.Authorization =
                new AuthenticationHeaderValue("Bearer", tokens.AccessToken);
            using var profileResponse = await client.SendAsync(profileRequest);
            Assert.Equal(HttpStatusCode.OK, profileResponse.StatusCode);
            var envelope = await profileResponse.Content
                .ReadFromJsonAsync<ApiEnvelope<CurrentUserProfileData>>();
            var profile = Assert.IsType<CurrentUserProfileData>(envelope?.Data);
            Assert.Equal("Updated Profile User", profile.FullName);
            Assert.Equal(FullStackWebApplicationFactory.UserEmail, profile.Email);
            Assert.Equal("image/png", profile.ProfileImageContentType);
            Assert.Equal(png, profile.ProfileImage);
        }

        using (var wrongPasswordResponse = await SendPasswordChangeAsync(
                   client,
                   tokens.AccessToken,
                   "WrongPassword1!",
                   "ChangedProfile123!"))
        {
            Assert.Equal(HttpStatusCode.BadRequest, wrongPasswordResponse.StatusCode);
        }

        using (var passwordResponse = await SendPasswordChangeAsync(
                   client,
                   tokens.AccessToken,
                   FullStackWebApplicationFactory.UserPassword,
                   "ChangedProfile123!"))
        {
            Assert.Equal(HttpStatusCode.OK, passwordResponse.StatusCode);
        }

        using var oldLogin = await client.PostAsJsonAsync(
            "/endpoint/auth/login",
            new
            {
                email = FullStackWebApplicationFactory.UserEmail,
                password = FullStackWebApplicationFactory.UserPassword
            });
        Assert.Equal(HttpStatusCode.Unauthorized, oldLogin.StatusCode);

        var replacementTokens = await LoginAsync(
            client,
            FullStackWebApplicationFactory.UserEmail,
            "ChangedProfile123!");
        Assert.False(string.IsNullOrWhiteSpace(replacementTokens.AccessToken));
    }

    private static async Task<TokenData> LoginAsync(
        HttpClient client,
        string email,
        string password)
    {
        using var response = await client.PostAsJsonAsync(
            "/endpoint/auth/login",
            new { email, password });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var envelope = await response.Content.ReadFromJsonAsync<ApiEnvelope<TokenData>>();
        return Assert.IsType<TokenData>(envelope?.Data);
    }

    private static Task<HttpResponseMessage> SendPasswordChangeAsync(
        HttpClient client,
        string accessToken,
        string currentPassword,
        string newPassword)
    {
        var request = new HttpRequestMessage(
            HttpMethod.Put,
            "/endpoint/auth/profile/password")
        {
            Content = JsonContent.Create(new { currentPassword, newPassword })
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        return client.SendAsync(request);
    }
}
