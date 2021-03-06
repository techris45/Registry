using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNet.OData.Builder;
using Microsoft.AspNet.OData.Extensions;
using Microsoft.AspNetCore.Authentication.Cookies;
using Registry.Web.Models;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.HttpsPolicy;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.CodeAnalysis.Options;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.StackExchangeRedis;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OData.Edm;
using Registry.Adapters.DroneDB;
using Registry.Adapters.ObjectSystem;
using Registry.Common;
using Registry.Ports.DroneDB;
using Registry.Ports.ObjectSystem;
using Registry.Web.Data;
using Registry.Web.Data.Models;
using Registry.Web.Models.Configuration;
using Registry.Web.Models.DTO;
using Registry.Web.Services;
using Registry.Web.Services.Adapters;
using Registry.Web.Services.Ports;
using Registry.Web.Utilities;

namespace Registry.Web
{
    public class Startup
    {
        private const string IdentityConnectionName = "IdentityConnection";
        private const string RegistryConnectionName = "RegistryConnection";

        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        public IConfiguration Configuration { get; }

        public void ConfigureServices(IServiceCollection services)
        {
            services.AddCors();
            services.AddControllers();

            services.AddMvcCore().AddNewtonsoftJson();

            // Let's use a strongly typed class for settings
            var appSettingsSection = Configuration.GetSection("AppSettings");
            services.Configure<AppSettings>(appSettingsSection);

            var appSettings = appSettingsSection.Get<AppSettings>();

            ConfigureDbProvider<ApplicationDbContext>(services, appSettings.AuthProvider, IdentityConnectionName);

            if (!string.IsNullOrWhiteSpace(appSettings.ExternalAuthUrl))
            {
                services.AddIdentityCore<User>()
                    .AddRoles<IdentityRole>()
                    .AddEntityFrameworkStores<ApplicationDbContext>()
                    .AddSignInManager<ExternalSignInManager>();
            }
            else
            {
                services.AddIdentityCore<User>()
                    .AddRoles<IdentityRole>()
                    .AddEntityFrameworkStores<ApplicationDbContext>()
                    .AddSignInManager();
            }

            ConfigureDbProvider<RegistryContext>(services, appSettings.RegistryProvider, RegistryConnectionName);

            var key = Encoding.ASCII.GetBytes(appSettings.Secret);
            services.AddAuthentication(auth =>
                {
                    auth.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
                    auth.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;

                })
                .AddJwtBearer(jwt =>
                {
                    jwt.RequireHttpsMetadata = false;
                    jwt.SaveToken = true;
                    jwt.TokenValidationParameters = new TokenValidationParameters
                    {
                        ValidateIssuerSigningKey = true,
                        IssuerSigningKey = new SymmetricSecurityKey(key),
                        ValidateIssuer = false,
                        ValidateAudience = false
                    };
                });

            services.Configure<IdentityOptions>(options =>
            {
                // Password settings.
                options.Password.RequireDigit = false;
                options.Password.RequireLowercase = false;
                options.Password.RequireNonAlphanumeric = false;
                options.Password.RequireUppercase = false;
                options.Password.RequiredLength = 1;
                options.Password.RequiredUniqueChars = 0;

                // Lockout settings.
                options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(5);
                options.Lockout.MaxFailedAccessAttempts = 5;
                options.Lockout.AllowedForNewUsers = true;

                // User settings.
                options.User.AllowedUserNameCharacters =
                    "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789-._@+";
                options.User.RequireUniqueEmail = false;
            });

            // Error messages that make sense
            services.Configure<ApiBehaviorOptions>(o =>
            {
                o.InvalidModelStateResponseFactory = actionContext =>
                    new BadRequestObjectResult(new ErrorResponse(actionContext.ModelState));
            });

            RegisterCacheProvider(services, appSettings);

            /*
             * NOTE about services lifetime:
             *
             * - A type should be registered as a "Singleton" only when it is fully thread-safe and is not dependent on other services or types.
             * - Scoped services are bound under a scope (request), and a new instance is created and reused inside a created "scope".
             * - If a service is defined as Transient, it is instantiated whenever invoked within a request.
             *   It is almost similar to creating an instance of the same type using "new" keyword and using it.
             *   It is also the safest option among all other service types, since we don't need to bother about the thread-safety and memory leaks.
             *
             * = In terms of lifetime, the singleton object gets the highest life per instantiation,
             *   followed by a Scoped service object and the least by a Transient object.
             */

            services.AddTransient<TokenManagerMiddleware>();
            services.AddTransient<JwtInCookieMiddleware>();
            services.AddTransient<ITokenManager, TokenManager>();
            services.AddSingleton<IHttpContextAccessor, HttpContextAccessor>();

            services.AddScoped<IUtils, WebUtils>();
            services.AddScoped<IAuthManager, AuthManager>();

            services.AddScoped<IChunkedUploadManager, ChunkedUploadManager>();
            services.AddScoped<IUsersManager, UsersManager>();
            services.AddScoped<IOrganizationsManager, OrganizationsManager>();
            services.AddScoped<IDatasetsManager, DatasetsManager>();
            services.AddScoped<IObjectsManager, ObjectsManager>();
            services.AddScoped<IShareManager, ShareManager>();
            services.AddScoped<IDdbFactory, DdbFactory>();
            services.AddSingleton<IPasswordHasher, PasswordHasher>();
            services.AddSingleton<IBatchTokenGenerator, BatchTokenGenerator>();
            services.AddSingleton<INameGenerator, NameGenerator>();

            RegisterStorageProvider(services, appSettings);

            services.Configure<FormOptions>(options =>
            {
                // See https://docs.microsoft.com/it-it/aspnet/core/fundamentals/servers/kestrel?view=aspnetcore-3.1#maximum-client-connections
                // We could put this in config "Kestrel->Limits" section
                options.MultipartBodyLengthLimit = appSettings.MaxRequestBodySize;
            });

            services.AddHttpContextAccessor();

            // TODO: Enable when needed. Should check return object structure
            // services.AddOData();

        }

        private void RegisterCacheProvider(IServiceCollection services, AppSettings appSettings)
        {

            if (appSettings.CacheProvider == null)
            {
                // No caching
                services.AddSingleton<IDistributedCache, DummyDistributedCache>();
                return;
            }

            switch (appSettings.CacheProvider.Type)
            {
                case CacheType.InMemory:

                    services.AddDistributedMemoryCache();

                    break;

                case CacheType.Redis:

                    var settings = appSettings.CacheProvider.Settings.ToObject<RedisProviderSettings>();

                    if (settings == null)
                        throw new ArgumentException("Invalid redis cache provider settings");

                    services.AddStackExchangeRedisCache(options =>
                    {
                        options.Configuration = settings.InstanceAddress;
                        options.InstanceName = settings.InstanceName;
                    });

                    break;

                default:
                    throw new InvalidOperationException(
                        $"Unsupported caching provider: '{(int)appSettings.CacheProvider.Type}'");
            }

        }

        private static void RegisterStorageProvider(IServiceCollection services, AppSettings appSettings)
        {
            switch (appSettings.StorageProvider.Type)
            {
                case StorageType.Physical:

                    var pySettings = appSettings.StorageProvider.Settings.ToObject<PhysicalProviderSettings>();
                    if (pySettings == null)
                        throw new ArgumentException("Invalid physical storage provider settings");

                    var basePath = pySettings.Path;

                    Directory.CreateDirectory(basePath);

                    services.AddScoped<IObjectSystem>(provider => new PhysicalObjectSystem(basePath));

                    break;

                case StorageType.S3:

                    var s3Settings = appSettings.StorageProvider.Settings.ToObject<S3ProviderSettings>();

                    if (s3Settings == null)
                        throw new ArgumentException("Invalid S3 storage provider settings");

                    services.AddScoped<IObjectSystem, S3ObjectSystem>(provider => new S3ObjectSystem(
                        s3Settings.Endpoint, 
                        s3Settings.AccessKey, 
                        s3Settings.SecretKey,
                        s3Settings.Region,
                        s3Settings.SessionToken,
                        s3Settings.UseSsl ?? false,
                        s3Settings.AppName,
                        s3Settings.AppVersion));

                    break;

                default:
                    throw new InvalidOperationException(
                        $"Unsupported storage provider: '{(int)appSettings.StorageProvider.Type}'");
            }
        }

        private void ConfigureDbProvider<T>(IServiceCollection services, DbProvider provider, string connectionStringName) where T : DbContext
        {
            switch (provider)
            {
                case DbProvider.Sqlite:

                    services.AddDbContext<T>(options =>
                        options.UseSqlite(
                            Configuration.GetConnectionString(connectionStringName)));

                    break;

                case DbProvider.Mysql:

                    services.AddDbContext<T>(options =>
                        options.UseMySql(
                            Configuration.GetConnectionString(connectionStringName)));

                    break;

                case DbProvider.Mssql:

                    services.AddDbContext<T>(options =>
                        options.UseSqlServer(
                            Configuration.GetConnectionString(connectionStringName)));

                    break;

                default:
                    throw new ArgumentOutOfRangeException($"Unrecognised provider: '{provider}'");
            }
        }

        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {

            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }

            // We are permissive now
            app.UseCors(cors => cors
                .AllowAnyOrigin()
                .AllowAnyMethod()
                .AllowAnyHeader());

            app.UseRouting();

            app.UseMiddleware<JwtInCookieMiddleware>();

            app.UseAuthentication();
            app.UseAuthorization();

            app.UseMiddleware<TokenManagerMiddleware>();

            app.UseDefaultFiles();
            app.UseStaticFiles();

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapControllers();
                // TODO: Enable when needed
                // endpoints.MapODataRoute("odata", "odata", GetEdmModel());
            });

            SetupDatabase(app);

        }
        //private IEdmModel GetEdmModel()
        //{
        //    var odataBuilder = new ODataConventionModelBuilder();
        //    odataBuilder.EntitySet<OrganizationDto>("Organizations");

        //    return odataBuilder.GetEdmModel();
        //}

        // NOTE: Maybe put all this as stated in https://stackoverflow.com/a/55707949
        private void SetupDatabase(IApplicationBuilder app)
        {
            using var serviceScope = app.ApplicationServices
                .GetRequiredService<IServiceScopeFactory>()
                .CreateScope();
            using var applicationDbContext = serviceScope.ServiceProvider.GetService<ApplicationDbContext>();

            if (applicationDbContext == null)
                throw new InvalidOperationException("Cannot get application db context from service provider");

            if (applicationDbContext.Database.IsSqlite())
                CommonUtils.EnsureFolderCreated(Configuration.GetConnectionString(IdentityConnectionName));

            applicationDbContext.Database.EnsureCreated();

            CreateDefaultAdmin(serviceScope.ServiceProvider).Wait();

            using var registryDbContext = serviceScope.ServiceProvider.GetService<RegistryContext>();

            if (registryDbContext == null)
                throw new InvalidOperationException("Cannot get registry db context from service provider");

            if (registryDbContext.Database.IsSqlite())
                CommonUtils.EnsureFolderCreated(Configuration.GetConnectionString(RegistryConnectionName));

            registryDbContext.Database.EnsureCreated();

            CreateInitialData(registryDbContext);

        }

        private void CreateInitialData(RegistryContext context)
        {
            // If no organizations in database, let's create the public one
            if (!context.Organizations.Any())
            {

                var entity = new Organization
                {
                    Slug = MagicStrings.PublicOrganizationSlug,
                    Name = "Public",
                    CreationDate = DateTime.Now,
                    Description = "Public organization",
                    IsPublic = true,
                    // NOTE: Maybe this is a good idea to flag this org as "system"
                    OwnerId = null
                };
                var ds = new Dataset
                {
                    Slug = MagicStrings.DefaultDatasetSlug,
                    Name = "Default",
                    Description = "Default dataset",
                    IsPublic = true,
                    CreationDate = DateTime.Now,
                    LastEdit = DateTime.Now
                };
                entity.Datasets = new List<Dataset> { ds };

                context.Organizations.Add(entity);
                context.SaveChanges();
            }
        }

        private async Task CreateDefaultAdmin(IServiceProvider provider)
        {

            var usersManager = provider.GetService<UserManager<User>>();
            var roleManager = provider.GetService<RoleManager<IdentityRole>>();
            var appSettings = provider.GetService<IOptions<AppSettings>>();

            if (usersManager == null)
                throw new InvalidOperationException("Cannot get users manager from service provider");

            if (roleManager == null)
                throw new InvalidOperationException("Cannot get role manager from service provider");

            if (appSettings == null)
                throw new InvalidOperationException("Cannot get app settings from service provider");

            // If no users in database, let's create the default admin
            if (!usersManager.Users.Any())
            {
                // first we create Admin role  
                var role = new IdentityRole { Name = ApplicationDbContext.AdminRoleName };
                await roleManager.CreateAsync(role);

                var defaultAdmin = appSettings.Value.DefaultAdmin;
                var user = new User
                {
                    Email = defaultAdmin.Email,
                    UserName = defaultAdmin.UserName
                };

                var usrRes = await usersManager.CreateAsync(user, defaultAdmin.Password);
                if (usrRes.Succeeded)
                {
                    await usersManager.AddToRoleAsync(user, ApplicationDbContext.AdminRoleName);
                }
            }
        }


    }
}
