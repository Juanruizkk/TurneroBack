using FluentAssertions;
using Moq;
using TurneroApp.Application.Interfaces;
using TurneroApp.Application.Services;
using TurneroApp.Domain.Entities;
using Xunit;

namespace TurneroApp.Tests.Services;

public class AdjuntoTurnoServiceTests
{
    private readonly Mock<IAdjuntoTurnoRepository> _repo;
    private readonly Mock<IAdjuntoService>         _cloudinary;
    private readonly AdjuntoTurnoService           _sut;

    public AdjuntoTurnoServiceTests()
    {
        _repo       = new Mock<IAdjuntoTurnoRepository>();
        _cloudinary = new Mock<IAdjuntoService>();
        _sut        = new AdjuntoTurnoService(_repo.Object, _cloudinary.Object);
    }

    // ─── ListarAsync ─────────────────────────────────────────────────────────

    [Fact]
    public async Task Listar_SinAdjuntos_RetornaListaVacia()
    {
        // Arrange
        var turnoId = Guid.NewGuid();
        _repo.Setup(r => r.GetByTurnoIdAsync(turnoId))
             .ReturnsAsync(new List<AdjuntoTurno>());

        // Act
        var result = await _sut.ListarAsync(turnoId);

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task Listar_ConAdjuntos_MapeoEsCorrecto()
    {
        // Arrange
        var turnoId  = Guid.NewGuid();
        var adjunto  = new AdjuntoTurno
        {
            Id            = Guid.NewGuid(),
            TurnoId       = turnoId,
            Url           = "https://res.cloudinary.com/demo/image/upload/foto.jpg",
            PublicId      = "TurneroApp/Adjuntos/foto_abc",
            NombreArchivo = "foto.jpg",
            TipoRecurso   = "image",
            Fecha         = DateTime.UtcNow,
        };

        _repo.Setup(r => r.GetByTurnoIdAsync(turnoId))
             .ReturnsAsync(new List<AdjuntoTurno> { adjunto });

        // Act
        var result = await _sut.ListarAsync(turnoId);

        // Assert
        result.Should().HaveCount(1);
        var dto = result[0];
        dto.Id.Should().Be(adjunto.Id);
        dto.TurnoId.Should().Be(turnoId);
        dto.Url.Should().Be(adjunto.Url);
        dto.PublicId.Should().Be(adjunto.PublicId);
        dto.NombreArchivo.Should().Be(adjunto.NombreArchivo);
        dto.TipoRecurso.Should().Be("image");
    }

    // ─── SubirAsync ──────────────────────────────────────────────────────────

    [Fact]
    public async Task Subir_Imagen_RetornaDtoConDatosDeCloudinary()
    {
        // Arrange
        var turnoId  = Guid.NewGuid();
        var autorId  = Guid.NewGuid();
        var stream   = new MemoryStream(new byte[] { 1, 2, 3 });
        var fileName = "foto.jpg";
        var contentType = "image/jpeg";

        _cloudinary
            .Setup(c => c.SubirArchivoAsync(stream, fileName, contentType, It.IsAny<string?>()))
            .ReturnsAsync(("https://res.cloudinary.com/foto.jpg", "TurneroApp/Adjuntos/foto_abc", "image"));

        _repo.Setup(r => r.AddAsync(It.IsAny<AdjuntoTurno>())).Returns(Task.CompletedTask);
        _repo.Setup(r => r.SaveChangesAsync()).Returns(Task.CompletedTask);

        // Act
        var result = await _sut.SubirAsync(turnoId, autorId, stream, fileName, contentType);

        // Assert
        result.Url.Should().Be("https://res.cloudinary.com/foto.jpg");
        result.PublicId.Should().Be("TurneroApp/Adjuntos/foto_abc");
        result.TipoRecurso.Should().Be("image");
        result.NombreArchivo.Should().Be("foto.jpg");
        result.TurnoId.Should().Be(turnoId);
        result.Id.Should().NotBeEmpty();
    }

    [Fact]
    public async Task Subir_Pdf_TipoRecursoEsRaw()
    {
        // Arrange
        var stream   = new MemoryStream(new byte[] { 1, 2, 3 });
        var fileName = "informe.pdf";
        var contentType = "application/pdf";

        _cloudinary
            .Setup(c => c.SubirArchivoAsync(stream, fileName, contentType, It.IsAny<string?>()))
            .ReturnsAsync(("https://res.cloudinary.com/informe.pdf", "TurneroApp/Adjuntos/informe_xyz", "raw"));

        _repo.Setup(r => r.AddAsync(It.IsAny<AdjuntoTurno>())).Returns(Task.CompletedTask);
        _repo.Setup(r => r.SaveChangesAsync()).Returns(Task.CompletedTask);

        // Act
        var result = await _sut.SubirAsync(Guid.NewGuid(), Guid.NewGuid(), stream, fileName, contentType);

        // Assert
        result.TipoRecurso.Should().Be("raw");
        result.NombreArchivo.Should().Be("informe.pdf");
    }

    [Fact]
    public async Task Subir_PersisteDespuesDeCloudinary()
    {
        // Arrange — verificar el orden: primero Cloudinary, luego la DB
        var llamadas = new List<string>();

        _cloudinary
            .Setup(c => c.SubirArchivoAsync(It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>()))
            .Callback(() => llamadas.Add("cloudinary"))
            .ReturnsAsync(("https://url", "publicId", "image"));

        _repo.Setup(r => r.AddAsync(It.IsAny<AdjuntoTurno>()))
             .Callback<AdjuntoTurno>(_ => llamadas.Add("addAsync"))
             .Returns(Task.CompletedTask);

        _repo.Setup(r => r.SaveChangesAsync())
             .Callback(() => llamadas.Add("saveChanges"))
             .Returns(Task.CompletedTask);

        // Act
        await _sut.SubirAsync(Guid.NewGuid(), Guid.NewGuid(), new MemoryStream(), "f.jpg", "image/jpeg");

        // Assert — orden estricto: Cloudinary → AddAsync → SaveChanges
        llamadas.Should().ContainInOrder("cloudinary", "addAsync", "saveChanges");
    }

    [Fact]
    public async Task Subir_CloudinaryFalla_NoPersiste()
    {
        // Arrange
        _cloudinary
            .Setup(c => c.SubirArchivoAsync(It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>()))
            .ThrowsAsync(new Exception("Cloudinary no disponible"));

        // Act
        var act = async () => await _sut.SubirAsync(
            Guid.NewGuid(), Guid.NewGuid(), new MemoryStream(), "foto.jpg", "image/jpeg");

        // Assert — la excepción se propaga y la DB nunca es tocada
        await act.Should().ThrowAsync<Exception>();
        _repo.Verify(r => r.AddAsync(It.IsAny<AdjuntoTurno>()), Times.Never);
        _repo.Verify(r => r.SaveChangesAsync(),                  Times.Never);
    }

    // ─── EliminarAsync ───────────────────────────────────────────────────────

    [Fact]
    public async Task Eliminar_AdjuntoNoExiste_RetornaFalse()
    {
        // Arrange
        _repo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>()))
             .ReturnsAsync((AdjuntoTurno?)null);

        // Act
        var result = await _sut.EliminarAsync(Guid.NewGuid());

        // Assert
        result.Should().BeFalse();
        _cloudinary.Verify(c => c.EliminarArchivoAsync(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task Eliminar_AdjuntoExiste_RetornaTrue()
    {
        // Arrange
        var adjunto = new AdjuntoTurno
        {
            Id          = Guid.NewGuid(),
            PublicId    = "TurneroApp/Adjuntos/foto_abc",
            TipoRecurso = "image"
        };

        _repo.Setup(r => r.GetByIdAsync(adjunto.Id)).ReturnsAsync(adjunto);
        _cloudinary.Setup(c => c.EliminarArchivoAsync(adjunto.PublicId, adjunto.TipoRecurso))
                   .Returns(Task.CompletedTask);
        _repo.Setup(r => r.SaveChangesAsync()).Returns(Task.CompletedTask);

        // Act
        var result = await _sut.EliminarAsync(adjunto.Id);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task Eliminar_LlamaCloudinaryConPublicIdYTipoRecursoCorrectos()
    {
        // Arrange
        var adjunto = new AdjuntoTurno
        {
            Id          = Guid.NewGuid(),
            PublicId    = "TurneroApp/Adjuntos/informe_xyz",
            TipoRecurso = "raw"
        };

        _repo.Setup(r => r.GetByIdAsync(adjunto.Id)).ReturnsAsync(adjunto);
        _cloudinary.Setup(c => c.EliminarArchivoAsync(It.IsAny<string>(), It.IsAny<string>()))
                   .Returns(Task.CompletedTask);
        _repo.Setup(r => r.SaveChangesAsync()).Returns(Task.CompletedTask);

        // Act
        await _sut.EliminarAsync(adjunto.Id);

        // Assert — PublicId y TipoRecurso correctos enviados a Cloudinary
        _cloudinary.Verify(
            c => c.EliminarArchivoAsync("TurneroApp/Adjuntos/informe_xyz", "raw"),
            Times.Once);
    }

    [Fact]
    public async Task Eliminar_BorraDbDespuesDeCloudinary()
    {
        // Arrange — verificar el orden: primero Cloudinary, luego la DB
        var llamadas = new List<string>();
        var adjunto  = new AdjuntoTurno
        {
            Id          = Guid.NewGuid(),
            PublicId    = "TurneroApp/Adjuntos/foto",
            TipoRecurso = "image"
        };

        _repo.Setup(r => r.GetByIdAsync(adjunto.Id)).ReturnsAsync(adjunto);

        _cloudinary
            .Setup(c => c.EliminarArchivoAsync(It.IsAny<string>(), It.IsAny<string>()))
            .Callback(() => llamadas.Add("cloudinary"))
            .Returns(Task.CompletedTask);

        _repo.Setup(r => r.SaveChangesAsync())
             .Callback(() => llamadas.Add("saveChanges"))
             .Returns(Task.CompletedTask);

        // Act
        await _sut.EliminarAsync(adjunto.Id);

        // Assert — orden estricto: Cloudinary antes que DB
        llamadas.Should().ContainInOrder("cloudinary", "saveChanges");
    }

    [Fact]
    public async Task Eliminar_CloudinaryFalla_NoEliminaDeDb()
    {
        // Arrange
        var adjunto = new AdjuntoTurno
        {
            Id          = Guid.NewGuid(),
            PublicId    = "TurneroApp/Adjuntos/foto",
            TipoRecurso = "image"
        };

        _repo.Setup(r => r.GetByIdAsync(adjunto.Id)).ReturnsAsync(adjunto);
        _cloudinary
            .Setup(c => c.EliminarArchivoAsync(It.IsAny<string>(), It.IsAny<string>()))
            .ThrowsAsync(new Exception("Cloudinary no disponible"));

        // Act
        var act = async () => await _sut.EliminarAsync(adjunto.Id);

        // Assert — la excepción se propaga y la DB no es modificada
        await act.Should().ThrowAsync<Exception>();
        _repo.Verify(r => r.Delete(It.IsAny<AdjuntoTurno>()), Times.Never);
        _repo.Verify(r => r.SaveChangesAsync(),                Times.Never);
    }
}
