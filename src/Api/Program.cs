using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using TurneroApp.Api.Hubs;
using TurneroApp.Api.Services;
using TurneroApp.Application.Interfaces;
using TurneroApp.Application.Services;
using TurneroApp.Infrastructure.Data;
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
builder.Services.AddScoped<IHorarioLaboralRepository, HorarioLaboralRepository>();
builder.Services.AddScoped<IUsuarioServicioRepository, UsuarioServicioRepository>();

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
    return new TurnoService(turnoRepo, servicioRepo, clienteRepo, sucursalRepo, emailService, comentarioRepo, frontendUrl, notificaciones, horarioRepo, usuarioServicioRepo);
});
builder.Services.AddScoped<CajaService>();
builder.Services.AddScoped<SuperAdminService>();
builder.Services.AddScoped<AjustesService>();
builder.Services.AddScoped<IAdjuntoService, CloudinaryService>();
builder.Services.AddScoped<AdjuntoTurnoService>();
builder.Services.AddScoped<PublicoService>(sp => new PublicoService(
    sp.GetRequiredService<ISucursalRepository>(),
    sp.GetRequiredService<IServicioRepository>(),
    sp.GetRequiredService<ITurnoRepository>(),
    sp.GetRequiredService<IUsuarioRepository>(),
    sp.GetRequiredService<IHorarioLaboralRepository>(),
    sp.GetRequiredService<IUsuarioServicioRepository>()));
builder.Services.AddScoped<InvitacionService>();
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
builder.Services.AddScoped<AuthService>(sp =>
{
    var repo    = sp.GetRequiredService<IUsuarioRepository>();
    var saEmail = builder.Configuration["SuperAdmin:Email"] ?? string.Empty;
    var saPass  = builder.Configuration["SuperAdmin:Password"] ?? string.Empty;
    return new AuthService(repo, saEmail, saPass);
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
    var sucursalRepo = sp.GetRequiredService<ISucursalRepository>();
    var emailService = sp.GetRequiredService<IEmailService>();
    var frontendUrl  = builder.Configuration["App:FrontendUrl"] ?? "http://localhost:5173";
    return new UsuarioService(usuarioRepo, sucursalRepo, emailService, frontendUrl,
        sp.GetRequiredService<IHorarioLaboralRepository>(),
        sp.GetRequiredService<IUsuarioServicioRepository>());
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
