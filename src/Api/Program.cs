using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using TurneroApp.Api.Services;
using TurneroApp.Application.Interfaces;
using TurneroApp.Application.Services;
using TurneroApp.Infrastructure.Data;
using TurneroApp.Infrastructure.Repositories;
using TurneroApp.Infrastructure.Services;

var builder = WebApplication.CreateBuilder(args);

// ─── JWT Authentication ───────────────────────────────────────────────────────
var jwtSecret = builder.Configuration["Jwt:Secret"]
    ?? throw new InvalidOperationException("Jwt:Secret no configurado.");

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer           = true,
            ValidateAudience         = true,
            ValidateLifetime         = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer              = builder.Configuration["Jwt:Issuer"],
            ValidAudience            = builder.Configuration["Jwt:Audience"],
            IssuerSigningKey         = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret)),
        };
    });

builder.Services.AddAuthorization();

// ─── EF Core / PostgreSQL ────────────────────────────────────────────────────
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

// ─── Repositories ────────────────────────────────────────────────────────────
builder.Services.AddScoped(typeof(IGenericRepository<>), typeof(GenericRepository<>));
builder.Services.AddScoped<ISucursalRepository, SucursalRepository>();
builder.Services.AddScoped<IServicioRepository, ServicioRepository>();
builder.Services.AddScoped<IClienteRepository, ClienteRepository>();
builder.Services.AddScoped<ITurnoRepository, TurnoRepository>();
builder.Services.AddScoped<IComentarioTurnoRepository, ComentarioTurnoRepository>();
builder.Services.AddScoped<IAdjuntoTurnoRepository, AdjuntoTurnoRepository>();
builder.Services.AddScoped<ICajaRepository, CajaRepository>();
builder.Services.AddScoped<IUsuarioRepository, UsuarioRepository>();

// ─── Application Services ────────────────────────────────────────────────────
builder.Services.AddScoped<SucursalService>();
builder.Services.AddScoped<ServicioService>();
builder.Services.AddScoped<ClienteService>();
builder.Services.AddScoped<TurnoService>(sp =>
{
    var turnoRepo      = sp.GetRequiredService<ITurnoRepository>();
    var servicioRepo   = sp.GetRequiredService<IServicioRepository>();
    var clienteRepo    = sp.GetRequiredService<IClienteRepository>();
    var sucursalRepo   = sp.GetRequiredService<ISucursalRepository>();
    var emailService   = sp.GetRequiredService<IEmailService>();
    var comentarioRepo = sp.GetRequiredService<IComentarioTurnoRepository>();
    var frontendUrl    = builder.Configuration["App:FrontendUrl"] ?? "http://localhost:5173";
    return new TurnoService(turnoRepo, servicioRepo, clienteRepo, sucursalRepo, emailService, comentarioRepo, frontendUrl);
});
builder.Services.AddScoped<CajaService>();
builder.Services.AddScoped<SuperAdminService>();
builder.Services.AddScoped<AjustesService>();
builder.Services.AddScoped<IAdjuntoService, CloudinaryService>();
builder.Services.AddScoped<AdjuntoTurnoService>();
builder.Services.AddScoped<PublicoService>();
builder.Services.AddScoped<InvitacionService>();
builder.Services.AddScoped<JwtService>();
builder.Services.AddHostedService<TurnoAutoNoShowService>();
builder.Services.AddHostedService<RecordatoriosEmailBackgroundService>();
builder.Services.AddScoped<AuthService>(sp =>
{
    var repo    = sp.GetRequiredService<IUsuarioRepository>();
    var saEmail = builder.Configuration["SuperAdmin:Email"] ?? string.Empty;
    var saPass  = builder.Configuration["SuperAdmin:Password"] ?? string.Empty;
    return new AuthService(repo, saEmail, saPass);
});
var smtpConfigured = !string.IsNullOrWhiteSpace(builder.Configuration["Smtp:Username"]);
if (smtpConfigured)
    builder.Services.AddScoped<IEmailService, EmailService>();
else
    builder.Services.AddScoped<IEmailService, ConsoleEmailService>();
builder.Services.AddScoped<UsuarioService>(sp =>
{
    var usuarioRepo  = sp.GetRequiredService<IUsuarioRepository>();
    var sucursalRepo = sp.GetRequiredService<ISucursalRepository>();
    var emailService = sp.GetRequiredService<IEmailService>();
    var frontendUrl  = builder.Configuration["App:FrontendUrl"] ?? "http://localhost:5173";
    return new UsuarioService(usuarioRepo, sucursalRepo, emailService, frontendUrl);
});

// ─── API & Swagger ───────────────────────────────────────────────────────────
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(
            new System.Text.Json.Serialization.JsonStringEnumConverter());
    });

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new Microsoft.OpenApi.Models.OpenApiInfo
    {
        Title = "TurneroApp API",
        Version = "v1",
        Description = "API de gestión de turnos para salones de belleza y barberías."
    });

    // JWT Bearer en Swagger UI
    c.AddSecurityDefinition("Bearer", new Microsoft.OpenApi.Models.OpenApiSecurityScheme
    {
        Name         = "Authorization",
        Type         = Microsoft.OpenApi.Models.SecuritySchemeType.Http,
        Scheme       = "bearer",
        BearerFormat = "JWT",
        In           = Microsoft.OpenApi.Models.ParameterLocation.Header,
        Description  = "Ingresá el token JWT (sin el prefijo 'Bearer ').",
    });

    c.AddSecurityRequirement(new Microsoft.OpenApi.Models.OpenApiSecurityRequirement
    {
        {
            new Microsoft.OpenApi.Models.OpenApiSecurityScheme
            {
                Reference = new Microsoft.OpenApi.Models.OpenApiReference
                {
                    Type = Microsoft.OpenApi.Models.ReferenceType.SecurityScheme,
                    Id   = "Bearer",
                },
            },
            Array.Empty<string>()
        }
    });
});

// ─── CORS ────────────────────────────────────────────────────────────────────
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });

    options.AddPolicy("AllowFrontend", policy =>
    {
        policy.WithOrigins(
                "http://localhost:3000",
                "http://localhost:5173",
                "https://turneroapp.com")
              .AllowAnyMethod()
              .AllowAnyHeader()
              .AllowCredentials();
    });
});

var app = builder.Build();

// ─── Middleware Pipeline ──────────────────────────────────────────────────────
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();

    app.UseCors("AllowAll");
}
else
{
    app.UseHttpsRedirection();
    app.UseCors("AllowFrontend");
}

app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

// ─── Auto-migrate on startup (development only) ───────────────────────────────
if (app.Environment.IsDevelopment())
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.Migrate();
}

app.Run();
