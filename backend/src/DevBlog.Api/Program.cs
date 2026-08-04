using System.Text;
using DevBlog.Api.Data;
using DevBlog.Api.Endpoints;
using DevBlog.Api.Repositories;
using DevBlog.Api.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

// 1. DbContext
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection")));

// 2. CORS — TODO: restrict in production
builder.Services.AddCors(options =>
    options.AddDefaultPolicy(policy =>
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader()));

// 3. JWT Authentication
var jwtSecret = "devblog-super-secret-key-2024-dev"; // TODO: move to config
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret)),
            ValidateIssuer = false,
            ValidateAudience = false
        };
    });

// 4. Authorization
builder.Services.AddAuthorization();

// 5. OpenAPI
builder.Services.AddOpenApi();

// 6. Repositories & Services
builder.Services.AddScoped<IPostRepository, PostRepository>();
builder.Services.AddScoped<IPostService, PostService>();

var app = builder.Build();

// 7. Apply migrations and seed
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.Migrate();
    DataSeeder.Seed(db);
}

app.UseCors();
app.UseAuthentication();
app.UseAuthorization();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

PostsEndpoint.Map(app);
CommentsEndpoint.Map(app);
AuthEndpoint.Map(app);

app.Run();
