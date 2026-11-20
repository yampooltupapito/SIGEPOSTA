/* =====================================================================
   SIGEPOSTA - Esquema de referencia de la base de datos
   ---------------------------------------------------------------------
   * Es una REFERENCIA de las tablas y columnas que el programa espera
     (se dedujo leyendo el código de la capa Datos). Si usted ya creó las
     tablas a mano en SSMS, NO hace falta ejecutarlo: sirve para comparar.
   * Los tamaños y tipos son los que usa el código en sus consultas.
   * Este script NO fue ejecutado ni probado por el asistente que escribió
     la documentación; revíselo antes de usarlo.
   * Cada CREATE está protegido con IF OBJECT_ID(...) IS NULL para no
     tocar tablas que ya existan.
   * Ejecutar con la base SIGEPOSTA ya creada y seleccionada.
   ===================================================================== */

-- USE SIGEPOSTA;
-- GO

/* ---------- Rol ---------- */
IF OBJECT_ID('dbo.Rol', 'U') IS NULL
CREATE TABLE dbo.Rol (
    IdRol   INT           NOT NULL CONSTRAINT PK_Rol PRIMARY KEY,
    Nombre  NVARCHAR(30)  NOT NULL
);
GO

/* ---------- Usuario ---------- */
IF OBJECT_ID('dbo.Usuario', 'U') IS NULL
CREATE TABLE dbo.Usuario (
    IdUsuario         INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Usuario PRIMARY KEY,
    NombreUsuario     NVARCHAR(50)   NOT NULL CONSTRAINT UQ_Usuario_NombreUsuario UNIQUE,
    NombreCompleto    NVARCHAR(120)  NOT NULL,
    PasswordHash      VARBINARY(32)  NOT NULL,   -- PBKDF2-SHA256, 32 bytes
    Salt              VARBINARY(16)  NOT NULL,   -- 16 bytes aleatorios
    Iteraciones       INT            NOT NULL,   -- 100000 (se guarda por usuario)
    IdRol             INT            NOT NULL CONSTRAINT FK_Usuario_Rol REFERENCES dbo.Rol(IdRol),
    Activo            BIT            NOT NULL CONSTRAINT DF_Usuario_Activo DEFAULT (1),
    IntentosFallidos  INT            NOT NULL CONSTRAINT DF_Usuario_Intentos DEFAULT (0),
    BloqueadoHasta    DATETIME2      NULL,
    FechaCreacion     DATETIME2      NOT NULL CONSTRAINT DF_Usuario_Fecha DEFAULT (SYSDATETIME())
);
GO

/* ---------- Posta ---------- */
IF OBJECT_ID('dbo.Posta', 'U') IS NULL
CREATE TABLE dbo.Posta (
    IdPosta    INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Posta PRIMARY KEY,
    Nombre     NVARCHAR(100) NOT NULL,
    Distrito   NVARCHAR(100) NULL,
    Categoria  NVARCHAR(20)  NULL,
    Activo     BIT           NOT NULL CONSTRAINT DF_Posta_Activo DEFAULT (1)
);
GO

/* ---------- Paciente ---------- */
IF OBJECT_ID('dbo.Paciente', 'U') IS NULL
CREATE TABLE dbo.Paciente (
    IdPaciente       INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Paciente PRIMARY KEY,
    DniCifrado       VARBINARY(256) NOT NULL,                 -- AES-256: IV (16) + datos
    DniHash          CHAR(64)       NOT NULL CONSTRAINT UQ_Paciente_DniHash UNIQUE,  -- HMAC-SHA256 en hex
    Nombres          NVARCHAR(80)   NOT NULL,
    Apellidos        NVARCHAR(80)   NOT NULL,
    FechaNacimiento  DATE           NOT NULL,
    Telefono         NVARCHAR(20)   NULL,
    Direccion        NVARCHAR(150)  NULL,
    IdPosta          INT            NOT NULL CONSTRAINT FK_Paciente_Posta REFERENCES dbo.Posta(IdPosta),
    Activo           BIT            NOT NULL CONSTRAINT DF_Paciente_Activo DEFAULT (1),
    FechaRegistro    DATETIME2      NOT NULL CONSTRAINT DF_Paciente_Fecha DEFAULT (SYSDATETIME())
);
GO

/* ---------- HistoriaClinica (una por paciente) ---------- */
IF OBJECT_ID('dbo.HistoriaClinica', 'U') IS NULL
CREATE TABLE dbo.HistoriaClinica (
    IdHistoria      INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_HistoriaClinica PRIMARY KEY,
    NumeroHistoria  NVARCHAR(20) NOT NULL CONSTRAINT UQ_Historia_Numero UNIQUE,      -- HC-000001
    IdPaciente      INT          NOT NULL CONSTRAINT UQ_Historia_Paciente UNIQUE
                                 CONSTRAINT FK_Historia_Paciente REFERENCES dbo.Paciente(IdPaciente),
    FechaApertura   DATETIME2    NOT NULL CONSTRAINT DF_Historia_Fecha DEFAULT (SYSDATETIME())
);
GO

/* ---------- Medicamento (antes que Atencion, porque Atencion lo referencia) ---------- */
IF OBJECT_ID('dbo.Medicamento', 'U') IS NULL
CREATE TABLE dbo.Medicamento (
    IdMedicamento     INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Medicamento PRIMARY KEY,
    Nombre            NVARCHAR(100) NOT NULL,
    Presentacion      NVARCHAR(60)  NULL,
    IdPosta           INT           NOT NULL CONSTRAINT FK_Medicamento_Posta REFERENCES dbo.Posta(IdPosta),
    StockActual       INT           NOT NULL CONSTRAINT CK_Medicamento_Stock CHECK (StockActual >= 0),
    StockMinimo       INT           NOT NULL CONSTRAINT DF_Medicamento_Minimo DEFAULT (0),
    FechaVencimiento  DATE          NOT NULL,
    Activo            BIT           NOT NULL CONSTRAINT DF_Medicamento_Activo DEFAULT (1)
);
GO

/* ---------- Atencion ---------- */
IF OBJECT_ID('dbo.Atencion', 'U') IS NULL
CREATE TABLE dbo.Atencion (
    IdAtencion          INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Atencion PRIMARY KEY,
    IdHistoria          INT            NOT NULL CONSTRAINT FK_Atencion_Historia REFERENCES dbo.HistoriaClinica(IdHistoria),
    Fecha               DATETIME2      NOT NULL CONSTRAINT DF_Atencion_Fecha DEFAULT (SYSDATETIME()),
    Motivo              NVARCHAR(200)  NOT NULL,
    DiagnosticoCifrado  VARBINARY(MAX) NOT NULL,              -- AES-256: IV (16) + datos
    IdUsuario           INT            NOT NULL CONSTRAINT FK_Atencion_Usuario REFERENCES dbo.Usuario(IdUsuario),
    -- Receta opcional (etapa 7): ambas NULL, o medicamento y cantidad > 0.
    IdMedicamento       INT            NULL CONSTRAINT FK_Atencion_Medicamento REFERENCES dbo.Medicamento(IdMedicamento),
    CantidadRecetada    INT            NULL,
    CONSTRAINT CK_Atencion_Receta CHECK (
        (IdMedicamento IS NULL AND CantidadRecetada IS NULL)
        OR (IdMedicamento IS NOT NULL AND CantidadRecetada > 0))
);
GO

/* ---------- MovimientoInventario (kardex) ---------- */
IF OBJECT_ID('dbo.MovimientoInventario', 'U') IS NULL
CREATE TABLE dbo.MovimientoInventario (
    IdMovimiento   INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Movimiento PRIMARY KEY,
    IdMedicamento  INT           NOT NULL CONSTRAINT FK_Mov_Medicamento REFERENCES dbo.Medicamento(IdMedicamento),
    Tipo           NVARCHAR(10)  NOT NULL CONSTRAINT CK_Mov_Tipo CHECK (Tipo IN (N'Ingreso', N'Salida')),
    Cantidad       INT           NOT NULL CONSTRAINT CK_Mov_Cantidad CHECK (Cantidad > 0),
    Fecha          DATETIME2     NOT NULL CONSTRAINT DF_Mov_Fecha DEFAULT (SYSDATETIME()),
    IdUsuario      INT           NOT NULL CONSTRAINT FK_Mov_Usuario REFERENCES dbo.Usuario(IdUsuario),
    Motivo         NVARCHAR(200) NULL
);
GO

/* ---------- Datos iniciales ---------- */
-- Los valores de IdRol DEBEN coincidir con el enum Rol del programa (1, 2, 3).
IF NOT EXISTS (SELECT 1 FROM dbo.Rol)
    INSERT INTO dbo.Rol (IdRol, Nombre) VALUES (1, N'Administrador'), (2, N'Medico'), (3, N'Farmacia');
GO

-- Hace falta al menos una posta para poder registrar pacientes y medicamentos.
-- Cambie los datos de ejemplo por los de su posta.
IF NOT EXISTS (SELECT 1 FROM dbo.Posta)
    INSERT INTO dbo.Posta (Nombre, Distrito, Categoria, Activo)
    VALUES (N'Posta de Salud (ejemplo)', N'Ayacucho', N'I-2', 1);
GO

-- NO se inserta ningún usuario: el primer administrador se crea desde el programa
-- (ventana "Administrador inicial"), así no existe ninguna contraseña predeterminada.
