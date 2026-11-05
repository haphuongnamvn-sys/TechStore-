using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Web.Data;
using Web.Middlewares;
using Web.Services;
using Web.Services.Interfaces;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllersWithViews();
builder.Services.AddHttpContextAccessor();
builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromHours(4);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
});

// Add DbContext
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(connectionString));

// Add CORS
builder.Services.AddCors(options =>
    options.AddDefaultPolicy(p =>
        p.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader()));

// Add Services (Dependency Injection)
builder.Services.AddScoped<IAuthService,    AuthService>();
builder.Services.AddScoped<IProductService, ProductService>();
builder.Services.AddScoped<IOrderService,   OrderService>();
builder.Services.AddScoped<ICartService,    CartService>();
builder.Services.AddScoped<IPaymentService, PaymentService>();
builder.Services.AddScoped<IReviewService,  ReviewService>();
builder.Services.AddScoped<IAdminService,   AdminService>();
builder.Services.AddScoped<IChatService,    ChatService>();
builder.Services.AddScoped<IEmailService,   MockEmailService>();

// Configure JWT Authentication
var jwtSettings = builder.Configuration.GetSection("Jwt");
var key = Encoding.ASCII.GetBytes(jwtSettings["Key"]!);

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme    = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.RequireHttpsMetadata = false;
    options.SaveToken            = true;
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuerSigningKey = true,
        IssuerSigningKey         = new SymmetricSecurityKey(key),
        ValidateIssuer           = true,
        ValidIssuer              = jwtSettings["Issuer"],
        ValidateAudience         = true,
        ValidAudience            = jwtSettings["Audience"],
        ValidateLifetime         = true,
        ClockSkew                = TimeSpan.Zero,
        RoleClaimType            = System.Security.Claims.ClaimTypes.Role
    };
});

builder.Services.AddAuthorization();

var app = builder.Build();

// Add Global Exception Handling Middleware
app.UseMiddleware<ExceptionMiddleware>();

// Auto-create database & tables on startup for SQL Server
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.EnsureCreated();
    // Seed additional products & specs
    Web.Data.DataSeeder.SeedAdditionalProducts(db);
    // Phiên bản / màu sắc / tồn kho theo biến thể & ảnh theo màu
    Web.Data.ProductCatalogSeeder.Seed(db, app.Environment.WebRootPath ?? Path.Combine(app.Environment.ContentRootPath, "wwwroot"));
}

app.UseCors();
app.UseStaticFiles();

app.UseSession();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.MapControllers();

app.Run();