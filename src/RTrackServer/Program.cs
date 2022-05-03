using System;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using RTrackServer.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSingleton<ITrackerService, TrackerService>();
builder.Services.AddHostedService(sp => (TrackerService)sp.GetRequiredService<ITrackerService>());

builder.Services.AddRazorPages();
builder.Services.AddServerSideBlazor();

if (string.Equals(Environment.GetEnvironmentVariable("ASPNETCORE_FORWARDEDHEADERS_ENABLED"), "true", StringComparison.OrdinalIgnoreCase))
    builder.Services.Configure<ForwardedHeadersOptions>(opts => {
        opts.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
        opts.KnownNetworks.Clear();
        opts.KnownProxies.Clear();
    });

var credentials = builder.Configuration.GetRequiredSection("TiraxAuthenticator").Get<OAuthCredentials>();

builder.Services.AddAuthentication(opt => {
            opt.DefaultAuthenticateScheme = CookieAuthenticationDefaults.AuthenticationScheme;
            opt.DefaultSignInScheme = CookieAuthenticationDefaults.AuthenticationScheme;
            opt.DefaultChallengeScheme = OpenIdConnectDefaults.AuthenticationScheme;
        })
       .AddCookie()
       .AddOpenIdConnect("oidc",
                         opts => {
                             opts.Authority = "https://auth.tirax.tech/";
                             opts.ClientSecret = credentials.ClientSecret;
                             opts.ClientId = credentials.ClientId;
                             opts.UsePkce = true;
                             opts.ResponseType = "code";
                             opts.SaveTokens = true;
                             opts.GetClaimsFromUserInfoEndpoint = true;
                             opts.Scope.Add("openid");
                             opts.Scope.Add("profile");
                             opts.TokenValidationParameters = new(){ NameClaimType = "name" };
                         });

var app = builder.Build();

var basePath = builder.Configuration["BasePath"] ?? "/";
Console.WriteLine("BasePath = {0}", basePath);
app.UsePathBase(basePath);

app.UseForwardedHeaders();

if (builder.Environment.IsDevelopment())
    app.UseDeveloperExceptionPage();
else {
    app.UseExceptionHandler("/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseAuthentication();
app.UseAuthentication();

app.UseStaticFiles();

app.UseRouting();

app.UseEndpoints(endpoints =>
{
    endpoints.MapBlazorHub();
    endpoints.MapFallbackToPage("/_Host");
});

app.Run();

sealed class OAuthCredentials
{
    public string ClientId { get; set; } = string.Empty;
    public string ClientSecret { get; set; } = string.Empty;
}