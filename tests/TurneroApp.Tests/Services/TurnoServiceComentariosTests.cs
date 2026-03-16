using FluentAssertions;
using Moq;
using Xunit;
using TurneroApp.Application.Interfaces;
using TurneroApp.Application.Services;
using TurneroApp.Domain.Entities;
using TurneroApp.Tests.Helpers;

namespace TurneroApp.Tests.Services;

public class TurnoServiceComentariosTests
{
    private readonly Mock<ITurnoRepository>           _turnoRepo      = new();
    private readonly Mock<IServicioRepository>        _servicioRepo   = new();
    private readonly Mock<IClienteRepository>         _clienteRepo    = new();
    private readonly Mock<ISucursalRepository>        _sucursalRepo   = new();
    private readonly Mock<IEmailService>              _emailService   = new();
    private readonly Mock<IComentarioTurnoRepository> _comentarioRepo = new();
    private readonly TurnoService                     _sut;

    public TurnoServiceComentariosTests()
    {
        _sut = new TurnoService(
            _turnoRepo.Object,
            _servicioRepo.Object,
            _clienteRepo.Object,
            _sucursalRepo.Object,
            _emailService.Object,
            _comentarioRepo.Object,
            "http://localhost:3000");
    }

    // ── CrearComentario ────────────────────────────────────────────────────────

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task CrearComentario_TextoVacio_LanzaException(string texto)
    {
        await _sut.Invoking(s => s.CrearComentarioAsync(Guid.NewGuid(), Guid.NewGuid(), texto))
            .Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task CrearComentario_TurnoNoExiste_LanzaException()
    {
        _turnoRepo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>()))
            .ReturnsAsync((Turno?)null);

        await _sut.Invoking(s => s.CrearComentarioAsync(Guid.NewGuid(), Guid.NewGuid(), "texto válido"))
            .Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task CrearComentario_TurnoExiste_CreaNuevoComentario()
    {
        var turno  = TurnoTestFactory.CrearTurnoConRelaciones();
        var autorId = Guid.NewGuid();

        _turnoRepo.Setup(r => r.GetByIdAsync(turno.Id)).ReturnsAsync(turno);
        _comentarioRepo.Setup(r => r.AddAsync(It.IsAny<ComentarioTurno>()))
            .Returns(Task.CompletedTask);
        _comentarioRepo.Setup(r => r.SaveChangesAsync()).Returns(Task.CompletedTask);
        _comentarioRepo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>()))
            .ReturnsAsync((Guid id) => new ComentarioTurno
            {
                Id             = id,
                TurnoId        = turno.Id,
                AutorUsuarioId = autorId,
                Texto          = "texto válido",
                Fecha          = DateTime.UtcNow
            });

        var result = await _sut.CrearComentarioAsync(turno.Id, autorId, "texto válido");

        result.Should().NotBeNull();
        result.TurnoId.Should().Be(turno.Id);
    }

    // ── EliminarComentario ─────────────────────────────────────────────────────

    [Fact]
    public async Task EliminarComentario_DeOtroAutor_LanzaException()
    {
        var autorId   = Guid.NewGuid();
        var otroAutor = Guid.NewGuid();
        var comentario = new ComentarioTurno
        {
            Id             = Guid.NewGuid(),
            TurnoId        = Guid.NewGuid(),
            AutorUsuarioId = otroAutor,
            Texto          = "texto"
        };

        _comentarioRepo.Setup(r => r.GetByIdAsync(comentario.Id))
            .ReturnsAsync(comentario);

        await _sut.Invoking(s => s.EliminarComentarioAsync(comentario.Id, autorId))
            .Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task EliminarComentario_NoExiste_RetornaFalse()
    {
        _comentarioRepo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>()))
            .ReturnsAsync((ComentarioTurno?)null);

        var result = await _sut.EliminarComentarioAsync(Guid.NewGuid(), Guid.NewGuid());

        result.Should().BeFalse();
    }
}
