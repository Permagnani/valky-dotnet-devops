/* =====================================================================
   Valky - script-bd.sql
   DDL dos objetos de banco (Azure SQL Database).
   Idempotente: pode ser executado várias vezes (a Release executa a cada deploy).
   ===================================================================== */

IF OBJECT_ID(N'dbo.Alertas', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Alertas (
        Id            INT IDENTITY(1,1) NOT NULL,
        CodigoBo      NVARCHAR(30)      NOT NULL,
        Latitude      DECIMAL(9,6)      NOT NULL,
        Longitude     DECIMAL(9,6)      NOT NULL,
        Status        NVARCHAR(20)      NOT NULL CONSTRAINT DF_Alertas_Status       DEFAULT N'ABERTO',
        CriadoEm      DATETIME2         NOT NULL CONSTRAINT DF_Alertas_CriadoEm     DEFAULT SYSUTCDATETIME(),
        AtualizadoEm  DATETIME2         NOT NULL CONSTRAINT DF_Alertas_AtualizadoEm DEFAULT SYSUTCDATETIME(),
        CONSTRAINT PK_Alertas PRIMARY KEY (Id),
        CONSTRAINT CK_Alertas_Status    CHECK (Status IN (N'ABERTO', N'VIATURA_A_CAMINHO', N'NO_LOCAL', N'ENCERRADO')),
        CONSTRAINT CK_Alertas_Latitude  CHECK (Latitude  BETWEEN -90  AND 90),
        CONSTRAINT CK_Alertas_Longitude CHECK (Longitude BETWEEN -180 AND 180)
    );
END;
GO

IF OBJECT_ID(N'dbo.AtualizacoesStatus', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.AtualizacoesStatus (
        Id           INT IDENTITY(1,1) NOT NULL,
        AlertaId     INT               NOT NULL,
        Status       NVARCHAR(20)      NOT NULL,
        Observacao   NVARCHAR(500)     NULL,
        Responsavel  NVARCHAR(100)     NOT NULL,
        CriadoEm     DATETIME2         NOT NULL CONSTRAINT DF_AtualizacoesStatus_CriadoEm DEFAULT SYSUTCDATETIME(),
        CONSTRAINT PK_AtualizacoesStatus PRIMARY KEY (Id),
        CONSTRAINT FK_AtualizacoesStatus_Alertas FOREIGN KEY (AlertaId)
            REFERENCES dbo.Alertas (Id) ON DELETE CASCADE,
        CONSTRAINT CK_AtualizacoesStatus_Status CHECK (Status IN (N'ABERTO', N'VIATURA_A_CAMINHO', N'NO_LOCAL', N'ENCERRADO'))
    );

    CREATE INDEX IX_AtualizacoesStatus_AlertaId ON dbo.AtualizacoesStatus (AlertaId);
END;
GO

/* Consultas usadas no vídeo para comprovar o CRUD direto no banco:
   SELECT * FROM dbo.Alertas ORDER BY Id DESC;
   SELECT * FROM dbo.AtualizacoesStatus ORDER BY Id DESC;
*/
