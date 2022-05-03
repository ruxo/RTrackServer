using System;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using RTrackServer.Services;

namespace RTrackServer
{
    sealed class OAuthCredentials
    {
        public string ClientId { get; set; } = string.Empty;
        public string ClientSecret { get; set; } = string.Empty;
    }

    public class Startup
    {
        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        public IConfiguration Configuration { get; }

        // This method gets called by the runtime. Use this method to add services to the container.
        // For more information on how to configure your application, visit https://go.microsoft.com/fwlink/?LinkID=398940
        public void ConfigureServices(IServiceCollection services) {
            services.AddSingleton<ITrackerService, TrackerService>();
            services.AddHostedService(sp => (TrackerService)sp.GetRequiredService<ITrackerService>());

            services.AddRazorPages();
            services.AddServerSideBlazor();

            var credentials = Configuration.GetRequiredSection("TiraxAuthenticator").Get<OAuthCredentials>();

            services.AddAuthentication(opt => {
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
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            var basePath = Configuration["BasePath"] ?? "/";
            Console.WriteLine("BasePath = {0}", basePath);
            app.UsePathBase(basePath);
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }
            else
            {
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
        }
    }
}