using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.HttpsPolicy;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.AspNetCore.Identity;
using HBA_ProgrammingForTheCloud.DataAccess;
using Google.Cloud.SecretManager.V1;
using Newtonsoft.Json.Linq;
using Google.Cloud.Diagnostics.AspNetCore3;
using Google.Cloud.Diagnostics.Common;
using Common.DataAccess;
using Microsoft.AspNetCore.Http;

namespace HBA_ProgrammingForTheCloud
{
    public class Startup
    {
        public Startup(IConfiguration configuration, IWebHostEnvironment host)
        {
            Configuration = configuration;

            string credential_path = host.ContentRootPath + "/hbaprogrammingforthecloud-ae18523f2725.json";
            System.Environment.SetEnvironmentVariable("GOOGLE_APPLICATION_CREDENTIALS", credential_path);
        }

        private string GetSecretValue(string nameOfSecret, string key)
        {

            SecretManagerServiceClient client = SecretManagerServiceClient.Create();

            // Build the resource name.
            SecretVersionName secretVersionName = new SecretVersionName(Configuration["projectid"].ToString(),
                nameOfSecret,
                "1");

            // Call the API.
            AccessSecretVersionResponse result = client.AccessSecretVersion(secretVersionName);

            // Convert the payload to a string. Payloads are bytes by default.
            String payload = result.Payload.Data.ToStringUtf8();

            var deserializedKeys = JObject.Parse(payload);
            var secret = deserializedKeys[key].ToString();
            return secret;
        }

        public void ConfigureServices(IServiceCollection services)
        {
            string projectId = Configuration["projectid"].ToString();


            services.AddGoogleErrorReportingForAspNetCore(new ErrorReportingServiceOptions
            {
                // Replace ProjectId with your Google Cloud Project ID.
                ProjectId = projectId,
                // Replace Service with a name or identifier for the service.
                ServiceName = "MainWebsite",
                // Replace Version with a version for the service.
                Version = "1"
            });
            services.AddControllersWithViews();

            var clientId = GetSecretValue("oauth_keys", "Authentication:Google:ClientId");
            var secretKey = GetSecretValue("oauth_keys", "Authentication:Google:ClientSecret");

            services.AddAuthentication( options =>
            {
                options.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
                options.DefaultChallengeScheme = GoogleDefaults.AuthenticationScheme;
            })
            .AddCookie()
            .AddGoogle(options =>
            {
                options.ClientId = clientId;
                options.ClientSecret = secretKey;
            });

            services.Configure<CookiePolicyOptions>(options =>
            {
                options.MinimumSameSitePolicy = Microsoft.AspNetCore.Http.SameSiteMode.Unspecified;
                options.OnAppendCookie = cookieContext => CheckSameSite(cookieContext.Context, cookieContext.CookieOptions);
                options.OnDeleteCookie = cookieContext => CheckSameSite(cookieContext.Context, cookieContext.CookieOptions);
            });

            services.AddScoped<FirestoreUploadRepository>(provider => new FirestoreUploadRepository(projectId));
            services.AddScoped<PubSubTranscriptRepository>(provider => new PubSubTranscriptRepository(projectId));
        }


        public IConfiguration Configuration { get; }


        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }
            else
            {
                app.UseExceptionHandler("/Home/Error");
                // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
                app.UseHsts();
            }
            app.UseHttpsRedirection();
            app.UseStaticFiles();

            app.UseRouting();
            app.UseCookiePolicy();
            app.UseAuthentication();
            app.UseAuthorization();

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapControllerRoute(
                    name: "default",
                    pattern: "{controller=Home}/{action=Index}/{id?}");
            }); 

        }

        private void CheckSameSite(HttpContext httpContext, CookieOptions options)
        {
            if(options.SameSite == SameSiteMode.None)
            {
                var userAgent = httpContext.Request.Headers["User-Agent"].ToString();

                if(true)
                {
                    options.SameSite = SameSiteMode.Unspecified;
                }
            }
        }
    }
}
