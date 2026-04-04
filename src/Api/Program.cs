using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using TurneroApp.Api.Hubs;
using TurneroApp.Api.Services;
using TurneroApp.Application.Interfaces;
using TurneroApp.Application.Services;
using TurneroApp.Infrastructure.Data;
using TurneroApp.Infrastructure.MultiTenant;
using TurneroApp.Infrastructure.Repositories;
using TurneroApp.Infrastructure.Services;
using TurneroApp.Infrastructure.Services.MercadoPago;
using Resend;

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

        // SignalR: leer el token JWT desde el query string ?access_token=...
        // (los WebSockets no soportan headers de Authorization)
        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                var accessToken = context.Request.Query["access_token"];
                var path        = context.HttpContext.Request.Path;
                if (!string.IsNullOrEmpty(accessToken) && path.StartsWithSegments("/hubs"))
                    context.Token = accessToken;
                return Task.CompletedTask;
            }
        };
    });

builder.Services.AddAuthorization();

// ─── Multi-tenant context ─────────────────────────────────────────────────────
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ITenantContext, TurneroApp.Api.Services.HttpTenantContext>();

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
builder.Services.AddScoped<IUsuarioSucursalRolRepository, UsuarioSucursalRolRepository>();
builder.Services.AddScoped<IHorarioLaboralRepository, HorarioLaboralRepository>();
builder.Services.AddScoped<IUsuarioServicioRepository, UsuarioServicioRepository>();
builder.Services.AddScoped<ISuscripcionRepository, SuscripcionRepository>();
builder.Services.AddScoped<IReportRepository, ReportRepository>();

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
    var notificaciones = sp.GetRequiredService<ITurnoNotificacionService>();
    var frontendUrl    = builder.Configuration["App:FrontendUrl"] ?? "http://localhost:5173";
    var horarioRepo         = sp.GetRequiredService<IHorarioLaboralRepository>();
    var usuarioServicioRepo = sp.GetRequiredService<IUsuarioServicioRepository>();
    var suscripcionRepo     = sp.GetRequiredService<ISuscripcionRepository>();
    var guard               = sp.GetRequiredService<SuscripcionGuard>();
    return new TurnoService(turnoRepo, servicioRepo, clienteRepo, sucursalRepo, emailService, comentarioRepo, frontendUrl, notificaciones, horarioRepo, usuarioServicioRepo, suscripcionRepo, guard);
});
builder.Services.AddScoped<CajaService>();
builder.Services.AddScoped<SuperAdminService>(sp => new SuperAdminService(
    sp.GetRequiredService<ISucursalRepository>(),
    sp.GetRequiredService<IUsuarioRepository>(),
    sp.GetRequiredService<IUsuarioSucursalRolRepository>(),
    sp.GetRequiredService<ICajaRepository>(),
    sp.GetRequiredService<ISuscripcionRepository>()));
builder.Services.AddScoped<AjustesService>();
builder.Services.AddScoped<OnboardingService>();
builder.Services.AddScoped<SuscripcionGuard>();
builder.Services.AddScoped<SuscripcionService>(sp => new SuscripcionService(
    sp.GetRequiredService<ISuscripcionRepository>(),
    sp.GetRequiredService<ISucursalRepository>()));
builder.Services.AddScoped<IAdjuntoService, CloudinaryService>();
builder.Services.AddScoped<AdjuntoTurnoService>();
builder.Services.AddScoped<PublicoService>(sp => new PublicoService(
    sp.GetRequiredService<ISucursalRepository>(),
    sp.GetRequiredService<IServicioRepository>(),
    sp.GetRequiredService<ITurnoRepository>(),
    sp.GetRequiredService<IUsuarioSucursalRolRepository>(),
    sp.GetRequiredService<IHorarioLaboralRepository>(),
    sp.GetRequiredService<IUsuarioServicioRepository>()));
builder.Services.AddScoped<InvitacionService>(sp => new InvitacionService(
    sp.GetRequiredService<IUsuarioSucursalRolRepository>(),
    sp.GetRequiredService<IUsuarioRepository>()));
builder.Services.AddScoped<RecuperacionContrasenaService>(sp => new RecuperacionContrasenaService(
    sp.GetRequiredService<IUsuarioRepository>(),
    sp.GetRequiredService<IEmailService>(),
    builder.Configuration["App:FrontendUrl"] ?? "http://localhost:5173"));
builder.Services.AddScoped<JwtService>();

// ─── MercadoPago ─────────────────────────────────────────────────────────────
builder.Services.Configure<MercadoPagoOptions>(
    builder.Configuration.GetSection(MercadoPagoOptions.SectionName));
builder.Services.AddScoped<IMercadoPagoGateway, MercadoPagoGateway>();
builder.Services.AddScoped<IPagoTurnoService>(sp => new PagoTurnoService(
    sp.GetRequiredService<ITurnoRepository>(),
    sp.GetRequiredService<ICajaRepository>(),
    sp.GetRequiredService<ISucursalRepository>(),
    sp.GetRequiredService<IMercadoPagoGateway>(),
    sp.GetRequiredService<IEmailService>(),
    sp.GetRequiredService<ILogger<PagoTurnoService>>(),
    builder.Configuration["App:FrontendUrl"] ?? "http://localhost:5173",
    sp.GetRequiredService<ITurnoNotificacionService>()
));
builder.Services.AddHostedService<TurnoAutoNoShowService>();
builder.Services.AddHostedService<RecordatoriosEmailBackgroundService>();
builder.Services.AddHostedService<TurnoPagoExpirationService>();
builder.Services.AddScoped<RegisterService>(sp => new RegisterService(
    sp.GetRequiredService<IUsuarioRepository>(),
    sp.GetRequiredService<IUsuarioSucursalRolRepository>(),
    sp.GetRequiredService<ISucursalRepository>(),
    sp.GetRequiredService<ICajaRepository>(),
    sp.GetRequiredService<ISuscripcionRepository>(),
    sp.GetRequiredService<IEmailService>(),
    builder.Configuration["App:FrontendUrl"] ?? "http://localhost:5173"));
builder.Services.AddScoped<AuthService>(sp =>
{
    var repo    = sp.GetRequiredService<IUsuarioRepository>();
    var usrRepo = sp.GetRequiredService<IUsuarioSucursalRolRepository>();
    var saEmail = builder.Configuration["SuperAdmin:Email"] ?? string.Empty;
    var saPass  = builder.Configuration["SuperAdmin:Password"] ?? string.Empty;
    return new AuthService(repo, usrRepo, saEmail, saPass);
});
var resendApiToken = builder.Configuration["Resend:ApiToken"];
if (!string.IsNullOrWhiteSpace(resendApiToken))
{
    builder.Services.AddOptions();
    builder.Services.AddHttpClient<ResendClient>();
    builder.Services.Configure<ResendClientOptions>(o => o.ApiToken = resendApiToken);
    builder.Services.AddTransient<IResend, ResendClient>();
    builder.Services.AddScoped<IEmailService, ResendEmailService>();
}
else
{
    builder.Services.AddScoped<IEmailService, ConsoleEmailService>();
}
builder.Services.AddScoped<UsuarioService>(sp =>
{
    var usuarioRepo  = sp.GetRequiredService<IUsuarioRepository>();
    var usrRepo      = sp.GetRequiredService<IUsuarioSucursalRolRepository>();
    var sucursalRepo = sp.GetRequiredService<ISucursalRepository>();
    var emailService = sp.GetRequiredService<IEmailService>();
    var frontendUrl  = builder.Configuration["App:FrontendUrl"] ?? "http://localhost:5173";
    var logger       = sp.GetRequiredService<ILogger<UsuarioService>>();
    var guard = sp.GetRequiredService<SuscripcionGuard>();
    return new UsuarioService(usuarioRepo, usrRepo, sucursalRepo, emailService, frontendUrl,
        sp.GetRequiredService<IHorarioLaboralRepository>(),
        sp.GetRequiredService<IUsuarioServicioRepository>(),
        sp.GetRequiredService<ITurnoRepository>(),
        sp.GetRequiredService<ISuscripcionRepository>(),
        guard,
        logger);
});

// ─── SignalR ─────────────────────────────────────────────────────────────────
builder.Services.AddSignalR();
builder.Services.AddScoped<ITurnoNotificacionService, TurnoNotificacionService>();

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

// ─── Startup config warnings ──────────────────────────────────────────────────
{
    var startupLogger = app.Logger;
    var fromEmail = app.Configuration["Resend:FromEmail"] ?? "";
    if (fromEmail == "delivered@resend.dev")
        startupLogger.LogWarning(
            "Resend:FromEmail está configurado como '{FromEmail}' (sandbox de Resend). " +
            "Los emails solo llegarán al dueño de la cuenta Resend. " +
            "Configurá un email de un dominio verificado para producción.",
            fromEmail);

    var frontendUrlCfg = app.Configuration["App:FrontendUrl"] ?? "";
    if (frontendUrlCfg.Contains("localhost"))
        startupLogger.LogWarning(
            "App:FrontendUrl está configurado como '{FrontendUrl}' (localhost). " +
            "Los links de invitación apuntarán a localhost. " +
            "Actualizá este valor para producción.",
            frontendUrlCfg);
}

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
app.MapHub<TurneroHub>("/hubs/turnero");

// ─── Auto-migrate on startup (development only) ───────────────────────────────
if (app.Environment.IsDevelopment())
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.Migrate();
}

app.Run();
