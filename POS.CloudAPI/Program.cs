using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using POS.CloudAPI.Database;
using POS.CloudAPI.Services;
using System.Text;

namespace POS.CloudAPI
{
    public class Program
    {
        public static async Task Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            // Configure Cloud API port: default to 5100 locally, but allow IIS/MonsterAsp to assign ports dynamically
            if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("ASPNETCORE_PORT")) &&
                string.IsNullOrEmpty(Environment.GetEnvironmentVariable("PORT")))
            {
                builder.WebHost.UseUrls("http://*:5100");
            }

            // Dynamically resolve SQL Server connection
            var resolvedConnectionString = ResolveWorkingConnectionString(builder.Configuration);
            builder.Configuration["ConnectionStrings:DefaultConnection"] = resolvedConnectionString;

            Console.WriteLine($"[Cloud API] Using SQL Server with DataSource: {new Microsoft.Data.SqlClient.SqlConnectionStringBuilder(resolvedConnectionString).DataSource}");

            // Add DbContext
            builder.Services.AddDbContext<CloudDbContext>(options =>
                options.UseSqlServer(resolvedConnectionString));

            // Services
            builder.Services.AddScoped<IJwtTokenService, JwtTokenService>();

            // JWT Authentication
            var jwtKey = builder.Configuration["JWT:SecretKey"] ?? "Super-Secret-Online-Cloud-Key-For-POS-Mobile-2026-Supermarket-Secure-Jwt-Token";
            builder.Services.AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
                options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
            })
            .AddJwtBearer(options =>
            {
                options.RequireHttpsMetadata = false;
                options.SaveToken = true;
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
                    ValidateIssuer = false,
                    ValidateAudience = false,
                    ClockSkew = TimeSpan.Zero
                };
            });

            builder.Services.AddAuthorization();
            builder.Services.AddControllers();
            builder.Services.AddEndpointsApiExplorer();

            // Swagger with JWT Bearer support
            builder.Services.AddSwaggerGen(c =>
            {
                c.SwaggerDoc("v1", new OpenApiInfo
                {
                    Title = "POS Cloud & Mobile API",
                    Version = "v1",
                    Description = "REST API for Supermarket Mobile App and POS Sync Service"
                });

                c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
                {
                    Description = "Enter JWT Bearer token: 'Bearer {token}'",
                    Name = "Authorization",
                    In = ParameterLocation.Header,
                    Type = SecuritySchemeType.ApiKey,
                    Scheme = "Bearer"
                });

                c.AddSecurityRequirement(new OpenApiSecurityRequirement
                {
                    {
                        new OpenApiSecurityScheme
                        {
                            Reference = new OpenApiReference
                            {
                                Type = ReferenceType.SecurityScheme,
                                Id = "Bearer"
                            }
                        },
                        Array.Empty<string>()
                    }
                });
            });

            builder.Services.AddCors(options =>
            {
                options.AddPolicy("AllowAll", policy =>
                {
                    policy.AllowAnyOrigin()
                          .AllowAnyMethod()
                          .AllowAnyHeader();
                });
            });

            var app = builder.Build();

            // Ensure Database Created & Seed
            using (var scope = app.Services.CreateScope())
            {
                try
                {
                    var db = scope.ServiceProvider.GetRequiredService<CloudDbContext>();
                    await db.Database.EnsureCreatedAsync();
                    await db.EnsureSchemaUpToDateAsync();
                    await db.SeedInitialDataAsync();
                    Console.WriteLine("[Cloud API] Online database initialized and seeded successfully.");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[Cloud API Notice] Database init: {ex.Message}");
                }
            }

            app.UseSwagger();
            app.UseSwaggerUI(c =>
            {
                c.SwaggerEndpoint("/swagger/v1/swagger.json", "POS Cloud API v1");
                c.RoutePrefix = "swagger";
            });

            app.UseCors("AllowAll");
            app.UseAuthentication();
            app.UseAuthorization();
            app.MapControllers();

            Console.WriteLine("[Cloud API] Server is running at http://localhost:5100 / http://*:5100");
            app.Run();
        }

        private static string ResolveWorkingConnectionString(IConfiguration configuration)
        {
            var configured = configuration.GetConnectionString("DefaultConnection");

            if (!string.IsNullOrWhiteSpace(configured) && CanConnectToSql(configured))
            {
                return configured;
            }

            var fallbackServers = new[] { ".\\SQLEXPRESS", ".", "localhost", "(localdb)\\MSSQLLocalDB", "127.0.0.1" };
            var connBuilder = new Microsoft.Data.SqlClient.SqlConnectionStringBuilder(configured ?? "Database=POSCashier_Online;Integrated Security=True;Encrypt=False;TrustServerCertificate=True;MultipleActiveResultSets=True;");

            foreach (var server in fallbackServers)
            {
                connBuilder.DataSource = server;
                connBuilder.ConnectTimeout = 2;
                if (CanConnectToSql(connBuilder.ConnectionString))
                {
                    connBuilder.ConnectTimeout = 30;
                    return connBuilder.ConnectionString;
                }
            }

            return configured ?? "Server=.\\SQLEXPRESS; Database=POSCashier_Online; Integrated Security=True; Encrypt=False; TrustServerCertificate=True; MultipleActiveResultSets=True;";
        }

        private static bool CanConnectToSql(string connectionString)
        {
            try
            {
                var testBuilder = new Microsoft.Data.SqlClient.SqlConnectionStringBuilder(connectionString)
                {
                    ConnectTimeout = 4
                };
                if (string.IsNullOrWhiteSpace(testBuilder.InitialCatalog))
                {
                    testBuilder.InitialCatalog = "master";
                }
                using var conn = new Microsoft.Data.SqlClient.SqlConnection(testBuilder.ConnectionString);
                conn.Open();
                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}
